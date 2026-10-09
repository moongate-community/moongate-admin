import { apiRequest } from "@/api/client";
import type { ServerInfo, ServerSummary } from "@/api/types";

export function listServers(): Promise<ServerSummary[]> {
  return apiRequest<ServerSummary[]>("GET", "/servers");
}

export function getServer(id: string): Promise<ServerInfo> {
  return apiRequest<ServerInfo>("GET", `/servers/${encodeURIComponent(id)}`);
}
