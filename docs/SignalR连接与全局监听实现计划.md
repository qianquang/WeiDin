# SignalR 连接与全局监听实现计划

## 一、目标

1. **连接生命周期**：SignalR 连接在用户登录后建立，在用户退出登录或关闭浏览器时断开。
2. **全局监听器**：在任意页面都能接收 SignalR 推送，由统一入口处理。
3. **界面联动**：收到推送后，将数据写入 Pinia；用户切换到对应功能页时，页面基于 Store 数据自动更新 UI。

---

## 二、现状与问题

| 项目 | 现状 | 问题 |
|------|------|------|
| 连接时机 | App.vue `onMounted` 中，若 `initUser` 后 `isLoggedIn` 则 `initConnection` | 刷新页面也会连，本质是「有 token 即连」，未严格绑定登录动作 |
| 断开时机 | `useSignalR` 的 `onUnmounted` 调用 `stopConnection` | 用在 store 中，store 常驻，卸载的是视图；且未在登出时显式断开 |
| 监听位置 | `chat.ts` 仅有聊天相关；`Friends.vue` 单独注册好友相关 | 分散、重复，且依赖先进入某页才能注册，易漏接或时序错乱 |
| 数据与 UI | 好友等在页面内 `loadPendingRequests` 等 | 收讯与 UI 强绑定在具体页面，切换页时无法统一利用推送数据 |

---

## 三、架构设计

### 3.1 总体思路

```
登录成功 → 建立 SignalR 连接 → 注册全局监听器（唯一处）
                    ↓
            收到推送 → 更新 Pinia（如 friendshipStore、chatStore）
                    ↓
            用户切换到 /friends、/chat 等 → 页面从 Store 读数据 → 自动更新 UI

登出 / 关闭浏览器 → 断开 SignalR
```

- **单一连接、单一监听入口**：连接与所有 SignalR 服务端事件的注册、处理，只在一处（新建的 `signalr` store 或扩展后的统一模块）完成。
- **Store 为数据重心**：推送只负责更新 Store；各页只消费 Store，不直接收 SignalR。
- **生命周期与登录绑定**：连接建立 = 登录成功后，断开 = 登出或 `beforeunload`。

### 3.2 模块职责划分

| 模块 | 职责 |
|------|------|
| **SignalR 连接与生命周期** | 登录后建连、登出/关页断开；不负责业务数据处理 |
| **全局监听器（统一入口）** | 注册所有 Hub 事件，收到后只做解析 + 调用各 store 的更新方法 |
| **FriendshipStore** | 好友/申请等状态；提供 `setPendingRequests`、`appendPendingRequest` 等，供全局监听器调用 |
| **ChatStore** | 消息、会话、未读等；提供 `addMessage`、`incrementUnread` 等，供全局监听器调用 |
| **Friends / Chat / Groups 等页面** | 仅从 Store 读数据、调 API 拉列表；不直接 `conn.on` |

---

## 三（附）、更优雅的全局监听设计

目标不变：**任意页面都能收到推送；用户切到对应功能页时，该页 UI 自动更新**。下面用更简洁、可扩展的方式实现。

### 核心思路：事件总线 + 失效标记 + 按域注册

```
SignalR 收到事件 → 转发到「实时事件总线」→ 各域注册的 handler 执行
                                                        ↓
                                   更新 Store 和/或 标记「某域数据已失效」
                                                        ↓
用户切到 /friends、/chat → 页面 onMounted/onActivated 检查「是否失效」
                                                        ↓
                                   若失效则 refetch → 更新 Store → UI 更新
```

- **事件总线**：与 SignalR 解耦的 pub/sub。总线只做 `on` / `off` / `emit`，不关心谁发、谁用。
- **失效标记**：handler 不一定要把推送的完整数据写 Store；可以只做「某域数据失效了」。页面进入时发现失效就拉接口，拉完清掉标记。
- **按域注册**：每个业务域（好友、聊天、群组等）自己 `on(事件, handler)`，总线不写死一大坨 if/switch。

### 1. 实时事件总线 `realtime`

**职责**：提供 `on(event, handler)`、`off(event, handler)`、`emit(event, payload)`；维护 `Map<event, Set<handler>>`。

- 不依赖 SignalR、不依赖 Pinia。
- 唯一与 SignalR 的衔接：**连接建立后**，在适配层里对每个 Hub 事件做 `conn.on(name, (data) => realtime.emit(name, data))`，其余全部走总线。

### 2. 失效标记 `useInvalidation`（小型 store 或 composable）

**职责**：记录「哪些域的数据可能过期了」。

