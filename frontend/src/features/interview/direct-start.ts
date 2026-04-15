export type InterviewEntryMode =
  | "choose-position"
  | "auto-create"
  | "interview";

const DEFAULT_QUESTION_TYPES = ["technical", "project", "scenario"] as const;
const DEFAULT_ROUND_COUNT = 5;

export function getInterviewEntryMode(params: {
  interviewId: string | null;
  positionCode: string | null;
}): InterviewEntryMode {
  if (params.interviewId) {
    return "interview";
  }

  if (params.positionCode) {
    return "auto-create";
  }

  return "choose-position";
}

export function buildCreateInterviewPayload(
  positionCode: string,
  interviewMode: string,
) {
  return {
    positionCode,
    interviewMode,
    questionTypes: [...DEFAULT_QUESTION_TYPES],
    roundCount: DEFAULT_ROUND_COUNT,
  };
}

export function getInterviewTargetUrl(interviewId: string) {
  return `/interview?interviewId=${encodeURIComponent(interviewId)}`;
}

export function createSingleFlight<Args extends unknown[], Result>(
  runner: (...args: Args) => Promise<Result>,
) {
  let inFlight: Promise<Result> | null = null;

  return (...args: Args) => {
    if (inFlight) {
      return inFlight;
    }

    inFlight = runner(...args).finally(() => {
      inFlight = null;
    });

    return inFlight;
  };
}
