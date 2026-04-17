export function buildResourceMeta({
  difficulty,
  duration,
  readingTime,
}: {
  difficulty?: string | null;
  duration?: string | null;
  readingTime?: string | null;
}) {
  const items = [
    { label: "难度", value: difficulty },
    { label: "时长", value: duration },
    { label: "阅读时长", value: readingTime },
  ];

  return items.filter(
    (item): item is { label: string; value: string } =>
      typeof item.value === "string" && item.value.trim().length > 0,
  );
}
