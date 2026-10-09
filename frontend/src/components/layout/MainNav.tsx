import { NavLink } from "react-router-dom";
import { useAuth } from "@/auth/useAuth";
import { cn } from "@/lib/utils";

function NavItem({ to, children }: { to: string; children: string }) {
  return (
    <NavLink
      to={to}
      className={({ isActive }) =>
        cn(
          "rounded-md px-3 py-2 text-sm font-medium hover:bg-accent",
          isActive ? "bg-accent text-accent-foreground" : "text-muted-foreground"
        )
      }
    >
      {children}
    </NavLink>
  );
}

export function MainNav({ className }: { className?: string }) {
  const { isAdministrator } = useAuth();
  return (
    <nav aria-label="Main" className={cn("flex gap-1", className)}>
      <NavItem to="/servers">Servers</NavItem>
      {isAdministrator ? <NavItem to="/accounts">Accounts</NavItem> : null}
    </nav>
  );
}
