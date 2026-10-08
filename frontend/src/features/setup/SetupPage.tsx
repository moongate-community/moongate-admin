import { useState, useRef, useEffect } from 'react';
import { api } from '../../lib/api/client';
import { ApiError, isAbort } from '../../lib/api/errors';
import { validateCatalog } from '../../lib/api/validation';
import { useAuth } from '../../app/AuthProvider';
import { CatalogEditor, emptyCatalog } from '../connections/CatalogEditor';
import { ConnectionProbeDialog } from '../connections/ConnectionProbeDialog';
import { FormField } from '../../components/FormField';
import { ApiErrorAlert } from '../../components/ApiErrorAlert';
import { Button } from '../../components/ui/button';
export function SetupPage({ available, onComplete }: { available: boolean; onComplete: () => void }) {
    const [key, setKey] = useState('');
    const [catalog, setCatalog] = useState(emptyCatalog);
    const [errors, setErrors] = useState<Record<string, string>>({});
    const [error, setError] = useState<unknown>();
    const [busy, setBusy] = useState(false);
    const dispatched = useRef(false);
    const controller = useRef(new AbortController());
    const auth = useAuth();
    useEffect(() => () => controller.current.abort(), []);
    async function submit() {
        if (dispatched.current) return;
        const invalid = validateCatalog(catalog);
        if (!key.trim()) invalid.key = 'Enter the initial setup key.';
        setErrors(invalid);
        if (Object.keys(invalid).length) return;
        dispatched.current = true;
        setBusy(true);
        setError(undefined);
        try {
            await api.request('/api/configuration/setup', {
                method: 'POST',
                body: catalog,
                setupToken: key,
                signal: controller.current.signal,
            });
            setKey('');
            onComplete();
        } catch (e) {
            if (e instanceof ApiError && e.status === 409) {
                setKey('');
                onComplete();
            } else if (!isAbort(e)) setError(e);
        } finally {
            dispatched.current = false;
            setBusy(false);
        }
    }
    return (
        <div className="space-y-6">
            <div>
                <p className="eyebrow">Welcome to Moongate</p>
                <h1 className="text-2xl font-semibold mt-2">Initial setup</h1>
                <p className="text-sm text-muted-foreground mt-2">Configure the servers this panel will administer.</p>
            </div>
            {!available ? (
                <p role="alert">An operator must enable initial setup on the backend before you can continue.</p>
            ) : (
                <form
                    className="space-y-5"
                    onSubmit={(e) => {
                        e.preventDefault();
                        void submit();
                    }}
                >
                    <FormField
                        label="Setup key"
                        type="password"
                        value={key}
                        disabled={busy}
                        error={errors.key}
                        onChange={(e) => setKey(e.target.value)}
                        autoComplete="off"
                    />
                    <CatalogEditor value={catalog} onChange={setCatalog} errors={errors} disabled={busy} />
                    {!!error && <ApiErrorAlert error={error} />}
                    <div className="flex flex-wrap justify-between gap-3">
                        <ConnectionProbeDialog
                            configuration={catalog}
                            pending={busy}
                            onProbe={(request, signal) => auth.probeCandidate(request, key, signal)}
                        />
                        <Button type="submit" disabled={busy}>
                            {busy ? 'Saving…' : 'Save connections'}
                        </Button>
                    </div>
                </form>
            )}
        </div>
    );
}
