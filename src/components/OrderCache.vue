<script setup lang="ts">
import { defineProps, defineEmits } from 'vue'
import type { OrderInfo } from '../types/mes'

const props = defineProps<{
  orders: OrderInfo[],
  highlightedCode?: string | null
}>()

const emit = defineEmits(['select'])

function selectOrder(order: OrderInfo) {
  emit('select', order)
}
</script>

<template>
  <div class="order-cache">
    <div class="cache-header">
      <div class="title">📦 待产工单缓存 (已拉取)</div>
      <div class="count-badge">{{ orders.length }} 个任务</div>
    </div>
    
    <div class="table-container">
      <table class="cache-table">
        <thead>
          <tr>
            <th>工单号</th>
            <th>关联工单</th>
            <th>工艺路线</th>
            <th>模组类型</th>
            <th>状态</th>
            <th>类型</th>
            <th>项目代码</th>
          </tr>
        </thead>
        <tbody>
          <tr 
            v-for="order in orders" 
            :key="order.code || order.orderCode"
            :class="{ 'is-highlighted': highlightedCode === (order.code || order.orderCode) }"
          >
            <td class="highlight">{{ order.code || order.orderCode }}</td>
            <td class="highlight-alt">{{ order.union_Code || '-' }}</td>
            <td class="mono">{{ order.route_No }}</td>
            <td class="module-type">
              {{ (order.formulaCount1 || '') + (order.formulaSpecs1 || '') + (order.formulaCount2 || '') + (order.formulaSpecs2 || '') || '-' }}
              <span v-if="highlightedCode === (order.code || order.orderCode)" class="match-badge">🎯 配方锁定</span>
            </td>
            <td>
              <span class="status-tag" :class="'status-' + order.order_Status">
                {{ 
                  order.order_Status === '1' ? '新建' : 
                  order.order_Status === '2' ? '下发' : 
                  order.order_Status === '3' ? '执行中' : 
                  order.order_Status 
                }}
              </span>
            </td>
            <td>
              <span v-if="(order.type ?? order.orderType) === 0 || (order.type ?? order.orderType) === '0'" class="type-tag gray">正常工单</span>
              <span v-else-if="(order.type ?? order.orderType) === 1 || (order.type ?? order.orderType) === '1'" class="type-tag blue">主工单</span>
              <span v-else-if="(order.type ?? order.orderType) === 2 || (order.type ?? order.orderType) === '2'" class="type-tag purple">辅工单</span>
              <span v-else class="type-tag">{{ order.type ?? order.orderType }}</span>
            </td>
            <td>{{ order.projectCode || '-' }}</td>
          </tr>
          <tr v-if="orders.length === 0">
            <td colspan="7" class="empty-hint">暂无缓存工单，请先扫描条码拉取数据</td>
          </tr>
        </tbody>
      </table>
    </div>
  </div>
</template>

<style scoped>
.order-cache {
  display: flex;
  flex-direction: column;
  height: 100%;
  padding: 12px;
  background: #0a0e1a;
  color: #c8d6e5;
}

.cache-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 12px;
  padding: 0 4px;
}

.cache-header .title {
  font-size: 14px;
  font-weight: 700;
  color: #90caf9;
  letter-spacing: 0.5px;
}

.count-badge {
  background: rgba(66, 165, 245, 0.2);
  color: #42a5f5;
  padding: 2px 10px;
  border-radius: 12px;
  font-size: 12px;
  font-weight: 600;
}

.table-container {
  flex: 1;
  overflow: auto;
  border: 1px solid rgba(144, 202, 249, 0.15);
  border-radius: 8px;
  background: rgba(19, 25, 41, 0.5);
}

.cache-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 13px;
  table-layout: fixed;
}

.cache-table th {
  position: sticky;
  top: 0;
  background: #1a2235;
  color: #42a5f5;
  padding: 12px 10px;
  text-align: left;
  font-weight: 600;
  border-bottom: 2px solid rgba(144, 202, 249, 0.2);
  z-index: 10;
}

.cache-table td {
  padding: 10px;
  border-bottom: 1px solid rgba(255, 255, 255, 0.05);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.cache-table tr:hover {
  background: rgba(66, 165, 245, 0.05);
}

.cache-table .highlight {
  color: #90caf9;
  font-weight: 600;
}

.cache-table .highlight-alt {
  color: #64b5f6;
  opacity: 0.8;
}

.cache-table .mono {
  font-family: 'Consolas', monospace;
  font-size: 12px;
  color: #78909c;
}

.module-type {
  color: #ffb74d;
  font-weight: 600;
  font-family: 'Consolas', monospace;
}

.cache-table .time {
  font-size: 11px;
  color: #546e7a;
}

.status-tag {
  padding: 2px 8px;
  border-radius: 4px;
  font-size: 11px;
  font-weight: 600;
}

.status-1 {
  background: rgba(33, 150, 243, 0.15);
  color: #2196f3;
  border: 1px solid rgba(33, 150, 243, 0.3);
}

.status-2 {
  background: rgba(0, 230, 118, 0.15);
  color: #00e676;
  border: 1px solid rgba(0, 230, 118, 0.3);
}

.status-3 {
  background: rgba(255, 152, 0, 0.15);
  color: #ff9800;
  border: 1px solid rgba(255, 152, 0, 0.3);
}

.type-tag {
  padding: 1px 6px;
  border-radius: 4px;
  font-size: 10px;
}

.type-tag.blue { background: rgba(33, 150, 243, 0.2); color: #64b5f6; }
.type-tag.purple { background: rgba(156, 39, 176, 0.2); color: #ce93d8; }
.type-tag.gray { background: rgba(255, 255, 255, 0.05); color: #90a4ae; }

.select-btn {
  background: linear-gradient(135deg, #1565c0, #0d47a1);
  border: none;
  border-radius: 4px;
  color: white;
  padding: 4px 8px;
  font-size: 11px;
  cursor: pointer;
  transition: all 0.2s;
}

.select-btn:hover {
  filter: brightness(1.2);
  box-shadow: 0 2px 6px rgba(21, 101, 192, 0.4);
}

.empty-hint {
  text-align: center;
  padding: 60px !important;
  color: #37474f;
  font-style: italic;
}

.cache-footer {
  margin-top: 12px;
  font-size: 12px;
  color: #546e7a;
  text-align: center;
}
</style>
