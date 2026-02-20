<template>
  <div class="groups-container">
    <el-card>
      <template #header>
        <div class="card-header">
          <span class="header-title">群组管理</span>
          <div class="header-actions">
            <el-button type="primary" :icon="ArrowLeft" @click="goToChatPage">
              返回聊天
            </el-button>
            <el-button type="primary" @click="showCreateGroupDialog = true">创建群组</el-button>
            <el-button type="success" @click="showRequestJoinDialog = true">申请加入群组</el-button>
          </div>
        </div>
      </template>
      
      <div class="groups-content">
        <el-tabs v-model="activeTab">
          <!-- 所有群组标签页 -->
          <el-tab-pane label="所有群组" name="all">
        <div class="groups-list">
          <div
                v-for="group in allGroups"
            :key="group.id"
            class="group-item"
          >
            <el-avatar :size="50" :src="group.avatar">
              {{ group.name.charAt(0) }}
            </el-avatar>
            <div class="group-info">
              <div class="group-name">{{ group.name }}</div>
              <div class="group-description">{{ group.description || '暂无描述' }}</div>
              <div class="group-meta">
                <span>成员: {{ group.currentMembers }}/{{ group.maxMembers }}</span>
                <span>创建时间: {{ formatDate(group.createdAt) }}</span>
                    <span class="group-id">
                      群组ID: 
                      <el-text type="primary" class="group-id-text" @click="copyGroupId(group.id)">
                        {{ group.id }}
                      </el-text>
                      <el-icon class="copy-icon"><DocumentCopy /></el-icon>
                    </span>
              </div>
            </div>
            <div class="group-actions">
              <el-button type="text" @click="viewGroup(group)">查看</el-button>
              <el-button v-if="group.ownerId === authStore.userId" type="text" @click="manageGroup(group)">管理</el-button>
            </div>
          </div>
              <div v-if="allGroups.length === 0" class="empty-state">
                <el-empty description="暂无群组" :image-size="100" />
              </div>
            </div>
          </el-tab-pane>

          <!-- 我的群组标签页 -->
          <el-tab-pane label="我的群组" name="my">
            <div class="groups-list">
              <div
                v-for="group in myGroups"
                :key="group.id"
                class="group-item"
              >
                <el-avatar :size="50" :src="group.avatar">
                  {{ group.name.charAt(0) }}
                </el-avatar>
                <div class="group-info">
                  <div class="group-name">{{ group.name }}</div>
                  <div class="group-description">{{ group.description || '暂无描述' }}</div>
                  <div class="group-meta">
                    <span>成员: {{ group.currentMembers }}/{{ group.maxMembers }}</span>
                    <span>创建时间: {{ formatDate(group.createdAt) }}</span>
                    <span class="group-id">
                      群组ID: 
                      <el-text type="primary" class="group-id-text" @click="copyGroupId(group.id)">
                        {{ group.id }}
                      </el-text>
                      <el-icon class="copy-icon"><DocumentCopy /></el-icon>
                    </span>
                  </div>
                </div>
                <div class="group-actions">
                  <el-button type="text" @click="viewGroup(group)">查看</el-button>
                  <el-button 
                    v-if="group.ownerId === authStore.userId" 
                    type="text" 
                    @click="manageGroup(group)"
                  >
                    管理
                  </el-button>
                  <el-button 
                    v-if="group.ownerId !== authStore.userId" 
                    type="text" 
                    @click="leaveGroup(group)"
                  >
                    退出
                  </el-button>
                  <el-button 
                    v-if="group.ownerId === authStore.userId" 
                    type="text" 
                    danger
                    @click="deleteGroup(group)"
                  >
                    解散
                  </el-button>
                </div>
              </div>
              <div v-if="myGroups.length === 0" class="empty-state">
                <el-empty description="暂无群组" :image-size="100" />
              </div>
            </div>
          </el-tab-pane>

          <!-- 待处理申请标签页（仅群主可见） -->
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
                      {{ formatTime(request.joinedAt) }}
                    </div>
                  </div>
                  <div class="request-group-name">
                    <el-tag type="info" size="small">群组: {{ request.groupName }}</el-tag>
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
                <el-empty description="暂无待处理的群组申请" :image-size="100" />
              </div>
            </div>
          </el-tab-pane>

          <!-- 已发送申请标签页 -->
          <el-tab-pane label="已发送申请" name="sent">
            <div class="requests-list">
              <div
                v-for="request in sentRequests"
                :key="request.id"
                class="request-item"
              >
                <el-avatar :size="50" :src="request.groupName ? undefined : undefined">
                  {{ request.groupName?.charAt(0)?.toUpperCase() || 'G' }}
                </el-avatar>
                <div class="request-info">
                  <div class="request-header">
                    <div class="request-name">{{ request.groupName || '未知群组' }}</div>
                    <div class="request-time">
                      <el-icon><Clock /></el-icon>
                      {{ formatTime(request.joinedAt) }}
                    </div>
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
                <el-empty description="暂无已发送的群组申请" :image-size="100" />
        </div>
            </div>
          </el-tab-pane>
        </el-tabs>
      </div>
    </el-card>

    <!-- 创建群组对话框 -->
    <el-dialog v-model="showCreateGroupDialog" title="创建群组" width="500px">
      <el-form :model="createGroupForm" label-width="100px">
        <el-form-item label="群组名称">
          <el-input v-model="createGroupForm.name" placeholder="请输入群组名称" />
        </el-form-item>
        <el-form-item label="群组描述">
          <el-input v-model="createGroupForm.description" type="textarea" placeholder="请输入群组描述" />
        </el-form-item>
        <el-form-item label="最大成员数">
          <el-input-number v-model="createGroupForm.maxMembers" :min="2" :max="1000" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="showCreateGroupDialog = false">取消</el-button>
        <el-button type="primary" @click="handleCreateGroup">确定</el-button>
      </template>
    </el-dialog>

    <!-- 申请加入群组对话框 -->
    <el-dialog v-model="showRequestJoinDialog" title="申请加入群组" width="400px">
      <el-form :model="requestJoinForm" label-width="80px">
        <el-form-item label="群组ID">
          <el-input v-model="requestJoinForm.groupId" placeholder="请输入群组ID" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="showRequestJoinDialog = false">取消</el-button>
        <el-button type="primary" @click="handleRequestJoin">确定</el-button>
      </template>
    </el-dialog>

    <!-- 群组详情对话框 -->
    <el-dialog v-model="showGroupDetailDialog" title="群组详情" width="600px">
      <div v-if="selectedGroup" class="group-detail">
        <div class="group-header">
          <el-avatar :size="60" :src="selectedGroup.avatar">
            {{ selectedGroup.name.charAt(0) }}
          </el-avatar>
          <div class="group-title">
            <h3>{{ selectedGroup.name }}</h3>
            <p>{{ selectedGroup.description || '暂无描述' }}</p>
            <p class="group-id-detail">
              群组ID: 
              <el-text type="primary" class="group-id-text" @click="copyGroupId(selectedGroup.id)">
                {{ selectedGroup.id }}
              </el-text>
              <el-icon class="copy-icon"><DocumentCopy /></el-icon>
            </p>
          </div>
        </div>
        
        <el-tabs v-model="detailTab">
          <el-tab-pane label="成员列表" name="members">
            <div class="members-list">
              <div
                v-for="member in groupMembers"
                :key="member.id"
                class="member-item"
              >
                <el-avatar :size="32" :src="member.userAvatar">
                  {{ member.userName.charAt(0) }}
                </el-avatar>
                <div class="member-info">
                  <div class="member-name">{{ member.userName }}</div>
                  <div class="member-role">{{ member.role }}</div>
                </div>
                <el-tag :type="member.role === 'Owner' ? 'danger' : member.role === 'Admin' ? 'warning' : 'info'">
                  {{ member.role }}
                </el-tag>
              </div>
            </div>
          </el-tab-pane>
          
          <el-tab-pane label="群组设置" name="settings">
            <el-form :model="groupSettings" label-width="100px">
              <el-form-item label="群组名称">
                <el-input v-model="groupSettings.name" />
              </el-form-item>
              <el-form-item label="群组描述">
                <el-input v-model="groupSettings.description" type="textarea" />
              </el-form-item>
              <el-form-item label="群组公告">
                <el-input v-model="groupSettings.announcement" type="textarea" />
              </el-form-item>
            </el-form>
          </el-tab-pane>
        </el-tabs>
      </div>
      <template #footer>
        <el-button @click="showGroupDetailDialog = false">关闭</el-button>
        <el-button v-if="detailTab === 'settings'" type="primary" @click="saveGroupSettings">保存设置</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted, onActivated, computed, watch } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import { ArrowLeft, Clock, ChatLineRound, Check, Close, Loading, DocumentCopy } from '@element-plus/icons-vue'
