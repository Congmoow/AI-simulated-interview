import test from "node:test";
import assert from "node:assert/strict";

import { buildRealtimeInterviewMessages } from "./realtime-message-flow.ts";

test("面试官分析时应追加分析中占位气泡", () => {
  const messages = buildRealtimeInterviewMessages({
    pendingAnswer: {
      id: "pending-1",
      text: "我会先描述系统边界，再解释缓存一致性策略。",
      timestamp: "2026-04-17T09:00:00.000Z",
      status: "evaluating",
    },
    pendingAnswerAlreadyPersisted: false,
    assistantThinking: true,
  });

  assert.equal(messages.length, 2);
  assert.equal(messages[0]?.kind, "user");
  assert.equal(messages[1]?.kind, "assistant");
  assert.equal(messages[1]?.body, "分析中");
  assert.equal(messages[1]?.isThinking, true);
});

test("用户消息已落库时仍应继续显示面试官分析气泡", () => {
  const messages = buildRealtimeInterviewMessages({
    pendingAnswer: {
      id: "pending-2",
      text: "我会补充库存补偿和对账机制。",
      timestamp: "2026-04-17T09:01:00.000Z",
      status: "evaluating",
    },
    pendingAnswerAlreadyPersisted: true,
    assistantThinking: true,
  });

  assert.equal(messages.length, 1);
  assert.equal(messages[0]?.kind, "assistant");
  assert.equal(messages[0]?.body, "分析中");
  assert.equal(messages[0]?.isThinking, true);
});
