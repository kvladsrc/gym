using System.IO;
using UnityEngine;

namespace Procedural.Editor
{
    /// <summary>Where the Editor side reads and writes; artifacts live outside Assets.</summary>
    static class ProceduralPaths
    {
        public const string Root = "Assets/Procedural";
        public const string Generated = Root + "/Generated";
        public const string Placeholders = Root + "/Placeholders";
        public const string Recipes = Root + "/Recipes";
        public const string Scene = Root + "/Scenes/Island.unity";

        /// <summary>games/procedural/artifacts (ignored by git).</summary>
        public static string Artifacts =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", "artifacts"));

        public static string Checkpoints => Path.Combine(Artifacts, "bake", "checkpoints");
        public static string Status => Path.Combine(Artifacts, "bake", "status.json");
        public static string Reports => Path.Combine(Artifacts, "reports");
        public static string Maps => Path.Combine(Artifacts, "maps");
    }
}
