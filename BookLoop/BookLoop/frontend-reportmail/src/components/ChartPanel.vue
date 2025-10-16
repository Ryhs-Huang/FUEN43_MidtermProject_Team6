
<template>
  <el-card shadow="hover" style="height: 520px">
    <template #header>
      <div class="card-header">
        <span>{{ title }}</span>
      </div>
    </template>
    <div ref="chartEl" style="height: 440px"></div>
  </el-card>
</template>

<script setup lang="ts">
import * as echarts from 'echarts'
import { onMounted, onBeforeUnmount, ref, watch } from 'vue'

const props = defineProps<{ title: string, labels: string[], series: number[], loading: boolean }>()

const chartEl = ref<HTMLDivElement | null>(null)
let chart: echarts.ECharts | null = null

function render() {
  if (!chart) return
  const hasData = Array.isArray(props.labels) && props.labels.length > 0 && props.series?.some(v => v != null && v !== 0)

  chart.setOption({
    title: { show: false },
    tooltip: { trigger: 'axis' },
    xAxis: { type: 'category', data: props.labels || [] },
    yAxis: { type: 'value' },
    series: [{ type: 'bar', data: props.series || [] }],
    animation: false,
    graphic: [{
      id: 'no-data', type: 'text', left: 'center', top: 'middle', silent: true,
      invisible: !!hasData,
      style: { text: '無資料', fontSize: 16, fill: '#666' }
    }]
  }, { notMerge: true })
}

onMounted(() => {
  if (chartEl.value) {
    chart = echarts.init(chartEl.value)
    render()
    window.addEventListener('resize', resize)
  }
})

onBeforeUnmount(() => {
  window.removeEventListener('resize', resize)
  chart?.dispose(); chart = null
})

watch(() => [props.labels, props.series, props.loading], render, { deep: true })

function resize() { chart?.resize() }
</script>
