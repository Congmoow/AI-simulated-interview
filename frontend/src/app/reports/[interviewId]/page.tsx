import { ReportClient } from "@/app/report/[interviewId]/report-client";

export default async function ReportDetailPage({
  params,
}: {
  params: Promise<{ interviewId: string }>;
}) {
  const { interviewId } = await params;
  return <ReportClient interviewId={interviewId} />;
}
