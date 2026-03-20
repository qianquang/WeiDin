using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using WeiDin.Application.Interfaces;
using WeiDin.Application.Plugins;
using WeiDin.Core.Interfaces;

namespace WeiDin.Application.Services;

/// <summary>
/// DeepSeek API 调用层实现，封装 Semantic Kernel 与消息上下文 Skill。
/// </summary>
public class ChatCompletionService : IAssistantChatService
{
    private readonly Kernel _kernel;
    private readonly IRetrievalService? _retrievalService;
    private readonly bool _enableRag;

    public ChatCompletionService(
        IConfiguration configuration,
        IMessageService messageService,
        IRetrievalService? retrievalService = null)
    {
        var apiKey = configuration["DeepSeek:ApiKey"] ?? string.Empty;
        var modelId = configuration["DeepSeek:ModelId"] ?? "deepseek-chat";
        var baseUrl = configuration["DeepSeek:BaseUrl"] ?? "https://api.deepseek.com";

        if (string.IsNullOrEmpty(apiKey))
            throw new InvalidOperationException("DeepSeek:ApiKey 未配置，请在 appsettings.json 中设置。");

        var chatCompletion = new OpenAIChatCompletionService(
            modelId,
            new Uri(baseUrl),
            apiKey);

        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton<Microsoft.SemanticKernel.ChatCompletion.IChatCompletionService>(chatCompletion);
        builder.Plugins.AddFromObject(new MessageContextPlugin(messageService), "MessageContext");

        _kernel = builder.Build();

        // RAG 配置
        _retrievalService = retrievalService;
        _enableRag = configuration.GetValue<bool>("VectorSearch:EnableRAG", false);
    }

    /// <inheritdoc />
    public Kernel GetKernel() => _kernel;

    /// <inheritdoc />
    public async Task<string> GetRecentMessagesJsonAsync(Guid relationId, int count = 20, CancellationToken cancellationToken = default)
    {
        var function = _kernel.Plugins.GetFunction("MessageContext", "get_recent_messages_as_json");
        var args = new KernelArguments
        {
            ["relationId"] = relationId,
            ["count"] = count
        };
        var result = await _kernel.InvokeAsync(function, args, cancellationToken);
        return result.GetValue<string>() ?? "[]";
    }

    /// <inheritdoc />
    public async Task<string> SendWithContextAsync(Guid relationId, string userPrompt, CancellationToken cancellationToken = default)
    {
        var messagesJson = await GetRecentMessagesJsonAsync(relationId, cancellationToken: cancellationToken);

        string ragContext = string.Empty;

        // 如果启用 RAG，从向量库检索相关知识
        if (_enableRag && _retrievalService != null)
        {
            try
            {
                var retrievedChunks = await _retrievalService.RetrieveAsync(userPrompt, cancellationToken: cancellationToken);
                if (retrievedChunks.Count > 0)
                {
                    ragContext = "相关知识背景：\n" + string.Join("\n---\n", retrievedChunks.Select(c => c.Content));
                }
            }
            catch (Exception)
            {
                // RAG 失败不影响正常流程
            }
        }

        var systemPrompt = $@"你是一个个人助手。以下是用户与好友/群组的最近聊天记录（JSON 格式），请结合上下文理解用户的意图并给出有帮助的回复。

聊天记录：
{messagesJson}
{(string.IsNullOrEmpty(ragContext) ? "" : $"\n\n{ragContext}")}";

        var chatHistory = new ChatHistory(systemPrompt);
        chatHistory.AddUserMessage(userPrompt);

        var chatCompletion = _kernel.GetRequiredService<Microsoft.SemanticKernel.ChatCompletion.IChatCompletionService>();
        var reply = await chatCompletion.GetChatMessageContentAsync(
            chatHistory,
            cancellationToken: cancellationToken);

        return reply.Content ?? string.Empty;
    }
}
