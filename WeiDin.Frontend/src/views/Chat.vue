<template>
  <div class="chat-container">
    <!-- 侧边栏 -->
    <div class="sidebar">
      <!-- 用户信息 -->
      <div class="user-info">
        <el-avatar :size="40" :src="authStore.user?.avatar">
          {{ authStore.user?.nickname?.charAt(0) || authStore.user?.username?.charAt(0) }}
        </el-avatar>
        <div class="user-details">
          <div class="username">{{ authStore.user?.nickname || authStore.user?.username }}</div>
          <div class="status-container">
            <div class="status" :class="{ online: authStore.user?.isOnline }">
              {{ authStore.user?.isOnline ? '在线' : '离线' }}
            </div>
            <el-switch
              v-model="isOnlineStatus"
              size="small"
              @change="handleOnlineStatusChange"
              style="margin-left: 8px;"
            />
          </div>
        </div>
        <el-dropdown @command="handleUserCommand">
          <el-button type="text" :icon="MoreFilled" />
          <template #dropdown>
            <el-dropdown-menu>
              <el-dropdown-item command="profile">个人资料</el-dropdown-item>
              <el-dropdown-item command="settings">设置</el-dropdown-item>
              <el-dropdown-item command="logout" divided>退出登录</el-dropdown-item>
            </el-dropdown-menu>
          </template>
        </el-dropdown>
      </div>

      <!-- 搜索框 -->
      <div class="search-box">
        <el-input
          v-model="searchKeyword"
          placeholder="搜索聊天"
          :prefix-icon="Search"
          clearable
        />
      </div>

      <!-- 快捷操作栏 -->
      <div class="quick-actions">
        <el-button
          type="primary"
          :icon="UserFilled"
          size="small"
          class="quick-action-btn"
          @click="goToFriendsPage"
        >
          好友管理
        </el-button>
        <el-button
          type="success"
          :icon="Grid"
          size="small"
          class="quick-action-btn"
          @click="goToGroupsPage"
        >
          群组管理
        </el-button>
      </div>

      <!-- 聊天列表 -->
      <div class="chat-list">
        <div
          v-for="session in filteredSessions"
          :key="session.id"
          class="chat-item"
          :class="{ active: currentSessionId === session.id }"
          @click="selectSession(session)"
        >
          <el-avatar :size="40" :src="session.avatar">
            {{ session.name?.charAt(0) || '?' }}
          </el-avatar>
          <div class="chat-info">
            <div class="chat-name">{{ session.name }}</div>
            <div class="last-message">{{ getLastMessagePreview(session) }}</div>
          </div>
          <div class="chat-meta">
            <div class="time">{{ formatTime(session.lastMessage?.createdAt || '') }}</div>
            <el-badge v-if="session.unreadCount > 0" :value="session.unreadCount" class="unread-badge" />
          </div>
        </div>
        <div v-if="filteredSessions.length === 0" class="empty-chat-list">
          <el-empty description="暂无聊天记录" :image-size="100" />
        </div>
      </div>
    </div>

    <!-- 主聊天区域 -->
    <div class="main-content">
      <div v-if="!currentSessionId" class="welcome">
        <el-empty description="选择一个聊天开始对话" />
      </div>
      
      <div v-else class="chat-area">
        <!-- 聊天头部 -->
        <div class="chat-header">
          <div class="chat-title">
            <el-avatar :size="32" :src="currentSession?.avatar">
              {{ currentSession?.name?.charAt(0) || '?' }}
            </el-avatar>
            <div class="title-info">
              <div class="title-name">{{ currentSession?.name }}</div>
              <div class="title-status">
                {{ currentSession?.type === 'private' ? '私聊' : '群聊' }}
              </div>
            </div>
          </div>
          <div class="chat-actions">
            <div v-if="currentSession?.type === 'private'" class="online-status-indicator">
              <span class="status-dot" :class="{ online: currentSession?.isOnline }"></span>
              <span class="status-text">{{ currentSession?.isOnline ? '在线' : '离线' }}</span>
            </div>
          </div>
        </div>

        <!-- 消息列表 -->
        <div class="message-list" ref="messageListRef">
          <div
            v-for="message in currentMessages"
            :key="message.id"
            class="message-item"
            :class="{ 'own-message': message.senderId === authStore.userId }"
          >
            <el-avatar :size="32" :src="message.senderAvatar">
              {{ message.senderName?.charAt(0) || '?' }}
            </el-avatar>
            <div class="message-content">
              <div class="message-header">
                <span class="sender-name">{{ message.senderName }}</span>
                <span class="message-time">{{ formatTime(message.createdAt) }}</span>
              </div>
              <div class="message-body">
                <div v-if="message.messageType === 'Text'" class="text-message">
                  {{ message.content }}
                </div>
                <div v-else-if="message.messageType === 'Image'" class="image-message">
                  <el-image :src="message.content" fit="cover" />
                </div>
                <div v-else-if="message.messageType === 'File'" class="file-message">
                  <el-icon><Document /></el-icon>
                  <span>{{ message.attachments[0]?.fileName }}</span>
                </div>
              </div>
            </div>
          </div>
        </div>

        <!-- 消息输入框 -->
        <div class="message-input">
          <div class="input-toolbar">
            <el-button type="text" :icon="Picture" @click="handleImageUpload" />
            <el-button type="text" :icon="Paperclip" @click="handleFileUpload" />
            <el-button type="text" :icon="Microphone" />
          </div>
          <div class="input-area">
            <el-input
              v-model="messageText"
              type="textarea"
              :rows="3"
              placeholder="输入消息..."
              resize="none"
              @keydown.enter.exact="handleSendMessage"
            />
            <el-button
              type="primary"
              :icon="Position"
              :disabled="!messageText.trim()"
              @click="handleSendMessage"
            >
              发送
            </el-button>
          </div>
        </div>
      </div>
    </div>

    <!-- 创建群组对话框 -->
    <el-dialog v-model="showCreateGroupDialog" title="创建群组" width="400px">
      <el-form :model="createGroupForm" label-width="80px">
        <el-form-item label="群组名称">
          <el-input v-model="createGroupForm.name" placeholder="请输入群组名称" />
        </el-form-item>
        <el-form-item label="群组描述">
          <el-input v-model="createGroupForm.description" type="textarea" placeholder="请输入群组描述" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="showCreateGroupDialog = false">取消</el-button>
        <el-button type="primary" @click="handleCreateGroup">确定</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, nextTick, reactive, watch } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { ElMessage } from 'element-plus'
