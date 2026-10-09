import { apiRequest } from "@/api/client";
import type { AccountPage, AccountSummary, CreateAccountInput } from "@/api/types";

export function listAccounts(pageSize: number, afterAccountId: number): Promise<AccountPage> {
  return apiRequest<AccountPage>("GET", `/accounts?pageSize=${pageSize}&afterAccountId=${afterAccountId}`);
}

export function createAccount(input: CreateAccountInput): Promise<AccountSummary> {
  const body: Record<string, unknown> = {
    username: input.username,
    password: input.password,
    canAccessApi: input.canAccessApi
  };
  if (input.accountType !== undefined) {
    body.accountType = input.accountType;
  }
  return apiRequest<AccountSummary>("POST", "/accounts", body);
}

export function revokeSessions(accountId: number): Promise<void> {
  return apiRequest<void>("POST", `/accounts/${accountId}/revoke-sessions`);
}
