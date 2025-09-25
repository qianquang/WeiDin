<template>
  <div class="profile-container">
    <el-card class="profile-card">
      <template #header>
        <div class="card-header">
          <span>个人资料</span>
          <el-button type="primary" @click="isEditing = true">编辑</el-button>
        </div>
      </template>
      
      <div class="profile-content">
        <div class="avatar-section">
          <el-avatar :size="100" :src="user?.avatar">
            {{ user?.nickname?.charAt(0) || user?.username?.charAt(0) }}
          </el-avatar>
          <el-button type="text" @click="handleAvatarUpload">更换头像</el-button>
        </div>
        
        <el-form :model="profileForm" label-width="100px" class="profile-form">
          <el-form-item label="用户名">
            <el-input v-model="profileForm.username" disabled />
          </el-form-item>
          <el-form-item label="邮箱">
            <el-input v-model="profileForm.email" disabled />
          </el-form-item>
          <el-form-item label="手机号">
            <el-input v-model="profileForm.phoneNumber" disabled />
          </el-form-item>
          <el-form-item label="昵称">
            <el-input v-model="profileForm.nickname" :disabled="!isEditing" />
          </el-form-item>
          <el-form-item label="个人简介">
            <el-input
              v-model="profileForm.bio"
              type="textarea"
              :rows="3"
              :disabled="!isEditing"
            />
          </el-form-item>
          <el-form-item label="在线状态">
            <el-tag :type="user?.isOnline ? 'success' : 'info'">
              {{ user?.isOnline ? '在线' : '离线' }}
            </el-tag>
          </el-form-item>
          <el-form-item label="注册时间">
            <span>{{ formatDate(user?.createdAt) }}</span>
          </el-form-item>
        </el-form>
        
        <div v-if="isEditing" class="form-actions">
          <el-button @click="cancelEdit">取消</el-button>
          <el-button type="primary" :loading="isLoading" @click="saveProfile">保存</el-button>
        </div>
      </div>
    </el-card>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import { ElMessage } from 'element-plus'
import { useAuthStore } from '@/stores/auth'
import dayjs from 'dayjs'

const authStore = useAuthStore()

const isEditing = ref(false)
const isLoading = ref(false)

const user = computed(() => authStore.user)

const profileForm = reactive({
  username: '',
  email: '',
  phoneNumber: '',
  nickname: '',
  bio: ''
})

const initForm = () => {
  if (user.value) {
    profileForm.username = user.value.username
    profileForm.email = user.value.email
    profileForm.phoneNumber = user.value.phoneNumber
    profileForm.nickname = user.value.nickname || ''
    profileForm.bio = user.value.bio || ''
  }
}

const saveProfile = async () => {
  try {
    isLoading.value = true
    await authStore.updateUser({
      nickname: profileForm.nickname,
      bio: profileForm.bio
    })
    ElMessage.success('保存成功')
    isEditing.value = false
  } catch (error) {
    console.error('保存失败:', error)
  } finally {
    isLoading.value = false
  }
}

const cancelEdit = () => {
  initForm()
  isEditing.value = false
}

const handleAvatarUpload = () => {
  // 处理头像上传
  console.log('上传头像')
}

const formatDate = (date: string) => {
  return dayjs(date).format('YYYY-MM-DD HH:mm:ss')
}

onMounted(() => {
  initForm()
})
</script>

<style scoped>
.profile-container {
  max-width: 800px;
  margin: 20px auto;
  padding: 0 20px;
}

.profile-card {
  box-shadow: 0 2px 12px rgba(0, 0, 0, 0.1);
}

.card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.profile-content {
  display: flex;
  gap: 30px;
}

.avatar-section {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 10px;
}

.profile-form {
  flex: 1;
}

.form-actions {
  display: flex;
  justify-content: flex-end;
  gap: 10px;
  margin-top: 20px;
}
</style>
