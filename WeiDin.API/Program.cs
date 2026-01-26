using Serilog;
using Volo.Abp;
using Volo.Abp.Autofac;
using WeiDin.API;

var builder = WebApplication.CreateBuilder(args);

// 配置Serilog - 只从配置文件读取，不手动添加输出
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .CreateLogger();

builder.Host
    .UseSerilog()
    .UseAutofac();

// ABP 应用 - 所有配置都在WeiDinApiModule中完成
await builder.AddApplicationAsync<WeiDinApiModule>();

var app = builder.Build();

await app.InitializeApplicationAsync();
await app.RunAsync();
