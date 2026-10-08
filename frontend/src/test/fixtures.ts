import { HttpResponse, http } from 'msw';
import { server } from './server';
import type { AccountSummary, ConfigurationRequest } from '../lib/api/types';
export const account: AccountSummary = {
    accountId: 1,
    username: 'Admin',
    accountType: 'administrator',
    canAccessApi: true,
    isLocked: false,
    createdAt: '2026-10-01T12:00:00Z',
};
export const catalog: ConfigurationRequest = {
    authenticationEndpointId: 'main',
    allowInsecureLoopback: false,
    endpoints: [{ id: 'main', label: 'Main shard', address: 'https://shard.example:5001' }],
};
export const info = {
    version: '0.14.0',
    codename: 'Moonrise',
    instanceId: 'instance-1',
    realmId: '',
    mode: 'standalone',
    uptimeSeconds: '90',
};
export const loginResult = () => ({
    accessToken: 'fixture-rest-jwt',
    tokenType: 'Bearer',
    account,
    expiresAt: new Date(Date.now() + 3600000).toISOString(),
});
export function defaults(configured = true) {
    server.use(
        http.get('*/api/configuration/status', () => HttpResponse.json({ configured, setupAvailable: !configured })),
        http.post('*/api/auth/login', () => HttpResponse.json(loginResult())),
        http.get('*/api/auth/session', () => HttpResponse.json({ account, expiresAt: loginResult().expiresAt })),
        http.post('*/api/auth/logout', () => new HttpResponse(null, { status: 204 })),
        http.get('*/api/servers', () => HttpResponse.json([{ id: 'main', label: 'Main shard' }])),
        http.get('*/api/servers/main', () => HttpResponse.json(info)),
    );
}
