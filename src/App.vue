<script setup lang="ts">
import { ref, reactive, computed, onMounted, nextTick, onUnmounted } from 'vue'
import { HubConnectionBuilder, LogLevel, type HubConnection } from '@microsoft/signalr'
import type { AppConfig, OrderInfo, RouteStep, TestResult, WorkStep } from './types/mes'
import { getOrderByProcess, getRouteList, checkSingleMaterial, checkDuplicateBarcode } from './services/mesApi'
import ConfigModal from './components/ConfigModal.vue'
import RouteTable from './components/RouteTable.vue'
import ApiDetail from './components/ApiDetail.vue'
import type { ApiRecord } from './components/ApiDetail.vue'
import MaterialScanner from './components/MaterialScanner.vue'
import RecipeSettings from './components/RecipeSettings.vue'
import PlcInteraction from './components/PlcInteraction.vue'
import OrderSelectModal from './components/OrderSelectModal.vue'
import PlcMonitor from './components/PlcMonitor.vue'
import { MOCK_ORDER_INFO, MOCK_ROUTE_DATA } from './utils/mockData'
import { BACKEND_URL, SIGNALR_HUB_URL, DEFAULT_APP_CONFIG } from './utils/constants'

const CONFIG_KEY = 'mes_app_config_v2'
const PRODUCT_KEY = 'mes_last_product_code'
const DEFAULT_CONFIG: AppConfig = DEFAULT_APP_CONFIG

const config = ref<AppConfig>({ ...DEFAULT_CONFIG })
const showConfig = ref(false)
const plcOnline = ref(false)
const plcBitValue = ref(false); const lastHeartbeatTime = ref('-');
let hubConnection: HubConnection | null = null

const plcMonitorRef = ref<any>(null)
async function fetchConfig() {
  try {
    const res = await fetch(`${BACKEND_URL}/api/config`)
    if (res.ok) {
      const data = await res.json()
      config.value = { ...config.value, ...data }
      addLog('success', '系统配置已从服务器加载');
    }
  } catch (err) {
    addLog('error', '无法从服务器获取配置')
  }
}

async function initSignalR() {
  hubConnection = new HubConnectionBuilder()
    .withUrl(SIGNALR_HUB_URL) 
    .withAutomaticReconnect()
    .configureLogging(LogLevel.Warning)
    .build()

  hubConnection.on('ReceiveHeartbeat', (data: any) => {
    // console.log('[SignalR] PLC Heartbeat:', data)
    plcOnline.value = data.online
    plcBitValue.value = !!data.bitValue // 更新位状态
    if (data.time) lastHeartbeatTime.value = data.time
    
    // 如果心跳包里带了备份日志，且当前日志列表第一条不是它，则塞进
    if (data.log) {
      const newLog = { type: data.online ? 'info' : 'error', message: data.log, time: data.time }
      if (logs.value.length === 0 || logs.value[0].message !== newLog.message) {
        logs.value.unshift(newLog)
        if (logs.value.length > 100) logs.value.pop()
      }
    }
  })

  hubConnection.on('ReceiveLog', (entry: any) => {
    // 过滤掉频繁的心跳日志，只记录业务日志
    if (entry && entry.message && !entry.message.includes('Heartbeat')) {
      addLog(entry.type, entry.message)
    }
  })

  hubConnection.on('ReceivePlcMonitor', (data: any) => {
    // console.log('[SignalR] PLC Monitor Raw Data:', data)
    plcLogs.value.push(data)
    if (plcLogs.value.length > 200) plcLogs.value.shift()
  })

  // 新增：监听后端发送的自动触发信号
  hubConnection.on('ReceivePlcTrigger', async (data: any) => {
    addLog('info', `[PLC自动触发] 收到 ${data.side} 堆叠完成信号，正在采集电芯条码...`);
    
    // 1. 批量读取条码逻辑
    try {
      const res = await fetch(`${BACKEND_URL}/api/plc/read-barcodes`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          layers: data.layers,
          barcodeLength: config.value.cellBarcodeLength || 24,
          col1Addr: config.value.col1StartAddr,
          col2Addr: config.value.col2StartAddr,
          col3Addr: config.value.col3StartAddr
        })
      })
      if (res.ok) {
        const result = await res.json()
        currentBarcodes.value = result.barcodes
        addLog('success', '✅ 采集矩阵已更新，共读取 ' + result.barcodes.length + ' 个条码');

        // 2. 自动代替人工扫码：使用第一个条码作为产品 SN 触发业务流
        if (result.barcodes.length > 0) {
          productCode.value = result.barcodes[0]
          addLog('info', `[自动触发] 正在使用首个条码 [${productCode.value}] 拉取工单...`)
          handleScan()
        }
      }
    } catch (err) {
      addLog('error', '❌ 自动采集条码或触发业务流失败');
    }
  })

  try {
    await hubConnection.start()
    addLog('success', '实时通讯服务 (SignalR) 已连接');
  } catch (err) {
    addLog('error', '无法连接到实时通讯服务')
  }
}

async function onConfigSaved(newConfig: AppConfig) {
  try {
    // 立即更新本地状态
    config.value = newConfig
    
    // 1. 保存配置到服务器文件
    await fetch(`${BACKEND_URL}/api/config`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(newConfig)
    })

    // 2. 通知 PLC 服务更新实时连接参数 (包括心跳地址)
    await fetch(`${BACKEND_URL}/api/plc/config`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        ip: newConfig.plcIp,
        cpuType: newConfig.plcCpu,
        rack: newConfig.plcRack,
        slot: newConfig.plcSlot,
        heartbeatAddress: newConfig.plcHeartbeatAddress,
        aStackAddr: newConfig.plcAStackFinishAddress,
        bStackAddr: newConfig.plcBStackFinishAddress,
        cellLayerAddr: newConfig.plcCellLayerAddress,
        col1StartAddr: newConfig.col1StartAddr,
        col2StartAddr: newConfig.col2StartAddr,
        col3StartAddr: newConfig.col3StartAddr
      })
    })

    addLog('success', '配置已保存并实时生效')
  } catch (err) {
    addLog('error', '配置同步失败')
  }
}

