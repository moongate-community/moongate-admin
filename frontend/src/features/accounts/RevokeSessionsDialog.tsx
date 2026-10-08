import { useState, useRef, useEffect } from 'react';
import { useAuth } from '../../app/AuthProvider';
import { isAbort } from '../../lib/api/errors';
import type { AccountSummary } from '../../lib/api/types';
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
import { Button } from '../../components/ui/button';
import { ApiErrorAlert } from '../../components/ApiErrorAlert';
export function RevokeSessionsDialog({ account, onRevoked }: { account: AccountSummary; onRevoked: () => void }) {
    const auth = useAuth();
    const [open, setOpen] = useState(false);
    const [busy, setBusy] = useState(false);
    const [error, setError] = useState<unknown>();
    const pending = useRef(false);
    const controller = useRef(new AbortController());
    useEffect(() => () => controller.current.abort(), []);
    async function revoke() {
        if (pending.current) return;
        pending.current = true;
        setBusy(true);
        setError(undefined);
        try {
            await auth.authorizedRequest('/api/accounts/' + account.accountId + '/revoke-sessions', {
                method: 'POST',
                signal: controller.current.signal,
            });
            if (account.accountId === auth.session?.account.accountId)
                auth.clearSession('Your administrative sessions were revoked. Sign in again.');
            else {
                setOpen(false);
                onRevoked();
            }
        } catch (e) {
            if (!isAbort(e)) setError(e);
        } finally {
            pending.current = false;
            setBusy(false);
        }
    }
    return (
        <>
            <Button
                variant="ghost"
                size="sm"
                aria-label={'Revoke sessions for ' + account.username}
                onClick={() => {
                    setError(undefined);
                    setOpen(true);
                }}
            >
                Revoke sessions
            </Button>
            <AlertDialog
                open={open}
                onOpenChange={(value) => {
                    if (!pending.current) setOpen(value);
                }}
            >
                <AlertDialogContent>
                    <AlertDialogHeader>
                        <AlertDialogTitle>Revoke administrative sessions for {account.username}</AlertDialogTitle>
                        <AlertDialogDescription>
                            This account's administrative sessions will end. Gameplay connections are not affected. Revoking
                            your own sessions signs you out of this panel.
                        </AlertDialogDescription>
                    </AlertDialogHeader>
                    {!!error && <ApiErrorAlert error={error} />}
                    <AlertDialogFooter>
                        <AlertDialogCancel disabled={busy}>Cancel</AlertDialogCancel>
                        <AlertDialogAction
                            disabled={busy}
                            onClick={(e) => {
                                e.preventDefault();
                                void revoke();
                            }}
                        >
                            {busy ? 'Revoking…' : 'Confirm revocation'}
                        </AlertDialogAction>
                    </AlertDialogFooter>
                </AlertDialogContent>
            </AlertDialog>
        </>
    );
}
