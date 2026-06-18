# 微钉即时通讯系统

基于 ASP.NET Core 8.0 和 Vue.js 3 开发的现代化即时通讯系统，集成 RAG（检索增强生成）知识库引擎，提供完整的用户管理、实时消息、群组管理、好友管理、AI 智能问答等功能。

## 项目结构

```
WeiDin/
├── WeiDin.API/                 # Web API 项目
│   ├── Controllers/            # API 控制器（含 Agent、Knowledge 控制器）
│   ├── Hubs/                   # SignalR Hub
│   └── Program.cs              # 应用程序入口
├── WeiDin.Application/         # 应用层
│   ├── DTOs/                   # 数据传输对象
│   ├── Interfaces/             # 服务接口
│   ├── Services/               # 服务实现（含 RAG 引擎、LLM、检索服务）
│   └── Mappings/               # AutoMapper 配置
├── WeiDin.Core/                # 核心层
│   ├── Entities/               # 实体模型（含知识库实体）
│   └── Interfaces/             # 核心接口
├── WeiDin.Infrastructure/      # 基础设施层
│   ├── Data/                   # 数据上下文
│   ├── Repositories/           # 仓储实现
│   ├── AI/                     # ONNX 推理、向量存储、文档解析
│   └── Migrations/             # EF Core 迁移文件
├── WeiDin.Frontend/            # 前端项目
│   ├── src/
│   │   ├── api/                # API 客户端
│   │   ├── views/              # 页面组件
│   │   ├── stores/             # Pinia 状态管理
│   │   ├── realtime/           # SignalR 三层解耦实时通信
│   │   ├── utils/              # 工具函数
│   │   └── router/             # 路由配置
│   └── package.json            # 前端依赖
├── WeiDin.sln                  # 解决方案文件
└── docker-compose.yml          # Docker 编排配置
```

> 📖 详细架构说明请查看 [项目架构说明文档.md](./项目架构说明文档.md)

## 技术栈

### 后端技术

| 类别 | 技术 | 版本 |
|------|------|------|
| 框架 | ASP.NET Core | 8.0 |
| 架构模式 | DDD（领域驱动设计）+ 分层架构 | — |
| 基础框架 | ABP Framework | 8.1.5 |
| ORM | Entity Framework Core | 8.0.8 |
| 数据库 | SQL Server | 2022 |
| 缓存 | StackExchange.Redis | 2.7.27 |
| 向量数据库 | Qdrant | Latest |
| 实时通信 | SignalR | 1.1.0 |
| 身份认证 | JWT Bearer Token | 8.0.8 |
| AI 编排 | Microsoft Semantic Kernel | 1.77.0 |
| 模型推理 | Microsoft.ML.OnnxRuntime | 1.18.1 |
| 文档解析 | PdfPig / DocumentFormat.OpenXml | 0.1.10 / 3.2.0 |
| 日志 | Serilog | — |
| API 文档 | Swagger/OpenAPI | — |
| 对象映射 | AutoMapper | 12.0.1 |
| 验证 | FluentValidation | 11.8.1 |
| 依赖注入 | Autofac | — |

### 前端技术

| 类别 | 技术 | 版本 |
|------|------|------|
| 框架 | Vue.js | 3.4.0 |
| 语言 | TypeScript | 5.3.0 |
| UI 组件库 | Element Plus | 2.4.4 |
| 状态管理 | Pinia | 2.1.7 |
| 路由 | Vue Router | 4.2.5 |
| HTTP 客户端 | Axios | 1.6.2 |
| 实时通信 | @microsoft/signalr | 8.0.0 |
| 构建工具 | Vite | 5.0.10 |

## 功能特性

### 用户管理
- 用户注册 / 登录 / 登出
- 用户信息管理（头像、昵称等）
- 在线状态管理（多设备连接追踪）
- 密码修改

### 消息系统
- 文字 / 图片 / 视频 / 文件消息发送与接收
- 消息状态管理（已发送 → 已送达 → 已读）
- 消息搜索
- 动态分表存储（每个会话独立分表）

### 群组管理
- 群组创建 / 解散
- 群成员管理（添加、移除、角色设置）
- 群组消息

### 好友管理
- 好友申请 / 接受 / 拒绝
- 黑名单管理
- 好友在线状态实时查看

### 文件管理
- 文件上传 / 下载（单文件最大 10MB）
- 多种文件类型支持（图片、视频、文档等）

### RAG 知识库与 AI 问答

系统内置完整的 RAG（Retrieval-Augmented Generation）知识库引擎，支持文档上传、智能检索和 LLM 问答。

**核心能力**：

