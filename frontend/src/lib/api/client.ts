import { ApiError } from './errors';
import type { ProblemDetails } from './types';
export interface ApiRequestOptions {
    method?: string;
    body?: unknown;
    signal?: AbortSignal;
    headers?: HeadersInit;
    bearerToken?: string;
    setupToken?: string;
}
export interface ApiResult<T> {
    data: T;
    etag?: string;
}
export class ApiClient {
    async request<T>(path: string, options: ApiRequestOptions = {}): Promise<ApiResult<T>> {
        const target = new URL(path, window.location.origin);
        if (!path.startsWith('/api/') || target.origin !== window.location.origin || !target.pathname.startsWith('/api/')) {
            throw new ApiError(0, { code: 'invalid_request_target' });
        }
        const headers = new Headers(options.headers);
        headers.set('Accept', 'application/json');
        headers.delete('Authorization');
        headers.delete('X-Moongate-Setup-Token');
        if (options.bearerToken) {
            headers.set('Authorization', 'Bearer ' + options.bearerToken);
        }
        if (options.setupToken) {
            headers.set('X-Moongate-Setup-Token', options.setupToken);
        }
        if (options.body !== undefined) {
            headers.set('Content-Type', 'application/json');
        }
        const response = await fetch(target, {
            method: options.method ?? 'GET',
            body: options.body === undefined ? undefined : JSON.stringify(options.body),
            signal: options.signal,
            headers,
            credentials: 'omit',
            cache: 'no-store',
        });
        if (response.status === 204 && response.ok) {
            return { data: undefined as T };
        }
        let data: unknown;
        try {
            const contentType = response.headers.get('Content-Type') ?? '';
            if (!contentType.includes('application/json') && !contentType.includes('application/problem+json')) {
                throw new Error('Unexpected content');
            }
            data = await response.json();
        } catch {
            throw new ApiError(response.status, { code: 'invalid_response' });
        }
        if (!response.ok) {
            throw new ApiError(response.status, data && typeof data === 'object' ? (data as ProblemDetails) : {});
        }
        return { data: data as T, etag: response.headers.get('ETag') ?? undefined };
    }
}
export const api = new ApiClient();
