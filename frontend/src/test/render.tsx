import { render } from '@testing-library/react';
import { MemoryRouter } from 'react-router';
import { ThemeProvider } from '../app/ThemeProvider';
import { App } from '../app/App';
export function renderApp(path = '/') {
    return render(
        <MemoryRouter initialEntries={[path]}>
            <ThemeProvider>
                <App />
            </ThemeProvider>
        </MemoryRouter>,
    );
}

import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
export async function signIn() {
    await userEvent.type(await screen.findByLabelText('Username'), 'Admin');
    await userEvent.type(screen.getByLabelText('Password'), 'fixture-password');
    await userEvent.click(screen.getByRole('button', { name: 'Sign in' }));
    await screen.findByRole('heading', { name: 'Overview' });
}
