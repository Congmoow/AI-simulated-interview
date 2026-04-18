export function formatReportScore(score: number) {
  if (!Number.isFinite(score)) {
    return "--";
  }

  return score.toString();
}
