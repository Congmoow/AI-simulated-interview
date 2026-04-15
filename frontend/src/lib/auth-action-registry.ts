type CallbackFn = () => void | Promise<void>;

const registry = new Map<string, CallbackFn>();
let counter = 0;

export function registerAuthCallback(fn: CallbackFn): string {
  const id = `auth-cb-${++counter}`;
  registry.set(id, fn);
  return id;
}

export function executeAuthCallback(id: string): void {
  const fn = registry.get(id);
  if (fn) {
    registry.delete(id);
    void fn();
  }
}

export function clearAuthCallback(id: string): void {
  registry.delete(id);
}
