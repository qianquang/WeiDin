# 微钉即时通讯系统

基于ASP.NET Core 8.0开发的即时通讯系统，提供完整的用户管理、消息发送、群组管理、好友管理等功能。

## 项目结构

```
WeiDin/
├── WeiDin.API/                 # Web API 项目
│   ├── Controllers/            # API 控制器
│   ├── Hubs/                   # SignalR Hub
│   └── Program.cs              # 应用程序入口
├── WeiDin.Application/         # 应用层
│   ├── DTOs/                   # 数据传输对象
│   ├── Interfaces/             # 服务接口
│   ├── Services/               # 服务实现
│   └── Mappings/               # AutoMapper 配置
├── WeiDin.Core/                # 核心层
│   ├── Entities/               # 实体模型
│   └── Interfaces/             # 核心接口
├── WeiDin.Infrastructure/      # 基础设施层
│   ├── Data/                   # 数据上下文
│   └── Repositories/           # 仓储实现
└── WeiDin.sln                  # 解决方案文件
```

## 技术栈

### 后端技术
- **框架**: ASP.NET Core 8.0
- **数据库**: SQL Server + Entity Framework Core
- **缓存**: Redis
- **实时通信**: SignalR
- **身份认证**: JWT Bearer Token
- **日志**: Serilog
- **API文档**: Swagger/OpenAPI
- **对象映射**: AutoMapper
- **验证**: FluentValidation

### 前端技术（计划中）
- **框架**: Vue.js 3
- **UI组件库**: Element Plus
- **状态管理**: Pinia
- **构建工具**: Vite

## 功能特性

### 用户管理
- 用户注册/登录
- 用户信息管理
- 在线状态管理
- 密码修改

### 消息系统
- 文字消息发送/接收
- 图片/视频/文件消息
- 消息状态管理（已发送/已送达/已读）
- 消息撤回
- 消息搜索

### 群组管理
- 群组创建/解散
- 群成员管理
- 群组设置
- 群组消息

### 好友管理
- 添加/删除好友
- 好友分组
- 黑名单管理
- 好友状态查看

### 文件管理
- 文件上传/下载
- 多种文件类型支持
- 文件大小限制
- 安全文件访问

## 快速开始

### 环境要求
- .NET 8.0 SDK
- SQL Server 2019 或更高版本
- Redis 6.0 或更高版本
- Visual Studio 2022 或 VS Code

### 安装步骤

1. **克隆项目**
   ```bash
   git clone <repository-url>
   cd WeiDin
   ```

2. **配置数据库连接**
   
   修改 `WeiDin.API/appsettings.json` 中的连接字符串：
   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=WeiDinDb;Trusted_Connection=true;MultipleActiveResultSets=true",
       "Redis": "localhost:6379"
     }
   }
   ```

3. **安装依赖包**
   ```bash
   dotnet restore
   ```

4. **运行项目**
   ```bash
   dotnet run --project WeiDin.API
   ```

5. **访问API文档**
   
   打开浏览器访问：
   - **HTTPS API**: `https://localhost:7000/swagger`
   - **HTTP API**: `http://localhost:5000`

## API 接口

### 认证接口
- `POST /api/v1/auth/register` - 用户注册
- `POST /api/v1/auth/login` - 用户登录
- `POST /api/v1/auth/logout` - 用户退出

### 用户管理
- `GET /api/v1/users` - 获取所有用户
- `GET /api/v1/users/{id}` - 获取用户详情
- `POST /api/v1/users` - 创建用户
- `PUT /api/v1/users/{id}` - 更新用户信息
- `DELETE /api/v1/users/{id}` - 删除用户

### 消息管理
- `GET /api/v1/messages/{id}` - 获取消息详情
- `POST /api/v1/messages` - 发送消息
- `DELETE /api/v1/messages/{id}` - 删除消息
- `PUT /api/v1/messages/{id}/status` - 更新消息状态

### 群组管理
- `GET /api/v1/groups` - 获取群组列表
- `POST /api/v1/groups` - 创建群组
- `PUT /api/v1/groups/{id}` - 更新群组信息
- `POST /api/v1/groups/{id}/join` - 加入群组
- `POST /api/v1/groups/{id}/leave` - 退出群组

### 好友管理
- `GET /api/v1/friendships` - 获取好友列表
- `POST /api/v1/friendships` - 添加好友
- `DELETE /api/v1/friendships/{id}` - 删除好友
- `POST /api/v1/friendships/blacklist` - 添加到黑名单

### 文件管理
- `POST /api/v1/files/upload` - 上传文件
- `GET /api/v1/files/{fileName}` - 下载文件
- `DELETE /api/v1/files/{fileName}` - 删除文件

## 实时通信

系统使用SignalR实现实时通信，Hub地址：`/chatHub`

### 客户端连接示例（JavaScript）
```javascript
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/chatHub", {
        accessTokenFactory: () => {
            return localStorage.getItem("token");
        }
    })
    .build();

// 连接
await connection.start();

// 监听消息
connection.on("ReceiveMessage", function (message) {
    console.log("收到消息:", message);
});

// 发送消息
connection.invoke("SendMessageToUser", targetUserId, message);
```

## 数据库设计

### 核心表结构
- **Users** - 用户基本信息
- **Messages** - 消息内容
- **MessageStatus** - 消息状态
- **MessageAttachments** - 消息附件
- **Groups** - 群组信息
- **GroupMembers** - 群组成员
- **Friendships** - 好友关系
- **Blacklists** - 黑名单

## 安全特性

- JWT身份认证
- 密码加密存储
- API接口权限控制
- 文件上传安全检查
- 跨域请求支持

## 开发计划

- [x] 项目架构搭建
- [x] 用户管理模块
- [x] 消息管理模块
- [x] 群组管理模块
- [x] 好友管理模块
- [x] 文件管理模块
- [x] SignalR实时通信
- [ ] 前端界面开发
- [ ] 移动端支持
- [ ] 消息推送
- [ ] 系统监控

## 贡献指南

1. Fork 项目
2. 创建功能分支 (`git checkout -b feature/AmazingFeature`)
3. 提交更改 (`git commit -m 'Add some AmazingFeature'`)
4. 推送到分支 (`git push origin feature/AmazingFeature`)
5. 打开 Pull Request

## 许可证

本项目采用 MIT 许可证 - 查看 [LICENSE](LICENSE) 文件了解详情

## 联系方式

如有问题或建议，请通过以下方式联系：

- 项目Issues: [GitHub Issues](https://github.com/your-repo/issues)
- 邮箱: your-email@example.com

---

**注意**: 这是一个毕业设计项目，主要用于学习和演示目的。在生产环境中使用前，请确保进行充分的安全测试和性能优化。



