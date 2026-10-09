import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { describe, expect, it } from "vitest";
import { adminAccount, regularAccount } from "@/test/fixtures";
import { renderApp } from "@/test/render";
import { api, server } from "@/test/server";

function listHandler(calls: URLSearchParams[]) {
  return http.get(api("/accounts"), ({ request }) => {
    const params = new URL(request.url).searchParams;
    calls.push(params);
    const after = Number(params.get("afterAccountId"));
    return HttpResponse.json({
      accounts: after === 0 ? [adminAccount] : [regularAccount],
      nextAfterAccountId: after === 0 ? 4294967295 : 0
    });
  });
}

describe("AccountsPage", () => {
  it("renders account rows", async () => {
    server.use(listHandler([]));
    renderApp("/accounts");
    const row = (await screen.findByRole("cell", { name: "admin" })).closest("tr")!;
    expect(within(row).getByText("administrator")).toBeInTheDocument();
    expect(within(row).getByText("7")).toBeInTheDocument();
  });

  it("starts at page size 50 with cursor 0 and pages with the exact uint32 cursor", async () => {
    const calls: URLSearchParams[] = [];
    server.use(listHandler(calls));
    const user = userEvent.setup();
    renderApp("/accounts");
    await screen.findByRole("cell", { name: "admin" });
    expect(calls[0].get("pageSize")).toBe("50");
    expect(calls[0].get("afterAccountId")).toBe("0");
    expect(screen.getByRole("button", { name: "First page" })).toBeDisabled();
    await user.click(screen.getByRole("button", { name: "Next page" }));
    await screen.findByRole("cell", { name: "player" });
    expect(calls.at(-1)?.get("afterAccountId")).toBe("4294967295");
    expect(screen.getByRole("button", { name: "Next page" })).toBeDisabled();
  });

  it("returns to the first page with the first-page button", async () => {
    const calls: URLSearchParams[] = [];
    server.use(listHandler(calls));
    const user = userEvent.setup();
    renderApp("/accounts");
    await screen.findByRole("cell", { name: "admin" });
    await user.click(screen.getByRole("button", { name: "Next page" }));
    await screen.findByRole("cell", { name: "player" });
    await user.click(screen.getByRole("button", { name: "First page" }));
    expect(await screen.findByRole("cell", { name: "admin" })).toBeInTheDocument();
  });

  it("changing the page size resets to the first page", async () => {
    const calls: URLSearchParams[] = [];
    server.use(listHandler(calls));
    const user = userEvent.setup();
    renderApp("/accounts");
    await screen.findByRole("cell", { name: "admin" });
    await user.click(screen.getByRole("button", { name: "Next page" }));
    await screen.findByRole("cell", { name: "player" });
    await user.selectOptions(screen.getByLabelText("Page size"), "200");
    await screen.findByRole("cell", { name: "admin" });
    const last = calls.at(-1)!;
    expect(last.get("pageSize")).toBe("200");
    expect(last.get("afterAccountId")).toBe("0");
  });

  it("shows an error state with retry when the list fails", async () => {
    server.use(http.get(api("/accounts"), () => HttpResponse.json({ code: "upstream_permissiondenied" }, { status: 403 })));
    renderApp("/accounts");
    expect(await screen.findByRole("alert")).toHaveTextContent(/permission/i);
  });
});
