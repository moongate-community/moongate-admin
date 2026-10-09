export type ThemeChoice = "dark" | "light" | "system";

const STORAGE_KEY = "moongate-admin-theme";
const CHOICES: readonly ThemeChoice[] = ["dark", "light", "system"];

export function getTheme(): ThemeChoice {
  try {
    const stored = localStorage.getItem(STORAGE_KEY);
    if (stored !== null && (CHOICES as readonly string[]).includes(stored)) {
      return stored as ThemeChoice;
    }
  } catch {
    // Storage can be blocked; the default applies.
  }
  return "dark";
}

export function applyTheme(choice: ThemeChoice = getTheme()): void {
  const dark = choice === "dark" || (choice === "system" && window.matchMedia("(prefers-color-scheme: dark)").matches);
  document.documentElement.classList.toggle("dark", dark);
  document.documentElement.style.colorScheme = dark ? "dark" : "light";
}

export function setTheme(choice: ThemeChoice): void {
  try {
    localStorage.setItem(STORAGE_KEY, choice);
  } catch {
    // The choice still applies for this page view.
  }
  applyTheme(choice);
}

export function watchSystemTheme(onChange: () => void): () => void {
  const query = window.matchMedia("(prefers-color-scheme: dark)");
  query.addEventListener("change", onChange);
  return () => query.removeEventListener("change", onChange);
}