const productCode = ref(localStorage.getItem(PRODUCT_KEY) || '')
const scanInputRef = ref<HTMLInputElement | null>(null)
const focusScan = () => nextTick(() => scanInputRef.value?.focus())

onMounted(async () => {
  focusScan()
  await fetchConfig() 
  // 初始化时同步一个 PLC 配置给后端
  if (config.value.plcIp) {
    await onConfigSaved(config.value)
  }
  await initSignalR()
})

onUnmounted(() => {
  if (hubConnection) hubConnection.stop()
})

const orderInfo = ref<OrderInfo | null>(null)
const orderLoading = ref(false)
const orderError = ref('')
const routeSteps = ref<RouteStep[]>([])
const routeLoading = ref(false)
const routeError = ref('')

const pendingOrders = ref<OrderInfo[]>([])
const showOrderSelect = ref(false)

const testResult = ref<TestResult>('IDLE')
const resultMessage = ref('')
const logs = ref<any[]>([])
const plcLogs = ref<any[]>([]) // 专门存储 PLC 监控原始数据
const apiRecords = ref<ApiRecord[]>([])
const currentBarcodes = ref<string[]>([]) // 新增：存储读取到的电芯条码
const activeTab = ref<'route' | 'api' | 'log' | 'material' | 'info' | 'plc' | 'recipe' | 'monitor'>('route')
const barcodeValidationResults = reactive<Record<string, { single: string, duplicate: string }>>({})

function addLog(level: any, msg: string) {
  logs.value.unshift({ time: new Date().toLocaleTimeString(), level, msg })
  if (logs.value.length > 50) logs.value.pop()
}

function resetAll() {
  orderInfo.value = null; orderError.value = ''; routeSteps.value = [];
  routeError.value = ''; testResult.value = 'IDLE'; resultMessage.value = '';
  apiRecords.value = [];
}

async function handleScan() {
  const code = productCode.value.trim()
  if (!config.value.technicsProcessCode) {
    addLog('error', '未设置工艺路线编号');
    return
  }
  
  // 1. 在重置前先记住当前的 SN
  const memorySN = (code || (orderInfo.value?.code || orderInfo.value?.orderCode) || '').trim().toUpperCase()
  
  localStorage.setItem(PRODUCT_KEY, code)
  resetAll()
  addLog('info', `[PLC触发] 开始拉取待生产任务...`)
  
  orderLoading.value = true
  const t0 = Date.now()
  const rec: ApiRecord = { 
    title: '获取任务', 
    url: config.value.orderApiUrl, 
    status: 'pending', 
    time: new Date().toLocaleTimeString(), 
    reqBody: { 
      produce_Type: config.value.produce_Type, 
      tenantID: config.value.tenantID 
    } 
  }
  apiRecords.value.unshift(rec)
  activeTab.value = 'api'
  
  try {
    const res = await getOrderByProcess(config.value, code)
    const duration = Date.now() - t0
    
    apiRecords.value[0] = { 
      ...apiRecords.value[0], 
      resBody: { ...res }, 
      status: 'success',
      duration 
    }
    
    const allOrders = res.datas || []
    const validOrders = allOrders.filter(o => {
      // 状态必须为 2
      const statusOk = String(o.order_Status) === '2'
      // 类型必须为 0 (普通) 或 1 (主工单)
      const type = o.order_Type ?? o.orderType
      const typeOk = type === 0 || type === 1 || type === '0' || type === '1'
      return statusOk && typeOk
    })
    
    // 使用重置前保存的 memorySN 进行比对
    const targetSN = memorySN
    
    // 情况 1: 如果存在可识别的 SN (来自输入或记录)
    if (targetSN) {
      const match = allOrders.find(o => {
        const fieldCode = String(o.code || '').trim().toUpperCase()
        const fieldOrderCode = String(o.orderCode || '').trim().toUpperCase()
        return fieldCode === targetSN || fieldOrderCode === targetSN
      })
      
      // 判定：如果找到了匹配项且状态为 2，直接静默通过
      if (match && String(match.order_Status) === '2') {
        orderInfo.value = match
        addLog('success', `[二次复核] SN [${targetSN}] 状态确认正常，自动继续`)
        await fetchRouteList(match.route_No)
        return
      }
      
      // 如果找到了但状态变了，或者没找到，则需要弹出
      const failReason = match ? `状态变为[${match.order_Status}]` : '任务已不存在'
      addLog('warn', `[核对中断] SN [${targetSN}] ${failReason}，请重新确认`)
    } 
    // 情况 2: 彻底没有条码记录 (初次进入且没扫码)
    else {
      // 即使只有 1 个工单号
    }

    // 情况 3: 兜底逻辑 - 弹出列表供人工确认
    if (validOrders.length > 0) {
      pendingOrders.value = validOrders
      addLog('info', `[待确认] 共有 ${validOrders.length} 个有效任务，请手动选择`)
      await nextTick()
      setTimeout(() => { showOrderSelect.value = true }, 200)
    } else {
      addLog('error', '未找到任何状态为2的待生产任务')
    }
  } catch (err: any) {
    rec.status = 'error'
    rec.resBody = { error: '请求失败', details: err.message }
    addLog('error', `获取工单失败: ${err.message}`)
  } finally { 
    orderLoading.value = false 
  }
}

async function onOrderSelected(order: OrderInfo) {
  showOrderSelect.value = false
  orderInfo.value = order
  addLog('success', `已选择工单号: ${order.orderCode}`)
  await fetchRouteList(order.route_No)
}

function simulatePlcTrigger() {
  addLog('info', '手动收到 PLC 触发信号 (模拟)')
  handleScan()
}

// 计算总层数
const matrixLayers = computed(() => {
  if (currentBarcodes.value.length === 0) return 0
  return Math.ceil(currentBarcodes.value.length / 3)
})

// 获取指定层、指定列的条码
function getMatrixBarcode(layerIdx: number, colIdx: number): string {
  // 假设存储结构是：前 L 个是第 1 列，中间 L 个是第 2 列...
  const layers = matrixLayers.value
  const targetIdx = layerIdx + (colIdx * layers)
  return currentBarcodes.value[targetIdx] || ''
}

