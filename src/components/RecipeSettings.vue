<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import type { RecipeMaster, RecipeDetail } from '../types/recipe'
import { BACKEND_URL } from '../utils/constants'

const MASTER_KEY = 'mes_recipe_master_v1'
const DETAIL_KEY = 'mes_recipe_detail_v1'

const masters = ref<RecipeMaster[]>([])
const details = ref<RecipeDetail[]>([])
const selectedMasterId = ref<string | null>(null)

// Modals
const showMasterModal = ref(false)
const showDetailModal = ref(false)
const editingMasterId = ref<string | null>(null)
const editingDetailId = ref<string | null>(null)

const masterForm = ref<Omit<RecipeMaster, 'id' | 'createTime'>>({
  productName: '',
  cellCount: '',
  plcModel: ''
})

const detailForm = ref<Omit<RecipeDetail, 'id' | 'masterId' | 'createTime'>>({
  productName: '',
  moduleType: '',
  productSpec: '',
  blockIndex: '',
  blockSpec: '',
  orderType: '0'
})

const API_URL = `${BACKEND_URL}/api/recipe/config`

async function loadRecipes() {
  try {
    const res = await fetch(API_URL)
    const data = await res.json()
    masters.value = data.masters || []
    details.value = data.details || []
    if (masters.value.length > 0 && !selectedMasterId.value) selectedMasterId.value = masters.value[0].id
  } catch (err) {
    console.error('加载配方失败:', err)
  }
}

async function saveToBackend() {
  try {
    await fetch(API_URL, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        masters: masters.value,
        details: details.value
      })
    })
  } catch (err) {
    console.error('保存配方失败:', err)
    alert('配方保存至后端失败，请检查网络或后端服务。')
  }
}

onMounted(() => {
  loadRecipes()
})

// Master Actions
function openAddMaster() {
  editingMasterId.value = null
  masterForm.value = { productName: '', cellCount: '', plcModel: '' }
  showMasterModal.value = true
}

function openEditMaster(item: RecipeMaster) {
  editingMasterId.value = item.id
  masterForm.value = { ...item }
  showMasterModal.value = true
}

async function deleteMaster(id: string) {
  if (!confirm('确定删除该项目及其所有子项吗？')) return
  masters.value = masters.value.filter(m => m.id !== id)
  details.value = details.value.filter(d => d.masterId !== id)
  if (selectedMasterId.value === id) selectedMasterId.value = masters.value[0]?.id || null
  await saveToBackend()
}

async function handleMasterSubmit() {
  if (editingMasterId.value) {
    const idx = masters.value.findIndex(m => m.id === editingMasterId.value)
    if (idx > -1) masters.value[idx] = { ...masters.value[idx], ...masterForm.value }
  } else {
    const newMaster: RecipeMaster = {
      ...masterForm.value,
      id: Date.now().toString(),
      createTime: new Date().toLocaleString()
    }
    masters.value.push(newMaster)
    selectedMasterId.value = newMaster.id
  }
  await saveToBackend()
  showMasterModal.value = false
}

// Detail Actions
const filteredDetails = computed(() => details.value.filter(d => d.masterId === selectedMasterId.value))

function openAddDetail() {
  if (!selectedMasterId.value) return alert('请先选择或创建一个项目')
  const master = masters.value.find(m => m.id === selectedMasterId.value)
  editingDetailId.value = null
  detailForm.value = { 
    productName: master?.productName || '', 
    moduleType: '', productSpec: '', blockIndex: '', 
    blockSpec: '', orderType: '0' 
  }
  showDetailModal.value = true
}

function openEditDetail(item: RecipeDetail) {
  editingDetailId.value = item.id
  detailForm.value = { ...item }
  showDetailModal.value = true
}

async function handleDetailSubmit() {
  if (editingDetailId.value) {
    const idx = details.value.findIndex(d => d.id === editingDetailId.value)
    if (idx > -1) details.value[idx] = { ...details.value[idx], ...detailForm.value }
  } else {
    const newDetail: RecipeDetail = {
      ...detailForm.value,
      id: Date.now().toString(),
      masterId: selectedMasterId.value!,
      createTime: new Date().toLocaleString()
    }
    details.value.push(newDetail)
  }
  await saveToBackend()
  showDetailModal.value = false
}

async function deleteDetail(id: string) {
  if (!confirm('确定删除该子项吗？')) return
  details.value = details.value.filter(d => d.id !== id)
  await saveToBackend()
}
</script>

