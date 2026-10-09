import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { describe, expect, it } from "vitest";

const css = readFileSync(resolve(process.cwd(), "src/index.css"), "utf8");

function tokens(selector: string): Record<string, string> {
  const start = css.indexOf(`${selector} {`);
  const body = css.slice(start, css.indexOf("}", start));
  const result: Record<string, string> = {};
  for (const match of body.matchAll(/--([a-z-]+):\s*(#[0-9a-fA-F]{6})\s*;/g)) {
    result[match[1]] = match[2];
  }
  return result;
}

function luminance(hex: string): number {
  const [r, g, b] = [1, 3, 5].map((i) => parseInt(hex.slice(i, i + 2), 16) / 255);
  const channel = (c: number) => (c <= 0.03928 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4);
  return 0.2126 * channel(r) + 0.7152 * channel(g) + 0.0722 * channel(b);
}

function contrast(a: string, b: string): number {
  const [light, dark] = [luminance(a), luminance(b)].sort((x, y) => y - x);
  return (light + 0.05) / (dark + 0.05);
}

const TEXT = 4.5;
const CONTROL = 3;

const pairs: [string, string, number][] = [
  ["foreground", "background", TEXT],
  ["card-foreground", "card", TEXT],
  ["popover-foreground", "popover", TEXT],
  ["muted-foreground", "background", TEXT],
  ["muted-foreground", "card", TEXT],
  ["muted-foreground", "muted", TEXT],
  ["primary-foreground", "primary", TEXT],
  ["secondary-foreground", "secondary", TEXT],
  ["accent-foreground", "accent", TEXT],
  ["primary", "background", TEXT],
  ["destructive", "background", TEXT],
  ["destructive", "card", TEXT],
  ["success", "background", TEXT],
  ["success", "card", TEXT],
  ["info", "background", TEXT],
  ["info", "card", TEXT],
  ["input", "background", CONTROL],
  ["input", "card", CONTROL],
  ["ring", "background", CONTROL]
];

describe.each([
  ["dark", ".dark"],
  ["light", ":root"]
])("%s palette contrast (WCAG)", (_name, selector) => {
  const palette = tokens(selector);

  it("defines every token used by the checks", () => {
    for (const [fg, bg] of pairs) {
      expect(palette[fg], `${selector} --${fg}`).toBeDefined();
      expect(palette[bg], `${selector} --${bg}`).toBeDefined();
    }
  });

  it.each(pairs)("--%s on --%s reaches the minimum ratio", (fg, bg, minimum) => {
    expect(contrast(palette[fg], palette[bg])).toBeGreaterThanOrEqual(minimum);
  });
});

describe("typography", () => {
  it("uses the system font stack, like moongate.sh, and ships no web font", () => {
    expect(css).toMatch(/--font-sans:\s*ui-sans-serif,\s*system-ui/);
    expect(css).not.toContain("@fontsource");
    expect(css).not.toContain("Geist");
  });
});
