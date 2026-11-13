using UnityEditor;

namespace HEXBOUND.Editor
{
    /// <summary>
    /// KinemIQ motion-capture files in Resources/Animations/KinemIQ are imported as Generic rigs
    /// (needed to play them through a PlayableGraph), without cameras, lights or materials.
    /// </summary>
    public sealed class HexboundAnimationImport : AssetPostprocessor
    {
        private const string Folder = "Assets/_Project/Resources/Animations/KinemIQ";

        private void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(Folder)) return;
            var importer = (ModelImporter)assetImporter;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importAnimation = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.animationCompression = ModelImporterAnimationCompression.KeyframeReduction;
        }
    }
}
