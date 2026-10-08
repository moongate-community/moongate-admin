import { useState } from 'react';
import { Server, Globe, Layers } from 'lucide-react';
import { useAuth } from '../../app/AuthProvider';
import { useResource } from '../../app/useResource';
import type { ServerSummary, ServerInfo } from '../../lib/api/types';
import { Card, CardContent, CardHeader, CardTitle } from '../../components/ui/card';
import { Button } from '../../components/ui/button';
import { Badge } from '../../components/ui/badge';
import { Skeleton } from '../../components/ui/skeleton';
import { ApiErrorAlert } from '../../components/ApiErrorAlert';
import { formatUptime } from './uptime';
function Information({ id, label }: { id: string; label: string }) {
    const auth = useAuth();
    const resource = useResource(
        async (signal) =>
            (await auth.authorizedRequest<ServerInfo>('/api/servers/' + encodeURIComponent(id), { signal })).data,
        [id, auth.generation],
    );
    return (
        <Card>
            <CardHeader className="flex flex-row items-center justify-between gap-4">
                <div>
                    <CardTitle>{label}</CardTitle>
                    <p className="text-sm text-muted-foreground mt-2">Information from the selected server</p>
                </div>
                <Button variant="outline" disabled={resource.loading} onClick={resource.reload}>
                    Refresh information
                </Button>
            </CardHeader>
            <CardContent>
                {resource.loading ? (
                    <div role="status" className="space-y-4">
                        <span className="sr-only">Loading server information</span>
                        <Skeleton className="h-8 w-48" />
                        <Skeleton className="h-24 w-full" />
                    </div>
                ) : resource.error ? (
                    <ApiErrorAlert error={resource.error} />
                ) : (
                    resource.data && (
                        <>
                            <div className="flex gap-3 items-center mb-7">
                                <span className="size-2 rounded-full bg-success" />
                                <span className="text-sm text-success">Information received</span>
                                <Badge variant="secondary" className="capitalize ml-auto">
                                    {resource.data.mode}
                                </Badge>
                            </div>
                            <dl className="grid sm:grid-cols-2 gap-x-8 gap-y-6">
                                {[
                                    ['Version', resource.data.version],
                                    ['Codename', resource.data.codename],
                                    ['Instance', resource.data.instanceId],
                                    ['Realm', resource.data.realmId || 'Unavailable'],
                                    ['Uptime', formatUptime(resource.data.uptimeSeconds)],
                                ].map(([title, value]) => (
                                    <div key={title} className="min-w-0">
                                        <dt className="eyebrow mb-2">{title}</dt>
                                        <dd className="text-sm font-medium break-words">{value}</dd>
                                    </div>
                                ))}
                            </dl>
                            <p className="text-xs text-muted-foreground border-t pt-4 mt-7">
                                This response confirms API connectivity. It does not represent gameplay health.
                            </p>
                        </>
                    )
                )}
            </CardContent>
        </Card>
    );
}
export function ServerPanel({ overview = false }: { overview?: boolean }) {
    const auth = useAuth();
    const [selected, setSelected] = useState('');
    const list = useResource(
        async (signal) => (await auth.authorizedRequest<ServerSummary[]>('/api/servers', { signal })).data,
        [auth.generation],
    );
    const active = list.data?.find((e) => e.id === selected) ?? list.data?.[0];
    return (
        <div className="space-y-7">
            <div>
                <p className="eyebrow mb-2">Your Moongate worlds</p>
                <h1 className="page-title">{overview ? 'Overview' : 'Servers'}</h1>
                <p className="text-sm text-muted-foreground mt-3">
                    {overview
                        ? 'Your administration workspace, connected to Moongate.'
                        : 'Inspect a configured server and refresh its information.'}
                </p>
            </div>
            {overview && (
                <div className="grid sm:grid-cols-3 gap-4">
                    {(
                        [
                            [Server, 'Configured servers', list.data?.length ?? '—'],
                            [Globe, 'Access', auth.session?.account.accountType ?? '—'],
                            [Layers, 'Session', 'Browser only'],
                        ] as const
                    ).map(([Icon, title, value]) => (
                        <Card key={String(title)}>
                            <CardContent className="flex gap-4 items-start">
                                <Icon className="size-5 text-primary mt-1" />
                                <div>
                                    <p className="text-xs text-muted-foreground mb-2">{String(title)}</p>
                                    <p className="text-xl font-semibold capitalize">{String(value)}</p>
                                </div>
                            </CardContent>
                        </Card>
                    ))}
                </div>
            )}
            {list.loading ? (
                <p role="status">Loading configured servers…</p>
            ) : list.error ? (
                <div className="space-y-4">
                    <ApiErrorAlert error={list.error} />
                    <Button onClick={list.reload}>Retry servers</Button>
                </div>
            ) : !active ? (
                <p className="text-muted-foreground">No servers configured.</p>
            ) : (
                <>
                    <div className="max-w-sm space-y-2">
                        <label htmlFor="selected-server" className="text-sm font-medium">
                            Selected server
                        </label>
                        <select
                            id="selected-server"
                            className="native-select"
                            value={active.id}
                            onChange={(e) => setSelected(e.target.value)}
                        >
                            {list.data?.map((e) => (
                                <option key={e.id} value={e.id}>
                                    {e.label}
                                </option>
                            ))}
                        </select>
                    </div>
                    <Information key={active.id} id={active.id} label={active.label} />
                </>
            )}
        </div>
    );
}
