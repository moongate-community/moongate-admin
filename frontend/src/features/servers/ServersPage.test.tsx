import { expect, it } from 'vitest';
import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { server } from '../../test/server';
import { defaults, info } from '../../test/fixtures';
import { renderApp, signIn } from '../../test/render';
import { formatUptime } from './uptime';
it.each([
    ['90', '0d 0h 1m 30s'],
    ['18446744073709551615', '213503982334601d 7h 0m 15s'],
    ['-1', 'Unavailable'],
    ['2e3', 'Unavailable'],
    ['18446744073709551616', 'Unavailable'],
])('formats uint64 uptime %s', (input, expected) => expect(formatUptime(input)).toBe(expected));
it('shows server information and unavailable realm without invented metrics', async () => {
    defaults();
    renderApp();
    await signIn();
    expect(await screen.findByText('Moonrise')).toBeVisible();
    expect(screen.getByText('0d 0h 1m 30s')).toBeVisible();
    expect(screen.getByText('Unavailable')).toBeVisible();
    expect(screen.queryByText('CPU')).toBeNull();
});
it('preserves server selection after an endpoint failure and supports refresh', async () => {
    defaults();
    server.use(
        http.get('*/api/servers', () =>
            HttpResponse.json([
                { id: 'main', label: 'Main shard' },
                { id: 'offline', label: 'Other shard' },
            ]),
        ),
        http.get('*/api/servers/offline', () => HttpResponse.json({ code: 'upstream_unavailable' }, { status: 503 })),
    );
    renderApp();
    await signIn();
    await userEvent.click(screen.getByRole('link', { name: 'Servers' }));
    await userEvent.selectOptions(await screen.findByLabelText('Selected server'), 'offline');
    expect(await screen.findByRole('alert')).toHaveTextContent('unavailable');
    expect(screen.getByLabelText('Selected server')).toHaveValue('offline');
    server.use(http.get('*/api/servers/offline', () => HttpResponse.json({ ...info, codename: 'Recovered' })));
    await userEvent.click(screen.getByRole('button', { name: 'Refresh information' }));
    expect(await screen.findByText('Recovered')).toBeVisible();
});
it('empty server list has an explicit empty state', async () => {
    defaults();
    server.use(http.get('*/api/servers', () => HttpResponse.json([])));
    renderApp();
    await signIn();
    expect(await screen.findByText('No servers configured.')).toBeVisible();
});
