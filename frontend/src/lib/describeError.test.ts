import { describe, expect, it } from "vitest";
import { ApiError } from "@/api/errors";
import { describeError } from "@/lib/describeError";

const make = (code: string, status = 400, correlationId?: string) =>
  new ApiError({ status, code, message: "x", correlationId });

describe("describeError", () => {
  it("maps known codes to readable messages", () => {
    expect(describeError(make("upstream_unauthenticated", 401))).toMatch(/username and password/i);
    expect(describeError(make("configuration_required", 503))).toMatch(/not configured/i);
    expect(describeError(make("upstream_unavailable", 503))).toMatch(/unreachable/i);
    expect(describeError(make("network_error", 0))).toMatch(/unreachable/i);
    expect(describeError(make("upstream_alreadyexists", 409))).toMatch(/already exists/i);
    expect(describeError(make("permission_denied", 403))).toMatch(/permission/i);
    expect(describeError(make("anything", 404))).toMatch(/not found/i);
  });
  it("falls back to the code and appends the correlation id", () => {
    expect(describeError(make("weird_code", 500, "abc123"))).toBe("Request failed (weird_code). (reference abc123)");
  });
  it("handles non-API errors", () => {
    expect(describeError(new Error("boom with secret"))).toBe("Unexpected error.");
  });
});
