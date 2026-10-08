import { expect, it } from 'vitest';
import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { server } from '../../test/server';
import { defaults, catalog, info } from '../../test/fixtures';
import { renderApp, signIn } from '../../test/render';
const tag = '"11111111111111111111111111111111"';
async function enter(etag: string | null = tag) {
    defaults();
    server.use(
        http.get('*/api/configuration', () =>
            HttpResponse.json(
                { ...catalog, revision: '11111111111111111111111111111111', reauthenticationRequired: false },
                { headers: etag ? { ETag: etag } : {} },
            ),
        ),
    );
    renderApp();
    await signIn();
    await userEvent.click(screen.getByRole('link', { name: 'Connections' }));
    await screen.findByLabelText('Label');
}
async function edit() {
    await userEvent.clear(screen.getByLabelText('Label'));
    await userEvent.type(screen.getByLabelText('Label'), 'My unsaved server');
}
async function probe() {
    await userEvent.click(screen.getByRole('button', { name: 'Test connection' }));
    await userEvent.type(screen.getByLabelText('Probe username'), 'Candidate');
    await userEvent.type(screen.getByLabelText('Probe password'), 'probe-secret');
    await userEvent.click(screen.getByRole('button', { name: 'Run test' }));
}
it('saves exactly once with quoted ETag and clears auth after success', async () => {
    await enter();
    let count = 0;
    let etag: string | null = null;
    server.use(
        http.put('*/api/configuration', async ({ request }) => {
            count++;
            etag = request.headers.get('If-Match');
            return HttpResponse.json({ ...catalog, revision: '2', reauthenticationRequired: true });
        }),
    );
    await edit();
    await userEvent.dblClick(screen.getByRole('button', { name: 'Save connections' }));
    expect(await screen.findByRole('heading', { name: 'Sign in' })).toBeVisible();
    expect(etag).toBe(tag);
    expect(count).toBe(1);
});
it('412 preserves draft until explicit discard/reload', async () => {
    await enter();
    server.use(http.put('*/api/configuration', () => HttpResponse.json({ code: 'configuration_changed' }, { status: 412 })));
    await edit();
    await userEvent.click(screen.getByRole('button', { name: 'Save connections' }));
    expect(await screen.findByRole('alert')).toBeVisible();
    expect(screen.getByLabelText('Label')).toHaveValue('My unsaved server');
    await userEvent.click(screen.getByRole('button', { name: 'Reload latest' }));
    expect(screen.getByRole('alertdialog')).toBeVisible();
    await userEvent.click(screen.getByRole('button', { name: 'Discard and reload' }));
    await waitFor(() => expect(screen.getByLabelText('Label')).toHaveValue('Main shard'));
});
it('missing strong ETag prevents update', async () => {
    await enter(null);
    await edit();
    expect(screen.getByRole('button', { name: 'Save connections' })).toBeDisabled();
});
it('allows adding/removing rows and choosing authentication server', async () => {
    await enter();
    await userEvent.click(screen.getByRole('button', { name: 'Add server' }));
    await userEvent.type(screen.getAllByLabelText('ID')[1], 'second');
    await userEvent.selectOptions(screen.getByLabelText('Authentication server'), 'second');
    expect(screen.getByLabelText('Authentication server')).toHaveValue('second');
    await userEvent.click(screen.getByRole('button', { name: 'Remove server 2' }));
    expect(screen.getAllByLabelText('ID')).toHaveLength(1);
    expect(screen.getByLabelText('Authentication server')).toHaveValue('main');
});
it.each([200, 401])('candidate401 checks current session (%i) independently', async (status) => {
    await enter();
    server.use(
        http.post('*/api/configuration/test-connection', () =>
            HttpResponse.json({ code: 'upstream_unauthenticated' }, { status: 401 }),
        ),
    );
    if (status === 401)
        server.use(
            http.get('*/api/auth/session', () => HttpResponse.json({ code: 'upstream_unauthenticated' }, { status: 401 })),
        );
    await probe();
    if (status === 401) expect(await screen.findByRole('heading', { name: 'Sign in' })).toBeVisible();
    else {
        expect(await screen.findByRole('alert')).toBeVisible();
        expect(screen.getByRole('link', { name: 'Accounts', hidden: true })).toBeInTheDocument();
        expect(screen.getByLabelText('Probe password')).toHaveValue('');
    }
});
it.each([403, 503])('probe %i preserves current session', async (status) => {
    await enter();
    server.use(
        http.post('*/api/configuration/test-connection', () =>
            HttpResponse.json({ code: status === 403 ? 'permission_denied' : 'upstream_unavailable' }, { status }),
        ),
    );
    await probe();
    expect(await screen.findByRole('alert')).toBeVisible();
    expect(screen.getByRole('link', { name: 'Accounts', hidden: true })).toBeInTheDocument();
});
it('renders safe candidate information after test', async () => {
    await enter();
    server.use(
        http.post('*/api/configuration/test-connection', () => HttpResponse.json({ endpointId: 'main', server: info })),
    );
    await probe();
    expect(await screen.findByText('Connection test succeeded')).toBeVisible();
    expect(screen.getByText('Moonrise')).toBeVisible();
});
it('unknown configuration outcome blocks resend and preserves draft for reconciliation', async () => {
    await enter();
    let count = 0;
    server.use(
        http.put('*/api/configuration', () => {
            count++;
            return HttpResponse.error();
        }),
    );
    await edit();
    await userEvent.click(screen.getByRole('button', { name: 'Save connections' }));
    expect(await screen.findByText(/Check the latest configuration before trying again/)).toBeVisible();
    expect(screen.getByRole('button', { name: 'Save connections' })).toBeDisabled();
    expect(screen.getByLabelText('Label')).toHaveValue('My unsaved server');
    expect(count).toBe(1);
});
it('unknown committed save checks current session and returns to login on 401', async () => {
    await enter();
    server.use(
        http.put('*/api/configuration', () => HttpResponse.error()),
        http.get('*/api/auth/session', () => HttpResponse.json({ code: 'authentication_required' }, { status: 401 })),
    );
    await edit();
    await userEvent.click(screen.getByRole('button', { name: 'Save connections' }));
    expect(await screen.findByRole('heading', { name: 'Sign in' })).toBeVisible();
});
it('428 is recoverable without blind resend', async () => {
    await enter();
    server.use(
        http.put('*/api/configuration', () =>
            HttpResponse.json({ code: 'configuration_precondition_required' }, { status: 428 }),
        ),
    );
    await edit();
    await userEvent.click(screen.getByRole('button', { name: 'Save connections' }));
    expect(await screen.findByRole('alert')).toBeVisible();
    expect(screen.getByLabelText('Label')).toHaveValue('My unsaved server');
});
