<script setup lang="ts">
import { reactive, watch } from 'vue'
import type { AppConfig } from '../types/mes'

const props = defineProps<{
  modelValue: AppConfig
  visible: boolean
}>()

const emit = defineEmits<{
  (e: 'update:modelValue', val: AppConfig): void
  (e: 'update:visible', val: boolean): void
  (e: 'save', val: AppConfig): void
}>()

// 本地表单副本
const form = reactive<AppConfig>({ ...props.modelValue })

// 重要：每次打开弹窗时，同步父组件的最新数据
watch(() => props.visible, (newVal) => {
  if (newVal) {
    Object.assign(form, props.modelValue)
  }
})

const handleSave = () => {
  const updatedData = JSON.parse(JSON.stringify(form)) // 深度克隆
  emit('update:modelValue', updatedData)
  emit('save', updatedData)
  emit('update:visible', false)
}

function handleCancel() {
  // 还原
  Object.assign(form, props.modelValue)
  emit('update:visible', false)
}
</script>

<template>
  <Transition name="modal">
    <div v-if="visible" class="modal-overlay" @click.self="handleCancel">
      <div class="modal-panel">
        <div class="modal-header">
          <span class="icon">⚙️</span>
          <h2>系统配置</h2>
          <button class="close-btn" @click="handleCancel">✕</button>
        </div>

        <div class="modal-body">
          <!-- 1. API 接口配置 -->
          <div class="config-section">
            <h3 class="section-title">🔌 接口配置 (MES API)</h3>
            <div class="field-grid">
              <div class="field-group">
                <label>获取工单 API</label>
                <input v-model="form.orderApiUrl" type="text" class="input-field" />
              </div>
              <div class="field-group">
                <label>获取工步 API</label>
                <input v-model="form.routeApiUrl" type="text" class="input-field" />
              </div>
              <div class="field-group">
                <label>单件物料验证 API</label>
                <input v-model="form.singleMaterialApiUrl" type="text" class="input-field" />
              </div>
              <div class="field-group">
                <label>全量物料验证 API</label>
                <input v-model="form.fullMaterialApiUrl" type="text" class="input-field" />
              </div>
              <div class="field-group">
                <label>模组码生成 API</label>
                <input v-model="form.codeCreateApiUrl" type="text" class="input-field" />
              </div>
               <div class="field-group">
                <label>信息推送 API</label>
                <input v-model="form.mesPushApiUrl" type="text" class="input-field" />
              </div>
              <div class="field-group">
                <label>重码校验 API</label>
                <input v-model="form.duplicateCheckApiUrl" type="text" class="input-field" />
              </div>
              <div class="field-group">
                <label>获取电芯数据 API</label>
                <input v-model="form.cellDataApiUrl" type="text" class="input-field" />
              </div>
            </div>
          </div>

          <!-- 3. 扫码与工序 -->
          <div class="config-section">
            <h3 class="section-title">🔍 扫码与工序</h3>
            <div class="field-grid">
              <div class="field-group">
                <label>工序代码 (workSeqNo)</label>
                <input v-model="form.technicsProcessCode" type="text" class="input-field" />
              </div>
              <div class="field-group">
                <label>工序名称</label>
                <input v-model="form.technicsProcessName" type="text" class="input-field" />
              </div>
              <div class="field-group">
                <label>条码匹配正则</label>
                <input v-model="form.barcodeMatchRegex" type="text" class="input-field" />
              </div>
              <div class="field-group">
                <label>生产类型</label>
                <input v-model="form.produce_Type" type="text" class="input-field" />
              </div>
              <div class="field-group">
                <label>租户 ID</label>
                <input v-model="form.tenantID" type="text" class="input-field" />
              </div>
              <div class="field-group">
                <label>扫码枪 IP</label>
                <input v-model="form.scannerIp" type="text" class="input-field" />
              </div>
              <div class="field-group">
                <label>扫码枪端口</label>
                <input v-model.number="form.scannerPort" type="number" class="input-field" />
              </div>
            </div>
          </div>

          <!-- 4. 硬件通讯 (PLC) -->
          <div class="config-section">
            <h3 class="section-title">⚙️ 硬件通讯</h3>
            <div class="field-grid">
              <div class="field-group">
                <label>PLC IP</label>
                <input v-model="form.plcIp" type="text" class="input-field" />
              </div>
              <div class="field-group">
                <label>CPU 类型</label>
                <select v-model="form.plcCpu" class="input-field">
                  <option value="S71200">S7-1200</option>
                  <option value="S71500">S7-1500</option>
                  <option value="S7300">S7-300</option>
                </select>
              </div>
              <div class="field-group">
                <label>PLC 心跳地址</label>
                <input v-model="form.plcHeartbeatAddress" type="text" class="input-field" placeholder="例如 DB1.DBX0.0" />
              </div>
              <div class="field-group">
                <label>A面数据块编号 (DB Number)</label>
                <input v-model.number="form.plcAStackDbNum" type="number" class="input-field" placeholder="例如 1590" />
              </div>
              <div class="field-group">
                <label>B面数据块编号 (DB Number)</label>
                <input v-model.number="form.plcBStackDbNum" type="number" class="input-field" placeholder="例如 1591" />
              </div>
              <div class="field-group">
                <label>A面堆叠完成信号</label>
                <input v-model="form.plcAStackFinishAddress" type="text" class="input-field" placeholder="例如 DB1.DBX0.1" />
              </div>
              <div class="field-group">
                <label>B面堆叠完成信号</label>
                <input v-model="form.plcBStackFinishAddress" type="text" class="input-field" placeholder="例如 DB1.DBX0.2" />
              </div>
              <div class="field-group">
                <label>电芯层数点位 (BYTE)</label>
                <input v-model="form.plcCellLayerAddress" type="text" class="input-field" placeholder="例如: DB1.DBB1" />
              </div>
              <div class="field-group">
                <label>堆叠模组序号点位 (BYTE/WORD)</label>
                <input v-model="form.plcModuleSnAddress" type="text" class="input-field" placeholder="例如: DB1.DBB2" />
              </div>
              <div class="field-group">
                <label>模组码写入起始地址 (STRING)</label>
                <input v-model="form.plcModuleCodeWriteAddress" type="text" class="input-field" placeholder="例如: DB1.DBB100" />
              </div>
              <div class="field-group">
                <label>每层电芯个数</label>
                <input v-model.number="form.cellsPerLayer" type="number" class="input-field" placeholder="输入整数" />
              </div>
              <div class="field-group">
                <label>1列电芯码起始位置</label>
                <input v-model="form.col1StartAddr" type="text" class="input-field" placeholder="例如: DB1.DBB10" />
              </div>
              <div class="field-group">
                <label>2列电芯码起始位置</label>
                <input v-model="form.col2StartAddr" type="text" class="input-field" placeholder="例如: DB1.DBB11" />
              </div>
              <div class="field-group">
                <label>3列电芯码起始位置</label>
                <input v-model="form.col3StartAddr" type="text" class="input-field" placeholder="例如: DB1.DBB12" />
              </div>
              <div class="field-group">
                <label>电芯码长度</label>
                <input v-model.number="form.cellBarcodeLength" type="number" class="input-field" placeholder="输入整数" />
              </div>
            </div>
          </div>

          <!-- 5. 系统安全 -->
          <div class="config-section">
            <h3 class="section-title">🛡️ 系统安全</h3>
            <div class="field-grid">
              <div class="field-group">
                <label>管理账号</label>
                <input v-model="form.adminUsername" type="text" class="input-field" />
              </div>
              <div class="field-group">
                <label>管理密码</label>
                <input v-model="form.adminPassword" type="password" class="input-field" />
              </div>
            </div>
          </div>
        </div>

        <div class="modal-footer">
          <button class="btn-cancel" @click="handleCancel">取消</button>
          <button class="btn-save" @click="handleSave">保存配置</button>
        </div>
      </div>
    </div>
  </Transition>
