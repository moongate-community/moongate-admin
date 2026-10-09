import { useQuery } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import { listServers } from "@/api/servers";
import { ErrorState } from "@/components/shared/ErrorState";
import { Card, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";

export function ServersPage() {
  const query = useQuery({ queryKey: ["servers"], queryFn: listServers });

  return (
    <section className="flex flex-col gap-4">
      <h2 className="text-xl font-semibold">Servers</h2>
      {query.isPending ? <Skeleton className="h-20 w-full max-w-sm" /> : null}
      {query.isError ? <ErrorState error={query.error} onRetry={() => void query.refetch()} /> : null}
      {query.data ? (
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {query.data.map((item) => (
            <Link key={item.id} to={`/servers/${encodeURIComponent(item.id)}`}>
              <Card className="hover:bg-accent">
                <CardHeader>
                  <CardTitle>{item.label}</CardTitle>
                  <p className="text-sm text-muted-foreground">{item.id}</p>
                </CardHeader>
              </Card>
            </Link>
          ))}
        </div>
      ) : null}
    </section>
  );
}
