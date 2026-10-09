import { setupServer } from "msw/node";

export const server = setupServer();

export function api(path: string): string {
  return "*/api" + path;
}
