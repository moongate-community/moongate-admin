import { AccountSafetyProvider } from '../features/accounts/AccountSafetyProvider';
import { useState } from 'react';
import { NavLink, Outlet } from 'react-router';
import { Moon, Sun, Menu, LayoutDashboard, Server, SlidersHorizontal, Users, LogOut } from 'lucide-react';
import { useAuth } from '../app/AuthProvider';
import { useTheme } from '../app/ThemeProvider';
import { Button } from './ui/button';
import { Sheet, SheetContent, SheetHeader, SheetTitle, SheetDescription } from './ui/sheet';
export function AppShell() {
    const auth = useAuth();
    const { theme, setTheme } = useTheme();
    const [open, setOpen] = useState(false);
    const links = [
        ['/', 'Overview', LayoutDashboard],
        ['/servers', 'Servers', Server],
        ...(auth.session?.account.accountType === 'administrator'
            ? [
                  ['/connections', 'Connections', SlidersHorizontal],
                  ['/accounts', 'Accounts', Users],
              ]
            : []),
    ] as const;
    const navigation = (
        <nav aria-label="Main navigation" className="space-y-1">
            {links.map(([path, label, Icon]) => (
                <NavLink
                    key={path as string}
                    to={path as string}
                    end
                    onClick={() => setOpen(false)}
                    className={({ isActive }) =>
                        'flex items-center gap-3 rounded-lg px-3 py-2.5 text-sm transition-colors ' +
                        (isActive
                            ? 'bg-sidebar-accent text-sidebar-accent-foreground'
                            : 'text-muted-foreground hover:bg-sidebar-accent/60 hover:text-foreground')
                    }
                >
                    <Icon className="size-4" />
                    {label as string}
                </NavLink>
            ))}
        </nav>
    );
    const brand = (
        <div className="flex items-center gap-3 mb-9">
            <img src="/moongate-mark.png" alt="" className="size-11 pixel-art rounded" />
            <div>
                <span className="font-semibold block">Moongate</span>
                <span className="eyebrow">Administration</span>
            </div>
        </div>
    );
    return (
        <AccountSafetyProvider>
            <div className="min-h-screen">
                <aside className="hidden md:flex fixed inset-y-0 left-0 w-60 flex-col bg-sidebar border-r border-sidebar-border p-5">
                    {brand}
                    {navigation}
                    <p className="mt-auto text-xs text-muted-foreground pt-6 border-t border-sidebar-border">
                        One gateway. Your worlds.
                    </p>
                </aside>
                <div className="md:pl-60">
                    <header className="h-18 border-b bg-background/90 flex items-center justify-between gap-3 px-4 sm:px-8">
                        <div className="flex items-center gap-3">
                            <Button
                                className="md:hidden"
                                variant="ghost"
                                size="icon"
                                aria-label="Open navigation"
                                onClick={() => setOpen(true)}
                            >
                                <Menu />
                            </Button>
                            <span className="text-sm text-muted-foreground">Moongate Admin</span>
                        </div>
                        <div className="flex items-center gap-3">
                            <div className="hidden sm:block text-right">
                                <p className="text-sm font-medium">{auth.session?.account.username}</p>
                                <p className="text-xs text-muted-foreground capitalize">
                                    {auth.session?.account.accountType}
                                </p>
                            </div>
                            <Button
                                variant="ghost"
                                size="icon"
                                aria-label={theme === 'dark' ? 'Switch to light theme' : 'Switch to dark theme'}
                                onClick={() => setTheme(theme === 'dark' ? 'light' : 'dark')}
                            >
                                {theme === 'dark' ? <Sun /> : <Moon />}
                            </Button>
                            <Button variant="outline" aria-label="Sign out" onClick={() => void auth.logout()}>
                                <LogOut />
                                <span className="hidden sm:inline">Sign out</span>
                            </Button>
                        </div>
                    </header>
                    <main className="mx-auto max-w-7xl p-4 sm:p-8">
                        <Outlet />
                    </main>
                </div>
                <Sheet open={open} onOpenChange={setOpen}>
                    <SheetContent side="left">
                        <SheetHeader>
                            <SheetTitle>Moongate Admin</SheetTitle>
                            <SheetDescription>Administration navigation</SheetDescription>
                        </SheetHeader>
                        <div className="p-5">{navigation}</div>
                    </SheetContent>
                </Sheet>
            </div>
        </AccountSafetyProvider>
    );
}
