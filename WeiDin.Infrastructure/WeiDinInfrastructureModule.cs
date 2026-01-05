using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.SqlServer;
using Volo.Abp.Modularity;
using WeiDin.Core;
using WeiDin.Infrastructure.Data;

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
            // 使用默认连接字符串（从配置中读取 "DefaultConnection"）
            options.UseSqlServer();
            
            // 配置特定 DbContext 的选项
            options.Configure<WeiDinDbContext>(opts =>
            {
                // 从配置中读取连接字符串
                var connString = opts.ConnectionString ?? configuration.GetConnectionString("DefaultConnection");
                if (string.IsNullOrEmpty(connString))
                {
                    throw new InvalidOperationException("数据库连接字符串未配置。请在 appsettings.json 中设置 ConnectionStrings:DefaultConnection");
                }
                
                // 使用 DbContextOptions 配置 SQL Server 和迁移程序集
                opts.DbContextOptions.UseSqlServer(connString, sqlServerOptions =>
                {
                    sqlServerOptions.MigrationsAssembly("WeiDin.API");
                });
            });
        });
    }
}

