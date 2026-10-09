import { useQuery } from "@tanstack/react-query";
import { useState } from "react";
import { listAccounts } from "@/api/accounts";
import { ErrorState } from "@/components/shared/ErrorState";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Label } from "@/components/ui/label";
import { Skeleton } from "@/components/ui/skeleton";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";

const PAGE_SIZES = [25, 50, 100, 200];

export function AccountsPage() {
  const [pageSize, setPageSize] = useState(50);
  const [cursors, setCursors] = useState<number[]>([0]);
  const after = cursors[cursors.length - 1];
  const query = useQuery({
    queryKey: ["accounts", pageSize, after],
    queryFn: () => listAccounts(pageSize, after)
  });
  const page = query.data;

  return (
    <section className="flex flex-col gap-4">
      <div className="flex items-center justify-between">
        <h2 className="text-xl font-semibold">Accounts</h2>
      </div>
      {query.isPending ? <Skeleton className="h-40 w-full" /> : null}
      {query.isError ? <ErrorState error={query.error} onRetry={() => void query.refetch()} /> : null}
      {page ? (
        <>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>ID</TableHead>
                <TableHead>Username</TableHead>
                <TableHead>Role</TableHead>
                <TableHead>API access</TableHead>
                <TableHead>Locked</TableHead>
                <TableHead>Created</TableHead>
                <TableHead />
              </TableRow>
            </TableHeader>
            <TableBody>
              {page.accounts.map((account) => (
                <TableRow key={account.accountId}>
                  <TableCell>{account.accountId}</TableCell>
                  <TableCell>{account.username}</TableCell>
                  <TableCell>{account.accountType}</TableCell>
                  <TableCell>{account.canAccessApi ? <Badge>yes</Badge> : <Badge variant="secondary">no</Badge>}</TableCell>
                  <TableCell>{account.isLocked ? <Badge variant="destructive">locked</Badge> : "no"}</TableCell>
                  <TableCell>{new Date(account.createdAt).toLocaleString()}</TableCell>
                  <TableCell />
                </TableRow>
              ))}
            </TableBody>
          </Table>
          <div className="flex flex-wrap items-center justify-between gap-3">
            <div className="flex items-center gap-2 text-sm">
              <Label htmlFor="page-size">Page size</Label>
              <select
                id="page-size"
                className="h-9 rounded-md border bg-background px-2"
                value={pageSize}
                onChange={(event) => {
                  setPageSize(Number(event.target.value));
                  setCursors([0]);
                }}
              >
                {PAGE_SIZES.map((size) => (
                  <option key={size} value={size}>
                    {size}
                  </option>
                ))}
              </select>
            </div>
            <div className="flex gap-2">
              <Button variant="outline" disabled={cursors.length === 1} onClick={() => setCursors([0])}>
                First page
              </Button>
              <Button
                variant="outline"
                disabled={page.nextAfterAccountId === 0}
                onClick={() => setCursors([...cursors, page.nextAfterAccountId])}
              >
                Next page
              </Button>
            </div>
          </div>
        </>
      ) : null}
    </section>
  );
}
