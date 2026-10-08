import { createRoot } from 'react-dom/client';
import { ThemeProvider, useTheme } from './app/ThemeProvider';
import { Card, CardContent, CardHeader, CardTitle } from './components/ui/card';
import { Button } from './components/ui/button';
import { Input } from './components/ui/input';
import './index.css';
function Foundation() {
    const { theme, setTheme } = useTheme();
    return (
        <main className="min-h-screen grid place-items-center p-6">
            <Card className="w-full max-w-md">
                <CardHeader>
                    <img src="/moongate-mark.png" alt="" className="size-20 mx-auto" />
                    <CardTitle>Moongate Admin</CardTitle>
                </CardHeader>
                <CardContent className="space-y-4">
                    <label htmlFor="server">Server name</label>
                    <Input id="server" placeholder="Your Moongate server" />
                    <Button onClick={() => setTheme(theme === 'dark' ? 'light' : 'dark')}>Switch theme</Button>
                </CardContent>
            </Card>
        </main>
    );
}
createRoot(document.getElementById('root')!).render(
    <ThemeProvider>
        <Foundation />
    </ThemeProvider>,
);
