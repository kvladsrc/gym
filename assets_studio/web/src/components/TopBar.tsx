import { hrefLibrary, hrefSection, route } from "../state/router";
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
        {sections.value.map((section) => {
          const state = bestState(section.servers);
          const models = section.servers
            .map((server) => server.title)
            .join(", ");
          return (
            <a
              key={section.id}
              href={hrefSection(section.id)}
              class={
                current.page === "section" && current.sectionId === section.id
                  ? "active"
                  : ""
              }
              title={`${stateTitle(state)} · ${models}`}
            >
              <span class={`dot ${state}`} />
              {section.title}
              <span class="visually-hidden"> ({stateTitle(state)})</span>
            </a>
          );
        })}
        <span class="separator" />
        <a
          href={hrefLibrary()}
          class={current.page === "library" ? "active" : ""}
        >
          {t("nav.library")}
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
      <span class={`queue${queue ? " busy" : ""}`}>
        {queue ? t("queue.busy", { count: queue }) : t("queue.empty")}
      </span>
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
