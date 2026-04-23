<script setup lang="ts">
import { computed } from 'vue'
import type { OrderInfo } from '../types/mes'

const props = defineProps<{
  orders: OrderInfo[]
  visible: boolean
}>()

const emit = defineEmits<{
  (e: 'select', order: OrderInfo): void
  (e: 'close'): void
}>()

// 仅显示 order_Type 为 0 或 1 的工单
const filteredOrders = computed(() => {
  return props.orders.filter(order => {
    const type = order.order_Type ?? order.orderType
    return type === 0 || type === 1 || type === '0' || type === '1'
  })
})

function onSelect(order: OrderInfo) {
  emit('select', order)
}
</script>

<template>
  <Transition name="modal">
    <div v-if="visible" class="modal-overlay" @click.self="emit('close')">
      <div class="modal-panel">
        <div class="modal-header">
          <span class="icon">📑</span>
          <h2>请选择工单</h2>
          <button class="close-btn" @click="emit('close')">✕</button>
        </div>
        
        <div class="modal-body">
          <div class="order-list">
            <div 
              v-for="order in filteredOrders" 
              :key="order.code || order.orderCode" 
              class="order-item"
              @click="onSelect(order)"
            >
              <div class="order-main">
                <span class="order-id-label">工单号:</span>
                <span class="order-id-value">{{ order.code || (order as any).orderCode }}</span>
              </div>
              <div class="select-indicator">选择 ➔</div>
            </div>
          </div>
        </div>
      </div>
    </div>
  </Transition>
</template>

<style scoped>
.modal-overlay {
  position: fixed;
  inset: 0;
  background: rgba(0, 0, 0, 0.7);
  backdrop-filter: blur(5px);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 2000;
}

.modal-panel {
  background: #131929;
  border: 1px solid rgba(100, 181, 246, 0.3);
  border-radius: 12px;
  width: 500px;
  max-width: 90vw;
  max-height: 80vh;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  box-shadow: 0 20px 60px rgba(0, 0, 0, 0.6);
}

.modal-header {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 16px 20px;
  background: linear-gradient(135deg, #0d47a1, #1565c0);
  color: white;
}

.modal-header h2 { margin: 0; font-size: 16px; font-weight: 600; flex: 1; }

.close-btn { background: none; border: none; color: white; font-size: 18px; cursor: pointer; opacity: 0.7; }
.close-btn:hover { opacity: 1; }

.modal-body { padding: 20px; overflow-y: auto; }

.order-list { display: flex; flex-direction: column; gap: 12px; }

.order-item {
  background: rgba(255, 255, 255, 0.03);
  border: 1px solid rgba(100, 181, 246, 0.1);
  border-radius: 8px;
  padding: 14px;
  cursor: pointer;
  transition: all 0.2s;
  position: relative;
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.order-item:hover {
  background: rgba(66, 165, 245, 0.08);
  border-color: #42a5f5;
  transform: translateX(4px);
}

.order-main { display: flex; align-items: center; gap: 8px; }
.order-id-label { color: #546e7a; font-size: 12px; }
.order-id-value { color: #42a5f5; font-size: 15px; font-weight: 700; font-family: 'Consolas', monospace; }

.order-sub { display: flex; gap: 8px; }
.tag { font-size: 11px; padding: 2px 8px; border-radius: 4px; background: rgba(100, 181, 246, 0.1); color: #90caf9; }
.tag.gray { background: rgba(255, 255, 255, 0.05); color: #78909c; }

.select-indicator {
  position: absolute;
  right: 16px;
  top: 50%;
  transform: translateY(-50%);
  font-size: 12px;
  color: #42a5f5;
  font-weight: 600;
  opacity: 0;
  transition: opacity 0.2s;
}

.order-item:hover .select-indicator { opacity: 1; }

/* 动画 */
.modal-enter-active, .modal-leave-active { transition: opacity 0.3s ease; }
.modal-enter-from, .modal-leave-to { opacity: 0; }
</style>
