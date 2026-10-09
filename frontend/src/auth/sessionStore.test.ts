import { describe, expect, it, vi } from "vitest";
import { sessionStore } from "@/auth/sessionStore";
import { makeSession } from "@/test/fixtures";

describe("sessionStore", () => {
  it("starts empty (a page reload is a logout)", () => {
    sessionStore.clear();
    expect(sessionStore.getState()).toEqual({ token: null, account: null, expiresAt: null });
  });

  it("stores a session, notifies subscribers and keeps a stable snapshot", () => {
    const listener = vi.fn();
    const unsubscribe = sessionStore.subscribe(listener);
    sessionStore.setSession(makeSession());
    expect(listener).toHaveBeenCalledTimes(1);
    expect(sessionStore.getState()).toBe(sessionStore.getState());
    expect(sessionStore.getState().token).toBe("jwt-token");
    sessionStore.clear();
    expect(listener).toHaveBeenCalledTimes(2);
    unsubscribe();
    sessionStore.setSession(makeSession());
    expect(listener).toHaveBeenCalledTimes(2);
  });

  it("never writes to web storage", () => {
    sessionStore.setSession(makeSession());
    expect(localStorage.length).toBe(0);
    expect(sessionStorage.length).toBe(0);
  });
});
