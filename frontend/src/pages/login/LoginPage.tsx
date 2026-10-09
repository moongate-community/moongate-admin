import { useState } from "react";
import { useForm } from "react-hook-form";
import { Navigate, useLocation } from "react-router-dom";
import { useAuth } from "@/auth/useAuth";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { describeError } from "@/lib/describeError";
import { validatePassword, validateUsername } from "@/lib/limits";

interface LoginForm {
  username: string;
  password: string;
}

export function LoginPage() {
  const { login, isAuthenticated } = useAuth();
  const location = useLocation();
  const from = (location.state as { from?: { pathname: string } } | null)?.from?.pathname ?? "/servers";
  const [error, setError] = useState<string | null>(null);
  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting }
  } = useForm<LoginForm>({ defaultValues: { username: "", password: "" } });

  if (isAuthenticated) {
    return <Navigate to={from} replace />;
  }

  const onSubmit = handleSubmit(async (values) => {
    setError(null);
    try {
      await login(values.username, values.password);
    } catch (cause) {
      setError(describeError(cause));
    }
  });

  return (
    <div className="flex min-h-svh items-center justify-center p-4">
      <Card className="w-full max-w-sm">
        <CardHeader className="items-center">
          <img
            src="/moongate_logo.png"
            alt="Moongate"
            className="mb-2 w-40 [image-rendering:pixelated]"
          />
          <CardTitle>Sign in to Moongate Admin</CardTitle>
        </CardHeader>
        <CardContent>
          <form onSubmit={onSubmit} className="flex flex-col gap-4" noValidate>
            <div className="flex flex-col gap-2">
              <Label htmlFor="username">Username</Label>
              <Input
                id="username"
                autoComplete="username"
                {...register("username", { validate: (value) => validateUsername(value) ?? true })}
              />
              {errors.username ? <p className="text-sm text-destructive">{errors.username.message}</p> : null}
            </div>
            <div className="flex flex-col gap-2">
              <Label htmlFor="password">Password</Label>
              <Input
                id="password"
                type="password"
                autoComplete="current-password"
                {...register("password", { validate: (value) => validatePassword(value) ?? true })}
              />
              {errors.password ? <p className="text-sm text-destructive">{errors.password.message}</p> : null}
            </div>
            {error ? (
              <p role="alert" className="text-sm text-destructive">
                {error}
              </p>
            ) : null}
            <Button type="submit" disabled={isSubmitting}>
              Sign in
            </Button>
          </form>
        </CardContent>
      </Card>
    </div>
  );
}
