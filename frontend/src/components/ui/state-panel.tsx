import { Card } from "@/components/ui/card";

export function LoadingState({ label = "正在加载内容..." }: { label?: string }) {
  return (
    <Card className="state-card">
      <div className="space-y-3">
        <div className="skeleton-bar h-3 w-40" />
        <div className="skeleton-bar h-3 w-56" />
      </div>
      <p className="text-caption">{label}</p>
    </Card>
  );
}

export function EmptyState({
  title,
  description,
}: {
  title: string;
  description: string;
}) {
  return (
    <Card className="state-card">
      <div className="space-y-3">
        <p className="section-title">{title}</p>
        <p className="text-caption max-w-[520px]">{description}</p>
      </div>
    </Card>
  );
}

export function ErrorState({
  title = "请求失败",
  description,
}: {
  title?: string;
  description: string;
}) {
  return (
    <Card className="state-card">
      <div className="space-y-3">
        <p className="section-title">{title}</p>
        <p className="text-caption max-w-[520px]">{description}</p>
      </div>
    </Card>
  );
}
