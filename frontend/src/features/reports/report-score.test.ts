import test from "node:test";
import assert from "node:assert/strict";

import { formatReportScore } from "./report-score";

test("报告分数应保留原始小数精度，不应在前端四舍五入成整数", () => {
  assert.equal(formatReportScore(68.27), "68.27");
  assert.equal(formatReportScore(0.04), "0.04");
});

test("报告分数在整数或单小数场景下应保持紧凑显示", () => {
  assert.equal(formatReportScore(5), "5");
  assert.equal(formatReportScore(5.2), "5.2");
  assert.equal(formatReportScore(0), "0");
});
