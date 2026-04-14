import type { PositionDetail, PositionSummary } from "@/types/api";
import { getRequest } from "@/services/request";

export function getPositions() {
  return getRequest<PositionSummary[]>("/api/v1/positions");
}

export function getPositionDetail(code: string) {
  return getRequest<PositionDetail>(`/api/v1/positions/${code}`);
}