</template>

<style scoped>
.modal-overlay {
  position: fixed;
  inset: 0;
  background: rgba(0, 0, 0, 0.65);
  backdrop-filter: blur(4px);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1000;
}

.modal-panel {
  background: #1a1f2e;
  border: 1px solid rgba(100, 181, 246, 0.25);
  border-radius: 12px;
  width: 560px;
  max-width: 95vw;
  box-shadow: 0 24px 64px rgba(0, 0, 0, 0.5);
  overflow: hidden;
}

.modal-header {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 18px 24px;
  background: linear-gradient(135deg, #0d47a1 0%, #1565c0 100%);
  border-bottom: 1px solid rgba(100, 181, 246, 0.2);
}

.modal-header h2 {
  flex: 1;
  font-size: 16px;
  font-weight: 600;
  color: #e3f2fd;
  margin: 0;
}

.icon {
  font-size: 18px;
}

.close-btn {
  background: none;
  border: none;
  color: #90caf9;
  font-size: 16px;
  cursor: pointer;
  padding: 4px 8px;
  border-radius: 4px;
  transition: background 0.2s;
}
.close-btn:hover {
  background: rgba(255, 255, 255, 0.1);
}

.modal-body {
  padding: 24px;
  display: flex;
  flex-direction: column;
  gap: 24px;
  max-height: 70vh;
  overflow-y: auto;
}

.config-section {
  display: flex;
  flex-direction: column;
  gap: 16px;
  padding-bottom: 16px;
  border-bottom: 1px solid rgba(255, 255, 255, 0.05);
}

.section-title {
  font-size: 14px;
  font-weight: 700;
  color: #64b5f6;
  margin: 0;
  padding-left: 8px;
  border-left: 3px solid #1565c0;
}

.field-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 16px;
}

