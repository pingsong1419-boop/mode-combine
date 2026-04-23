<script setup lang="ts">
import { ref, onMounted, onUnmounted } from 'vue'
import { HubConnectionBuilder, LogLevel, type HubConnection } from '@microsoft/signalr'
import type { TighteningTask } from '../types/mes'
import { BACKEND_URL, SIGNALR_HUB_URL } from '../utils/constants'

const props = defineProps<{
  ip: string
  port: number
  tasks: TighteningTask[]
}>()

const emit = defineEmits<{
  (e: 'log', level: 'info'|'success'|'warn'|'error', msg: string): void
  (e: 'update:tasks', tasks: TighteningTask[]): void
}>()

// ==================== 状态管理 ====================
const isConnected = ref(false)
const currentPSet = ref<string>('-')
const currentTorque = ref<string>('0.000')
const currentAngle = ref<string>('0.0')
const statusText = ref('等待连接控制器...')

const isHeartbeatActive = ref(false)
const isPSetSet = ref(false)
const isToolEnabled = ref(false)
const isDataSubscribed = ref(false)

const targetPSet = ref('001')
let connection: HubConnection | null = null

onMounted(() => {
  initSignalR()
})

onUnmounted(() => {
  if (connection) connection.stop()
})

async function initSignalR() {
  connection = new HubConnectionBuilder()
    .withUrl(SIGNALR_HUB_URL) 
    .withAutomaticReconnect()
    .configureLogging(LogLevel.Information)
    .build()

  connection.on('ReceiveLog', (entry: any) => {
    if (entry.level === 'success' && entry.msg.includes('[RX]')) {
       statusText.value = 'MID 0002 通信已建立 (已连接)'
    }

    if (entry.isHeartbeat) {
      logHeartbeat(entry.level, entry.msg)
      isHeartbeatActive.value = false
      setTimeout(() => isHeartbeatActive.value = true, 100)
    } else {
      logLocal(entry.level, entry.msg)
    }
  })

  // 【核心交互】：处理拧紧结果并自动填入任务矩阵
  connection.on('ReceiveData', (data: any) => {
    currentTorque.value = data.torque
    currentAngle.value = data.angle
    statusText.value = `收到拧紧结果: ${data.status}`

    // 复制一份任务列表进行更新
    const updatedTasks = JSON.parse(JSON.stringify(props.tasks)) as TighteningTask[]
    
    // 逻辑：寻找当前所有任务中，第一个还没有实测值的“扭矩”项
    const nextTorqueIdx = updatedTasks.findIndex(t => t.paramName.includes('扭矩') && !t.actualValue)
    if (nextTorqueIdx !== -1) {
      const task = updatedTasks[nextTorqueIdx]
      task.actualValue = data.torque
      const val = parseFloat(data.torque)
      task.result = (val >= task.min && val <= task.max) ? 'PASS' : 'FAIL'
      task.timestamp = new Date().toLocaleTimeString()
      
      // 联动找到紧随其后的“角度”项（通常配对出现）
      const nextAngleIdx = updatedTasks.findIndex((t, idx) => idx > nextTorqueIdx && t.paramName.includes('角度') && !t.actualValue)
      if (nextAngleIdx !== -1) {
        const aTask = updatedTasks[nextAngleIdx]
        aTask.actualValue = data.angle
        const aVal = parseFloat(data.angle)
        aTask.result = (aVal >= aTask.min && aVal <= aTask.max) ? 'PASS' : 'FAIL'
        aTask.timestamp = new Date().toLocaleTimeString()
      }
    }

    emit('update:tasks', updatedTasks)
    logLocal('success', `结果入库：${data.torque}Nm / ${data.angle}Deg [${data.status}]`)
  })

  try {
    await connection.start()
    isConnected.value = true 
    logLocal('success', '[System] 已连接到 C# 实时通讯服务')
  } catch (err) {
    isConnected.value = false
    logLocal('error', '[System] 无法连接到 C# 服务')
  }
}

