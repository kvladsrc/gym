"""A comparison page of a sweep: every image with what each 3D model made of
it, side by side (``sweep.py gallery SPEC.toml``).

Meshes are rendered by Blender (render_views.py: front, three-quarter,
back and from above) into ~/.local/share/assets-studio/sweeps/<name>/renders/, once each:
run it again as jobs finish, or to show new ratings. Images and links come
from the running studio (open a model there to turn it and rate it).
"""

import html
import json
import shutil
import statistics
import subprocess
import tempfile
import urllib.error
import urllib.request
from collections.abc import Callable
from pathlib import Path
from typing import Any

RENDER_SCRIPT = Path(__file__).with_name("render_views.py")
VIEWS = 4

Call = Callable[..., Any]

STATUS = {
    "queued": "в очереди",
    "waiting_model": "ждёт модель",
    "running": "делается",
    "failed": "ошибка",
    "cancelled": "отменено",
}


def _mesh_cells(call: Call, entry: dict[str, Any]) -> dict[str, dict[str, Any]]:
    """Model → the job's state and its first output asset (None until it has one)."""
    cells = {}
    for mesh in entry["meshes"]:
        job = call("GET", f"/api/jobs/{mesh['job']}")
        asset = call("GET", f"/api/assets/{job['outputs'][0]}") if job["outputs"] else None
        cells[mesh["model"]] = {"job": job, "asset": asset}
    return cells


def _image_of(call: Call, entry: dict[str, Any]) -> str | None:
    if entry["image_asset"]:
        return entry["image_asset"]
    outputs = call("GET", f"/api/jobs/{entry['image_job']}")["outputs"]
    return outputs[0] if outputs else None


def _prompt_of(call: Call, image: str | None) -> str | None:
    """The prompt the image was made with (the studio's /job of an asset)."""
    if image is None:
        return None
    try:
        job = call("GET", f"/api/assets/{image}/job")
    except urllib.error.HTTPError:  # a studio from before /job
        return None
    return None if job is None else job.get("prompt")


def _render(studio: str, assets: list[dict[str, Any]], renders: Path) -> None:
    """Render the meshes that have no views yet, in one Blender run."""
    missing = [a for a in assets if not (renders / f"{a['id']}.{VIEWS - 1}.jpg").exists()]
    if not missing:
        return
    blender = shutil.which("blender")
    if blender is None:
        print(f"blender not found: {len(missing)} meshes left without views")
        return
    renders.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix="sweep-gallery-") as scratch:
        jobs = []
        for asset in missing:
            path = Path(scratch) / f"{asset['id']}.glb"
            with urllib.request.urlopen(f"{studio}{asset['file_url']}", timeout=120) as response:  # noqa: S310 -- the studio's http URL
                path.write_bytes(response.read())
            jobs.append([str(path), str(renders / asset["id"])])
        listing = Path(scratch) / "list.json"
        listing.write_text(json.dumps(jobs))
        print(f"rendering {len(jobs)} meshes…")
        result = subprocess.run(  # noqa: S603 -- blender with our own script
            [blender, "-b", "--factory-startup", "-P", str(RENDER_SCRIPT), "--", str(listing)],
            capture_output=True,
            text=True,
            check=False,
        )
        for line in result.stdout.splitlines():
            if line.startswith("RENDER FAILED"):
                print(line)


def _stars(rating: int | None) -> str:
    return "" if rating is None else "★" * rating + "☆" * (5 - rating)


def _cell(studio: str, cell: dict[str, Any] | None, renders: Path, column: int) -> str:
    """One model's result; ``m{column}`` lets the page hide the column."""
    if cell is None:
        return f'<div class="cell empty m{column}">—</div>'
    job, asset = cell["job"], cell["asset"]
    if asset is None:
        status = html.escape(STATUS.get(job["status"], job["status"]))
        error = html.escape(job.get("error_message") or "")
        return f'<div class="cell empty m{column}"><b>{status}</b><small>{error}</small></div>'
    views = "".join(
        f'<a class="v{i}" href="renders/{asset["id"]}.{i}.jpg"><img loading="lazy" '
        f'src="renders/{asset["id"]}.{i}.jpg" alt=""></a>'
        if (renders / f"{asset['id']}.{i}.jpg").exists()
        else f'<span class="noview v{i}"></span>'
        for i in range(VIEWS)
    )
    meta = asset["meta"]
    facts = [f"{_number(meta['triangles'])} △"] if "triangles" in meta else []
    if "reduced_from" in meta:
        facts.append(f"из {_number(meta['reduced_from'])}")
    if meta.get("watertight") is False:
        facts.append("не замкнута")
    seconds = (job.get("timing") or {}).get("generate_s")
    if isinstance(seconds, int | float):
        facts.append(f"{seconds:.0f} с")
    link = f"{studio}/#/library/{asset['id']}"
    return (
        f'<div class="cell m{column}"><div class="views">{views}</div>'
        f'<div class="facts">{html.escape(" · ".join(facts))} '
        f'<span class="stars">{_stars(asset["rating"])}</span> '
        f'<a href="{link}" target="_blank">в студии</a></div></div>'
    )


