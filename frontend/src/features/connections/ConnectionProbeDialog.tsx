import { useState, useRef, useEffect } from 'react';
import type { ConfigurationRequest, ProbeRequest, ProbeResponse } from '../../lib/api/types';
import { validateCatalog, validateCredentials } from '../../lib/api/validation';
import { isAbort } from '../../lib/api/errors';
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription } from '../../components/ui/dialog';
import { Button } from '../../components/ui/button';
import { FormField } from '../../components/FormField';
import { ApiErrorAlert } from '../../components/ApiErrorAlert';
export function ConnectionProbeDialog({
    configuration,
    onProbe,
    pending = false,
}: {
    configuration: ConfigurationRequest;
    onProbe: (request: ProbeRequest, signal: AbortSignal) => Promise<ProbeResponse>;
    pending?: boolean;
}) {
    const [open, setOpen] = useState(false);
    const [username, setUsername] = useState('');
    const [password, setPassword] = useState('');
    const [endpointId, setEndpointId] = useState(configuration.authenticationEndpointId);
    const [busy, setBusy] = useState(false);
    const [error, setError] = useState<unknown>();
    const [validation, setValidation] = useState<Record<string, string>>({});
    const [result, setResult] = useState<ProbeResponse>();
    const controller = useRef<AbortController | null>(null);
    useEffect(() => () => controller.current?.abort(), []);
    function changeOpen(value: boolean) {
        controller.current?.abort();
        setPassword('');
        setUsername('');
        setError(undefined);
        setResult(undefined);
        setBusy(false);
        setOpen(value);
        if (value) setEndpointId(configuration.authenticationEndpointId);
    }
    async function probe() {
        if (controller.current && !controller.current.signal.aborted && busy) return;
        const errors = { ...validateCatalog(configuration), ...validateCredentials({ username, password }) };
        setValidation(errors);
        if (Object.keys(errors).length) return;
        const active = new AbortController();
        controller.current = active;
        setBusy(true);
        setError(undefined);
        setResult(undefined);
        try {
            const data = await onProbe({ configuration, endpointId, username, password }, active.signal);
            if (!active.signal.aborted) setResult(data);
        } catch (e) {
            if (!active.signal.aborted && !isAbort(e)) setError(e);
        } finally {
            setPassword('');
            if (!active.signal.aborted) setBusy(false);
        }
    }
    return (
        <>
            <Button type="button" variant="outline" disabled={pending} onClick={() => changeOpen(true)}>
                Test connection
            </Button>
            <Dialog open={open} onOpenChange={changeOpen}>
                <DialogContent>
                    <DialogHeader>
                        <DialogTitle>Test connection</DialogTitle>
                        <DialogDescription>
                            Use temporary credentials. This test does not save your connections.
                        </DialogDescription>
                    </DialogHeader>
                    <label className="space-y-2 text-sm">
                        Server
                        <select
                            className="native-select"
                            aria-label="Probe server"
                            value={endpointId}
                            disabled={busy}
                            onChange={(e) => setEndpointId(e.target.value)}
                        >
                            {configuration.endpoints
                                .filter((e) => e.id)
                                .map((e) => (
                                    <option key={e.id} value={e.id}>
                                        {e.label || e.id}
                                    </option>
                                ))}
                        </select>
                    </label>
                    <FormField
                        label="Probe username"
                        value={username}
                        disabled={busy}
                        error={validation.username}
                        onChange={(e) => setUsername(e.target.value)}
                        autoComplete="off"
                    />
                    <FormField
                        label="Probe password"
                        type="password"
                        value={password}
                        disabled={busy}
                        error={validation.password}
                        onChange={(e) => setPassword(e.target.value)}
                        autoComplete="off"
                    />
                    {Object.keys(validation).some((k) => k !== 'username' && k !== 'password') && (
                        <p role="alert">Check the connection settings before testing.</p>
                    )}
                    {!!error && <ApiErrorAlert error={error} />}
                    {result && (
                        <div role="status" className="rounded-lg border p-4 text-sm">
                            <p className="text-success font-medium">Connection test succeeded</p>
                            <p>{result.server.codename}</p>
                            <p>
                                Version {result.server.version} · {result.server.mode}
                            </p>
                        </div>
                    )}
                    <div className="flex justify-end gap-2">
                        <Button variant="outline" onClick={() => changeOpen(false)}>
                            Close test
                        </Button>
                        <Button disabled={busy} onClick={() => void probe()}>
                            {busy ? 'Testing…' : 'Run test'}
                        </Button>
                    </div>
                </DialogContent>
            </Dialog>
        </>
    );
}
