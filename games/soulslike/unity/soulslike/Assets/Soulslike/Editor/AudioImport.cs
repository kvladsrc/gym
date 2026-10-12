using UnityEditor;

namespace Soulslike.EditorTools
{
    /// <summary>
    /// Import settings for the studio's sounds: effects are played in 3D, so
    /// mono (they are dual-mono anyway) and preloaded to avoid a hitch on the
    /// first swing; music and the bonfire stay stereo and are decompressed on
    /// load, because the looper reads their samples to find the silent ends.
    /// </summary>
    public class AudioImport : AssetPostprocessor
    {
        static readonly string[] Loops = { "AmbientMusic", "BossMusic", "Bonfire" };

        void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith("Assets/Soulslike/Art/Audio/")) return;
            var importer = (AudioImporter)assetImporter;
            bool loop = System.Array.Exists(Loops, name => assetPath.EndsWith("/" + name + ".wav"));
            importer.forceToMono = !loop;
            importer.loadInBackground = loop;
            var settings = importer.defaultSampleSettings;
            settings.loadType = UnityEngine.AudioClipLoadType.DecompressOnLoad;
            settings.preloadAudioData = true;
            settings.compressionFormat = loop ? UnityEngine.AudioCompressionFormat.Vorbis : UnityEngine.AudioCompressionFormat.PCM;
            settings.quality = 0.7f;
            importer.defaultSampleSettings = settings;
        }
    }
}
