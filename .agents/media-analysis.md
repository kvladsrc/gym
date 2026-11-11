# Media analysis for all agents

These rules cover images, screenshots, video, contact sheets, rendered assets
and animation previews, for Codex, Claude, delegated agents and other
repository tools.

Agents may look at media with their own vision: Claude's image Read, Codex's
`view_image`, browser and Blender screenshots. Studio's local vision-language
model remains available when a text report is more useful, for example inside
a Flow, for batch captions, or to keep a description with its provenance.
Explicit user instructions for the current task take precedence: if the user
says not to inspect media, do not, not even with the local model.

## Workflow

1. Start with text and numeric evidence: source/configuration, logs, ffprobe,
   transforms, joint trajectories, weights, validation JSON and existing
   reports. Look at media when these do not answer the question.
2. Look with a specific question in mind. Put related frames into one labelled
   contact sheet rather than many separate views.
3. Sampled video frames show only those moments, without audio. They cannot
   prove absence of frame drops, jitter, sliding or brief pose failures. Use
   numeric temporal checks and targeted additional samples when needed.
4. For comparisons, name each panel and chosen subject, align timestamps and
   account for different cameras, framing and heading normalization. A
   different 2D silhouette from another viewpoint is not proof of a pose error.
5. Treat visual impressions, your own or a model's, as fallible observations.
   Record uncertainty and occlusion. Do not promote one to a rigging diagnosis,
   ground truth, accepted asset or automatic quality score; invariants and
   metrics decide where they exist.

Do not base64-dump media into the conversation.

## Local vision model

The `qwen` Studio server (Qwen3.8-27B) runs `image-to-text` and
`video-to-text`. The launcher starts it when a job needs it. It shares the
single GPU with other models; submit through Studio rather than starting
competing workers. Keep `thinking=false`; start with 256–512 output tokens,
one image or four video samples. From the repository root:

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

## Image generation

Images may come from Studio's local models or from Codex's built-in image
generation. Run Codex with its ChatGPT session,
`env -u OPENAI_API_KEY codex exec ...`, never with an API key from the
environment. Keep the prompts, the tool and the authentication used next to
the images, for example in a `manifest.json`.
