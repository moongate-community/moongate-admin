import { afterEach, describe, expect, it, vi } from "vitest";
import { applyTheme, getTheme, setTheme } from "@/theme";

function mockSystemDark(matches: boolean): void {
  window.matchMedia = ((query: string) => ({
    matches,
    media: query,
    onchange: null,
    addEventListener: () => undefined,
    removeEventListener: () => undefined,
    addListener: () => undefined,
    removeListener: () => undefined,
    dispatchEvent: () => false
  })) as unknown as typeof window.matchMedia;
}

afterEach(() => {
  vi.restoreAllMocks();
});

describe("theme", () => {
  it("defaults to dark without a stored choice", () => {
    expect(getTheme()).toBe("dark");
  });

  it("returns a stored valid choice and ignores invalid values", () => {
    localStorage.setItem("moongate-admin-theme", "light");
    expect(getTheme()).toBe("light");
    localStorage.setItem("moongate-admin-theme", "purple");
    expect(getTheme()).toBe("dark");
  });

  it("falls back to dark when storage is unavailable", () => {
    vi.spyOn(Storage.prototype, "getItem").mockImplementation(() => {
      throw new Error("blocked");
    });
    expect(getTheme()).toBe("dark");
  });

  it("applies dark and light to the document", () => {
    applyTheme("dark");
    expect(document.documentElement).toHaveClass("dark");
    expect(document.documentElement.style.colorScheme).toBe("dark");
    applyTheme("light");
    expect(document.documentElement).not.toHaveClass("dark");
    expect(document.documentElement.style.colorScheme).toBe("light");
  });

  it("follows the system preference for the system choice", () => {
    mockSystemDark(true);
    applyTheme("system");
    expect(document.documentElement).toHaveClass("dark");
    mockSystemDark(false);
    applyTheme("system");
    expect(document.documentElement).not.toHaveClass("dark");
  });

  it("stores and applies the choice, and survives a failing storage", () => {
    setTheme("light");
    expect(localStorage.getItem("moongate-admin-theme")).toBe("light");
    expect(document.documentElement).not.toHaveClass("dark");
    vi.spyOn(Storage.prototype, "setItem").mockImplementation(() => {
      throw new Error("blocked");
    });
    expect(() => setTheme("dark")).not.toThrow();
    expect(document.documentElement).toHaveClass("dark");
  });
});
