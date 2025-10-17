<template>
  <el-popconfirm :title="disabled ? '沒有資料可匯出' : '匯出目前結果？'" @confirm="doExport">
    <template #reference>
      <el-button type="success" :disabled="disabled">匯出 Excel / PDF</el-button>
    </template>
  </el-popconfirm>
</template>

<script setup lang="ts">
import axios from 'axios'

declare global { interface Window { ReportPageConfig?: any } }
const RPC = (typeof window !== 'undefined' ? window.ReportPageConfig : {}) || {}
const API = RPC.api || RPC.apis || {}

const props = defineProps<{ disabled: boolean, payload: any }>()

// 這裡先示範送 Excel，如要 PDF 可改成 API.sendPdf
async function doExport() {
  if (props.disabled) return
  await axios.post(API.sendExcel ?? '/ReportMail/ReportExport/SendExcel', props.payload)
}
</script>
