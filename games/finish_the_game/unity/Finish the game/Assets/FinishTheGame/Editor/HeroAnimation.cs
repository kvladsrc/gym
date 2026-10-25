using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace FinishTheGame.Editor
{
    /// <summary>The hero's rig (the studio's 3d-to-rig, Mixamo's skeleton:
    /// Art/Characters/hero.fbx) and his clips from the shared package
    /// dev.bonfirelit.mixamo, both Generic (Humanoid retargeting stiffened the
    /// legs): idle, walk and run, played in place (a copy with the hips' steady
    /// drift across the ground taken out), blended by Speed (m/s) at the speeds
    /// the clips themselves move at.</summary>
    public class HeroAnimation : AssetPostprocessor
    {
        const string Characters = "Assets/FinishTheGame/Art/Characters/";
        const string Package = "Packages/dev.bonfirelit.mixamo/";
        const string Clips = "Assets/FinishTheGame/Art/Animation/";
        static readonly (string state, string clip)[] Gaits =
        {
            ("Idle", "Locomotion Pack/idle"),
            ("Walk", "Locomotion Pack/walking"),
            ("Run", "Locomotion Pack/running"),
        };

        // The package's own import settings (Generic, its loops) are left as
        // they are: it is shared with the other games; only the rig is set here.
        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(Characters)) return;
            var importer = (ModelImporter)assetImporter;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.NoAvatar;
            importer.importAnimation = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        }

        /// <summary>Each character's painted texture, embedded in its FBX:
        /// extracted to Textures beside it (named by its hash, so they do not
        /// clash), and the model reimported to find it.</summary>
        public static void ExtractTextures()
        {
            if (!System.IO.Directory.Exists(Characters)) return;
            foreach (var path in System.IO.Directory.GetFiles(Characters, "*.fbx"))
            {
                string model = path.Replace('\\', '/');
                var look = AssetDatabase.LoadAssetAtPath<GameObject>(model);
                var made = look != null ? look.GetComponentInChildren<Renderer>() : null;
                if (made == null || made.sharedMaterial == null || made.sharedMaterial.mainTexture != null
                    || !(AssetImporter.GetAtPath(model) is ModelImporter importer)) continue;
                importer.ExtractTextures(Characters + "Textures");
                AssetDatabase.Refresh();
                AssetDatabase.ImportAsset(model, ImportAssetOptions.ForceUpdate);
            }
        }

        /// <summary>The controller, Art/Animation/Hero.controller: a blend tree of
        /// the gaits by Speed. Null without the package.</summary>
        public static AnimatorController Build()
        {
            System.IO.Directory.CreateDirectory(Clips);
            string path = Clips + "Hero.controller";
            AssetDatabase.DeleteAsset(path);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            var tree = new BlendTree { name = "Gaits", blendParameter = "Speed", useAutomaticThresholds = false };
            AssetDatabase.AddObjectToAsset(tree, controller);
            foreach (var (state, name) in Gaits)
            {
                var (clip, speed) = Copy(name);
                if (clip == null) return null;
                tree.AddChild(clip, state == "Idle" ? 0f : speed);
            }
            var machine = controller.layers[0].stateMachine;
            var move = machine.AddState("Move");
            move.motion = tree;
            machine.defaultState = move;
            AssetDatabase.SaveAssets();
            return controller;
        }

        /// <summary>The valley's people's moves (Villager.cs): standing,
        /// looking about, kneeling to the ground (a deep crouch, the hands
        /// low), rising, walking in place; each a state of its own name, played
        /// by the script. Null without the package.</summary>
        public static AnimatorController Villager()
        {
            System.IO.Directory.CreateDirectory(Clips);
            string path = Clips + "Villager.controller";
            AssetDatabase.DeleteAsset(path);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            var machine = controller.layers[0].stateMachine;
            foreach (var (state, name, loop) in new[]
            {
                ("Idle", "Locomotion Pack/idle", true),
                ("Look", "Pro Melee Axe Pack/unarmed_idle_looking_ver_1", true),
                ("Kneel", "Pro Sword and Shield Pack/sword_and_shield_crouch_idle", true),
                ("Rise", "Pro Melee Axe Pack/crouch_to_standing_idle", false),
                ("Walk", "Locomotion Pack/walking", true),
            })
            {
                var (clip, _) = Copy(name, loop, "villager ");
                if (clip == null) return null;
                var made = machine.AddState(state);
                made.motion = clip;
                if (state == "Idle") machine.defaultState = made;
            }
            AssetDatabase.SaveAssets();
            return controller;
        }

        /// <summary>A clip copied out of the package, looping or not, in place (the
        /// hips' steady drift across the ground taken out), and the speed it
        /// moves at, m/s (the original's).</summary>
        static (AnimationClip, float) Copy(string name, bool loop = true, string prefix = "")
        {
            var source = AssetDatabase.LoadAllAssetsAtPath(Package + name + ".fbx")
                .OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
            if (source == null)
            {
                Debug.LogError($"Mixamo clip missing: {Package}{name}.fbx");
                return (null, 0);
            }
            var copy = Object.Instantiate(source);
            copy.name = name.Split('/').Last();
            foreach (var binding in AnimationUtility.GetCurveBindings(source))
            {
                bool across = binding.propertyName == "m_LocalPosition.x" || binding.propertyName == "m_LocalPosition.z";
                if (!across || !binding.path.EndsWith("mixamorig:Hips")) continue;
                var curve = AnimationUtility.GetEditorCurve(source, binding);
                float slope = (curve.Evaluate(source.length) - curve.Evaluate(0f)) / Mathf.Max(source.length, 1e-3f);
                var keys = curve.keys;
                for (int i = 0; i < keys.Length; i++)
                {
                    keys[i].value -= slope * keys[i].time;
                    keys[i].inTangent -= slope;
                    keys[i].outTangent -= slope;
                }
                curve.keys = keys;
                AnimationUtility.SetEditorCurve(copy, binding, curve);
            }
            var settings = AnimationUtility.GetAnimationClipSettings(copy);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(copy, settings);
            string path = Clips + prefix + copy.name + ".anim";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(copy, path);
            float speed = Mathf.Max(0.1f, new Vector2(source.averageSpeed.x, source.averageSpeed.z).magnitude);
            return (copy, speed);
        }
    }
}
