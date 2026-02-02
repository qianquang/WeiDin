<template>
  <div class="friends-container">
    <el-card>
      <template #header>
        <div class="card-header">
          <span class="header-title">好友管理</span>
          <div class="header-actions">
            <el-button type="primary" :icon="ArrowLeft" @click="goToChatPage">
              返回聊天
            </el-button>
            <el-button type="primary" @click="showAddFriendDialog = true">添加好友</el-button>
          </div>
        </div>
      </template>
      
      <div class="friends-content">
        <el-tabs v-model="activeTab">
          <el-tab-pane label="好友列表" name="friends">
            <div class="friends-list">
              <div
                v-for="friend in friends"
                :key="friend.id"
                class="friend-item"
              >
                <el-avatar :size="50" :src="friend.friendAvatar">
                  {{ friend.friendName?.charAt(0)?.toUpperCase() || 'F' }}
                </el-avatar>
                <div class="friend-info">
                  <div class="friend-name">{{ friend.friendName || '未知好友' }}</div>
                  <div class="friend-remark">{{ friend.remark || '暂无备注' }}</div>
                  <div v-if="friend.groupName" class="friend-group">
                    <el-tag type="info" size="small">{{ friend.groupName }}</el-tag>
                  </div>
                </div>
                <div class="friend-actions">
                  <el-button type="text" @click="startChat(friend)">聊天</el-button>
                  <el-button type="text" @click="editFriend(friend)">编辑</el-button>
                  <el-button type="text" @click="removeFriend(friend.id)">删除</el-button>
                </div>
              </div>
              <div v-if="friends.length === 0" class="empty-state">
                <el-empty description="暂无好友" :image-size="100" />
              </div>
            </div>
          </el-tab-pane>
          
          <el-tab-pane label="待处理申请" name="pending">
            <div class="requests-list">
              <div
                v-for="request in pendingRequests"
                :key="request.id"
                class="request-item"
              >
                <el-avatar :size="50" :src="request.userAvatar">
                  {{ request.userName?.charAt(0)?.toUpperCase() || 'U' }}
                </el-avatar>
                <div class="request-info">
                  <div class="request-header">
                    <div class="request-name">{{ request.userName || '未知用户' }}</div>
                    <div class="request-time">
                      <el-icon><Clock /></el-icon>
                      {{ formatTime(request.createdAt) }}
                    </div>
                  </div>
                  <div v-if="request.remark" class="request-remark">
                    <el-icon><ChatLineRound /></el-icon>
                    <span>{{ request.remark }}</span>
                  </div>
                  <div v-if="request.groupName" class="request-group">
                    <el-tag type="info" size="small">{{ request.groupName }}</el-tag>
                  </div>
                </div>
                <div class="request-actions">
                  <el-button type="success" size="small" :icon="Check" @click="acceptRequest(request.id)">
                    接受
                  </el-button>
                  <el-button type="danger" size="small" :icon="Close" @click="rejectRequest(request.id)">
                    拒绝
                  </el-button>
                </div>
              </div>
              <div v-if="pendingRequests.length === 0" class="empty-state">
                <el-empty description="暂无待处理的好友申请" :image-size="100" />
              </div>
            </div>
          </el-tab-pane>

          <el-tab-pane label="已发送申请" name="sent">
            <div class="requests-list">
              <div
                v-for="request in sentRequests"
                :key="request.id"
                class="request-item"
              >
                <el-avatar :size="50" :src="request.friendAvatar">
                  {{ request.friendName?.charAt(0)?.toUpperCase() || 'U' }}
                </el-avatar>
                <div class="request-info">
                  <div class="request-header">
                    <div class="request-name">{{ request.friendName || '未知用户' }}</div>
                    <div class="request-time">
                      <el-icon><Clock /></el-icon>
                      {{ formatTime(request.createdAt) }}
                    </div>
                  </div>
                  <div v-if="request.remark" class="request-remark">
                    <el-icon><ChatLineRound /></el-icon>
                    <span>{{ request.remark }}</span>
                  </div>
                  <div v-if="request.groupName" class="request-group">
                    <el-tag type="info" size="small">{{ request.groupName }}</el-tag>
                  </div>
                </div>
                <div class="request-status">
                  <el-tag type="warning" size="small">
                    <el-icon><Loading /></el-icon>
                    等待处理
                  </el-tag>
                </div>
              </div>
              <div v-if="sentRequests.length === 0" class="empty-state">
                <el-empty description="暂无已发送的好友申请" :image-size="100" />
              </div>
            </div>
          </el-tab-pane>
          
          <el-tab-pane label="黑名单" name="blacklist">
            <div class="blacklist-list">
              <div
                v-for="blocked in blacklist"
                :key="blocked.id"
                class="blacklist-item"
              >
                <el-avatar :size="40" :src="(blocked as any).blockedUserAvatar">
                  {{ blocked.blockedUserName?.charAt(0) || '?' }}
                </el-avatar>
                <div class="blacklist-info">
                  <div class="blocked-name">{{ blocked.blockedUserName }}</div>
                  <div class="block-reason">{{ blocked.reason || '无原因' }}</div>
                </div>
                <el-button type="text" @click="removeFromBlacklist(blocked.id)">移除</el-button>
              </div>
              <div v-if="blacklist.length === 0" class="empty-state">
                黑名单为空
              </div>
            </div>
          </el-tab-pane>
        </el-tabs>
      </div>
    </el-card>

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

    <!-- 编辑好友对话框 -->
    <el-dialog v-model="showEditFriendDialog" title="编辑好友" width="400px">
      <el-form :model="editFriendForm" label-width="80px">
        <el-form-item label="备注">
          <el-input v-model="editFriendForm.remark" placeholder="请输入备注" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="showEditFriendDialog = false">取消</el-button>
        <el-button type="primary" @click="handleEditFriend">确定</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted, onActivated, computed } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import { Clock, ChatLineRound, Check, Close, Loading, ArrowLeft } from '@element-plus/icons-vue'
