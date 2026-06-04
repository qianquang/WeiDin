using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using WeiDin.Core.Interfaces;

namespace WeiDin.Infrastructure.Services;

/// <summary>
/// ONNX 运行时嵌入服务 - 使用 BGE-small-zh-v1.5 模型生成向量嵌入
/// 实现了正确的 BERT WordPiece tokenizer，加载模型目录下的 vocab.txt
/// </summary>
public class OnnxEmbeddingService : IEmbeddingService, IDisposable
{
    private InferenceSession? _session;
    private readonly string _modelPath;
    private readonly string _modelDir;
    private readonly int _dimension;
    private readonly ILogger<OnnxEmbeddingService> _logger;
    private bool _disposed;

    // BERT 特殊 token IDs
    private const int PadTokenId = 0;
    private const int UnkTokenId = 100;
    private const int ClsTokenId = 101;
    private const int SepTokenId = 102;

    // 词表：token -> id
    private readonly Dictionary<string, int> _vocab = new();
    // 词表：id -> token（用于调试）
    private readonly Dictionary<int, string> _vocabReverse = new();
    private bool _vocabLoaded;

    /// <inheritdoc />
    public int Dimension => _dimension;

    public OnnxEmbeddingService(IConfiguration configuration, ILogger<OnnxEmbeddingService> logger)
    {
        _logger = logger;
        _dimension = 512; // BGE small zh v1.5 是 512 维

        // 1. 优先使用URL下载模式
        var modelUrl = configuration["VectorSearch:ModelUrl"];
        if (!string.IsNullOrEmpty(modelUrl))
        {
            _modelPath = LoadModelFromUrl(modelUrl).GetAwaiter().GetResult();
            _modelDir = Path.GetDirectoryName(_modelPath)!;
        }
        // 2. 使用本地文件
        else
        {
            var modelFolder = configuration["VectorSearch:ModelPath"] ?? "Models";
            _modelDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, modelFolder);
            _modelPath = Path.Combine(_modelDir, "bge-small-zh-v1.5.onnx");
        }

        // 加载词表
        LoadVocab();