// 校验某一层的所有条码是否都符合规则
function isLayerValid(layerIdx: number): boolean {
  for (let col = 0; col < 3; col++) {
    const code = getMatrixBarcode(layerIdx, col)
    if (code && !isBarcodeValid(code)) return false
  }
  return true
}

// 校验函数：判断单个条码是否符合物料规则（前缀+长度）
function isBarcodeValid(code: string): boolean {
  if (!code || !routeSteps.value.length) return false
  
  const rules = routeSteps.value.flatMap(seq => 
    (seq.workStepList || []).flatMap(ws => 
      ((ws as any).workStepMaterialList || [])
    )
  )

  return rules.some(rule => {
    const prefixMatch = code.startsWith(rule.material_No)
    const lengthMatch = rule.noLength > 0 ? code.length === Number(rule.noLength) : true
    return prefixMatch && lengthMatch
  })
}

/** 异步执行单物料和重码校验 */
async function runBarcodeValidations(barcode: string) {
  if (!barcode || !orderInfo.value) return
  
  // 初始化状态为“进行中”
  barcodeValidationResults[barcode] = { single: 'loading', duplicate: 'loading' }

  const barcodeTail = barcode.slice(-8)

  // 1. 单物料校验 - 构造记录与调用
  if (config.value.singleMaterialApiUrl) {
    const t0 = Date.now()
    const rec = reactive<ApiRecord>({
      title: `单物料校验 [${barcodeTail}]`,
      url: config.value.singleMaterialApiUrl,
      status: 'pending',
      time: new Date().toLocaleTimeString(),
      reqBody: {
        produceOrderCode: orderInfo.value.code || orderInfo.value.orderCode,
        routeNo: orderInfo.value.route_No,
        technicsProcessCode: config.value.technicsProcessCode,
        materialCode: barcode,
        tenantID: orderInfo.value.tenantID || config.value.tenantID
      }
    })
    apiRecords.value.unshift(rec)

    checkSingleMaterial(config.value.singleMaterialApiUrl, rec.reqBody)
      .then(res => {
        const msg = JSON.stringify(res)
        barcodeValidationResults[barcode].single = msg.includes('校验成功') ? 'success' : 'error'
        rec.status = 'success'
        rec.resBody = res
        rec.duration = Date.now() - t0
      })
      .catch(err => { 
        barcodeValidationResults[barcode].single = 'error'
        rec.status = 'error'
        rec.resBody = { error: err.message }
      })
  }

  // 2. 重码校验 - 构造记录与调用
  if (config.value.duplicateCheckApiUrl) {
    const t0 = Date.now()
    const rec = reactive<ApiRecord>({
      title: `重码校验 [${barcodeTail}]`,
      url: config.value.duplicateCheckApiUrl,
      status: 'pending',
      time: new Date().toLocaleTimeString(),
      reqBody: {
        processCode: config.value.technicsProcessCode,
        code: barcode,
        type: '1'
      }
    })
    apiRecords.value.unshift(rec)

    checkDuplicateBarcode(config.value.duplicateCheckApiUrl, rec.reqBody)
      .then(res => {
        const msg = JSON.stringify(res)
        barcodeValidationResults[barcode].duplicate = msg.includes('未检测到重码信息') ? 'success' : 'error'
        rec.status = 'success'
        rec.resBody = res
        rec.duration = Date.now() - t0
      })
      .catch(err => { 
        barcodeValidationResults[barcode].duplicate = 'error'
        rec.status = 'error'
        rec.resBody = { error: err.message }
      })
  }
}

/** 判定某一层是否全部通过单物料校验 */
function isLayerSingleValid(layerIdx: number): string {
  let hasLoading = false
  for (let col = 0; col < 3; col++) {
    const code = getMatrixBarcode(layerIdx, col)
    if (!code) continue
    const status = barcodeValidationResults[code]?.single
    if (status === 'error') return 'error'
    if (status === 'loading') hasLoading = true
  }
  return hasLoading ? 'loading' : 'success'
}

/** 判定某一层是否全部通过重码校验 */
function isLayerDuplicateValid(layerIdx: number): string {
  let hasLoading = false
  for (let col = 0; col < 3; col++) {
    const code = getMatrixBarcode(layerIdx, col)
    if (!code) continue
    const status = barcodeValidationResults[code]?.duplicate
    if (status === 'error') return 'error'
    if (status === 'loading') hasLoading = true
  }
  return hasLoading ? 'loading' : 'success'
}

/** 获取处理后的最终条码 (根据清洗规则) */
function getFinalBarcode(layerIdx: number, colIdx: number): string {
  const originalCode = getMatrixBarcode(layerIdx, colIdx)
  
  // 规则 2: 若该位置不存在电芯码，补充为 24 位 9
  if (!originalCode) {
    return '999999999999999999999999'
  }

  // 获取三项校验状态
  const ruleValid = isBarcodeValid(originalCode)
  const singleStatus = barcodeValidationResults[originalCode]?.single
  const duplicateStatus = barcodeValidationResults[originalCode]?.duplicate

  // 规则 1: 若任意一项校验失败，替换为 24 位 0
  if (ruleValid === false || singleStatus === 'error' || duplicateStatus === 'error') {
    return '000000000000000000000000'
  }

  // 规则 3: 全部通过则保留原码
  if (ruleValid === true && singleStatus === 'success' && duplicateStatus === 'success') {
    return originalCode
  }

  // 校验中状态暂回显原码
  return originalCode
}

/** 判定整个矩阵是否已经完全采集并校验完成 */
const isMatrixFullyValidated = computed(() => {
  if (matrixLayers.value === 0) return false
  for (let col = 0; col < 3; col++) {
    for (let row = 0; row < matrixLayers.value; row++) {
      const code = getMatrixBarcode(row, col)
      if (!code) return false // 任何一个位置为空则未完成
      
      const res = barcodeValidationResults[code]
      // 只有规则校验通过，且单物料和重码校验不是 loading 状态，才算该条码处理完成
      const ruleValid = isBarcodeValid(code)
      if (!ruleValid) continue // 如果规则校验失败，该条码已处于确定状态（全0），继续检查下一个
      
      if (!res || res.single === 'loading' || res.duplicate === 'loading') {
        return false
      }
    }
  }
  return true
})

