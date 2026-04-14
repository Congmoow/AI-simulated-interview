import Link from "next/link";

export default function HomePage() {
  return (
    <section className="surface-card flex min-h-[70vh] flex-col justify-between gap-10 px-6 py-8 lg:px-10 lg:py-10">
      <div className="max-w-[760px] space-y-5">
        <span className="section-label">AI Interview MVP</span>
        <h1 className="display-title">把“模拟面试”真正做成一条能跑通的训练闭环。</h1>
        <p className="text-caption max-w-[620px] text-[length:var(--token-font-size-lg)]">
          这一版已经按文档约束拆成 Next.js 前端、ASP.NET Core 业务后端和 FastAPI AI
          服务，并围绕登录、岗位选择、问答推进、报告与历史做最小可演示闭环。
        </p>
      </div>
      <div className="flex flex-wrap gap-3">
        <Link className="primary-button" href="/login">
          进入登录页
        </Link>
        <Link className="secondary-button" href="/dashboard">
          查看仪表盘
        </Link>
      </div>
    </section>
  );
}
