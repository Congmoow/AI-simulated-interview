import test from "node:test";
import assert from "node:assert/strict";

import {
  buildInterviewTimelineMessages,
  hasPersistedPendingAnswer,
} from "./message-flow.ts";

const detail = {
  interviewId: "interview-1",
  positionCode: "java-backend",
  positionName: "Java 后端工程师",
  interviewMode: "standard",
  status: "in_progress",
  currentRound: 2,
  totalRounds: 5,
  createdAt: "2026-04-16T11:00:00.000Z",
  messages: [
    {
      id: "m1",
      role: "assistant",
      messageType: "opening",
      content: "请先介绍一个最相关的后端项目。",
      relatedQuestionId: "q1",
      sequence: 1,
      metadata: {},
      createdAt: "2026-04-16T11:00:01.000Z",
    },
    {
      id: "m2",
      role: "user",
      messageType: "answer",
      content: "我主要负责订单和库存一致性。",
      relatedQuestionId: "q1",
      sequence: 2,
      metadata: {},
      createdAt: "2026-04-16T11:00:10.000Z",
    },
    {
      id: "m3",
      role: "assistant",
      messageType: "follow_up",
      content: "你刚才提到库存一致性，具体怎么做补偿和对账？",
      relatedQuestionId: "q1",
      sequence: 3,
      metadata: {},
      createdAt: "2026-04-16T11:00:20.000Z",
    },
    {
      id: "m4",
      role: "assistant",
      messageType: "question",
      content: "下面切到事务设计，你通常如何选择 Spring 事务传播行为？",
      relatedQuestionId: "q2",
      sequence: 4,
      metadata: {},
      createdAt: "2026-04-16T11:01:00.000Z",
    },
  ],
  rounds: [],
};

test("消息流应直接映射为聊天时间线，并标记当前面试官消息", () => {
  const messages = buildInterviewTimelineMessages(detail);

  assert.equal(messages.length, 4);
  assert.equal(messages[0]?.kind, "assistant");
  assert.equal(messages[0]?.title, undefined);
  assert.equal(messages[0]?.body, "请先介绍一个最相关的后端项目。");
  assert.equal(messages[2]?.kind, "assistant");
  assert.equal(messages[2]?.title, undefined);
  assert.equal(messages[3]?.kind, "assistant");
  assert.equal(messages[3]?.title, undefined);
  assert.equal(messages[3]?.isCurrent, true);
});

test("当用户消息已在服务端落库时，应识别为已持久化", () => {
  assert.equal(hasPersistedPendingAnswer(detail, "我主要负责订单和库存一致性。"), true);
  assert.equal(hasPersistedPendingAnswer(detail, "另一条回答"), false);
});