        // 加载模型
        LoadModel();
    }

    /// <summary>
    /// 加载 BERT 词表 (vocab.txt)
    /// </summary>
    private void LoadVocab()
    {
        var vocabPath = Path.Combine(_modelDir, "vocab.txt");
        if (!File.Exists(vocabPath))
        {
            _logger.LogWarning(
                "vocab.txt not found at {VocabPath}. " +
                "Tokenizer will use fallback character-level mode. " +
                "Download vocab.txt from BGE-small-zh-v1.5 model.",
                vocabPath);
            return;
        }

        try
        {
            var lines = File.ReadAllLines(vocabPath);
            for (int i = 0; i < lines.Length; i++)
            {
                var token = lines[i].Trim();
                if (!string.IsNullOrEmpty(token))
                {
                    _vocab[token] = i;
                    _vocabReverse[i] = token;
                }
            }

            _vocabLoaded = true;
            _logger.LogInformation("Loaded vocab.txt with {Count} tokens", _vocab.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load vocab.txt from {VocabPath}", vocabPath);
        }
    }

    /// <summary>
    /// 从URL下载模型到本地缓存
    /// </summary>
    private async Task<string> LoadModelFromUrl(string modelUrl)
    {
        var cacheFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WeiDin", "Models");
        Directory.CreateDirectory(cacheFolder);

        var modelFileName = Path.GetFileName(new Uri(modelUrl).LocalPath);
        var localPath = Path.Combine(cacheFolder, modelFileName);

        if (File.Exists(localPath))
        {
            _logger.LogInformation("Using cached model from {LocalPath}", localPath);
            return localPath;
        }

        _logger.LogInformation("Downloading model from {ModelUrl}...", modelUrl);

        using var client = new HttpClient();
        client.Timeout = TimeSpan.FromMinutes(10);

        var response = await client.GetAsync(modelUrl);
        response.EnsureSuccessStatusCode();

        using var contentStream = await response.Content.ReadAsStreamAsync();
        using var fileStream = new FileStream(localPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);
        await contentStream.CopyToAsync(fileStream);

        _logger.LogInformation("Model downloaded to {LocalPath}", localPath);
        return localPath;
    }

    /// <summary>
    /// 加载 ONNX 模型
    /// </summary>
    private void LoadModel()
    {
        if (File.Exists(_modelPath))
        {
            try
            {
                var sessionOptions = new SessionOptions();
                sessionOptions.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;

                _session = new InferenceSession(_modelPath, sessionOptions);
                _logger.LogInformation("ONNX embedding model loaded from {ModelPath}", _modelPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load ONNX model from {ModelPath}", _modelPath);
            }
        }
        else
        {
            _logger.LogWarning(
                "ONNX model not found at {ModelPath}. " +
                "Embedding service will return zero vectors. " +
                "Configure VectorSearch:ModelUrl in appsettings.json to download automatically.",
                _modelPath);
        }
    }

    /// <inheritdoc />
    public async Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default)
    {
        var vectors = await EmbedBatchAsync(new[] { text }, cancellationToken);
        return vectors.FirstOrDefault() ?? new float[_dimension];
    }

    /// <inheritdoc />
    public async Task<float[][]> EmbedBatchAsync(IEnumerable<string> texts, CancellationToken cancellationToken = default)
    {
        var textsList = texts.ToList();

        if (_session == null)
        {
            _logger.LogWarning("ONNX session not initialized, returning zero vectors");
            return textsList.Select(_ => new float[_dimension]).ToArray();
        }

        if (textsList.Count == 0)
            return Array.Empty<float[]>();

        return await Task.Run(() =>
        {
            var results = new List<float[]>();

            foreach (var text in textsList)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    // BGE 模型建议在查询前加前缀
                    var processedText = text;
                    var inputIds = Tokenize(processedText);
                    var attentionMask = new long[inputIds.Length];
                    var tokenTypeIds = new long[inputIds.Length]; // segment_ids，单句全为 0

                    // attention mask: 非 PAD 位置为 1
                    for (int i = 0; i < inputIds.Length; i++)
                    {
                        attentionMask[i] = inputIds[i] == PadTokenId ? 0 : 1;
                    }

                    // 创建输入张量
                    var inputIdsTensor = new DenseTensor<long>(inputIds, new[] { 1, inputIds.Length });
                    var attentionMaskTensor = new DenseTensor<long>(attentionMask, new[] { 1, attentionMask.Length });
                    var tokenTypeIdsTensor = new DenseTensor<long>(tokenTypeIds, new[] { 1, tokenTypeIds.Length });

                    var inputs = new List<NamedOnnxValue>
                    {
                        NamedOnnxValue.CreateFromTensor("input_ids", inputIdsTensor),
                        NamedOnnxValue.CreateFromTensor("attention_mask", attentionMaskTensor),
                        NamedOnnxValue.CreateFromTensor("token_type_ids", tokenTypeIdsTensor)
                    };

                    // 运行推理
                    using var outputs = _session.Run(inputs);
                    var outputTensor = outputs.FirstOrDefault(v => v.Name == "last_hidden_state")
                        ?? outputs.First();

                    // Mean pooling + L2 归一化
                    var embeddings = MeanPooling(outputTensor.AsTensor<float>(), attentionMaskTensor);
                    results.Add(embeddings);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error embedding text: {Text}", text.Length > 50 ? text.Substring(0, 50) + "..." : text);
                    results.Add(new float[_dimension]);
                }
            }

            return results.ToArray();
        }, cancellationToken);
    }

    /// <summary>
    /// BERT WordPiece Tokenizer
    /// 支持中文字符级分词和英文 WordPiece 子词切分
    /// </summary>
    private long[] Tokenize(string text)
    {
        if (!_vocabLoaded)
        {
            return TokenizeFallback(text);
        }

        var tokenIds = new List<long> { ClsTokenId };

        // 预处理：按空格分词，然后对每部分做 WordPiece
        var words = PreTokenize(text);

        foreach (var word in words)
        {
            if (tokenIds.Count >= 510) break; // 留给 [CLS] 和 [SEP]

            // 中文字符：逐字符查词表
            if (IsChinese(word))
            {
                foreach (var c in word)
                {
                    if (tokenIds.Count >= 510) break;
                    var charStr = c.ToString();
                    if (_vocab.TryGetValue(charStr, out var id))
                    {
                        tokenIds.Add(id);
                    }
                    else
                    {
                        tokenIds.Add(UnkTokenId);
                    }
                }
            }
            // 英文/数字：尝试整词匹配，失败则 WordPiece
            else
            {
                if (_vocab.TryGetValue(word.ToLower(), out var id))
                {
                    tokenIds.Add(id);
                }
                else
                {
                    // WordPiece 子词切分
                    var subTokens = WordPieceSplit(word);
                    tokenIds.AddRange(subTokens);
                }
            }
        }

        tokenIds.Add(SepTokenId);

        // Padding 到固定长度 512
        var maxLength = 512;
        if (tokenIds.Count < maxLength)
        {
            tokenIds.AddRange(Enumerable.Repeat((long)PadTokenId, maxLength - tokenIds.Count));
        }
        else if (tokenIds.Count > maxLength)
        {
            tokenIds = tokenIds.Take(maxLength).ToList();
        }

        return tokenIds.ToArray();
    }

    /// <summary>
    /// 预分词：按空格和标点分割，保留中文字符为单独 token
    /// </summary>
    private static List<string> PreTokenize(string text)
    {
        var result = new List<string>();
        // 用正则按空格和标点分割，同时将中文字符单独分离
        var pattern = @"[\x{4e00}-\x{9fff}\x{3400}-\x{4dbf}]|[a-zA-Z]+|[0-9]+|[^\s\w\x{4e00}-\x{9fff}\x{3400}-\x{4dbf}]+";
        var matches = Regex.Matches(text, pattern);

        foreach (Match match in matches)
        {
            result.Add(match.Value);
        }

        return result;
    }

    /// <summary>
    /// 判断是否为中文字符
    /// </summary>
    private static bool IsChinese(string text)
    {
        foreach (var c in text)
        {
            if (c >= 0x4e00 && c <= 0x9fff) return true;
            if (c >= 0x3400 && c <= 0x4dbf) return true;
        }
        return false;
    }

    /// <summary>
    /// WordPiece 子词切分（贪心最长匹配）
    /// </summary>
    private List<long> WordPieceSplit(string word)
    {
        var result = new List<long>();
        var start = 0;

        while (start < word.Length)
        {
            var end = word.Length;
            bool found = false;

            while (start < end)
            {
                var substr = word.Substring(start, end - start);
                var lookup = start > 0 ? "##" + substr.ToLower() : substr.ToLower();

                if (_vocab.TryGetValue(lookup, out var id))
                {
                    result.Add(id);
                    start = end;
                    found = true;
                    break;
                }

                end--;
            }

            if (!found)
            {
                result.Add(UnkTokenId);
                start++;
            }
        }

        return result;
    }

    /// <summary>
    /// 回退 tokenizer（无词表时使用字符编码）
    /// </summary>
    private static long[] TokenizeFallback(string text)
    {
        var tokens = new List<long> { ClsTokenId };

        foreach (var c in text)
        {
            if (tokens.Count >= 510) break;
            // 简单映射：使用字符的 Unicode 值，但限制在合理范围内
            tokens.Add(c < 21128 ? c : UnkTokenId);
        }

        tokens.Add(SepTokenId);

        // Padding
        while (tokens.Count < 512)
        {
            tokens.Add(PadTokenId);
        }

        return tokens.ToArray();
    }

    /// <summary>
    /// Mean pooling + L2 归一化
    /// </summary>
    private static float[] MeanPooling(Tensor<float> embeddings, Tensor<long> attentionMask)
    {
        var batchSize = embeddings.Dimensions[0];
        var seqLength = embeddings.Dimensions[1];
        var hiddenSize = embeddings.Dimensions[2];
        var result = new float[hiddenSize];

        for (int b = 0; b < batchSize; b++)
        {
            float sum = 0;
            for (int i = 0; i < seqLength; i++)
            {
                if (attentionMask[b, i] > 0)
                {
                    for (int j = 0; j < hiddenSize; j++)
                    {
                        result[j] += embeddings[b, i, j];
                    }
                    sum += 1;
                }
            }

            if (sum > 0)
            {
                for (int j = 0; j < hiddenSize; j++)
                {
                    result[j] /= sum;
                }
            }
        }

        // L2 normalize
        float norm = 0;
        foreach (var v in result)
            norm += v * v;
        norm = (float)Math.Sqrt(norm);

        if (norm > 0)
        {
            for (int i = 0; i < result.Length; i++)
                result[i] /= norm;
        }

        return result;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _session?.Dispose();
            _disposed = true;
        }
    }
}