async function fetchRouteList(routeCode: string) {
  routeLoading.value = true
  const t0 = Date.now()
  const rec: ApiRecord = { 
    title: '获取工步', 
    url: config.value.routeApiUrl, 
    status: 'pending', 
    time: new Date().toLocaleTimeString(), 
    reqBody: { 
      routeCode: routeCode,
      workSeqNo: config.value.technicsProcessCode // 修正显示：记录实际发送的工序代码
    } 
  }
  apiRecords.value.unshift(rec)
  try {
    if (routeCode === 'ROUTE_BASE_001') {
       await new Promise(r => setTimeout(r, 500))
       rec.duration = Date.now() - t0
       rec.resBody = MOCK_ROUTE_DATA
       const steps = MOCK_ROUTE_DATA.data.workSeqList
       routeSteps.value = steps
       rec.status = 'success'; addLog('success', '[仿真] 模拟工艺路线获取成功')
       activeTab.value = 'material'
       return
    }

    const res = await getRouteList(config.value, routeCode)
    const duration = Date.now() - t0
    const steps = (res.data as any)?.workSeqList || (Array.isArray(res.data) ? res.data : [])
    
    // 强制替换对象以触发 Vue 响应式更新
    apiRecords.value[0] = { 
      ...apiRecords.value[0], 
      resBody: res, 
      status: 'success',
      duration 
    }
    
    routeSteps.value = steps
    addLog('success', `获取到 ${steps.length} 条工步，开始校验条码矩阵...`)
    
    // 关键：工单和工步加载完成后，触发所有当前条码的校验
    if (currentBarcodes.value.length > 0) {
      currentBarcodes.value.forEach(code => {
        if (code) runBarcodeValidations(code)
      })
    }
    
    activeTab.value = 'material'
  } catch (err: any) {
    apiRecords.value[0] = { ...apiRecords.value[0], status: 'error' }
    addLog('error', err.message)
  } finally { routeLoading.value = false }
}

function setOK() {
  testResult.value = 'OK'
  resultMessage.value = '测试综合判定通过'
  addLog('success', '人工判定 OK')
  activeTab.value = 'info' // 验证完成后自动跳转到“获取信息”标签页
}

function setNG() {
  testResult.value = 'NG'
  resultMessage.value = '测试综合判定不通过'
  addLog('error', '人工判定 NG')
}

function resetResult() {
  testResult.value = 'IDLE'
  resultMessage.value = ''
  addLog('info', '状态已复位，等待下一次触发');
}
</script>

