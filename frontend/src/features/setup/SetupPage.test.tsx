import { expect, it } from 'vitest';
import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { defaults, catalog, info } from '../../test/fixtures';
import { server } from '../../test/server';
import { renderApp } from '../../test/render';
async function fill() {
    await userEvent.type(await screen.findByLabelText('Setup key'), 'fixture-setup-key');
    await userEvent.type(screen.getByLabelText('ID'), 'main');
    await userEvent.type(screen.getByLabelText('Label'), 'Main shard');
    await userEvent.type(screen.getByLabelText('Address'), 'https://shard.example:5001');
}
it('shows unavailable initial setup without a save action', async () => {
    server.use(
        http.get('*/api/configuration/status', () => HttpResponse.json({ configured: false, setupAvailable: false })),
    );
    renderApp();
    expect(await screen.findByRole('heading', { name: 'Initial setup' })).toBeVisible();
    expect(screen.getByText(/enable initial setup/i)).toBeVisible();
    expect(screen.queryByRole('button', { name: 'Save connections' })).toBeNull();
});
it('sends setup once with intended key and catalog then redirects to login', async () => {
    defaults(false);
    let count = 0;
    let key: string | null = null;
    let body: unknown;
    server.use(
        http.post('*/api/configuration/setup', async ({ request }) => {
            count++;
            key = request.headers.get('X-Moongate-Setup-Token');
            expect(request.headers.has('Authorization')).toBe(false);
            body = await request.json();
            defaults(true);
            return HttpResponse.json({ ...catalog, revision: '1', reauthenticationRequired: true }, { status: 201 });
        }),
    );
    renderApp();
    await fill();
    expect(screen.getByLabelText('Setup key')).toHaveAttribute('type', 'password');
    await userEvent.dblClick(screen.getByRole('button', { name: 'Save connections' }));
    expect(await screen.findByRole('heading', { name: 'Sign in' })).toBeVisible();
    expect(count).toBe(1);
    expect(key).toBe('fixture-setup-key');
    expect(body).toEqual(catalog);
    expect(sessionStorage.length).toBe(0);
});
it('concurrent setup 409 rereads status and closes setup', async () => {
    defaults(false);
    server.use(
        http.post('*/api/configuration/setup', () => {
            defaults(true);
            return HttpResponse.json({ code: 'configuration_already_configured' }, { status: 409 });
        }),
    );
    renderApp();
    await fill();
    await userEvent.click(screen.getByRole('button', { name: 'Save connections' }));
    expect(await screen.findByRole('heading', { name: 'Sign in' })).toBeVisible();
});
it('probe clears temporary password while retaining the setup key for saving', async () => {
    defaults(false);
    server.use(
        http.post('*/api/configuration/test-connection', () => HttpResponse.json({ endpointId: 'main', server: info })),
    );
    renderApp();
    await fill();
    await userEvent.click(screen.getByRole('button', { name: 'Test connection' }));
    await userEvent.type(screen.getByLabelText('Probe username'), 'Admin');
    await userEvent.type(screen.getByLabelText('Probe password'), 'probe-secret');
    await userEvent.click(screen.getByRole('button', { name: 'Run test' }));
    expect(await screen.findByText('Moonrise')).toBeVisible();
    expect(screen.getByLabelText('Probe password')).toHaveValue('');
    await userEvent.click(screen.getByRole('button', { name: 'Close test' }));
    expect(screen.getByLabelText('Setup key')).toHaveValue('fixture-setup-key');
});
