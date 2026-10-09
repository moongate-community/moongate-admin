import { describe, expect, it } from "vitest";
import { formatUptime } from "@/lib/formatUptime";

describe("formatUptime", () => {
  it("formats small values", () => {
    expect(formatUptime("5")).toBe("0d 0h 0m 5s");
    expect(formatUptime("93784")).toBe("1d 2h 3m 4s");
  });
  it("keeps unsigned 64-bit values exact", () => {
    expect(formatUptime("18446744073709551615")).toBe("213503982334601d 7h 0m 15s");
  });
  it("returns non-numeric input unchanged", () => {
    expect(formatUptime("n/a")).toBe("n/a");
  });
});