def _number(value: float) -> str:
    return f"{value:,.0f}".replace(",", " ")


def _summary(rows: list[dict[str, Any]], models: list[str]) -> str:
    """Per model: done, failed, median time and triangles, closed meshes, ratings."""
    lines = []
    for model in models:
        cells = [r["cells"][model] for r in rows if model in r["cells"]]
        done = [c for c in cells if c["asset"]]
        times = [
            seconds
            for c in done
            if isinstance(seconds := (c["job"].get("timing") or {}).get("generate_s"), int | float)
        ]
        metas = [c["asset"]["meta"] for c in done]
        triangles = [meta["triangles"] for meta in metas if "triangles" in meta]
        closed = sum(meta.get("watertight") is True for meta in metas)
        ratings = [c["asset"]["rating"] for c in done if c["asset"]["rating"] is not None]
        columns = [
            f"{len(done)} / {len(cells)}",
            str(sum(c["job"]["status"] == "failed" for c in cells)),
            f"{statistics.median(times):.0f} с" if times else "—",
            _number(statistics.median(triangles)) if triangles else "—",
            f"{closed} / {len(done)}",
            f"{statistics.mean(ratings):.1f} ({len(ratings)})" if ratings else "—",
        ]
        tds = "".join(f"<td>{html.escape(c)}</td>" for c in columns)
        lines.append(f"<tr><th>{html.escape(model)}</th>{tds}</tr>")
    return (
        "<table><tr><th></th><th>готово</th><th>ошибок</th><th>время, медиана</th>"
        "<th>треугольников, медиана</th><th>замкнутых</th><th>оценка (сколько)</th></tr>"
        + "".join(lines)
        + "</table>"
    )


STYLE = """
:root { color-scheme: light dark; --bg: #f5f6f8; --card: #fff; --muted: #6a717c;
  --border: #e1e4e8; --accent: #2f6fed; }
@media (prefers-color-scheme: dark) {
  :root { --bg: #16181c; --card: #1f2227; --muted: #9aa1ab; --border: #2e3238; --accent: #7aa2ff; }
}
body { margin: 0; padding: 16px; background: var(--bg); font: 14px/1.4 system-ui, sans-serif; }
h1 { margin: 0 0 4px; font-size: 20px; }
h2 { margin: 28px 0 8px; font-size: 16px; text-transform: capitalize; }
p { color: var(--muted); margin: 0 0 12px; max-width: 70em; }
a { color: var(--accent); }
table { border-collapse: collapse; background: var(--card); margin-bottom: 8px; }
th, td { padding: 4px 10px; border: 1px solid var(--border); text-align: left; }
.controls { display: flex; flex-wrap: wrap; gap: 6px 16px; align-items: center;
  margin: 12px 0; }
.controls label { white-space: nowrap; cursor: pointer; }
.head { display: grid; grid-template-columns: 160px repeat(var(--shown), minmax(0, var(--cell)));
  gap: 8px; position: sticky; top: 0; background: var(--bg); padding: 6px 0; z-index: 1;
  font-weight: 600; overflow-wrap: anywhere; }
.row { display: grid; grid-template-columns: 160px repeat(var(--shown), minmax(0, var(--cell)));
  gap: 8px; margin-bottom: 8px; }
.cell { container-type: inline-size; min-width: 0; }
/* Renders are 384 px (render_views.SIZE): not blown up much past that. */
body { --cell: 1040px; }
body[data-view] { --cell: 480px; }
.source, .cell { background: var(--card); border: 1px solid var(--border); border-radius: 8px;
  padding: 6px; }
.source img { width: 100%; aspect-ratio: 1; object-fit: contain; background: #fff;
  border-radius: 4px; }
.source small, .cell small { display: block; color: var(--muted); font-size: 11px; }
.views { display: grid; grid-template-columns: repeat(4, 1fr); gap: 2px; }
@container (max-width: 380px) { .views { grid-template-columns: repeat(2, 1fr); } }
body[data-view] .views { grid-template-columns: 1fr; }
body[data-view] .views > * { display: none; }
body[data-view="0"] .v0, body[data-view="1"] .v1, body[data-view="2"] .v2,
body[data-view="3"] .v3 { display: block; }
.views img { width: 100%; display: block; border-radius: 3px; }
.noview { aspect-ratio: 1; background: var(--border); border-radius: 3px; }
.facts { font-size: 12px; color: var(--muted); margin-top: 4px; }
.stars { color: #e3a008; }
.empty { display: grid; place-content: center; text-align: center; color: var(--muted); }
"""

