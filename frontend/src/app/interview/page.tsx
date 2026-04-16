import { Suspense } from "react";

import { InterviewClientV2 } from "@/app/interview/interview-client-v2";
import { LoadingState } from "@/components/ui/state-panel";

export default function InterviewPage() {
  return (
    <Suspense fallback={<LoadingState label="正在准备面试工作台..." />}>
      <InterviewClientV2 />
    </Suspense>
  );
}
