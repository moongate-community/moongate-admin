import { Outlet } from "react-router-dom";
import { useAuth } from "@/auth/useAuth";
import { ThemeSwitcher } from "@/components/layout/ThemeSwitcher";
import { MainNav } from "@/components/layout/MainNav";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";

export function AppShell() {
  const { account, logout } = useAuth();
  return (
    <div className="flex min-h-svh flex-col">
      <header className="flex flex-wrap items-center justify-between gap-3 border-b px-4 py-3">
        <div className="flex flex-wrap items-center gap-4">
          <img
            src="/moongate_mark.png"
            alt="Moongate"
            className="size-8 [image-rendering:pixelated]"
          />
          <h1 className="text-lg font-semibold">Moongate Admin</h1>
          <MainNav />
        </div>
        <div className="flex items-center gap-3 text-sm">
          <ThemeSwitcher />
          <span className="font-medium">{account?.username}</span>
          <Badge variant="secondary">{account?.accountType}</Badge>
          <Button variant="outline" size="sm" onClick={() => void logout()}>
            Sign out
          </Button>
        </div>
      </header>
      <main className="flex-1 p-4 md:p-6">
        <Outlet />
      </main>
    </div>
  );
}
