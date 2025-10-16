
<template>
  <el-popconfirm :title="disabled ? '沒有資料可匯出' : '匯出目前結果？'" @confirm="doExport">
    <template #reference>
      <el-button type="success" :disabled="disabled">匯出 Excel / PDF</el-button>
    </template>
  </el-popconfirm>
</template>

<script setup lang="ts">
import axios from 'axios'

const props = defineProps<{ disabled: boolean, payload: any }>()

async function doExport() {
  if (props.disabled) return
  await axios.post('/ReportMail/Reports/Export', props.payload)
}
</script>
