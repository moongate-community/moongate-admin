import { render, screen, act, waitFor } from '@testing-library/react';
import { expect, it, vi } from 'vitest';
import { http, HttpResponse } from 'msw';
import { server } from '../test/server';
import { defaults, loginResult } from '../test/fixtures';
import { AuthProvider, useAuth } from './AuthProvider';
let auth: ReturnType<typeof useAuth>;
function Observe() {
    auth = useAuth();
    return <span>{auth.session?.account.username ?? 'Signed out'}</span>;
}
it('late login after logout cannot restore identity', async () => {
    defaults();
    let finish!: () => void;
    server.use(
        http.post('*/api/auth/login', async () => {
            await new Promise<void>((r) => (finish = r));
            return HttpResponse.json(loginResult());
        }),
    );
    render(
        <AuthProvider>
            <Observe />
        </AuthProvider>,
    );
    let promise!: Promise<unknown>;
    act(() => {
        promise = auth.login({ username: 'Admin', password: 'secret' }).catch(() => {});
    });
    await waitFor(() => expect(finish).toBeTypeOf('function'));
    await act(async () => {
        await auth.logout();
        finish();
        await promise;
    });
    expect(screen.getByText('Signed out')).toBeVisible();
});
it('obsolete read is rejected after local logout', async () => {
    defaults();
    let finish!: () => void;
    server.use(
        http.get('*/api/servers', async () => {
            await new Promise<void>((r) => (finish = r));
            return HttpResponse.json([]);
        }),
    );
    render(
        <AuthProvider>
            <Observe />
        </AuthProvider>,
    );
    await act(async () => auth.login({ username: 'Admin', password: 'secret' }));
    let result!: Promise<unknown>;
    act(() => {
        result = auth.authorizedRequest('/api/servers').catch((e: Error) => e.name);
    });
    await waitFor(() => expect(finish).toBeTypeOf('function'));
    await act(async () => {
        auth.clearSession('Signed out');
        finish();
        expect(await result).toBe('AbortError');
    });
    expect(screen.getByText('Signed out')).toBeVisible();
});
it('expires an in-memory session without restoring it on remount', async () => {
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout', 'Date'], shouldAdvanceTime: true });
    defaults();
    const view = render(
        <AuthProvider>
            <Observe />
        </AuthProvider>,
    );
    await act(async () => auth.login({ username: 'Admin', password: 'secret' }));
    expect(screen.getByText('Admin')).toBeVisible();
    await act(async () => {
        vi.advanceTimersByTime(3600001);
    });
    vi.useRealTimers();
    expect(screen.getByText('Signed out')).toBeVisible();
    view.unmount();
    render(
        <AuthProvider>
            <Observe />
        </AuthProvider>,
    );
    expect(screen.getByText('Signed out')).toBeVisible();
});