import { useFriendshipStore } from '@/stores/friendship'
import { useInvalidationStore } from '@/stores/invalidation'
import type { CreateFriendshipDto, UpdateFriendshipDto, Friendship } from '@/types'
import dayjs from 'dayjs'

const router = useRouter()
const friendshipStore = useFriendshipStore()
const invalidationStore = useInvalidationStore()

const activeTab = ref('friends')
const showAddFriendDialog = ref(false)
const showEditFriendDialog = ref(false)

// 从 store 获取数据
const friends = computed(() => friendshipStore.friends)
const pendingRequests = computed(() => friendshipStore.pendingRequests)
const sentRequests = computed(() => friendshipStore.sentRequests)
const blacklist = computed(() => friendshipStore.blacklist)

const addFriendForm = reactive<CreateFriendshipDto>({
  friendId: '',
  remark: ''
})

const editFriendForm = reactive<UpdateFriendshipDto & { id: string }>({
  id: '',
  remark: ''
})

// 加载数据的辅助函数（使用 store 的方法）
const loadAllData = async () => {
  await Promise.all([
    friendshipStore.loadFriends(),
    friendshipStore.loadPendingRequests(),
    friendshipStore.loadSentRequests(),
    friendshipStore.loadBlacklist()
  ])
}

const handleAddFriend = async () => {
  try {
    // 验证输入
    if (!addFriendForm.friendId || addFriendForm.friendId.trim() === '') {
      ElMessage.error('请输入用户ID')
      return
    }
    
    // 验证GUID格式
    const guidRegex = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i
    if (!guidRegex.test(addFriendForm.friendId.trim())) {
      ElMessage.error('用户ID格式不正确，请输入有效的GUID格式')
      return
    }
    
    // 确保发送的是trimmed的值，只发送有值的字段
    const requestData: CreateFriendshipDto = {
      friendId: addFriendForm.friendId.trim(),
    }
    
    // 只有当 remark 有值时才添加
    if (addFriendForm.remark && addFriendForm.remark.trim()) {
      requestData.remark = addFriendForm.remark.trim()
    }
    
    // groupName 字段在表单中未使用，如果需要可以添加
    // if (addFriendForm.groupName && addFriendForm.groupName.trim()) {
    //   requestData.groupName = addFriendForm.groupName.trim()
    // }
    
    await friendshipStore.addFriend(requestData)
    ElMessage.success('好友申请已发送')
    showAddFriendDialog.value = false
    addFriendForm.friendId = ''
    addFriendForm.remark = ''
    // 切换到已发送申请标签页
    if (activeTab.value !== 'sent') {
      activeTab.value = 'sent'
    }
  } catch (error: any) {
    ElMessage.error(error.response?.data?.message || '发送好友申请失败')
    console.error('添加好友失败:', error)
  }
}

const editFriend = (friend: Friendship) => {
  editFriendForm.id = friend.id
  editFriendForm.remark = friend.remark || ''
  showEditFriendDialog.value = true
}

const handleEditFriend = async () => {
  try {
    await friendshipStore.updateFriendship(editFriendForm.id, {
      remark: editFriendForm.remark
    })
    ElMessage.success('编辑成功')
    showEditFriendDialog.value = false
  } catch (error) {
    console.error('编辑好友失败:', error)
  }
}

const removeFriend = async (id: string) => {
  try {
    await ElMessageBox.confirm('确定要删除这个好友吗？', '确认删除', {
      type: 'warning'
    })
    await friendshipStore.deleteFriend(id)
    ElMessage.success('删除成功')
  } catch (error) {
    if (error !== 'cancel') {
      console.error('删除好友失败:', error)
    }
  }
}

