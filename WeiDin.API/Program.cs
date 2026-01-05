using Serilog;
using Volo.Abp;
using Volo.Abp.Autofac;
using WeiDin.API;

var builder = WebApplication.CreateBuilder(args);

// 配置Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/weidin-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host
    .UseSerilog()
    .UseAutofac();

// ABP 应用 - 所有配置都在WeiDinApiModule中完成
await builder.AddApplicationAsync<WeiDinApiModule>();

var app = builder.Build();

await app.InitializeApplicationAsync();
await app.RunAsync();
