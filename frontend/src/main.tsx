import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { createBrowserRouter, RouterProvider } from "react-router-dom";
import { AppProviders } from "@/AppProviders";
import { createQueryClient } from "@/queryClient";
import { routes } from "@/routes";
import { applySystemTheme } from "@/theme";
import "./index.css";

applySystemTheme();
const router = createBrowserRouter(routes);
const queryClient = createQueryClient();

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <AppProviders queryClient={queryClient}>
      <RouterProvider router={router} />
    </AppProviders>
  </StrictMode>
);