<template>
  <div class="recipe-container">
    <!-- Level 1: Master -->
    <div class="section master-section">
      <div class="section-header">
        <div class="title">📋 项目管理 (一级)</div>
        <button class="add-btn" @click="openAddMaster">+ 新增项目</button>
      </div>
      <div class="table-wrap">
        <table class="data-table">
          <thead>
            <tr>
              <th>产品名称</th>
              <th>电芯数量</th>
              <th>PLC型号</th>
              <th>生成时间</th>
              <th class="ops">操作</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="m in masters" :key="m.id" :class="{ selected: selectedMasterId === m.id }" @click="selectedMasterId = m.id">
              <td>{{ m.productName }}</td>
              <td>{{ m.cellCount }}</td>
              <td>{{ m.plcModel }}</td>
              <td class="time">{{ m.createTime }}</td>
              <td class="ops">
                <button class="text-btn blue" @click.stop="openAddDetail">添加箱模组</button>
                <button class="text-btn" @click.stop="openEditMaster(m)">修改</button>
                <button class="text-btn red" @click.stop="deleteMaster(m.id)">删除</button>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>

    <!-- Level 2: Detail -->
    <div class="section detail-section">
      <div class="section-header">
        <div class="title">🔗 子项明细 (二级) - {{ masters.find(m => m.id === selectedMasterId)?.productName || '未选择' }}</div>
      </div>
      <div class="table-wrap">
        <table class="data-table small">
          <thead>
            <tr>
              <th>产品名称</th>
              <th>大模组类型</th>
              <th>产品规格</th>
              <th>模组块序号</th>
              <th>模组块规格</th>
              <th>工单类型</th>
              <th>生成时间</th>
              <th class="ops">操作</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="d in filteredDetails" :key="d.id">
              <td>{{ d.productName }}</td>
              <td>{{ d.moduleType }}</td>
              <td>{{ d.productSpec }}</td>
              <td>{{ d.blockIndex }}</td>
              <td>{{ d.blockSpec }}</td>
              <td>
                <span v-if="d.orderType === '0'" class="tag green">普通</span>
                <span v-else-if="d.orderType === '1'" class="tag blue">主工单</span>
                <span v-else-if="d.orderType === '2'" class="tag orange">辅工单</span>
              </td>
              <td class="time">{{ d.createTime }}</td>
              <td class="ops">
                <button class="text-btn" @click="openEditDetail(d)">修改</button>
                <button class="text-btn red" @click="deleteDetail(d.id)">删除</button>
              </td>
            </tr>
            <tr v-if="!filteredDetails.length">
              <td colspan="10" class="empty">暂无子项数据</td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>

    <!-- Master Modal -->
    <Teleport to="body">
      <div v-if="showMasterModal" class="modal-overlay">
        <div class="modal-card wide">
          <div class="modal-header">
            <h3>{{ editingMasterId ? '修改项目' : '新增项目' }}</h3>
            <button @click="showMasterModal = false" class="close">&times;</button>
          </div>
          <form @submit.prevent="handleMasterSubmit" class="modal-body">
            <div class="grid-4">
              <div class="f-item"><label>产品名称</label><input v-model="masterForm.productName" required /></div>
              <div class="f-item"><label>电芯数量</label><input v-model="masterForm.cellCount" required /></div>
              <div class="f-item"><label>写PLC型号</label><input v-model="masterForm.plcModel" required /></div>
            </div>
            <div class="modal-foot">
              <button type="button" @click="showMasterModal = false">取消</button>
              <button type="submit" class="prime">保存项目</button>
            </div>
          </form>
        </div>
      </div>
    </Teleport>

    <!-- Detail Modal -->
    <Teleport to="body">
      <div v-if="showDetailModal" class="modal-overlay">
        <div class="modal-card super-wide">
          <div class="modal-header">
            <h3>{{ editingDetailId ? '修改子项' : '新增子项' }}</h3>
            <button @click="showDetailModal = false" class="close">&times;</button>
          </div>
          <form @submit.prevent="handleDetailSubmit" class="modal-body">
            <div class="grid-4">
              <div class="f-item"><label>产品名称</label><input v-model="detailForm.productName" readonly /></div>
              <div class="f-item"><label>大模组类型</label><input v-model="detailForm.moduleType" required /></div>
              <div class="f-item"><label>产品规格</label><input v-model="detailForm.productSpec" required /></div>
              <div class="f-item"><label>模组块序号</label><input v-model="detailForm.blockIndex" required /></div>
              <div class="f-item"><label>模组块规格</label><input v-model="detailForm.blockSpec" required /></div>
              <div class="f-item">
                <label>所属于工单</label>
                <select v-model="detailForm.orderType" required class="sel">
                  <option value="0">0. 普通工单</option>
                  <option value="1">1. 主工单</option>
                  <option value="2">2. 辅工单</option>
                </select>
              </div>
            </div>
            <div class="modal-foot">
              <button type="button" @click="showDetailModal = false">取消</button>
              <button type="submit" class="prime">保存子项</button>
            </div>
          </form>
        </div>
      </div>
    </Teleport>
  </div>
