// src/utils/nudgeStore.ts

// === 使用者作用域：讓每個使用者有自己的 snooze/ignore 狀態 ===
// 從 JWT 取 mid（優先）或 email，拿不到就 'anon'
export function getUserScope():string{
    const raw = localStorage.getItem('token');
    if(raw){
        try{
            const payload =JSON.parse(atob(raw.split('.')[1]));
            return String(payload.mid || payload.email || 'anon');
        }
        catch{}
    }
    return 'anon';
}

const SEEN=`mail-nudge-seen`;
const SNOOZE=`mail-nudge-snooze`;
const IGNORE=`mail-nudge-ignore`;

export function markSeen(rid: number) {
    const KEY_SEEN=`${SEEN}-${getUserScope()}`;
    // Debug 1: 看看 scope 到底拿到了什麼
    console.log(`[Critical Debug] 當前 Scope: "${getUserScope()}"`);
    console.log(`[Critical Debug] 完整的 Key: "${KEY_SEEN}"`);
    const seenStr = localStorage.getItem(KEY_SEEN) || '[]';
    const seenArray = JSON.parse(seenStr);
    const seenSet = new Set<number>(seenArray);
    seenSet.add(rid);
    //從 Set 轉回 Array ([...]) 再從 Array 轉成 String (JSON.stringify) 存入 localStorage
    localStorage.setItem(KEY_SEEN, JSON.stringify([...seenSet]));
    console.log(`[Critical Debug] 存入後的內容:`, localStorage.getItem(KEY_SEEN));
}

export function hasSeen(rid: number): boolean {
    const KEY_SEEN=`${SEEN}-${getUserScope()}`;
    const seenStr = localStorage.getItem(KEY_SEEN) || '[]';
    const seenArray = JSON.parse(seenStr);
    const seenSet = new Set<number>(seenArray);
    return seenSet.has(rid);
}

export function setSnooze(ms: number) {
    const KEY_SNOOZE = `${SNOOZE}-${getUserScope()}`;
    localStorage.setItem(KEY_SNOOZE, String(Date.now() + ms));
}

export function snoozed(): boolean {
    const KEY_SNOOZE = `${SNOOZE}-${getUserScope()}`;
    const until = Number(localStorage.getItem(KEY_SNOOZE) || "0");
    const active = until > Date.now();
    
    // 如果時間已經過了，主動清理掉這個 Key，保持 LocalStorage 乾淨
    if (!active && until !== 0) {
        localStorage.removeItem(KEY_SNOOZE);
    }
    
    return active;
}

export function setIgnore(rid: number) {
    const KEY_IGNORE = `${IGNORE}-${getUserScope()}`;
    localStorage.setItem(KEY_IGNORE, String(rid));
}

export function isIgnored(rid: number): boolean {
    const KEY_IGNORE = `${IGNORE}-${getUserScope()}`;
    const ignoredRid = localStorage.getItem(KEY_IGNORE);
    return ignoredRid === String(rid);
}

export function clearSnooze() {
    const KEY_SNOOZE = `${SNOOZE}-${getUserScope()}`;
    localStorage.removeItem(KEY_SNOOZE);
}

export function clearIgnore() {
    const KEY_IGNORE = `${IGNORE}-${getUserScope()}`;
    localStorage.removeItem(KEY_IGNORE);
}
