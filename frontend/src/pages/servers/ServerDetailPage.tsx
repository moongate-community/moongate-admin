import { useQuery } from "@tanstack/react-query";
import { Link, useParams } from "react-router-dom";
import { getServer } from "@/api/servers";
import { ErrorState } from "@/components/shared/ErrorState";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { formatUptime } from "@/lib/formatUptime";

export function ServerDetailPage() {
  const { id = "" } = useParams();
  const query = useQuery({ queryKey: ["servers", id], queryFn: () => getServer(id) });

  return (
    <section className="flex flex-col gap-4">
      <Link to="/servers" className="text-sm underline">
        All servers
      </Link>
      <h2 className="text-xl font-semibold">Server {id}</h2>
      {query.isPending ? <Skeleton className="h-40 w-full max-w-md" /> : null}
      {query.isError ? <ErrorState error={query.error} onRetry={() => void query.refetch()} /> : null}
      {query.data ? (
        <Card className="max-w-md">
          <CardHeader>
            <CardTitle>{query.data.codename}</CardTitle>
          </CardHeader>
          <CardContent>
            <dl className="grid grid-cols-[auto_1fr] gap-x-4 gap-y-2 text-sm">
              <dt className="text-muted-foreground">Version</dt>
              <dd>{query.data.version}</dd>
              <dt className="text-muted-foreground">Mode</dt>
              <dd>{query.data.mode}</dd>
              <dt className="text-muted-foreground">Instance</dt>
              <dd>{query.data.instanceId}</dd>
              <dt className="text-muted-foreground">Realm</dt>
              <dd>{query.data.realmId}</dd>
              <dt className="text-muted-foreground">Uptime</dt>
              <dd>{formatUptime(query.data.uptimeSeconds)}</dd>
            </dl>
          </CardContent>
        </Card>
      ) : null}
    </section>
  );
}
