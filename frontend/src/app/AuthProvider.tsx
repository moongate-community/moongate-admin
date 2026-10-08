import { createContext, useContext, useRef, useState, useEffect, useCallback, type ReactNode } from 'react';
import { api, type ApiRequestOptions, type ApiResult } from '../lib/api/client';
import { ApiError } from '../lib/api/errors';
import { parseLogin, parseSession } from '../lib/api/guards';
import type { Credentials, SessionResponse, ProbeRequest, ProbeResponse } from '../lib/api/types';
interface Auth {
    session: SessionResponse | null;
    generation: number;
    notice: string;
    login(credentials: Credentials): Promise<void>;
    logout(): Promise<void>;
    clearSession(reason: string): void;
    authorizedRequest<T>(path: string, options?: ApiRequestOptions): Promise<ApiResult<T>>;
    probeCandidate(request: ProbeRequest, setupToken?: string, signal?: AbortSignal): Promise<ProbeResponse>;
}
const AuthContext = createContext<Auth | null>(null);
const obsolete = () => new DOMException('The request belongs to an obsolete session.', 'AbortError');
export function AuthProvider({ children }: { children: ReactNode }) {
    const [session, setSession] = useState<SessionResponse | null>(null);
    const [generation, setGeneration] = useState(0);
    const [notice, setNotice] = useState('');
    const current = useRef({ token: '', generation: 0, controller: new AbortController() });
    const clearSession = useCallback((reason: string) => {
        current.current.controller.abort();
        current.current = { token: '', generation: current.current.generation + 1, controller: new AbortController() };
        setSession(null);
        setGeneration(current.current.generation);
        setNotice(reason);
    }, []);
    useEffect(
        () => () => {
            current.current.controller.abort();
            current.current.generation++;
            current.current.token = '';
        },
        [],
    );
    useEffect(() => {
        if (!session) return;
        const remaining = Date.parse(session.expiresAt) - Date.now();
        const timer = setTimeout(
            () => clearSession('Your session expired. Sign in again.'),
            Math.min(Math.max(remaining, 0), 2147483647),
        );
        return () => clearTimeout(timer);
    }, [session, clearSession]);
    const authorizedRequest = useCallback(
        async <T,>(path: string, options: ApiRequestOptions = {}): Promise<ApiResult<T>> => {
            const captured = current.current;
            if (!captured.token) throw new ApiError(401, { code: 'authentication_required' });
            const signal = options.signal
                ? AbortSignal.any([options.signal, captured.controller.signal])
                : captured.controller.signal;
            try {
                const result = await api.request<T>(path, {
                    ...options,
                    setupToken: undefined,
                    bearerToken: captured.token,
                    signal,
                });
                if (current.current !== captured || signal.aborted) throw obsolete();
                return result;
            } catch (error) {
                if (current.current !== captured || signal.aborted) throw obsolete();
                if (error instanceof ApiError && (error.status === 401 || error.localSessionCleared))
                    clearSession('Your session ended. Sign in again.');
                throw error;
            }
        },
        [clearSession],
    );
    const login = useCallback(
        async (credentials: Credentials) => {
            clearSession('');
            const captured = current.current;
            const signal = captured.controller.signal;
            const login = parseLogin(
                (await api.request<unknown>('/api/auth/login', { method: 'POST', body: credentials, signal })).data,
            );
            if (current.current !== captured || signal.aborted) throw obsolete();
            const validated = parseSession(
                (await api.request<unknown>('/api/auth/session', { bearerToken: login.accessToken, signal })).data,
            );
            if (current.current !== captured || signal.aborted) throw obsolete();
            if (validated.account.accountId !== login.account.accountId) throw new ApiError(0, { code: 'invalid_response' });
            captured.token = login.accessToken;
            setSession({
                ...validated,
                expiresAt: new Date(Math.min(Date.parse(login.expiresAt), Date.parse(validated.expiresAt))).toISOString(),
            });
        },
        [clearSession],
    );
    const logout = useCallback(async () => {
        const token = current.current.token;
        clearSession('Signed out.');
        if (token) {
            try {
                await api.request('/api/auth/logout', { method: 'POST', bearerToken: token });
            } catch (error) {
                setNotice(
                    'Signed out. ' + (error instanceof ApiError ? error.message : 'The server could not confirm sign out.'),
                );
            }
        }
    }, [clearSession]);
    const probeCandidate = useCallback(
        async (request: ProbeRequest, setupToken?: string, signal?: AbortSignal) => {
            const captured = current.current;
            const combined = signal ? AbortSignal.any([signal, captured.controller.signal]) : captured.controller.signal;
            try {
                const result = await api.request<ProbeResponse>('/api/configuration/test-connection', {
                    method: 'POST',
                    body: request,
                    setupToken,
                    bearerToken: setupToken ? undefined : captured.token,
                    signal: combined,
                });
                if (current.current !== captured || combined.aborted) throw obsolete();
                return result.data;
            } catch (error) {
                if (current.current !== captured || combined.aborted) throw obsolete();
                if (error instanceof ApiError && error.status === 401 && captured.token) {
                    try {
                        parseSession((await authorizedRequest<unknown>('/api/auth/session', { signal })).data);
                    } catch {
                        /* Only the current authority's 401 clears local authentication. */
                    }
                }
                throw error;
            }
        },
        [authorizedRequest],
    );
    return (
        <AuthContext.Provider
            value={{ session, generation, notice, login, logout, clearSession, authorizedRequest, probeCandidate }}
        >
            {children}
        </AuthContext.Provider>
    );
}
export function useAuth(): Auth {
    const context = useContext(AuthContext);
    if (!context) throw new Error('AuthProvider is required.');
    return context;
}
