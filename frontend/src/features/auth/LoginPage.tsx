import { useState, useRef } from 'react';
import { useAuth } from '../../app/AuthProvider';
import { validateCredentials } from '../../lib/api/validation';
import { isAbort } from '../../lib/api/errors';
import { FormField } from '../../components/FormField';
import { ApiErrorAlert } from '../../components/ApiErrorAlert';
import { Button } from '../../components/ui/button';
export function LoginPage() {
    const auth = useAuth();
    const [username, setUsername] = useState('');
    const [password, setPassword] = useState('');
    const [busy, setBusy] = useState(false);
    const [error, setError] = useState<unknown>();
    const [errors, setErrors] = useState<Record<string, string>>({});
    const dispatched = useRef(false);
    async function submit() {
        if (dispatched.current) return;
        const invalid = validateCredentials({ username, password });
        setErrors(invalid);
        if (Object.keys(invalid).length) return;
        dispatched.current = true;
        setBusy(true);
        setError(undefined);
        try {
            await auth.login({ username, password });
        } catch (e) {
            if (!isAbort(e)) setError(e);
        } finally {
            setPassword('');
            dispatched.current = false;
            setBusy(false);
        }
    }
    return (
        <form
            className="space-y-5"
            onSubmit={(e) => {
                e.preventDefault();
                void submit();
            }}
        >
            <h1 className="text-2xl font-semibold">Sign in</h1>
            <p className="text-muted-foreground text-sm">
                Connect with your Moongate account. Reloading this page ends your browser session.
            </p>
            {auth.notice && (
                <p role="status" className="text-sm">
                    {auth.notice}
                </p>
            )}
            <FormField
                label="Username"
                value={username}
                disabled={busy}
                error={errors.username}
                onChange={(e) => setUsername(e.target.value)}
                autoComplete="username"
            />
            <FormField
                label="Password"
                type="password"
                value={password}
                disabled={busy}
                error={errors.password}
                onChange={(e) => setPassword(e.target.value)}
                autoComplete="current-password"
            />
            {!!error && <ApiErrorAlert error={error} />}
            <Button type="submit" className="w-full" size="lg" disabled={busy}>
                {busy ? 'Signing in…' : 'Sign in'}
            </Button>
        </form>
    );
}
