@echo off
chcp 65001 >nul 2>&1
setlocal enabledelayedexpansion
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
echo 6. 检查数据库迁移状态...

REM 检查 Migrations 文件夹是否存在
if exist "WeiDin.Infrastructure\Migrations" (
    echo Migrations 文件夹已存在，检查是否有待应用的迁移...
    dotnet ef database update --project WeiDin.Infrastructure --startup-project WeiDin.API --dry-run >nul 2>&1
    if %errorlevel% equ 0 (
        echo 数据库已是最新版本。
    ) else (
        echo 检测到待应用的迁移，将在下一步应用。
    )
    
    REM 检查是否有模型更改需要创建新迁移
    dotnet ef migrations has-pending-model-changes --project WeiDin.Infrastructure --startup-project WeiDin.API >nul 2>&1
    if %errorlevel% equ 0 (
        echo.
        echo [重要] 检测到模型更改，需要创建新的迁移！
        echo 请输入迁移名称（直接回车使用默认名称 UpdateModel）:
        set /p MIGRATION_NAME=
        if "!MIGRATION_NAME!"=="" set MIGRATION_NAME=UpdateModel
        echo 正在创建迁移: !MIGRATION_NAME!
        call dotnet ef migrations add "!MIGRATION_NAME!" --project WeiDin.Infrastructure --startup-project WeiDin.API
        if !errorlevel! neq 0 (
            echo 错误: 创建迁移失败
            pause
            exit /b 1
        )
        echo.
        echo [重要] 迁移创建成功！请将 WeiDin.Infrastructure\Migrations 文件夹提交到版本控制。
        echo.
    )
) else (
    echo 首次运行，创建初始迁移...
    dotnet ef migrations add InitialCreate --project WeiDin.Infrastructure --startup-project WeiDin.API
    if %errorlevel% neq 0 (
        echo 错误: 创建迁移失败
        pause
        exit /b 1
    )
    echo 初始迁移创建成功！
)

echo.
echo 7. 应用数据库迁移...
dotnet ef database update --project WeiDin.Infrastructure --startup-project WeiDin.API
if %errorlevel% neq 0 (
    echo 错误: 数据库更新失败
    pause
    exit /b 1
)
echo 数据库迁移应用成功！

echo.
echo 8. 启动应用程序...
echo 数据库设置完成！正在启动应用程序...
echo 请在浏览器中访问: https://localhost:7000/swagger
echo HTTP API: http://localhost:5000
echo HTTPS API: https://localhost:7000
echo 按 Ctrl+C 停止应用程序

dotnet run --project WeiDin.API

endlocal
pause
