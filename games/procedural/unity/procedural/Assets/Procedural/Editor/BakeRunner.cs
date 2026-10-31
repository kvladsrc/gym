using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Procedural.Generation;
using Procedural.World;
using UnityEditor;
using UnityEngine;

namespace Procedural.Editor
{
    /// <summary>
    /// Runs the generator on a worker thread and builds the level when it is
    /// done, keeping artifacts/bake/status.json current so MCP can poll it.
    /// One run at a time; assembly reloads wait until it ends.
    /// </summary>
    public static class BakeRunner
    {
        [Serializable]
        class Status
        {
            public string state;      // running | done | failed | canceled
            public string mode;
            public int seed;
            public string stage;
            public int stageIndex;
            public int stageCount;
            public float fraction;
            public string startedUtc;
            public double elapsedSeconds;
            public double buildSeconds;
            public bool passed;
            public string report;
            public string map;
            public string error;
        }

        /// <summary>Thread-safe progress from the worker thread.</summary>
        sealed class Sink : IProgress<PipelineProgress>
        {
            PipelineProgress last = new PipelineProgress("Starting", 0, 1, 0f);
            readonly object gate = new object();

            public void Report(PipelineProgress value)
            {
                lock (gate) last = value;
            }

            public PipelineProgress Last
            {
                get { lock (gate) return last; }
            }
        }

        static Task<IslandResult> task;
        static CancellationTokenSource cancel;
        static Sink sink;
        static IslandGenerator target;
        static Status status;
        static DateTime started;
        static double nextStatusWrite;
        static int progressId;

        public static bool Running => task != null;

        /// <summary>One line for the inspector: the running stage or the last outcome.</summary>
        public static string Summary
        {
            get
            {
                if (status == null) return "No bake in this session.";
                if (status.state == "running")
                    return $"{status.mode}: {status.stage} ({status.stageIndex + 1}/{status.stageCount}), " +
                           $"{status.fraction:P0}, {status.elapsedSeconds:F0} s";
                string outcome = status.state == "done" ? (status.passed ? "passed" : "invariants FAILED") : status.state;
                return $"{status.mode} {status.state} in {status.elapsedSeconds:F1} s, {outcome}. {status.error}";
            }
        }

        public static bool Start(IslandGenerator generator, BakeMode mode)
        {
            if (Running)
            {
                Debug.LogWarning("Procedural: a bake is already running.");
                return false;
            }
            if (generator == null || generator.recipe == null)
            {
                Debug.LogError("Procedural: the generator has no recipe.");
                return false;
            }

            var recipe = generator.recipe.recipe.Clone();
            string checkpoints = ProceduralPaths.Checkpoints;
            Directory.CreateDirectory(checkpoints);
            target = generator;
            sink = new Sink();
            cancel = new CancellationTokenSource();
            started = DateTime.UtcNow;
            status = new Status
            {
                state = "running",
                mode = mode.ToString(),
                seed = recipe.seed,
                startedUtc = started.ToString("o")
            };
            var token = cancel.Token;
            var progress = sink;
            task = Task.Run(() => IslandPipeline.Run(recipe, mode, checkpoints, progress, token), token);

            progressId = UnityEditor.Progress.Start($"Island {mode}", $"seed {recipe.seed}");
            UnityEditor.Progress.RegisterCancelCallback(progressId, () =>
            {
                cancel.Cancel();
                return true;
            });
            EditorApplication.LockReloadAssemblies();
            EditorApplication.update += Tick;
            WriteStatus();
            return true;
        }

        public static void Cancel() => cancel?.Cancel();

        static void Tick()
        {
            var last = sink.Last;
            status.stage = last.Stage;
            status.stageIndex = last.Index;
            status.stageCount = last.Count;
            status.fraction = last.Fraction;
            status.elapsedSeconds = (DateTime.UtcNow - started).TotalSeconds;
            float overall = last.Count > 0 ? (last.Index + last.Fraction) / last.Count : 0f;
            UnityEditor.Progress.Report(progressId, overall, last.Stage);
            if (EditorApplication.timeSinceStartup >= nextStatusWrite)
            {
                nextStatusWrite = EditorApplication.timeSinceStartup + 0.5;
                WriteStatus();
            }
            if (!task.IsCompleted) return;

            EditorApplication.update -= Tick;
            try
            {
                if (task.IsCanceled || task.Exception?.InnerException is OperationCanceledException)
                {
                    status.state = "canceled";
                }
                else if (task.IsFaulted)
                {
                    status.state = "failed";
                    status.error = task.Exception.InnerException?.ToString();
                    Debug.LogException(task.Exception.InnerException);
                }
                else
                {
                    var result = task.Result;
                    // A scene reload during the bake (Play Mode tests, opening a scene) destroys the
                    // generator we started from; save into the island scene's current one.
                    if (target == null)
                        target = IslandScene.FindOrCreate();
                    var saved = LevelAssets.Save(result, target);
                    status.state = "done";
                    status.passed = result.Passed;
                    status.buildSeconds = saved.BuildSeconds;
                    status.report = saved.Report;
                    status.map = saved.Map;
                    Debug.Log($"Procedural: {status.mode} of seed {status.seed} done, " +
                              $"invariants {(result.Passed ? "passed" : "FAILED")}; report {saved.Report}");
                }
            }
            catch (Exception e)
            {
                status.state = "failed";
                status.error = e.ToString();
                Debug.LogException(e);
            }
            finally
            {
                status.elapsedSeconds = (DateTime.UtcNow - started).TotalSeconds;
                WriteStatus();
                UnityEditor.Progress.Finish(progressId, status.state == "done"
                    ? UnityEditor.Progress.Status.Succeeded
                    : status.state == "canceled" ? UnityEditor.Progress.Status.Canceled : UnityEditor.Progress.Status.Failed);
                EditorApplication.UnlockReloadAssemblies();
                cancel.Dispose();
                task = null;
                cancel = null;
            }
        }

        static void WriteStatus()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ProceduralPaths.Status));
            File.WriteAllText(ProceduralPaths.Status, JsonUtility.ToJson(status, true));
        }
    }
}
