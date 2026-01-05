# 微钉即时通讯系统 - 前端

基于 Vue 3 + TypeScript + Element Plus 开发的现代化即时通讯系统前端应用。

## 🚀 技术栈

- **框架**: Vue 3.4
- **语言**: TypeScript
- **UI组件库**: Element Plus
- **状态管理**: Pinia
- **路由**: Vue Router
- **HTTP客户端**: Axios
- **实时通信**: SignalR
- **构建工具**: Vite
- **时间处理**: Day.js

## 📁 项目结构

```
src/
├── api/                    # API接口
│   ├── auth.ts            # 认证相关API
│   ├── message.ts         # 消息相关API
│   ├── group.ts           # 群组相关API
│   ├── friendship.ts      # 好友相关API
│   ├── file.ts            # 文件相关API
│   ├── request.ts         # Axios配置
│   └── index.ts           # API导出
├── assets/                # 静态资源
├── components/            # 公共组件
├── router/                # 路由配置
│   └── index.ts
├── stores/                # Pinia状态管理
│   ├── auth.ts           # 用户认证状态
│   └── chat.ts           # 聊天状态
├── types/                 # TypeScript类型定义
│   └── index.ts
├── utils/                 # 工具函数
│   ├── index.ts          # 通用工具
│   └── signalr.ts        # SignalR工具
├── views/                 # 页面组件
│   ├── Login.vue         # 登录页
│   ├── Register.vue      # 注册页
│   ├── Chat.vue          # 聊天主页面
│   ├── Profile.vue       # 个人资料页
│   ├── Friends.vue       # 好友管理页
│   ├── Groups.vue        # 群组管理页
│   ├── Settings.vue      # 设置页
│   └── NotFound.vue      # 404页面
├── App.vue               # 根组件
└── main.ts               # 入口文件
```

## 🛠️ 开发环境

### 环境要求

- Node.js >= 16.0.0
- npm >= 8.0.0 或 yarn >= 1.22.0

### 安装依赖

```bash
# 使用npm
npm install

# 或使用yarn
yarn install
```

### 启动开发服务器

```bash
# 使用npm
npm run dev

# 或使用yarn
yarn dev
```

访问 http://localhost:3000

### 构建生产版本

```bash
# 使用npm
npm run build

# 或使用yarn
yarn build
```

### 预览生产版本

```bash
# 使用npm
npm run preview

# 或使用yarn
yarn preview
```

## 🔧 配置说明

### 环境变量

创建 `.env.local` 文件：

```env
# API基础URL
VITE_API_BASE_URL=http://localhost:7000/api/v1

# SignalR Hub URL
VITE_SIGNALR_HUB_URL=http://localhost:7000/chatHub

# 应用标题
VITE_APP_TITLE=微钉即时通讯系统
```

### 代理配置

开发环境已配置代理，将 `/api` 和 `/chatHub` 请求代理到后端服务器。

## 📱 功能特性

### 用户认证
- ✅ 用户注册/登录
- ✅ JWT Token认证
- ✅ 自动登录状态保持
- ✅ 用户信息管理

### 即时通讯
- ✅ 私聊消息
- ✅ 群聊消息
- ✅ 实时消息推送
- ✅ 消息状态管理（已发送/已送达/已读）
- ✅ 多媒体消息支持（图片/视频/文件）
- ✅ 消息搜索

### 好友管理
- ✅ 添加/删除好友
- ✅ 好友列表管理
- ✅ 好友备注设置
- ✅ 黑名单功能

### 群组管理
- ✅ 创建/加入群组
- ✅ 群成员管理
- ✅ 群组设置
- ✅ 群组权限控制

### 文件管理
- ✅ 文件上传/下载
- ✅ 图片预览
- ✅ 文件类型检测
- ✅ 文件大小限制

### 实时通信
- ✅ SignalR WebSocket连接
- ✅ 自动重连机制
- ✅ 在线状态同步
- ✅ 消息实时推送

## 🎨 UI设计

- 采用Element Plus组件库
- 响应式设计，支持移动端
- 现代化界面风格
- 深色/浅色主题支持（计划中）

## 🔒 安全特性

- JWT Token认证
- 请求拦截器自动添加认证头
- 响应拦截器统一错误处理
- 敏感信息保护

## 📦 构建优化

- Vite构建工具，快速热更新
- 代码分割，按需加载
- 生产环境代码压缩
- 静态资源优化

## 🚀 部署

### 构建

```bash
npm run build
```

### 部署到Nginx

```nginx
server {
    listen 80;
    server_name your-domain.com;
    
    location / {
        root /path/to/dist;
        try_files $uri $uri/ /index.html;
    }
    
    location /api {
        proxy_pass http://localhost:7000;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
    }
    
    location /chatHub {
        proxy_pass http://localhost:7000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_set_header Host $host;
    }
}
```

### Docker部署

```dockerfile
FROM node:16-alpine as build-stage
WORKDIR /app
COPY package*.json ./
RUN npm ci --only=production
COPY . .
RUN npm run build

FROM nginx:stable-alpine as production-stage
COPY --from=build-stage /app/dist /usr/share/nginx/html
COPY nginx.conf /etc/nginx/nginx.conf
EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
```

## 🤝 贡献指南

1. Fork 项目
2. 创建功能分支 (`git checkout -b feature/AmazingFeature`)
3. 提交更改 (`git commit -m 'Add some AmazingFeature'`)
4. 推送到分支 (`git push origin feature/AmazingFeature`)
5. 打开 Pull Request

## 📄 许可证

本项目采用 MIT 许可证 - 查看 [LICENSE](LICENSE) 文件了解详情

## 📞 联系方式

如有问题或建议，请通过以下方式联系：

- 项目Issues: [GitHub Issues](https://github.com/your-repo/issues)

---

**注意**: 这是一个毕业设计项目，主要用于学习和演示目的。在生产环境中使用前，请确保进行充分的安全测试和性能优化。
