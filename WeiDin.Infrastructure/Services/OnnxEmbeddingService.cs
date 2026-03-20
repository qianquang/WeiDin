using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using WeiDin.Core.Interfaces;

namespace WeiDin.Infrastructure.Services;

/// <summary>
/// ONNX 运行时嵌入服务 - 使用 BGE 模型生成向量嵌入
/// 支持多种模型加载方式：
/// 1. 本地文件 (默认)
/// 2. 启动时从URL下载
/// 3. 嵌入式资源
/// </summary>
public class OnnxEmbeddingService : IEmbeddingService, IDisposable
{
    private InferenceSession? _session;
    private string _modelPath;
    private readonly int _dimension;
    private readonly ILogger<OnnxEmbeddingService> _logger;
    private bool _disposed;

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
        }
        // 2. 使用本地文件
        else
        {
            var modelFolder = configuration["VectorSearch:ModelPath"] ?? "Models";
            _modelPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, modelFolder, "bge-small-zh-v1.5.onnx");
        }

        LoadModel();
    }

    /// <summary>
    /// 从URL下载模型到本地缓存
    /// </summary>
    private async Task<string> LoadModelFromUrl(string modelUrl)
    {
        // 缓存目录
        var cacheFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WeiDin", "Models");
        Directory.CreateDirectory(cacheFolder);

        var modelFileName = Path.GetFileName(new Uri(modelUrl).LocalPath);
        var localPath = Path.Combine(cacheFolder, modelFileName);

        // 检查本地是否已有模型
        if (File.Exists(localPath))
        {
            _logger.LogInformation("Using cached model from {LocalPath}", localPath);
            return localPath;
        }

        // 下载模型
        _logger.LogInformation("Downloading model from {ModelUrl}...", modelUrl);

        using var client = new HttpClient();
        client.Timeout = TimeSpan.FromMinutes(10);

        var response = await client.GetAsync(modelUrl);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? -1;
        var downloadedBytes = 0L;

        using (var contentStream = await response.Content.ReadAsStreamAsync())
        using (var fileStream = new FileStream(localPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
        {
            var buffer = new byte[8192];
            int bytesRead;
            while ((bytesRead = await contentStream.ReadAsync(buffer)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                downloadedBytes += bytesRead;

                if (totalBytes > 0)
                {
                    var progress = (double)downloadedBytes / totalBytes * 100;
                    _logger.LogDebug("Downloading model: {Progress:F1}%", progress);
                }
            }
        }

        _logger.LogInformation("Model downloaded to {LocalPath}", localPath);
        return localPath;
    }

    /// <summary>
    /// 加载模型
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
                    // Tokenize and prepare input
                    var inputIds = Tokenize(text);
                    var attentionMask = new long[inputIds.Length];
                    for (int i = 0; i < attentionMask.Length; i++)
                        attentionMask[i] = 1;

                    // Create input tensors
                    var inputIdsTensor = new DenseTensor<long>(inputIds, new[] { 1, inputIds.Length });
                    var attentionMaskTensor = new DenseTensor<long>(attentionMask, new[] { 1, attentionMask.Length });

                    var inputs = new[]
                    {
                        NamedOnnxValue.CreateFromTensor("input_ids", inputIdsTensor),
                        NamedOnnxValue.CreateFromTensor("attention_mask", attentionMaskTensor)
                    };

                    // Run inference
                    using var outputs = _session.Run(inputs);
                    var outputTensor = outputs.FirstOrDefault(v => v.Name == "last_hidden_state")
                        ?? outputs.First();

                    // Get embeddings (mean pooling)
                    var embeddings = MeanPooling(outputTensor.AsTensor<float>(), attentionMaskTensor);
                    results.Add(embeddings);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error embedding text: {Text}", text.Substring(0, Math.Min(50, text.Length)));
                    results.Add(new float[_dimension]);
                }
            }

            return results.ToArray();
        }, cancellationToken);
    }

    private static long[] Tokenize(string text)
    {
        // 简单的中文分词 (实际生产环境应使用专业分词库如 Jieba)
        // 这里使用字符级tokenization作为简化
        var tokens = new List<long>();
        var chars = text.ToCharArray();

        // 模拟 BERT tokenizer
        tokens.Add(101); // [CLS]

        foreach (var c in chars)
        {
            // 简单映射：汉字 -> unicode codepoint
            tokens.Add(c);
        }

        tokens.Add(102); // [SEP]

        // Padding to fixed length (512)
        var maxLength = 512;
        if (tokens.Count < maxLength)
        {
            tokens.AddRange(Enumerable.Repeat(0L, maxLength - tokens.Count));
        }
        else if (tokens.Count > maxLength)
        {
            tokens = tokens.Take(maxLength).ToList();
        }

        return tokens.ToArray();
    }

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
