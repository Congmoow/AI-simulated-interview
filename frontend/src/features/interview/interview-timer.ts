export function shouldAdvanceElapsedTimer(status?: string | null) {
  return status === "in_progress";
}
