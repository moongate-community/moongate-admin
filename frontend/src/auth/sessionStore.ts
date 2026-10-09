import type { AccountSummary } from "@/api/types";

export interface SessionState {
  token: string | null;
  account: AccountSummary | null;
  expiresAt: Date | null;
}

const empty: SessionState = { token: null, account: null, expiresAt: null };
let state: SessionState = empty;
const listeners = new Set<() => void>();

function publish(next: SessionState): void {
  state = next;
  listeners.forEach((listener) => listener());
}

export const sessionStore = {
  getState(): SessionState {
    return state;
  },
  setSession(next: { token: string; account: AccountSummary; expiresAt: Date }): void {
    publish({ token: next.token, account: next.account, expiresAt: next.expiresAt });
  },
  clear(): void {
    publish(empty);
  },
  subscribe(listener: () => void): () => void {
    listeners.add(listener);
    return () => {
      listeners.delete(listener);
    };
  }
};
