import type { DashboardInsightsDetail } from "@/types/api";
import { getRequest } from "@/services/request";

export function getDashboardInsights() {
  return getRequest<DashboardInsightsDetail>("/api/v1/dashboard/insights");
}