import {
  MoreFilled,
  Search,
  Picture,
  Paperclip,
  Microphone,
  Position,
  Document,
  UserFilled,
  Grid
} from '@element-plus/icons-vue'
import { useAuthStore } from '@/stores/auth'
import { useChatStore } from '@/stores/chat'
import { useFriendshipStore } from '@/stores/friendship'
import { useSignalRStore } from '@/stores/signalr'
import { groupApi, messageApi } from '@/api'
import type { CreateGroupDto, ChatSession, Friendship } from '@/types'
import dayjs from 'dayjs'

const router = useRouter()
const route = useRoute()
const authStore = useAuthStore()
const chatStore = useChatStore()
const friendshipStore = useFriendshipStore()
const signalrStore = useSignalRStore()

const searchKeyword = ref('')
const currentSessionId = ref<string | null>(null)
const messageText = ref('')
const messageListRef = ref<HTMLElement>()
const showCreateGroupDialog = ref(false)
const isOnlineStatus = ref(authStore.user?.isOnline || false)

watch(() => authStore.user?.isOnline, (v) => {
  if (v !== undefined) isOnlineStatus.value = v
}, { immediate: true })

const createGroupForm = reactive<CreateGroupDto>({
  name: '',
  description: '',
  maxMembers: 500
})

