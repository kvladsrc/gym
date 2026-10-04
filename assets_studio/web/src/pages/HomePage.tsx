import { hrefSection } from "../state/router";
import { sections, unseenServers } from "../state/sections";
import { loaded, servers } from "../state/store";
import { t } from "../i18n";

/** Servers that never answered: their tasks, and so their sections, are unknown. */
function Unseen() {
  if (!unseenServers.value.length) return null;
  return (
    <div class="empty-page">
      <h2>{t("home.unseenTitle")}</h2>
      <p>{t("home.unseenText")}</p>
      <ul class="unseen">
        {unseenServers.value.map((server) => (
          <li key={server.id}>
            <span class={`dot ${server.state}`} />
            <b>{server.title}</b> — <code>{server.url}</code>
          </li>
        ))}
      </ul>
    </div>
  );
}

export function HomePage() {
  if (!loaded.value)
    return (
      <div class="placeholder">
        <span class="spinner" />
      </div>
    );
  const first = sections.value[0];
  if (first && !unseenServers.value.length) {
    // The first section is the natural start page.
    window.location.replace(hrefSection(first.id));
    return null;
  }
  if (servers.value.length) return <Unseen />;
  return (
    <div class="empty-page">
      <h2>{t("home.emptyTitle")}</h2>
      <p>
        {t("home.emptyBefore")} <code>~/.config/assets-studio/studio.toml</code>
        {t("home.emptyExample")}
      </p>
      <pre>{`[[servers]]\nid = "flux"\nurl = "http://127.0.0.1:9104"`}</pre>
      <p>{t("home.emptyAfter")}</p>
    </div>
  );
}