async function sendCommandToBackend(mid: string, pset: string = "") {
  try {
    await fetch(`${BACKEND_URL}/api/command?mid=${mid}&pset=${pset}`, { 
      method: 'POST' 
    })
  } catch (err) {
    logLocal('error', '[System] 指令下发失败，请求后端 API 异常')
  }
}

interface LocalLog {
  time: string
  level: string
  msg: string
}
const localLogs = ref<LocalLog[]>([])
const heartbeatLogs = ref<LocalLog[]>([])

function stringToHex(str: string): string {
  const parts: string[] = []
  const cleanStr = str.replace(/\\0/g, '\0')
  for (let i = 0; i < cleanStr.length; i++) {
    parts.push(cleanStr.charCodeAt(i).toString(16).toUpperCase().padStart(2, '0'))
  }
  return parts.join(' ')
}

function logLocal(level: 'info'|'success'|'warn'|'error', msg: string) {
  const time = new Date().toLocaleTimeString()
  let displayMsg = msg
  if (msg.startsWith('[TX] ') || msg.startsWith('[RX] ')) {
    const prefix = msg.slice(0, 5)
    const content = msg.slice(5)
    displayMsg = `${prefix}${stringToHex(content)}`
  }
  localLogs.value.unshift({ time, level, msg: displayMsg })
  if (localLogs.value.length > 50) localLogs.value.pop()
}

function logHeartbeat(level: 'info'|'success', msg: string) {
  const time = new Date().toLocaleTimeString()
  let displayMsg = msg
  if (msg.startsWith('[TX] ') || msg.startsWith('[RX] ')) {
    const prefix = msg.slice(0, 5)
    const content = msg.slice(5)
    displayMsg = `${prefix}${stringToHex(content)}`
  }
  heartbeatLogs.value.unshift({ time, level, msg: displayMsg })
  if (heartbeatLogs.value.length > 20) heartbeatLogs.value.pop()
}

function handleConnect() {
  logLocal('info', `[System] 请求后端建立 TCP 连接...`)
  // C# 后端通常会自动维持连接，这里可以做重连触发
}

function handleDisconnect() {
  // 断开业务逻辑
}

async function sendPSet() {
  const psetVal = targetPSet.value.toString().padStart(3, '0')
  await sendCommandToBackend('0018', psetVal)
  statusText.value = `正在下载 PSet ${psetVal}...`
  isPSetSet.value = true
}

async function enableTool() {
  await sendCommandToBackend('0043')
  statusText.value = `正在解锁工具...`
}

async function lockTool() {
  await sendCommandToBackend('0042')
  statusText.value = `正在强制锁定工具...`
}

async function subscribeData() {
  await sendCommandToBackend('0060')
  statusText.value = `正在订阅拧紧结果数据...`
  isDataSubscribed.value = true
}

// receiveResult 逻辑现在由后端自动推送处理

</script>

