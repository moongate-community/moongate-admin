import { Routes, Route, Navigate, useLocation } from 'react-router';
import { AuthProvider, useAuth } from './AuthProvider';
import { useResource } from './useResource';
import { api } from '../lib/api/client';
import { parseStatus } from '../lib/api/guards';
import { SetupPage } from '../features/setup/SetupPage';
import { LoginPage } from '../features/auth/LoginPage';
import { AppShell } from '../components/AppShell';
import { ProtectedRoute } from '../components/ProtectedRoute';
import { Button } from '../components/ui/button';
import { Card, CardContent } from '../components/ui/card';
import { ApiErrorAlert } from '../components/ApiErrorAlert';
function Entry() {
    const auth = useAuth();
    const location = useLocation();
    const status = useResource(
        async (signal) => parseStatus((await api.request<unknown>('/api/configuration/status', { signal })).data),
        [],
    );
    if (status.loading)
        return (
            <div role="status" className="min-h-screen grid place-items-center text-muted-foreground">
                Connecting to Moongate Admin…
            </div>
        );
    if (status.error)
        return (
            <div className="min-h-screen grid place-items-center p-6">
                <Card className="max-w-md">
                    <CardContent className="space-y-4">
                        <h1 className="text-xl font-semibold">Unable to reach the backend</h1>
                        <ApiErrorAlert error={status.error} />
                        <Button onClick={status.reload}>Retry</Button>
                    </CardContent>
                </Card>
            </div>
        );
    const setup = !status.data?.configured;
    if (setup || !auth.session) {
        const target = setup ? '/setup' : '/login';
        return (
            <>
                {location.pathname !== target && <Navigate to={target} replace />}
                <main className="entry-page">
                    <div className="entry-brand">
                        <img src="/moongate-logo.png" className="w-48 pixel-art" alt="Moongate" />
                        <p className="text-sm text-muted-foreground mt-6">A gateway to your worlds.</p>
                    </div>
                    <Card className={'w-full ' + (setup ? 'max-w-2xl' : 'max-w-md')}>
                        <CardContent className="p-6 sm:p-8">
                            {setup ? (
                                <SetupPage available={!!status.data?.setupAvailable} onComplete={status.reload} />
                            ) : (
                                <LoginPage />
                            )}
                        </CardContent>
                    </Card>
                </main>
            </>
        );
    }
    return (
        <Routes key={auth.generation}>
            <Route
                element={
                    <ProtectedRoute>
                        <AppShell />
                    </ProtectedRoute>
                }
            >
                <Route index element={<h1 className="page-title">Overview</h1>} />
                <Route path="servers" element={<h1 className="page-title">Servers</h1>} />
                <Route
                    path="connections"
                    element={
                        <ProtectedRoute administrator>
                            <h1 className="page-title">Connections</h1>
                        </ProtectedRoute>
                    }
                />
                <Route
                    path="accounts"
                    element={
                        <ProtectedRoute administrator>
                            <h1 className="page-title">Accounts</h1>
                        </ProtectedRoute>
                    }
                />
                <Route path="login" element={<Navigate to="/" replace />} />
                <Route path="setup" element={<Navigate to="/" replace />} />
                <Route
                    path="*"
                    element={
                        <div>
                            <h1 className="page-title">Page not found</h1>
                            <p>Choose a page from the navigation.</p>
                        </div>
                    }
                />
            </Route>
        </Routes>
    );
}
export function App() {
    return (
        <AuthProvider>
            <Entry />
        </AuthProvider>
    );
}