const filteredSessions = computed(() => {
  if (!searchKeyword.value) return chatStore.sessions
  return chatStore.sessions.filter(s =>
    s.name.toLowerCase().includes(searchKeyword.value.toLowerCase())
  )
})

const currentSession = computed(() => chatStore.currentSession)
const currentMessages = computed(() => chatStore.currentMessages)

// 当消息列表变化时自动滚动到底部（接收到新消息或发送消息后）
watch(currentMessages, () => {
  nextTick(() => scrollToBottom())
}, { deep: true })

function selectSession(session: ChatSession) {
  currentSessionId.value = session.id
  chatStore.setCurrentSession(session.id)
  chatStore.loadMessages(session.relationId)
}

async function ensureSessionsAndSelectFromQuery() {
  const uid = authStore.userId
  if (!uid) return

  await friendshipStore.loadFriends()
  const friends = friendshipStore.friends
  let groups: { id: string; name: string; avatar?: string }[] = []
  try {
    const list = await groupApi.getUserGroups(uid)
    groups = list.map((g: { id: string; name: string; avatar?: string }) => ({
      id: g.id,
      name: g.name,
      avatar: g.avatar
    }))
  } catch (e) {
    console.warn('加载用户群组失败:', e)
  }

  const sessions: ChatSession[] = []
  for (const f of friends) {
    const cid = f.conversationId
    if (!cid) continue
    sessions.push({
      id: cid,
      relationId: cid,
      type: 'private',
      name: f.friendName || '好友',
      avatar: f.friendAvatar,
      unreadCount: 0,
      friendId: f.friendId
    })
  }
  for (const g of groups) {
    sessions.push({
      id: g.id,
      relationId: g.id,
      type: 'group',
      name: g.name,
      avatar: g.avatar,
      unreadCount: 0
    })
  }
  sessions.forEach(s => chatStore.addSession(s))

  // 为每个会话加载最新一条消息，用于侧边栏显示
  // 好友在线状态由 SignalR 连接时通过 FriendsOnlineStatusLoaded 事件推送，无需逐个 HTTP 查询
  await Promise.all(
    sessions.map(async (s) => {
      try {
        const msgs = await messageApi.getByRelationId(s.relationId, { page: 1, pageSize: 1 })
        if (msgs.length > 0) {
          chatStore.updateSessionLastMessage(s.relationId, msgs[0])
        }
      } catch {
        // 忽略单个会话加载失败
      }
    })
  )

  const friendId = route.query.friend as string | undefined
  const groupId = route.query.group as string | undefined
  if (friendId) {
    const fr = friends.find((f: Friendship) => f.friendId === friendId)
    if (fr?.conversationId) {
      chatStore.setCurrentSession(fr.conversationId)
      currentSessionId.value = fr.conversationId
      await chatStore.loadMessages(fr.conversationId)
    } else {
      ElMessage.warning('无法发起会话，请确认已互为好友')
    }
  } else if (groupId) {
    const exists = sessions.some(s => s.id === groupId)
    if (exists) {
      chatStore.setCurrentSession(groupId)
      currentSessionId.value = groupId
      await chatStore.loadMessages(groupId)
    } else {
      const g = groups.find(gr => gr.id === groupId)
      if (g) {
        chatStore.addSession({
          id: g.id,
          relationId: g.id,
          type: 'group',
          name: g.name,
          avatar: g.avatar,
          unreadCount: 0
        })
        chatStore.setCurrentSession(g.id)
        currentSessionId.value = g.id
        await chatStore.loadMessages(g.id)
      }
    }
  }
}

const handleSendMessage = async () => {
  const txt = messageText.value.trim()
  const session = chatStore.currentSession
  if (!txt || !session) return

  try {
    await chatStore.sendMessage({
      relationId: session.relationId,
      content: txt,
      messageType: 'Text'
    })
    messageText.value = ''
    nextTick(() => scrollToBottom())
  } catch (error) {
    console.error('发送消息失败:', error)
  }
}

