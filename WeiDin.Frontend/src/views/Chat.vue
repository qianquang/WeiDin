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

      <!-- 标签页 -->
      <el-tabs v-model="activeTab" class="chat-tabs">
        <el-tab-pane label="聊天" name="chats">
          <div class="chat-list">
            <div
              v-for="session in filteredSessions"
              :key="session.id"
              class="chat-item"
              :class="{ active: currentSessionId === session.id }"
              @click="selectSession(session)"
            >
              <el-avatar :size="40" :src="session.avatar">
                {{ session.name.charAt(0) }}
              </el-avatar>
              <div class="chat-info">
                <div class="chat-name">{{ session.name }}</div>
                <div class="last-message">{{ session.lastMessage?.content || '暂无消息' }}</div>
              </div>
              <div class="chat-meta">
                <div class="time">{{ formatTime(session.lastMessage?.createdAt || '') }}</div>
                <el-badge v-if="session.unreadCount > 0" :value="session.unreadCount" class="unread-badge" />
              </div>
            </div>
          </div>
        </el-tab-pane>
        
        <el-tab-pane label="好友" name="friends">
          <div class="friends-list">
            <div class="friends-header">
              <el-button type="primary" size="small" @click="showAddFriendDialog = true">
                添加好友
              </el-button>
            </div>
            <!-- 好友列表内容 -->
          </div>
        </el-tab-pane>
        
        <el-tab-pane label="群组" name="groups">
          <div class="groups-list">
            <div class="groups-header">
              <el-button type="primary" size="small" @click="showCreateGroupDialog = true">
                创建群组
              </el-button>
            </div>
            <!-- 群组列表内容 -->
          </div>
        </el-tab-pane>
      </el-tabs>
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
              {{ currentSession?.name.charAt(0) }}
            </el-avatar>
            <div class="title-info">
              <div class="title-name">{{ currentSession?.name }}</div>
              <div class="title-status">
                {{ currentSession?.type === 'private' ? '私聊' : '群聊' }}
              </div>
            </div>
          </div>
          <div class="chat-actions">
            <el-button type="text" :icon="Phone" />
            <el-button type="text" :icon="VideoCamera" />
            <el-button type="text" :icon="MoreFilled" />
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
              {{ message.senderName.charAt(0) }}
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

    <!-- 添加好友对话框 -->
    <el-dialog v-model="showAddFriendDialog" title="添加好友" width="400px">
      <el-form :model="addFriendForm" label-width="80px">
        <el-form-item label="用户ID">
          <el-input v-model="addFriendForm.friendId" placeholder="请输入用户ID" />
        </el-form-item>
        <el-form-item label="备注">
          <el-input v-model="addFriendForm.remark" placeholder="请输入备注（可选）" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="showAddFriendDialog = false">取消</el-button>
        <el-button type="primary" @click="handleAddFriend">确定</el-button>
      </template>
    </el-dialog>

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
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import {
  MoreFilled,
  Search,
  Phone,
  VideoCamera,
  Picture,
  Paperclip,
  Microphone,
  Position,
  Document
} from '@element-plus/icons-vue'
import { useAuthStore } from '@/stores/auth'
import { useChatStore } from '@/stores/chat'
import { friendshipApi, groupApi } from '@/api'
import type { CreateFriendshipDto, CreateGroupDto } from '@/types'
import dayjs from 'dayjs'

const router = useRouter()
const authStore = useAuthStore()
const chatStore = useChatStore()

// 响应式数据
const searchKeyword = ref('')
const activeTab = ref('chats')
const currentSessionId = ref<string | null>(null)
const messageText = ref('')
const messageListRef = ref<HTMLElement>()
const showAddFriendDialog = ref(false)
const showCreateGroupDialog = ref(false)

// 在线状态
const isOnlineStatus = ref(authStore.user?.isOnline || false)

// 监听用户在线状态变化
watch(() => authStore.user?.isOnline, (newValue) => {
  if (newValue !== undefined) {
    isOnlineStatus.value = newValue
  }
}, { immediate: true })

// 添加好友表单
const addFriendForm = reactive<CreateFriendshipDto>({
  friendId: '',
  remark: ''
})

// 创建群组表单
const createGroupForm = reactive<CreateGroupDto>({
  name: '',
  description: '',
  maxMembers: 500
})

// 计算属性
const filteredSessions = computed(() => {
  if (!searchKeyword.value) return chatStore.sessions
  return chatStore.sessions.filter(session =>
    session.name.toLowerCase().includes(searchKeyword.value.toLowerCase())
  )
})

const currentSession = computed(() => chatStore.currentSession)
const currentMessages = computed(() => chatStore.currentMessages)

// 方法
const selectSession = (session: any) => {
  currentSessionId.value = session.id
  chatStore.setCurrentSession(session.id)
  chatStore.loadMessages(session.id)
}

const handleSendMessage = async () => {
  if (!messageText.value.trim() || !currentSessionId.value) return

  try {
    const messageData = {
      content: messageText.value,
      messageType: 'Text' as const,
      receiverId: currentSession.value?.type === 'private' ? currentSessionId.value : undefined,
      groupId: currentSession.value?.type === 'group' ? currentSessionId.value : undefined
    }

    await chatStore.sendMessage(messageData)
    messageText.value = ''
    
    // 滚动到底部
    nextTick(() => {
      scrollToBottom()
    })
  } catch (error) {
    console.error('发送消息失败:', error)
  }
}

const scrollToBottom = () => {
  if (messageListRef.value) {
    messageListRef.value.scrollTop = messageListRef.value.scrollHeight
  }
}

const formatTime = (time: string) => {
  return dayjs(time).format('HH:mm')
}

const handleOnlineStatusChange = async (isOnline: boolean) => {
  try {
    await authStore.setOnlineStatus(isOnline)
    ElMessage.success(isOnline ? '已设置为在线' : '已设置为离线')
  } catch (error) {
    // 回滚状态
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
        // 断开 SignalR 连接
        await chatStore.disconnect()
        // 执行退出登录
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

const handleAddFriend = async () => {
  try {
    await friendshipApi.addFriend(addFriendForm)
    ElMessage.success('添加好友成功')
    showAddFriendDialog.value = false
    addFriendForm.friendId = ''
    addFriendForm.remark = ''
  } catch (error) {
    console.error('添加好友失败:', error)
  }
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
  // 初始化聊天连接
  if (authStore.isLoggedIn) {
    chatStore.initConnection()
  }
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

.chat-tabs {
  flex: 1;
  overflow: hidden;
}

.chat-list {
  height: 100%;
  overflow-y: auto;
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

.friends-list,
.groups-list {
  padding: 15px;
}

.friends-header,
.groups-header {
  margin-bottom: 15px;
}
</style>
