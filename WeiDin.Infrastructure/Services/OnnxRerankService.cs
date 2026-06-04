using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using WeiDin.Core.Interfaces;

namespace WeiDin.Infrastructure.Services;

/// <summary>
/// ONNX Rerank 精排服务
/// 使用交叉编码器模型 (如 bge-reranker-base) 对 query-document 对打相关性分
/// 与 Embedding 双塔模型不同，交叉编码器让 query 和 document 在注意力层充分交互，精度更高
/// </summary>
public class OnnxRerankService : IRerankService, IDisposable
{
    private InferenceSession? _session;
    private readonly string _modelPath;
    private readonly string _modelDir;
    private readonly ILogger<OnnxRerankService> _logger;
    private bool _disposed;

    // BERT 特殊 token IDs
    private const int PadTokenId = 0;
    private const int UnkTokenId = 100;
    private const int ClsTokenId = 101;
    private const int SepTokenId = 102;

    private readonly Dictionary<string, int> _vocab = new();
    private bool _vocabLoaded;

    public OnnxRerankService(IConfiguration configuration, ILogger<OnnxRerankService> logger)
    {
        _logger = logger;

        var modelFolder = configuration["Rerank:ModelPath"] ?? "Models";
        _modelDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, modelFolder);
        var modelName = configuration["Rerank:ModelName"] ?? "bge-reranker-base.onnx";
        _modelPath = Path.Combine(_modelDir, modelName);

