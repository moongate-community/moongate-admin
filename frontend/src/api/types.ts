export type AccountType = "regular" | "gameMaster" | "administrator";

export interface AccountSummary {
  accountId: number;
  username: string;
  accountType: AccountType;
  canAccessApi: boolean;
  isLocked: boolean;
  createdAt: string;
}

export interface AccountPage {
  accounts: AccountSummary[];
  nextAfterAccountId: number;
}

export interface CreateAccountInput {
  username: string;
  password: string;
  accountType?: AccountType;
  canAccessApi: boolean;
}

export interface ServerSummary {
  id: string;
  label: string;
}

export type ServerMode = "login" | "game" | "standalone";

export interface ServerInfo {
  version: string;
  codename: string;
  instanceId: string;
  realmId: string;
  mode: ServerMode;
  uptimeSeconds: string;
}

export interface LoginResponse {
  accessToken: string;
  tokenType: string;
  account: AccountSummary;
  expiresAt: string;
}

export interface SessionInfo {
  account: AccountSummary;
  expiresAt: string;
}
