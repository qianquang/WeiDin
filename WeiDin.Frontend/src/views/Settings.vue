<template>
  <div class="settings-container">
    <el-card>
      <template #header>
        <span>系统设置</span>
      </template>
      
      <el-tabs v-model="activeTab">
        <el-tab-pane label="账户设置" name="account">
          <el-form :model="accountSettings" label-width="120px">
            <el-form-item label="修改密码">
              <el-button @click="showChangePasswordDialog = true">修改密码</el-button>
            </el-form-item>
            <el-form-item label="在线状态">
              <el-switch v-model="accountSettings.isOnline" @change="handleOnlineStatusChange" />
              <span class="setting-desc">{{ accountSettings.isOnline ? '显示为在线' : '显示为离线' }}</span>
            </el-form-item>
          </el-form>
        </el-tab-pane>
        
        <el-tab-pane label="通知设置" name="notifications">
          <el-form :model="notificationSettings" label-width="120px">
            <el-form-item label="消息通知">
              <el-switch v-model="notificationSettings.messageNotification" />
            </el-form-item>
            <el-form-item label="声音提醒">
              <el-switch v-model="notificationSettings.soundNotification" />
            </el-form-item>
            <el-form-item label="桌面通知">
              <el-switch v-model="notificationSettings.desktopNotification" />
            </el-form-item>
          </el-form>
        </el-tab-pane>
        
        <el-tab-pane label="隐私设置" name="privacy">
          <el-form :model="privacySettings" label-width="120px">
            <el-form-item label="允许陌生人添加">
              <el-switch v-model="privacySettings.allowStrangerAdd" />
            </el-form-item>
            <el-form-item label="显示在线状态">
              <el-switch v-model="privacySettings.showOnlineStatus" />
            </el-form-item>
            <el-form-item label="显示最后在线时间">
              <el-switch v-model="privacySettings.showLastSeen" />
            </el-form-item>
          </el-form>
        </el-tab-pane>
        
        <el-tab-pane label="关于" name="about">
          <div class="about-content">
            <div class="app-info">
              <h3>微钉即时通讯系统</h3>
              <p>版本: 1.0.0</p>
              <p>基于 Vue 3 + TypeScript + Element Plus 开发</p>
            </div>
            <div class="contact-info">
              <h4>联系我们</h4>
              <p>邮箱: support@weidin.com</p>
              <p>官网: https://www.weidin.com</p>
            </div>
          </div>
        </el-tab-pane>
      </el-tabs>
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
import { ref, reactive, onMounted } from 'vue'
import { ElMessage, type FormInstance, type FormRules } from 'element-plus'
import { useAuthStore } from '@/stores/auth'

const authStore = useAuthStore()

const activeTab = ref('account')
const showChangePasswordDialog = ref(false)
const isChangingPassword = ref(false)

const accountSettings = reactive({
  isOnline: authStore.user?.isOnline || false
})

const notificationSettings = reactive({
  messageNotification: true,
  soundNotification: true,
  desktopNotification: true
})

const privacySettings = reactive({
  allowStrangerAdd: true,
  showOnlineStatus: true,
  showLastSeen: true
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

const passwordFormRef = ref<FormInstance>()

const handleOnlineStatusChange = async (isOnline: boolean) => {
  try {
    await authStore.setOnlineStatus(isOnline)
    ElMessage.success(isOnline ? '已设置为在线' : '已设置为离线')
  } catch (error) {
    console.error('设置在线状态失败:', error)
    accountSettings.isOnline = !isOnline // 回滚状态
  }
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
    console.error('修改密码失败:', error)
  } finally {
    isChangingPassword.value = false
  }
}

onMounted(() => {
  // 加载设置
  accountSettings.isOnline = authStore.user?.isOnline || false
})
</script>

<style scoped>
.settings-container {
  max-width: 800px;
  margin: 20px auto;
  padding: 0 20px;
}

.setting-desc {
  margin-left: 10px;
  color: #666;
  font-size: 14px;
}

.about-content {
  padding: 20px 0;
}

.app-info {
  margin-bottom: 30px;
}

.app-info h3 {
  color: #333;
  margin-bottom: 10px;
}

.app-info p {
  color: #666;
  margin: 5px 0;
}

.contact-info h4 {
  color: #333;
  margin-bottom: 10px;
}

.contact-info p {
  color: #666;
  margin: 5px 0;
}
</style>
