export function formatUptime(value: string): string {
  if (!/^\d+$/.test(value)) {
    return value;
  }
  const total = BigInt(value);
  const days = total / 86400n;
  const hours = (total % 86400n) / 3600n;
  const minutes = (total % 3600n) / 60n;
  const seconds = total % 60n;
  return `${days}d ${hours}h ${minutes}m ${seconds}s`;
}
