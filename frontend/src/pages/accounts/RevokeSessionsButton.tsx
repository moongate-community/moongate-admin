import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { toast } from "sonner";
import { revokeSessions } from "@/api/accounts";
import type { AccountSummary } from "@/api/types";
import { useAuth } from "@/auth/useAuth";
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
  AlertDialogTrigger
} from "@/components/ui/alert-dialog";
import { buttonVariants } from "@/components/ui/button";
import { describeError } from "@/lib/describeError";

export function RevokeSessionsButton({ account }: { account: AccountSummary }) {
  const [open, setOpen] = useState(false);
  const { account: me, logout } = useAuth();
  const queryClient = useQueryClient();
  const mutation = useMutation({
    mutationFn: () => revokeSessions(account.accountId),
    onSuccess: async () => {
      if (me?.accountId === account.accountId) {
        toast.success("Your sessions were revoked.");
        await logout();
        return;
      }
      toast.success(`Sessions revoked for ${account.username}.`);
      void queryClient.invalidateQueries({ queryKey: ["accounts"] });
    },
    onError: (cause) => {
      toast.error(describeError(cause));
    }
  });

  return (
    <AlertDialog open={open} onOpenChange={setOpen}>
      <AlertDialogTrigger
        aria-label={`Revoke sessions for ${account.username}`}
        className={buttonVariants({ variant: "outline", size: "sm" })}
      >
        Revoke sessions
      </AlertDialogTrigger>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>Revoke sessions for {account.username}?</AlertDialogTitle>
          <AlertDialogDescription>
            Every administrative session of this account ends the next time it makes a request.
          </AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel>Cancel</AlertDialogCancel>
          <AlertDialogAction
            disabled={mutation.isPending}
            onClick={() => {
              if (mutation.isPending) {
                return;
              }
              setOpen(false);
              mutation.mutate();
            }}
          >
            Revoke
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}
