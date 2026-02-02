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
- 黑名单管理
- 好友状态查看

### 文件管理
- 文件上传/下载
- 多种文件类型支持
- 文件大小限制
- 安全文件访问


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
- 🗄️ [数据库设置](./WeiDin.API/DatabaseSetup.md) - 数据库配置说明

---

**注意**: 这是一个毕业设计项目，主要用于学习和演示目的。在生产环境中使用前，请确保进行充分的安全测试和性能优化。



