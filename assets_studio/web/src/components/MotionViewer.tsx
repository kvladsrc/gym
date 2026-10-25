// A native motion (SMPL-X or SOMA) as a skeleton, frame by frame: drag to turn it, scrub or
// play, and collect key frames. Kimodo makes a new motion through them: Kimodo SMPL-X from an
// SMPL-X motion, Kimodo SOMA from a SOMA pose or motion (GEM-X).
import { useEffect, useRef, useState } from "preact/hooks";

import { api, type Asset, type Skeleton } from "../api/client";
import { t } from "../i18n";
import { attempt, mergeJob, say, servers } from "../state/store";
import { cssColor, dark } from "../state/theme";

const HEIGHT = 360;

/** The Kimodo task for key frames of each body. */
const KEYFRAMES_TASK: Record<string, string> = {
  "studio.smplx-motion.v1": "keyframes-to-native-motion",
  "studio.soma-motion.v1": "keyframes-to-soma-motion",
};

function useSkeleton(asset: Asset) {
  const [skeleton, setSkeleton] = useState<Skeleton | null>(null);
  const [failed, setFailed] = useState(false);
  useEffect(() => {
    let current = true;
    setSkeleton(null);
    setFailed(false);
    api.skeleton(asset.id).then(
      (value) => current && setSkeleton(value),
      () => current && setFailed(true),
    );
    return () => {
      current = false;
    };
  }, [asset.id]);
  return { skeleton, failed };
}

/** Centre and size of all frames, for a camera that does not move while playing. */
function bounds(skeleton: Skeleton) {
  let [x0, y0, z0, x1, y1, z1] = [
    Infinity,
    Infinity,
    Infinity,
    -Infinity,
    -Infinity,
    -Infinity,
  ];
  for (const frame of skeleton.positions)
    for (let i = 0; i + 2 < frame.length; i += 3) {
      const [x, y, z] = [frame[i] ?? 0, frame[i + 1] ?? 0, frame[i + 2] ?? 0];
      [x0, y0, z0] = [Math.min(x0, x), Math.min(y0, y), Math.min(z0, z)];
      [x1, y1, z1] = [Math.max(x1, x), Math.max(y1, y), Math.max(z1, z)];
    }
  const centre: [number, number, number] = [
    (x0 + x1) / 2,
    (y0 + y1) / 2,
    (z0 + z1) / 2,
  ];
  return { centre, size: Math.max(x1 - x0, y1 - y0, z1 - z0, 0.5), floor: y0 };
}

function draw(
  canvas: HTMLCanvasElement,
  skeleton: Skeleton,
  frame: number,
  yaw: number,
  pitch: number,
  keys: number[],
) {
  const ratio = window.devicePixelRatio || 1;
  const width = canvas.clientWidth;
  canvas.width = width * ratio;
  canvas.height = HEIGHT * ratio;
  const context = canvas.getContext("2d");
  if (!context) return;
  context.setTransform(ratio, 0, 0, ratio, 0, 0);
  context.clearRect(0, 0, width, HEIGHT);
  const { centre, size, floor } = bounds(skeleton);
  const scale = (HEIGHT * 0.8) / size;
  const [cy, sy, cp, sp] = [
    Math.cos(yaw),
    Math.sin(yaw),
    Math.cos(pitch),
    Math.sin(pitch),
  ];
  const project = (x: number, y: number, z: number): [number, number] => {
    const dx = x - centre[0];
    const dy = y - centre[1];
    const dz = z - centre[2];
    const rx = dx * cy + dz * sy; // turn about the vertical
    const rz = -dx * sy + dz * cy;
    const ry = dy * cp - rz * sp; // then tilt towards the viewer
    return [width / 2 + rx * scale, HEIGHT / 2 - ry * scale];
  };
  // the floor: a grid of half-metre squares under the motion
  context.strokeStyle = cssColor("--border");
  context.lineWidth = 1;
  for (let i = -4; i <= 4; i++) {
    const a = project(centre[0] + i * 0.5, floor, centre[2] - 2);
    const b = project(centre[0] + i * 0.5, floor, centre[2] + 2);
    const c = project(centre[0] - 2, floor, centre[2] + i * 0.5);
    const d = project(centre[0] + 2, floor, centre[2] + i * 0.5);
    context.beginPath();
    context.moveTo(a[0], a[1]);
    context.lineTo(b[0], b[1]);
    context.moveTo(c[0], c[1]);
    context.lineTo(d[0], d[1]);
    context.stroke();
  }
  const points = skeleton.positions[frame] ?? [];
  const at = (joint: number) =>
    project(
      points[joint * 3] ?? 0,
      points[joint * 3 + 1] ?? 0,
      points[joint * 3 + 2] ?? 0,
    );
  context.strokeStyle = keys.includes(frame)
    ? cssColor("--wait")
    : cssColor("--accent");
  context.lineWidth = 3;
  context.lineCap = "round";
  skeleton.parents.forEach((parent, joint) => {
    if (parent < 0) return;
    const [a, b] = [at(parent), at(joint)];
    context.beginPath();
    context.moveTo(a[0], a[1]);
    context.lineTo(b[0], b[1]);
    context.stroke();
  });
  context.fillStyle = cssColor("--text");
  skeleton.parents.forEach((_, joint) => {
    const [x, y] = at(joint);
    context.beginPath();
    context.arc(x, y, 2.2, 0, Math.PI * 2);
    context.fill();
  });
}

