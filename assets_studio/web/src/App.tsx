import { HomePage } from "./pages/HomePage";
import { LibraryPage } from "./pages/LibraryPage";
import { SectionPage } from "./pages/SectionPage";
import { TopBar } from "./components/TopBar";
import { route } from "./state/router";
import { sectionById } from "./state/sections";
import { connected, notice } from "./state/store";
import { t } from "./i18n";

export function App() {
  const current = route.value;
  const section =
    current.page === "section" ? sectionById(current.sectionId) : undefined;
  return (
    <div class="app">
      <TopBar />
      <main
        style={{
          display: "grid",
          minHeight: 0,
          gridTemplateRows: connected.value ? "1fr" : "auto 1fr",
        }}
      >
        {!connected.value && <div class="offline">{t("app.offline")}</div>}
        {current.page === "library" ? (
          <LibraryPage selectedId={current.assetId} />
        ) : section ? (
          <SectionPage key={section.id} section={section} />
        ) : (
          <HomePage />
        )}
      </main>
      {notice.value && (
        <div class={`notice${notice.value.error ? " error" : ""}`}>
          {notice.value.text}
        </div>
      )}
    </div>
  );
}
