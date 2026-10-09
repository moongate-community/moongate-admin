import { useEffect, useSyncExternalStore, type ReactNode } from "react";
import { toast } from "sonner";
import { configureApi } from "@/api/client";
import { sessionStore } from "@/auth/sessionStore";

const MAX_TIMER_MS = 2 ** 31 - 1;

configureApi({
  getToken: () => sessionStore.getState().token,
  onUnauthorized: () => {
    if (sessionStore.getState().token !== null) {
      sessionStore.clear();
      toast.error("Your session has ended. Sign in again.");
    }
  }
});

export function AuthProvider({ children }: { children: ReactNode }) {
  const { expiresAt } = useSyncExternalStore(sessionStore.subscribe, sessionStore.getState);

  useEffect(() => {
    if (expiresAt === null) {
      return;
    }
    const delay = Math.min(Math.max(expiresAt.getTime() - Date.now(), 0), MAX_TIMER_MS);
    const timer = setTimeout(() => {
      sessionStore.clear();
      toast.error("Your session has expired.");
    }, delay);
    return () => clearTimeout(timer);
  }, [expiresAt]);

  return <>{children}</>;
}
