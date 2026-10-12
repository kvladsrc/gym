using UnityEditor;

namespace Soulslike.EditorTools
{
    /// <summary>
    /// Import settings for rigged characters and their animations, both
    /// Generic: Art/Characters holds the studio's rigs (3d-to-rig, Mixamo's
    /// skeleton), the Mixamo clips come from the repository's shared package
    /// dev.bonfirelit.mixamo (animations/mixamo). The rigs share Mixamo's
    /// skeleton, bone names and axes, so the clips play bone for bone, as on
    /// Mixamo's site; Humanoid retargeting stiffened the legs (a run's knee
    /// bent to 72° instead of 32°).
    /// Clips play in place: the hips' forward motion is root motion, which the
    /// Animator does not apply; rotation and height stay in the pose.
    /// Locomotion and idles loop.
    /// </summary>
    public class CharacterImport : AssetPostprocessor
    {
        const string Characters = "Assets/Soulslike/Art/Characters/";
        const string Animations = "Packages/dev.bonfirelit.mixamo/";
        const string RootBone = "mixamorig:Hips";
        static readonly string[] Loops = { "idle", "walk", "run", "strafe", "ginga" };

        void OnPreprocessModel()
        {
            bool character = assetPath.StartsWith(Characters);
            if (!character && !assetPath.StartsWith(Animations)) return;
            var importer = (ModelImporter)assetImporter;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = character ? ModelImporterAvatarSetup.NoAvatar : ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = !character;
            importer.materialImportMode = character ? ModelImporterMaterialImportMode.ImportStandard : ModelImporterMaterialImportMode.None;
            if (!character) importer.motionNodeName = RootBone;
        }

        void OnPreprocessAnimation()
        {
            if (!assetPath.StartsWith(Animations)) return;
            var importer = (ModelImporter)assetImporter;
            string name = System.IO.Path.GetFileNameWithoutExtension(assetPath).ToLowerInvariant();
            bool loop = System.Array.Exists(Loops, word => name.Contains(word));
            var clips = importer.defaultClipAnimations;
            foreach (var clip in clips)
            {
                clip.loopTime = loop;
                clip.lockRootRotation = true;
                clip.keepOriginalOrientation = true;
                clip.lockRootHeightY = true;
                clip.keepOriginalPositionY = true;
                clip.lockRootPositionXZ = false;
            }
            importer.clipAnimations = clips;
        }
    }
}
