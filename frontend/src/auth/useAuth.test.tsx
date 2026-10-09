import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { act, render, screen, waitFor } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import type { ReactNode } from "react";
import { describe, expect, it, vi } from "vitest";
import { AuthProvider } from "@/auth/AuthProvider";
import { sessionStore } from "@/auth/sessionStore";
import { useAuth } from "@/auth/useAuth";
import { adminAccount, makeSession } from "@/test/fixtures";
import { api, server } from "@/test/server";

function Probe() {
  const auth = useAuth();
  return (
    <div>
      <span data-testid="state">{auth.isAuthenticated ? `in:${auth.account?.username}` : "out"}</span>
      <button onClick={() => void auth.login("admin", "secret-pass")}>login</button>
      <button onClick={() => void auth.logout()}>logout</button>
    </div>
  );
}

function wrap(children: ReactNode) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <AuthProvider>{children}</AuthProvider>
    </QueryClientProvider>
  );
}

describe("useAuth", () => {
  it("login stores the session from the API response", async () => {
    server.use(
      http.post(api("/auth/login"), () =>
        HttpResponse.json({
          accessToken: "new-jwt",
          tokenType: "Bearer",
          account: adminAccount,
          expiresAt: new Date(Date.now() + 600_000).toISOString()
        })
      )
    );
    wrap(<Probe />);
    await act(async () => screen.getByText("login").click());
    await waitFor(() => expect(screen.getByTestId("state")).toHaveTextContent("in:admin"));
    expect(sessionStore.getState().token).toBe("new-jwt");
  });

  it("logout clears local state even when the request fails", async () => {
    sessionStore.setSession(makeSession());
    server.use(http.post(api("/auth/logout"), () => HttpResponse.json({ code: "upstream_unavailable" }, { status: 503 })));
    wrap(<Probe />);
    await act(async () => screen.getByText("logout").click());
    await waitFor(() => expect(screen.getByTestId("state")).toHaveTextContent("out"));
    expect(sessionStore.getState().token).toBeNull();
  });

  it("ends the session at the absolute expiry", async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    try {
      sessionStore.setSession(makeSession(adminAccount, 1));
      wrap(<Probe />);
      expect(screen.getByTestId("state")).toHaveTextContent("in:admin");
      await act(async () => {
        vi.advanceTimersByTime(61_000);
      });
      expect(screen.getByTestId("state")).toHaveTextContent("out");
    } finally {
      vi.useRealTimers();
    }
  });

  it("a 401 from any call clears the session", async () => {
    sessionStore.setSession(makeSession());
    server.use(http.post(api("/auth/logout"), () => HttpResponse.json({ code: "authentication_required" }, { status: 401 })));
    wrap(<Probe />);
    await act(async () => screen.getByText("logout").click());
    await waitFor(() => expect(sessionStore.getState().token).toBeNull());
  });
});