const scrollToBottom = () => {
  if (messageListRef.value) {
    messageListRef.value.scrollTop = messageListRef.value.scrollHeight
  }
}

const getLastMessagePreview = (session: ChatSession) => {
  const msg = session.lastMessage
  if (!msg) return '暂无消息'
  switch (msg.messageType) {
    case 'Image': return '[图片]'
    case 'Video': return '[视频]'
    case 'File': return '[文件]'
    default: {
      const text = msg.content || ''
      return text.length > 20 ? text.slice(0, 20) + '...' : text
    }
  }
}

const formatTime = (time: string) => {
  if (!time) return ''
  const d = dayjs(time)
  if (!d.isValid()) return ''
  const now = dayjs()
  if (d.isSame(now, 'day')) return d.format('HH:mm')
  if (d.isSame(now.subtract(1, 'day'), 'day')) return '昨天'
  if (d.isSame(now, 'year')) return d.format('MM/DD')
  return d.format('YYYY/MM/DD')
}

const handleOnlineStatusChange = async (v: string | number | boolean) => {
  const isOnline = v === true
  try {
    // 通过 SignalR 通知服务端，服务端会更新数据库并广播给好友
    const conn = signalrStore.connection
    if (conn?.state === 'Connected') {
      await conn.invoke('UpdateOnlineStatus', isOnline)
    }
    // 更新本地状态
    if (authStore.user) {
      authStore.user.isOnline = isOnline
      authStore.user.lastSeen = new Date().toISOString()
    }
    ElMessage.success(isOnline ? '已设置为在线' : '已设置为离线')
  } catch (error) {
    isOnlineStatus.value = !isOnline
    ElMessage.error('设置在线状态失败')
  }
}

const handleUserCommand = async (command: string) => {
  switch (command) {
    case 'profile':
      router.push('/profile')
      break
    case 'settings':
      router.push('/settings')
      break
    case 'logout':
      try {
        // 断开 SignalR 连接（logout 中已经会断开，这里可以省略，但保留也无妨）
        // await signalrStore.disconnect()
        // 执行退出登录（logout 中会自动断开连接）
        await authStore.logout()
        // 跳转到登录页面
        router.push('/login')
      } catch (error) {
        console.error('退出登录失败:', error)
        // 即使出错也跳转到登录页面
        router.push('/login')
      }
      break
  }
}

const handleImageUpload = () => {
  // 处理图片上传
  console.log('上传图片')
}

const handleFileUpload = () => {
  // 处理文件上传
  console.log('上传文件')
}

// 跳转到好友管理页面
const goToFriendsPage = () => {
  router.push('/friends')
}

// 跳转到群组管理页面
const goToGroupsPage = () => {
  router.push('/groups')
}

const handleCreateGroup = async () => {
  try {
    await groupApi.createGroup(createGroupForm)
    ElMessage.success('创建群组成功')
    showCreateGroupDialog.value = false
    createGroupForm.name = ''
    createGroupForm.description = ''
  } catch (error) {
    console.error('创建群组失败:', error)
  }
}

onMounted(() => {
  ensureSessionsAndSelectFromQuery()
})

watch(() => [route.query.friend, route.query.group], () => {
  ensureSessionsAndSelectFromQuery()
})
</script>

<style scoped>
.chat-container {
  display: flex;
  height: 100vh;
  background: #f5f5f5;
}

.sidebar {
  width: 300px;
  background: white;
  border-right: 1px solid #e0e0e0;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.user-info {
  display: flex;
  align-items: center;
  padding: 15px;
  border-bottom: 1px solid #e0e0e0;
}

.user-details {
  flex: 1;
  margin-left: 10px;
}

.username {
  font-weight: 500;
  color: #333;
}

.status-container {
  display: flex;
  align-items: center;
}

