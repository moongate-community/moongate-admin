import { useState, useRef, useEffect } from 'react';
import { useAuth } from '../../app/AuthProvider';
import { useResource } from '../../app/useResource';
import { ApiError, isAbort } from '../../lib/api/errors';
import { validateCatalog } from '../../lib/api/validation';
import type { ConfigurationRequest, ConfigurationResponse } from '../../lib/api/types';
import { CatalogEditor } from './CatalogEditor';
import { ConnectionProbeDialog } from './ConnectionProbeDialog';
import { Button } from '../../components/ui/button';
import { Card, CardContent } from '../../components/ui/card';
import { ApiErrorAlert } from '../../components/ApiErrorAlert';
import {
    AlertDialog,
    AlertDialogContent,
    AlertDialogHeader,
    AlertDialogTitle,
    AlertDialogDescription,
    AlertDialogFooter,
    AlertDialogCancel,
    AlertDialogAction,
} from '../../components/ui/alert-dialog';
export function ConnectionsPage() {
    const auth = useAuth();
    const resource = useResource(
        async (signal) => {
            const result = await auth.authorizedRequest<ConfigurationResponse>('/api/configuration', { signal });
            if (
                !result.data ||
                typeof result.data.allowInsecureLoopback !== 'boolean' ||
                typeof result.data.authenticationEndpointId !== 'string' ||
                Object.keys(validateCatalog(result.data)).length
            )
                throw new ApiError(0, { code: 'invalid_response' });
            return result;
        },
        [auth.generation],
    );
    const [draft, setDraft] = useState<ConfigurationRequest>();
    const [error, setError] = useState<unknown>();
    const [errors, setErrors] = useState<Record<string, string>>({});
    const [busy, setBusy] = useState(false);
    const [blocked, setBlocked] = useState('');
    const [confirm, setConfirm] = useState(false);
    const pending = useRef(false);
    const controller = useRef(new AbortController());
    useEffect(() => () => controller.current.abort(), []);
    useEffect(() => {
        if (resource.data) {
            const { authenticationEndpointId, allowInsecureLoopback, endpoints } = resource.data.data;
            setDraft({ authenticationEndpointId, allowInsecureLoopback, endpoints });
            setBlocked('');
            setError(undefined);
            setErrors({});
        }
    }, [resource.data]);
    const etag = resource.data?.etag;
    const validTag = !!etag && /^"[a-fA-F0-9]{32}"$/.test(etag);
    const original = resource.data?.data;
    const dirty =
        !!draft &&
        !!original &&
        JSON.stringify(draft) !==
            JSON.stringify({
                authenticationEndpointId: original.authenticationEndpointId,
                allowInsecureLoopback: original.allowInsecureLoopback,
                endpoints: original.endpoints,
            });
    async function save() {
        if (pending.current || !draft || !validTag || blocked) return;
        const invalid = validateCatalog(draft);
        setErrors(invalid);
        if (Object.keys(invalid).length) return;
        pending.current = true;
        setBusy(true);
        setError(undefined);
        try {
            await auth.authorizedRequest('/api/configuration', {
                method: 'PUT',
                body: draft,
                headers: { 'If-Match': etag! },
                signal: controller.current.signal,
            });
            auth.clearSession('Connections saved. Sign in again with the new configuration.');
        } catch (e) {
            if (isAbort(e)) return;
            setError(e);
            if (e instanceof ApiError && e.status === 412)
                setBlocked('Connections changed. Reload the latest configuration before saving.');
            if (
                !(e instanceof ApiError) ||
                e.mutationOutcomeUnknown ||
                (e.code === 'invalid_response' && (e.status === 0 || e.status < 300))
            ) {
                setBlocked('The save result is unknown. Check the latest configuration before trying again.');
                try {
                    await auth.authorizedRequest('/api/auth/session', { signal: controller.current.signal });
                } catch {
                    /* Current-session authorization handles confirmed session loss. */
                }
            }
        } finally {
            pending.current = false;
            setBusy(false);
        }
    }
    return (
        <div className="space-y-6 max-w-3xl">
            <div>
                <p className="eyebrow mb-2">Administration</p>
                <h1 className="page-title">Connections</h1>
                <p className="text-sm text-muted-foreground mt-3">
                    Manage the servers this panel can access. Saving ends the current session.
                </p>
            </div>
            {resource.loading ? (
                <p role="status">Loading connections…</p>
            ) : resource.error ? (
                <>
                    <ApiErrorAlert error={resource.error} />
                    <Button onClick={resource.reload}>Retry connections</Button>
                </>
            ) : (
                draft && (
                    <Card>
                        <CardContent className="space-y-6">
                            <div className="flex items-center justify-between">
                                <p className="text-sm text-muted-foreground">
                                    {dirty ? 'Unsaved changes' : 'Configuration loaded'}
                                </p>
                                <Button
                                    variant="outline"
                                    disabled={busy}
                                    onClick={() => (dirty || blocked ? setConfirm(true) : resource.reload())}
                                >
                                    Reload latest
                                </Button>
                            </div>
                            <CatalogEditor value={draft} onChange={setDraft} errors={errors} disabled={busy} />
                            {!validTag && (
                                <p role="alert" className="text-destructive text-sm">
                                    The configuration has no valid revision. Reload before saving.
                                </p>
                            )}
                            {!!error && <ApiErrorAlert error={error} />}
                            {blocked && (
                                <p role="status" className="text-warning text-sm">
                                    {blocked}
                                </p>
                            )}
                            <div className="flex flex-wrap justify-between gap-3">
                                <ConnectionProbeDialog
                                    configuration={draft}
                                    pending={busy}
                                    onProbe={(request, signal) => auth.probeCandidate(request, undefined, signal)}
                                />
                                <Button disabled={busy || !dirty || !validTag || !!blocked} onClick={() => void save()}>
                                    {busy ? 'Saving…' : 'Save connections'}
                                </Button>
                            </div>
                        </CardContent>
                    </Card>
                )
            )}
            <AlertDialog open={confirm} onOpenChange={setConfirm}>
                <AlertDialogContent>
                    <AlertDialogHeader>
                        <AlertDialogTitle>Discard connection changes?</AlertDialogTitle>
                        <AlertDialogDescription>
                            Reloading replaces your draft with the current saved configuration.
                        </AlertDialogDescription>
                    </AlertDialogHeader>
                    <AlertDialogFooter>
                        <AlertDialogCancel>Keep draft</AlertDialogCancel>
                        <AlertDialogAction
                            onClick={() => {
                                setConfirm(false);
                                resource.reload();
                            }}
                        >
                            Discard and reload
                        </AlertDialogAction>
                    </AlertDialogFooter>
                </AlertDialogContent>
            </AlertDialog>
        </div>
    );
}
