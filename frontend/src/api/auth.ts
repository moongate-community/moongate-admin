import { apiRequest } from "@/api/client";
import type { LoginResponse, SessionInfo } from "@/api/types";

export function login(username: string, password: string): Promise<LoginResponse> {
  return apiRequest<LoginResponse>("POST", "/auth/login", { username, password });
}

export function logout(): Promise<void> {
  return apiRequest<void>("POST", "/auth/logout");
}

export function getSession(): Promise<SessionInfo> {
  return apiRequest<SessionInfo>("GET", "/auth/session");
}
