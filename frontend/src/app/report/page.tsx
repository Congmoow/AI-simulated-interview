import Link from "next/link";

export default function ReportIndexPage() {
  return (
    <section className="surface-card space-y-5 px-6 py-8 lg:px-8">
      <span className="section-label">报告中心</span>
      <h2 className="section-title">选择一场已完成的面试进入报告详情。</h2>
      <p className="text-caption max-w-[720px]">
        报告页会展示综合得分、能力维度、学习建议和训练计划。你可以先去历史页挑选一场面试，或在当前面试结束后自动跳转到该页面。
      </p>
      <div className="flex gap-3">
        <Link className="primary-button" href="/history">
          去历史页选择
        </Link>
        <Link className="secondary-button" href="/interview">
          返回面试页
        </Link>
      </div>
    </section>
  );
}
