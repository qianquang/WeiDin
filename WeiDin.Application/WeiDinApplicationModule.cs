using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.AutoMapper;
using Volo.Abp.Modularity;
using WeiDin.Application.Interfaces;
using WeiDin.Application.Interfaces.Knowledge;
using WeiDin.Application.Mappings;
using WeiDin.Application.Services.Chat;
using WeiDin.Application.Services.Knowledge;
using WeiDin.Core;
using WeiDin.Core.Interfaces;

namespace WeiDin.Application;

[DependsOn(
    typeof(AbpAutoMapperModule),
    typeof(WeiDinCoreModule)
)]
public class WeiDinApplicationModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // 配置AutoMapper
        context.Services.AddAutoMapper(typeof(MappingProfile));

        // ABP会自动注册所有实现了IApplicationService接口的服务
        // 所以不需要手动注册服务

        // 注册检索服务 (RAG)
        context.Services.AddScoped<IRetrievalService, RetrievalService>();

        // 注册知识库服务（内部使用，不对外暴露 API）
        context.Services.AddScoped<IKnowledgeBaseService, KnowledgeBaseService>();

        // 注册文档入库服务
        context.Services.AddScoped<IDocumentIngestionService, DocumentIngestionService>();

        // 注册文本分块服务
        context.Services.AddScoped<ITextChunkingService, TextChunkingService>();

        // 注册 LLM 服务
        context.Services.AddSingleton<ILlmService, DeepSeekLlmService>();

        // 注册查询理解服务
        context.Services.AddScoped<IQueryUnderstandingService, QueryUnderstandingService>();

        // 注册 Agent 引擎
        context.Services.AddScoped<IAgentEngine, RagAgentEngine>();
    }
}
