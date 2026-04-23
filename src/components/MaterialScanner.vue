<script setup lang="ts">
import { ref, watch, computed, nextTick, onMounted } from 'vue'
import type { RouteStep, WorkStep } from '../types/mes'

const props = defineProps<{
  steps: RouteStep[]
  autoBarcodes?: string[] // PLC 自动采集的条码列表
  forceComplete?: boolean // 新增：由外部矩阵状态决定的强制完成标志
}>()

const emit = defineEmits<{
  (e: 'complete'): void
  (e: 'log', level: 'info'|'success'|'warn'|'error', msg: string): void
}>()

// 需要扫描的物料任务项
export interface MaterialTask {
  uid: string
  seqIdx: number
  material_No: string
  material_Name: string
  material_number: number
  noLength: number
  retrospect_Type: unknown
  scannedCount: number
  scannedBarcodes: string[]
  status: 'pending' | 'completed'
}

const taskList = ref<MaterialTask[]>([])
const scanInput = ref('')
const inputRef = ref<HTMLInputElement | null>(null)

// 提取所有物料，生成任务列表
function buildTasks() {
  const tasks: MaterialTask[] = []
  let uidCounter = 0
  
  props.steps.forEach((seq, si) => {
    const wsList = (seq.workStepList as WorkStep[]) || []
    wsList.forEach((ws) => {
      const matList = (ws.workStepMaterialList as any[]) || []
      matList.forEach((mat) => {
        // 如果数量为0或没有返回物料编号，跳过
        const reqNum = Number(mat.material_number) || 0
        if (!mat.material_No || reqNum <= 0) return

        tasks.push({
          uid: `mat-${uidCounter++}`,
          seqIdx: si + 1,
          material_No: mat.material_No,
          material_Name: mat.material_Name || '',
          material_number: reqNum,
          noLength: Number(mat.noLength) || 0,
          retrospect_Type: mat.retrospect_Type,
          scannedCount: 0,
          scannedBarcodes: [],
          status: 'pending'
        })
      })
    })
  })
  
  taskList.value = tasks
}

// 监听工步数据变化，重新构建任务
watch(
  () => props.steps,
  () => {
    buildTasks()
    if (taskList.value.length > 0) {
      nextTick(() => inputRef.value?.focus())
    }
  },
  { immediate: true, deep: true }
)

const isAllCompleted = computed(() => {
  // 优先级 1：如果外部传入矩阵校验已完成，则直接判定为完成
  if (props.forceComplete) return true
  
  // 优先级 2：传统的 BOM 清单匹配校验
  if (taskList.value.length === 0) return false
  return taskList.value.every(t => t.status === 'completed')
})

watch(isAllCompleted, (val) => {
  if (val) emit('complete')
})

// 核心逻辑：监听外部（PLC）自动条码变化
watch(
  () => props.autoBarcodes,
  (newBarcodes) => {
    if (newBarcodes && newBarcodes.length > 0) {
      // 每次 PLC 触发新条码时，重置旧的匹配记录（可选，根据业务决定，这里选择追加/更新）
      // addLog('info', `收到 ${newBarcodes.length} 个自动采集条码，开始规则校验...`)
      
      newBarcodes.forEach(code => {
        handleBatchVerify(code)
      })
    }
  },
  { immediate: true, deep: true }
)

function handleBatchVerify(code: string) {
  if (!code) return

  // 规则：只要条码的前 N 位能匹配上物料列表中的任意一项即可
  const target = taskList.value.find(t => {
    const prefixMatch = code.startsWith(t.material_No)
    const lengthMatch = t.noLength > 0 ? code.length === Number(t.noLength) : true
    return prefixMatch && lengthMatch
  })

  if (target) {
    if (!target.scannedBarcodes.includes(code)) {
      target.scannedBarcodes.push(code)
      target.scannedCount = target.scannedBarcodes.length
      
      // 只要匹配上了，标记该物料项为通过
      target.status = 'completed'
      emit('log', 'success', `[自动校验] 条码 ${code} 匹配成功 -> ${target.material_Name}`)
    }
    
    // 检查是否全局完成
    if (isAllCompleted.value) {
      emit('complete')
    }
  } else {
    // 即使没匹配到，也要检查是否已经全部完成（可能之前已经完成了）
    if (isAllCompleted.value) {
      emit('complete')
    }
    emit('log', 'warn', `[自动校验] 条码 ${code} 未能匹配规则 (前缀或长度不符)`)
  }
}
</script>

