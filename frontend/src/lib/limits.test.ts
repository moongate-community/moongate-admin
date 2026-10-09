import { describe, expect, it } from "vitest";
import { validatePassword, validateUsername } from "@/lib/limits";

describe("validateUsername", () => {
  it("accepts padded names and exactly 255 characters", () => {
    expect(validateUsername(" Admin ")).toBeNull();
    expect(validateUsername("a".repeat(255))).toBeNull();
  });
  it("rejects blank, too long and NUL", () => {
    expect(validateUsername("   ")).not.toBeNull();
    expect(validateUsername("a".repeat(256))).not.toBeNull();
    expect(validateUsername("ad\0min")).not.toBeNull();
  });
});

describe("validatePassword", () => {
  it("counts UTF-8 bytes, not characters", () => {
    expect(validatePassword("é".repeat(512))).toBeNull();
    expect(validatePassword("é".repeat(513))).not.toBeNull();
  });
  it("rejects blank and NUL", () => {
    expect(validatePassword("  ")).not.toBeNull();
    expect(validatePassword("bad\0value")).not.toBeNull();
  });
});
