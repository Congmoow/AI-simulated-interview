import { redirect } from "next/navigation";

export default async function LegacyReportDetailPage({
  params,
}: {
  params: Promise<{ interviewId: string }>;
}) {
  const { interviewId } = await params;
  redirect(`/reports/${interviewId}`);
}
