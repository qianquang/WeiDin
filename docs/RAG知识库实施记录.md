# RAG 知识库实施记录

## 1. 文档信息

- **创建时间**：2026-06-03
- **最后更新**：2026-06-04
- **实施人**：xiangtengyi
- **工具**：Claude Code（Opus 4.8）

---

## 2. 整体实施进度

```
[✅] 阶段一：集成 SK → Kernel → DeepSeek API → ChatCompletionService
[✅] 阶段二：Dify 知识库迁移 → Core/Infrastructure 层搭建
[✅] 阶段三：RAG Agent Controller + 智能路由（直接对话 vs RAG）
[✅] 阶段四：分批向量化 + FAISS 向量存储 + 精排 Rerank
[✅] 阶段五：存储分离 + 异步入库管线 + SK VectorStore 连接器
[⬜] 阶段六：高级特性（摘要、图谱、多模态）
```

---

## 3. 最新架构（2026-06-04 更新）

### 存储分离架构

```
┌─────────────────────────────────────────────────────────┐
│                    Application Layer                     │
│  RetrievalService / DocumentIngestionService             │
│       ↓ VectorStore (SK 抽象)                           │
│       ↓ IEmbeddingService (ONNX 本地模型)               │
└──────────┬──────────────────────────────┬───────────────┘
           │                              │
  ┌────────▼────────┐          ┌──────────▼──────────┐
  │   SQL Server    │          │  VectorStore        │
  │  (EF Core)      │          │  (InMemoryVectorStore)│
  │                 │          │                     │
  │  KnowledgeBase  │          │  Collection per KB  │
  │  Knowledge      │          │  "kb_{kbId}"        │
  │  Chunk          │          │  ChunkVectorRecord  │
  │  (无 Vector 列) │          │  (Id, Content,      │
  │                 │          │   Embedding,        │
  │                 │          │   KnowledgeBaseId)  │
  └─────────────────┘          └─────────────────────┘
                                     ↑
                              当前: InMemory (开发)
                              生产: Qdrant/Redis (待切换)
```

### 异步入库管线

```
POST /api/v1/knowledge/bases/{kbId}/documents/upload
  ├─ 保存文件到 wwwroot/uploads/knowledge/
  ├─ 创建 Knowledge 记录 (ParseStatus=pending)
  ├─ 向 Channel<T> 写入任务消息
  └─ 立即返回 { knowledgeId, fileName, parseStatus: "pending" }

BackgroundWorker (Channel 消费循环)
  ├─ 更新 Knowledge.ParseStatus → processing
  ├─ DocumentParser.ExtractTextAsync (解析 txt/pdf/docx/md)
  ├─ TextChunkingService.Chunk (分块)
  ├─ 创建 Chunk 实体 + 构建双向链表 → 写入 SQL Server
  ├─ 分批 EmbedBatchAsync (ONNX 向量化，每批10条)
  ├─ ChunkVectorRecord → VectorStore.UpsertAsync
  ├─ 更新 Knowledge.ParseStatus → completed
  └─ 异常时 → failed + ErrorMessage

GET /api/v1/knowledge/documents/{knowledgeId}/status
  └─ 返回 { knowledgeId, parseStatus, errorMessage, chunkCount }
```

### 数据模型（三层）

```
KnowledgeBase (知识库)
  ├── Id, Name, Description
  ├── IndexingStrategy: enum (Vector/Keyword/Wiki/Graph)
  ├── Knowledge: Collection<Knowledge>
  └── Chunks: Collection<Chunk>

Knowledge (知识条目 = 一个上传的文件)
  ├── Id, KnowledgeBaseId → KnowledgeBase
  ├── FileName, FilePath
  ├── ParseStatus: enum (pending/processing/completed/failed)
  ├── SummaryStatus: enum (none/pending/completed)
  └── CreatedAt

Chunk (分块 — SQL Server 只存结构化数据)
  ├── Id, KnowledgeId → Knowledge
  ├── KnowledgeBaseId (冗余字段)
  ├── Content, ChunkIndex
  ├── PreChunkId, NextChunkId (双向链表)
  └── CreatedAt

ChunkVectorRecord (向量存储记录 — SK VectorStore)
  ├── Id (Chunk.Id.ToString()) [VectorStoreKey]
  ├── KnowledgeBaseId [VectorStoreData, IsIndexed]
  ├── KnowledgeId [VectorStoreData, IsIndexed]
  ├── Content [VectorStoreData]
  └── Embedding [VectorStoreVector(512, Hnsw, CosineSimilarity)]
```

