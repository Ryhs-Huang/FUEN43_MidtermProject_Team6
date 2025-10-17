import { defineStore } from 'pinia'
import axios from 'axios'

export type ReportDef = { id: number; name: string; category: 'line'|'bar'|'pie'; description?: string }

declare global { interface Window { ReportPageConfig?: any } }
const RPC = (typeof window !== 'undefined' ? window.ReportPageConfig : {}) || {}
// 同時支援 api / apis 兩種欄位名稱
const API = RPC.api || RPC.apis || {}

export const useReportsStore = defineStore('reports', {
  state: () => ({
    defs: [] as ReportDef[],
    activeDef: null as ReportDef | null,
    result: { labels: [] as string[], data: [] as number[] },
    filters: {
      category: 'bar' as 'line'|'bar'|'pie',
      reportId: null as number | null,
      range: [] as any[],
      publisherId: null as number | null
    }
  }),
  actions: {
    // 取清單：嘗試呼叫 /ReportMail/ReportDefinitions/List，失敗則只放預設選項
    async fetchDefinitions() {
      const cat = this.filters.category
      const preset: ReportDef[] = [{ id: 0, name: `（預設）${cat.toUpperCase()}`, category: cat }]
      try {
        const { data } = await axios.get(`/ReportMail/ReportDefinitions/List`, { params: { category: cat } })
        const list = Array.isArray(data) ? data as ReportDef[] : []
        this.defs = [...preset, ...list]
      } catch {
        this.defs = preset
      }
      if (!this.activeDef && this.defs.length) {
        this.activeDef = this.defs[0]
        this.filters.reportId = this.defs[0].id
      }
    },

    async fetchData() {
      const cat = this.filters.category
      const reportId = this.filters.reportId ?? 0
      const from = this.filters.range?.[0] ?? null
      const to   = this.filters.range?.[1] ?? null

      // ① 預設圖（reportId=0）→ 直接打既有端點
      if (!reportId || reportId === 0) {
        if (cat === 'line') {
          const { data } = await axios.get(API.line ?? '/ReportMail/Reports/Line', { params: { from, to, granularity: 'day' } })
          this.result = { labels: data?.labels ?? [], data: data?.data ?? [] }
          return
        }
        if (cat === 'bar') {
          const { data } = await axios.get(API.bar ?? '/ReportMail/Reports/Bar', { params: { from, to, top: 10 } })
          this.result = { labels: data?.labels ?? [], data: data?.data ?? [] }
          return
        }
        if (cat === 'pie') {
          const { data } = await axios.get(API.pie ?? '/ReportMail/Reports/Pie', { params: { from, to } })
          this.result = { labels: data?.labels ?? [], data: data?.data ?? [] }
          return
        }
      }

      // ② 自訂定義 → DefinitionPayload → PreviewDraft
      const { data: def } = await axios.get(API.defPayload ?? '/ReportMail/ReportDefinitions/DefinitionPayload', {
        params: { id: reportId }
      })

      const body: any = {
        Category: def?.Category ?? cat,       // line/bar/pie
        BaseKind: def?.BaseKind ?? 'sales',   // 視你的後端定義
        Filters: [] as any[]
      }

      // 日期
      if (from || to) {
        body.Filters.push({
          FieldName: 'OrderDate',
          Operator: 'between',
          DataType: 'date',
          ValueJson: JSON.stringify({ from, to })
        })
      }

      // 出版社（選填）
      if (this.filters.publisherId) {
        body.Filters.push({
          FieldName: 'PublisherId',
          Operator: 'eq',
          DataType: 'number',
          ValueJson: JSON.stringify(this.filters.publisherId)
        })
      }

      const { data: preview } = await axios.post(API.preview ?? '/ReportMail/ReportPreview/PreviewDraft', body)
      this.result = {
        labels: Array.isArray(preview?.labels) ? preview.labels : [],
        data: Array.isArray(preview?.data) ? preview.data : []
      }
    },

    resetFilters() {
      this.filters.range = []
      this.filters.publisherId = null
    }
  }
})