const removeFromBlacklist = async (id: string) => {
  try {
    await ElMessageBox.confirm('确定要从黑名单中移除吗？', '确认移除', {
      type: 'warning'
    })
    await friendshipStore.removeFromBlacklist(id)
    ElMessage.success('移除成功')
  } catch (error) {
    if (error !== 'cancel') {
      console.error('移除黑名单失败:', error)
    }
  }
}

const acceptRequest = async (id: string) => {
  try {
    await friendshipStore.acceptFriendRequest(id)
    ElMessage.success('已接受好友申请')
    // 如果当前在待处理标签页，切换到好友列表
    if (activeTab.value === 'pending') {
      activeTab.value = 'friends'
    }
  } catch (error: any) {
    ElMessage.error(error.response?.data?.message || '接受申请失败')
    console.error('接受申请失败:', error)
  }
}

const rejectRequest = async (id: string) => {
  try {
    await ElMessageBox.confirm('确定要拒绝这个好友申请吗？', '确认拒绝', {
      type: 'warning'
    })
    await friendshipStore.rejectFriendRequest(id)
    ElMessage.success('已拒绝好友申请')
  } catch (error: any) {
    if (error !== 'cancel') {
      ElMessage.error(error.response?.data?.message || '拒绝申请失败')
      console.error('拒绝申请失败:', error)
    }
  }
}

const formatTime = (time: string) => {
  return dayjs(time).format('YYYY-MM-DD HH:mm')
}

const startChat = (friend: any) => {
  router.push(`/chat?friend=${friend.friendId}`)
}

// 返回聊天页面
const goToChatPage = () => {
  router.push('/chat')
}

// 检查失效标记并刷新数据
const checkAndRefresh = async () => {
  if (invalidationStore.isStale('friends')) {
    await loadAllData()
    invalidationStore.clear('friends')
  }
}

onMounted(async () => {
  // 检查失效标记，若失效则刷新数据
  await checkAndRefresh()
  
  // 如果数据为空，也加载一次（首次进入页面）
  if (friends.value.length === 0 && pendingRequests.value.length === 0) {
    await loadAllData()
  }
})

// 如果使用 keep-alive，切换回页面时也检查
onActivated(async () => {
  await checkAndRefresh()
})
</script>

<style scoped>
.friends-container {
  max-width: 800px;
  margin: 20px auto;
  padding: 0 20px;
}

.card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.header-title {
  font-size: 16px;
  font-weight: 500;
  color: #303133;
}

.header-actions {
  display: flex;
  align-items: center;
  gap: 10px;
}

.friends-list,
.blacklist-list {
  max-height: 500px;
  overflow-y: auto;
}

.friend-item,
.blacklist-item {
  display: flex;
  align-items: flex-start;
  padding: 16px;
  border-bottom: 1px solid #f0f0f0;
  transition: background-color 0.2s;
}

.friend-item:hover,
.blacklist-item:hover {
  background-color: #fafafa;
}

.friend-info,
.blacklist-info {
  flex: 1;
  margin-left: 15px;
  min-width: 0;
}

.friend-name,
.blocked-name {
  font-weight: 500;
  color: #333;
  font-size: 15px;
  margin-bottom: 4px;
}

.friend-remark,
.block-reason {
  font-size: 13px;
  color: #666;
  margin-top: 4px;
}

.friend-group {
  margin-top: 6px;
}

.friend-actions {
  display: flex;
  gap: 10px;
}

.requests-list {
  max-height: 500px;
  overflow-y: auto;
}

.request-item {
  display: flex;
  align-items: flex-start;
  padding: 16px;
  border-bottom: 1px solid #f0f0f0;
  transition: background-color 0.2s;
}

.request-item:hover {
  background-color: #fafafa;
}

.request-info {
  flex: 1;
  margin-left: 15px;
  min-width: 0;
}

.request-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 8px;
}

.request-name {
  font-weight: 500;
  color: #333;
  font-size: 15px;
  flex: 1;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.request-time {
  font-size: 12px;
  color: #999;
  display: flex;
  align-items: center;
  gap: 4px;
  flex-shrink: 0;
  margin-left: 12px;
}

.request-remark {
  font-size: 13px;
  color: #666;
  margin-top: 6px;
  display: flex;
  align-items: center;
  gap: 6px;
  line-height: 1.5;
}

.request-remark .el-icon {
  color: #909399;
  font-size: 14px;
}

.request-group {
  margin-top: 8px;
}

.request-actions {
  display: flex;
  gap: 8px;
  flex-shrink: 0;
  margin-left: 12px;
}

.request-status {
  display: flex;
  align-items: center;
  flex-shrink: 0;
  margin-left: 12px;
}

.request-status .el-tag {
  display: flex;
  align-items: center;
  gap: 4px;
}

.empty-state {
  display: flex;
  justify-content: center;
  align-items: center;
  min-height: 300px;
  padding: 40px;
}
</style>
