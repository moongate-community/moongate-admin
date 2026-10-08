import { ApiError } from './errors';
import type { AccountSummary, ConfigurationStatus, LoginResponse, SessionResponse } from './types';
function object(value: unknown): value is Record<string, unknown> {
    return value !== null && typeof value === 'object';
}
function validAccount(value: unknown): value is AccountSummary {
    return (
        object(value) &&
        Number.isInteger(value.accountId) &&
        Number(value.accountId) > 0 &&
        Number(value.accountId) <= 4294967295 &&
        typeof value.username === 'string' &&
        ['regular', 'gameMaster', 'administrator'].includes(String(value.accountType)) &&
        typeof value.canAccessApi === 'boolean' &&
        typeof value.isLocked === 'boolean' &&
        typeof value.createdAt === 'string' &&
        Number.isFinite(Date.parse(value.createdAt))
    );
}
function expiry(value: unknown): value is string {
    return typeof value === 'string' && Number.isFinite(Date.parse(value)) && Date.parse(value) > Date.now();
}
export function parseStatus(value: unknown): ConfigurationStatus {
    if (!object(value) || typeof value.configured !== 'boolean' || typeof value.setupAvailable !== 'boolean')
        throw new ApiError(0, { code: 'invalid_response' });
    return value as unknown as ConfigurationStatus;
}
export function parseSession(value: unknown): SessionResponse {
    if (!object(value) || !validAccount(value.account) || !expiry(value.expiresAt))
        throw new ApiError(0, { code: 'invalid_response' });
    return value as unknown as SessionResponse;
}
export function parseLogin(value: unknown): LoginResponse {
    parseSession(value);
    if (!object(value) || typeof value.accessToken !== 'string' || !value.accessToken.trim() || value.tokenType !== 'Bearer')
        throw new ApiError(0, { code: 'invalid_response' });
    return value as unknown as LoginResponse;
}
