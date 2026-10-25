import {
  hrefJobs,
  hrefLibrary,
  hrefSection,
  lastSection,
  route,
} from "../state/router";
import { bestState, sections, unseenServers } from "../state/sections";
import { activeJobs } from "../state/store";
import { nextTheme, type ThemeChoice, themeChoice } from "../state/theme";
import { has, type Key, language, setLanguage, t } from "../i18n";

const THEME_LABELS: Record<ThemeChoice, Key> = {
  auto: "theme.auto",
  light: "theme.light",
  dark: "theme.dark",
};

const stateTitle = (state: string) => {
  const key = `state.${state}`;
  return has(key) ? t(key) : state;
};

export function TopBar() {
  const current = route.value;
  const queue = activeJobs.value;
  const unseen = unseenServers.value.length;
  return (
    <header class="topbar">
      <a class="brand" href="#/">
        {t("brand")}
      </a>
      <nav class="nav">
        <a
          href={hrefSection(lastSection.value ?? sections.value[0]?.id ?? "")}
          class={
            current.page === "section" || current.page === "tool"
              ? "active"
              : ""
          }
          title={sections.value
            .map(
              (section) =>
                `${section.title}: ${stateTitle(bestState(section.servers))}`,
            )
            .join(" · ")}
        >
          <span
            class={`dot ${bestState(sections.value.flatMap((section) => section.servers))}`}
          />
          {t("nav.generate")}
        </a>
        <a
          href={hrefLibrary()}
          class={current.page === "library" ? "active" : ""}
        >
          {t("nav.library")}
        </a>
        <a href={hrefJobs} class={current.page === "jobs" ? "active" : ""}>
          {t("nav.jobs")}
        </a>
        {unseen > 0 && (
          <a
            href="#/"
            class={current.page === "home" ? "active muted" : "muted"}
            title={t("nav.unseenTitle")}
          >
            {t("nav.unseen", { count: unseen })}
          </a>
        )}
      </nav>
      <span class="spacer" />
      <a class={`queue${queue ? " busy" : ""}`} href={hrefJobs}>
        {queue ? t("queue.busy", { count: queue }) : t("queue.empty")}
      </a>
      <button
        class="button small theme"
        onClick={nextTheme}
        title={t("theme.title")}
      >
        {t(THEME_LABELS[themeChoice.value])}
      </button>
      <button
        class="button small language"
        lang={language.value === "ru" ? "en" : "ru"}
        title={t("language.title")}
        onClick={() => setLanguage(language.value === "ru" ? "en" : "ru")}
      >
        {t("language.switch")}
      </button>
    </header>
  );
}
