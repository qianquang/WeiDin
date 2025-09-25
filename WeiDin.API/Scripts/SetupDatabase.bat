@echo off
echo 微钉即时通讯系统 - 数据库设置脚本
echo =====================================

echo.
echo 1. 检查.NET环境...
dotnet --version
if %errorlevel% neq 0 (
    echo 错误: 未找到.NET环境，请先安装.NET 8.0 SDK
    pause
    exit /b 1
)

echo.
echo 2. 还原NuGet包...
dotnet restore
if %errorlevel% neq 0 (
    echo 错误: 包还原失败
    pause
    exit /b 1
)

echo.
echo 3. 构建项目...
dotnet build
if %errorlevel% neq 0 (
    echo 错误: 项目构建失败
    pause
    exit /b 1
)

echo.
echo 4. 安装EF Core工具...
dotnet tool install --global dotnet-ef
if %errorlevel% neq 0 (
    echo 警告: EF Core工具安装失败，尝试继续...
)

echo.
echo 5. 创建数据库迁移...
dotnet ef migrations add InitialCreate --project WeiDin.API --startup-project WeiDin.API
if %errorlevel% neq 0 (
    echo 错误: 创建迁移失败
    pause
    exit /b 1
)

echo.
echo 6. 更新数据库...
dotnet ef database update --project WeiDin.API --startup-project WeiDin.API
if %errorlevel% neq 0 (
    echo 错误: 数据库更新失败
    pause
    exit /b 1
)

echo.
echo 7. 启动应用程序...
echo 数据库设置完成！正在启动应用程序...
echo 请在浏览器中访问: https://localhost:7000/swagger
echo 按 Ctrl+C 停止应用程序

dotnet run --project WeiDin.API

pause
