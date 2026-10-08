import { describe, expect, it } from 'vitest';
import { http, HttpResponse } from 'msw';
import { server } from '@/test/server';
import { ApiClient } from './client';

describe('REST transport', () => {
    it('sends only the selected authentication header and preserves values', async () => {
        server.use(
            http.post('http://localhost/api/auth/login', async ({ request }) => {
                expect(request.headers.get('Authorization')).toBe('Bearer fixture-rest-jwt');
                expect(request.headers.has('X-Moongate-Setup-Token')).toBe(false);
                expect(await request.json()).toEqual({ username: ' Admin ', password: ' fixture-only ' });
                return HttpResponse.json({ ok: true }, { headers: { ETag: '"11111111111111111111111111111111"' } });
            }),
        );
        const result = await new ApiClient().request('/api/auth/login', {
            method: 'POST',
            body: { username: ' Admin ', password: ' fixture-only ' },
            bearerToken: 'fixture-rest-jwt',
        });
        expect(result.data).toEqual({ ok: true });
        expect(result.etag).toBe('"11111111111111111111111111111111"');
    });
    it('preserves safe problem flags without echoing raw server text', async () => {
        server.use(
            http.post('http://localhost/api/accounts', () =>
                HttpResponse.json(
                    {
                        code: 'upstream_unavailable',
                        correlationId: 'request-7',
                        mutationOutcomeUnknown: true,
                        detail: 'private-server-detail',
                    },
                    { status: 503 },
                ),
            ),
        );
        await expect(new ApiClient().request('/api/accounts', { method: 'POST' })).rejects.toMatchObject({
            status: 503,
            code: 'upstream_unavailable',
            correlationId: 'request-7',
            mutationOutcomeUnknown: true,
        });
    });
    it.each(['https://foreign.example/api/accounts', '//foreign.example/api/accounts', '/api/../../foreign', '/not-api'])(
        'rejects an unsafe target %s before sending credentials',
        async (path) => {
            await expect(new ApiClient().request(path, { bearerToken: 'fixture-rest-jwt' })).rejects.toMatchObject({
                code: 'invalid_request_target',
            });
        },
    );
    it('rejects non-JSON success safely', async () => {
        server.use(
            http.get(
                'http://localhost/api/servers',
                () => new HttpResponse('<h1>private-body</h1>', { headers: { 'content-type': 'text/html' } }),
            ),
        );
        await expect(new ApiClient().request('/api/servers')).rejects.toMatchObject({ code: 'invalid_response' });
    });
    it('supports successful 204 without JSON binding', async () => {
        server.use(http.post('http://localhost/api/auth/logout', () => new HttpResponse(null, { status: 204 })));
        expect((await new ApiClient().request('/api/auth/logout', { method: 'POST' })).data).toBeUndefined();
    });
});