import { useAuthStore } from '@/stores/auth'
import { useGroupStore } from '@/stores/group'
import { useInvalidationStore } from '@/stores/invalidation'
import { groupApi } from '@/api'
import type { Group, CreateGroupDto, UpdateGroupDto, GroupMember } from '@/types'
import dayjs from 'dayjs'

const router = useRouter()
const authStore = useAuthStore()
const groupStore = useGroupStore()
const invalidationStore = useInvalidationStore()

const activeTab = ref('all')
const showCreateGroupDialog = ref(false)
const showRequestJoinDialog = ref(false)
const showGroupDetailDialog = ref(false)
const detailTab = ref('members')
const groupMembers = ref<GroupMember[]>([])
const selectedGroup = ref<Group | null>(null)

// 从 store 获取数据
const allGroups = computed(() => groupStore.allGroups)
const myGroups = computed(() => groupStore.myGroups)
const pendingRequests = computed(() => groupStore.pendingRequests)
const sentRequests = computed(() => groupStore.sentRequests)

const createGroupForm = reactive<CreateGroupDto>({
  name: '',
  description: '',
  maxMembers: 500
})

const requestJoinForm = reactive({
  groupId: ''
})

const groupSettings = reactive<UpdateGroupDto>({
  name: '',
  description: '',
  announcement: ''
})

