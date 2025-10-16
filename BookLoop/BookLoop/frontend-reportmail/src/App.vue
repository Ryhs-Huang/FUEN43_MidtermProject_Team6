
<template>
  <div class="p-4">
    <el-row :gutter="16">
      <el-col :span="6">
        <FiltersForm @run="runQuery" :loading="loading" />
        <el-divider />
        <ExportDialog :disabled="lastResult.labels.length === 0" :payload="exportPayload" />
      </el-col>
      <el-col :span="18">
        <ChartPanel :title="chartTitle" :labels="lastResult.labels" :series="lastResult.data" :loading="loading" />
      </el-col>
    </el-row>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import { useReportsStore } from './stores/reports'
import FiltersForm from './components/FiltersForm.vue'
import ChartPanel from './components/ChartPanel.vue'
import ExportDialog from './components/ExportDialog.vue'

const store = useReportsStore()
const loading = ref(false)

const lastResult = computed(() => store.result)
const chartTitle = computed(() => store.activeDef?.name ?? '預設圖表')

const exportPayload = computed(() => ({
  reportId: store.activeDef?.id ?? null,
  kind: store.activeDef?.category ?? 'line',
  filters: store.filters
}))

async function runQuery() {
  loading.value = true
  try { await store.fetchData() } finally { loading.value = false }
}
</script>

<style scoped>
.p-4 { padding: 16px; }
</style>
