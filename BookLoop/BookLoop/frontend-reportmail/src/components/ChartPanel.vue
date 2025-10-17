<template>
    <div class="card">
        <div class="card-header">{{ title }}</div>
        <div ref="chartEl" :style="{ height: (height ?? 220) + 'px' }"></div>
    </div>
</template>

<script setup lang="ts">
    import * as echarts from 'echarts'
    import { onMounted, onBeforeUnmount, ref, watch } from 'vue'

    const props = defineProps<{ title: string, labels: string[], series: number[], loading: boolean, height?: number }>()
    const chartEl = ref<HTMLDivElement | null>(null)
    let chart: echarts.ECharts | null = null

    function render() {
        if (!chart) return
        const hasData = Array.isArray(props.labels) && props.labels.length > 0 && props.series?.some(v => v != null && v !== 0)
        chart.setOption({
            tooltip: { trigger: 'axis' },
            xAxis: { type: 'category', data: props.labels || [] },
            yAxis: { type: 'value' },
            series: [{ type: 'bar', data: props.series || [] }],
            animation: false,
            graphic: [{
                id: 'no-data', type: 'text', left: 'center', top: 'middle', silent: true,
                invisible: !!hasData,
                style: { text: '無資料', fontSize: 14, fill: '#666' }
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
    onBeforeUnmount(() => { window.removeEventListener('resize', resize); chart?.dispose(); chart = null })
    watch(() => [props.labels, props.series, props.loading], render, { deep: true })
    function resize() { chart?.resize() }
</script>

<style scoped>
    .card {
        background: #fff;
        border: 1px solid rgba(0,0,0,.06);
        border-radius: 8px;
        box-shadow: 0 1px 6px rgba(0,0,0,.04);
    }

    .card-header {
        padding: 8px 12px;
        border-bottom: 1px solid rgba(0,0,0,.06);
        font-weight: 600;
    }
</style>
