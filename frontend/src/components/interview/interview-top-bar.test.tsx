import test from "node:test";
import assert from "node:assert/strict";
import { renderToStaticMarkup } from "react-dom/server";

import { InterviewTopBar } from "./interview-top-bar";

test("顶部栏不应再显示轮次信息，计时应位于结束按钮左侧", () => {
  const html = renderToStaticMarkup(
    <InterviewTopBar
      elapsedLabel="13:55"
      finishDisabled={false}
      finishLabel="已结束"
      onFinish={() => {}}
      positionName="Java后端开发工程师"
    />,
  );

  assert.doesNotMatch(html, /第 3 \/ 5 轮/);
  assert.match(html, /计时 13:55/);
  assert.match(html, /class="flex items-center gap-3"/);
});