<template>
  <div class="material-scanner-panel">
    <!-- 移除手动扫码控件，改为自动状态显示 -->
    <div class="auto-verify-status" v-if="taskList.length">
      <div class="status-indicator">
        <span class="label">校验状态:</span>
        <span v-if="isAllCompleted" class="all-ok">✅ 校验完成</span>
        <span v-else-if="autoBarcodes && autoBarcodes.length > 0" class="processing">⚙️ 正在校验中...</span>
        <span v-else class="pending">⏳ 等待 PLC 条码矩阵数据...</span>
      </div>
      <div class="rule-hint">规则：前缀匹配 + 长度校验</div>
    </div>

    <!-- 空状态 -->
    <div v-if="!taskList.length" class="empty-state">
      当前工步无物料绑定信息，无需扫描验证。
    </div>

    <!-- 任务清单表格 -->
    <div v-else class="table-scroll">
      <table>
        <thead>
          <tr>
            <th style="width: 50px">序号</th>
            <th width="150">物料编号</th>
            <th width="300">物料名称</th>
            <th class="left">条码长度</th>
          </tr>
        </thead>
        <tbody>
          <tr 
            v-for="(task, idx) in taskList" 
            :key="task.uid"
            class="data-row"
            :class="{ 'done-row': task.status === 'completed' }"
          >
            <td>
              <span class="seq-badge" :class="{ 'done-badge': task.status === 'completed' }">
                {{ task.status === 'completed' ? '✓' : idx + 1 }}
              </span>
            </td>
            <td class="mono c-blue">{{ task.material_No }}</td>
            <td class="mat-name">
              {{ task.material_Name }}
              <span v-if="task.status === 'completed'" class="done-tag">已通过</span>
            </td>
            <td class="left mono">{{ task.noLength > 0 ? task.noLength : '—' }}</td>
          </tr>
        </tbody>
      </table>
    </div>
  </div>
</template>

<style scoped>
.material-scanner-panel {
  display: flex;
  flex-direction: column;
  height: 100%;
}

.auto-verify-status {
  padding: 15px 20px;
  background: rgba(13, 71, 161, 0.2);
  border-bottom: 1px solid rgba(100, 181, 246, 0.2);
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.status-indicator {
  display: flex;
  align-items: center;
  gap: 10px;
  font-weight: 700;
}

.status-indicator .label { color: #90a4ae; font-size: 13px; }
.status-indicator .all-ok { color: #00e676; font-size: 16px; }
.status-indicator .processing { color: #42a5f5; animation: blink 1s infinite; }
.status-indicator .pending { color: #ffab40; }

@keyframes blink { 0%, 100% { opacity: 1; } 50% { opacity: 0.6; } }

.rule-hint {
  font-size: 11px;
  color: #546e7a;
  background: rgba(255, 255, 255, 0.05);
  padding: 2px 8px;
  border-radius: 4px;
}

.progress-status {
  font-size: 13px;
  font-weight: 600;
}

.status-all-done { color: #00e676; animation: pulse 2s infinite; }
.status-pending { color: #ffab40; }

.empty-state {
  flex: 1;
  display: flex;
  align-items: center;
  justify-content: center;
  color: #546e7a;
  font-size: 13px;
}

.table-scroll {
  flex: 1;
  overflow: auto;
}
.table-scroll::-webkit-scrollbar { width: 4px; height: 4px; }
.table-scroll::-webkit-scrollbar-thumb { background: rgba(100, 181, 246, 0.2); }

table {
  width: 100%;
  border-collapse: collapse;
  font-size: 12px;
}
thead tr {
  background: rgba(21, 101, 192, 0.2);
  position: sticky;
  top: 0;
  z-index: 2;
}
th {
  padding: 8px 12px;
  text-align: left;
  color: #78909c;
  font-weight: 600;
  border-bottom: 1px solid rgba(100, 181, 246, 0.1);
  white-space: nowrap;
}

.data-row {
  border-bottom: 1px solid rgba(100, 181, 246, 0.05);
  transition: background 0.15s;
}
.data-row:hover { background: rgba(66, 165, 245, 0.04); }

.done-row { background: rgba(0, 230, 118, 0.03); }

td {
  padding: 8px 12px;
  color: #cfd8dc;
  vertical-align: middle;
}
.center { text-align: center; }
.mono { font-family: 'Consolas', monospace; }
.small { font-size: 11px; }
.c-blue { color: #64b5f6; }
.mat-name { font-weight: 500; color: #e0e6ed; }
.req-num { font-weight: 600; color: #90caf9; }

.done-tag {
  font-size: 10px;
  background: rgba(0, 230, 118, 0.1);
  color: #00e676;
  padding: 1px 6px;
  border-radius: 4px;
  margin-left: 8px;
  border: 1px solid rgba(0, 230, 118, 0.2);
}

.seq-badge {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 20px;
  height: 20px;
  background: rgba(100, 181, 246, 0.15);
  border-radius: 4px;
  font-size: 10px;
  color: #90caf9;
}
.done-badge { background: #00e676; color: #000; }

.scan-count {
  font-size: 13px;
  font-weight: 700;
  color: #78909c;
}
.scan-count.partial { color: #ffab40; }
.scan-count.full { color: #00e676; }

.status-tag {
  padding: 2px 8px;
  border-radius: 10px;
  font-size: 10px;
  font-weight: 600;
}
.status-tag.pending { background: rgba(255, 171, 64, 0.15); color: #ffab40; }
.status-tag.success { background: rgba(0, 230, 118, 0.15); color: #00e676; }

.code-item {
  color: #80cbc4;
  background: rgba(128, 203, 196, 0.1);
  padding: 1px 6px;
  border-radius: 4px;
  margin-bottom: 2px;
  display: inline-block;
}
.code-item:last-child { margin-bottom: 0; }

@keyframes pulse {
  0%, 100% { opacity: 1; }
  50% { opacity: 0.6; }
}
</style>
