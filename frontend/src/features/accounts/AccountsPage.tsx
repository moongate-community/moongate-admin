import { useState } from 'react';
import { useAuth } from '../../app/AuthProvider';
import { useResource } from '../../app/useResource';
import type { AccountPage } from '../../lib/api/types';
import { Card, CardContent } from '../../components/ui/card';
import { Button } from '../../components/ui/button';
import { Badge } from '../../components/ui/badge';
import { Table, TableHeader, TableBody, TableHead, TableRow, TableCell } from '../../components/ui/table';
import { ApiErrorAlert } from '../../components/ApiErrorAlert';
import { CreateAccountDialog } from './CreateAccountDialog';
import { RevokeSessionsDialog } from './RevokeSessionsDialog';
import { ApiError } from '../../lib/api/errors';
const roles = { regular: 'Regular', gameMaster: 'Game Master', administrator: 'Administrator' };
export function AccountsPage() {
    const auth = useAuth();
    const [size, setSize] = useState(50);
    const [cursors, setCursors] = useState([0]);
    const [notice, setNotice] = useState('');
    const [uncertain, setUncertain] = useState('');
    const cursor = cursors.at(-1)!;
    const page = useResource(
        async (signal) => {
            const result = (
                await auth.authorizedRequest<AccountPage>('/api/accounts?pageSize=' + size + '&afterAccountId=' + cursor, {
                    signal,
                })
            ).data;
            if (
                !result ||
                !Array.isArray(result.accounts) ||
                !Number.isInteger(result.nextAfterAccountId) ||
                result.nextAfterAccountId < 0 ||
                result.nextAfterAccountId > 4294967295
            )
                throw new ApiError(0, { code: 'invalid_response' });
            return result;
        },
        [size, cursor, auth.generation],
    );
    function verify() {
        setCursors([0]);
        page.reload();
    }
    return (
        <div className="space-y-6">
            <div className="flex flex-wrap justify-between gap-4 items-start">
                <div>
                    <p className="eyebrow mb-2">Administration</p>
                    <h1 className="page-title">Accounts</h1>
                    <p className="text-sm text-muted-foreground mt-3">Manage account access and administrative sessions.</p>
                </div>
                <CreateAccountDialog
                    disabled={!!uncertain}
                    onUnknown={setUncertain}
                    onVerify={verify}
                    onCreated={(account) => {
                        setNotice('Account created: ' + account.username.trim());
                        verify();
                    }}
                />
            </div>
            {notice && (
                <p role="status" className="text-success text-sm">
                    {notice}
                </p>
            )}
            {uncertain && (
                <div role="alert" className="rounded-lg border p-4 space-y-3">
                    <p className="text-warning text-sm">
                        Verify the account list before trying again. Username:{' '}
                        <span className="font-medium">{uncertain}</span>
                    </p>
                    <Button variant="outline" disabled={page.loading || !!page.error} onClick={() => setUncertain('')}>
                        I verified the account list
                    </Button>
                </div>
            )}
            <div className="flex items-end justify-between gap-4">
                <label className="space-y-2 text-sm">
                    Page size
                    <select
                        aria-label="Page size"
                        className="native-select w-24"
                        value={size}
                        onChange={(e) => {
                            setSize(Number(e.target.value));
                            setCursors([0]);
                        }}
                    >
                        {[25, 50, 100, 200].map((s) => (
                            <option key={s} value={s}>
                                {s}
                            </option>
                        ))}
                    </select>
                </label>
                <Button variant="outline" disabled={page.loading} onClick={page.reload}>
                    Refresh accounts
                </Button>
            </div>
            <Card>
                <CardContent className="p-0">
                    {page.loading ? (
                        <p role="status" className="p-6 text-muted-foreground">
                            Loading accounts…
                        </p>
                    ) : page.error ? (
                        <div className="p-6">
                            <ApiErrorAlert error={page.error} />
                        </div>
                    ) : (
                        page.data && (
                            <Table>
                                <TableHeader>
                                    <TableRow>
                                        <TableHead>ID</TableHead>
                                        <TableHead>Username</TableHead>
                                        <TableHead>Role</TableHead>
                                        <TableHead>API access</TableHead>
                                        <TableHead>Lock</TableHead>
                                        <TableHead>Created</TableHead>
                                        <TableHead>
                                            <span className="sr-only">Actions</span>
                                        </TableHead>
                                    </TableRow>
                                </TableHeader>
                                <TableBody>
                                    {page.data.accounts.length === 0 ? (
                                        <TableRow>
                                            <TableCell colSpan={7} className="text-center text-muted-foreground py-12">
                                                No accounts on this page.
                                            </TableCell>
                                        </TableRow>
                                    ) : (
                                        page.data.accounts.map((account) => (
                                            <TableRow key={account.accountId}>
                                                <TableCell className="font-mono text-xs">{account.accountId}</TableCell>
                                                <TableCell className="font-medium">{account.username}</TableCell>
                                                <TableCell>
                                                    <Badge variant="secondary">{roles[account.accountType]}</Badge>
                                                </TableCell>
                                                <TableCell>{account.canAccessApi ? 'Enabled' : 'Disabled'}</TableCell>
                                                <TableCell>{account.isLocked ? 'Locked' : 'Unlocked'}</TableCell>
                                                <TableCell className="text-muted-foreground text-xs">
                                                    {new Date(account.createdAt).toLocaleString()}
                                                </TableCell>
                                                <TableCell>
                                                    <RevokeSessionsDialog
                                                        account={account}
                                                        onRevoked={() =>
                                                            setNotice(
                                                                'Administrative sessions revoked for ' + account.username,
                                                            )
                                                        }
                                                    />
                                                </TableCell>
                                            </TableRow>
                                        ))
                                    )}
                                </TableBody>
                            </Table>
                        )
                    )}
                </CardContent>
            </Card>
            <div className="flex items-center justify-between">
                <p className="text-xs text-muted-foreground">
                    Page {cursors.length} · {page.data?.accounts.length ?? 0} accounts
                </p>
                <div className="flex gap-2">
                    <Button
                        variant="outline"
                        disabled={page.loading || cursors.length === 1}
                        onClick={() => setCursors(cursors.slice(0, -1))}
                    >
                        Previous page
                    </Button>
                    <Button
                        variant="outline"
                        disabled={page.loading || !page.data?.nextAfterAccountId}
                        onClick={() => setCursors([...cursors, page.data!.nextAfterAccountId])}
                    >
                        Next page
                    </Button>
                </div>
            </div>
        </div>
    );
}