<template>
  <div class="torque-panel">
    <div class="header">
      <span class="icon">🔧</span> 定扭控制器对接 (Desoutter Open Protocol)
    </div>

    <!-- 顶栏连接状态区域 -->
    <div class="status-bar" :class="{ connected: isConnected }">
      <div class="conn-info">
        <div class="ip-row">
          <span class="label">目标控制器：</span>
          <span class="mono ip-text">{{ ip }}:{{ port }}</span>
        </div>
        <div class="status-row">
          <div class="indicator" :class="{ 'on': isConnected }"></div>
          <span>{{ statusText }}</span>
        </div>
      </div>
      <div class="actions">
        <button v-if="!isConnected" class="btn connect" @click="handleConnect">🔗 连接接口 (MID 0001)</button>
        <button v-else class="btn disconnect" @click="handleDisconnect">🔌 断开连接 (MID 0003)</button>
      </div>
    </div>

    <div class="dashboard" :class="{ 'dimmed': !isConnected }">
      <!-- 状态指示灯 -->
      <div class="state-flags">
        <div class="flag" :class="{ on: isHeartbeatActive }">
          <div class="flag-led"></div>心跳信号(MID 9999)
        </div>
        <div class="flag" :class="{ on: isPSetSet }">
          <div class="flag-led"></div>程序设定(MID 0019)
        </div>
        <div class="flag" :class="{ on: isToolEnabled }">
          <div class="flag-led"></div>枪体使能(MID 0044)
        </div>
        <div class="flag" :class="{ on: isDataSubscribed }">
          <div class="flag-led"></div>实时数据(MID 0060)
        </div>
      </div>

      <!-- 实时数据看板 -->
      <div class="dashboard-metrics">
        <div class="metric-card">
          <div class="m-label">当前 PSet</div>
          <div class="m-val mono">{{ currentPSet }}</div>
        </div>
        <div class="metric-card highlight">
          <div class="m-label">实时扭矩 (Nm)</div>
          <div class="m-val mono green">{{ currentTorque }}</div>
        </div>
        <div class="metric-card highlight">
          <div class="m-label">实时角度 (deg)</div>
          <div class="m-val mono green">{{ currentAngle }}</div>
        </div>
      </div>

      <!-- 操作面板 -->
      <div class="controls-panel">
        <h3 class="panel-title">快捷指令下发及测试</h3>
        <div class="cmd-buttons">
          <div class="pset-input-wrap">
            <span class="pset-label">PSet编号:</span>
            <input type="text" v-model="targetPSet" class="pset-input mono" placeholder="001" maxlength="3">
          </div>
          <button class="btn cmd" @click="sendPSet" :disabled="!isConnected">
            📥 PSet 设置 (MID 0018)
          </button>
          <button class="btn cmd primary" @click="enableTool" :disabled="!isConnected">
            🔓 枪体使能 (MID 0043)
          </button>
          <button class="btn cmd lock" @click="lockTool" :disabled="!isConnected">
            🔒 锁定工具 (MID 0042)
          </button>
          <button class="btn cmd outline" @click="subscribeData" :disabled="!isConnected">
            📡 获取数据 (MID 0060)
          </button>
        </div>


        <div class="logs-container">
          <!-- 主通信日志 -->
          <div class="terminal-panel flex-2">
            <div class="terminal-header">
              <span>实时通讯日志 (Open Protocol 消息流)</span>
              <button class="clear-btn" @click="localLogs = []">清空</button>
            </div>
            <div class="terminal-content">
              <div 
                v-for="(log, idx) in localLogs" 
                :key="idx" 
                class="t-log"
                :class="log.level"
              >
                <span class="t-time">[{{ log.time }}]</span>
                <span class="t-msg">{{ log.msg }}</span>
              </div>
              <div v-if="!localLogs.length" class="t-empty">暂无交互日志...</div>
            </div>
          </div>

          <!-- 心跳专用独立日志 -->
          <div class="terminal-panel heartbeat-panel">
            <div class="terminal-header">
              <span>心跳专属保活日志 (Keep-Alive)</span>
            </div>
            <div class="terminal-content">
              <div 
                v-for="(log, idx) in heartbeatLogs" 
                :key="'hb-'+idx" 
                class="t-log"
                :class="log.level"
              >
                <span class="t-time">[{{ log.time }}]</span>
                <span class="t-msg">{{ log.msg }}</span>
              </div>
              <div v-if="!heartbeatLogs.length" class="t-empty">等待心跳启动...</div>
            </div>
          </div>
        </div>

      </div>
    </div>

  </div>
</template>

<style scoped>
.torque-panel {
  display: flex;
  flex-direction: column;
  height: 100%;
  background: #111520;
  border-radius: 8px;
  border: 1px solid rgba(171, 71, 188, 0.15);
}

.header {
  padding: 12px 16px;
  background: linear-gradient(90deg, rgba(142, 36, 170, 0.3), transparent);
  border-bottom: 1px solid rgba(171, 71, 188, 0.15);
  font-weight: 600;
  color: #e1bee7;
  display: flex;
  align-items: center;
  gap: 8px;
}

