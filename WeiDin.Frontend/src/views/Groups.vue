<template>
  <div class="groups-container">
    <el-card>
      <template #header>
        <div class="card-header">
          <span>群组管理</span>
          <el-button type="primary" @click="showCreateGroupDialog = true">创建群组</el-button>
        </div>
      </template>
      
      <div class="groups-content">
        <div class="groups-list">
          <div
            v-for="group in groups"
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
              </div>
            </div>
            <div class="group-actions">
              <el-button type="text" @click="joinGroup(group)">加入</el-button>
              <el-button type="text" @click="viewGroup(group)">查看</el-button>
              <el-button v-if="group.ownerId === authStore.userId" type="text" @click="manageGroup(group)">管理</el-button>
            </div>
          </div>
        </div>
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
import { ref, reactive, onMounted, computed } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import { useAuthStore } from '@/stores/auth'
import { groupApi } from '@/api'
import type { Group, CreateGroupDto, UpdateGroupDto, GroupMember } from '@/types'
import dayjs from 'dayjs'

const router = useRouter()
const authStore = useAuthStore()

const showCreateGroupDialog = ref(false)
const showGroupDetailDialog = ref(false)
const detailTab = ref('members')
const groups = ref<Group[]>([])
const groupMembers = ref<GroupMember[]>([])
const selectedGroup = ref<Group | null>(null)

const createGroupForm = reactive<CreateGroupDto>({
  name: '',
  description: '',
  maxMembers: 500
})

const groupSettings = reactive<UpdateGroupDto>({
  name: '',
  description: '',
  announcement: ''
})

const loadGroups = async () => {
  try {
    groups.value = await groupApi.getAllGroups({ page: 1, pageSize: 100 })
  } catch (error) {
    console.error('加载群组列表失败:', error)
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
    await groupApi.createGroup(createGroupForm)
    ElMessage.success('创建群组成功')
    showCreateGroupDialog.value = false
    createGroupForm.name = ''
    createGroupForm.description = ''
    createGroupForm.maxMembers = 500
    loadGroups()
  } catch (error) {
    console.error('创建群组失败:', error)
  }
}

const joinGroup = async (group: Group) => {
  try {
    await ElMessageBox.confirm(`确定要加入群组"${group.name}"吗？`, '确认加入', {
      type: 'info'
    })
    await groupApi.joinGroup(group.id)
    ElMessage.success('加入群组成功')
    loadGroups()
  } catch (error) {
    if (error !== 'cancel') {
      console.error('加入群组失败:', error)
    }
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
    await groupApi.updateGroup(selectedGroup.value.id, groupSettings)
    ElMessage.success('保存设置成功')
    loadGroups()
  } catch (error) {
    console.error('保存设置失败:', error)
  }
}

const formatDate = (date: string) => {
  return dayjs(date).format('YYYY-MM-DD')
}

onMounted(() => {
  loadGroups()
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
}

.group-actions {
  display: flex;
  gap: 10px;
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