// 加载所有群组
const loadAllGroups = async () => {
  try {
    await groupStore.loadAllGroups(1, 100)
  } catch (error) {
    console.error('加载群组列表失败:', error)
  }
}

// 加载我的群组
const loadMyGroups = async () => {
  try {
    await groupStore.loadMyGroups()
  } catch (error) {
    console.error('加载我的群组列表失败:', error)
  }
}

// 加载待处理申请（需要先知道用户是哪些群组的群主）
const loadPendingRequests = async () => {
  try {
    // 先确保我的群组列表已加载
    if (groupStore.myGroups.length === 0) {
      await loadMyGroups()
    }
    
    // 获取用户的所有群组，找出用户是群主的群组
    const myGroupsList = groupStore.myGroups
    const ownerGroups = myGroupsList.filter(g => g.ownerId === authStore.userId)
    
    // 为每个群组加载待处理申请并合并
    const allPendingRequests: any[] = []
    for (const group of ownerGroups) {
      try {
        const requests = await groupApi.getPendingRequests(group.id)
        allPendingRequests.push(...requests)
      } catch (error) {
        // 忽略权限错误（可能不是群主）
        console.warn(`加载群组 ${group.id} 的待处理申请失败:`, error)
      }
    }
    
    // 更新 store 中的 pendingRequests（通过直接赋值）
    // 注意：由于 store 的 loadPendingRequests 是按群组加载的，我们需要手动合并
    // 这里我们直接使用 API 调用并手动更新 store
    groupStore.pendingRequests.splice(0, groupStore.pendingRequests.length, ...allPendingRequests)
  } catch (error) {
    console.error('加载待处理申请失败:', error)
  }
}

// 加载已发送申请
const loadSentRequests = async () => {
  try {
    await groupStore.loadSentRequests()
  } catch (error) {
    console.error('加载已发送申请失败:', error)
  }
}

const loadGroupMembers = async (groupId: string) => {
  try {
    groupMembers.value = await groupApi.getGroupMembers(groupId)
  } catch (error) {
    console.error('加载群成员失败:', error)
  }
}

const handleCreateGroup = async () => {
  try {
    await groupStore.createGroup(createGroupForm)
    ElMessage.success('创建群组成功')
    showCreateGroupDialog.value = false
    createGroupForm.name = ''
    createGroupForm.description = ''
    createGroupForm.maxMembers = 500
    // 切换到"我的群组"标签页
    activeTab.value = 'my'
  } catch (error: any) {
    ElMessage.error(error.response?.data?.message || '创建群组失败')
    console.error('创建群组失败:', error)
  }
}

