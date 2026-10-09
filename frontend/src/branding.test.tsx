import { screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { renderApp } from "@/test/render";

describe("branding", () => {
  it("shows the Moongate logo on the login page", () => {
    renderApp("/login", { session: null });
    const logo = screen.getByRole("img", { name: "Moongate" });
    expect(logo).toHaveAttribute("src", "/moongate_logo.png");
  });

  it("centers the login logo and title horizontally", () => {
    renderApp("/login", { session: null });
    const logo = screen.getByRole("img", { name: "Moongate" });
    expect(logo).toHaveClass("mx-auto");
    expect(logo.parentElement).toHaveClass("justify-items-center", "text-center");
  });

  it("shows the Moongate mark in the app header with the theme switcher", async () => {
    renderApp("/servers");
    const mark = await screen.findByRole("img", { name: "Moongate" });
    expect(mark).toHaveAttribute("src", "/moongate_mark.png");
    expect(screen.getByLabelText("Theme")).toBeInTheDocument();
  });
});
