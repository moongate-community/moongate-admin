import { useQueryClient } from "@tanstack/react-query";
import { useCallback, useSyncExternalStore } from "react";
import * as authApi from "@/api/auth";
import { sessionStore } from "@/auth/sessionStore";

export function useAuth() {
  const state = useSyncExternalStore(sessionStore.subscribe, sessionStore.getState);
  const queryClient = useQueryClient();

  const login = useCallback(async (username: string, password: string): Promise<void> => {
    const response = await authApi.login(username, password);
    sessionStore.setSession({
      token: response.accessToken,
      account: response.account,
      expiresAt: new Date(response.expiresAt)
    });
  }, []);

  const logout = useCallback(async (): Promise<void> => {
    try {
      await authApi.logout();
    } catch {
      // The local session is always cleared, even if the server could not confirm.
    } finally {
      sessionStore.clear();
      queryClient.clear();
    }
  }, [queryClient]);

  return {
    account: state.account,
    isAuthenticated: state.token !== null,
    isAdministrator: state.account?.accountType === "administrator",
    expiresAt: state.expiresAt,
    login,
    logout
  };
}
