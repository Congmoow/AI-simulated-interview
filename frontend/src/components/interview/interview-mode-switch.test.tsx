import test from "node:test";
import assert from "node:assert/strict";
import { renderToStaticMarkup } from "react-dom/server";

import { InterviewModeSwitch } from "./interview-mode-switch";

test("面试模式切换应渲染三段式滑块，并将高亮定位到当前选中项", () => {
  const html = renderToStaticMarkup(
    <InterviewModeSwitch
      onChange={() => {}}
      options={[
        { label: "轻松", value: "friendly" },
        { label: "标准", value: "standard" },
        { label: "高压", value: "stress" },
      ]}
      value="standard"
    />,
  );

  assert.match(html, /interview-mode-switch/);
  assert.match(html, /interview-mode-switch__indicator/);
  assert.match(html, /translateX\(100%\)/);
});
