import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { expect, it } from 'vitest';
import { ThemeProvider, useTheme } from './ThemeProvider';
function Toggle() {
    const { theme, setTheme } = useTheme();
    return <button onClick={() => setTheme(theme === 'dark' ? 'light' : 'dark')}>Switch theme</button>;
}
it('defaults to dark and persists an explicit light choice', async () => {
    render(
        <ThemeProvider>
            <Toggle />
        </ThemeProvider>,
    );
    expect(document.documentElement).toHaveClass('dark');
    await userEvent.click(screen.getByRole('button', { name: 'Switch theme' }));
    expect(document.documentElement).toHaveClass('light');
    expect(localStorage.getItem('moongate-admin-theme')).toBe('light');
});
