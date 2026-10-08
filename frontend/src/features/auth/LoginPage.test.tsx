import { expect, it } from 'vitest';
import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { defaults, loginResult } from '../../test/fixtures';
import { server } from '../../test/server';
import { renderApp } from '../../test/render';
it('preserves credentials exactly and never persists JWT', async () => {
    defaults();
    let body: unknown;
    server.use(
        http.post('*/api/auth/login', async ({ request }) => {
            body = await request.json();
            return HttpResponse.json(loginResult());
        }),
    );
    renderApp();
    await userEvent.type(await screen.findByLabelText('Username'), ' Admin ');
    await userEvent.type(screen.getByLabelText('Password'), ' secret ');
    await userEvent.click(screen.getByRole('button', { name: 'Sign in' }));
    await screen.findByRole('heading', { name: 'Overview' });
    expect(body).toEqual({ username: ' Admin ', password: ' secret ' });
    expect(localStorage.length).toBe(1);
    expect(sessionStorage.length).toBe(0);
});
it('malformed successful login stays signed out and clears password', async () => {
    defaults();
    server.use(http.post('*/api/auth/login', () => HttpResponse.json({ accessToken: 'fixture-rest-jwt' })));
    renderApp();
    await userEvent.type(await screen.findByLabelText('Username'), 'Admin');
    await userEvent.type(screen.getByLabelText('Password'), 'secret');
    await userEvent.click(screen.getByRole('button', { name: 'Sign in' }));
    expect(await screen.findByRole('alert')).toBeVisible();
    expect(screen.getByLabelText('Password')).toHaveValue('');
    expect(screen.queryByRole('link', { name: 'Accounts' })).toBeNull();
});
