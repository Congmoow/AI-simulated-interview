import test from "node:test";
import assert from "node:assert/strict";

type StorageMock = {
  getItem: (key: string) => string | null;
  setItem: (key: string, value: string) => void;
  removeItem: (key: string) => void;
  clear: () => void;
  key: (index: number) => string | null;
  readonly length: number;
};

function createStorageMock(): StorageMock {
  const values = new Map<string, string>();

  return {
    getItem: (key) => values.get(key) ?? null,
    setItem: (key, value) => {
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
  };
}

// Install localStorage mock BEFORE importing the store, because zustand's
// persist middleware captures the storage reference at module load time.
const sharedLocalStorage = createStorageMock();
Object.defineProperty(globalThis, "localStorage", {
  configurable: true,
  value: sharedLocalStorage,
});
Object.defineProperty(globalThis, "window", {
  configurable: true,
  value: { localStorage: sharedLocalStorage, sessionStorage: createStorageMock() },
});

// require() runs synchronously, so the mock is in place when the store initializes.
// eslint-disable-next-line @typescript-eslint/no-require-imports
const { useAuthStore } = require("./auth-store.ts") as typeof import("./auth-store.ts");

function resetAuthStore() {
  useAuthStore.setState(useAuthStore.getInitialState());
}

test("认证状态默认未 hydrate，需等待持久化恢复", () => {
  resetAuthStore();

  const state = useAuthStore.getState();

  assert.equal(state.hydrated, false);
  assert.equal(state.accessToken, null);
  assert.equal(state.refreshToken, null);
  assert.equal(state.user, null);
});

test("登录态更新应持久化到 localStorage", () => {
  resetAuthStore();
  sharedLocalStorage.clear();

  try {
    useAuthStore.getState().setSession({
      accessToken: "access-token",
      refreshToken: "refresh-token",
      expiresIn: 3600,
      user: {
        id: "user-1",
        username: "tester",
        email: "tester@example.com",
        role: "user",
        createdAt: new Date().toISOString(),
      },
    });

    assert.equal(useAuthStore.getState().accessToken, "access-token");

    const stored = sharedLocalStorage.getItem("ai-interview-auth");
    assert.notEqual(stored, null);
    const parsed = JSON.parse(stored!);
    assert.equal(parsed.state.accessToken, "access-token");
    assert.equal(parsed.state.refreshToken, "refresh-token");
    assert.equal(parsed.state.user.username, "tester");

    useAuthStore.getState().clearSession();

    assert.equal(useAuthStore.getState().accessToken, null);
    assert.equal(useAuthStore.getState().refreshToken, null);
    assert.equal(useAuthStore.getState().user, null);
  } finally {
    // cleanup
  }
});
