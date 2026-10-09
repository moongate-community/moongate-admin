import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { describe, expect, it } from "vitest";
import { sessionStore } from "@/auth/sessionStore";
import { adminAccount } from "@/test/fixtures";
import { renderApp } from "@/test/render";
import { api, server } from "@/test/server";

describe("LoginPage", () => {
  it("signs in and lands on the servers page", async () => {
    server.use(
      http.post(api("/auth/login"), async ({ request }) => {
        const body = (await request.json()) as { username: string; password: string };
        expect(body).toEqual({ username: "admin", password: "secret-pass" });
        return HttpResponse.json({
          accessToken: "jwt",
          tokenType: "Bearer",
          account: adminAccount,
          expiresAt: new Date(Date.now() + 600_000).toISOString()
        });
      }),
      http.get(api("/servers"), () => HttpResponse.json([]))
    );
    const user = userEvent.setup();
    const { router } = renderApp("/login", { session: null });
    await user.type(screen.getByLabelText("Username"), "admin");
    await user.type(screen.getByLabelText("Password"), "secret-pass");
    await user.click(screen.getByRole("button", { name: "Sign in" }));
    await waitFor(() => expect(router.state.location.pathname).toBe("/servers"));
    expect(sessionStore.getState().token).toBe("jwt");
  });

  it("blocks blank fields without calling the API", async () => {
    const user = userEvent.setup();
    renderApp("/login", { session: null });
    await user.type(screen.getByLabelText("Username"), "   ");
    await user.type(screen.getByLabelText("Password"), "   ");
    await user.click(screen.getByRole("button", { name: "Sign in" }));
    expect(await screen.findByText("Username is required.")).toBeInTheDocument();
    expect(screen.getByText("Password is required.")).toBeInTheDocument();
  });

  it("shows a safe message for rejected credentials and never echoes the password", async () => {
    server.use(
      http.post(api("/auth/login"), () =>
        HttpResponse.json({ code: "upstream_unauthenticated", correlationId: "c1" }, { status: 401 })
      )
    );
    const user = userEvent.setup();
    renderApp("/login", { session: null });
    await user.type(screen.getByLabelText("Username"), "admin");
    await user.type(screen.getByLabelText("Password"), "super-secret-value");
    await user.click(screen.getByRole("button", { name: "Sign in" }));
    const alert = await screen.findByRole("alert");
    expect(alert).toHaveTextContent(/username and password/i);
    expect(alert).toHaveTextContent("c1");
    expect(document.body.textContent).not.toContain("super-secret-value");
  });
});