const handleRequestJoin = async () => {
  if (!requestJoinForm.groupId.trim()) {
    ElMessage.warning('请输入群组ID')
    return
  }
  
  try {
    await groupStore.requestJoinGroup(requestJoinForm.groupId)
    ElMessage.success('申请已提交，请等待群主审核')
    showRequestJoinDialog.value = false
    requestJoinForm.groupId = ''
    // 切换到"已发送申请"标签页
    activeTab.value = 'sent'
    // 重新加载已发送申请
    await loadSentRequests()
  } catch (error: any) {
    ElMessage.error(error.response?.data?.message || '申请加入群组失败')
    console.error('申请加入群组失败:', error)
  }
}

const viewGroup = async (group: Group) => {
  selectedGroup.value = group
  groupSettings.name = group.name
  groupSettings.description = group.description || ''
  groupSettings.announcement = group.announcement || ''
  showGroupDetailDialog.value = true
  await loadGroupMembers(group.id)
}

const manageGroup = (group: Group) => {
  viewGroup(group)
  detailTab.value = 'settings'
}

const saveGroupSettings = async () => {
  if (!selectedGroup.value) return
  
  try {
    await groupStore.updateGroup(selectedGroup.value.id, groupSettings)
    ElMessage.success('保存设置成功')
    // Store 会自动更新，无需手动 reload
    // 更新 selectedGroup 以反映最新数据
    const updated = groupStore.allGroups.find(g => g.id === selectedGroup.value!.id) ||
                    groupStore.myGroups.find(g => g.id === selectedGroup.value!.id)
    if (updated) {
      selectedGroup.value = updated
    }
  } catch (error: any) {
    ElMessage.error(error.response?.data?.message || '保存设置失败')
    console.error('保存设置失败:', error)
  }
}

const leaveGroup = async (group: Group) => {
  try {
    await ElMessageBox.confirm(`确定要退出群组"${group.name}"吗？`, '确认退出', {
      type: 'warning'
    })
    await groupStore.leaveGroup(group.id)
    ElMessage.success('已退出群组')
  } catch (error: any) {
    if (error !== 'cancel') {
      ElMessage.error(error.response?.data?.message || '退出群组失败')
      console.error('退出群组失败:', error)
    }
  }
}

const deleteGroup = async (group: Group) => {
  try {
    await ElMessageBox.confirm(`确定要解散群组"${group.name}"吗？解散后所有成员将被移除。`, '确认解散', {
      type: 'warning'
    })
    await groupStore.deleteGroup(group.id)
    ElMessage.success('群组已解散')
  } catch (error: any) {
    if (error !== 'cancel') {
      ElMessage.error(error.response?.data?.message || '解散群组失败')
      console.error('解散群组失败:', error)
    }
  }
}

const acceptRequest = async (memberId: string) => {
  try {
    await groupStore.acceptGroupRequest(memberId)
    ElMessage.success('已接受申请')
    // 重新加载待处理申请
    await loadPendingRequests()
  } catch (error: any) {
    ElMessage.error(error.response?.data?.message || '接受申请失败')
    console.error('接受申请失败:', error)
  }
}

const rejectRequest = async (memberId: string) => {
  try {
    await ElMessageBox.confirm('确定要拒绝该申请吗？', '确认拒绝', {
      type: 'warning'
    })
    await groupStore.rejectGroupRequest(memberId)
    ElMessage.success('已拒绝申请')
    // 重新加载待处理申请
    await loadPendingRequests()
  } catch (error: any) {
    if (error !== 'cancel') {
      ElMessage.error(error.response?.data?.message || '拒绝申请失败')
      console.error('拒绝申请失败:', error)
    }
  }
}

const copyGroupId = async (groupId: string) => {
  try {
    await navigator.clipboard.writeText(groupId)
    ElMessage.success('群组ID已复制到剪贴板')
  } catch (error) {
    // 降级方案
    const textArea = document.createElement('textarea')
    textArea.value = groupId
    document.body.appendChild(textArea)
    textArea.select()
    document.execCommand('copy')
    document.body.removeChild(textArea)
    ElMessage.success('群组ID已复制到剪贴板')
  }
}

const formatDate = (date: string) => {
  return dayjs(date).format('YYYY-MM-DD')
}

const formatTime = (date: string) => {
  return dayjs(date).format('YYYY-MM-DD HH:mm')
}

// 返回聊天页面
const goToChatPage = () => {
  router.push('/chat')
}

