export type AccountType = 'regular' | 'gameMaster' | 'administrator';
export type ServerMode = 'login' | 'game' | 'standalone';
export interface AccountSummary {
    accountId: number;
    username: string;
    accountType: AccountType;
    canAccessApi: boolean;
    isLocked: boolean;
    createdAt: string;
}
export interface Credentials {
    username: string;
    password: string;
}
export interface LoginResponse {
    accessToken: string;
    tokenType: 'Bearer';
    account: AccountSummary;
    expiresAt: string;
}
export interface SessionResponse {
    account: AccountSummary;
    expiresAt: string;
}
export interface ConfigurationStatus {
    configured: boolean;
    setupAvailable: boolean;
}
export interface Endpoint {
    id: string;
    label: string;
    address: string;
}
export interface ConfigurationRequest {
    authenticationEndpointId: string;
    allowInsecureLoopback: boolean;
    endpoints: Endpoint[];
}
export interface ConfigurationResponse extends ConfigurationRequest {
    revision: string;
    reauthenticationRequired: boolean;
}
export interface ProbeRequest extends Credentials {
    configuration: ConfigurationRequest;
    endpointId?: string;
}
export interface ServerSummary {
    id: string;
    label: string;
}
export interface ServerInfo {
    version: string;
    codename: string;
    instanceId: string;
    realmId: string;
    mode: ServerMode;
    uptimeSeconds: string;
}
export interface ProbeResponse {
    endpointId: string;
    server: ServerInfo;
}
export interface AccountPage {
    accounts: AccountSummary[];
    nextAfterAccountId: number;
}
export interface CreateAccountRequest extends Credentials {
    accountType: AccountType;
    canAccessApi: boolean;
}
export interface ProblemDetails {
    code?: string;
    correlationId?: string;
    mutationOutcomeUnknown?: boolean;
    localSessionCleared?: boolean;
}