---

## 4. 技术栈

### Semantic Kernel

- **版本**：1.77.0（从 1.72.0 升级，以支持 VectorStore 抽象）
- **依赖链**：SK 1.77.0 → Abstractions 1.77.0 → Microsoft.Extensions.VectorData.Abstractions 10.1.0
- **使用场景**：
  - `OpenAIChatCompletionService`：调用 DeepSeek API
  - `VectorStore` / `VectorStoreCollection<TKey, TRecord>`：向量存储抽象
- **命名空间**：
  - 聊天补全：`Microsoft.SemanticKernel`
  - 向量存储：`Microsoft.Extensions.VectorData`

### 向量存储

- **接口**：`VectorStore`（抽象类）+ `VectorStoreCollection<TKey, TRecord>`
- **当前实现**：`InMemoryVectorStore` — 内存字典 + 余弦相似度暴力搜索
- **记录模型**：`ChunkVectorRecord` — 带 `[VectorStoreKey]`、`[VectorStoreData]`、`[VectorStoreVector]` 属性
- **Collection 命名**：每个知识库一个 collection，名称 = `$"kb_{knowledgeBaseId}"`
- **生产切换**：只需在 DI 注册中替换 `VectorStore` 实现（Qdrant/Redis/Postgres 等）

### Embedding 模型

- **模型**：BAAI/bge-small-zh-v1.5（512 维）
- **运行时**：ONNX Runtime 本地推理（`OnnxEmbeddingService`）
- **Tokenizer**：BERT WordPiece（自实现，加载 vocab.txt）

### Rerank 模型

- **模型**：BAAI/bge-reranker-base
- **运行时**：ONNX Runtime 本地推理（`OnnxRerankService`）
- **类型**：Cross-encoder（query+document pair → relevance score）

### LLM

- **模型**：DeepSeek v4-pro
- **调用方式**：SK `OpenAIChatCompletionService` → DeepSeek API（OpenAI 兼容）

---

## 5. 实施步骤详解

### 5.1 阶段一：SK 集成 + DeepSeek 聊天

> 2026-06-03 完成

- 安装 `Microsoft.SemanticKernel` + `Connectors.OpenAI`
- 创建 `DeepSeekLlmService`（Kernel → OpenAIChatCompletionService → DeepSeek API）
- 通过 SK 的 `ExtensionData` 传递 DeepSeek 特有参数（thinking、reasoning_effort）

### 5.2 阶段二：知识库基础设施

> 2026-06-03 完成

- 创建 `KnowledgeBase`、`KnowledgeChunk` 实体
- 创建 `KnowledgeBaseRepository`、`KnowledgeChunkRepository`
- 创建 `KnowledgeBaseController`（CRUD + 知识块管理 + 文档上传）
- 配置 EF Core DbContext + Migration

### 5.3 阶段三：RAG Agent

> 2026-06-03 完成

- 创建 `IQueryUnderstandingService`（查询改写 + 意图分类）
- 创建 `RagAgentEngine`（查询理解 → 检索 → Rerank → LLM 生成）
- 创建 `AgentController`（`POST /api/v1/agent/ask`）
- 意图路由：DirectChat 直接对话 vs KnowledgeQA 知识库问答

### 5.4 阶段四：向量化 + Rerank

> 2026-06-03 完成

- `OnnxEmbeddingService`：BERT WordPiece tokenizer + ONNX 推理 + MeanPooling + L2 归一化
- `OnnxRerankService`：Cross-encoder 精排（query+document pair → sigmoid score）
- 检索流程：粗排 3×topK → Rerank 精排 → 取 topK
- 向量存储在 SQL Server `KnowledgeChunk.Vector` (byte[]) 列

### 5.5 阶段五：存储分离 + 异步入库 + SK VectorStore

> 2026-06-04 完成

**核心变更**：
1. **SK 升级**：1.72.0 → 1.77.0（引入 `Microsoft.Extensions.VectorData.Abstractions`）
2. **存储分离**：SQL Server 只存结构化数据，向量通过 SK `VectorStore` 抽象存储
3. **异步入库**：`Channel<T>` + `BackgroundService` 消费队列，文件上传不阻塞
4. **三层数据模型**：KnowledgeBase → Knowledge → Chunk（Knowledge 跟踪文件解析状态）
5. **双向链表**：Chunk 的 PreChunkId/NextChunkId 为后续关联检索预留

