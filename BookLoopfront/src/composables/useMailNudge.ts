import { ref } from "vue";
import { getUnopenedMail, MailNudgeResponse } from "@/api/mail";
import { hasSeen } from "@/utils/nudgeStore";

export function useMailNudge() {
    const item = ref<MailNudgeResponse | null>(null);

    async function fetchOnce() {
        try {
            const res = await getUnopenedMail();
            // 只做最基本的「有沒有資料」判斷
            item.value = res.hasItem ? res : null;
        } catch (err: any) {
            item.value = null;
        }
    }
    return { item, fetchOnce };
}
