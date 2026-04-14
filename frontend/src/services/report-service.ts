import type {
  GrowthDetail,
  ReportDetail,
  ResourceRecommendation,
  TrainingPlan,
} from "@/types/api";
import { getRequest } from "@/services/request";

export function getReport(interviewId: string) {
  return getRequest<ReportDetail>(`/api/v1/reports/${interviewId}`);
}

export function getGrowth(params?: {
  position?: string;
  timeRange?: string;
}) {
  return getRequest<GrowthDetail>("/api/v1/reports/growth", params);
}

export function getResources(params?: {
  dimensions?: string;
  position?: string;
  limit?: number;
}) {
  return getRequest<ResourceRecommendation[]>(
    "/api/v1/recommendations/resources",
    params,
  );
}

export function getTrainingPlan(params?: {
  interviewId?: string;
  weeks?: number;
}) {
  return getRequest<TrainingPlan>("/api/v1/recommendations/training-plan", params);
}
