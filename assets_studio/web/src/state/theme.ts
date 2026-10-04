// The colour theme: the system's, or light or dark by choice (remembered in
// this browser). index.html applies the remembered choice before the first
// paint; this module keeps it and follows the system setting.
import { computed, signal } from "@preact/signals";

export type ThemeChoice = "auto" | "light" | "dark";

const KEY = "assets-studio.theme";
const CHOICES: readonly ThemeChoice[] = ["auto", "light", "dark"];

function remembered(): ThemeChoice {
  try {
    const value = localStorage.getItem(KEY);
    return CHOICES.includes(value as ThemeChoice)
      ? (value as ThemeChoice)
      : "auto";
  } catch {
    return "auto"; // storage blocked: the system's theme
  }
}

const systemDark = window.matchMedia("(prefers-color-scheme: dark)");
const systemIsDark = signal(systemDark.matches);
systemDark.addEventListener("change", (event) => {
  systemIsDark.value = event.matches;
});

export const themeChoice = signal<ThemeChoice>(remembered());

/** Whether the dark palette is in effect, whichever way it was chosen. */
export const dark = computed(() =>
  themeChoice.value === "auto"
    ? systemIsDark.value
    : themeChoice.value === "dark",
);

function apply(choice: ThemeChoice) {
  const root = document.documentElement;
  if (choice === "auto") delete root.dataset.theme;
  else root.dataset.theme = choice;
}

/** auto → light → dark → auto. */
export function nextTheme() {
  const choice =
    CHOICES[(CHOICES.indexOf(themeChoice.value) + 1) % CHOICES.length] ??
    "auto";
  themeChoice.value = choice;
  apply(choice);
  try {
    if (choice === "auto") localStorage.removeItem(KEY);
    else localStorage.setItem(KEY, choice);
  } catch {
    // Storage blocked: the choice lasts until the page is reloaded.
  }
}

/** A CSS colour variable's current value (for canvases drawn from JS). */
export function cssColor(name: string): string {
  return getComputedStyle(document.documentElement)
    .getPropertyValue(name)
    .trim();
}
