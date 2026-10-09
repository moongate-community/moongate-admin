import { Navigate, type RouteObject } from "react-router-dom";
import { RequireAdministrator } from "@/auth/RequireAdministrator";
import { RequireAuth } from "@/auth/RequireAuth";
import { AppShell } from "@/components/layout/AppShell";
import { AccountsPage } from "@/pages/accounts/AccountsPage";
import { LoginPage } from "@/pages/login/LoginPage";
import { NotFoundPage } from "@/pages/NotFoundPage";
import { ServerDetailPage } from "@/pages/servers/ServerDetailPage";
import { ServersPage } from "@/pages/servers/ServersPage";

export const routes: RouteObject[] = [
  { path: "/login", element: <LoginPage /> },
  {
    element: <RequireAuth />,
    children: [
      {
        element: <AppShell />,
        children: [
          { index: true, element: <Navigate to="/servers" replace /> },
          { path: "servers", element: <ServersPage /> },
          { path: "servers/:id", element: <ServerDetailPage /> },
          {
            element: <RequireAdministrator />,
            children: [{ path: "accounts", element: <AccountsPage /> }]
          }
        ]
      }
    ]
  },
  { path: "*", element: <NotFoundPage /> }
];