**新增文件**：
- `Core/Entities/Knowledge.cs` — 知识条目实体
- `Core/Models/ChunkVectorRecord.cs` — SK 向量存储记录模型
- `Core/Enums/IndexingStrategy.cs` — 索引策略枚举
- `Core/Enums/ParseStatus.cs` — 解析状态枚举
- `Core/Enums/SummaryStatus.cs` — 摘要状态枚举
- `Core/Interfaces/IDocumentIngestionService.cs` — 入库服务接口
- `Infrastructure/Services/InMemoryVectorStore.cs` — 基于 SK VectorStore 的内存实现
- `Infrastructure/Background/DocumentProcessingWorker.cs` — 后台处理服务
- `Infrastructure/Repositories/KnowledgeRepository.cs` — Knowledge 仓储
- `Application/Services/Knowledge/DocumentIngestionService.cs` — 入库服务
- `Application/DTOs/DocumentIngestionDto.cs` — DTO
- `API/Controllers/KnowledgeController.cs` — 文件上传 + 状态查询

**删除文件**：
- `Core/Interfaces/IVectorIndexService.cs` — 被 SK VectorStore 替代
- `Infrastructure/Services/FaissIndexService.cs` — 被 InMemoryVectorStore 替代

**修改文件**：
- `KnowledgeChunk.cs`：删除 Vector/VectorDimension 列，KnowledgeBaseId→KnowledgeId，添加链表指针
- `KnowledgeBase.cs`：添加 IndexingStrategy、Knowledge 导航属性
- `RetrievalService.cs`：**重写**，从内存暴力搜索改为 SK VectorStore.SearchAsync
- `RagAgentEngine.cs`：传 knowledgeBaseId 给 RetrieveAsync
- 4 个 `.csproj`：SK 版本升级 + SKEXP0010 抑制

---

## 6. 文件结构

```
WeiDin.Core/
├── Entities/
│   ├── KnowledgeBase.cs          // 知识库实体（含 IndexingStrategy）
│   ├── Knowledge.cs              // 知识条目实体（跟踪解析状态）[新增]
│   └── KnowledgeChunk.cs         // 知识块实体（无 Vector 列，含链表指针）
├── Enums/
│   ├── IndexingStrategy.cs       // 索引策略枚举 [新增]
│   ├── ParseStatus.cs            // 解析状态枚举 [新增]
│   ├── SummaryStatus.cs          // 摘要状态枚举 [新增]
│   └── KnowledgeChunkType.cs
├── Models/
│   └── ChunkVectorRecord.cs      // SK 向量存储记录模型 [新增]
└── Interfaces/
    ├── IEmbeddingService.cs       // 向量嵌入接口
    ├── IRerankService.cs          // Rerank 精排接口
    ├── IRetrievalService.cs       // 检索接口（已适配 VectorStore）
    ├── IDocumentParser.cs         // 文档解析接口
    ├── ITextChunkingService.cs    // 文本分块接口
    ├── IDocumentIngestionService.cs // 入库服务接口 [新增]
    ├── IAgentEngine.cs            // Agent 引擎接口
    ├── ILlmService.cs             // LLM 接口
    └── IQueryUnderstandingService.cs

WeiDin.Infrastructure/
├── Services/
│   ├── OnnxEmbeddingService.cs   // ONNX 向量嵌入服务
│   ├── OnnxRerankService.cs      // ONNX Rerank 精排服务
│   ├── DocumentParser.cs         // 文档解析（txt/pdf/docx/md）
│   └── InMemoryVectorStore.cs    // SK VectorStore 内存实现 [新增]
├── Repositories/
│   ├── KnowledgeBaseRepository.cs
│   ├── KnowledgeChunkRepository.cs
│   └── KnowledgeRepository.cs    // Knowledge 仓储 [新增]
└── Background/
    └── DocumentProcessingWorker.cs // 异步入库后台服务 [新增]

WeiDin.Application/
├── Services/Knowledge/
│   ├── RetrievalService.cs       // 检索服务（SK VectorStore）[重写]
│   ├── KnowledgeBaseService.cs   // 知识库服务 [修改]
│   ├── DocumentIngestionService.cs // 入库服务 [新增]
│   ├── RagAgentEngine.cs         // RAG Agent 引擎 [修改]
│   ├── QueryUnderstandingService.cs // 查询理解
│   └── TextChunkingService.cs    // 文本分块
└── DTOs/
    ├── KnowledgeBaseDto.cs
    └── DocumentIngestionDto.cs    // 入库 DTO [新增]

WeiDin.API/Controllers/
├── AgentController.cs            // Agent 问答接口
└── KnowledgeController.cs        // 知识库管理 + 文件上传 [新增]
```