.status-bar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 16px;
  background: #0d1117;
  border-bottom: 1px solid rgba(100, 181, 246, 0.1);
}

.status-bar.connected {
  background: rgba(0, 230, 118, 0.03);
}

.conn-info {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.ip-row {
  font-size: 13px;
  color: #cfd8dc;
}
.ip-text {
  color: #64b5f6;
  font-weight: bold;
}

.status-row {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 13px;
  color: #90caf9;
}

.indicator {
  width: 10px;
  height: 10px;
  border-radius: 50%;
  background: #f44336;
  box-shadow: 0 0 8px rgba(244, 67, 54, 0.5);
}
.indicator.on {
  background: #00e676;
  box-shadow: 0 0 8px rgba(0, 230, 118, 0.5);
}

.actions {
  display: flex;
  gap: 12px;
}

.btn {
  border: none;
  border-radius: 6px;
  padding: 8px 16px;
  font-size: 13px;
  font-weight: 600;
  cursor: pointer;
  transition: all 0.2s;
}
.btn:disabled { opacity: 0.5; cursor: not-allowed; }

.btn.connect {
  background: rgba(66, 165, 245, 0.15);
  color: #64b5f6;
  border: 1px solid rgba(66, 165, 245, 0.3);
}
.btn.connect:hover { background: rgba(66, 165, 245, 0.25); }

.btn.disconnect {
  background: rgba(244, 67, 54, 0.15);
  color: #e57373;
  border: 1px solid rgba(244, 67, 54, 0.3);
}

.dashboard {
  flex: 1;
  padding: 24px;
  display: flex;
  flex-direction: column;
  gap: 24px;
  transition: opacity 0.3s;
}
.dashboard.dimmed { opacity: 0.3; pointer-events: none; }

.dashboard-metrics {
  display: flex;
  gap: 16px;
}

.metric-card {
  flex: 1;
  background: rgba(100, 181, 246, 0.05);
  border: 1px solid rgba(100, 181, 246, 0.1);
  border-radius: 8px;
  padding: 20px;
  text-align: center;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 12px;
}

.metric-card.highlight {
  background: rgba(0, 230, 118, 0.03);
  border-color: rgba(0, 230, 118, 0.15);
}

.m-label {
  font-size: 12px;
  color: #78909c;
  font-weight: 600;
}

.m-val {
  font-size: 36px;
  font-weight: 700;
  color: #e0e6ed;
}

.m-val.green {
  color: #00e676;
  text-shadow: 0 0 16px rgba(0, 230, 118, 0.2);
}

.controls-panel {
  margin-top: auto;
  border-top: 1px solid rgba(100, 181, 246, 0.1);
  padding-top: 20px;
}

.panel-title {
  margin: 0 0 16px 0;
  font-size: 14px;
  color: #b0bec5;
}

.cmd-buttons {
  display: flex;
  flex-wrap: wrap;
  gap: 16px;
  margin-bottom: 24px;
  align-items: center;
}

.pset-input-wrap {
  display: flex;
  align-items: center;
  gap: 8px;
  background: rgba(255, 255, 255, 0.05);
  padding: 6px 14px;
  border-radius: 6px;
  border: 1px solid rgba(255, 255, 255, 0.1);
  box-shadow: inset 0 2px 4px rgba(0, 0, 0, 0.2);
}
.pset-label {
  font-size: 13px;
  color: #90a4ae;
  font-weight: 500;
}
.pset-input {
  background: transparent;
  border: none;
  border-bottom: 2px solid #64b5f6;
  color: #00e676;
  width: 50px;
  text-align: center;
  font-size: 18px;
  font-weight: bold;
  outline: none;
  transition: all 0.2s;
}
.pset-input:focus {
  border-bottom-color: #00c853;
  background: rgba(0, 230, 118, 0.05);
}

.btn.cmd {
  background: rgba(142, 36, 170, 0.15);
  color: #ce93d8;
  border: 1px solid rgba(142, 36, 170, 0.3);
}

.btn.cmd.lock {
  background: rgba(244, 67, 54, 0.2);
  color: #ef9a9a;
  border: 1px solid rgba(244, 67, 54, 0.4);
}

.btn.cmd.lock:hover {
  background: rgba(244, 67, 54, 0.3);
}

.btn.cmd.primary {
  background: linear-gradient(135deg, #1565c0, #0d47a1);
  color: white;
  border: none;
}

.btn.cmd.outline {
  background: transparent;
  color: #64b5f6;
  border: 1px dashed rgba(66, 165, 245, 0.5);
}
.btn.cmd.outline:hover {
  background: rgba(66, 165, 245, 0.1);
}

.state-flags {
  display: flex;
  justify-content: space-between;
  background: rgba(255, 255, 255, 0.02);
  border: 1px solid rgba(255, 255, 255, 0.05);
  padding: 16px;
  border-radius: 8px;
}

.flag {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 13px;
  color: #78909c;
  transition: color 0.3s;
}

.flag-led {
  width: 14px;
  height: 14px;
  border-radius: 50%;
  background: #37474f;
  box-shadow: inset 0 2px 4px rgba(0,0,0,0.5);
  transition: all 0.3s;
}

.flag.on {
  color: #e0e6ed;
  font-weight: 600;
}

.flag.on .flag-led {
  background: #00e676;
  box-shadow: 0 0 10px rgba(0, 230, 118, 0.5), inset 0 1px 2px rgba(255,255,255,0.5);
}

.info-note {
  font-size: 12px;
  color: #78909c;
  line-height: 1.5;
  background: rgba(0, 0, 0, 0.2);
  padding: 12px;
  border-radius: 6px;
  border-left: 3px solid #64b5f6;
}

.logs-container {
  display: flex;
  gap: 16px;
  margin-top: 16px;
}

.terminal-panel {
  background: #0d1117;
  border: 1px solid rgba(100, 181, 246, 0.2);
  border-radius: 8px;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  max-height: 200px;
}
.terminal-panel.flex-2 {
  flex: 2;
}
.terminal-panel.heartbeat-panel {
  flex: 1;
  border-color: rgba(0, 230, 118, 0.2);
}
.heartbeat-panel .terminal-header {
  background: rgba(0, 230, 118, 0.05);
  border-bottom-color: rgba(0, 230, 118, 0.1);
  color: #69f0ae;
}

.terminal-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 8px 12px;
  background: rgba(100, 181, 246, 0.05);
  border-bottom: 1px solid rgba(100, 181, 246, 0.1);
  font-size: 12px;
  color: #90caf9;
  font-weight: 600;
}

.clear-btn {
  background: none;
  border: 1px solid rgba(244, 67, 54, 0.3);
  color: #e57373;
  border-radius: 4px;
  font-size: 11px;
  cursor: pointer;
  padding: 2px 8px;
  transition: all 0.2s;
}
.clear-btn:hover {
  background: rgba(244, 67, 54, 0.1);
}

.terminal-content {
  padding: 8px;
  overflow-y: auto;
  font-family: 'Consolas', monospace;
  font-size: 12px;
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.t-log {
  display: flex;
  gap: 8px;
  padding: 4px 6px;
  border-radius: 4px;
}
.t-log:hover {
  background: rgba(255, 255, 255, 0.03);
}
.t-time {
  color: #78909c;
  flex-shrink: 0;
}
.t-msg {
  word-break: break-all;
  white-space: pre-wrap;
}

.t-log.info .t-msg { color: #e0e6ed; }
.t-log.success .t-msg { color: #00e676; }
.t-log.warn .t-msg { color: #ffb300; }
.t-log.error .t-msg { color: #f44336; }

.t-empty {
  color: #546e7a;
  text-align: center;
  padding: 16px;
  font-style: italic;
}

.mono { font-family: 'Consolas', monospace; }
</style>
