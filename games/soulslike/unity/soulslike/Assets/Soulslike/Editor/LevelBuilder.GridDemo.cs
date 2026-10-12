using System.Linq;
using Soulslike.Training;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Soulslike.EditorTools
{
    /// <summary>
    /// What a duellist's grid sees, in the editor ("Soulslike/Grid Demo"): the
    /// arena in play mode, one arena's pair fighting by the agents' crude
    /// heuristic (no model, no trainer), and the first fighter's grid drawn on
    /// the floor (GridView), seen from behind and above.
    /// </summary>
    public static partial class LevelBuilder
    {
        [MenuItem("Soulslike/Grid Demo")]
        static void GridDemo()
        {
            if (EditorApplication.isPlaying) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ArenaPath);
            EditorApplication.playModeStateChanged -= GridDemoState;
            EditorApplication.playModeStateChanged += GridDemoState;
            EditorApplication.isPlaying = true;
        }

        static void GridDemoState(PlayModeStateChange change)
        {
            // After the arena's own Start (it lays out and equips).
            if (change == PlayModeStateChange.EnteredPlayMode) EditorApplication.delayCall += GridDemoBegin;
            else if (change == PlayModeStateChange.ExitingPlayMode) EditorApplication.playModeStateChanged -= GridDemoState;
        }

        static void GridDemoBegin()
        {
            var arenas = Object.FindObjectsByType<DuelEpisode>(FindObjectsSortMode.None);
            var shown = arenas.OrderBy(a => a.transform.position.x + a.transform.position.z).First();
            // The other arenas' fighters out of the way.
            foreach (var arena in arenas)
                if (arena != shown)
                    foreach (var agent in new[] { arena.first, arena.second }) agent.gameObject.SetActive(false);
            foreach (var chase in Object.FindObjectsByType<ThirdPersonCamera>(FindObjectsSortMode.None)) chase.enabled = false;
            var view = shown.first.gameObject.AddComponent<GridView>();
            view.fighter = shown.first.Body;
            view.shader = Shader.Find("Sprites/Default");
        }
    }
}
