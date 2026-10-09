import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";
import { ThemeSwitcher } from "@/components/layout/ThemeSwitcher";

describe("ThemeSwitcher", () => {
  it("shows dark by default and switches and stores the choice", async () => {
    const user = userEvent.setup();
    render(<ThemeSwitcher />);
    const select = screen.getByLabelText("Theme");
    expect(select).toHaveValue("dark");
    await user.selectOptions(select, "light");
    expect(localStorage.getItem("moongate-admin-theme")).toBe("light");
    expect(document.documentElement).not.toHaveClass("dark");
    await user.selectOptions(select, "dark");
    expect(document.documentElement).toHaveClass("dark");
  });

  it("restores the stored choice", () => {
    localStorage.setItem("moongate-admin-theme", "system");
    render(<ThemeSwitcher />);
    expect(screen.getByLabelText("Theme")).toHaveValue("system");
  });
});
