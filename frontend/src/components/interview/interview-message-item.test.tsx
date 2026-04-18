import test from "node:test";
import assert from "node:assert/strict";
import { renderToStaticMarkup } from "react-dom/server";

import { InterviewMessageItem } from "./interview-message-item";

test("面试官消息应带有位于左上角并指向头像的气泡尖角", () => {
  const html = renderToStaticMarkup(
    <InterviewMessageItem
      message={{
        id: "assistant-1",
        kind: "assistant",
        body: "请结合真实项目回答。",
      }}
    />,
  );

  assert.match(html, /interview-bubble-tail--assistant/);
  assert.match(html, /interview-bubble-tail--assistant-top/);
});

test("用户消息应保留聊天气泡尾巴样式", () => {
  const html = renderToStaticMarkup(
    <InterviewMessageItem
      message={{
        id: "user-1",
        kind: "user",
        body: "你好",
        timestamp: "16:20",
        status: "sent",
      }}
    />,
  );

  assert.match(
    html,
    /interview-bubble-shell interview-bubble-shell--user max-w-\[48%\] self-end/,
  );
  assert.doesNotMatch(html, /interview-bubble-tail--user/);
  assert.match(html, /<article class="inline-block w-fit max-w-full/);
});
