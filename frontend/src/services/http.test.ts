import test from "node:test";
import assert from "node:assert/strict";
import type { InternalAxiosRequestConfig } from "axios";

import { httpClient } from "./http.ts";
import { useAuthStore } from "../stores/auth-store.ts";
import { useAuthModalStore } from "../stores/auth-modal-store.ts";

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

function resetStores() {
  useAuthStore.setState({
    accessToken: null,
    refreshToken: null,
    expiresIn: null,
    user: null,
    hydrated: true,
  });
  useAuthModalStore.setState({
    open: false,
    mode: "login",
    pendingAction: null,
  });
}

function readAuthorizationHeader(config: InternalAxiosRequestConfig) {
  if (typeof config.headers.get === "function") {
    return config.headers.get("Authorization");
  }

  return config.headers.Authorization;
}

test("HTTP 请求应从内存态会话注入 Authorization 头", async () => {
  resetStores();
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

  const originalAdapter = httpClient.defaults.adapter;

  try {
    httpClient.defaults.adapter = async (config) => {
      assert.equal(readAuthorizationHeader(config), "Bearer access-token");

      return {
        data: { ok: true },
        status: 200,
        statusText: "OK",
        headers: {},
        config,
      };
    };

    await httpClient.get("/api/test-auth-header");
  } finally {
    httpClient.defaults.adapter = originalAdapter;
  }
});

test("收到 401 时应清空内存会话并拉起登录弹窗", async () => {
  resetStores();
  const { localStorage, sessionStorage, cleanup } = installWindowMock();

  localStorage.setItem("ai-interview-auth", "legacy-token");
  sessionStorage.setItem("ai-interview-auth", "legacy-token");

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

  const originalAdapter = httpClient.defaults.adapter;

  try {
    httpClient.defaults.adapter = async (config) =>
      Promise.reject({
        response: { status: 401 },
        config,
      });

    await assert.rejects(() => httpClient.get("/api/test-unauthorized"));

    assert.equal(useAuthStore.getState().accessToken, null);
    assert.equal(useAuthStore.getState().refreshToken, null);
    assert.equal(useAuthModalStore.getState().open, true);
    assert.equal(localStorage.getItem("ai-interview-auth"), null);
    assert.equal(sessionStorage.getItem("ai-interview-auth"), null);
  } finally {
    httpClient.defaults.adapter = originalAdapter;
    cleanup();
    await new Promise((resolve) => setTimeout(resolve, 1100));
  }
});
