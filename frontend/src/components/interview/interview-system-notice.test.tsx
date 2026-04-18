import test from "node:test";
import assert from "node:assert/strict";
import { renderToStaticMarkup } from "react-dom/server";

import { InterviewSystemNotice } from "./interview-system-notice";

test("报告已生成提示应支持去掉外围卡片包裹效果的行内样式", () => {
  const html = renderToStaticMarkup(
    <InterviewSystemNotice
      actionKey="view-report"
      actionLabel="查看报告"
      body="报告已生成"
      displayStyle="inline"
      tone="success"
    />,
  );

  assert.match(html, /interview-system-notice-inline/);
  assert.doesNotMatch(html, /max-w-\[760px\]/);
  assert.match(html, />报告已生成</);
});
