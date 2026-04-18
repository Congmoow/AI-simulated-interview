import test from "node:test";
import assert from "node:assert/strict";
import { renderToStaticMarkup } from "react-dom/server";

import { InterviewComposer } from "./interview-composer";

test("输入区不应再带有聊天区与输入区之间的上边分隔线", () => {
  const html = renderToStaticMarkup(
    <InterviewComposer
      canRestoreDraft={false}
      canSubmit={false}
      disabled={false}
      onChange={() => {}}
      onSubmit={() => {}}
      placeholder="请输入"
      sendLabel="发送回答"
      value=""
    />,
  );

  assert.doesNotMatch(html, /class="shrink-0 border-t/);
});
