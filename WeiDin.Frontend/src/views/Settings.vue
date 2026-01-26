<template>
  <div class="settings-container">
    <el-card>
      <template #header>
        <div class="card-header">
          <span>设置</span>
          <el-button type="primary" @click="goToChat">
            <el-icon><ArrowLeft /></el-icon>
            返回聊天
          </el-button>
        </div>
      </template>
      
      <div class="settings-content">
        <el-empty description="设置功能待开发中">
          <template #description>
            <p>设置功能待开发中</p>
          </template>
        </el-empty>
      </div>
    </el-card>
  </div>
</template>

<script setup lang="ts">
import { useRouter } from 'vue-router'
import { ArrowLeft } from '@element-plus/icons-vue'

const router = useRouter()

const goToChat = () => {
  router.push('/chat')
}
</script>

<style scoped>
.settings-container {
  max-width: 800px;
  margin: 20px auto;
  padding: 0 20px;
}

.card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.settings-content {
  padding: 40px 20px;
  text-align: center;
}
</style>
