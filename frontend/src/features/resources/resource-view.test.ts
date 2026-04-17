import test from "node:test";
import assert from "node:assert/strict";

import { buildResourceMeta } from "./resource-view.ts";

test("应按顺序组装学习资源的展示元信息", () => {
  const result = buildResourceMeta({
    difficulty: "中级",
    duration: "3 小时",
    readingTime: "20 分钟",
  });

  assert.deepEqual(result, [
    { label: "难度", value: "中级" },
    { label: "时长", value: "3 小时" },
    { label: "阅读时长", value: "20 分钟" },
  ]);
});

test("应过滤空白的学习资源展示元信息", () => {
  const result = buildResourceMeta({
    difficulty: " ",
    duration: null,
    readingTime: undefined,
  });

  assert.deepEqual(result, []);
});
