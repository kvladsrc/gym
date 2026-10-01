import { hrefSection } from "../state/router";
import { sections, unseenServers } from "../state/sections";
import { loaded, servers } from "../state/store";

/** Servers that never answered: their tasks, and so their sections, are unknown. */
function Unseen() {
  if (!unseenServers.value.length) return null;
  return (
    <div class="empty-page">
      <h2>Ещё не запускались</h2>
      <p>
        Задачи этих моделей станут известны, когда их сервер ответит хотя бы
        раз:
      </p>
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
      <h2>Моделей пока нет</h2>
      <p>
        Опишите серверы моделей в{" "}
        <code>~/.config/assets-studio/studio.toml</code>, например:
      </p>
      <pre>{`[[servers]]\nid = "flux"\nurl = "http://127.0.0.1:9104"`}</pre>
      <p>и перезапустите студию.</p>
    </div>
  );
}
