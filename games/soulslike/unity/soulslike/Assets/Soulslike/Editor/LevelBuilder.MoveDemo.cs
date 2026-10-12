using System.Linq;
using Soulslike.Training;
using Unity.MLAgents;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Soulslike.EditorTools
{
    /// <summary>
    /// A look at the duel's movement in the editor ("Soulslike/Move Demo"):
    /// the arena in play mode, its agents stilled, one fighter of 1.8 m alone
    /// in the first arena, driven by random orders (MoveDemoView).
    /// </summary>
    public static partial class LevelBuilder
    {
        [MenuItem("Soulslike/Move Demo")]
        static void MoveDemo()
        {
            if (EditorApplication.isPlaying) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ArenaPath);
            EditorApplication.playModeStateChanged -= MoveDemoState;
            EditorApplication.playModeStateChanged += MoveDemoState;
            EditorApplication.isPlaying = true;
        }

        static void MoveDemoState(PlayModeStateChange change)
        {
            // After the arena's own Start (it lays out and equips).
            if (change == PlayModeStateChange.EnteredPlayMode) EditorApplication.delayCall += MoveDemoBegin;
            else if (change == PlayModeStateChange.ExitingPlayMode) EditorApplication.playModeStateChanged -= MoveDemoState;
        }

        static void MoveDemoBegin()
        {
            foreach (var requester in Object.FindObjectsByType<DecisionRequester>(FindObjectsSortMode.None)) requester.enabled = false;
            foreach (var agent in Object.FindObjectsByType<FighterAgent>(FindObjectsSortMode.None)) agent.enabled = false;
            foreach (var chase in Object.FindObjectsByType<ThirdPersonCamera>(FindObjectsSortMode.None)) chase.enabled = false;
            var arenas = Object.FindObjectsByType<DuelEpisode>(FindObjectsSortMode.None);
            foreach (var arena in arenas) arena.enabled = false;
            var here = arenas.OrderBy(a => a.transform.position.x + a.transform.position.z).First();
            foreach (var piece in here.trees.Concat(here.walls)) piece.gameObject.SetActive(false);
            foreach (var arena in arenas)
                foreach (var agent in new[] { arena.first, arena.second })
                    agent.gameObject.SetActive(arena == here && agent == here.first);
            var body = here.first.Body;
            body.Equip(Training.Weapon.Hands, 1.8f);
            body.ResetAt(here.Centre, 0f);
            var label = new GameObject("Move demo label").AddComponent<TextMesh>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.GetComponent<MeshRenderer>().sharedMaterial = label.font.material;
            label.characterSize = 0.03f;
            label.fontSize = 64;
            label.anchor = TextAnchor.MiddleCenter;
            label.color = Color.white;
            var view = body.gameObject.AddComponent<MoveDemoView>();
            view.body = body;
            view.centre = here.Centre;
            view.label = label;
        }
    }
}