.field-group {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.field-group label {
  font-size: 13px;
  font-weight: 600;
  color: #90caf9;
  letter-spacing: 0.5px;
}

.field-group small {
  font-size: 11px;
  color: #546e7a;
}

.input-field {
  background: #0d1117;
  border: 1px solid rgba(100, 181, 246, 0.2);
  border-radius: 6px;
  color: #e0e6ed;
  padding: 10px 14px;
  font-size: 13px;
  font-family: 'Consolas', monospace;
  transition: border-color 0.2s, box-shadow 0.2s;
  outline: none;
}

.input-field:focus {
  border-color: #42a5f5;
  box-shadow: 0 0 0 3px rgba(66, 165, 245, 0.15);
}

.input-field::placeholder {
  color: #37474f;
}

.modal-footer {
  padding: 16px 24px;
  display: flex;
  gap: 12px;
  justify-content: flex-end;
  border-top: 1px solid rgba(100, 181, 246, 0.1);
}

.btn-cancel {
  padding: 9px 20px;
  background: transparent;
  border: 1px solid rgba(100, 181, 246, 0.25);
  border-radius: 6px;
  color: #78909c;
  font-size: 13px;
  cursor: pointer;
  transition: all 0.2s;
}
.btn-cancel:hover {
  border-color: #42a5f5;
  color: #42a5f5;
}

.btn-save {
  padding: 9px 24px;
  background: linear-gradient(135deg, #1565c0, #0d47a1);
  border: 1px solid rgba(100, 181, 246, 0.3);
  border-radius: 6px;
  color: #e3f2fd;
  font-size: 13px;
  font-weight: 600;
  cursor: pointer;
  transition: all 0.2s;
}
.btn-save:hover {
  background: linear-gradient(135deg, #1976d2, #1565c0);
  box-shadow: 0 4px 16px rgba(21, 101, 192, 0.4);
}

.modal-enter-active,
.modal-leave-active {
  transition: opacity 0.25s ease;
}
.modal-enter-from,
.modal-leave-to {
  opacity: 0;
}
.modal-enter-active .modal-panel,
.modal-leave-active .modal-panel {
  transition: transform 0.25s ease;
}
.modal-enter-from .modal-panel {
  transform: scale(0.92) translateY(-20px);
}
.modal-leave-to .modal-panel {
  transform: scale(0.92) translateY(-20px);
}
</style>
