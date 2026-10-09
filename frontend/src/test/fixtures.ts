import type { AccountSummary } from "@/api/types";

export const adminAccount: AccountSummary = {
  accountId: 7,
  username: "admin",
  accountType: "administrator",
  canAccessApi: true,
  isLocked: false,
  createdAt: "2026-01-01T00:00:00Z"
};

export const regularAccount: AccountSummary = {
  accountId: 9,
  username: "player",
  accountType: "regular",
  canAccessApi: true,
  isLocked: false,
  createdAt: "2026-01-02T00:00:00Z"
};

export function makeSession(account: AccountSummary = adminAccount, minutes = 30) {
  return { token: "jwt-token", account, expiresAt: new Date(Date.now() + minutes * 60_000) };
}
