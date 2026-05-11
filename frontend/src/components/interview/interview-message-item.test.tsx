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

test("面试官消息应展示统一的 persona 名称与首字头像", () => {
  const html = renderToStaticMarkup(
    <InterviewMessageItem
      message={{
        id: "assistant-persona",
        kind: "assistant",
        body: "请展开讲一下你的角色。",
      }}
    />,
  );

  assert.match(html, /面试官 陈航/);
  assert.match(html, />陈</);
  assert.doesNotMatch(html, />HR</);
});

test("面试官消息应根据 messageType 写入 data 属性与 variant class", () => {
  const followUpHtml = renderToStaticMarkup(
    <InterviewMessageItem
      message={{
        id: "assistant-followup",
        kind: "assistant",
        body: "刚才提到的库存一致性，能再具体一些吗？",
        messageType: "follow_up",
      }}
    />,
  );

  assert.match(followUpHtml, /data-message-type="follow_up"/);
  assert.match(followUpHtml, /interview-bubble--follow-up/);

  const openingHtml = renderToStaticMarkup(
    <InterviewMessageItem
      message={{
        id: "assistant-opening",
        kind: "assistant",
        body: "你好，先聊聊你的项目。",
        messageType: "opening",
      }}
    />,
  );

  assert.match(openingHtml, /data-message-type="opening"/);
  assert.match(openingHtml, /interview-bubble--opening/);
});

test("当 messageType 为 follow_up 时应渲染追问标签", () => {
  const html = renderToStaticMarkup(
    <InterviewMessageItem
      message={{
        id: "assistant-followup-tag",
        kind: "assistant",
        body: "再具体一些。",
        messageType: "follow_up",
      }}
    />,
  );

  assert.match(html, /data-testid="interview-message-tag"/);
  assert.match(html, />追问</);
});
