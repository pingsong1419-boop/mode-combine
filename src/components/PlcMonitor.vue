<script setup lang="ts">
import { ref, onMounted, onUnmounted } from 'vue'

interface PlcLogEntry {
  time: string
  action: 'READ' | 'WRITE' | 'HEARTBEAT'
  address: string
  value: string
  status: 'SUCCESS' | 'ERROR'
}

const props = defineProps<{
  logs: PlcLogEntry[]
}>()

const autoScroll = ref(true)
const logContainer = ref<HTMLElement | null>(null)

// 过滤器状态
const filterAction = ref<'ALL' | 'READ' | 'WRITE' | 'HEARTBEAT'>('ALL')
const filterAddress = ref('')
const filterStatus = ref<'ALL' | 'SUCCESS' | 'ERROR'>('ALL')

import { computed, watch, onUpdated } from 'vue'

// 计算过滤后的日志
const filteredLogs = computed(() => {
  return props.logs.filter(log => {
    const matchAction = filterAction.value === 'ALL' || log.action === filterAction.value
    const matchStatus = filterStatus.value === 'ALL' || log.status === filterStatus.value
    const matchAddress = !filterAddress.value || log.address.toLowerCase().includes(filterAddress.value.toLowerCase())
    return matchAction && matchStatus && matchAddress
  })
})

// 监听日志长度变化
watch(() => props.logs.length, () => {
  if (autoScroll.value) {
    scrollToBottom()
  }
})

// 确保初始加载或更新时也执行一次
const scrollToBottom = () => {
  nextTick(() => {
    if (logContainer.value) {
      logContainer.value.scrollTop = logContainer.value.scrollHeight
    }
  })
}

// 处理滚动事件，实现“手动滚动时自动暂停”
const handleScroll = () => {
  if (!logContainer.value) return
  
  const { scrollTop, scrollHeight, clientHeight } = logContainer.value
  // 如果当前不在底部（预留 10px 误差），且自动滚动原本是开着的
  // 则认为用户在手动向上翻阅，自动关闭自动滚动
  const isAtBottom = scrollTop + clientHeight >= scrollHeight - 10
  
  if (!isAtBottom && autoScroll.value) {
    autoScroll.value = false
  }
}

// 在组件更新后再次强制滚动（双重保险）
onUpdated(() => {
  // 严格检查：只有勾选了且有容器时才滚动
  if (autoScroll.value === true && logContainer.value) {
    logContainer.value.scrollTop = logContainer.value.scrollHeight
  }
})

import { nextTick } from 'vue'

const clearLogs = () => {
  // 注意：如果是 props，应该通知父组件清除，或者在本地维护一个 offset
  // 这里为了简单，我们先不实现清除功能，或者由父组件清除
}
</script>

<template>
  <div class="plc-monitor">
    <div class="monitor-header">
      <div class="title">
        <span class="pulse-icon"></span>
        PLC 监视器 (Raw)
      </div>
      
      <div class="monitor-filters">
        <select v-model="filterAction" class="filter-select">
          <option value="ALL">所有动作</option>
          <option value="READ">读取 (READ)</option>
          <option value="WRITE">写入 (WRITE)</option>
          <option value="HEARTBEAT">心跳 (HB)</option>
        </select>
        
        <input type="text" v-model="filterAddress" placeholder="搜索地址..." class="filter-input" />
        
        <select v-model="filterStatus" class="filter-select">
          <option value="ALL">所有状态</option>
          <option value="SUCCESS">成功 (OK)</option>
          <option value="ERROR">失败 (ERR)</option>
        </select>
      </div>

      <div class="controls">
        <label class="checkbox-label">
          <input type="checkbox" v-model="autoScroll"> 自动滚动
        </label>
        <button class="clear-btn" @click="clearLogs">清除</button>
      </div>
    </div>

    <div class="monitor-content" ref="logContainer" @scroll="handleScroll">
      <table class="log-table">
        <thead>
          <tr>
            <th width="110">时间</th>
            <th width="80">动作</th>
            <th width="150">地址</th>
            <th>数据结果</th>
            <th width="80">状态</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="(log, index) in filteredLogs" :key="index" :class="log.status.toLowerCase()">
            <td class="time">{{ log.time }}</td>
            <td>
              <span :class="['type-tag', log.action.toLowerCase()]">{{ log.action }}</span>
            </td>
            <td class="address">{{ log.address }}</td>
            <td class="value">{{ log.value }}</td>
            <td class="status">
              <span :class="['status-dot', log.status.toLowerCase()]"></span>
              {{ log.status }}
            </td>
          </tr>
          <tr v-if="filteredLogs.length === 0">
            <td colspan="5" class="empty">没有符合条件的记录</td>
          </tr>
        </tbody>
      </table>
    </div>
  </div>
