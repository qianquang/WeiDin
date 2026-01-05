<template>
  <div class="friends-container">
    <el-card>
      <template #header>
        <div class="card-header">
          <span>好友管理</span>
          <el-button type="primary" @click="showAddFriendDialog = true">添加好友</el-button>
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
                <el-avatar :size="40" :src="friend.friendAvatar">
                  {{ friend.friendName.charAt(0) }}
                </el-avatar>
                <div class="friend-info">
                  <div class="friend-name">{{ friend.friendName }}</div>
                  <div class="friend-remark">{{ friend.remark || '暂无备注' }}</div>
                </div>
                <div class="friend-actions">
                  <el-button type="text" @click="startChat(friend)">聊天</el-button>
                  <el-button type="text" @click="editFriend(friend)">编辑</el-button>
                  <el-button type="text" @click="removeFriend(friend.id)">删除</el-button>
                </div>
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
                <el-avatar :size="40" :src="blocked.blockedUserAvatar">
                  {{ blocked.blockedUserName.charAt(0) }}
                </el-avatar>
                <div class="blacklist-info">
                  <div class="blocked-name">{{ blocked.blockedUserName }}</div>
                  <div class="block-reason">{{ blocked.reason || '无原因' }}</div>
                </div>
                <el-button type="text" @click="removeFromBlacklist(blocked.id)">移除</el-button>
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
import { ref, reactive, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import { friendshipApi } from '@/api'
import type { Friendship, CreateFriendshipDto, UpdateFriendshipDto } from '@/types'

const router = useRouter()

const activeTab = ref('friends')
const showAddFriendDialog = ref(false)
const showEditFriendDialog = ref(false)
const friends = ref<Friendship[]>([])
const blacklist = ref<any[]>([])

const addFriendForm = reactive<CreateFriendshipDto>({
  friendId: '',
  remark: ''
})

const editFriendForm = reactive<UpdateFriendshipDto & { id: string }>({
  id: '',
  remark: ''
})

const loadFriends = async () => {
  try {
    friends.value = await friendshipApi.getFriendships()
  } catch (error) {
    console.error('加载好友列表失败:', error)
  }
}

const loadBlacklist = async () => {
  try {
    blacklist.value = await friendshipApi.getBlacklist()
  } catch (error) {
    console.error('加载黑名单失败:', error)
  }
}

const handleAddFriend = async () => {
  try {
    await friendshipApi.addFriend(addFriendForm)
    ElMessage.success('添加好友成功')
    showAddFriendDialog.value = false
    addFriendForm.friendId = ''
    addFriendForm.remark = ''
    loadFriends()
  } catch (error) {
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
    await friendshipApi.updateFriendship(editFriendForm.id, {
      remark: editFriendForm.remark
    })
    ElMessage.success('编辑成功')
    showEditFriendDialog.value = false
    loadFriends()
  } catch (error) {
    console.error('编辑好友失败:', error)
  }
}

const removeFriend = async (id: string) => {
  try {
    await ElMessageBox.confirm('确定要删除这个好友吗？', '确认删除', {
      type: 'warning'
    })
    await friendshipApi.removeFriend(id)
    ElMessage.success('删除成功')
    loadFriends()
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
    await friendshipApi.removeFromBlacklist(id)
    ElMessage.success('移除成功')
    loadBlacklist()
  } catch (error) {
    if (error !== 'cancel') {
      console.error('移除黑名单失败:', error)
    }
  }
}

const startChat = (friend: Friendship) => {
  router.push(`/chat?friend=${friend.friendId}`)
}

onMounted(() => {
  loadFriends()
  loadBlacklist()
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

.friends-list,
.blacklist-list {
  max-height: 500px;
  overflow-y: auto;
}

.friend-item,
.blacklist-item {
  display: flex;
  align-items: center;
  padding: 15px;
  border-bottom: 1px solid #f0f0f0;
}

.friend-info,
.blacklist-info {
  flex: 1;
  margin-left: 15px;
}

.friend-name,
.blocked-name {
  font-weight: 500;
  color: #333;
}

.friend-remark,
.block-reason {
  font-size: 12px;
  color: #999;
  margin-top: 5px;
}

.friend-actions {
  display: flex;
  gap: 10px;
}
</style>
