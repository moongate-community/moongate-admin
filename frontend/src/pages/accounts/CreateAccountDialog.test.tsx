import { fireEvent, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { describe, expect, it } from "vitest";
import { adminAccount } from "@/test/fixtures";
import { renderApp } from "@/test/render";
import { api, server } from "@/test/server";

function useList() {
  server.use(http.get(api("/accounts"), () => HttpResponse.json({ accounts: [adminAccount], nextAfterAccountId: 0 })));
}

async function openDialog(user: ReturnType<typeof userEvent.setup>) {
  await screen.findByRole("cell", { name: "admin" });
  await user.click(screen.getByRole("button", { name: "Create account" }));
  return screen.findByRole("dialog");
}

describe("CreateAccountDialog", () => {
  it("creates a regular account without API access by default", async () => {
    useList();
    let body: Record<string, unknown> = {};
    server.use(
      http.post(api("/accounts"), async ({ request }) => {
        body = (await request.json()) as Record<string, unknown>;
        return HttpResponse.json(
          { ...adminAccount, accountId: 8, username: "newbie", accountType: "regular", canAccessApi: false },
          { status: 201 }
        );
      })
    );
    const user = userEvent.setup();
    renderApp("/accounts");
    await openDialog(user);
    await user.type(screen.getByLabelText("Username"), " newbie ");
    await user.type(screen.getByLabelText("Password"), "a-long-password");
    await user.click(screen.getByRole("button", { name: "Create" }));
    await waitFor(() => expect(screen.queryByRole("dialog")).not.toBeInTheDocument());
    expect(body).toEqual({ username: " newbie ", password: "a-long-password", accountType: "regular", canAccessApi: false });
    expect(await screen.findByText("Account newbie created.")).toBeInTheDocument();
  });

  it("sends the chosen role and API access", async () => {
    useList();
    let body: Record<string, unknown> = {};
    server.use(
      http.post(api("/accounts"), async ({ request }) => {
        body = (await request.json()) as Record<string, unknown>;
        return HttpResponse.json({ ...adminAccount, accountId: 9, username: "gm1" }, { status: 201 });
      })
    );
    const user = userEvent.setup();
    renderApp("/accounts");
    await openDialog(user);
    await user.type(screen.getByLabelText("Username"), "gm1");
    await user.type(screen.getByLabelText("Password"), "a-long-password");
    await user.selectOptions(screen.getByLabelText("Role"), "gameMaster");
    await user.click(screen.getByRole("switch", { name: "API access" }));
    await user.click(screen.getByRole("button", { name: "Create" }));
    await waitFor(() => expect(body.accountType).toBe("gameMaster"));
    expect(body.canAccessApi).toBe(true);
  });

  it("validates blank input without calling the API", async () => {
    useList();
    let calls = 0;
    server.use(
      http.post(api("/accounts"), () => {
        calls += 1;
        return HttpResponse.json({}, { status: 201 });
      })
    );
    const user = userEvent.setup();
    renderApp("/accounts");
    await openDialog(user);
    await user.click(screen.getByRole("button", { name: "Create" }));
    expect(await screen.findByText("Username is required.")).toBeInTheDocument();
    expect(screen.getByText("Password is required.")).toBeInTheDocument();
    expect(calls).toBe(0);
  });

  it("shows the duplicate username message on 409", async () => {
    useList();
    server.use(http.post(api("/accounts"), () => HttpResponse.json({ code: "upstream_alreadyexists" }, { status: 409 })));
    const user = userEvent.setup();
    renderApp("/accounts");
    await openDialog(user);
    await user.type(screen.getByLabelText("Username"), "admin");
    await user.type(screen.getByLabelText("Password"), "a-long-password");
    await user.click(screen.getByRole("button", { name: "Create" }));
    expect(await screen.findByText(/already exists/i)).toBeInTheDocument();
    expect(screen.getByRole("dialog")).toBeInTheDocument();
  });

  it("reports an unknown outcome, keeps the form and offers a reload", async () => {
    let lists = 0;
    server.use(
      http.get(api("/accounts"), () => {
        lists += 1;
        return HttpResponse.json({ accounts: [adminAccount], nextAfterAccountId: 0 });
      }),
      http.post(api("/accounts"), () =>
        HttpResponse.json({ code: "upstream_deadlineexceeded", mutationOutcomeUnknown: true }, { status: 504 })
      )
    );
    const user = userEvent.setup();
    renderApp("/accounts");
    await openDialog(user);
    await user.type(screen.getByLabelText("Username"), "maybe");
    await user.type(screen.getByLabelText("Password"), "a-long-password");
    await user.click(screen.getByRole("button", { name: "Create" }));
    expect(await screen.findByText(/outcome is unknown/i)).toBeInTheDocument();
    expect(screen.getByLabelText("Username")).toHaveValue("maybe");
    const before = lists;
    await user.click(screen.getByRole("button", { name: "Reload accounts to check" }));
    await waitFor(() => expect(lists).toBeGreaterThan(before));
  });

  it("sends exactly one request on rapid double clicks", async () => {
    useList();
    let calls = 0;
    server.use(
      http.post(api("/accounts"), async () => {
        calls += 1;
        await new Promise((resolve) => setTimeout(resolve, 150));
        return HttpResponse.json({ ...adminAccount, accountId: 10, username: "once" }, { status: 201 });
      })
    );
    const user = userEvent.setup();
    renderApp("/accounts");
    await openDialog(user);
    await user.type(screen.getByLabelText("Username"), "once");
    await user.type(screen.getByLabelText("Password"), "a-long-password");
    await user.dblClick(screen.getByRole("button", { name: "Create" }));
    await waitFor(() => expect(screen.queryByRole("dialog")).not.toBeInTheDocument());
    expect(calls).toBe(1);
  });

  it("sends exactly one request when two submits fire before React re-renders", async () => {
    useList();
    let calls = 0;
    server.use(
      http.post(api("/accounts"), async () => {
        calls += 1;
        await new Promise((resolve) => setTimeout(resolve, 150));
        return HttpResponse.json({ ...adminAccount, accountId: 11, username: "sync" }, { status: 201 });
      })
    );
    const user = userEvent.setup();
    renderApp("/accounts");
    await openDialog(user);
    await user.type(screen.getByLabelText("Username"), "sync");
    await user.type(screen.getByLabelText("Password"), "a-long-password");
    const submit = screen.getByRole("button", { name: "Create" });
    fireEvent.click(submit);
    fireEvent.click(submit);
    await waitFor(() => expect(screen.queryByRole("dialog")).not.toBeInTheDocument());
    expect(calls).toBe(1);
  });
});
