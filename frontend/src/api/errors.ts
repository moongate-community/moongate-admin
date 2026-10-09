export interface ApiErrorInit {
  status: number;
  code: string;
  message: string;
  correlationId?: string;
  mutationOutcomeUnknown?: boolean;
  localSessionCleared?: boolean;
}

export class ApiError extends Error {
  readonly status: number;
  readonly code: string;
  readonly correlationId?: string;
  readonly mutationOutcomeUnknown: boolean;
  readonly localSessionCleared: boolean;

  constructor(init: ApiErrorInit) {
    super(init.message);
    this.name = "ApiError";
    this.status = init.status;
    this.code = init.code;
    this.correlationId = init.correlationId;
    this.mutationOutcomeUnknown = init.mutationOutcomeUnknown ?? false;
    this.localSessionCleared = init.localSessionCleared ?? false;
  }
}

export async function toApiError(response: Response): Promise<ApiError> {
  let body: Record<string, unknown> = {};
  try {
    const parsed: unknown = await response.json();
    if (typeof parsed === "object" && parsed !== null) {
      body = parsed as Record<string, unknown>;
    }
  } catch {
    // The body is not JSON; fall back to the status line.
  }

  return new ApiError({
    status: response.status,
    code: typeof body.code === "string" ? body.code : `http_${response.status}`,
    message: typeof body.title === "string" ? body.title : response.statusText || "Request failed",
    correlationId: typeof body.correlationId === "string" ? body.correlationId : undefined,
    mutationOutcomeUnknown: body.mutationOutcomeUnknown === true,
    localSessionCleared: body.localSessionCleared === true
  });
}
