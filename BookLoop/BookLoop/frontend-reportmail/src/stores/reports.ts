
import { defineStore } from 'pinia'
import axios from 'axios'

export type ReportDef = { id: number; name: string; category: 'line'|'bar'|'pie'; description?: string }

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
    async fetchDefinitions() {
      const { category } = this.filters
      const { data } = await axios.get<ReportDef[]>(`/api/reportmail/reports/definitions?category=${category}`)
      this.defs = data
      if (!this.activeDef && data.length) {
        this.activeDef = data[0]
        this.filters.reportId = data[0].id
      }
    },
    async fetchData() {
      if (!this.filters.reportId) { this.result = { labels: [], data: [] }; return }
      const body: any = {
        dateFrom: this.filters.range?.[0] ?? null,
        dateTo: this.filters.range?.[1] ?? null,
        publisherId: this.filters.publisherId ?? null
      }
      const { data } = await axios.post(`/api/reportmail/reports/${this.filters.reportId}/query`, body)
      this.result = {
        labels: Array.isArray(data?.labels) ? data.labels : [],
        data: Array.isArray(data?.data) ? data.data : []
      }
    },
    resetFilters() {
      this.filters.range = []
      this.filters.publisherId = null
    }
  }
})
