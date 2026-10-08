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
