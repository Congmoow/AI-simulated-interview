export interface ApiResponse<T> {
  success: boolean;
  code: number;
  message: string;
  data: T;
  errors?: Array<{
    field?: string;
    message: string;
  }>;
  timestamp: string;
}

export interface PagedResult<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface CurrentUser {
  id: string;
  username: string;
  email: string;
  phone?: string | null;
  role: string;
  avatarUrl?: string | null;
  targetPosition?: {
    code: string;
    name: string;
  } | null;
  createdAt: string;
}

export interface LoginPayload {
  accessToken: string;
  tokenType: string;
  expiresIn: number;
  refreshToken: string;
  user: CurrentUser;
}

export interface PositionSummary {
  code: string;
  name: string;
  description: string;
  questionCount: number;
  tags: string[];
}

export interface PositionDetail extends PositionSummary {
  questionTypes: Array<{
    type: string;
    name: string;
    count: number;
  }>;
  difficulty: string[];
}

export interface CreateInterviewPayload {
  interviewId: string;
  positionCode: string;
  positionName: string;
  interviewMode: string;
  status: string;
  currentRound: number;
  totalRounds: number;
  createdAt: string;
  firstQuestion: {
    questionId: string;
    title: string;
    type: string;
    roundNumber: number;
  };
}

export interface InterviewCurrentDetail {
  interviewId: string;
  positionCode: string;
  positionName: string;
  interviewMode: string;
  status: string;
  currentRound: number;
  totalRounds: number;
  createdAt: string;
  rounds: Array<{
    roundNumber: number;
    question: {
      questionId: string;
      title: string;
      type: string;
    };
    userAnswer?: string | null;
    aiFollowUp?: string | null;
    answeredAt?: string | null;
  }>;
}

export interface SubmitAnswerPayload {
  roundNumber: number;
  interviewStatus: string;
  nextRoundAvailable: boolean;
  aiResponse: {
    type: string;
    content: string;
    suggestions: string[];
  };
}

export interface FinishInterviewPayload {
  interviewId: string;
  status: string;
  reportId: string;
  estimatedTime: number;
}

export interface InterviewHistoryItem {
  interviewId: string;
  positionName: string;
  interviewMode: string;
  status: string;
  totalScore?: number | null;
  roundCount: number;
  duration: number;
  createdAt: string;
  completedAt?: string | null;
}

export interface ReportDetail {
  reportId: string;
  interviewId: string;
  positionName: string;
  overallScore: number;
  dimensionScores: Record<string, { score: number; weight: number }>;
  strengths: string[];
  weaknesses: string[];
  learningSuggestions: string[];
  trainingPlan: Array<Record<string, unknown>>;
  generatedAt: string;
}

export interface GrowthDetail {
  summary: {
    totalInterviews: number;
    averageScore: number;
    scoreChange: number;
    strongestDimension?: string | null;
    weakestDimension?: string | null;
  };
  trend: Array<{
    date: string;
    overallScore: number;
    dimensions: Record<string, number>;
  }>;
}

export interface DashboardInsightsDetail {
  overview: {
    totalInterviews: number;
    totalReports: number;
    recent30DayInterviews: number;
    strengthsCount: number;
    weaknessesCount: number;
    trend: "up" | "flat" | "down";
    updatedAt?: string | null;
  };
  scope: {
    scopeStrategy: string;
    actualScope: string;
    targetPositionCode?: string | null;
    targetPositionName?: string | null;
    fallbackTriggered: boolean;
    fallbackReason?: string | null;
    reportCount: number;
  };
  strengths: Array<{
    key: string;
    title: string;
    description: string;
    evidenceCount: number;
    lastSeenAt: string;
    evidenceSamples: string[];
    sources: DashboardInsightSource[];
  }>;
  weaknesses: Array<{
    key: string;
    title: string;
    description: string;
    evidenceCount: number;
    lastSeenAt: string;
    typicalBehaviors: string[];
    suggestion: string;
    sources: DashboardInsightSource[];
  }>;
  abilityDimensions6: Array<{
    key: string;
    name: string;
    score: number;
    sourceDimensions: string[];
  }>;
  recentTrend: Array<{
    date: string;
    score: number;
    interviewId: string;
    reportId: string;
  }>;
  nextActions: string[];
}

export interface DashboardInsightSource {
  interviewId: string;
  reportId: string;
  generatedAt: string;
  positionName: string;
}

export interface ResourceRecommendation {
  resourceId: string;
  title: string;
  type: string;
  provider?: string | null;
  url?: string | null;
  targetDimensions: string[];
  difficulty?: string | null;
  duration?: string | null;
  readingTime?: string | null;
  rating?: number | null;
  matchScore: number;
}

export interface TrainingPlan {
  planId: string;
  weeks: number;
  dailyCommitment: string;
  goals: string[];
  schedule: Array<Record<string, unknown>>;
  milestones: Array<Record<string, unknown>>;
  generatedAt: string;
}

export interface KnowledgeDocumentItem {
  documentId: string;
  title: string;
  positionCode: string;
  status: string;
  chunkCount: number;
  fileSize: string;
  createdAt: string;
  processedAt?: string | null;
}
