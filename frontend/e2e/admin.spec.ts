import { test, expect, type Page } from '@playwright/test';
const account = {
    accountId: 1,
    username: 'Admin',
    accountType: 'administrator',
    canAccessApi: true,
    isLocked: false,
    createdAt: '2026-10-01T12:00:00Z',
};
const player = { ...account, accountId: 2, username: 'PlayerOne', exact: true, accountType: 'regular', canAccessApi: false };
const catalog = {
    authenticationEndpointId: 'main',
    allowInsecureLoopback: false,
    endpoints: [
        { id: 'main', label: 'Main shard', address: 'https://shard.example:5001' },
        { id: 'other', label: 'Other shard', address: 'https://other.example:5001' },
    ],
};
const info = {
    version: '0.14.0',
    codename: 'Moonrise',
    instanceId: 'instance-1',
    realmId: '',
    mode: 'standalone',
    uptimeSeconds: '18446744073709551615',
};
async function fixture(page: Page, configured = true) {
    const state = {
        configured,
        creates: 0,
        revokes: 0,
        conflict: false,
        unknown: false,
        gateway: false,
        reads: 0,
        sessionChecks: 0,
        etags: [] as (string | undefined)[],
    };
    await page.route(/\/api\//, async (route) => {
        const request = route.request();
        const path = new URL(request.url()).pathname;
        if (!path.startsWith('/api/')) return route.continue();
        let status = 200;
        let data: unknown;
        let headers: Record<string, string> = {};
        const session = { account, expiresAt: new Date(Date.now() + 3600000).toISOString() };
        if (path === '/api/configuration/status') data = { configured: state.configured, setupAvailable: !state.configured };
        else if (path === '/api/configuration/setup') {
            expect(request.headers()['x-moongate-setup-token']).toBe('fixture-setup-key');
            expect(request.headers()['authorization']).toBeUndefined();
            state.configured = true;
            status = 201;
            data = { ...catalog, revision: '11111111111111111111111111111111', reauthenticationRequired: true };
        } else if (path === '/api/auth/login') data = { ...session, accessToken: 'fixture-rest-jwt', tokenType: 'Bearer' };
        else if (path === '/api/auth/session') {
            state.sessionChecks++;
            data = session;
        } else {
            expect(request.headers()['authorization']).toBe('Bearer fixture-rest-jwt');
            if (path === '/api/servers') data = catalog.endpoints.map(({ id, label }) => ({ id, label }));
            else if (path.startsWith('/api/servers/'))
                data = { ...info, codename: path.endsWith('/other') ? 'Other world' : 'Moonrise' };
            else if (path === '/api/configuration') {
                if (request.method() === 'PUT') {
                    state.etags.push(request.headers()['if-match']);
                    if (state.gateway)
                        return route.fulfill({
                            status: 502,
                            contentType: 'text/html',
                            body: '<html>Gateway failure</html>',
                        });
                    status = state.conflict ? 412 : 200;
                    data = state.conflict
                        ? { code: 'configuration_changed' }
                        : { ...catalog, revision: '2', reauthenticationRequired: true };
                } else {
                    data = { ...catalog, revision: '11111111111111111111111111111111', reauthenticationRequired: false };
                    headers = { ETag: '"11111111111111111111111111111111"' };
                }
            } else if (path === '/api/configuration/test-connection') {
                status = 401;
                data = { code: 'upstream_unauthenticated' };
            } else if (path === '/api/accounts') {
                if (request.method() === 'POST') {
                    state.creates++;
                    if (state.gateway)
                        return route.fulfill({
                            status: 502,
                            contentType: 'text/html',
                            body: '<html>Gateway failure</html>',
                        });
                    const body = request.postDataJSON();
                    expect(body.accountType).toBe('regular');
                    expect(body.canAccessApi).toBe(false);
                    status = state.unknown ? 503 : 201;
                    data = state.unknown
                        ? { code: 'upstream_unavailable', mutationOutcomeUnknown: true }
                        : { ...player, accountId: 3, username: body.username };
                } else {
                    state.reads++;
                    data = { accounts: [player], nextAfterAccountId: 0 };
                }
            } else if (path.endsWith('/revoke-sessions')) {
                state.revokes++;
                return route.fulfill({ status: 204 });
            } else if (path === '/api/auth/logout') return route.fulfill({ status: 204 });
            else throw Error('Unexpected fixture route ' + path);
        }
        await route.fulfill({ status, json: data, headers });
    });
    return state;
}
async function login(page: Page) {
    await page.getByLabel('Username', { exact: true }).fill('Admin');
    await page.getByLabel('Password', { exact: true }).fill('fixture-password');
    await page.getByRole('button', { name: 'Sign in', exact: true }).click();
    await expect(page.getByRole('heading', { name: 'Overview' })).toBeVisible();
}
test('initial setup, login, theme persistence and memory-only authentication', async ({ page }, testInfo) => {
    await fixture(page, false);
    await page.goto('/setup');
    await expect(page.getByRole('heading', { name: 'Initial setup' })).toBeVisible();
    await page.screenshot({ path: testInfo.outputPath('setup-desktop.png'), fullPage: true, animations: 'disabled' });
    for (const [label, value] of [
        ['Setup key', 'fixture-setup-key'],
        ['ID', 'main'],
        ['Label', 'Main shard'],
        ['Address', 'https://shard.example:5001'],
    ])
        await page.getByLabel(label, { exact: true }).fill(value);
    await page.getByRole('button', { name: 'Save connections' }).click();
    await login(page);
    await expect(page.getByText('213503982334601d 7h 0m 15s')).toBeVisible();
    await page.screenshot({ path: testInfo.outputPath('overview-dark.png'), fullPage: true, animations: 'disabled' });
    await page.getByRole('button', { name: 'Switch to light theme' }).click();
    await expect(page.locator('html')).toHaveClass('light');
    await page.screenshot({ path: testInfo.outputPath('overview-light.png'), fullPage: true, animations: 'disabled' });
    expect(await page.evaluate(() => ({ local: { ...localStorage }, session: { ...sessionStorage } }))).toEqual({
        local: { 'moongate-admin-theme': 'light' },
        session: {},
    });
    await page.reload();
    await expect(page.getByRole('heading', { name: 'Sign in' })).toBeVisible();
    await expect(page.locator('html')).toHaveClass('light');
});
test('mobile Sheet navigation and independent server selection', async ({ page }, testInfo) => {
    await page.setViewportSize({ width: 390, height: 844 });
    await fixture(page);
    await page.goto('/login');
    await login(page);
    await page.getByRole('button', { name: 'Open navigation' }).click();
    await expect(page.getByRole('dialog')).toBeVisible();
    await page.screenshot({ path: testInfo.outputPath('navigation-mobile.png'), fullPage: true, animations: 'disabled' });
    await page.keyboard.press('Tab');
    await page.getByRole('link', { name: 'Servers', exact: true }).click();
    await expect(page.getByRole('heading', { name: 'Servers' })).toBeVisible();
    await page.getByLabel('Selected server').selectOption('other');
    await expect(page.getByText('Other world')).toBeVisible();
    await expect(page.getByRole('dialog')).not.toBeVisible();
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
    await page.screenshot({ path: testInfo.outputPath('servers-mobile.png'), fullPage: true, animations: 'disabled' });
});
test('ETag conflict, candidate401, uncertain creation and confirmed revocation', async ({ page }, testInfo) => {
    const state = await fixture(page);
    state.conflict = true;
    state.unknown = true;
    await page.goto('/login');
    await login(page);
    await page.getByRole('link', { name: 'Connections' }).click();
    await page.getByLabel('Label', { exact: true }).first().fill('My unsaved server');
    await page.getByRole('button', { name: 'Save connections' }).click();
    await expect(page.getByRole('alert')).toBeVisible();
    await expect(page.getByLabel('Label', { exact: true }).first()).toHaveValue('My unsaved server');
    expect(state.etags).toEqual(['"11111111111111111111111111111111"']);
    await page.getByRole('button', { name: 'Test connection', exact: true }).click();
    await page.getByLabel('Probe username').fill('Candidate');
    await page.getByLabel('Probe password').fill('fixture-probe-password');
    await page.getByRole('button', { name: 'Run test' }).click();
    await expect(page.getByRole('dialog').getByRole('alert')).toBeVisible();
    await expect(page.getByLabel('Probe password')).toHaveValue('');
    await page.getByRole('button', { name: 'Close test' }).click();
    await page.getByRole('link', { name: 'Accounts' }).click();
    await page.getByRole('button', { name: 'Create account', exact: true }).click();
    await page.getByLabel('New username').fill('NewUser');
    await page.getByLabel('New password').fill('fixture-new-password');
    await page.getByRole('button', { name: 'Create', exact: true }).click();
    await expect(page.getByRole('dialog').getByText(/Verify the account list before trying again/)).toBeVisible();
    await expect(page.getByLabel('New password')).toHaveValue('');
    await expect(page.getByRole('button', { name: 'Create', exact: true })).toBeDisabled();
    expect(state.creates).toBe(1);
    await page.getByRole('button', { name: 'Verify account list' }).click();
    await expect(page.getByRole('cell', { name: 'PlayerOne', exact: true })).toBeVisible();
    await page.getByRole('button', { name: 'I verified the account list' }).click();
    await page.getByRole('button', { name: 'Revoke sessions for PlayerOne' }).click();
    await expect(page.getByRole('alertdialog')).toHaveAccessibleName('Revoke administrative sessions for PlayerOne');
    await page.screenshot({ path: testInfo.outputPath('revoke-desktop.png'), animations: 'disabled' });
    await page.getByRole('button', { name: 'Cancel', exact: true }).click();
    expect(state.revokes).toBe(0);
    await page.getByRole('button', { name: 'Revoke sessions for PlayerOne' }).click();
    await page.getByRole('button', { name: 'Confirm revocation' }).click();
    await expect(page.getByText('Administrative sessions revoked for PlayerOne')).toBeVisible();
    expect(state.revokes).toBe(1);
});
test('gateway uncertainty survives navigation and needs a fresh list before acknowledgement', async ({ page }) => {
    const state = await fixture(page);
    state.gateway = true;
    await page.goto('/login');
    await login(page);
    await page.getByRole('link', { name: 'Connections' }).click();
    await page.getByLabel('Label', { exact: true }).first().fill('Gateway draft');
    await page.getByRole('button', { name: 'Save connections' }).click();
    await expect(page.getByText(/Check the latest configuration before trying again/)).toBeVisible();
    await expect(page.getByRole('button', { name: 'Save connections' })).toBeDisabled();
    await expect.poll(() => state.sessionChecks).toBe(2);
    await page.getByRole('link', { name: 'Accounts' }).click();
    await page.getByRole('button', { name: 'Create account', exact: true }).click();
    await page.getByLabel('New username').fill('GatewayUser');
    await page.getByLabel('New password').fill('fixture-new-password');
    await page.getByRole('button', { name: 'Create', exact: true }).click();
    await expect(page.getByRole('dialog').getByText(/Verify the account list before trying again/)).toBeVisible();
    await page.keyboard.press('Escape');
    await expect(page.getByRole('button', { name: 'I verified the account list' })).toBeDisabled();
    expect(state.reads).toBe(1);
    await page.getByRole('link', { name: 'Servers' }).click();
    await expect(page.getByRole('heading', { name: 'Servers' })).toBeVisible();
    await page.getByRole('link', { name: 'Accounts' }).click();
    await expect(page.getByRole('cell', { name: 'PlayerOne', exact: true })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Create account', exact: true })).toBeDisabled();
    await expect(page.getByRole('button', { name: 'I verified the account list' })).toBeEnabled();
    await page.getByRole('button', { name: 'I verified the account list' }).click();
    await expect(page.getByRole('button', { name: 'Create account', exact: true })).toBeEnabled();
    expect(state.creates).toBe(1);
    expect(state.etags).toHaveLength(1);
});