- `invalidations: Record<string, boolean>`，例如 `{ friends: true, chat: true }`。
- `markStale(domain)`：某域有推送 → `invalidations[domain] = true`。
- `clear(domain)`：页面 refetch 完 → `invalidations[domain] = false`。
- `isStale(domain)`：页面 `onMounted` / `onActivated` 时用来决定要不要 refetch。

 handler 里可以**只**调用 `markStale('friends')`，不碰 Store；也可在需要时既更新 Store 又 `markStale`（例如新消息直接 append，同时把 `chat` 标失效以便未读等二次校准）。

### 3. 按域注册 handler（推荐集中在一个模块）

- **注册时机**：应用启动时（如 `main.ts` 或已登录下的根组件）**只执行一次** `registerRealtimeHandlers()`。Handler 一直监听总线，与连接无关。
- **连接/断开**：仅 `adapter.attach(conn)` / `adapter.detach(conn)` 在建连 / 断连时调用；负责把 Hub 事件转发到总线，或取消转发。不断开、不移除 handler。

在 `realtime/handlers.ts`（或按域拆成 `realtime/handlers/friends.ts`、`chat.ts` 等）里：

```ts
// 伪代码
import { realtime } from './bus'
import { useInvalidation } from '@/stores/invalidation'
import { useFriendshipStore } from '@/stores/friendship'

export function registerRealtimeHandlers() {
  const inv = useInvalidation()
  const friendship = useFriendshipStore()

  realtime.on('PendingFriendRequestsLoaded', (data) => {
    friendship.setPendingRequests(data)
  })
  realtime.on('FriendRequestReceived', (data) => {
    inv.markStale('friends')
    // 可选：friendship.appendPendingRequest(data)，或仅 invalidate
  })
  realtime.on('FriendRequestAccepted', (data) => {
    inv.markStale('friends')
    // 可选：更新 store
  })
  // ...
}
```

- 所有「收推送 → 更新 Store / 标失效」的逻辑都在这里，**页面里不再出现任何 `conn.on`**。
- 新事件、新域只需加 `realtime.on(...)` 和对应的 store / invalidation 调用，易扩展。

### 4. 页面的唯一职责：消费 Store + 按需 refetch

以 Friends 页为例：

```ts
// Friends.vue
const inv = useInvalidation()
const friendship = useFriendshipStore()

onMounted(async () => {
  if (inv.isStale('friends')) {
    await friendship.loadPendingRequests()
    await friendship.loadFriends()
    inv.clear('friends')
  }
})

onActivated(async () => {
  // 若使用 keep-alive，切换回该页时再检查一次
  if (inv.isStale('friends')) {
    await friendship.loadPendingRequests()
    await friendship.loadFriends()
    inv.clear('friends')
  }
})
```

- 页面不关心 SignalR、不关心事件名；只关心「好友域有没有失效」和「拉接口 → 清失效」。
- 这样即使用户在其它页收到好友相关推送，只要 `markStale('friends')` 被调了，**一旦切回 /friends，就会 refetch 并更新 UI**。

### 5. 效果小结

| 点 | 做法 |
|----|------|
| **在哪收推送** | 任意页面；底层始终是「SignalR → 总线 → handler」，与当前路由无关。 |
| **何时更新 UI** | 用户**切到**对应功能页时：`onMounted` / `onActivated` 发现 `isStale(域)` → refetch → 更新 Store → UI 响应式更新。 |
| **谁写业务逻辑** | 各域 handler 注册到总线；页面只负责「失效则拉、拉完清掉」。 |
| **优雅性** | 总线与传输解耦、按域注册、失效驱动 refetch，无巨型 switch、无页面级 `conn.on`。 |

### 6. 可选增强

- **即时更新**：对需要「看到即更新」的数据（如新聊天消息），在 handler 里直接 `chatStore.addMessage(data)`；同时可 `markStale('chat')` 做未读等二次校准。
- **统一 Toast**：在 handler 里根据事件调用 `ElMessage.info` 等（例如「收到好友申请」），避免在页面里拆 SignalR 逻辑。
- **重连**：仅重建连接并重新 `conn.on(...) → realtime.emit(...)`， handler 注册在应用启动时完成一次即可，无需随重连而变。

---

## 四、实现计划（分步）

### 阶段 1：连接生命周期与统一管理

**1.1 调整 `useSignalR`（`utils/signalr.ts`）**

- 移除 `onUnmounted` 里对 `stopConnection` 的调用，避免与 store 生命周期混淆。
- 保留 `startConnection` / `stopConnection` 为纯方法，由外部按登录/登出主动调用。
- 若有 `beforeunload` 逻辑，可保留在此工具内，或放在调用方（见下）。

**1.2 新建 `signalr` Store（或保留在 `chat` 中集中管理，二选一）**

- 持有 `useSignalR` 的 `connection`、`startConnection`、`stopConnection`。
- 提供：
  - `initConnection()`：`startConnection()`，并在连接成功后 **仅在此处** 注册全局监听器（见阶段 2）。
  - `disconnect()`：`stopConnection()`，并清理所有 `conn.off(...)`。
