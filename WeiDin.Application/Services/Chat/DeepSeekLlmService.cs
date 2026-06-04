using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using WeiDin.Core.Interfaces;

namespace WeiDin.Application.Services.Chat;

/// <summary>
/// LLM 服务实现 — 通过 Semantic Kernel 调用大语言模型
/// 默认配置为 DeepSeek API，但可切换为其他 OpenAI 兼容的 LLM 服务
/// </summary>
public class DeepSeekLlmService : ILlmService
{
    private readonly Kernel _kernel;
    private readonly string _modelId;
    private readonly bool _thinkingEnabled;
    private readonly string _reasoningEffort;
    private readonly ILogger<DeepSeekLlmService> _logger;

    public DeepSeekLlmService(IConfiguration configuration, ILogger<DeepSeekLlmService> logger)
    {
        _logger = logger;

        var apiKey = configuration["DeepSeek:ApiKey"] ?? string.Empty;
        var baseUrl = configuration["DeepSeek:BaseUrl"] ?? "https://api.deepseek.com";
        _modelId = configuration["DeepSeek:ModelId"] ?? "deepseek-chat";
        _thinkingEnabled = configuration.GetValue<bool>("DeepSeek:ThinkingEnabled", false);
        _reasoningEffort = configuration["DeepSeek:ReasoningEffort"] ?? "high";

        if (string.IsNullOrEmpty(apiKey))
            throw new InvalidOperationException("DeepSeek:ApiKey 未配置，请在 appsettings.json 中设置。");

        // Semantic Kernel 封装了 OpenAI 兼容 API，支持 DeepSeek 等兼容服务
        var chatCompletion = new OpenAIChatCompletionService(
            _modelId,
            new Uri(baseUrl),
            apiKey);

        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton<IChatCompletionService>(chatCompletion);
        _kernel = builder.Build();
    }

    /// <inheritdoc />
    public async Task<string> ChatAsync(string systemPrompt, string userMessage, CancellationToken cancellationToken = default)
    {
        var chatHistory = new ChatHistory(systemPrompt);
        chatHistory.AddUserMessage(userMessage);

        // 通过 ExtensionData 传入 DeepSeek 特有参数
        // 这些参数会被序列化到请求体中，发给 API
        var executionSettings = new OpenAIPromptExecutionSettings
        {
            Temperature = 0.7,
            MaxTokens = 2048
        };

        // DeepSeek thinking 参数
        executionSettings.ExtensionData["thinking"] = new { type = _thinkingEnabled ? "enabled" : "disabled" };
        executionSettings.ExtensionData["reasoning_effort"] = _reasoningEffort;

        _logger.LogDebug("调用 LLM: model={Model}, thinking={Thinking}, effort={Effort}",
            _modelId, _thinkingEnabled, _reasoningEffort);

        var chatCompletion = _kernel.GetRequiredService<IChatCompletionService>();
        var reply = await chatCompletion.GetChatMessageContentAsync(
            chatHistory,
            executionSettings,
            cancellationToken: cancellationToken);

        return reply.Content ?? string.Empty;
    }
}
