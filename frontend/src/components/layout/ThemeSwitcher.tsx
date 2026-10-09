import { useEffect, useState } from "react";
import { applyTheme, getTheme, setTheme, watchSystemTheme, type ThemeChoice } from "@/theme";

export function ThemeSwitcher() {
  const [choice, setChoice] = useState<ThemeChoice>(getTheme);

  useEffect(() => {
    if (choice !== "system") {
      return;
    }
    return watchSystemTheme(() => applyTheme("system"));
  }, [choice]);

  return (
    <select
      aria-label="Theme"
      className="h-8 rounded-md border border-input bg-background px-2 text-sm"
      value={choice}
      onChange={(event) => {
        const next = event.target.value as ThemeChoice;
        setChoice(next);
        setTheme(next);
      }}
    >
      <option value="dark">Dark</option>
      <option value="light">Light</option>
      <option value="system">System</option>
    </select>
  );
}