<template>
  <div class="app-root">
    <!-- 顶部标题栏 -->
    <header class="app-header">
      <div class="header-left">
        <div class="brand-icon">MES</div>
        <div class="brand-text">
          <span class="brand-title">工序扫码系统</span>
          <span class="brand-sub">MES Process Scanner v1.0</span>
        </div>
      </div>
      <div class="header-center">
        <div class="heartbeat-status" :class="{ online: plcOnline, active: plcBitValue }">
          <div class="heartbeat-led"></div>
          <span class="heartbeat-label">PLC 心跳: {{ plcOnline ? (plcBitValue ? '脉冲(1)' : '在线(0)') : '离线' }}</span>
          <span class="heartbeat-time" v-if="plcOnline">{{ lastHeartbeatTime }}</span>
        </div>
        <span class="process-badge">
          <span class="label">当前工序：</span>
          <span class="value">{{ config.technicsProcessCode || '未设置' }}</span>
        </span>
      </div>
      <div class="header-right">
        <button class="icon-btn" title="系统配置" @click="showConfig = true">
          ⚙️ 配置
        </button>
      </div>
    </header>

    <!-- 主体内容 -->
    <main class="app-main">
      <section class="left-panel">
        <div class="card scan-card">
          <div class="card-title"><span class="step-badge">1</span> 扫描产品条码</div>
          <div class="scan-input-wrap" :class="{ 'scanning': orderLoading }">
            <span class="scan-icon">📷</span>
            <input
              ref="scanInputRef"
              v-model="productCode"
              type="text"
              placeholder="请扫描或输入产品条码..."
              class="scan-input"
              :disabled="orderLoading || routeLoading"
              @keydown.enter="handleScan"
            />
            <button
              class="scan-btn"
              :disabled="orderLoading || !productCode.trim()"
              @click="handleScan"
            >
              {{ orderLoading ? '查询中...' : '查询' }}
            </button>
            <button
              class="plc-mock-btn"
              title="模拟 PLC 触发信号"
              @click="simulatePlcTrigger"
            >
              🤖 PLC
            </button>
          </div>
          <p class="scan-hint">扫描后请按 <kbd>Enter</kbd> 提交，或点击 <b>🤖 PLC</b> 模拟外部触发</p>
        </div>

        <div class="card info-card">
          <div class="card-title">
            <span class="step-badge">2</span> 工单信息
            <div v-if="orderLoading" class="loading-spin" />
          </div>
          <div v-if="orderError" class="error-box"><span>⚠️</span> {{ orderError }}</div>
          <div v-else-if="orderInfo" class="info-grid">
            <div class="info-item">
              <span class="info-label">工单号</span>
              <span class="info-value highlight">{{ orderInfo.orderCode }}</span>
            </div>
            <div class="info-item">
              <span class="info-label">工艺路线编码</span>
              <span class="info-value mono">{{ orderInfo.route_No }}</span>
            </div>
            <div class="info-item">
              <span class="info-label">工单编码</span>
              <span class="info-value mono highlight-sn">{{ orderInfo?.code || productCode || '等待获取...' }}</span>
            </div>
            <template v-for="(val, key) in orderInfo" :key="key">
              <div v-if="key !== 'orderCode' && key !== 'route_No'" class="info-item">
                <span class="info-label">{{ key }}</span>
                <span class="info-value">{{ val }}</span>
              </div>
            </template>
          </div>
          <div v-else class="empty-hint">等待扫码查询...</div>
        </div>

        <div class="card result-card">
          <div class="card-title"><span class="step-badge">3</span> 测试结果</div>
          <div class="result-display" :class="testResult.toLowerCase()">
            <span class="result-icon">{{ testResult === 'OK' ? '✅' : testResult === 'NG' ? '❌' : '⏳' }}</span>
            <span class="result-text">{{ testResult === 'IDLE' ? '待检测' : testResult }}</span>
            <span v-if="resultMessage" class="result-msg">{{ resultMessage }}</span>
          </div>
          <div class="result-actions">
            <button class="btn-ok" :disabled="!orderInfo || testResult !== 'IDLE'" @click="setOK">✅ OK (合格)</button>
            <button class="btn-ng" :disabled="!orderInfo || testResult !== 'IDLE'" @click="setNG">❌ NG (不合格)</button>
          </div>
          <button v-if="testResult !== 'IDLE'" class="btn-reset" @click="resetResult">🔄 复位 / 下一件</button>
        </div>
      </section>

      <section class="right-panel">
        <div class="tab-bar">
          <button class="tab-btn" :class="{ active: activeTab === 'route' }" @click="activeTab = 'route'">
            <span>📋</span> 工步列表
            <span v-if="routeSteps.length" class="tab-count">{{ routeSteps.length }}</span>
          </button>
          <button class="tab-btn" :class="{ active: activeTab === 'material' }" @click="activeTab = 'material'">
            <span>📦</span> 物料验证
          </button>
          <button class="tab-btn" :class="{ active: activeTab === 'info' }" @click="activeTab = 'info'">
            <span>ℹ️</span> 获取信息
          </button>
          <button class="tab-btn" :class="{ active: activeTab === 'plc' }" @click="activeTab = 'plc'">
            <span>💻</span> PLC交互
          </button>
          <button class="tab-btn" :class="{ active: activeTab === 'api' }" @click="activeTab = 'api'">
            <span>🔌</span> 接口交互
            <span v-if="apiRecords.length" class="tab-count">{{ apiRecords.length }}</span>
          </button>
          <button class="tab-btn" :class="{ active: activeTab === 'recipe' }" @click="activeTab = 'recipe'">
            <span>🧪</span> 配方设置
          </button>
          <button class="tab-btn" :class="{ active: activeTab === 'monitor' }" @click="activeTab = 'monitor'">
            <span>📟</span> PLC 监控
          </button>
          <button class="tab-btn" :class="{ active: activeTab === 'log' }" @click="activeTab = 'log'">
            <span>📄</span> 操作日志
            <span v-if="logs.length" class="tab-count">{{ logs.length }}</span>
          </button>
        </div>

        <div class="tab-content">
          <div v-show="activeTab === 'route'" class="tab-pane">
            <div v-if="routeError" class="error-box"><span>⚠️</span> {{ routeError }}</div>
            <RouteTable :steps="routeSteps" :loading="routeLoading" />
          </div>
          <div v-show="activeTab === 'material'" class="tab-pane flex-column">
            <MaterialScanner 
              :steps="routeSteps" 
              :auto-barcodes="currentBarcodes"
              :force-complete="isMatrixFullyValidated"
              @log="addLog" 
              @complete="setOK" 
            />
            
            <!-- 底部条码矩阵显示 (仅在物料验证页显示) -->
            <div class="barcode-display-card flex-grow">
              <div class="card-title">
                电芯采集矩阵
              </div>
              <div class="matrix-table-container">
                <table class="matrix-table">
                  <thead>
                    <tr>
                      <th width="80">位置序号</th>
                      <th>采集电芯条码</th>
                      <th width="120">规则校验</th>
                      <th width="120">重码校验</th>
                      <th width="120">单物料校验</th>
                      <th>处理后结果 (24位)</th>
                    </tr>
                  </thead>
                  <tbody>
                    <template v-for="cIdx in [0, 1, 2]" :key="`col-${cIdx}`">
                      <tr v-for="rIdx in Array.from({length: matrixLayers}, (_, i) => i)" :key="`cell-${cIdx}-${rIdx}`">
                        <td class="text-center font-bold" style="background: rgba(255,255,255,0.02)">{{ cIdx + 1 }}.{{ rIdx + 1 }}</td>
                        <td :class="{ 'has-code': getMatrixBarcode(rIdx, cIdx) }">
                          <div class="cell-content">
                            <span class="barcode-text">{{ getMatrixBarcode(rIdx, cIdx) || '未采集' }}</span>
                            <span v-if="getMatrixBarcode(rIdx, cIdx) && isBarcodeValid(getMatrixBarcode(rIdx, cIdx))" class="mini-ok">✓</span>
                          </div>
                        </td>
                        <td class="text-center">
                          <template v-if="getMatrixBarcode(rIdx, cIdx)">
                            <span v-if="isBarcodeValid(getMatrixBarcode(rIdx, cIdx))" class="badge success">通过</span>
                            <span v-else class="badge error">失败</span>
                          </template>
                          <span v-else>-</span>
                        </td>
                        <td class="text-center">
                          <template v-if="getMatrixBarcode(rIdx, cIdx)">
                            <span v-if="barcodeValidationResults[getMatrixBarcode(rIdx, cIdx)]?.duplicate === 'loading'" class="badge info">校验中...</span>
                            <span v-else-if="barcodeValidationResults[getMatrixBarcode(rIdx, cIdx)]?.duplicate === 'success'" class="badge success">通过</span>
                            <span v-else-if="barcodeValidationResults[getMatrixBarcode(rIdx, cIdx)]?.duplicate === 'error'" class="badge error">失败</span>
                            <span v-else class="badge info">等待</span>
                          </template>
                          <span v-else>-</span>
                        </td>
                        <td class="text-center">
                          <template v-if="getMatrixBarcode(rIdx, cIdx)">
                            <span v-if="barcodeValidationResults[getMatrixBarcode(rIdx, cIdx)]?.single === 'loading'" class="badge info">校验中...</span>
                            <span v-else-if="barcodeValidationResults[getMatrixBarcode(rIdx, cIdx)]?.single === 'success'" class="badge success">校验成功</span>
                            <span v-else-if="barcodeValidationResults[getMatrixBarcode(rIdx, cIdx)]?.single === 'error'" class="badge error">校验失败</span>
                            <span v-else class="badge info">等待</span>
                          </template>
                          <span v-else>-</span>
                        </td>
                        <td class="text-center">
                          <span class="final-code-item" 
                               :class="{ 'replaced-zero': getFinalBarcode(rIdx, cIdx).startsWith('000'), 
                                         'replaced-nine': getFinalBarcode(rIdx, cIdx).startsWith('999') }">
                            {{ getFinalBarcode(rIdx, cIdx) }}
                          </span>
                        </td>
                      </tr>
                    </template>
                    <tr v-if="matrixLayers === 0">
                      <td colspan="6" class="empty-row">等待 PLC 信号触发采集...</td>
                    </tr>
                  </tbody>
                </table>
              </div>
            </div>
          </div>
          <div v-show="activeTab === 'info'" class="tab-pane">
            <div class="card" style="margin: 20px; border-style: dashed;">
              <div class="card-title">ℹ️ 采集信息详情</div>
              <div class="empty-hint" style="padding: 40px;">
                正在开发中... <br/>
                此区域将用于展示更详细的工序采集数据。
              </div>
            </div>
          </div>
          <div v-show="activeTab === 'plc'" class="tab-pane">
            <PlcInteraction 
              :ip="config.plcIp || '192.168.0.1'"
              :cpu-type="config.plcCpu || 'S71200'"
              :rack="config.plcRack || 0"
              :slot="config.plcSlot || 1"
              @log="addLog"
            />
          </div>
          <div v-show="activeTab === 'api'" class="tab-pane">
            <ApiDetail :records="apiRecords" />
          </div>
          <div v-show="activeTab === 'recipe'" class="tab-pane">
            <RecipeSettings />
          </div>
          <div v-show="activeTab === 'monitor'" class="tab-pane">
            <PlcMonitor :logs="plcLogs" />
          </div>
          <div v-show="activeTab === 'log'" class="tab-pane log-pane">
            <div class="log-scroll">
              <div v-for="(entry, i) in logs" :key="i" class="log-entry" :class="entry.level">
                <span class="log-time">{{ entry.time }}</span>
                <span class="log-msg">{{ entry.msg }}</span>
              </div>
              <div v-if="!logs.length" class="log-empty">暂无日志</div>
            </div>
          </div>

        </div>

      </section>
    </main>
    <ConfigModal v-model="config" v-model:visible="showConfig" @save="onConfigSaved" />
    <OrderSelectModal 
      :orders="pendingOrders" 
      :visible="showOrderSelect" 
      @select="onOrderSelected"
      @close="showOrderSelect = false"
    />
  </div>
