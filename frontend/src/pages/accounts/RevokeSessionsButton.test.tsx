import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { describe, expect, it } from "vitest";
import { sessionStore } from "@/auth/sessionStore";
import { adminAccount, regularAccount } from "@/test/fixtures";
import { renderApp } from "@/test/render";
import { api, server } from "@/test/server";

function useList() {
  server.use(
    http.get(api("/accounts"), () =>
      HttpResponse.json({ accounts: [adminAccount, regularAccount], nextAfterAccountId: 0 })
    )
  );
}

describe("RevokeSessionsButton", () => {
  it("asks for confirmation before revoking another account's sessions", async () => {
    useList();
    let revoked: string | null = null;
    server.use(
      http.post(api("/accounts/:id/revoke-sessions"), ({ params }) => {
        revoked = String(params.id);
        return new HttpResponse(null, { status: 204 });
      })
    );
    const user = userEvent.setup();
    renderApp("/accounts");
    await screen.findByRole("cell", { name: "player" });
    await user.click(screen.getByRole("button", { name: "Revoke sessions for player" }));
    expect(revoked).toBeNull();
    await user.click(await screen.findByRole("button", { name: "Revoke" }));
    await waitFor(() => expect(revoked).toBe("9"));
    expect(await screen.findByText("Sessions revoked for player.")).toBeInTheDocument();
    expect(sessionStore.getState().token).not.toBeNull();
  });

  it("does nothing when the confirmation is cancelled", async () => {
    useList();
    let calls = 0;
    server.use(
      http.post(api("/accounts/:id/revoke-sessions"), () => {
        calls += 1;
        return new HttpResponse(null, { status: 204 });
      })
    );
    const user = userEvent.setup();
    renderApp("/accounts");
    await screen.findByRole("cell", { name: "player" });
    await user.click(screen.getByRole("button", { name: "Revoke sessions for player" }));
    await user.click(await screen.findByRole("button", { name: "Cancel" }));
    expect(calls).toBe(0);
  });

  it("logs out locally after revoking the signed-in account's own sessions", async () => {
    useList();
    server.use(
      http.post(api("/accounts/:id/revoke-sessions"), () => new HttpResponse(null, { status: 204 })),
      http.post(api("/auth/logout"), () => HttpResponse.json({ code: "authentication_required" }, { status: 401 }))
    );
    const user = userEvent.setup();
    const { router } = renderApp("/accounts");
    await screen.findByRole("cell", { name: "player" });
    await user.click(screen.getByRole("button", { name: "Revoke sessions for admin" }));
    await user.click(await screen.findByRole("button", { name: "Revoke" }));
    await waitFor(() => expect(router.state.location.pathname).toBe("/login"));
    expect(sessionStore.getState().token).toBeNull();
  });

  it("shows a safe error and keeps the session on failure", async () => {
    useList();
    server.use(
      http.post(api("/accounts/:id/revoke-sessions"), () => HttpResponse.json({ code: "upstream_notfound" }, { status: 404 }))
    );
    const user = userEvent.setup();
    renderApp("/accounts");
    await screen.findByRole("cell", { name: "player" });
    await user.click(screen.getByRole("button", { name: "Revoke sessions for player" }));
    await user.click(await screen.findByRole("button", { name: "Revoke" }));
    expect(await screen.findByText("Not found.")).toBeInTheDocument();
    expect(sessionStore.getState().token).not.toBeNull();
  });

  it("sends exactly one request on rapid double clicks of Revoke", async () => {
    useList();
    let calls = 0;
    server.use(
      http.post(api("/accounts/:id/revoke-sessions"), async () => {
        calls += 1;
        await new Promise((resolve) => setTimeout(resolve, 150));
        return new HttpResponse(null, { status: 204 });
      })
    );
    const user = userEvent.setup();
    renderApp("/accounts");
    await screen.findByRole("cell", { name: "player" });
    await user.click(screen.getByRole("button", { name: "Revoke sessions for player" }));
    await user.dblClick(await screen.findByRole("button", { name: "Revoke" }));
    await screen.findByText("Sessions revoked for player.");
    expect(calls).toBe(1);
  });
});
