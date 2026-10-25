# Local media analysis for all agents

The user requires economical media inspection. Use Studio's local Qwen3.8
vision-language model for image descriptions, OCR, screenshots, rendered assets
and sampled video observations. Return/read text in the agent conversation.
These rules apply to Codex, Claude, delegated agents and other repository tools.

## Required workflow

1. Start with text and numeric evidence: source/configuration, logs, ffprobe,
   transforms, joint trajectories, weights, validation JSON and existing reports.
   Do not inspect media when these already answer the question.
2. For visual evidence, ask a specific question of the local VLM. Use the existing
   `qwen` Studio server (Qwen3.8-27B), with `image-to-text` or `video-to-text`.
   The launcher starts it when a job needs it. It shares the single GPU with
   other models; submit through Studio rather than starting competing workers.
3. Read only the output `text/plain` asset or the helper's text stdout. Keep
   `thinking=false`; start with 256–512 output tokens, one image or four video
   samples. Narrow the question before increasing the budget. Save provenance
   and reuse reports for the same bytes, prompt, model revision and parameters.
4. Video reports cover sampled frames, without audio. They cannot prove absence
   of frame drops, jitter, sliding or brief pose failures. Use numeric temporal
   checks and targeted additional samples when needed.
5. For comparisons, name each panel and chosen subject, align timestamps and
   account for different cameras, framing and heading normalization. A different
   2D silhouette from another viewpoint is not proof of a pose error.
6. Treat VLM descriptions as fallible observations. Record uncertainty and
   occlusion. Do not promote an unsupported description to a rigging diagnosis,
   ground truth, accepted asset or automatic quality score.

From the repository root:

```sh
python assets_studio/agent_tools/describe_media.py \
  --path /absolute/path/frame.png \
  --prompt 'Describe only visible head, spine, hands, feet; note occlusion.' \
  --output workspace/review/frame-description.json

python assets_studio/agent_tools/describe_media.py \
  --path /absolute/path/clip.mp4 --frames 4 --max-tokens 512 \
  --prompt 'Describe each timestamp; flag clipped bodies and occlusion.' \
  --output workspace/review/clip-description.json
```

For existing library assets use `--asset ID` (and `--video` for a video).
For two images add `--reference /absolute/path/second.png`. Full reports are
cached under `~/.cache/assets-studio/media-reports/`; `--refresh` explicitly
requests another inference. A timeout includes the job ID: inspect that job
instead of repeatedly creating requests.

The same workflow is available through the existing Studio MCP: `list_servers`,
`import_asset`, `generate(server="qwen", task="image-to-text", ...,
input_assets={"image": ID})`,
`wait_for_job`, then read the **text** output path. For videos the role is `video`.
Image comparisons may additionally supply `reference`. Flow can save these tasks
as ordinary nodes with text outputs; there is no special model contract.

## Provider media calls

Do not use `view_image`, image content blocks, provider vision APIs, automatic
image previews from file readers (including Claude's image Read), browser/Blender
screenshot tool responses or contact sheets in agent context by default. If a
screenshot is needed, save it locally and submit that file to the local VLM.
Do not base64-dump media into the conversation. Do not replace an image call
with a PDF/screenshot call that still sends pixels to the provider.

If the local model is unavailable, inspect its status/logs, repair the local
path when in scope, and continue useful text/numeric work. Report the blocker;
**do not silently fall back to remote vision**. Use provider media only when the
user explicitly authorizes it for the task. If the user says no media inspection
at all, do not invoke even the local VLM on their media; use synthetic smoke
fixtures only when testing the tool is within the requested work.

These are repository instructions, not a technical block on the provider API.
Do not claim that they intercept every client or eliminate provider text costs.
