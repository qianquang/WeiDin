# 微钉即时通讯系统

基于 ASP.NET Core 8.0 和 Vue.js 3 开发的现代化即时通讯系统，提供完整的用户管理、消息发送、群组管理、好友管理等功能。

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
├── WeiDin.Frontend/            # 前端项目
│   ├── src/
│   │   ├── api/                # API 客户端
│   │   ├── views/              # 页面组件
│   │   ├── stores/             # Pinia 状态管理
│   │   ├── utils/              # 工具函数
│   │   └── router/             # 路由配置
│   └── package.json            # 前端依赖
├── WeiDin.sln                  # 解决方案文件
└── docker-compose.yml          # Docker 编排配置
```

> 📖 详细架构说明请查看 [项目架构说明文档.md](./项目架构说明文档.md)

## 技术栈

### 后端技术
- **框架**: ASP.NET Core 8.0
- **架构模式**: DDD（领域驱动设计）+ 分层架构
- **基础框架**: ABP Framework 8.1.5
- **数据库**: SQL Server + Entity Framework Core 8.0
- **缓存**: Redis（已配置，可选）
- **实时通信**: SignalR 1.1.0
- **身份认证**: JWT Bearer Token
- **日志**: Serilog
- **API文档**: Swagger/OpenAPI
- **对象映射**: AutoMapper 12.0.1
- **验证**: FluentValidation 11.8.1
- **依赖注入**: Autofac

### 前端技术
- **框架**: Vue.js 3.4.0
- **语言**: TypeScript 5.3.0
- **UI组件库**: Element Plus 2.4.4
- **状态管理**: Pinia 2.1.7
- **路由**: Vue Router 4.2.5
- **HTTP客户端**: Axios 1.6.2
- **实时通信**: @microsoft/signalr 8.0.0
- **构建工具**: Vite 5.0.10

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

**后端**：
- .NET 8.0 SDK
- SQL Server 2019 或更高版本（或 SQL Server LocalDB）
- Redis 6.0 或更高版本（可选，已配置但未强制使用）
- Visual Studio 2022 或 VS Code

**前端**：
- Node.js >= 16.0.0
- npm >= 8.0.0 或 yarn >= 1.22.0

### 安装步骤

#### 1. 克隆项目
```bash
git clone <repository-url>
cd WeiDin
```

#### 2. 配置数据库连接

修改 `WeiDin.API/appsettings.json` 中的连接字符串：
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=WeiDinDb;Trusted_Connection=true;MultipleActiveResultSets=true;TrustServerCertificate=true",
    "Redis": "localhost:6379"
  }
}
```

> 💡 **提示**：如果使用 SQL Server LocalDB，通常无需修改配置。Redis 为可选，如果未安装 Redis，应用仍可正常运行（Redis 已配置但当前未在业务代码中使用）。

#### 3. 安装后端依赖
```bash
dotnet restore
```

#### 4. 启动后端 API
```bash
dotnet run --project WeiDin.API
```

后端启动后，访问：
- **Swagger API 文档**: `https://localhost:7000/swagger`
- **HTTPS API**: `https://localhost:7000`
- **HTTP API**: `http://localhost:5000`

#### 5. 安装并启动前端（可选）

打开新的终端窗口：
```bash
cd WeiDin.Frontend
npm install
npm run dev
```

前端启动后，访问：
- **前端应用**: `http://localhost:3000`

> 📖 更详细的启动说明请查看 [快速启动指南.md](./快速启动指南.md)

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

- ✅ **JWT 身份认证**：安全的 Token 认证机制
- ✅ **密码加密存储**：使用哈希算法加密存储
- ✅ **API 接口权限控制**：基于 ABP Framework 的权限系统
- ✅ **文件上传安全检查**：文件类型和大小验证
- ✅ **跨域请求支持**：CORS 配置
- ✅ **HTTPS 支持**：生产环境强制 HTTPS

## 架构特点

- 🎯 **分层架构**：清晰的分层设计，职责分离
- 🔒 **DDD 设计**：领域驱动设计，业务逻辑集中在领域层
- 🔌 **依赖注入**：使用 ABP Framework 的 Autofac 容器
- ⚡ **高性能**：Redis 缓存支持（已配置）
- 📡 **实时通信**：SignalR 实现双向实时通信
- 📚 **可维护性**：代码规范，模块化设计
- 🚀 **可扩展性**：易于扩展新功能

## 开发计划

### 已完成 ✅
- [x] 项目架构搭建（DDD + 分层架构）
- [x] 用户管理模块
- [x] 消息管理模块
- [x] 群组管理模块
- [x] 好友管理模块
- [x] 文件管理模块
- [x] SignalR 实时通信
- [x] 前端界面开发（Vue.js 3 + Element Plus）
- [x] JWT 身份认证
- [x] Redis 缓存配置

### 进行中 🚧
- [ ] 前端功能完善
- [ ] 性能优化

### 计划中 📋
- [ ] 移动端支持
- [ ] 消息推送（Push Notification）
- [ ] 系统监控和日志分析
- [ ] 单元测试和集成测试
- [ ] Redis 缓存实际应用

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
---

## 相关文档

- 📖 [项目架构说明文档](./项目架构说明文档.md) - 详细的架构设计和模块说明
- 🚀 [快速启动指南](./快速启动指南.md) - 快速上手指南
- 🗄️ [数据库设置](./WeiDin.API/DatabaseSetup.md) - 数据库配置说明

## 技术亮点

1. **现代化技术栈**：采用最新的 .NET 8.0 和 Vue.js 3
2. **完整的前后端分离**：RESTful API + SPA 前端
3. **实时通信**：SignalR WebSocket 双向实时通信
4. **企业级架构**：ABP Framework + DDD 设计模式
5. **类型安全**：TypeScript + C# 强类型支持

---

**注意**: 这是一个毕业设计项目，主要用于学习和演示目的。在生产环境中使用前，请确保进行充分的安全测试和性能优化。