// 检查失效标记并刷新数据
const checkAndRefresh = async () => {
  if (invalidationStore.isStale('groups')) {
    await loadAllGroups()
    await loadMyGroups()
    invalidationStore.clear('groups')
  }
}

// 监听标签页切换，加载对应数据
watch(activeTab, async (newTab) => {
  if (newTab === 'all') {
    await loadAllGroups()
  } else if (newTab === 'my') {
    await loadMyGroups()
  } else if (newTab === 'pending') {
    await loadPendingRequests()
  } else if (newTab === 'sent') {
    await loadSentRequests()
  }
})

onMounted(async () => {
  // 检查失效标记，若失效则刷新数据
  await checkAndRefresh()
  
  // 根据当前标签页加载数据
  if (activeTab.value === 'all') {
    if (allGroups.value.length === 0) {
      await loadAllGroups()
    }
  } else if (activeTab.value === 'my') {
    if (myGroups.value.length === 0) {
      await loadMyGroups()
    }
  } else if (activeTab.value === 'sent') {
    await loadSentRequests()
  }
})

// 如果使用 keep-alive，切换回页面时也检查
onActivated(async () => {
  await checkAndRefresh()
})
</script>

<style scoped>
.groups-container {
  max-width: 1000px;
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

.groups-list {
  max-height: 600px;
  overflow-y: auto;
}

.group-item {
  display: flex;
  align-items: center;
  padding: 20px;
  border-bottom: 1px solid #f0f0f0;
  transition: background-color 0.2s;
}

.group-item:hover {
  background: #f5f5f5;
}

.group-info {
  flex: 1;
  margin-left: 15px;
}

.group-name {
  font-weight: 500;
  color: #333;
  font-size: 16px;
  margin-bottom: 5px;
}

.group-description {
  color: #666;
  font-size: 14px;
  margin-bottom: 8px;
}

.group-meta {
  display: flex;
  gap: 20px;
  font-size: 12px;
  color: #999;
  flex-wrap: wrap;
}

.group-id {
  display: flex;
  align-items: center;
  gap: 5px;
}

.group-id-text {
  cursor: pointer;
  user-select: all;
}

.copy-icon {
  cursor: pointer;
  margin-left: 5px;
}

.group-id-text:hover,
.copy-icon:hover {
  color: #409eff;
}

.group-actions {
  display: flex;
  gap: 10px;
}

.requests-list {
  max-height: 600px;
  overflow-y: auto;
}

.request-item {
  display: flex;
  align-items: center;
  padding: 15px;
  border-bottom: 1px solid #f0f0f0;
  transition: background-color 0.2s;
}

.request-item:hover {
  background: #f5f5f5;
}

.request-info {
  flex: 1;
  margin-left: 15px;
}

.request-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 5px;
}

.request-name {
  font-weight: 500;
  color: #333;
  font-size: 16px;
}

.request-time {
  display: flex;
  align-items: center;
  gap: 5px;
  font-size: 12px;
  color: #999;
}

.request-group-name {
  margin-bottom: 5px;
}

.request-message {
  display: flex;
  align-items: center;
  gap: 5px;
  font-size: 14px;
  color: #666;
  margin-top: 5px;
}

.request-actions {
  display: flex;
  gap: 10px;
}

.request-status {
  display: flex;
  align-items: center;
}

.empty-state {
  padding: 40px;
  text-align: center;
}

.group-detail {
  max-height: 500px;
  overflow-y: auto;
}

.group-header {
  display: flex;
  align-items: center;
  margin-bottom: 20px;
  padding-bottom: 20px;
  border-bottom: 1px solid #f0f0f0;
}

.group-title {
  margin-left: 15px;
}

.group-title h3 {
  margin: 0 0 5px 0;
  color: #333;
}

.group-title p {
  margin: 0;
  color: #666;
  font-size: 14px;
}

.group-id-detail {
  margin-top: 5px;
  display: flex;
  align-items: center;
  gap: 5px;
}

.members-list {
  max-height: 300px;
  overflow-y: auto;
}

.member-item {
  display: flex;
  align-items: center;
  padding: 10px;
  border-bottom: 1px solid #f0f0f0;
}

.member-info {
  flex: 1;
  margin-left: 10px;
}

.member-name {
  font-weight: 500;
  color: #333;
}

.member-role {
  font-size: 12px;
  color: #999;
}
</style>