</template>

<style scoped>
.recipe-container { display: flex; flex-direction: column; height: 100%; padding: 12px; gap: 12px; overflow: hidden; background: #0a0e1a; }
.section { flex: 1; display: flex; flex-direction: column; background: rgba(19, 25, 41, 0.8); border: 1px solid rgba(100, 181, 246, 0.15); border-radius: 8px; overflow: hidden; }
.section-header { padding: 10px 16px; background: rgba(100, 181, 246, 0.05); display: flex; justify-content: space-between; align-items: center; border-bottom: 1px solid rgba(100, 181, 246, 0.1); }
.title { font-size: 14px; font-weight: 600; color: #90caf9; }
.table-wrap { flex: 1; overflow: auto; }
.data-table { width: 100%; border-collapse: collapse; font-size: 12px; min-width: 1000px; }
.data-table th { position: sticky; top: 0; background: #1a2235; color: #42a5f5; padding: 10px; text-align: left; border-bottom: 1px solid rgba(100, 181, 246, 0.2); }
.data-table td { padding: 8px 10px; border-bottom: 1px solid rgba(100, 181, 246, 0.05); color: #c8d6e5; }
.data-table tr:hover { background: rgba(66, 165, 245, 0.05); cursor: pointer; }
.data-table tr.selected { background: rgba(66, 165, 245, 0.12); border-left: 3px solid #42a5f5; }
.time { color: #546e7a; font-size: 11px; font-family: monospace; }
.ops { display: flex; gap: 10px; min-width: 140px; }
.text-btn { background: none; border: none; padding: 0; cursor: pointer; font-size: 12px; color: #90caf9; text-decoration: underline; }
.text-btn:hover { color: #42a5f5; }
.text-btn.red { color: #ef5350; }
.text-btn.blue { color: #42a5f5; font-weight: 600; }
.add-btn { background: #1565c0; color: white; border: none; padding: 4px 12px; border-radius: 4px; cursor: pointer; font-size: 12px; }

.tag { padding: 1px 6px; border-radius: 10px; font-size: 10px; }
.tag.green { background: rgba(76, 175, 80, 0.2); color: #81c784; }
.tag.blue { background: rgba(33, 150, 243, 0.2); color: #64b5f6; }
.tag.orange { background: rgba(255, 152, 0, 0.2); color: #ffb74d; }

.modal-overlay { position: fixed; top: 0; left: 0; right: 0; bottom: 0; background: rgba(0,0,0,0.8); display: flex; align-items: center; justify-content: center; z-index: 3000; }
.modal-card { background: #111b2d; border: 1px solid #42a5f5; border-radius: 8px; width: 600px; }
.modal-card.wide { width: 800px; }
.modal-card.super-wide { width: 1000px; }
.modal-header { padding: 12px 20px; border-bottom: 1px solid rgba(100, 181, 246, 0.2); display: flex; justify-content: space-between; }
.modal-header h3 { margin: 0; font-size: 16px; color: #e3f2fd; }
.close { background: none; border: none; color: #546e7a; font-size: 24px; cursor: pointer; }
.modal-body { padding: 20px; }
.grid-4 { display: grid; grid-template-columns: repeat(4, 1fr); gap: 15px; }
.f-item { display: flex; flex-direction: column; gap: 5px; }
.f-item.full { grid-column: span 4; }
.f-item label { font-size: 12px; color: #90caf9; }
.f-item input, .sel { background: #0d1525; border: 1px solid rgba(100, 181, 246, 0.3); border-radius: 4px; padding: 8px; color: white; font-size: 12px; }
.modal-foot { margin-top: 20px; display: flex; justify-content: flex-end; gap: 12px; }
.modal-foot button { padding: 6px 20px; border-radius: 4px; cursor: pointer; border: 1px solid #546e7a; background: none; color: #c8d6e5; }
.modal-foot button.prime { background: #1565c0; border: none; color: white; }
.empty { text-align: center; padding: 30px !important; color: #546e7a; }
</style>
