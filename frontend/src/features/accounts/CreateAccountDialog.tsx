import { useState, useRef, useEffect } from 'react';
import { useAuth } from '../../app/AuthProvider';
import { validateCredentials } from '../../lib/api/validation';
import { ApiError, isAbort } from '../../lib/api/errors';
import type { AccountType, AccountSummary } from '../../lib/api/types';
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription } from '../../components/ui/dialog';
import { FormField } from '../../components/FormField';
import { Button } from '../../components/ui/button';
import { ApiErrorAlert } from '../../components/ApiErrorAlert';
export function CreateAccountDialog({
    disabled,
    onCreated,
    onUnknown,
    onVerify,
}: {
    disabled: boolean;
    onCreated: (account: AccountSummary) => void;
    onUnknown: (username: string) => void;
    onVerify: () => void;
}) {
    const auth = useAuth();
    const [open, setOpen] = useState(false);
    const [username, setUsername] = useState('');
    const [password, setPassword] = useState('');
    const [accountType, setAccountType] = useState<AccountType>('regular');
    const [canAccessApi, setCanAccessApi] = useState(false);
    const [busy, setBusy] = useState(false);
    const [unknown, setUnknown] = useState(false);
    const [error, setError] = useState<unknown>();
    const [errors, setErrors] = useState<Record<string, string>>({});
    const pending = useRef(false);
    const controller = useRef(new AbortController());
    useEffect(() => () => controller.current.abort(), []);
    function changeOpen(value: boolean) {
        if (pending.current) return;
        setOpen(value);
        setPassword('');
        if (value) {
            setUsername('');
            setAccountType('regular');
            setCanAccessApi(false);
            setUnknown(false);
            setError(undefined);
            setErrors({});
        }
    }
    async function create() {
        if (pending.current || unknown || disabled) return;
        const invalid = validateCredentials({ username, password });
        setErrors(invalid);
        if (Object.keys(invalid).length) return;
        pending.current = true;
        setBusy(true);
        setError(undefined);
        try {
            const result = await auth.authorizedRequest<AccountSummary>('/api/accounts', {
                method: 'POST',
                body: { username, password, accountType, canAccessApi },
                signal: controller.current.signal,
            });
            if (!result.data || typeof result.data.username !== 'string')
                throw new ApiError(201, { code: 'invalid_response' });
            setOpen(false);
            onCreated(result.data);
        } catch (e) {
            if (!isAbort(e)) {
                setError(e);
                if (
                    !(e instanceof ApiError) ||
                    e.mutationOutcomeUnknown ||
                    (e.code === 'invalid_response' && (e.status === 0 || e.status < 300))
                ) {
                    setUnknown(true);
                    onUnknown(username);
                }
            }
        } finally {
            setPassword('');
            pending.current = false;
            setBusy(false);
        }
    }
    return (
        <>
            <Button disabled={disabled} onClick={() => changeOpen(true)}>
                Create account
            </Button>
            <Dialog open={open} onOpenChange={changeOpen}>
                <DialogContent>
                    <DialogHeader>
                        <DialogTitle>Create account</DialogTitle>
                        <DialogDescription>
                            Create a Moongate account with explicit role and API permissions.
                        </DialogDescription>
                    </DialogHeader>
                    <form
                        className="space-y-5"
                        onSubmit={(e) => {
                            e.preventDefault();
                            void create();
                        }}
                    >
                        <FormField
                            label="New username"
                            value={username}
                            disabled={busy || unknown}
                            error={errors.username}
                            onChange={(e) => setUsername(e.target.value)}
                            autoComplete="off"
                        />
                        <FormField
                            label="New password"
                            type="password"
                            value={password}
                            disabled={busy || unknown}
                            error={errors.password}
                            onChange={(e) => setPassword(e.target.value)}
                            autoComplete="new-password"
                        />
                        <label className="space-y-2 block text-sm">
                            Account role
                            <select
                                aria-label="Account role"
                                className="native-select"
                                value={accountType}
                                disabled={busy || unknown}
                                onChange={(e) => setAccountType(e.target.value as AccountType)}
                            >
                                <option value="regular">Regular</option>
                                <option value="gameMaster">Game Master</option>
                                <option value="administrator">Administrator</option>
                            </select>
                        </label>
                        <label className="flex items-center gap-3 text-sm">
                            <input
                                type="checkbox"
                                checked={canAccessApi}
                                disabled={busy || unknown}
                                onChange={(e) => setCanAccessApi(e.target.checked)}
                            />
                            Allow API access
                        </label>
                        {!!error && <ApiErrorAlert error={error} />}
                        {unknown && (
                            <p role="alert" className="text-warning text-sm">
                                The creation result is unknown. Verify the account list before trying again.
                            </p>
                        )}
                        <div className="flex flex-wrap justify-end gap-2">
                            {unknown ? (
                                <Button
                                    type="button"
                                    variant="outline"
                                    onClick={() => {
                                        changeOpen(false);
                                        onVerify();
                                    }}
                                >
                                    Verify account list
                                </Button>
                            ) : (
                                <Button type="button" variant="outline" disabled={busy} onClick={() => changeOpen(false)}>
                                    Cancel
                                </Button>
                            )}
                            <Button type="submit" disabled={busy || unknown}>
                                {busy ? 'Creating…' : 'Create'}
                            </Button>
                        </div>
                    </form>
                </DialogContent>
            </Dialog>
        </>
    );
}