export function MotionViewer({ asset }: { asset: Asset }) {
  const { skeleton, failed } = useSkeleton(asset);
  const canvas = useRef<HTMLCanvasElement>(null);
  const [frame, setFrame] = useState(0);
  const [playing, setPlaying] = useState(false);
  const [view, setView] = useState({ yaw: 0.6, pitch: 0.25 });
  const [keys, setKeys] = useState<number[]>([]);
  const [prompt, setPrompt] = useState("");
  const [seconds, setSeconds] = useState(4);
  const [loop, setLoop] = useState(false);
  const [started, setStarted] = useState(false);
  const drag = useRef<{
    x: number;
    y: number;
    yaw: number;
    pitch: number;
  } | null>(null);

  useEffect(() => {
    setFrame(0);
    setKeys([]);
    setPlaying(false);
    setStarted(false);
  }, [asset.id]);
  useEffect(() => {
    if (skeleton && canvas.current)
      draw(canvas.current, skeleton, frame, view.yaw, view.pitch, keys);
  }, [skeleton, frame, view, keys, dark.value]);
  useEffect(() => {
    if (!playing || !skeleton) return;
    const timer = window.setInterval(
      () => setFrame((value) => (value + 1) % skeleton.frames),
      1000 / skeleton.fps,
    );
    return () => window.clearInterval(timer);
  }, [playing, skeleton]);

  if (failed) return <p class="hint error">{t("motion.failed")}</p>;
  if (!skeleton) return <span class="spinner" />;
  const last = skeleton.frames - 1;
  const source = (index: number) => index * skeleton.stride; // a frame of the stored motion
  const task = KEYFRAMES_TASK[skeleton.format];
  const server = servers.value.find((candidate) =>
    candidate.tasks.some((offered) => offered.task === task),
  );
  const span = keys.length
    ? (source(keys.at(-1) ?? 0) - source(keys[0] ?? 0)) / 30
    : 0;

  async function generate() {
    if (!skeleton || !server || !task) return;
    const job = await attempt(() =>
      api.createJob({
        server: server.id,
        task,
        prompt,
        count: 1,
        params: {
          frames: keys.map((key) => source(key) + 1).join(","),
          seconds,
          loop,
        },
        inputs: { motion: asset.id },
      }),
    );
    if (job) {
      mergeJob(job);
      setStarted(true);
      say(t("motion.started"));
    }
  }

  return (
    <div class="motion-viewer">
      <canvas
        ref={canvas}
        style={{ height: `${HEIGHT}px` }}
        aria-label={t("motion.canvas")}
        onPointerDown={(event) => {
          drag.current = { x: event.clientX, y: event.clientY, ...view };
          event.currentTarget.setPointerCapture(event.pointerId);
        }}
        onPointerMove={(event) => {
          const start = drag.current;
          if (!start) return;
          setView({
            yaw: start.yaw + (event.clientX - start.x) / 120,
            pitch: Math.max(
              -1.2,
              Math.min(1.2, start.pitch + (event.clientY - start.y) / 160),
            ),
          });
        }}
        onPointerUp={() => (drag.current = null)}
      />
      <div class="row motion-controls">
        <button class="button small" onClick={() => setPlaying(!playing)}>
          {playing ? t("motion.pause") : t("motion.play")}
        </button>
        <button
          class="button small"
          aria-label={t("motion.previous")}
          onClick={() => setFrame(Math.max(0, frame - 1))}
        >
          ‹
        </button>
        <input
          id="motion-frame"
          type="range"
          min={0}
          max={last}
          value={frame}
          aria-label={t("motion.frame")}
          onInput={(event) => {
            setPlaying(false);
            setFrame(Number(event.currentTarget.value));
          }}
        />
        <button
          class="button small"
          aria-label={t("motion.next")}
          onClick={() => setFrame(Math.min(last, frame + 1))}
        >
          ›
        </button>
        <span class="motion-time">
          {t("motion.at", {
            frame: source(frame) + 1,
            frames: skeleton.source_frames,
            seconds: (source(frame) / 30).toFixed(2),
          })}
        </span>
      </div>
      <div class="row motion-keys">
        <button
          class="button small"
          disabled={keys.includes(frame)}
          onClick={() => setKeys([...keys, frame].sort((a, b) => a - b))}
        >
          {t("motion.addKey")}
        </button>
        {keys.map((key) => (
          <span class="chip" key={key}>
            <button
              class="link"
              onClick={() => {
                setPlaying(false);
                setFrame(key);
              }}
            >
              {source(key) + 1}
            </button>
            <button
              class="link"
              aria-label={t("motion.removeKey")}
              onClick={() => setKeys(keys.filter((k) => k !== key))}
            >
              ×
            </button>
          </span>
        ))}
      </div>
      {keys.length > 0 &&
        (server ? (
          <div class="motion-generate">
            <input
              id="motion-prompt"
              placeholder={t("motion.prompt")}
              value={prompt}
              onInput={(event) => setPrompt(event.currentTarget.value)}
            />
            <label>
              {t("motion.seconds")}
              <input
                id="motion-seconds"
                type="number"
                min={Math.max(1, Math.ceil(span + 0.1))}
                max={10}
                step={0.5}
                value={seconds}
                onInput={(event) =>
                  setSeconds(Number(event.currentTarget.value))
                }
              />
            </label>
            <label>
              <input
                id="motion-loop"
                type="checkbox"
                checked={loop}
                onChange={(event) => setLoop(event.currentTarget.checked)}
              />
              {t("motion.loop")}
            </label>
            <button
              class="button small primary"
              disabled={!prompt.trim() || seconds < span}
              onClick={() => void generate()}
            >
              {t("motion.generate")}
            </button>
            {started && <span class="hint">{t("motion.started")}</span>}
          </div>
        ) : (
          <p class="hint">{t("motion.noKimodo")}</p>
        ))}
    </div>
  );
}