        LoadVocab();
        LoadModel();
    }

    private void LoadVocab()
    {
        var vocabPath = Path.Combine(_modelDir, "vocab.txt");
        if (!File.Exists(vocabPath))
        {
            _logger.LogWarning("vocab.txt not found at {VocabPath}, rerank tokenizer will use fallback mode", vocabPath);
            return;
        }

        try
        {
            var lines = File.ReadAllLines(vocabPath);
            for (int i = 0; i < lines.Length; i++)
            {
                var token = lines[i].Trim();
                if (!string.IsNullOrEmpty(token))
                    _vocab[token] = i;
            }
            _vocabLoaded = true;
            _logger.LogInformation("Rerank service loaded vocab.txt with {Count} tokens", _vocab.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load vocab.txt for rerank service");
        }
    }

    private void LoadModel()
    {
        if (!File.Exists(_modelPath))
        {
            _logger.LogWarning(
                "Rerank model not found at {ModelPath}. " +
                "Reranking will be disabled. " +
                "Place bge-reranker-base.onnx in the Models directory.",
                _modelPath);
            return;
        }

        try
        {
            var options = new SessionOptions();
            options.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;
            _session = new InferenceSession(_modelPath, options);
            _logger.LogInformation("Rerank model loaded from {ModelPath}", _modelPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load rerank model from {ModelPath}", _modelPath);
        }
    }

    /// <inheritdoc />
    public async Task<float[]> ScoreAsync(string query, List<string> documents, CancellationToken cancellationToken = default)
    {
        if (_session == null)
        {
            _logger.LogWarning("Rerank session not initialized, returning zero scores");
            return documents.Select(_ => 0f).ToArray();
        }

        if (documents.Count == 0)
            return Array.Empty<float>();

        return await Task.Run(() =>
        {
            var scores = new float[documents.Count];

            for (int i = 0; i < documents.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    scores[i] = ScorePair(query, documents[i]);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error scoring document {Index}", i);
                    scores[i] = 0f;
                }
            }

            return scores;
        }, cancellationToken);
    }

    /// <summary>
    /// 对单个 query-document 对进行交叉编码打分
    /// </summary>
    private float ScorePair(string query, string document)
    {
        // 交叉编码器输入: [CLS] query tokens [SEP] document tokens [SEP]
        var inputIds = TokenizePair(query, document);
        var seqLen = inputIds.Length;

        var attentionMask = new long[seqLen];
        var tokenTypeIds = new long[seqLen];

        for (int i = 0; i < seqLen; i++)
        {
            attentionMask[i] = inputIds[i] == PadTokenId ? 0 : 1;
            // segment_ids: query 部分为 0，document 部分由 TokenizePair 设置
        }

        // 检测是否有 token_type_ids 输入（XLMRoberta 系列模型不使用）
        var hasTokenType = _session!.InputMetadata.ContainsKey("token_type_ids");

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input_ids",
                new DenseTensor<long>(inputIds, new[] { 1, seqLen })),
            NamedOnnxValue.CreateFromTensor("attention_mask",
                new DenseTensor<long>(attentionMask, new[] { 1, seqLen }))
        };

        if (hasTokenType)
        {
            inputs.Add(NamedOnnxValue.CreateFromTensor("token_type_ids",
                new DenseTensor<long>(tokenTypeIds, new[] { 1, seqLen })));
        }

        using var outputs = _session.Run(inputs);

        // 尝试取 logits 输出，否则取第一个输出
        var output = outputs.FirstOrDefault(v => v.Name == "logits") ?? outputs.First();
        var tensor = output.AsTensor<float>();

        // 输出形状:
        //   [1, 1]  → 直接 sigmoid
        //   [1, 2]  → softmax 取相关类
        //   [1]     → 直接 sigmoid
        var dims = tensor.Dimensions;

        if (dims.Length == 2 && dims[1] == 2)
        {
            // 二分类: softmax 取 index=1 (相关类)
            var neg = tensor[0, 0];
            var pos = tensor[0, 1];
            return Sigmoid(pos - neg);
        }
        else if (dims.Length == 2 && dims[1] == 1)
        {
            return Sigmoid(tensor[0, 0]);
        }
        else if (dims.Length == 1)
        {
            return Sigmoid(tensor[0]);
        }
        else
        {
            // 兜底：取缓冲区第一个值做 sigmoid
            var buffer = tensor.ToArray();
            return Sigmoid(buffer.Length > 0 ? buffer[0] : 0f);
        }
    }

    /// <summary>
    /// 交叉编码器 Tokenizer: [CLS] query [SEP] document [SEP] + padding
    /// query 和 document 拼接为单序列，用 token_type_ids 标记边界
    /// </summary>
    private long[] TokenizePair(string query, string document)
    {
        var maxLength = 512;

        if (!_vocabLoaded)
            return TokenizePairFallback(query, document, maxLength);

        var tokens = new List<long> { ClsTokenId };

        // Query 部分
        var queryTokens = TokenizeText(query);
        tokens.AddRange(queryTokens);
        tokens.Add(SepTokenId);

        // Document 部分
        var docTokens = TokenizeText(document);
        // 留 1 个位置给末尾 [SEP]
        var remaining = maxLength - tokens.Count - 1;
        if (docTokens.Count > remaining)
            docTokens = docTokens.Take(remaining).ToList();
        tokens.AddRange(docTokens);
        tokens.Add(SepTokenId);

        // Padding
        if (tokens.Count < maxLength)
            tokens.AddRange(Enumerable.Repeat((long)PadTokenId, maxLength - tokens.Count));

        return tokens.ToArray();
    }

    /// <summary>
    /// 将文本 tokenize 为 ID 列表（不含 [CLS]/[SEP]）
    /// </summary>
    private List<long> TokenizeText(string text)
    {
        var result = new List<long>();
        var words = PreTokenize(text);

        foreach (var word in words)
        {
            if (IsChinese(word))
            {
                foreach (var c in word)
                {
                    var charStr = c.ToString();
                    result.Add(_vocab.TryGetValue(charStr, out var id) ? id : UnkTokenId);
                }
            }
            else
            {
                if (_vocab.TryGetValue(word.ToLower(), out var id))
                {
                    result.Add(id);
                }
                else
                {
                    result.AddRange(WordPieceSplit(word));
                }
            }
        }

        return result;
    }

    private static List<string> PreTokenize(string text)
    {
        var result = new List<string>();
        var pattern = @"[\x{4e00}-\x{9fff}\x{3400}-\x{4dbf}]|[a-zA-Z]+|[0-9]+|[^\s\w\x{4e00}-\x{9fff}\x{3400}-\x{4dbf}]+";
        foreach (Match match in Regex.Matches(text, pattern))
            result.Add(match.Value);
        return result;
    }

    private static bool IsChinese(string text)
    {
        foreach (var c in text)
        {
            if (c >= 0x4e00 && c <= 0x9fff) return true;
            if (c >= 0x3400 && c <= 0x4dbf) return true;
        }
        return false;
    }

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

    private static long[] TokenizePairFallback(string query, string document, int maxLength)
    {
        var tokens = new List<long> { ClsTokenId };

        foreach (var c in query)
        {
            if (tokens.Count >= maxLength - 2) break;
            tokens.Add(c < 21128 ? c : UnkTokenId);
        }
        tokens.Add(SepTokenId);

        foreach (var c in document)
        {
            if (tokens.Count >= maxLength - 1) break;
            tokens.Add(c < 21128 ? c : UnkTokenId);
        }
        tokens.Add(SepTokenId);

        while (tokens.Count < maxLength)
            tokens.Add(PadTokenId);

        return tokens.ToArray();
    }

    private static float Sigmoid(float x)
    {
        return 1f / (1f + MathF.Exp(-x));
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
