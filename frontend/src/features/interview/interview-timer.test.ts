import test from "node:test";
import assert from "node:assert/strict";

import { shouldAdvanceElapsedTimer } from "./interview-timer";

test("只有进行中的面试才应继续推进计时器", () => {
  assert.equal(shouldAdvanceElapsedTimer("in_progress"), true);
  assert.equal(shouldAdvanceElapsedTimer("completed"), false);
  assert.equal(shouldAdvanceElapsedTimer("generating_report"), false);
  assert.equal(shouldAdvanceElapsedTimer("report_failed"), false);
});
