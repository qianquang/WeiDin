using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.VectorData;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.SqlServer;
using Volo.Abp.Modularity;
using WeiDin.Core;
using WeiDin.Core.Interfaces;
using WeiDin.Core.Models;

using KnowledgeEntity = WeiDin.Core.Entities.Knowledge;
using WeiDin.Infrastructure.Background;
using WeiDin.Infrastructure.Data;
using WeiDin.Infrastructure.Repositories;
using WeiDin.Infrastructure.Services;

namespace WeiDin.Infrastructure;

[DependsOn(
    typeof(AbpEntityFrameworkCoreSqlServerModule),
    typeof(WeiDinCoreModule)
)]
public class WeiDinInfrastructureModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var configuration = context.Services.GetConfiguration();

        // 配置DbContext
        context.Services.AddAbpDbContext<WeiDinDbContext>(options =>
        {
            options.AddDefaultRepositories(includeAllEntities: true);
        });

        // 配置DbContext选项
        Configure<AbpDbContextOptions>(options =>
        {
            options.UseSqlServer();

            options.Configure<WeiDinDbContext>(opts =>
            {
                var connString = opts.ConnectionString ?? configuration.GetConnectionString("DefaultConnection");
                if (string.IsNullOrEmpty(connString))
                {
                    throw new InvalidOperationException("数据库连接字符串未配置。请在 appsettings.json 中设置 ConnectionStrings:DefaultConnection");
                }

                opts.DbContextOptions.UseSqlServer(connString, sqlServerOptions =>
                {
                    sqlServerOptions.MigrationsAssembly("WeiDin.Infrastructure");
                });
            });
        });

        // 注册动态表服务和消息仓储
        context.Services.AddTransient<IDynamicTableService, DynamicTableService>();
        context.Services.AddTransient<IDynamicMessageRepository, DynamicMessageRepository>();

        // 注册知识库仓储
        context.Services.AddTransient<KnowledgeChunkRepository>();
        context.Services.AddTransient<KnowledgeBaseRepository>();
        context.Services.AddTransient<KnowledgeRepository>();

        // 注册嵌入服务
        context.Services.AddSingleton<IEmbeddingService, OnnxEmbeddingService>();

        // 注册 Rerank 精排服务
        context.Services.AddSingleton<IRerankService, OnnxRerankService>();

        // 注册文档解析服务
        context.Services.AddSingleton<IDocumentParser, DocumentParser>();

        // 注册向量存储（开发用内存实现，生产换 Qdrant/Redis 只需改这一行）
        context.Services.AddSingleton<VectorStore, InMemoryVectorStore>();

        // 注册文档处理 Channel（有界队列，容量 100）
        context.Services.AddSingleton(Channel.CreateBounded<(KnowledgeEntity, string)>(new BoundedChannelOptions(100)
        {
            FullMode = BoundedChannelFullMode.Wait
        }));

        // 注册文档处理后台服务
        context.Services.AddHostedService<DocumentProcessingWorker>();
    }
}