| 能力 | 说明 |
|------|------|
| 知识库管理 | 创建 / 编辑 / 删除知识库，支持按知识库隔离数据 |
| 文档上传与解析 | 支持 .txt、.md、.pdf、.docx 及 20+ 种代码/配置格式 |
| 文本分块 | 段落分割 → 合并至目标大小 → 超长句切分 → 重叠窗口 |
| 向量嵌入 | BGE-small-zh-v1.5（ONNX 本地推理，512 维） |
| 向量检索 | 内存向量存储 + 余弦相似度搜索，TopK 候选召回 |
| 交叉编码重排 | bge-reranker-base（ONNX 本地推理），精排提升准确率 |
| 意图识别 | LLM 驱动的查询改写与意图分类（知识检索 / 直接对话） |
| LLM 生成 | DeepSeek API（通过 Semantic Kernel OpenAI 兼容接口） |

**6 阶段 RAG 流水线**：

```
用户提问
  │
  ▼
① 查询理解（Query Understanding）
  │  LLM 改写模糊查询 + 意图分类
  │
  ├─ [DirectChat] ──────────────────────────────▶ ⑥ LLM 直接回答
  │
  ▼ [KnowledgeSearch]
② 向量检索（Retrieval）
  │  ONNX 嵌入 → 内存向量搜索 → 3x TopK 候选
  │
  ▼
③ 交叉编码重排（Rerank）
  │  bge-reranker-base 精排 → TopK 结果
  │
  ▼
④ 上下文组装（Context Assembly）
  │  编号知识片段 + 原始问题
  │
  ▼
⑤ LLM 生成（Generation）
  │  DeepSeek API 生成回答
  │
  ▼
⑥ 返回结果（含引用来源）
```

**异步文档摄取**：使用 `System.Threading.Channels` 实现生产者-消费者模式，文档上传后异步解析、分块、向量化，通过 `ParseStatus` 枚举跟踪处理进度。

## 快速开始

### 环境要求

- .NET 8.0 SDK
- Node.js 18+
- SQL Server（或 LocalDB）
- Redis（可选，用于缓存）
- Qdrant（可选，用于向量存储；不启动则默认使用内存向量存储）

### 1. 启动数据库、Redis 和 Qdrant

```bash
docker-compose up -d sqlserver redis qdrant
```

或使用 LocalDB（Windows 自带），连接字符串在 `WeiDin.API/appsettings.json` 中配置。

> 如不需要 Qdrant，将 `appsettings.json` 中 `VectorStore:Provider` 设为 `InMemory` 即可使用内存向量存储（进程重启后数据丢失）。

### 2. 应用数据库迁移

```bash
dotnet ef database update --project WeiDin.Infrastructure --startup-project WeiDin.API
```

### 3. 启动后端

```bash
cd WeiDin.API
dotnet run
```

后端监听 `https://localhost:7000`（HTTPS）和 `http://localhost:5000`（HTTP），Swagger 文档地址：`https://localhost:7000/swagger`。

### 4. 启动前端

```bash
cd WeiDin.Frontend
npm install
npm run dev
```

### 5. 访问应用

打开 `http://localhost:5173`（或 Vite 显示的地址），注册用户并登录。

### 使用 Docker Compose 一键启动

```bash
docker-compose up -d
```

将同时启动 SQL Server、Redis、Qdrant 和 WeiDin API 容器（端口 5000/5001）。Qdrant Dashboard 地址：`http://localhost:6333/dashboard`。

## 关键架构设计

### 动态消息分表

每个会话（私聊或群聊）拥有独立的消息表，通过 `RelationId` 路由：

| 表名 | 说明 |
|------|------|
| `Message_{relationId}` | 消息内容 |
| `MessageAttachment_{relationId}` | 消息附件 |
| `MessageStatus_{relationId}` | 消息状态（已发送/已送达/已读） |

`DynamicTableService` 负责分表的创建、结构同步和删除。

### SignalR 三层解耦架构

前端实时通信采用三层解耦设计：

```
SignalR Connection → SignalR Adapter → Event Bus → Handlers → Pinia Store → Vue 组件
```

- **Adapter**：将 Hub 事件 1:1 转发到事件总线
- **Event Bus**：发布/订阅通道，与 SignalR 完全解耦
- **Handlers**：订阅事件并调用 Pinia Store 方法更新状态

### 多连接在线状态管理

`ChatHub` 使用 `ConcurrentDictionary<Guid, HashSet<string>>` 追踪每个用户的多个 WebSocket 连接。仅在第一个连接建立或最后一个连接断开时广播状态变化，避免通知风暴。

## 项目文档

- 📖 [项目架构说明文档](./项目架构说明文档.md) — 详细的架构设计、模块说明、API 端点清单、SignalR 事件规范、数据流时序
- 🗄️ [数据库设置](./WeiDin.API/DatabaseSetup.md) — 数据库配置说明

## 贡献指南

1. Fork 项目
2. 创建功能分支 (`git checkout -b feature/AmazingFeature`)
3. 提交更改 (`git commit -m 'Add some AmazingFeature'`)
4. 推送到分支 (`git push origin feature/AmazingFeature`)
5. 打开 Pull Request

## 许可证

本项目采用 MIT 许可证 — 查看 [LICENSE](LICENSE) 文件了解详情。

---

**注意**：这是一个毕业设计项目，主要用于学习和演示目的。在生产环境中使用前，请确保进行充分的安全测试和性能优化。