- 不在 store 的 `onUnmounted` 里断连；断开只由登录/登出/关页触发。

**1.3 连接建立时机：登录后**

- **登录**：在 `authStore.login`（及 `register`，若注册即登录）成功、写 token/user 后，调用 `signalrStore.initConnection()`。
- **刷新 / 已有 token**：在 `App.vue` 的 `onMounted` 中，`authStore.initUser()` 完成后，若 `isLoggedIn`，再调用 `signalrStore.initConnection()`。  
  这样：首次登录 → 登录逻辑里建连；刷新 → `initUser` 后建连。

**1.4 连接断开时机：登出 + 关浏览器**

- **登出**：在 `authStore.logout` 中，**在清 token/user 之前**，调用 `signalrStore.disconnect()`，再执行原有登出逻辑。
- **关闭/刷新浏览器**：在 `App.vue` 或 `main.ts` 中监听 `window` 的 `beforeunload`，在此调用 `disconnect()`。  
  注意：`beforeunload` 中只能做同步或 `navigator.sendBeacon` 等有限操作，若 `stopConnection` 是异步的，可考虑只做标记 + 短时 `await`，或仅做「尽量发送关闭」的尝试。

**1.5 与 `chat` Store 的关系**

- 若 Chat 仍负责会话、消息等业务状态：  
  - 方案 A：Chat 只做业务 Store；SignalR 连接 + 全局监听放在新的 `signalr` store。  
  - 方案 B：不新增 store，把连接与全局监听都迁到 `chat` store，由 `chat` 统一管理生命周期。  
- 无论 A/B，**全局监听器只有一处**，且建连后立即注册，避免遗漏或重复。

---

### 阶段 2：全局监听器与 Store 更新

**2.1 全局监听器注册位置**

- 仅在 `initConnection` 成功之后、在创建连接的那个 store 内注册。
- 使用 `connection.value.on('...', handler)` 注册所有服务端会推送的事件，例如：
  - `PendingFriendRequestsLoaded`
  - `FriendRequestReceived`
  - `FriendRequestAccepted`
  - `FriendRequestRejected`
  - `NewFriendAdded`
  - `ReceiveMessage` / `ReceiveGroupMessage`
  - `UserStatusChanged`
  - `MessageRead` / `MessageDelivered`
  - 其他群组、通知等事件（按现有后端枚举）。

**2.2 处理逻辑原则**

- Handler 内只做：解析 payload → 调用对应 store 的更新方法。
- 不依赖当前路由；不依赖具体挂载的页面组件。

**2.3 各事件与 Store 的对应关系（示例）**

| 事件 | 调用 Store | 更新方法（示例） |
|------|------------|------------------|
| `PendingFriendRequestsLoaded` | friendshipStore | `setPendingRequests(data)` |
| `FriendRequestReceived` | friendshipStore | `appendPendingRequest(data)` 或 `invalidatePending()` 触发重拉 |
| `FriendRequestAccepted` | friendshipStore | `removeSentRequest(id)`、`appendFriend(data)` 等 |
| `FriendRequestRejected` | friendshipStore | `removeSentRequest(id)` |
| `NewFriendAdded` | friendshipStore | `appendFriend(data)` |
| `ReceiveMessage` | chatStore | `addMessage(data)`、`incrementUnread(sessionId)` 等 |
| `ReceiveGroupMessage` | chatStore | 同上，按群维度 |
| `UserStatusChanged` | chatStore / 或专门 userStore | 更新对应用户在线状态 |
| `MessageRead` / `MessageDelivered` | chatStore | 更新消息状态、已读角标等 |

具体方法名、参数以你现有 store 设计为准；这里只约定「由全局 handler 调 store，不调页面」。

**2.4 新建或扩展 FriendshipStore**

- 若尚无独立 `friendship` store：新建 `stores/friendship.ts`，集中存放：
  - `friends`、`pendingRequests`、`sentRequests`、`blacklist` 等。
  - `setPendingRequests`、`appendPendingRequest`、`loadPendingRequests`（调 API）、`loadFriends` 等。
- `Friends.vue` 只从 `friendshipStore` 取数据并展示；收到推送后由全局 listener 更新 store，页面自动响应。

---

### 阶段 3：页面与 UI 更新

**3.1 移除页面内对 SignalR 的直接监听**

- 在 `Friends.vue` 等中，删除对 `conn.on('PendingFriendRequestsLoaded', ...)`、`FriendRequestReceived` 等的注册与 `conn.off`。
- 这些页面不再使用 `useSignalR` 或 `chatStore.connection` 做事件监听。

**3.2 页面只消费 Store**

- `Friends.vue`：  
  - 使用 `friendshipStore.pendingRequests`、`friends` 等计算属性或 ref。  
  - 进入页时若需刷新，可调用 `friendshipStore.loadPendingRequests()` 等；日常更新完全依赖全局推送对 store 的更新。
