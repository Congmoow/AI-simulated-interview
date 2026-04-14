import type { KnowledgeDocumentItem, PagedResult } from "@/types/api";
import { getRequest, postRequest } from "@/services/request";
import { httpClient } from "@/services/http";

export function createQuestion(payload: {
  positionCode: string;
  type: string;
  difficulty: string;
  title: string;
  content: string;
  tags: string[];
  idealAnswer: string;
  scoringRubric: Record<string, number>;
}) {
  return postRequest("/api/v1/admin/questions", payload);
}

export async function uploadKnowledgeDocument(payload: {
  title: string;
  positionCode: string;
  tags: string[];
  file: File;
}) {
  const formData = new FormData();
  formData.append("title", payload.title);
  formData.append("positionCode", payload.positionCode);
  payload.tags.forEach((tag) => formData.append("tags", tag));
  formData.append("file", payload.file);

  const response = await httpClient.post("/api/v1/admin/knowledge/documents", formData, {
    headers: {
      "Content-Type": "multipart/form-data",
    },
  });

  return response.data.data;
}

export function getKnowledgeDocuments(params?: {
  positionCode?: string;
  status?: string;
  page?: number;
  pageSize?: number;
}) {
  return getRequest<PagedResult<KnowledgeDocumentItem>>(
    "/api/v1/admin/knowledge/documents",
    params,
  );
}
