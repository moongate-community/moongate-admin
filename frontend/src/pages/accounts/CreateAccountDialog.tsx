import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useRef, useState } from "react";
import { Controller, useForm } from "react-hook-form";
import { toast } from "sonner";
import { createAccount } from "@/api/accounts";
import { ApiError } from "@/api/errors";
import type { AccountType } from "@/api/types";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button, buttonVariants } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
  DialogTrigger
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Switch } from "@/components/ui/switch";
import { describeError } from "@/lib/describeError";
import { validatePassword, validateUsername } from "@/lib/limits";

interface CreateForm {
  username: string;
  password: string;
  accountType: AccountType;
  canAccessApi: boolean;
}

const defaults: CreateForm = { username: "", password: "", accountType: "regular", canAccessApi: false };

export function CreateAccountDialog() {
  const [open, setOpen] = useState(false);
  const [unknownOutcome, setUnknownOutcome] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const queryClient = useQueryClient();
  const submitting = useRef(false);
  const {
    register,
    control,
    handleSubmit,
    reset,
    formState: { errors }
  } = useForm<CreateForm>({ defaultValues: defaults });

  const mutation = useMutation({
    mutationFn: createAccount,
    onSuccess: (account) => {
      toast.success(`Account ${account.username} created.`);
      void queryClient.invalidateQueries({ queryKey: ["accounts"] });
      reset(defaults);
      setOpen(false);
    },
    onSettled: () => {
      submitting.current = false;
    },
    onError: (cause) => {
      if (cause instanceof ApiError && cause.mutationOutcomeUnknown) {
        setUnknownOutcome(true);
        setError(null);
      } else {
        setUnknownOutcome(false);
        setError(describeError(cause));
      }
    }
  });

  const onSubmit = handleSubmit((values) => {
    if (submitting.current) {
      return;
    }
    submitting.current = true;
    setError(null);
    setUnknownOutcome(false);
    mutation.mutate({
      username: values.username,
      password: values.password,
      accountType: values.accountType,
      canAccessApi: values.canAccessApi
    });
  });

  return (
    <Dialog
      open={open}
      onOpenChange={(next) => {
        setOpen(next);
        if (!next) {
          setError(null);
          setUnknownOutcome(false);
        }
      }}
    >
      <DialogTrigger className={buttonVariants()}>Create account</DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Create account</DialogTitle>
          <DialogDescription>New accounts have no API access unless you enable it.</DialogDescription>
        </DialogHeader>
        <form onSubmit={onSubmit} className="flex flex-col gap-4" noValidate>
          <div className="flex flex-col gap-2">
            <Label htmlFor="new-username">Username</Label>
            <Input
              id="new-username"
              autoComplete="off"
              {...register("username", { validate: (value) => validateUsername(value) ?? true })}
            />
            {errors.username ? <p className="text-sm text-destructive">{errors.username.message}</p> : null}
          </div>
          <div className="flex flex-col gap-2">
            <Label htmlFor="new-password">Password</Label>
            <Input
              id="new-password"
              type="password"
              autoComplete="new-password"
              {...register("password", { validate: (value) => validatePassword(value) ?? true })}
            />
            {errors.password ? <p className="text-sm text-destructive">{errors.password.message}</p> : null}
          </div>
          <div className="flex flex-col gap-2">
            <Label htmlFor="new-role">Role</Label>
            <select id="new-role" className="h-9 rounded-md border bg-background px-2" {...register("accountType")}>
              <option value="regular">regular</option>
              <option value="gameMaster">gameMaster</option>
              <option value="administrator">administrator</option>
            </select>
          </div>
          <div className="flex items-center gap-2">
            <Controller
              control={control}
              name="canAccessApi"
              render={({ field }) => (
                <Switch
                  id="new-api-access"
                  aria-label="API access"
                  checked={field.value}
                  onCheckedChange={field.onChange}
                />
              )}
            />
            <Label htmlFor="new-api-access">API access</Label>
          </div>
          {unknownOutcome ? (
            <Alert role="alert">
              <AlertDescription className="flex flex-col gap-2">
                <span>
                  The outcome is unknown: the account may or may not have been created. Check the account list before
                  trying again.
                </span>
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={() => void queryClient.invalidateQueries({ queryKey: ["accounts"] })}
                >
                  Reload accounts to check
                </Button>
              </AlertDescription>
            </Alert>
          ) : null}
          {error ? (
            <p role="alert" className="text-sm text-destructive">
              {error}
            </p>
          ) : null}
          <Button type="submit" disabled={mutation.isPending}>
            Create
          </Button>
        </form>
      </DialogContent>
    </Dialog>
  );
}