- `Chat.vue`：  
  - 使用 `chatStore` 的会话、消息、未读等。  
  - 同上，列表/未读等由 store 驱动，推送仅更新 store。

**3.3 切换界面时的 UI 更新**

- 无需额外「切换时再拉一次」逻辑即可正确展示：只要推送到了就更新 store，页面始终绑的是 store。
- 若希望「进入某页时强制刷新一次」以校准离线期间的变更，可在对应页的 `onMounted` 或 `onActivated` 中调用 `loadPendingRequests`、`loadMessages` 等 API 方法，仍通过 store 更新后再渲染。

---

### 阶段 4：重连、beforeunload 与边界情况

**4.1 自动重连**

- 保持 `withAutomaticReconnect`。重连成功后，若当前连接实例未销毁，一般无需重新注册 `on`；若 Hub 服务端要求重连后重新加入 Group，在 `onreconnected` 里调 `JoinGroup` 等。

**4.2 登出时 token 清除顺序**

- 先 `signalrStore.disconnect()`，再 `localStorage` 清 token、清 user，避免断开过程中仍用已失效 token 请求。

**4.3 beforeunload**

- 仅用于关闭/刷新页时尽量断开连接；不断开时，服务端通常也会在心跳超时后清理。  
- 若 `disconnect` 为 async，可 `beforeunload` 里同步调用并忽略未完成的 `await`，或使用「同步标记 + 短时等待」折中。

**4.4 重复注册与清理**

- 全局监听器只在 `initConnection` 成功时注册一次；`disconnect` 时对所有已注册事件 `conn.off`，避免重复注册或卸组件时误删全局 handler。

---

## 五、文件与改动清单（建议）

### 采用「事件总线 + 失效标记」方案时

| 文件 | 改动概要 |
|------|----------|
| `realtime/bus.ts`（新建） | 事件总线：`on` / `off` / `emit`；`Map<event, Set<handler>>` |
| `realtime/adapter.ts`（新建） | SignalR 适配：`attach(conn)` 将 Hub 事件转发到 `bus.emit`；`detach(conn)` 做 `conn.off` |
| `realtime/handlers.ts`（新建） | 按域注册 `realtime.on(...)`，内部调 store / `useInvalidation`；应用启动时 `registerRealtimeHandlers()` |
| `stores/invalidation.ts`（新建） | `markStale(domain)`、`clear(domain)`、`isStale(domain)` |
| `utils/signalr.ts` | 去掉 `onUnmounted` 断连；仅导出 `start`/`stop`；由外部调 `adapter.attach` / `detach` |
| `stores/signalr.ts`（新建）或扩展现有 | 管理连接生命周期、`initConnection` / `disconnect`；建连后 `adapter.attach(conn)`，断连前 `adapter.detach(conn)`。`registerRealtimeHandlers()` 在应用启动时调用一次，与连接无关 |
| `stores/friendship.ts` | 新建或扩展：`setPendingRequests`、`loadPendingRequests` 等；供 handlers 与页面使用 |
| `stores/auth.ts` | `login`/`register` 成功后 `initConnection`；`logout` 中先 `disconnect` 再清 token |
| `App.vue` | `initUser` 后若已登录则 `initConnection`；`beforeunload` 时 `disconnect` |
| `views/Friends.vue` | 移除所有 `conn.on`/`conn.off`；用 `friendshipStore` + `useInvalidation`；`onMounted`/`onActivated` 里 `isStale('friends')` → refetch → `clear('friends')` |
| `views/Chat.vue` | 同上思路，仅消费 store + invalidation；若有 `conn.on` 迁移到 handlers |
| `router` | 无需改 |

---

## 六、实施顺序建议

1. **阶段 1**：生命周期（登录建连、登出/关页断开）、统一连接管理（含 `init`/`disconnect`）。  
2. **阶段 2**：在唯一连接管理处注册全局监听器，并实现「收事件 → 更 store」的映射；若有缺失的 store 方法则补齐。  
3. **阶段 3**：Friends/Chat 等页面移除 `conn.on`，改为纯 store 消费；验证推送 → store → UI 全链路。  
4. **阶段 4**：重连、beforeunload、登出顺序等收尾与边界 case。

---

## 七、验收要点

- 登录后建立连接；登出或关闭标签页后连接断开（可通过日志或 Hub 侧确认）。  
- 在任意页面（如 Settings、Profile）登录态下，另一端触发好友申请、发消息等，Store 中对应数据会更新。  
- 再切换到 Friends、Chat 等页，无需手动刷新即可看到最新列表/未读等。  
- 无重复注册、无 `"No client method ... found"` 等告警；刷新页面后依然行为正确。

---

*文档版本：1.0 | 日期：2026-01-28*
