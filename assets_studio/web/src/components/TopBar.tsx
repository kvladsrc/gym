import { hrefLibrary, hrefSection, route } from "../state/router";
import { bestState, sections, unseenServers } from "../state/sections";
import { activeJobs } from "../state/store";

const STATE_TITLES: Record<string, string> = {
  ready: "Есть готовая модель",
  busy: "Модели заняты",
  loading: "Модели загружаются",
  error: "Ошибка загрузки модели",
  unavailable: "Серверы моделей не запущены",
};

export function TopBar() {
  const current = route.value;
  const queue = activeJobs.value;
  const unseen = unseenServers.value.length;
  return (
    <header class="topbar">
      <a class="brand" href="#/">
        Студия
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
              title={`${STATE_TITLES[state] ?? state} · ${models}`}
            >
              <span class={`dot ${state}`} />
              {section.title}
              <span class="visually-hidden">
                {" "}
                ({STATE_TITLES[state] ?? state})
              </span>
            </a>
          );
        })}
        <span class="separator" />
        <a
          href={hrefLibrary()}
          class={current.page === "library" ? "active" : ""}
        >
          Библиотека
        </a>
        {unseen > 0 && (
          <a
            href="#/"
            class={current.page === "home" ? "active muted" : "muted"}
            title="Модели, которые ещё ни разу не запускались"
          >
            Не запускались: {unseen}
          </a>
        )}
      </nav>
      <span class="spacer" />
      <span class={`queue${queue ? " busy" : ""}`}>
        {queue ? `В работе: ${queue}` : "Очередь пуста"}
      </span>
    </header>
  );
}