</template>

<style scoped>
.plc-monitor {
  display: flex;
  flex-direction: column;
  height: 100%;
  background: #0d1117;
  border: 1px solid #30363d;
  border-radius: 8px;
  overflow: hidden;
  font-family: 'Consolas', 'Monaco', monospace;
}

.monitor-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 12px 20px;
  background: #161b22;
  border-bottom: 1px solid #30363d;
}

.title {
  display: flex;
  align-items: center;
  gap: 10px;
  color: #58a6ff;
  font-size: 14px;
  font-weight: bold;
}

.pulse-icon {
  width: 8px;
  height: 8px;
  background: #238636;
  border-radius: 50%;
  box-shadow: 0 0 8px #238636;
  animation: pulse 2s infinite;
}

@keyframes pulse {
  0% { opacity: 1; }
  50% { opacity: 0.4; }
  100% { opacity: 1; }
}

.monitor-filters {
  display: flex;
  gap: 10px;
  flex: 1;
  justify-content: center;
}

.filter-select, .filter-input {
  background: #161b22;
  border: 1px solid #30363d;
  color: #c9d1d9;
  padding: 4px 8px;
  border-radius: 4px;
  font-size: 11px;
  outline: none;
}

.filter-input {
  width: 150px;
}

.filter-select:focus, .filter-input:focus {
  border-color: #58a6ff;
}

.controls {
  display: flex;
  align-items: center;
  gap: 15px;
}

.checkbox-label {
  color: #8b949e;
  font-size: 12px;
  display: flex;
  align-items: center;
  gap: 5px;
  cursor: pointer;
}

.clear-btn {
  background: transparent;
  border: 1px solid #30363d;
  color: #f85149;
  padding: 4px 12px;
  border-radius: 4px;
  font-size: 12px;
  cursor: pointer;
  transition: all 0.2s;
}

.clear-btn:hover {
  background: rgba(248, 81, 73, 0.1);
  border-color: #f85149;
}

.monitor-content {
  flex: 1;
  overflow-y: auto;
  padding: 0;
}

.log-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 12px;
}

.log-table th {
  position: sticky;
  top: 0;
  background: #161b22;
  color: #8b949e;
  text-align: left;
  padding: 10px 15px;
  border-bottom: 1px solid #30363d;
  z-index: 10;
}

.log-table td {
  padding: 8px 15px;
  border-bottom: 1px solid #21262d;
  color: #c9d1d9;
}

.log-table tr:hover {
  background: rgba(255, 255, 255, 0.03);
}

.log-table tr.error td {
  color: #ff7b72;
}

.time { color: #8b949e; }
.address { color: #d2a8ff; font-weight: bold; }
.value { color: #79c0ff; }

.type-tag {
  padding: 2px 6px;
  border-radius: 3px;
  font-size: 10px;
  font-weight: bold;
}

.type-tag.read { background: #0d47a1; color: #e3f2fd; }
.type-tag.write { background: #1b5e20; color: #e8f5e9; }
.type-tag.heartbeat { background: #333; color: #aaa; }

.status-dot {
  display: inline-block;
  width: 6px;
  height: 6px;
  border-radius: 50%;
  margin-right: 5px;
}
.status-dot.success { background: #3fb950; box-shadow: 0 0 5px #3fb950; }
.status-dot.error { background: #f85149; box-shadow: 0 0 5px #f85149; }

.empty {
  text-align: center;
  padding: 40px !important;
  color: #484f58;
  font-style: italic;
}
</style>
