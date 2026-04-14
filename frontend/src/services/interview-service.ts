import type {
  CreateInterviewPayload,
  FinishInterviewPayload,
  InterviewCurrentDetail,
  InterviewHistoryItem,
  PagedResult,
  SubmitAnswerPayload,
} from "@/types/api";
import { getRequest, postRequest } from "@/services/request";

export function createInterview(payload: {
  positionCode: string;
  interviewMode: string;
  questionTypes: string[];
  roundCount: number;
}) {
  return postRequest<CreateInterviewPayload>("/api/v1/interviews", payload);
}

export function getInterview(interviewId: string) {
  return getRequest<InterviewCurrentDetail>(`/api/v1/interviews/${interviewId}`);
}

export function submitAnswer(
  interviewId: string,
  payload: { answer: string; inputMode: string; transcription?: string },
) {
  return postRequest<SubmitAnswerPayload>(
    `/api/v1/interviews/${interviewId}/answers`,
    payload,
  );
}

export function finishInterview(interviewId: string) {
  return postRequest<FinishInterviewPayload>(
    `/api/v1/interviews/${interviewId}/finish`,
  );
}

export function getInterviewHistory(params?: {
  position?: string;
  status?: string;
  page?: number;
  pageSize?: number;
}) {
  return getRequest<PagedResult<InterviewHistoryItem>>("/api/v1/interviews", params);
}