.status {
  font-size: 12px;
  color: #999;
}

.status.online {
  color: #67c23a;
}

.search-box {
  padding: 15px;
  border-bottom: 1px solid #e0e0e0;
}

.quick-actions {
  display: flex;
  gap: 8px;
  padding: 12px 15px;
  border-bottom: 1px solid #e0e0e0;
  background: #fafafa;
}

.quick-action-btn {
  flex: 1;
}

.chat-list {
  flex: 1;
  overflow-y: auto;
  padding: 10px;
  min-height: 0;
}

.empty-chat-list {
  display: flex;
  align-items: center;
  justify-content: center;
  min-height: 300px;
  padding: 40px;
}

.chat-item {
  display: flex;
  align-items: center;
  padding: 15px;
  cursor: pointer;
  border-bottom: 1px solid #f0f0f0;
  transition: background-color 0.2s;
}

.chat-item:hover {
  background: #f5f5f5;
}

.chat-item.active {
  background: #e3f2fd;
}

.chat-info {
  flex: 1;
  margin-left: 10px;
  min-width: 0;
}

.chat-name {
  font-weight: 500;
  color: #333;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.last-message {
  font-size: 12px;
  color: #999;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.chat-meta {
  display: flex;
  flex-direction: column;
  align-items: flex-end;
}

.time {
  font-size: 12px;
  color: #999;
}

.main-content {
  flex: 1;
  display: flex;
  flex-direction: column;
}

.welcome {
  flex: 1;
  display: flex;
  align-items: center;
  justify-content: center;
}

.chat-area {
  flex: 1;
  display: flex;
  flex-direction: column;
}

.chat-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 15px 20px;
  background: white;
  border-bottom: 1px solid #e0e0e0;
}

.chat-title {
  display: flex;
  align-items: center;
}

.title-info {
  margin-left: 10px;
}

.title-name {
  font-weight: 500;
  color: #333;
}

.title-status {
  font-size: 12px;
  color: #999;
}

.chat-actions {
  display: flex;
  gap: 10px;
  align-items: center;
}

.online-status-indicator {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 4px 12px;
  border-radius: 12px;
  background: #f5f5f5;
}

.status-dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  background: #c0c4cc;
  transition: background-color 0.3s;
}

.status-dot.online {
  background: #67c23a;
  box-shadow: 0 0 4px rgba(103, 194, 58, 0.5);
}

.online-status-indicator .status-text {
  font-size: 13px;
  color: #999;
}

.status-dot.online + .status-text {
  color: #67c23a;
}

.message-list {
  flex: 1;
  overflow-y: auto;
  padding: 20px;
  background: #fafafa;
}

.message-item {
  display: flex;
  margin-bottom: 15px;
}

.message-item.own-message {
  flex-direction: row-reverse;
}

.message-content {
  margin: 0 10px;
  max-width: 60%;
}

.message-header {
  display: flex;
  align-items: center;
  margin-bottom: 5px;
}

.sender-name {
  font-size: 12px;
  color: #666;
  margin-right: 10px;
}

.message-time {
  font-size: 12px;
  color: #999;
}

.message-body {
  background: white;
  padding: 10px 15px;
  border-radius: 8px;
  box-shadow: 0 1px 2px rgba(0, 0, 0, 0.1);
}

.own-message .message-body {
  background: #409eff;
  color: white;
}

.text-message {
  word-wrap: break-word;
}

.image-message img {
  max-width: 200px;
  border-radius: 4px;
}

.file-message {
  display: flex;
  align-items: center;
  gap: 5px;
}

.message-input {
  background: white;
  border-top: 1px solid #e0e0e0;
  padding: 15px 20px;
}

.input-toolbar {
  display: flex;
  gap: 10px;
  margin-bottom: 10px;
}

.input-area {
  display: flex;
  gap: 10px;
  align-items: flex-end;
}

.input-area .el-textarea {
  flex: 1;
}

</style>
