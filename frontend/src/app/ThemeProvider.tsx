import { createContext, useContext, useEffect, useState, type ReactNode } from 'react';
type Theme = 'dark' | 'light';
const storageKey = 'moongate-admin-theme';
const ThemeContext = createContext<{ theme: Theme; setTheme: (theme: Theme) => void } | null>(null);
export function ThemeProvider({ children }: { children: ReactNode }) {
    const [theme, setTheme] = useState<Theme>(() => {
        try {
            return localStorage.getItem(storageKey) === 'light' ? 'light' : 'dark';
        } catch {
            return 'dark';
        }
    });
    useEffect(() => {
        document.documentElement.classList.remove('dark', 'light');
        document.documentElement.classList.add(theme);
        try {
            localStorage.setItem(storageKey, theme);
        } catch {
            /* Theme remains usable without browser storage. */
        }
    }, [theme]);
    return <ThemeContext.Provider value={{ theme, setTheme }}>{children}</ThemeContext.Provider>;
}
export function useTheme() {
    const context = useContext(ThemeContext);
    if (!context) {
        throw new Error('ThemeProvider is required.');
    }
    return context;
}