# Columns and views to show, chosen on the page and remembered by the browser.
SCRIPT = """
const models = [...document.querySelectorAll("[data-model]")];
const hidden = document.getElementById("hidden");
function remembered(key, fallback) {
  try { return JSON.parse(localStorage.getItem(key)) ?? fallback; } catch { return fallback; }
}
function remember(key, value) {
  try { localStorage.setItem(key, JSON.stringify(value)); } catch {}
}
const key = "sweep-gallery:" + document.title;
const saved = remembered(key, {});
function apply() {
  const off = models.filter((box) => !box.checked).map((box) => box.dataset.model);
  hidden.textContent = off.map((m) => `.m${m} { display: none; }`).join(" ");
  document.body.style.setProperty("--shown", Math.max(1, models.length - off.length));
  const view = document.querySelector("input[name=view]:checked").value;
  if (view === "all") delete document.body.dataset.view; else document.body.dataset.view = view;
  remember(key, { off, view });
}
for (const box of models) box.checked = !(saved.off ?? []).includes(box.dataset.model);
const view = document.querySelector(`input[name=view][value="${saved.view ?? "all"}"]`);
if (view) view.checked = true;
for (const row of document.querySelectorAll(".controls")) row.addEventListener("change", apply);
apply();
"""

VIEW_NAMES = ("спереди", "¾", "сзади", "сверху")


def build(call: Call, studio: str, manifest: Path) -> Path:
    data = json.loads(manifest.read_text())
    out = manifest.with_suffix("")
    renders = out / "renders"
    rows = []
    for entry in data["entries"]:
        image = _image_of(call, entry)
        rows.append(
            {
                "entry": entry,
                "image": image,
                "prompt": entry.get("prompt") or _prompt_of(call, image),
                "cells": _mesh_cells(call, entry),
            }
        )
    models: list[str] = []
    for row in rows:
        models += [m for m in row["cells"] if m not in models]
    _render(studio, [c["asset"] for r in rows for c in r["cells"].values() if c["asset"]], renders)

    body = []
    category = None
    for row in rows:
        entry = row["entry"]
        if entry["category"] != category:
            category = entry["category"]
            body.append(f"<h2>{html.escape(category)}</h2>")
        image = row["image"]
        source = (
            f'<a href="{studio}/#/library/{image}" target="_blank">'
            f'<img loading="lazy" src="{studio}/api/assets/{image}/file" alt=""></a>'
            if image
            else ""
        )
        prompt = html.escape((row["prompt"] or entry["subject"])[:200])
        cells = "".join(
            _cell(studio, row["cells"].get(m), renders, i) for i, m in enumerate(models)
        )
        source = f'<div class="source">{source}<small>{prompt}</small></div>'
        body.append(f'<div class="row">{source}{cells}</div>')
    head = "".join(f'<div class="m{i}">{html.escape(m)}</div>' for i, m in enumerate(models))
    toggles = "".join(
        f'<label><input type="checkbox" data-model="{i}" checked> {html.escape(m)}</label>'
        for i, m in enumerate(models)
    )
    views = '<label><input type="radio" name="view" value="all" checked> все 4</label>' + "".join(
        f'<label><input type="radio" name="view" value="{i}"> {name}</label>'
        for i, name in enumerate(VIEW_NAMES)
    )
    page = f"""<!doctype html>
<html lang="ru"><head><meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>Sweep {html.escape(data["name"])}</title><style>{STYLE}</style>
<style id="hidden"></style></head>
<body style="--shown: {len(models)}">
<h1>Sweep {html.escape(data["name"])}</h1>
<p>Каждая строка — одна картинка и то, что сделала из неё каждая модель: спереди,
в три четверти, сзади и сверху. Формы — глиной с затенением впадин, покрашенные — в цвете.
Клик по виду — крупнее; «в студии» — модель целиком, там её можно покрутить и оценить.
Лишние модели можно скрыть, а вместо четырёх видов оставить один — остальное станет крупнее.
Оценки из студии попадают сюда при следующей сборке страницы.</p>
{_summary(rows, models)}
<div class="controls"><b>Модели:</b> {toggles}</div>
<div class="controls"><b>Виды:</b> {views}</div>
<div class="head"><div>картинка</div>{head}</div>
{"".join(body)}
<script>{SCRIPT}</script>
</body></html>
"""
    out.mkdir(parents=True, exist_ok=True)
    index = out / "index.html"
    index.write_text(page)
    return index
