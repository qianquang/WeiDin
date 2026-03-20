using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.AutoMapper;
using Volo.Abp.Modularity;
using WeiDin.Application.Mappings;
using WeiDin.Application.Services;
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
    }
}