---

## 7. API 接口

### 7.1 知识库管理

| 方法 | 路径 | 说明 |
|------|------|------|
| POST | `/api/v1/knowledge/bases` | 创建知识库 |
| GET | `/api/v1/knowledge/bases` | 获取所有知识库 |
| GET | `/api/v1/knowledge/bases/{id}` | 获取知识库详情 |
| PUT | `/api/v1/knowledge/bases/{id}` | 更新知识库 |
| DELETE | `/api/v1/knowledge/bases/{id}` | 删除知识库（含清空向量） |

### 7.2 文档上传（异步入库）

| 方法 | 路径 | 说明 |
|------|------|------|
| POST | `/api/v1/knowledge/bases/{kbId}/documents/upload` | 上传文档（异步处理） |
| GET | `/api/v1/knowledge/documents/{knowledgeId}/status` | 查询处理状态 |

### 7.3 知识块管理

| 方法 | 路径 | 说明 |
|------|------|------|
| POST | `/api/v1/knowledge/bases/{kbId}/chunks` | 添加单个知识块 |
| POST | `/api/v1/knowledge/bases/{kbId}/chunks/batch` | 批量添加知识块 |
| GET | `/api/v1/knowledge/bases/{kbId}/chunks` | 获取知识块列表 |
| DELETE | `/api/v1/knowledge/bases/{kbId}/chunks/{chunkId}` | 删除知识块 |
| DELETE | `/api/v1/knowledge/bases/{kbId}/chunks` | 清空知识库 |

### 7.4 Agent 问答

| 方法 | 路径 | 说明 |
|------|------|------|
| POST | `/api/v1/agent/ask` | RAG 问答 |

---

## 8. 配置

### appsettings.json

```json
{
  "DeepSeek": {
    "ApiKey": "",
    "ModelId": "deepseek-v4-pro",
    "BaseUrl": "https://api.deepseek.com",
    "ThinkingEnabled": false,
    "ReasoningEffort": "high"
  },
  "VectorSearch": {
    "ModelPath": "Models",
    "ModelUrl": "",
    "IndexName": "chat_knowledge",
    "TopK": 5,
    "EnableRAG": true
  },
  "Rerank": {
    "ModelPath": "Models",
    "ModelName": "bge-reranker-base.onnx",
    "TopK": 5,
    "EnableRerank": true
  },
  "Ingestion": {
    "ChunkSize": 500,
    "ChunkOverlap": 50,
    "BatchSize": 10,
    "UploadDir": "wwwroot/uploads/knowledge"
  }
}
```

### 模型文件

```
Models/
├── bge-small-zh-v1.5.onnx        // Embedding 模型（~90MB）
├── vocab.txt                       // BERT 词表
└── bge-reranker-base.onnx          // Rerank 模型（~1.1GB）
```

---

## 9. 涉及的 NuGet 包

| 包名 | 版本 | 用途 |
|------|------|------|
| Microsoft.SemanticKernel | 1.77.0 | SK 核心（含 VectorData.Abstractions） |
| Microsoft.SemanticKernel.Connectors.OpenAI | 1.77.0 | DeepSeek API 调用 |
| Microsoft.ML.OnnxRuntime | 1.18.1 | ONNX 推理（Embedding + Rerank） |
| PdfPig | 0.1.10 | PDF 文本提取 |
| DocumentFormat.OpenXml | 3.2.0 | Word 文档解析 |

---

## 10. 待办事项

### 短期
- [ ] 生产环境向量存储：替换 `InMemoryVectorStore` 为 Qdrant/Redis 等
- [ ] EF Migration：为新增的 Knowledge 实体和修改的 Chunk 实体生成迁移
- [ ] 文件大小/类型校验：上传接口添加限制
- [ ] 前端对接：知识库管理页面 + 文件上传组件

### 中期
- [ ] 摘要自动生成（LLM 生成文档摘要，SummaryStatus 状态机）
- [ ] 更多文件格式支持（xlsx、pptx、html）
- [ ] 向量维度可配置（当前硬编码 512）
- [ ] 检索结果缓存（热门查询）

### 长期
- [ ] 知识图谱抽取（实体+关系 → 图数据库）
- [ ] 混合检索（向量 + 关键词 BM25）
- [ ] 多模态知识（图片、音频转文本后向量化）
- [ ] Agent 能力扩展（多步推理、工具调用、状态管理）
