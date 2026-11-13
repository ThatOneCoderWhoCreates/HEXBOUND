using UnityEditor;
using UnityEngine;

namespace HEXBOUND.Editor
{
    /// <summary>
    /// Music files in Resources/Audio/Music are long, so they are imported as compressed streaming audio
    /// instead of being fully decompressed into memory. Also fixes files imported before this script existed.
    /// </summary>
    [InitializeOnLoad]
    public sealed class HexboundAudioImport : AssetPostprocessor
    {
        private const string MusicFolder = "Assets/_Project/Resources/Audio/Music";

        static HexboundAudioImport()
        {
            EditorApplication.delayCall += FixExisting;
        }

        private void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith(MusicFolder)) return;
            Apply((AudioImporter)assetImporter);
        }

        private static bool Apply(AudioImporter importer)
        {
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            bool changed = settings.loadType != AudioClipLoadType.Streaming || settings.compressionFormat != AudioCompressionFormat.Vorbis || !importer.loadInBackground;
            settings.loadType = AudioClipLoadType.Streaming;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.6f;
            importer.defaultSampleSettings = settings;
            importer.loadInBackground = true;
            return changed;
        }

        private static void FixExisting()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!AssetDatabase.IsValidFolder(MusicFolder)) return;
            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { MusicFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as AudioImporter;
                if (importer != null && Apply(importer)) importer.SaveAndReimport();
            }
        }
    }
}
