<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { BACKEND_URL } from '../utils/constants'

const props = defineProps<{
  ip: string
  cpuType: string
  rack: number
  slot: number
}>()

const emit = defineEmits<{
  (e: 'log', level: 'info'|'success'|'warn'|'error', msg: string): void
}>()

// ==================== 状态管理 ====================
const readAddress = ref('DB1.DBB0')
const readCount = ref(1)
const readValue = ref<any>('--')
const hexValue = ref('')      // 十六进制显示
const stringValue = ref('')   // 字符串显示
const writeAddress = ref('DB1.DBD0')
const writeValue = ref<any>('')
const isReading = ref(false)
const isWriting = ref(false)
const isConfiguring = ref(false)

async function configurePlc() {
  isConfiguring.value = true
  try {
    const res = await fetch(`${BACKEND_URL}/api/plc/config`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        ip: props.ip,
        cpuType: props.cpuType,
        rack: props.rack,
        slot: props.slot
      })
    })
    if (res.ok) {
      emit('log', 'success', `[PLC] 配置已下发: ${props.ip}`)
    } else {
      emit('log', 'error', '[PLC] 配置下发失败')
    }
  } catch (err) {
    emit('log', 'error', '[PLC] 无法连接到后端服务')
  } finally {
    isConfiguring.value = false
  }
}

async function handleRead() {
  if (!readAddress.value) return
  isReading.value = true
  try {
    const res = await fetch(`${BACKEND_URL}/api/plc/read?address=${readAddress.value}&count=${readCount.value}`)
    if (res.ok) {
      const data = await res.json()
      let bytes: number[] = []

      // 处理 .NET 默认发送的 Base64 字符串或数组
      if (typeof data.value === 'string') {
        try {
          const binaryString = atob(data.value)
          bytes = Array.from(binaryString).map(char => char.charCodeAt(0))
        } catch (e) {
          bytes = []
        }
      } else if (Array.isArray(data.value)) {
        bytes = data.value
      }

      if (bytes.length > 0) {
        // 1. 十六进制空格分隔
        hexValue.value = bytes.map(b => b.toString(16).toUpperCase().padStart(2, '0')).join(' ')
        // 2. 尝试转换为 ASCII 字符串
        stringValue.value = bytes.map(b => b >= 32 && b <= 126 ? String.fromCharCode(b) : '.').join('')
        readValue.value = hexValue.value // 默认显示十六进制
      } else {
        readValue.value = data.value
        hexValue.value = ''
        stringValue.value = ''
      }
      
      emit('log', 'success', `[PLC] 读取成功 [${readAddress.value}]`)
    } else {
      const text = await res.text()
      emit('log', 'error', `[PLC] 读取失败: ${text}`)
    }
  } catch (err) {
    emit('log', 'error', '[PLC] 请求异常')
  } finally {
    isReading.value = false
  }
}

async function handleWrite() {
  if (!writeAddress.value || writeValue.value === '') return
  isWriting.value = true
  try {
    // 尝试根据输入转换类型 (简单处理)
    let val: any = writeValue.value
    if (!isNaN(Number(val)) && val.trim() !== '') {
        val = val.includes('.') ? parseFloat(val) : parseInt(val)
    } else if (val.toLowerCase() === 'true') {
        val = true
    } else if (val.toLowerCase() === 'false') {
        val = false
    }

    const res = await fetch(`${BACKEND_URL}/api/plc/write`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        address: writeAddress.value,
        value: val
      })
    })
    if (res.ok) {
      emit('log', 'success', `[PLC] 写入成功 [${writeAddress.value}] -> ${val}`)
    } else {
      const text = await res.text()
      emit('log', 'error', `[PLC] 写入失败: ${text}`)
    }
  } catch (err) {
    emit('log', 'error', '[PLC] 请求异常')
  } finally {
    isWriting.value = false
  }
}

onMounted(() => {
  // 初始可能自动配置一次
  if (props.ip) configurePlc()
})
</script>

