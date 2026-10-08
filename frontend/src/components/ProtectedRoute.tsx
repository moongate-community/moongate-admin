import type { ReactNode } from 'react';
import { Navigate } from 'react-router';
import { useAuth } from '../app/AuthProvider';
export function ProtectedRoute({ children, administrator = false }: { children: ReactNode; administrator?: boolean }) {
    const { session } = useAuth();
    if (!session) return <Navigate to="/login" replace />;
    if (administrator && session.account.accountType !== 'administrator') return <Navigate to="/" replace />;
    return children;
}
