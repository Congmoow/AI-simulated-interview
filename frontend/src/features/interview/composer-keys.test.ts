import { test } from "node:test";
import assert from "node:assert/strict";

import { shouldSubmitOnKeyDown, type ComposerKeyEvent } from "./composer-keys";

function buildEvent(overrides: Partial<ComposerKeyEvent> = {}): ComposerKeyEvent {
  return {
    key: "Enter",
    shiftKey: false,
    ctrlKey: true,
    metaKey: false,
    repeat: false,
    ...overrides,
  };
}

test("Ctrl+Enter 单次按下、可提交时应触发提交", () => {
  assert.equal(shouldSubmitOnKeyDown(buildEvent(), true), true);
});

test("Cmd+Enter 单次按下、可提交时应触发提交", () => {
  assert.equal(
    shouldSubmitOnKeyDown(buildEvent({ ctrlKey: false, metaKey: true }), true),
    true,
  );
});

test("event.repeat=true 时不应触发提交（防止键盘 auto-repeat 重复发送）", () => {
  assert.equal(shouldSubmitOnKeyDown(buildEvent({ repeat: true }), true), false);
});

test("Shift+Enter 不应触发提交（保留 textarea 换行）", () => {
  assert.equal(shouldSubmitOnKeyDown(buildEvent({ shiftKey: true }), true), false);
});

test("仅 Enter 不带 Ctrl/Meta 不应触发提交", () => {
  assert.equal(
    shouldSubmitOnKeyDown(buildEvent({ ctrlKey: false, metaKey: false }), true),
    false,
  );
});

test("非 Enter 键不应触发提交", () => {
  assert.equal(shouldSubmitOnKeyDown(buildEvent({ key: "a" }), true), false);
});

test("canSubmit=false 时即便 Ctrl+Enter 也不应触发提交", () => {
  assert.equal(shouldSubmitOnKeyDown(buildEvent(), false), false);
});

test("Ctrl+Shift+Enter 不应触发提交（Shift 优先级高于 Ctrl）", () => {
  assert.equal(shouldSubmitOnKeyDown(buildEvent({ shiftKey: true }), true), false);
});