<template>
  <div class="plc-panel">
    <div class="header">
      <span class="icon">💻</span> PLC 交互集成 (S7 Protocol)
    </div>

    <div class="status-bar">
      <div class="conn-info">
        <div class="ip-row">
          <span class="label">PLC 地址：</span>
          <span class="mono ip-text">{{ ip || '未配置' }}</span>
          <span class="badge">{{ cpuType }}</span>
        </div>
      </div>
      <div class="actions">
        <button class="btn config" @click="configurePlc" :disabled="isConfiguring">
          {{ isConfiguring ? '配置中...' : '重新初始化连接' }}
        </button>
      </div>
    </div>

    <div class="dashboard">
      <div class="control-grid">
        <!-- 读取区 -->
        <div class="control-card">
          <div class="card-title">📥 寄存器读取 (Read)</div>
          <div class="form-group">
            <label>起始地址</label>
            <input v-model="readAddress" type="text" class="plc-input mono" placeholder="例如 DB1.DBB0">
          </div>
          <div class="form-group">
            <label>读取长度 (Byte)</label>
            <input v-model.number="readCount" type="number" class="plc-input mono" placeholder="读取字节数">
          </div>
          <div class="result-display multi-row">
            <div class="res-item">
              <span class="res-label">Hex 序列：</span>
              <span class="res-val mono small">{{ hexValue || readValue }}</span>
            </div>
            <div class="res-item" v-if="stringValue">
              <span class="res-label">字符串值：</span>
              <span class="res-val mono text-blue">{{ stringValue }}</span>
            </div>
          </div>
          <button class="btn primary full" @click="handleRead" :disabled="isReading">
            {{ isReading ? '读取中...' : '立即读取' }}
          </button>
        </div>

        <!-- 写入区 -->
        <div class="control-card">
          <div class="card-title">📤 寄存器写入 (Write)</div>
          <div class="form-group">
            <label>起始地址</label>
            <input v-model="writeAddress" type="text" class="plc-input mono" placeholder="例如 DB1.DBX0.0">
          </div>
          <div class="form-group">
            <label>写入数值</label>
            <input v-model="writeValue" type="text" class="plc-input mono" placeholder="输入数值或 true/false">
          </div>
          <button class="btn warning full" @click="handleWrite" :disabled="isWriting">
            {{ isWriting ? '写入中...' : '确认写入' }}
          </button>
        </div>
      </div>

      <div class="info-note">
        <p>💡 **提示**：地址格式遵循 S7.Net 规范：</p>
        <ul>
          <li>DB块：`DB1.DBD0` (DWord), `DB1.DBW2` (Word), `DB1.DBX4.0` (Bit)</li>
          <li>输入输出：`I0.0`, `Q0.0`</li>
          <li>中间变量：`M0.0`, `MW2`</li>
        </ul>
      </div>
    </div>
  </div>
</template>

<style scoped>
.plc-panel {
  display: flex;
  flex-direction: column;
  height: 100%;
  background: #0d1117;
}

.header {
  padding: 12px 16px;
  background: linear-gradient(90deg, rgba(25, 118, 210, 0.3), transparent);
  border-bottom: 1px solid rgba(25, 118, 210, 0.2);
  font-weight: 600;
  color: #90caf9;
  display: flex;
  align-items: center;
  gap: 8px;
}

.status-bar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 12px 16px;
  background: rgba(255, 255, 255, 0.02);
  border-bottom: 1px solid rgba(255, 255, 255, 0.05);
}

.ip-text { color: #64b5f6; font-weight: bold; margin-right: 8px; }
.badge { background: rgba(144, 202, 249, 0.2); color: #90caf9; padding: 2px 6px; border-radius: 4px; font-size: 11px; }

.dashboard {
  flex: 1;
  padding: 20px;
  overflow-y: auto;
}

.control-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 20px;
  margin-bottom: 20px;
}

.control-card {
  background: rgba(255, 255, 255, 0.03);
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 8px;
  padding: 16px;
}

.card-title {
  font-size: 14px;
  font-weight: 600;
  color: #e3f2fd;
  margin-bottom: 16px;
  padding-bottom: 8px;
  border-bottom: 1px solid rgba(255, 255, 255, 0.05);
}

.form-group {
  margin-bottom: 12px;
}

.form-group label {
  display: block;
  font-size: 12px;
  color: #78909c;
  margin-bottom: 6px;
}

.plc-input {
  width: 100%;
  background: #05070a;
  border: 1px solid #1e293b;
  border-radius: 4px;
  padding: 8px 10px;
  color: #00e676;
  font-size: 14px;
  outline: none;
}
.plc-input:focus { border-color: #3b82f6; }

.result-display {
  background: rgba(0, 0, 0, 0.3);
  padding: 12px;
  border-radius: 4px;
  margin-bottom: 16px;
  display: flex;
  align-items: center;
  justify-content: space-between;
}
.result-display.multi-row {
  flex-direction: column;
  align-items: flex-start;
  gap: 8px;
}
.res-item {
  width: 100%;
  display: flex;
  justify-content: space-between;
  align-items: center;
  border-bottom: 1px solid rgba(255,255,255,0.05);
  padding-bottom: 4px;
}
.res-item:last-child { border: none; }

.res-label { font-size: 11px; color: #546e7a; }
.res-val { font-size: 18px; font-weight: bold; color: #ffeb3b; word-break: break-all; text-align: right; }
.res-val.small { font-size: 14px; color: #00e676; }
.text-blue { color: #64b5f6 !important; }

.btn {
  border: none;
  border-radius: 4px;
  padding: 8px 16px;
  font-size: 13px;
  font-weight: 600;
  cursor: pointer;
  transition: all 0.2s;
}
.btn.full { width: 100%; }
.btn.primary { background: #1976d2; color: white; }
.btn.warning { background: #ef6c00; color: white; }
.btn.config { background: rgba(144, 202, 249, 0.1); color: #90caf9; border: 1px solid rgba(144, 202, 249, 0.3); }
.btn:hover { opacity: 0.9; }
.btn:disabled { opacity: 0.5; cursor: not-allowed; }

.info-note {
  background: rgba(66, 165, 245, 0.05);
  border-left: 3px solid #1e88e5;
  padding: 12px;
  border-radius: 4px;
  font-size: 12px;
  color: #90a4ae;
}
.info-note p { margin: 0 0 8px 0; color: #cfd8dc; }
.info-note ul { margin: 0; padding-left: 20px; }
.info-note li { margin-bottom: 4px; }

.mono { font-family: 'Consolas', monospace; }
</style>
