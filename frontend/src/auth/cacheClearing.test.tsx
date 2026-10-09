import { screen, waitFor } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { describe, expect, it } from "vitest";
import { sessionStore } from "@/auth/sessionStore";
import { renderApp } from "@/test/render";
import { api, server } from "@/test/server";

describe("query cache and session end", () => {
  it("clears cached data when a 401 ends the session", async () => {
    server.use(
      http.get(api("/servers"), () => HttpResponse.json([{ id: "login", label: "Login" }])),
      http.get(api("/servers/login"), () => HttpResponse.json({ code: "authentication_required" }, { status: 401 }))
    );
    const { queryClient, router } = renderApp("/servers");
    await screen.findByRole("link", { name: /Login/ });
    expect(queryClient.getQueryCache().getAll().length).toBeGreaterThan(0);
    router.navigate("/servers/login");
    await waitFor(() => expect(router.state.location.pathname).toBe("/login"));
    expect(sessionStore.getState().token).toBeNull();
    expect(queryClient.getQueryCache().getAll()).toHaveLength(0);
  });

  it("clears cached data when the session is cleared locally (expiry path)", async () => {
    server.use(http.get(api("/servers"), () => HttpResponse.json([{ id: "login", label: "Login" }])));
    const { queryClient } = renderApp("/servers");
    await screen.findByRole("link", { name: /Login/ });
    sessionStore.clear();
    await waitFor(() => expect(queryClient.getQueryCache().getAll()).toHaveLength(0));
  });
});
