import { expect, it } from 'vitest';
import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { server } from '../test/server';
import { defaults, loginResult, account } from '../test/fixtures';
import { renderApp } from '../test/render';
export async function signIn() {
    await userEvent.type(await screen.findByLabelText('Username'), 'Admin');
    await userEvent.type(screen.getByLabelText('Password'), 'fixture-password');
    await userEvent.click(screen.getByRole('button', { name: 'Sign in' }));
    await screen.findByRole('heading', { name: 'Overview' });
}
it('shows loading until status is known and recovers status failure', async () => {
    let finish!: () => void;
    server.use(
        http.get('*/api/configuration/status', async () => {
            await new Promise<void>((r) => {
                finish = r;
            });
            return HttpResponse.json({ configured: true, setupAvailable: false });
        }),
    );
    renderApp();
    expect(screen.getByRole('status')).toHaveTextContent('Connecting');
    await new Promise((r) => setTimeout(r, 50));
    finish();
    expect(await screen.findByRole('heading', { name: 'Sign in' })).toBeVisible();
});
it('shows an explicit recoverable backend error without login', async () => {
    server.use(http.get('*/api/configuration/status', () => HttpResponse.error()));
    renderApp();
    expect(await screen.findByRole('alert')).toHaveTextContent('Unable to reach');
    expect(screen.queryByLabelText('Password')).toBeNull();
    expect(screen.getByRole('button', { name: 'Retry' })).toBeEnabled();
});
it('does not accept malformed status as configured', async () => {
    server.use(http.get('*/api/configuration/status', () => HttpResponse.json({ configured: 'yes' })));
    renderApp();
    expect(await screen.findByRole('alert')).toBeVisible();
    expect(screen.queryByRole('heading', { name: 'Sign in' })).toBeNull();
});
it.each(['regular', 'gameMaster'] as const)('guards administrator direct routes for %s', async (role) => {
    defaults();
    server.use(
        http.post('*/api/auth/login', () =>
            HttpResponse.json({ ...loginResult(), account: { ...account, accountType: role } }),
        ),
        http.get('*/api/auth/session', () =>
            HttpResponse.json({ account: { ...account, accountType: role }, expiresAt: loginResult().expiresAt }),
        ),
    );
    renderApp('/accounts');
    await signIn();
    expect(screen.queryByRole('link', { name: 'Accounts' })).toBeNull();
    expect(screen.queryByRole('link', { name: 'Connections' })).toBeNull();
});
it('administrator sees navigation and logout clears it on network failure', async () => {
    defaults();
    server.use(http.post('*/api/auth/logout', () => HttpResponse.error()));
    renderApp();
    await signIn();
    expect(screen.getByRole('link', { name: 'Accounts' })).toBeVisible();
    await userEvent.click(screen.getByRole('button', { name: 'Sign out' }));
    expect(await screen.findByRole('heading', { name: 'Sign in' })).toBeVisible();
    expect(screen.queryByRole('link', { name: 'Accounts' })).toBeNull();
    expect(localStorage.getItem('moongate-admin-theme')).toBe('dark');
    expect(localStorage.length).toBe(1);
    expect(sessionStorage.length).toBe(0);
});
