import { ApiError, toApiError } from "@/api/errors";

interface ClientConfig {
  getToken: () => string | null;
  onUnauthorized: () => void;
}

let config: ClientConfig = { getToken: () => null, onUnauthorized: () => undefined };

export function configureApi(next: ClientConfig): void {
  config = next;
}

export async function apiRequest<T>(
  method: string,
  path: string,
  body?: unknown,
  signal?: AbortSignal
): Promise<T> {
  const headers: Record<string, string> = { Accept: "application/json" };
  const token = config.getToken();
  if (token) {
    headers.Authorization = `Bearer ${token}`;
  }
  if (body !== undefined) {
    headers["Content-Type"] = "application/json";
  }

  let response: Response;
  try {
    response = await fetch(new URL(`/api${path}`, window.location.href), {
      method,
      headers,
      body: body === undefined ? undefined : JSON.stringify(body),
      signal
    });
  } catch (cause) {
    if (cause instanceof DOMException && cause.name === "AbortError") {
      throw cause;
    }
    throw new ApiError({ status: 0, code: "network_error", message: "Network error." });
  }

  if (!response.ok) {
    const error = await toApiError(response);
    if (response.status === 401 && token && path !== "/auth/login") {
      config.onUnauthorized();
    }
    throw error;
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return (await response.json()) as T;
}
