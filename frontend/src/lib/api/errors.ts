import type { ProblemDetails } from './types';
const messages: Record<string, string> = {
    authentication_required: 'Your session ended. Sign in again.',
    permission_denied: 'You do not have permission to perform this action.',
    setup_token_required: 'Enter the initial setup key.',
    configuration_invalid: 'Check the connection settings.',
    configuration_changed: 'Connections changed since you loaded them.',
    configuration_required: 'Initial setup is required.',
    configuration_already_configured: 'Initial setup has already been completed.',
    upstream_unauthenticated: 'Authentication was rejected.',
    upstream_unavailable: 'The server is currently unavailable.',
    upstream_deadlineexceeded: 'The server did not respond in time.',
    upstream_alreadyexists: 'This account already exists.',
    invalid_response: 'The server returned an unexpected response.',
};
export class ApiError extends Error {
    readonly status: number;
    readonly code: string;
    readonly correlationId?: string;
    readonly mutationOutcomeUnknown: boolean;
    readonly localSessionCleared: boolean;
    constructor(status: number, problem: ProblemDetails = {}) {
        const code =
            typeof problem.code === 'string' && /^[a-z0-9_]{1,80}$/.test(problem.code) ? problem.code : 'request_failed';
        super(messages[code] ?? 'The request could not be completed.');
        this.name = 'ApiError';
        this.status = status;
        this.code = code;
        this.correlationId =
            typeof problem.correlationId === 'string' && /^[a-zA-Z0-9:._-]{1,128}$/.test(problem.correlationId)
                ? problem.correlationId
                : undefined;
        this.mutationOutcomeUnknown = problem.mutationOutcomeUnknown === true;
        this.localSessionCleared = problem.localSessionCleared === true;
    }
}
export function errorMessage(error: unknown): string {
    return error instanceof ApiError ? error.message : 'Unable to reach the administration backend.';
}
export function isAbort(error: unknown): boolean {
    return error instanceof Error && error.name === 'AbortError';
}
