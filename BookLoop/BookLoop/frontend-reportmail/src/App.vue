<template>
    <div class="page-wrap">
        <div class="grid">
            <!-- 圓餅 -->
            <ChartPanel class="panel" title="圓餅圖" kind="pie"
                        :labels="pie.labels" :series="pie.data" :loading="loading" :height="420">
                <template #toolbar>
                    <div class="toolbar-wrap">
                        <select v-model="store.selected.pie" class="select" @change="run('pie')">
                            <option v-for="d in defs('pie')" :key="d.id" :value="d.id">{{ d.name }}</option>
                        </select>
                        <div class="btns">
                            <ExportDialog :payload="payload('pie')" :requireEmail="true" />
                            <button class="btn btn-blue" @click="createDef('pie')">新增</button>
                            <button class="btn btn-outline-blue" @click="editDef('pie')">編輯</button>
                            <button class="btn btn-outline-red" @click="deleteDef('pie')">刪除</button>
                        </div>
                    </div>
                </template>
            </ChartPanel>

            <!-- 折線 -->
            <ChartPanel class="panel" title="折線圖" kind="line"
                        :labels="line.labels" :series="line.data" :loading="loading" :height="220">
                <template #toolbar>
                    <div class="toolbar-wrap">
                        <select v-model="store.selected.line" class="select" @change="run('line')">
                            <option v-for="d in defs('line')" :key="d.id" :value="d.id">{{ d.name }}</option>
                        </select>
                        <div class="btns">
                            <ExportDialog :payload="payload('line')" :requireEmail="true" />
                            <button class="btn btn-blue" @click="createDef('line')">新增</button>
                            <button class="btn btn-outline-blue" @click="editDef('line')">編輯</button>
                            <button class="btn btn-outline-red" @click="deleteDef('line')">刪除</button>
                        </div>
                    </div>
                </template>
            </ChartPanel>

            <!-- 長條 -->
            <ChartPanel class="panel" title="長條圖" kind="bar"
                        :labels="bar.labels" :series="bar.data" :loading="loading" :height="220">
                <template #toolbar>
                    <div class="toolbar-wrap">
                        <select v-model="store.selected.bar" class="select" @change="run('bar')">
                            <option v-for="d in defs('bar')" :key="d.id" :value="d.id">{{ d.name }}</option>
                        </select>
                        <div class="btns">
                            <ExportDialog :payload="payload('bar')" :requireEmail="true" />
                            <button class="btn btn-blue" @click="createDef('bar')">新增</button>
                            <button class="btn btn-outline-blue" @click="editDef('bar')">編輯</button>
                            <button class="btn btn-outline-red" @click="deleteDef('bar')">刪除</button>
                        </div>
                    </div>
                </template>
            </ChartPanel>
        </div>
    </div>
</template>

<script setup lang="ts">
    import { computed, onMounted, ref } from 'vue'
    import axios from 'axios'
    import ChartPanel from './components/ChartPanel.vue'
    import ExportDialog from './components/ExportDialog.vue'
    import { useReportsStore } from './stores/reports'

    declare global { interface Window { ReportPageConfig?: any } }
    const RPC = (typeof window !== 'undefined' ? window.ReportPageConfig : {}) || {}
    const URLS = RPC.urls || {}

    const store = useReportsStore()
    const loading = ref(false)

    const pie = computed(() => store.resultPie)
    const line = computed(() => store.resultLine)
    const bar = computed(() => store.resultBar)
    const defs = (k: 'line' | 'bar' | 'pie') => store.defsOf(k)

    function payload(kind: 'line' | 'bar' | 'pie') {
        return { reportId: store.selected[kind] ?? 0, kind, filters: { range: store.range, publisherId: store.publisherId } }
    }

    async function run(kind: 'line' | 'bar' | 'pie') {
        loading.value = true
        try { await store.fetchData(kind) } finally { loading.value = false }
    }

    function createDef(kind: 'line' | 'bar' | 'pie') {
        const u = URLS?.create ?? '/ReportMail/ReportDefinitions/Create'
        window.location.href = `${u}?category=${kind}`
    }
    function editDef(kind: 'line' | 'bar' | 'pie') {
        const id = store.selected[kind]
        if (!id) { alert('請先選擇「自訂報表」'); return }
        const base = URLS?.editBase ?? '/ReportMail/ReportDefinitions/Edit'
        window.location.href = `${base}/${id}`
    }
    async function deleteDef(kind: 'line' | 'bar' | 'pie') {
        const id = store.selected[kind]
        if (!id) { alert('請先選擇「自訂報表」'); return }
        if (!confirm('確定要刪除此報表定義？')) return
        const token = document.querySelector<HTMLInputElement>('form#__af__ input[name="__RequestVerificationToken"]')?.value ?? ''
        await axios.post(URLS?.delete ?? '/ReportMail/ReportDefinitions/Delete', new URLSearchParams({ id: String(id), __RequestVerificationToken: token }))
        await store.fetchDefinitions(kind)
        store.selected[kind] = 0
        await run(kind)
    }

    onMounted(async () => {
        await store.fetchDefinitions()        // ① 先抓清單（含預設）
        await run('pie'); await run('line'); await run('bar') // ② 載入資料
    })
</script>

<style scoped>
    .page-wrap {
        max-width: 1200px;
        margin: 0 auto;
        padding: 12px 16px 24px;
    }

    .grid {
        display: grid;
        grid-template-columns: 1.1fr 1fr;
        grid-template-rows: 1fr 1fr;
        gap: 16px;
        grid-template-areas:
            "left right-top"
            "left right-bottom";
    }

    .panel:nth-child(1) {
        grid-area: left;
    }

    .panel:nth-child(2) {
        grid-area: right-top;
    }

    .panel:nth-child(3) {
        grid-area: right-bottom;
    }

    .toolbar-wrap {
        display: flex;
        align-items: center;
        gap: 8px;
    }

    .select {
        height: 32px;
        min-width: 320px;
        padding: 4px 10px;
        border: 1px solid #d0d5dd;
        border-radius: 6px;
        background: #fff;
        outline: none;
    }

        .select:focus {
            border-color: #86b7fe;
            box-shadow: 0 0 0 2px rgba(13,110,253,.15);
        }

    .btns {
        display: flex;
        gap: 6px;
    }

    .btn {
        height: 30px;
        padding: 0 10px;
        border-radius: 6px;
        font-size: 13px;
        cursor: pointer;
        border: 1px solid transparent;
        background: #f8f9fa;
    }

    .btn-success {
        background: #28a745;
        color: #fff;
        border-color: #28a745;
    }

    .btn-blue {
        background: #0d6efd;
        color: #fff;
        border-color: #0d6efd;
    }

    .btn-outline-blue {
        background: #fff;
        color: #0d6efd;
        border-color: #0d6efd;
    }

        .btn-outline-blue:hover {
            background: #0d6efd;
            color: #fff;
        }

    .btn-outline-red {
        background: #fff;
        color: #dc3545;
        border-color: #dc3545;
    }

        .btn-outline-red:hover {
            background: #dc3545;
            color: #fff;
        }

    @media (max-width: 1100px) {
        .grid {
            grid-template-columns: 1fr;
            grid-template-rows: auto;
            grid-template-areas: "left" "right-top" "right-bottom";
        }

        .select {
            min-width: 200px;
        }
    }
</style>
