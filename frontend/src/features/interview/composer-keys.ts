/**
 * 判断 InterviewComposer 的 keydown 事件是否应该触发提交。
 *
 * 关注两类反例，避免回到 1033 行 client 组件里复测：
 * - 键盘 auto-repeat：操作系统在长按时反复派发 keydown，会导致同一次按键触发多次提交。
 * - 修饰键组合不完整：仅按 Enter（无 Ctrl/Meta）应让 textarea 走默认换行行为。
 */
export type ComposerKeyEvent = {
  key: string;
  shiftKey: boolean;
  ctrlKey: boolean;
  metaKey: boolean;
  repeat: boolean;
};

export function shouldSubmitOnKeyDown(
  event: ComposerKeyEvent,
  canSubmit: boolean,
): boolean {
  if (event.repeat) return false;
  if (event.key !== "Enter") return false;
  if (event.shiftKey) return false;
  if (!event.ctrlKey && !event.metaKey) return false;
  return canSubmit;
}
