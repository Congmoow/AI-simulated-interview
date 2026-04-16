"use client";

import { useEffect, useMemo, useRef } from "react";
import {
  InterviewMessageItem,
  type InterviewTimelineMessage,
} from "@/components/interview/interview-message-item";
import type { InterviewSystemAction } from "@/components/interview/interview-system-notice";

interface InterviewMessageListProps {
  messages: InterviewTimelineMessage[];
  onAction?: (action: InterviewSystemAction) => void;
}

export function InterviewMessageList({
  messages,
  onAction,
}: InterviewMessageListProps) {
  const containerRef = useRef<HTMLDivElement | null>(null);
  const lastMessageId = useMemo(
    () => messages[messages.length - 1]?.id ?? "",
    [messages],
  );

  useEffect(() => {
    const container = containerRef.current;
    if (!container) {
      return;
    }

    container.scrollTo({
      top: container.scrollHeight,
      behavior: "smooth",
    });
  }, [lastMessageId]);

  return (
    <div
      aria-live="polite"
      className="min-h-0 flex-1 overflow-y-auto rounded-[28px] bg-[rgba(241,245,249,0.9)] px-3"
      ref={containerRef}
    >
      <div className="mx-auto flex w-full max-w-[920px] flex-col gap-4 pb-8 pt-4">
        {messages.map((message) => (
          <InterviewMessageItem
            key={message.id}
            message={message}
            onAction={onAction}
          />
        ))}
      </div>
    </div>
  );
}
