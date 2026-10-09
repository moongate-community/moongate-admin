import { Navigate, Outlet } from "react-router-dom";
import { useAuth } from "@/auth/useAuth";

export function RequireAdministrator() {
  const { isAdministrator } = useAuth();
  if (!isAdministrator) {
    return <Navigate to="/servers" replace />;
  }
  return <Outlet />;
}
