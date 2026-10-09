import { QueryClient } from "@tanstack/react-query";
import { render } from "@testing-library/react";
import { createMemoryRouter, RouterProvider } from "react-router-dom";
import { AppProviders } from "@/AppProviders";
import { sessionStore } from "@/auth/sessionStore";
import { routes } from "@/routes";
import { makeSession } from "@/test/fixtures";

type SessionArg = ReturnType<typeof makeSession>;

export function renderApp(route = "/", options: { session?: SessionArg | null } = {}) {
  sessionStore.clear();
  if (options.session !== null) {
    sessionStore.setSession(options.session ?? makeSession());
  }
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } }
  });
  const router = createMemoryRouter(routes, { initialEntries: [route] });
  const utils = render(
    <AppProviders queryClient={queryClient}>
      <RouterProvider router={router} />
    </AppProviders>
  );
  return { ...utils, router, queryClient };
}
