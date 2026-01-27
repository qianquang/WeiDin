<template>
  <div class="profile-container">
    <el-card class="profile-card">
      <template #header>
        <div class="card-header">
          <span>个人资料</span>
          <div class="header-actions">
            <el-button type="primary" @click="goToChat">
              <el-icon><ArrowLeft /></el-icon>
              返回聊天
            </el-button>
            <el-button type="primary" @click="isEditing = true">编辑</el-button>
          </div>
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
          <el-form-item label="用户ID">
            <el-input v-model="profileForm.id" disabled />
          </el-form-item>
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
            <span>{{ formatDate(user?.createdAt || '') }}</span>
          </el-form-item>
          <el-form-item label="修改密码">
            <el-button @click="showChangePasswordDialog = true">修改密码</el-button>
          </el-form-item>
        </el-form>
        
        <div v-if="isEditing" class="form-actions">
          <el-button @click="cancelEdit">取消</el-button>
          <el-button type="primary" :loading="isLoading" @click="saveProfile">保存</el-button>
        </div>
      </div>
    </el-card>

    <!-- 修改密码对话框 -->
    <el-dialog v-model="showChangePasswordDialog" title="修改密码" width="400px">
      <el-form :model="passwordForm" :rules="passwordRules" ref="passwordFormRef" label-width="100px">
        <el-form-item label="当前密码" prop="currentPassword">
          <el-input v-model="passwordForm.currentPassword" type="password" show-password />
        </el-form-item>
        <el-form-item label="新密码" prop="newPassword">
          <el-input v-model="passwordForm.newPassword" type="password" show-password />
        </el-form-item>
        <el-form-item label="确认密码" prop="confirmPassword">
          <el-input v-model="passwordForm.confirmPassword" type="password" show-password />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="showChangePasswordDialog = false">取消</el-button>
        <el-button type="primary" :loading="isChangingPassword" @click="handleChangePassword">确定</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage, type FormInstance, type FormRules } from 'element-plus'
import { ArrowLeft } from '@element-plus/icons-vue'
import { useAuthStore } from '@/stores/auth'
import dayjs from 'dayjs'

const router = useRouter()

const authStore = useAuthStore()

const isEditing = ref(false)
const isLoading = ref(false)
const showChangePasswordDialog = ref(false)
const isChangingPassword = ref(false)
const passwordFormRef = ref<FormInstance>()

const user = computed(() => authStore.user)

const profileForm = reactive({
  id: '',
  username: '',
  email: '',
  phoneNumber: '',
  nickname: '',
  bio: ''
})

const passwordForm = reactive({
  currentPassword: '',
  newPassword: '',
  confirmPassword: ''
})

const passwordRules: FormRules = {
  currentPassword: [
    { required: true, message: '请输入当前密码', trigger: 'blur' }
  ],
  newPassword: [
    { required: true, message: '请输入新密码', trigger: 'blur' },
    { min: 6, max: 20, message: '密码长度在 6 到 20 个字符', trigger: 'blur' }
  ],
  confirmPassword: [
    { required: true, message: '请确认新密码', trigger: 'blur' },
    {
      validator: (rule, value, callback) => {
        if (value !== passwordForm.newPassword) {
          callback(new Error('两次输入的密码不一致'))
        } else {
          callback()
        }
      },
      trigger: 'blur'
    }
  ]
}

const initForm = () => {
  if (user.value) {
    profileForm.id = user.value.id || ''
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
    // 保存失败，错误消息已通过 ElMessage 显示
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
}

const handleChangePassword = async () => {
  if (!passwordFormRef.value) return
  
  try {
    await passwordFormRef.value.validate()
    isChangingPassword.value = true
    
    await authStore.changePassword({
      currentPassword: passwordForm.currentPassword,
      newPassword: passwordForm.newPassword
    })
    
    ElMessage.success('密码修改成功')
    showChangePasswordDialog.value = false
    passwordForm.currentPassword = ''
    passwordForm.newPassword = ''
    passwordForm.confirmPassword = ''
  } catch (error) {
    // 修改密码失败，错误消息已通过 ElMessage 显示
  } finally {
    isChangingPassword.value = false
  }
}

const formatDate = (date: string) => {
  return dayjs(date).format('YYYY-MM-DD HH:mm:ss')
}

const goToChat = () => {
  router.push('/chat')
}

onMounted(async () => {
  try {
    // 如果用户信息不存在但有 token，从 API 刷新用户信息
    if (!authStore.user && authStore.token) {
      await authStore.refreshUser()
    } else if (!authStore.user && !authStore.token) {
      ElMessage.warning('请先登录')
      return
    }
    
    // 如果刷新后仍然没有用户信息，说明可能未登录或 token 无效
    if (!authStore.user) {
      ElMessage.warning('用户信息加载失败，请重新登录')
      return
    }
    
    // 初始化表单
    initForm()
  } catch (error) {
    ElMessage.error('加载用户信息失败，请重新登录')
  }
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

.header-actions {
  display: flex;
  gap: 10px;
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
