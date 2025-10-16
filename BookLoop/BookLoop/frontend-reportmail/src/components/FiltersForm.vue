
<template>
  <el-form label-width="90px" :disabled="loading">
    <el-form-item label="圖表種類">
      <el-select v-model="state.category" placeholder="選擇類型" @change="onCategoryChange">
        <el-option label="折線" value="line" />
        <el-option label="長條" value="bar" />
        <el-option label="圓餅" value="pie" />
      </el-select>
    </el-form-item>

    <el-form-item label="報表定義">
      <el-select v-model="state.reportId" placeholder="選擇報表定義" filterable :loading="defsLoading" @change="$emit('run')">
        <el-option v-for="d in defs" :key="d.id" :label="d.name" :value="d.id" />
      </el-select>
    </el-form-item>

    <el-form-item label="日期區間">
      <el-date-picker v-model="state.range" type="daterange" unlink-panels range-separator="至" start-placeholder="開始" end-placeholder="結束" />
    </el-form-item>

    <el-form-item label="出版社">
      <el-input v-model.number="state.publisherId" placeholder="可留空" />
    </el-form-item>

    <el-form-item>
      <el-button type="primary" :loading="loading" @click="$emit('run')">查詢</el-button>
      <el-button @click="reset">重置</el-button>
    </el-form-item>
  </el-form>
</template>

<script setup lang="ts">
import { onMounted, ref, watch } from 'vue'
import { storeToRefs } from 'pinia'
import { useReportsStore } from '../stores/reports'

const props = defineProps<{ loading: boolean }>()
const emit = defineEmits(['run'])
const store = useReportsStore()
const { defs } = storeToRefs(store)
const defsLoading = ref(false)

const state = store.filters

async function loadDefs() {
  defsLoading.value = true
  try { await store.fetchDefinitions() } finally { defsLoading.value = false }
}

function onCategoryChange() {
  store.activeDef = null
  state.reportId = null
  loadDefs()
}

function reset() {
  store.resetFilters()
}

onMounted(loadDefs)

watch(() => state.reportId, (val) => {
  const found = defs.value.find(d => d.id === val)
  store.activeDef = found ?? null
})
</script>
