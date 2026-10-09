import { type QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { useEffect, type ReactNode } from "react";
import { AuthProvider } from "@/auth/AuthProvider";
import { sessionStore } from "@/auth/sessionStore";
import { Toaster } from "@/components/ui/sonner";

export function AppProviders({ queryClient, children }: { queryClient: QueryClient; children: ReactNode }) {
  useEffect(() => {
    let hadSession = sessionStore.getState().token !== null;
    return sessionStore.subscribe(() => {
      const hasSession = sessionStore.getState().token !== null;
      if (hadSession && !hasSession) {
        queryClient.clear();
      }
      hadSession = hasSession;
    });
  }, [queryClient]);

  return (
    <QueryClientProvider client={queryClient}>
      <AuthProvider>
        {children}
        <Toaster />
      </AuthProvider>
    </QueryClientProvider>
  );
}
