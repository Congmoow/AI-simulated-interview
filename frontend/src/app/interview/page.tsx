import { Suspense } from "react";

import { InterviewClient } from "@/app/interview/interview-client";
import { LoadingState } from "@/components/ui/state-panel";

export default function InterviewPage() {
  return (
    <Suspense fallback={<LoadingState label="正在准备面试工作台..." />}>
      <InterviewClient />
    </Suspense>
  );
}
