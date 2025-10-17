<template>
    <div class="export">
        <button class="btn btn-success" @click="open = true">匯出</button>

        <div v-if="open" class="rm-modal-backdrop" @click.self="open=false">
            <div class="rm-modal">
                <h3 class="rm-modal-title">匯出報表</h3>

                <div class="form-row">
                    <label class="form-label">格式</label>
                    <div class="radio-row">
                        <label><input type="radio" value="xlsx" v-model="format"> Excel</label>
                        <label><input type="radio" value="pdf" v-model="format"> PDF</label>
                    </div>
                </div>

                <div class="form-row">
                    <label class="form-label">寄送 Email</label>
                    <input class="input" type="email" v-model.trim="email" placeholder="name@example.com">
                    <div v-if="email && !validEmail" class="hint-error">Email 格式不正確</div>
                </div>

                <div class="actions">
                    <button class="btn" @click="open=false">取消</button>
                    <button class="btn btn-success" :disabled="submitting || (requireEmail && !validEmail)" @click="submit">
                        {{ submitting ? '送出中...' : '送出' }}
                    </button>
                </div>
            </div>
        </div>
    </div>
</template>

<script setup lang="ts">
    import { ref, computed } from 'vue'
    import axios from 'axios'

    declare global { interface Window { ReportPageConfig?: any } }
    const RPC = (typeof window !== 'undefined' ? window.ReportPageConfig : {}) || {}
    const API = RPC.api || RPC.apis || {}

    const props = defineProps<{
        disabled?: boolean,
        payload: { reportId: number, kind: 'pie' | 'line' | 'bar', filters: any },
        requireEmail?: boolean
    }>()

    const open = ref(false)
    const format = ref<'xlsx' | 'pdf'>('xlsx')
    const email = ref('')
    const submitting = ref(false)

    const validEmail = computed(() => /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email.value))

    async function submit() {
        if (props.disabled) return
        submitting.value = true
        try {
            const body: any = {
                ...props.payload,
                // 同時帶幾個常見名稱，讓後端其中之一對到
                Email: email.value || null,
                ReceiverEmail: email.value || null,
                ToEmail: email.value || null
            }
            if (format.value === 'xlsx') {
                await axios.post(API.sendExcel ?? '/ReportMail/ReportExport/SendExcel', body)
            } else {
                await axios.post(API.sendPdf ?? '/ReportMail/ReportExport/SendPdf', body)
            }
            open.value = false
            alert('已送出匯出')
        } finally {
            submitting.value = false
        }
    }

</script>

<style scoped>
    .btn {
        height: 30px;
        padding: 0 10px;
        border-radius: 6px;
        font-size: 13px;
        cursor: pointer;
        border: 1px solid #d0d5dd;
        background: #f8f9fa;
    }

    .btn-success {
        background: #28a745;
        color: #fff;
        border-color: #28a745;
    }

    /* 改成 rm- 前綴，避免撞到 Bootstrap 的 .modal */
    .rm-modal-backdrop {
        position: fixed;
        inset: 0;
        background: rgba(0,0,0,.35);
        display: flex;
        align-items: center;
        justify-content: center;
        z-index: 2000;
    }

    .rm-modal {
        width: 420px;
        max-width: 92vw;
        background: #fff;
        border-radius: 8px;
        box-shadow: 0 10px 30px rgba(0,0,0,.25);
        padding: 16px;
    }

    .rm-modal-title {
        margin: 0 0 12px;
        font-size: 18px;
        font-weight: 700;
    }

    .form-row {
        margin-bottom: 12px;
    }

    .form-label {
        display: block;
        font-weight: 600;
        margin-bottom: 6px;
    }

    .radio-row {
        display: flex;
        gap: 16px;
    }

    .input {
        width: 100%;
        height: 34px;
        padding: 6px 10px;
        border: 1px solid #d0d5dd;
        border-radius: 6px;
        outline: none;
    }

        .input:focus {
            border-color: #86b7fe;
            box-shadow: 0 0 0 2px rgba(13,110,253,.15);
        }

    .hint-error {
        margin-top: 6px;
        color: #dc3545;
        font-size: 12px;
    }

    .actions {
        display: flex;
        justify-content: flex-end;
        gap: 8px;
        margin-top: 8px;
    }
</style>
