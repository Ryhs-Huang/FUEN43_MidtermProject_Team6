import { defineStore } from 'pinia'
import axios from 'axios'

export type ReportDef = { id: number; name: string; category: 'line' | 'bar' | 'pie' }

declare global { interface Window { ReportPageConfig?: any } }
const RPC = (typeof window !== 'undefined' ? window.ReportPageConfig : {}) || {}
const API = RPC.api || RPC.apis || {}
const BASE = RPC.base || '/ReportMail'

type Kind = 'line' | 'bar' | 'pie'
type Series = { labels: (string | number)[], data: number[] }

export const useReportsStore = defineStore('reports', {
    state: () => ({
        defs: [] as ReportDef[],          // 全部定義，依 category 分類
        resultPie: { labels: [], data: [] } as Series,
        resultLine: { labels: [], data: [] } as Series,
        resultBar: { labels: [], data: [] } as Series,
        // 三塊各自的目前選擇；0=預設
        selected: { pie: 0, line: 0, bar: 0 } as Record<Kind, number>,
        range: [] as any[],               // 共用日期區間（需要你再加 UI 就能用）
        publisherId: null as number | null
    }),
    getters: {
        defsOf: (s) => (k: Kind) => s.defs.filter(d => d.category === k)
    },
    actions: {
        async fetchDefinitions(kind?: 'line' | 'bar' | 'pie') {
            const kinds = kind ? [kind] : ['pie', 'line', 'bar'] as const
            for (const k of kinds) {
                const preset = { id: 0, name: `（預設）${k.toUpperCase()}`, category: k }
                try {
                    const listUrl = API.list ?? '/ReportMail/ReportDefinitions/List' // ← 用 server 給的
                    const { data } = await axios.get(listUrl, { params: { category: k } })
                    const list = Array.isArray(data) ? data : []
                    this.defs = this.defs.filter(d => d.category !== k).concat([preset, ...list])
                } catch {
                    this.defs = this.defs.filter(d => d.category !== k).concat([preset])
                }
            }
        
        },

        // 依 kind 取資料（會自動挑對的 API）
        async fetchData(kind: Kind) {
            const reportId = this.selected[kind] ?? 0
            const from = this.range?.[0] ?? null
            const to = this.range?.[1] ?? null

            // 預設圖
            if (!reportId || reportId === 0) {
                let data: any
                if (kind === 'pie') ({ data } = await axios.get(API.pie ?? '/ReportMail/Reports/Pie', { params: { from, to } }))
                if (kind === 'line') ({ data } = await axios.get(API.line ?? '/ReportMail/Reports/Line', { params: { from, to, granularity: 'day' } }))
                if (kind === 'bar') ({ data } = await axios.get(API.bar ?? '/ReportMail/Reports/Bar', { params: { from, to, top: 10 } }))
                this._apply(kind, { labels: data?.labels ?? [], data: data?.data ?? [] })
                return
            }

            // 自訂圖：DefinitionPayload → PreviewDraft
            const { data: def } = await axios.get(API.defPayload ?? '/ReportMail/ReportDefinitions/DefinitionPayload', { params: { id: reportId } })
            const body: any = {
                Category: def?.Category ?? kind,
                BaseKind: def?.BaseKind ?? 'sales',
                Filters: [] as any[]
            }
            if (from || to) body.Filters.push({ FieldName: 'OrderDate', Operator: 'between', DataType: 'date', ValueJson: JSON.stringify({ from, to }) })
            if (this.publisherId) body.Filters.push({ FieldName: 'PublisherId', Operator: 'eq', DataType: 'number', ValueJson: JSON.stringify(this.publisherId) })

            const { data: preview } = await axios.post(API.preview ?? '/ReportMail/ReportPreview/PreviewDraft', body)
            this._apply(kind, { labels: Array.isArray(preview?.labels) ? preview.labels : [], data: Array.isArray(preview?.data) ? preview.data : [] })
        },

        _apply(kind: Kind, series: Series) {
            if (kind === 'pie') this.resultPie = series
            if (kind === 'line') this.resultLine = series
            if (kind === 'bar') this.resultBar = series
        }
    }
})
