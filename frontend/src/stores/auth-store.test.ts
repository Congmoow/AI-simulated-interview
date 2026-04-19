import test from "node:test";
import assert from "node:assert/strict";

import { useAuthStore } from "./auth-store.ts";

type StorageMock = {
  getItem: (key: string) => string | null;
  setItem: (key: string, value: string) => void;
  removeItem: (key: string) => void;
  clear: () => void;
  key: (index: number) => string | null;
  readonly length: number;
  resetCounts: () => void;
  readonly setItemCalls: number;
};

function createStorageMock(): StorageMock {
  const values = new Map<string, string>();
  let setItemCalls = 0;

  return {
    getItem: (key) => values.get(key) ?? null,
    setItem: (key, value) => {
      setItemCalls += 1;
      values.set(key, value);
    },
    removeItem: (key) => {
      values.delete(key);
    },
    clear: () => {
      values.clear();
    },
    key: (index) => Array.from(values.keys())[index] ?? null,
    get length() {
      return values.size;
    },
    resetCounts: () => {
      setItemCalls = 0;
    },
    get setItemCalls() {
      return setItemCalls;
    },
  };
}

function installWindowMock() {
  const localStorage = createStorageMock();
  const sessionStorage = createStorageMock();

  Object.defineProperty(globalThis, "window", {
    configurable: true,
    value: {
      localStorage,
      sessionStorage,
    },
  });

  return {
    localStorage,
    sessionStorage,
    cleanup() {
      Reflect.deleteProperty(globalThis, "window");
    },
  };
}

function resetAuthStore() {
  useAuthStore.setState(useAuthStore.getInitialState());
}

test("认证状态默认应立即可用且不依赖持久化恢复", () => {
  resetAuthStore();

  const state = useAuthStore.getState();

  assert.equal(state.hydrated, true);
  assert.equal(state.accessToken, null);
  assert.equal(state.refreshToken, null);
  assert.equal(state.user, null);
});

test("登录态更新应只保存在内存中且不再写入本地存储", () => {
  resetAuthStore();
  const { localStorage, sessionStorage, cleanup } = installWindowMock();

  try {
    localStorage.setItem("ai-interview-auth", "legacy-token");
    sessionStorage.setItem("ai-interview-auth", "legacy-token");
    localStorage.setItem("interview-draft:test", "keep-me");
    localStorage.resetCounts();
    sessionStorage.resetCounts();

    useAuthStore.getState().setSession({
      accessToken: "access-token",
      refreshToken: "refresh-token",
      expiresIn: 3600,
      user: {
        id: "user-1",
        username: "tester",
        email: "tester@example.com",
        role: "user",
      },
    });

    assert.equal(useAuthStore.getState().accessToken, "access-token");
    assert.equal(localStorage.setItemCalls, 0);
    assert.equal(sessionStorage.setItemCalls, 0);
    assert.equal(localStorage.getItem("ai-interview-auth"), null);
    assert.equal(sessionStorage.getItem("ai-interview-auth"), null);
    assert.equal(localStorage.getItem("interview-draft:test"), "keep-me");

    useAuthStore.getState().clearSession();

    assert.equal(useAuthStore.getState().accessToken, null);
    assert.equal(useAuthStore.getState().refreshToken, null);
    assert.equal(useAuthStore.getState().user, null);
  } finally {
    cleanup();
  }
});
