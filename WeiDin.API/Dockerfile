# 使用官方.NET 8.0运行时镜像作为基础镜像
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

# 使用官方.NET 8.0 SDK镜像进行构建
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# 复制项目文件并还原依赖
COPY ["WeiDin.API/WeiDin.API.csproj", "WeiDin.API/"]
COPY ["WeiDin.Application/WeiDin.Application.csproj", "WeiDin.Application/"]
COPY ["WeiDin.Core/WeiDin.Core.csproj", "WeiDin.Core/"]
COPY ["WeiDin.Infrastructure/WeiDin.Infrastructure.csproj", "WeiDin.Infrastructure/"]

RUN dotnet restore "WeiDin.API/WeiDin.API.csproj"

# 复制所有源代码
COPY . .

# 构建应用程序
WORKDIR "/src/WeiDin.API"
RUN dotnet build "WeiDin.API.csproj" -c Release -o /app/build

# 发布应用程序
FROM build AS publish
RUN dotnet publish "WeiDin.API.csproj" -c Release -o /app/publish

# 最终镜像
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# 创建上传目录
RUN mkdir -p /app/uploads
RUN mkdir -p /app/logs

ENTRYPOINT ["dotnet", "WeiDin.API.dll"]
