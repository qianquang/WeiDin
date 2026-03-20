# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

WeiDin is an instant messaging system built with ASP.NET Core 8.0 (backend) and Vue.js 3 (frontend). It follows Domain-Driven Design (DDD) layered architecture using ABP Framework as the foundation. The system provides user management, real‑time messaging, group chats, friend management, and file sharing.

## Architecture

The solution consists of five main projects:

- **WeiDin.Core** – Domain layer with entities, enums, and core interfaces.
- **WeiDin.Application** – Application services, DTOs, and business logic.
- **WeiDin.Infrastructure** – Data access (Entity Framework Core), dynamic table management, and repository implementations.
- **WeiDin.API** – ASP.NET Core Web API with SignalR hub, JWT authentication, and Swagger.
- **WeiDin.Frontend** – Vue.js 3 SPA with Element Plus UI, Pinia state management, and SignalR client.

**Dependency flow**: Frontend → API → (Application + Infrastructure) → Core.

**Key architectural concepts**:

- **Dynamic message tables**: Each conversation (private or group) has its own `Message_{relationId}`, `MessageAttachment_{relationId}`, and `MessageStatus_{relationId}` tables, managed by `DynamicTableService`.
- **Real‑time communication**: SignalR hub (`ChatHub`) with multi‑connection online‑state tracking and event‑forwarding architecture.
- **Layered communication**: Frontend uses a three‑layer real‑time stack: SignalR adapter → event bus → Pinia store handlers.

## Common Development Tasks

### Backend (ASP.NET Core)

- **Build the solution**:
  ```bash
  dotnet build
  ```

- **Run the API project** (listens on `https://localhost:7000` and `http://localhost:5000`):
  ```bash
  cd WeiDin.API
  dotnet run
  ```

- **Apply database migrations** (requires SQL Server instance; connection string in `appsettings.json`):
  ```bash
  cd WeiDin.Infrastructure
  dotnet ef database update
  ```

- **Create a new migration** (after entity changes):
  ```bash
  dotnet ef migrations add <MigrationName> --project WeiDin.Infrastructure --startup-project WeiDin.API
  ```

- **Run with Docker Compose** (starts SQL Server, Redis, and the API container):
  ```bash
  docker-compose up -d
  ```

### Frontend (Vue.js 3)

- **Install dependencies**:
  ```bash
  cd WeiDin.Frontend
  npm install
  ```

- **Start development server** (proxies API requests to `https://localhost:7000`):
  ```bash
  npm run dev
  ```

- **Build for production**:
  ```bash
  npm run build
  ```

- **Lint and type‑check**:
  ```bash
  npm run lint
  npm run type-check
  ```

### Database Setup

- Use the scripts in `WeiDin.API/Scripts/` to initialize the database (run `SetupDatabase.bat` on Windows or `./SetupDatabase.sh` on Unix).
- The SQL script `InitDatabase.sql` creates the main tables; dynamic conversation tables are created on‑the‑fly by the application.

## Key Implementation Patterns

### SignalR Events and Client‑Side Handling

The frontend real‑time layer is deliberately decoupled:

1. **SignalR adapter** (`src/realtime/adapter.ts`) forwards hub events to an internal event bus.
2. **Event bus** (`src/realtime/bus.ts`) provides a publish‑subscribe channel independent of SignalR.
3. **Handlers** (`src/realtime/handlers.ts`) subscribe to the bus and update Pinia stores.

When adding a new server‑pushed event:
- Define the event name in `ChatHub` (server) and add the corresponding `On` method in the adapter.
- Add a handler that calls the appropriate store method.
- The store updates reactive state, which automatically refreshes Vue components.

### Online‑State Management

`ChatHub` maintains a static `ConcurrentDictionary<Guid, HashSet<string>>` that tracks all active WebSocket connections per user. A user is considered “online” if at least one connection exists. State‑change broadcasts are only sent when the first connection appears or the last connection disappears.

### Dynamic Table Creation

When a new friendship or group is activated, the `DynamicTableService` creates three partitioned tables (`Message_{relationId}`, `MessageAttachment_{relationId}`, `MessageStatus_{relationId}`) that mirror the structure of the main template tables. All message CRUD operations for that conversation use `DynamicMessageRepository` which routes queries to the appropriate physical table.

## File and Naming Conventions

- **Backend**: PascalCase for classes, methods, and properties; `I` prefix for interfaces.
- **Frontend**: camelCase for variables and functions; PascalCase for Vue components and TypeScript types.
- **API endpoints**: Follow RESTful conventions under `/api/v1/` (e.g., `/api/v1/messages`).
- **SignalR hub methods**: Use descriptive names like `SendMessageToUser`, `JoinGroup`.

## Important Paths

- **API Controllers**: `WeiDin.API/Controllers/`
- **SignalR Hub**: `WeiDin.API/Hubs/ChatHub.cs`
- **Application services**: `WeiDin.Application/Services/`
- **Domain entities**: `WeiDin.Core/Entities/`
- **Frontend stores**: `WeiDin.Frontend/src/stores/`
- **Real‑time layer**: `WeiDin.Frontend/src/realtime/`
- **Database migrations**: `WeiDin.Infrastructure/Migrations/`

## Notes for Contributors

- The backend uses ABP Framework modules; check `*Module.cs` files for dependency injection setup.
- JWT authentication is configured in `WeiDinApiModule.cs`.
- The frontend Vite dev server proxies API requests to avoid CORS issues.
- Redis is used for caching (optional); connection string is in `appsettings.json`.
- Detailed architecture documentation is available in `项目架构说明文档.md` (Chinese).

## Quick Start

1. Start database and Redis:
   ```bash
   docker-compose up -d sqlserver redis
   ```

2. Apply migrations:
   ```bash
   dotnet ef database update --project WeiDin.Infrastructure --startup-project WeiDin.API
   ```

3. Run the backend:
   ```bash
   cd WeiDin.API
   dotnet run
   ```

4. In another terminal, start the frontend:
   ```bash
   cd WeiDin.Frontend
   npm install
   npm run dev
   ```

5. Open `http://localhost:5173` (or the URL shown by Vite) and log in with a registered user.