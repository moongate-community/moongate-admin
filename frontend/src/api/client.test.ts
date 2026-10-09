import { http, HttpResponse } from "msw";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { ApiError } from "@/api/errors";
import { apiRequest, configureApi } from "@/api/client";
import { api, server } from "@/test/server";

const onUnauthorized = vi.fn();

beforeEach(() => {
  onUnauthorized.mockReset();
  configureApi({ getToken: () => "jwt-token", onUnauthorized });
});

describe("apiRequest", () => {
  it("sends the bearer token and parses JSON", async () => {
    let authorization: string | null = null;
    server.use(
      http.get(api("/servers"), ({ request }) => {
        authorization = request.headers.get("authorization");
        return HttpResponse.json([{ id: "login", label: "Login" }]);
      })
    );
    const result = await apiRequest<{ id: string }[]>("GET", "/servers");
    expect(authorization).toBe("Bearer jwt-token");
    expect(result[0].id).toBe("login");
  });

  it("omits the authorization header without a token", async () => {
    configureApi({ getToken: () => null, onUnauthorized });
    let authorization: string | null = "unset";
    server.use(
      http.post(api("/auth/login"), ({ request }) => {
        authorization = request.headers.get("authorization");
        return HttpResponse.json({});
      })
    );
    await apiRequest("POST", "/auth/login", { username: "a", password: "b" });
    expect(authorization).toBeNull();
  });

  it("returns undefined for 204", async () => {
    server.use(http.post(api("/auth/logout"), () => new HttpResponse(null, { status: 204 })));
    await expect(apiRequest("POST", "/auth/logout")).resolves.toBeUndefined();
  });

  it("parses problem+json into an ApiError", async () => {
    server.use(
      http.post(api("/accounts"), () =>
        HttpResponse.json(
          { code: "upstream_unavailable", correlationId: "abc", mutationOutcomeUnknown: true },
          { status: 503, headers: { "Content-Type": "application/problem+json" } }
        )
      )
    );
    const error = await apiRequest("POST", "/accounts", {}).catch((e: unknown) => e);
    expect(error).toBeInstanceOf(ApiError);
    const apiError = error as ApiError;
    expect(apiError.status).toBe(503);
    expect(apiError.code).toBe("upstream_unavailable");
    expect(apiError.correlationId).toBe("abc");
    expect(apiError.mutationOutcomeUnknown).toBe(true);
    expect(apiError.localSessionCleared).toBe(false);
  });

  it("falls back to an http_<status> code for non-JSON errors", async () => {
    server.use(http.get(api("/servers"), () => new HttpResponse("oops", { status: 502 })));
    const error = (await apiRequest("GET", "/servers").catch((e: unknown) => e)) as ApiError;
    expect(error.code).toBe("http_502");
  });

  it("maps network failures to network_error", async () => {
    server.use(http.get(api("/servers"), () => HttpResponse.error()));
    const error = (await apiRequest("GET", "/servers").catch((e: unknown) => e)) as ApiError;
    expect(error.code).toBe("network_error");
    expect(error.status).toBe(0);
  });

  it("calls onUnauthorized once on 401 with a token, but not for login", async () => {
    server.use(
      http.get(api("/servers"), () => HttpResponse.json({ code: "authentication_required" }, { status: 401 })),
      http.post(api("/auth/login"), () => HttpResponse.json({ code: "upstream_unauthenticated" }, { status: 401 }))
    );
    await apiRequest("GET", "/servers").catch(() => undefined);
    expect(onUnauthorized).toHaveBeenCalledTimes(1);
    await apiRequest("POST", "/auth/login", {}).catch(() => undefined);
    expect(onUnauthorized).toHaveBeenCalledTimes(1);
  });

  it("does not end the session on 403", async () => {
    server.use(http.get(api("/accounts"), () => HttpResponse.json({ code: "permission_denied" }, { status: 403 })));
    await apiRequest("GET", "/accounts").catch(() => undefined);
    expect(onUnauthorized).not.toHaveBeenCalled();
  });

  it("never retries a failed request", async () => {
    let calls = 0;
    server.use(
      http.post(api("/accounts"), () => {
        calls += 1;
        return HttpResponse.json({ code: "upstream_unavailable" }, { status: 503 });
      })
    );
    await apiRequest("POST", "/accounts", {}).catch(() => undefined);
    expect(calls).toBe(1);
  });
});

describe("apiRequest network failures on mutations", () => {
  it("flags an unknown outcome for a non-GET network error, not for GET", async () => {
    server.use(
      http.post(api("/accounts"), () => HttpResponse.error()),
      http.get(api("/servers"), () => HttpResponse.error())
    );
    const post = (await apiRequest("POST", "/accounts", {}).catch((e: unknown) => e)) as ApiError;
    const get = (await apiRequest("GET", "/servers").catch((e: unknown) => e)) as ApiError;
    expect(post.code).toBe("network_error");
    expect(post.mutationOutcomeUnknown).toBe(true);
    expect(get.mutationOutcomeUnknown).toBe(false);
  });
});