</template>

<style scoped>
.app-root { display: flex; flex-direction: column; height: 100vh; width: 100vw; background: #0a0e1a; color: #c8d6e5; font-family: 'Segoe UI', 'Microsoft YaHei', sans-serif; overflow: hidden; }
.app-header { display: flex; align-items: center; padding: 0 20px; height: 52px; background: linear-gradient(135deg, #0d1b2a 0%, #112240 100%); border-bottom: 1px solid rgba(100, 181, 246, 0.2); box-shadow: 0 2px 16px rgba(0, 0, 0, 0.4); flex-shrink: 0; gap: 16px; }
.header-left { display: flex; align-items: center; gap: 10px; }
.brand-icon { width: 34px; height: 34px; background: linear-gradient(135deg, #1565c0, #0d47a1); border-radius: 8px; display: flex; align-items: center; justify-content: center; font-size: 11px; font-weight: 800; color: #e3f2fd; letter-spacing: -0.5px; box-shadow: 0 0 12px rgba(21, 101, 192, 0.5); }
.brand-text { display: flex; flex-direction: column; }
.brand-title { font-size: 15px; font-weight: 700; color: #e3f2fd; line-height: 1.2; }
.brand-sub { font-size: 10px; color: #546e7a; letter-spacing: 0.5px; }
.header-center { flex: 1; display: flex; justify-content: center; }
.process-badge { background: rgba(21, 101, 192, 0.2); border: 1px solid rgba(100, 181, 246, 0.2); border-radius: 20px; padding: 4px 16px; font-size: 12px; display: flex; gap: 6px; }
.process-badge .label { color: #78909c; }
.process-badge .value { color: #42a5f5; font-weight: 600; }

.heartbeat-status {
  display: flex;
  align-items: center;
  gap: 8px;
  background: rgba(0, 0, 0, 0.2);
  padding: 4px 12px;
  border-radius: 12px;
  margin-right: 12px;
  border: 1px solid rgba(255, 255, 255, 0.05);
}
.heartbeat-led {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  background: #f44336;
  box-shadow: 0 0 6px #f44336;
  transition: all 0.3s;
}
.heartbeat-status.online .heartbeat-led {
  background: #00897b; 
  box-shadow: 0 0 4px #00897b;
}
.heartbeat-status.online.active .heartbeat-led {
  background: #00e676; 
  box-shadow: 0 0 12px #00e676;
  transform: scale(1.2);
}
.heartbeat-label {
  font-size: 11px;
  color: #90a4ae;
}
.heartbeat-time {
  font-size: 10px;
  color: #455a64;
  font-family: 'Consolas', monospace;
}

@keyframes pulse-green {
  0% { box-shadow: 0 0 0 0 rgba(0, 230, 118, 0.7); }
  70% { box-shadow: 0 0 0 6px rgba(0, 230, 118, 0); }
  100% { box-shadow: 0 0 0 0 rgba(0, 230, 118, 0); }
}

.header-right { display: flex; gap: 8px; }
.icon-btn { background: rgba(21, 101, 192, 0.2); border: 1px solid rgba(100, 181, 246, 0.2); border-radius: 6px; color: #90caf9; padding: 5px 14px; font-size: 12px; cursor: pointer; transition: all 0.2s; }
.icon-btn:hover { background: rgba(21, 101, 192, 0.4); border-color: #42a5f5; color: #e3f2fd; }
.app-main { display: flex; gap: 12px; padding: 12px; flex: 1; overflow: hidden; }
.left-panel { display: flex; flex-direction: column; gap: 10px; width: 360px; flex-shrink: 0; overflow-y: auto; }
.right-panel { flex: 1; display: flex; flex-direction: column; overflow: hidden; min-width: 0; background: #131929; border: 1px solid rgba(100, 181, 246, 0.12); border-radius: 10px; }
.tab-bar { display: flex; gap: 2px; padding: 8px 10px 0; border-bottom: 1px solid rgba(100, 181, 246, 0.1); background: linear-gradient(180deg, #0d1525 0%, #131929 100%); flex-shrink: 0; }
.tab-btn { display: flex; align-items: center; gap: 6px; padding: 7px 16px; background: transparent; border: 1px solid transparent; border-bottom: none; border-radius: 6px 6px 0 0; color: #546e7a; font-size: 12px; font-weight: 500; cursor: pointer; transition: all 0.2s; position: relative; bottom: -1px; }
.tab-btn:hover { color: #90caf9; background: rgba(100, 181, 246, 0.05); }
.tab-btn.active { color: #42a5f5; background: #131929; border-color: rgba(100, 181, 246, 0.15); font-weight: 600; }
.tab-count { background: rgba(66, 165, 245, 0.2); color: #42a5f5; font-size: 10px; padding: 1px 6px; border-radius: 10px; font-weight: 600; }
.tab-content { flex: 1; overflow: hidden; position: relative; }
.tab-pane { 
  position: absolute; 
  inset: 0; 
  overflow-y: auto !important; 
  display: flex; 
  flex-direction: column; 
}
.tab-pane::-webkit-scrollbar {
  width: 10px !important;
}
.tab-pane::-webkit-scrollbar-track {
  background: rgba(255, 255, 255, 0.05);
}
.tab-pane::-webkit-scrollbar-thumb {
  background-color: #ffffff !important;
  border-radius: 10px;
  border: 2px solid #0d1117;
}
.log-pane { padding: 10px; }
.card { background: #131929; border: 1px solid rgba(100, 181, 246, 0.12); border-radius: 10px; padding: 14px; flex-shrink: 0; }
.card-title { display: flex; align-items: center; gap: 8px; font-size: 13px; font-weight: 700; color: #90caf9; margin-bottom: 12px; letter-spacing: 0.5px; }
.step-badge { width: 20px; height: 20px; background: linear-gradient(135deg, #1565c0, #0d47a1); border-radius: 50%; display: inline-flex; align-items: center; justify-content: center; font-size: 11px; color: white; font-weight: 700; flex-shrink: 0; }
.scan-input-wrap { display: flex; align-items: center; gap: 8px; background: #0d1117; border: 2px solid rgba(100, 181, 246, 0.2); border-radius: 8px; padding: 4px 6px 4px 12px; transition: border-color 0.2s, box-shadow 0.2s; }
.scan-input-wrap.scanning { border-color: #42a5f5; box-shadow: 0 0 0 3px rgba(66, 165, 245, 0.1), 0 0 20px rgba(66, 165, 245, 0.2); }
.scan-input-wrap:focus-within { border-color: #42a5f5; box-shadow: 0 0 0 3px rgba(66, 165, 245, 0.1); }
.scan-icon { font-size: 16px; flex-shrink: 0; }
.scan-input { flex: 1; background: none; border: none; outline: none; color: #e0e6ed; font-size: 14px; font-family: 'Consolas', monospace; padding: 8px 0; min-width: 0; }
.scan-input::placeholder { color: #37474f; }
.scan-btn { background: linear-gradient(135deg, #1565c0, #0d47a1); border: none; border-radius: 6px; color: #e3f2fd; padding: 7px 16px; font-size: 12px; font-weight: 600; cursor: pointer; transition: all 0.2s; white-space: nowrap; flex-shrink: 0; }
.scan-btn:hover:not(:disabled) { background: linear-gradient(135deg, #1976d2, #1565c0); box-shadow: 0 4px 12px rgba(21, 101, 192, 0.4); }
.scan-btn:disabled { opacity: 0.4; cursor: not-allowed; }
.scan-hint { font-size: 11px; color: #37474f; margin: 6px 0 0 0; }
kbd { background: rgba(100, 181, 246, 0.1); border: 1px solid rgba(100, 181, 246, 0.2); border-radius: 3px; padding: 1px 5px; font-size: 10px; color: #64b5f6; }
.info-grid { display: flex; flex-direction: column; gap: 8px; }
.info-item { display: flex; justify-content: space-between; align-items: center; padding: 7px 10px; background: rgba(21, 101, 192, 0.06); border-radius: 6px; border: 1px solid rgba(100, 181, 246, 0.08); gap: 8px; }
.info-label { font-size: 11px; color: #546e7a; flex-shrink: 0; }
.info-value { font-size: 12px; color: #cfd8dc; text-align: right; word-break: break-all; }
.info-value.highlight { color: #42a5f5; font-weight: 700; font-size: 13px; }
.info-value.mono { font-family: 'Consolas', monospace; }
.info-value.highlight-sn { color: #fbc02d; font-weight: 700; font-size: 13px; }
.empty-hint { text-align: center; color: #37474f; font-size: 12px; padding: 16px 0; }
.result-display { display: flex; align-items: center; justify-content: center; gap: 10px; padding: 16px; border-radius: 8px; margin-bottom: 12px; background: rgba(21, 101, 192, 0.05); border: 1px solid rgba(100, 181, 246, 0.1); transition: all 0.3s; }
.result-display.ok { background: rgba(0, 230, 118, 0.08); border-color: rgba(0, 230, 118, 0.3); box-shadow: 0 0 24px rgba(0, 230, 118, 0.15); }
.result-display.ng { background: rgba(255, 82, 82, 0.08); border-color: rgba(255, 82, 82, 0.3); box-shadow: 0 0 24px rgba(255, 82, 82, 0.15); }
.result-icon { font-size: 28px; }
.result-text { font-size: 28px; font-weight: 800; letter-spacing: 1px; color: #e0e6ed; }
.result-display.ok .result-text { color: #00e676; }
.result-display.ng .result-text { color: #ff5252; }
.result-msg { font-size: 12px; color: #78909c; }
.result-actions { display: flex; gap: 10px; margin-bottom: 10px; }
.btn-ok, .btn-ng { flex: 1; padding: 12px; border: none; border-radius: 8px; font-size: 14px; font-weight: 700; cursor: pointer; transition: all 0.2s; letter-spacing: 0.5px; }
.btn-ok { background: linear-gradient(135deg, #00c853, #00897b); color: white; box-shadow: 0 4px 16px rgba(0, 200, 83, 0.2); }
.btn-ng { background: linear-gradient(135deg, #f44336, #c62828); color: white; box-shadow: 0 4px 16px rgba(244, 67, 54, 0.2); }
.btn-ok:disabled, .btn-ng:disabled { opacity: 0.3; cursor: not-allowed; }
.btn-reset { width: 100%; padding: 9px; background: rgba(100, 181, 246, 0.08); border: 1px solid rgba(100, 181, 246, 0.2); border-radius: 6px; color: #90caf9; font-size: 12px; cursor: pointer; transition: all 0.2s; }
.error-box { background: rgba(244, 67, 54, 0.08); border: 1px solid rgba(244, 67, 54, 0.25); border-radius: 6px; padding: 10px 14px; font-size: 12px; color: #ef9a9a; display: flex; gap: 8px; align-items: flex-start; margin-bottom: 8px; }
.loading-spin { width: 14px; height: 14px; border: 2px solid rgba(66, 165, 245, 0.2); border-top-color: #42a5f5; border-radius: 50%; animation: spin 0.8s linear infinite; margin-left: auto; }
.log-scroll { flex: 1; overflow-y: auto; display: flex; flex-direction: column; gap: 3px; padding-right: 4px; }
.log-entry { display: flex; gap: 10px; font-size: 11px; padding: 4px 8px; border-radius: 4px; background: rgba(255, 255, 255, 0.02); }
.log-entry.success { color: #69f0ae; }
.log-entry.error { color: #ff5252; }
.log-time { color: #455a64; flex-shrink: 0; width: 70px; }
.log-msg { word-break: break-all; line-height: 1.5; }
.log-empty { text-align: center; color: #37474f; font-size: 12px; padding: 16px; }
@keyframes spin { to { transform: rotate(360deg); } }

.plc-mock-btn {
  background: linear-gradient(135deg, #6a1b9a, #4a148c);
  border: none;
  border-radius: 6px;
  color: #f3e5f5;
  padding: 7px 12px;
  font-size: 12px;
  font-weight: 600;
  cursor: pointer;
  transition: all 0.2s;
  white-space: nowrap;
  flex-shrink: 0;
  border: 1px solid rgba(171, 71, 188, 0.3);
  margin-left: 8px;
}

.plc-mock-btn:hover {
  background: linear-gradient(135deg, #7b1fa2, #6a1b9a);
  box-shadow: 0 4px 12px rgba(106, 27, 154, 0.4);
  transform: translateY(-1px);
}

.barcode-display-card { 
  margin-top: 12px; 
  background: rgba(13, 17, 23, 0.6); 
  border: 1px solid rgba(144, 202, 249, 0.15); 
  border-radius: 8px; 
  padding: 12px; 
  display: flex; 
  flex-direction: column; 
  margin-bottom: 8px;
  flex: 1;
  min-height: 480px;
}
.flex-grow { flex: 1; }
.matrix-table-container { flex: 1; overflow-y: auto; border-radius: 8px; background: #0d1117; border: 1px solid rgba(144, 202, 249, 0.1); margin-top: 10px; }
.matrix-table { width: 100%; border-collapse: collapse; font-size: 12px; table-layout: fixed; }
.matrix-table th { background: rgba(13, 71, 161, 0.4); color: #90a4ae; padding: 10px 8px; text-align: center; font-weight: 600; border-bottom: 2px solid rgba(144, 202, 249, 0.2); position: sticky; top: 0; z-index: 10; }
.matrix-table td { padding: 8px; border-bottom: 1px solid rgba(255, 255, 255, 0.05); color: #e3f2fd; vertical-align: middle; text-align: center; }
.matrix-table tr:hover { background: rgba(255, 255, 255, 0.03); }
.cell-content { display: flex; align-items: center; justify-content: center; gap: 4px; }
.barcode-text { font-family: "Consolas", monospace; font-size: 11px; color: #bbdefb; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; max-width: 140px; }
.mini-ok { color: #00e676; font-weight: bold; font-size: 14px; }
.text-center { text-align: center !important; }
.badge { font-size: 10px; padding: 1px 6px; border-radius: 4px; font-weight: 700; display: inline-block; }
.badge.success { background: rgba(0, 230, 118, 0.15); color: #00e676; border: 1px solid rgba(0, 230, 118, 0.3); }
.badge.error { background: rgba(255, 82, 82, 0.15); color: #ff5252; border: 1px solid rgba(255, 82, 82, 0.3); }
.badge.info { background: rgba(33, 150, 243, 0.15); color: #2196f3; border: 1px solid rgba(33, 150, 243, 0.3); }
.empty-row { text-align: center; padding: 60px !important; color: #546e7a; font-style: italic; }
@keyframes fadeIn { from { opacity: 0; transform: translateX(10px); } to { opacity: 1; transform: translateX(0); } }
.final-barcode { font-family: 'Consolas', monospace; font-size: 10px; color: #64b5f6; }
.final-barcode.replaced { color: #ffab40; font-weight: bold; }
.final-codes-column { display: flex; flex-direction: column; gap: 2px; padding: 4px; background: rgba(0,0,0,0.2); }
.final-code-item { font-family: 'Consolas', monospace; font-size: 10px; color: #64b5f6; padding: 2px 4px; border-radius: 2px; }
.final-code-item.replaced-zero { color: #ff5252; background: rgba(255, 82, 82, 0.1); font-weight: bold; }
.final-code-item.replaced-nine { color: #bdbdbd; background: rgba(255, 255, 255, 0.05); }
</style>
