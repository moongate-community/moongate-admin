import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { describe, expect, it } from "vitest";
import { renderApp } from "@/test/render";
import { api, server } from "@/test/server";

describe("servers pages", () => {
  it("lists configured servers by id and label only", async () => {
    server.use(
      http.get(api("/servers"), () =>
        HttpResponse.json([
          { id: "login", label: "Login" },
          { id: "game-1", label: "Game 1" }
        ])
      )
    );
    renderApp("/servers");
    expect(await screen.findByRole("link", { name: /Game 1/ })).toHaveAttribute("href", "/servers/game-1");
    expect(screen.getByText("Login")).toBeInTheDocument();
  });

  it("shows server details with an exact huge uptime", async () => {
    server.use(
      http.get(api("/servers/login"), () =>
        HttpResponse.json({
          version: "0.14.0",
          codename: "fixture",
          instanceId: "inst-1",
          realmId: "realm-1",
          mode: "standalone",
          uptimeSeconds: "18446744073709551615"
        })
      )
    );
    renderApp("/servers/login");
    expect(await screen.findByText("0.14.0")).toBeInTheDocument();
    expect(screen.getByText("standalone")).toBeInTheDocument();
    expect(screen.getByText("213503982334601d 7h 0m 15s")).toBeInTheDocument();
  });

  it("shows a not-found message for an unknown server and offers retry", async () => {
    server.use(http.get(api("/servers/nope"), () => HttpResponse.json({ code: "bad_request" }, { status: 404 })));
    const user = userEvent.setup();
    renderApp("/servers/nope");
    expect(await screen.findByRole("alert")).toHaveTextContent("Not found.");
    await user.click(screen.getByRole("button", { name: "Retry" }));
    expect(await screen.findByRole("alert")).toHaveTextContent("Not found.");
  });

  it("shows an error state when the list fails", async () => {
    server.use(http.get(api("/servers"), () => HttpResponse.json({ code: "upstream_unavailable" }, { status: 503 })));
    renderApp("/servers");
    expect(await screen.findByRole("alert")).toHaveTextContent(/unreachable/i);
  });
});
