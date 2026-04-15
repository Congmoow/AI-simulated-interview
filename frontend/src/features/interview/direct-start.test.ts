import test from "node:test";
import assert from "node:assert/strict";

import {
  buildCreateInterviewPayload,
  createSingleFlight,
  getInterviewEntryMode,
  getInterviewTargetUrl,
} from "./direct-start.ts";

test("当存在 interviewId 时应直接进入正式面试态", () => {
  assert.equal(
    getInterviewEntryMode({ interviewId: "interview-1", positionCode: "web-frontend" }),
    "interview",
  );
});

test("当只有 positionCode 时应进入直达创建态而不是旧中间态", () => {
  assert.equal(
    getInterviewEntryMode({ interviewId: null, positionCode: "web-frontend" }),
    "auto-create",
  );
});

test("当没有 interviewId 和 positionCode 时应展示岗位卡片列表", () => {
  assert.equal(
    getInterviewEntryMode({ interviewId: null, positionCode: null }),
    "choose-position",
  );
});

test("创建面试请求应复用现有默认参数", () => {
  assert.deepEqual(buildCreateInterviewPayload("web-frontend", "stress"), {
    positionCode: "web-frontend",
    interviewMode: "stress",
    questionTypes: ["technical", "project", "scenario"],
    roundCount: 5,
  });
});

test("创建成功后应跳转到基于 interviewId 的正式面试页", () => {
  assert.equal(
    getInterviewTargetUrl("interview-1"),
    "/interview?interviewId=interview-1",
  );
});

test("快速重复触发时只能执行一次创建动作", async () => {
  let callCount = 0;
  const wrapped = createSingleFlight(async (positionCode: string) => {
    callCount += 1;
    await new Promise((resolve) => setTimeout(resolve, 10));
    return positionCode;
  });

  const [firstResult, secondResult] = await Promise.all([
    wrapped("web-frontend"),
    wrapped("web-frontend"),
  ]);

  assert.equal(callCount, 1);
  assert.equal(firstResult, "web-frontend");
  assert.equal(secondResult, "web-frontend");
});
