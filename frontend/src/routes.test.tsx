import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { describe, expect, it } from "vitest";
import { adminAccount, makeSession, regularAccount } from "@/test/fixtures";
import { renderApp } from "@/test/render";
import { api, server } from "@/test/server";

describe("routes", () => {
  it("redirects signed-out users to login and returns to the deep link after signing in", async () => {
    server.use(
      http.post(api("/auth/login"), () =>
        HttpResponse.json({
          accessToken: "jwt",
          tokenType: "Bearer",
          account: adminAccount,
          expiresAt: new Date(Date.now() + 600_000).toISOString()
        })
      )
    );
    const user = userEvent.setup();
    const { router } = renderApp("/accounts", { session: null });
    await waitFor(() => expect(router.state.location.pathname).toBe("/login"));
    await user.type(screen.getByLabelText("Username"), "admin");
    await user.type(screen.getByLabelText("Password"), "secret-pass");
    await user.click(screen.getByRole("button", { name: "Sign in" }));
    await waitFor(() => expect(router.state.location.pathname).toBe("/accounts"));
  });

  it("hides the Accounts link and blocks the route for non-administrators", async () => {
    const { router } = renderApp("/accounts", { session: makeSession(regularAccount) });
    await waitFor(() => expect(router.state.location.pathname).toBe("/servers"));
    expect(screen.queryByRole("link", { name: "Accounts" })).not.toBeInTheDocument();
  });

  it("shows the Accounts link for administrators", async () => {
    renderApp("/servers");
    expect(await screen.findByRole("link", { name: "Accounts" })).toBeInTheDocument();
  });

  it("renders a not-found page for unknown routes", async () => {
    renderApp("/nope");
    expect(await screen.findByText("Page not found")).toBeInTheDocument();
  });

  it("signs out through the header button", async () => {
    server.use(http.post(api("/auth/logout"), () => new HttpResponse(null, { status: 204 })));
    const user = userEvent.setup();
    const { router } = renderApp("/servers");
    await user.click(await screen.findByRole("button", { name: "Sign out" }));
    await waitFor(() => expect(router.state.location.pathname).toBe("/login"));
  });
});
