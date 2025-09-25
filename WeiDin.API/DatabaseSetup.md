# 数据库连接配置说明

## 数据库连接方式

### 1. SQL Server LocalDB（推荐用于开发）

**优点**：
- 轻量级，无需安装完整SQL Server
- 自动创建和删除数据库
- 适合开发和测试

**配置**：
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=WeiDinDb;Trusted_Connection=true;MultipleActiveResultSets=true;TrustServerCertificate=true"
  }
}
```

**安装步骤**：
1. 安装 Visual Studio 2022（包含SQL Server LocalDB）
2. 或单独安装 SQL Server LocalDB：https://docs.microsoft.com/en-us/sql/database-engine/configure-windows/sql-server-express-localdb

### 2. SQL Server Express（免费版本）

**配置**：
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=.\\SQLEXPRESS;Database=WeiDinDb;Trusted_Connection=true;MultipleActiveResultSets=true;TrustServerCertificate=true"
  }
}
```

### 3. SQL Server 完整版

**配置**：
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=WeiDinDb;User Id=sa;Password=your-password;MultipleActiveResultSets=true;TrustServerCertificate=true"
  }
}
```

### 4. Azure SQL Database（云数据库）

**配置**：
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=tcp:your-server.database.windows.net,1433;Initial Catalog=WeiDinDb;Persist Security Info=False;User ID=your-username;Password=your-password;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
  }
}
```

## 数据库初始化方法

### 方法1：使用Entity Framework迁移（推荐）

```bash
# 1. 安装EF Core工具（如果未安装）
dotnet tool install --global dotnet-ef

# 2. 在API项目目录下创建迁移
cd WeiDin.API
dotnet ef migrations add InitialCreate

# 3. 更新数据库
dotnet ef database update
```

### 方法2：使用SQL脚本

1. 打开SQL Server Management Studio (SSMS)
2. 连接到你的SQL Server实例
3. 执行 `Scripts/InitDatabase.sql` 脚本

### 方法3：程序自动创建（开发环境）

项目已配置为在启动时自动创建数据库：

```csharp
// 在Program.cs中
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<WeiDinDbContext>();
    context.Database.EnsureCreated();
}
```

## Redis配置

### 本地Redis

**安装**：
- Windows: 下载Redis for Windows
- 或使用Docker: `docker run -d -p 6379:6379 redis:latest`

**配置**：
```json
{
  "ConnectionStrings": {
    "Redis": "localhost:6379"
  }
}
```

### Redis云服务

**配置**：
```json
{
  "ConnectionStrings": {
    "Redis": "your-redis-server:6379,password=your-password"
  }
}
```

## 环境配置

### 开发环境 (appsettings.Development.json)
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=WeiDinDb_Dev;Trusted_Connection=true;MultipleActiveResultSets=true;TrustServerCertificate=true",
    "Redis": "localhost:6379"
  }
}
```

### 生产环境 (appsettings.Production.json)
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=your-production-server;Database=WeiDinDb_Prod;User Id=your-username;Password=your-password;MultipleActiveResultSets=true;TrustServerCertificate=true",
    "Redis": "your-redis-server:6379"
  }
}
```

## 常见问题解决

### 1. 连接字符串错误
- 检查服务器名称是否正确
- 确认数据库是否存在
- 验证用户名和密码

### 2. TrustServerCertificate错误
- 在连接字符串中添加 `TrustServerCertificate=true`
- 或配置SSL证书

### 3. 数据库不存在
- 使用 `EnsureCreated()` 自动创建
- 或手动创建数据库

### 4. 权限问题
- 确保用户有创建数据库的权限
- 或使用管理员账户

## 测试连接

运行以下命令测试数据库连接：

```bash
dotnet run --project WeiDin.API
```

如果启动成功，说明数据库连接正常。

## 数据库备份

### 备份命令
```sql
BACKUP DATABASE WeiDinDb TO DISK = 'C:\Backup\WeiDinDb.bak'
```

### 恢复命令
```sql
RESTORE DATABASE WeiDinDb FROM DISK = 'C:\Backup\WeiDinDb.bak'
```
