import { expect, it } from 'vitest';
import { screen, waitFor, within, fireEvent } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { server } from '../../test/server';
import { defaults, account } from '../../test/fixtures';
import { renderApp, signIn } from '../../test/render';
const player = { ...account, accountId: 2, username: 'PlayerOne', accountType: 'regular', canAccessApi: false };
async function enter() {
    defaults();
    server.use(http.get('*/api/accounts', () => HttpResponse.json({ accounts: [player], nextAfterAccountId: 0 })));
    renderApp();
    await signIn();
    await userEvent.click(screen.getByRole('link', { name: 'Accounts' }));
    await screen.findByRole('cell', { name: 'PlayerOne' });
}
async function create() {
    await userEvent.click(screen.getByRole('button', { name: 'Create account' }));
    await userEvent.type(screen.getByLabelText('New username'), ' NewUser ');
    await userEvent.type(screen.getByLabelText('New password'), ' secret ');
}
it('uses default 50 and next/previous cursors, resetting on size change', async () => {
    await enter();
    const requests: string[] = [];
    server.use(
        http.get('*/api/accounts', ({ request }) => {
            requests.push(new URL(request.url).search);
            return HttpResponse.json({
                accounts: [player],
                nextAfterAccountId: new URL(request.url).searchParams.get('afterAccountId') === '0' ? 50 : 0,
            });
        }),
    );
    await userEvent.click(screen.getByRole('button', { name: 'Refresh accounts' }));
    await waitFor(() => expect(screen.getByRole('button', { name: 'Next page' })).toBeEnabled());
    await userEvent.click(screen.getByRole('button', { name: 'Next page' }));
    await waitFor(() => expect(screen.getByRole('button', { name: 'Next page' })).toBeDisabled());
    await userEvent.click(screen.getByRole('button', { name: 'Previous page' }));
    await waitFor(() => expect(screen.getByRole('button', { name: 'Previous page' })).toBeDisabled());
    await userEvent.selectOptions(screen.getByLabelText('Page size'), '25');
    await waitFor(() => expect(requests.at(-1)).toBe('?pageSize=25&afterAccountId=0'));
    expect(requests.slice(0, 3)).toEqual([
        '?pageSize=50&afterAccountId=0',
        '?pageSize=50&afterAccountId=50',
        '?pageSize=50&afterAccountId=0',
    ]);
    expect(
        within(screen.getByLabelText('Page size'))
            .getAllByRole('option')
            .map((e) => e.textContent),
    ).toEqual(['25', '50', '100', '200']);
});
it('renders role, API access, lock and ID without unsupported actions', async () => {
    await enter();
    expect(screen.getByRole('cell', { name: '2' })).toBeVisible();
    expect(screen.getByRole('cell', { name: 'Regular' })).toBeVisible();
    expect(screen.getByRole('cell', { name: 'Disabled' })).toBeVisible();
    expect(screen.getByRole('cell', { name: 'Unlocked' })).toBeVisible();
    expect(screen.queryByRole('searchbox')).toBeNull();
    expect(screen.queryByRole('button', { name: 'Delete' })).toBeNull();
});
it('creates once with exact credentials, Regular role and API disabled then refreshes', async () => {
    await enter();
    let count = 0;
    let body: unknown;
    server.use(
        http.post('*/api/accounts', async ({ request }) => {
            count++;
            body = await request.json();
            return HttpResponse.json({ ...player, accountId: 3, username: ' NewUser ' }, { status: 201 });
        }),
    );
    await create();
    await userEvent.dblClick(screen.getByRole('button', { name: 'Create' }));
    expect(await screen.findByText('Account created: NewUser')).toBeVisible();
    expect(body).toEqual({ username: ' NewUser ', password: ' secret ', accountType: 'regular', canAccessApi: false });
    expect(count).toBe(1);
    expect(screen.queryByLabelText('New password')).toBeNull();
});
it('explicit role and API choice are transmitted', async () => {
    await enter();
    let body: unknown;
    server.use(
        http.post('*/api/accounts', async ({ request }) => {
            body = await request.json();
            return HttpResponse.json({ ...player, username: 'NewUser' }, { status: 201 });
        }),
    );
    await create();
    await userEvent.selectOptions(screen.getByLabelText('Account role'), 'gameMaster');
    await userEvent.click(screen.getByLabelText('Allow API access'));
    await userEvent.click(screen.getByRole('button', { name: 'Create' }));
    await screen.findByText('Account created: NewUser');
    expect(body).toMatchObject({ accountType: 'gameMaster', canAccessApi: true });
});
it('validates UTF8 password bytes before dispatch', async () => {
    await enter();
    await create();
    fireEvent.change(screen.getByLabelText('New password'), { target: { value: '😀'.repeat(257) } });
    await userEvent.click(screen.getByRole('button', { name: 'Create' }));
    expect(screen.getByText(/at most 1024 UTF-8 bytes/)).toBeVisible();
});
it('409 preserves username and clears password', async () => {
    await enter();
    server.use(http.post('*/api/accounts', () => HttpResponse.json({ code: 'upstream_alreadyexists' }, { status: 409 })));
    await create();
    await userEvent.click(screen.getByRole('button', { name: 'Create' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('already exists');
    expect(screen.getByLabelText('New password')).toHaveValue('');
    expect(screen.getByLabelText('New username')).toHaveValue(' NewUser ');
});
it('unknown outcome blocks repeat creation until explicit reconciliation', async () => {
    await enter();
    let count = 0;
    server.use(
        http.post('*/api/accounts', () => {
            count++;
            return HttpResponse.json({ code: 'upstream_unavailable', mutationOutcomeUnknown: true }, { status: 503 });
        }),
    );
    await create();
    await userEvent.click(screen.getByRole('button', { name: 'Create' }));
    expect(await within(screen.getByRole('dialog')).findByText(/Verify the account list before trying again/)).toBeVisible();
    expect(screen.getByLabelText('New password')).toHaveValue('');
    expect(screen.getByLabelText('New username')).toHaveValue(' NewUser ');
    expect(screen.getByRole('button', { name: 'Create' })).toBeDisabled();
    await userEvent.click(screen.getByRole('button', { name: 'Verify account list' }));
    await screen.findByRole('cell', { name: 'PlayerOne' });
    expect(screen.getByRole('button', { name: 'Create account' })).toBeDisabled();
    await userEvent.click(screen.getByRole('button', { name: 'I verified the account list' }));
    expect(screen.getByRole('button', { name: 'Create account' })).toBeEnabled();
    expect(count).toBe(1);
});
it('revocation cancel dispatches nothing and named confirmation sends once', async () => {
    await enter();
    let count = 0;
    server.use(
        http.post('*/api/accounts/2/revoke-sessions', () => {
            count++;
            return new HttpResponse(null, { status: 204 });
        }),
    );
    await userEvent.click(screen.getByRole('button', { name: 'Revoke sessions for PlayerOne' }));
    expect(screen.getByRole('alertdialog')).toHaveAccessibleName('Revoke administrative sessions for PlayerOne');
    expect(screen.getByText(/administrative sessions will end/i)).toBeVisible();
    await userEvent.click(screen.getByRole('button', { name: 'Cancel' }));
    expect(count).toBe(0);
    await userEvent.click(screen.getByRole('button', { name: 'Revoke sessions for PlayerOne' }));
    await userEvent.click(screen.getByRole('button', { name: 'Confirm revocation' }));
    expect(await screen.findByText('Administrative sessions revoked for PlayerOne')).toBeVisible();
    expect(count).toBe(1);
});
it('successful self revocation clears identity immediately', async () => {
    await enter();
    server.use(
        http.get('*/api/accounts', () => HttpResponse.json({ accounts: [account], nextAfterAccountId: 0 })),
        http.post('*/api/accounts/1/revoke-sessions', () => new HttpResponse(null, { status: 204 })),
    );
    await userEvent.click(screen.getByRole('button', { name: 'Refresh accounts' }));
    await userEvent.click(await screen.findByRole('button', { name: 'Revoke sessions for Admin' }));
    await userEvent.click(screen.getByRole('button', { name: 'Confirm revocation' }));
    expect(await screen.findByRole('heading', { name: 'Sign in' })).toBeVisible();
});
it('revocation outage keeps session and does not retry', async () => {
    await enter();
    let count = 0;
    server.use(
        http.post('*/api/accounts/2/revoke-sessions', () => {
            count++;
            return HttpResponse.json({ code: 'upstream_unavailable' }, { status: 503 });
        }),
    );
    await userEvent.click(screen.getByRole('button', { name: 'Revoke sessions for PlayerOne' }));
    await userEvent.click(screen.getByRole('button', { name: 'Confirm revocation' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('unavailable');
    expect(count).toBe(1);
});
it('HTML gateway response after creation requires reconciliation rather than resend', async () => {
    await enter();
    let count = 0;
    server.use(
        http.post('*/api/accounts', () => {
            count++;
            return new HttpResponse('<html>Gateway failure</html>', {
                status: 502,
                headers: { 'Content-Type': 'text/html' },
            });
        }),
    );
    await create();
    await userEvent.click(screen.getByRole('button', { name: 'Create' }));
    expect(await within(screen.getByRole('dialog')).findByText(/Verify the account list before trying again/)).toBeVisible();
    expect(screen.getByRole('button', { name: 'Create' })).toBeDisabled();
    expect(count).toBe(1);
});
it('closing unknown creation cannot acknowledge an unrefreshed account list', async () => {
    await enter();
    let reads = 0;
    server.use(
        http.get('*/api/accounts', () => {
            reads++;
            return HttpResponse.json({ accounts: [player], nextAfterAccountId: 0 });
        }),
        http.post('*/api/accounts', () =>
            HttpResponse.json({ code: 'upstream_unavailable', mutationOutcomeUnknown: true }, { status: 503 }),
        ),
    );
    await create();
    await userEvent.click(screen.getByRole('button', { name: 'Create' }));
    await within(screen.getByRole('dialog')).findByText(/Verify the account list before trying again/);
    await userEvent.keyboard('{Escape}');
    expect(screen.getByRole('button', { name: 'Create account' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'I verified the account list' })).toBeDisabled();
    expect(reads).toBe(0);
    await userEvent.click(screen.getByRole('button', { name: 'Refresh accounts' }));
    await waitFor(() => expect(screen.getByRole('button', { name: 'I verified the account list' })).toBeEnabled());
    expect(reads).toBe(1);
    await userEvent.click(screen.getByRole('button', { name: 'I verified the account list' }));
    expect(screen.getByRole('button', { name: 'Create account' })).toBeEnabled();
});
it('unknown creation remains locked across protected route remounts', async () => {
    await enter();
    server.use(
        http.post('*/api/accounts', () =>
            HttpResponse.json({ code: 'upstream_unavailable', mutationOutcomeUnknown: true }, { status: 503 }),
        ),
    );
    await create();
    await userEvent.click(screen.getByRole('button', { name: 'Create' }));
    await within(screen.getByRole('dialog')).findByText(/Verify the account list before trying again/);
    await userEvent.keyboard('{Escape}');
    await userEvent.click(screen.getByRole('link', { name: 'Servers' }));
    await screen.findByRole('heading', { name: 'Servers' });
    await userEvent.click(screen.getByRole('link', { name: 'Accounts' }));
    await screen.findByRole('cell', { name: 'PlayerOne' });
    expect(screen.getByRole('button', { name: 'Create account' })).toBeDisabled();
    expect(screen.getByText(/Username:/)).toHaveTextContent('NewUser');
});
