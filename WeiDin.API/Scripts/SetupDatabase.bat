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
echo 2. 切换到项目根目录...
cd /d "%~dp0..\.."
echo 当前目录: %CD%

echo.
echo 3. 还原NuGet包...
dotnet restore
if %errorlevel% neq 0 (
    echo 错误: 包还原失败
    pause
    exit /b 1
)

echo.
echo 4. 构建项目...
dotnet build
if %errorlevel% neq 0 (
    echo 错误: 项目构建失败
    pause
    exit /b 1
)

echo.
echo 5. 安装EF Core工具...
dotnet tool install --global dotnet-ef
if %errorlevel% neq 0 (
    echo 警告: EF Core工具安装失败，尝试继续...
)

echo.
echo 6. 创建数据库迁移...
dotnet ef migrations add InitialCreate --project WeiDin.Infrastructure --startup-project WeiDin.API
if %errorlevel% neq 0 (
    echo 错误: 创建迁移失败
    pause
    exit /b 1
)

echo.
echo 7. 更新数据库...
dotnet ef database update --project WeiDin.Infrastructure --startup-project WeiDin.API
if %errorlevel% neq 0 (
    echo 错误: 数据库更新失败
    pause
    exit /b 1
)

echo.
echo 8. 启动应用程序...
echo 数据库设置完成！正在启动应用程序...
echo 请在浏览器中访问: https://localhost:7000/swagger
echo HTTP API: http://localhost:5000
echo HTTPS API: https://localhost:7000
echo 按 Ctrl+C 停止应用程序

dotnet run --project WeiDin.API

pause
