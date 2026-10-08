export function formatUptime(seconds: string): string {
    if (typeof seconds !== 'string' || !/^\d{1,20}$/.test(seconds)) return 'Unavailable';
    const value = BigInt(seconds);
    if (value > 18446744073709551615n) return 'Unavailable';
    return `${value / 86400n}d ${(value % 86400n) / 3600n}h ${(value % 3600n) / 60n}m ${value % 60n}s`;
}
