<template>
    <div class="rm-wrap">
        <!-- 控制列：三區塊 -->
        <div class="row g-3 mb-3">
            <div class="col-4" v-for="sec in sections" :key="sec.kind">
                <label class="form-label">{{ sec.label }}定義</label>
                <select class="form-select" v-model="sec.model.reportId" @change="onChange(sec.kind)">
                    <option :value="0">（預設）</option>
                    <option v-for="d in defsByKind(sec.kind)" :key="d.id" :value="d.id">{{ d.name }}</option>
                </select>
                <div class="btns">
                    <button class="btn btn-outline-primary btn-sm" @click="editDef(sec.kind)">編輯</button>
                    <button class="btn btn-outline-danger btn-sm" @click="deleteDef(sec.kind)">刪除</button>
                    <button class="btn btn-success btn-sm" @click="exportChart(sec.kind)">匯出</button>
                </div>
            </div>
        </div>

        <!-- 三張圖 -->
        <div class="row g-4">
            <div class="col-4">
                <ChartPanel :title="titleOf('line')" :labels="result('line').labels" :series="result('line').data" :loading="loading" :height="220" />
            </div>
            <div class="col-4">
                <ChartPanel :title="titleOf('bar')" :labels="result('bar').labels" :series="result('bar').data" :loading="loading" :height="220" />
            </div>
            <div class="col-4">
                <ChartPanel :title="titleOf('pie')" :labels="result('pie').labels" :series="result('pie').data" :loading="loading" :height="220" />
            </div>
        </div>

        <!-- 匯出（沿用你後端端點；按鈕觸發內部 confirm） -->
        <div class="mt-3">
            <ExportDialog :disabled="!canExport" :payload="exportPayload" />
        </div>
    </div>
</template>

<script setup lang="ts">
    import { ref, computed } from 'vue'
    import { useReportsStore } from './stores/reports'
    import ChartPanel from './components/ChartPanel.vue'
    import ExportDialog from './components/ExportDialog.vue'

    const store = useReportsStore()
    const loading = ref(false)

    const sections = [
        { kind: 'line' as const, label: '折線', model: store.filters },
        { kind: 'bar' as const, label: '長條', model: store.filters },
        { kind: 'pie' as const, label: '圓餅', model: store.filters }
    ]

    function defsByKind(kind: 'line' | 'bar' | 'pie') {
        return store.defs.filter(d => d.category === kind)
    }
    function titleOf(kind: 'line' | 'bar' | 'pie') {
        const preset = { line: '預設折線圖', bar: '預設長條圖', pie: '預設圓餅圖' }
        return preset[kind]
    }
    function result(_kind: 'line' | 'bar' | 'pie') {
        // 目前共用一份 result；若你要拆成三份，這裡改對應即可
        return store.result
    }
    async function onChange(_kind: 'line' | 'bar' | 'pie') {
        loading.value = true
        try { await store.fetchData() } finally { loading.value = false }
    }
    function editDef(kind: 'line' | 'bar' | 'pie') { /* TODO: 帶去你的編輯頁 */ }
    function deleteDef(kind: 'line' | 'bar' | 'pie') { /* TODO: 呼叫刪除後重刷 */ }
    function exportChart(kind: 'line' | 'bar' | 'pie') { /* TODO: 若需帶 kind 可加到 payload */ }

    const canExport = computed(() => (store.result.labels?.length ?? 0) > 0)
    const exportPayload = computed(() => ({
        reportId: store.filters.reportId ?? 0,
        kind: store.filters.category,
        filters: store.filters
    }))
</script>

<style scoped>
    /* 容器與格線（自製 12 欄） */
    .rm-wrap {
        max-width: 1320px;
        margin: 0 auto;
        padding: 16px;
    }

    .row {
        display: flex;
        flex-wrap: wrap;
    }

    .g-3 > * {
        padding: 12px;
    }
    /* 間距對齊 g-3 */
    .g-4 > * {
        padding: 16px;
    }
    /* 間距對齊 g-4 */
    .mb-3 {
        margin-bottom: 16px;
    }

    .mt-3 {
        margin-top: 16px;
    }

    .col-4 {
        width: 100%;
    }

    @media (min-width: 992px) {
        .col-4 {
            width: 33.3333%;
        }
    }

    /* 表單與按鈕（外觀貼近原本） */
    .form-label {
        display: block;
        margin-bottom: 6px;
        font-weight: 600;
    }

    .form-select {
        width: 100%;
        height: 36px;
        padding: 6px 10px;
        border: 1px solid #ced4da;
        border-radius: 6px;
        background: #fff;
        outline: none;
    }

        .form-select:focus {
            border-color: #86b7fe;
            box-shadow: 0 0 0 2px rgba(13,110,253,.15);
        }

    .btns {
        margin-top: 8px;
        display: flex;
        gap: 8px;
    }

    .btn {
        display: inline-flex;
        align-items: center;
        justify-content: center;
        height: 30px;
        padding: 0 10px;
        border-radius: 6px;
        font-size: 13px;
        cursor: pointer;
        border: 1px solid transparent;
        background: #f8f9fa;
    }

    .btn-sm {
        height: 28px;
        padding: 0 8px;
        font-size: 12px;
    }

    .btn-outline-primary {
        border-color: #0d6efd;
        color: #0d6efd;
        background: #fff;
    }

        .btn-outline-primary:hover {
            background: #0d6efd;
            color: #fff;
        }

    .btn-outline-danger {
        border-color: #dc3545;
        color: #dc3545;
        background: #fff;
    }

        .btn-outline-danger:hover {
            background: #dc3545;
            color: #fff;
        }

    .btn-success {
        background: #198754;
        color: #fff;
        border-color: #198754;
    }

        .btn-success:hover {
            filter: brightness(0.95);
        }
</style>
