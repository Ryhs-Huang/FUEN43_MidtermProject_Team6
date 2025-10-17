<template>
    <div class="card">
        <div class="card-header">
            <div class="title">{{ title }}</div>
            <div class="toolbar">
                <slot name="toolbar"></slot>
            </div>
        </div>
        <div class="card-body">
            <div ref="chartEl" :style="{ height: heightPx }"></div>
        </div>
    </div>
</template>

<script setup lang="ts">
    import * as echarts from 'echarts'
    import { onMounted, onBeforeUnmount, ref, watch, computed } from 'vue'

    type Kind = 'line' | 'bar' | 'pie'
    const props = defineProps<{
        title: string
        kind: Kind
        labels: (string | number)[]
        series: number[]
        loading: boolean
        height?: number
    }>()

    const heightPx = computed(() => ((props.height ?? 260) + 'px'))
    const chartEl = ref<HTMLDivElement | null>(null)
    let chart: echarts.ECharts | null = null

    function buildOption(): echarts.EChartsOption {
        const hasData = Array.isArray(props.labels) && props.labels.length > 0 &&
            props.series?.some(v => v != null && v !== 0)

        if (props.kind === 'pie') {
            const data = (props.labels || []).map((name, i) => ({ name, value: props.series?.[i] ?? 0 }))
            return {
                tooltip: { trigger: 'item' },
                legend: { top: 8, left: 'center' },
                series: [{ type: 'pie', radius: ['55%', '80%'], center: ['50%', '58%'], data, label: { show: false }, itemStyle: { borderColor: '#fff', borderWidth: 2 } }],
                animation: false,
                graphic: [{
                    id: 'no-data', type: 'text', left: 'center', top: 'middle', silent: true,
                    invisible: !!hasData,
                    style: { text: '無資料', fontSize: 14, fill: '#666' }
                }]
            }
        }

        return {
            tooltip: { trigger: 'axis' },
            grid: { top: 32, left: 40, right: 16, bottom: 28 },
            xAxis: { type: 'category', data: props.labels || [] },
            yAxis: { type: 'value' },
            series: [{ type: props.kind, data: props.series || [], smooth: props.kind === 'line' }],
            animation: false,
            graphic: [{
                id: 'no-data', type: 'text', left: 'center', top: 'middle', silent: true,
                invisible: !!hasData,
                style: { text: '無資料', fontSize: 14, fill: '#666' }
            }]
        }
    }

    function render() { if (chart) chart.setOption(buildOption(), { notMerge: true }) }
    function resize() { chart?.resize() }

    onMounted(() => { if (chartEl.value) { chart = echarts.init(chartEl.value); render(); window.addEventListener('resize', resize) } })
    onBeforeUnmount(() => { window.removeEventListener('resize', resize); chart?.dispose(); chart = null })
    watch(() => [props.kind, props.labels, props.series, props.loading], render, { deep: true })
</script>

<style scoped>
    .card {
        background: #fff;
        border: 1px solid #e5e7eb;
        border-radius: 6px;
        box-shadow: 0 1px 3px rgba(0,0,0,.06);
    }

    .card-header {
        display: flex;
        align-items: center;
        justify-content: space-between;
        padding: 8px 12px;
        border-bottom: 1px solid #e5e7eb;
    }

    .title {
        font-weight: 600;
    }

    .card-body {
        padding: 12px;
    }

    .toolbar {
        display: flex;
        align-items: center;
        gap: 8px;
    }
</style>
