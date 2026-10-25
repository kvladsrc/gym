// The studio's own tool, no model: a video frame by frame, any frame saved as an image asset
// made from the video. Image tasks then take it like any other picture.
import { useState } from "preact/hooks";

import type { InputSpec } from "../api/client";
import { VideoPreview } from "../components/AssetPreview";
import { InputPicker } from "../components/InputPicker";
import { ModelTree } from "../components/ModelTree";
import { assetById } from "../state/store";
import { t } from "../i18n";

const VIDEO: InputSpec = {
  role: "video",
  mime: ["video/mp4", "video/webm"],
  required: true,
  description: "",
  labels: null,
};

export function FramePage() {
  const [videoId, setVideoId] = useState<string | null>(null);
  const video = videoId ? assetById(videoId) : undefined;
  return (
    <div class="workspace">
      <ModelTree />
      <section class="launch panel" aria-label={t("form.label")}>
        <header class="launch-head">
          <h1>{t("tool.frame")}</h1>
          <span class="hint">{t("tool.frameWhere")}</span>
        </header>
        <InputPicker spec={VIDEO} assetId={videoId} onChange={setVideoId} />
        {video && !video.deleted_at && (
          <div class="preview tool-video">
            <VideoPreview key={video.id} asset={video} />
          </div>
        )}
      </section>
      <aside class="model-panel" aria-label={t("info.label")}>
        <h2>{t("tool.studio")}</h2>
        <p>{t("tool.frameAbout")}</p>
      </aside>
    </div>
  );
}
