using System;
using System.IO;
using System.IO.Compression;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace HEXBOUND.Editor
{
    /// <summary>
    /// Menu: HEXBOUND > Build Windows Game. Builds WUSHEN.exe (the game's player-facing name) into Builds/Windows and packs the whole
    /// folder into Builds/WUSHEN-Windows.zip, ready to send to a friend.
    /// </summary>
    [InitializeOnLoad]
    public static class HexboundWindowsBuild
    {
        private const string ShaderKeepFolder = "Assets/_Project/Resources/Materials/ShaderKeep";

        static HexboundWindowsBuild()
        {
            EditorApplication.delayCall += () =>
            {
                if (!EditorApplication.isPlayingOrWillChangePlaymode) EnsureShaderKeepMaterials();
            };
        }

        /// <summary>
        /// The game makes its materials in code, so a build would leave out the URP shaders it needs
        /// (Unity only ships shaders that some asset uses). These small materials in Resources keep
        /// those shaders and the exact variants (emission, transparent, additive) in every build.
        /// </summary>
        public static void EnsureShaderKeepMaterials()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Resources/Materials")) AssetDatabase.CreateFolder("Assets/_Project/Resources", "Materials");
            if (!AssetDatabase.IsValidFolder(ShaderKeepFolder)) AssetDatabase.CreateFolder("Assets/_Project/Resources/Materials", "ShaderKeep");

            Keep("Lit_Opaque", "Universal Render Pipeline/Lit", false, false);
            Keep("Lit_Emission", "Universal Render Pipeline/Lit", true, false);
            Keep("Unlit_Opaque", "Universal Render Pipeline/Unlit", false, false);
            Keep("Unlit_Additive", "Universal Render Pipeline/Unlit", false, true);
            Keep("Particles_Additive", "Universal Render Pipeline/Particles/Unlit", false, true);
            Keep("Skybox_Panoramic", "Skybox/Panoramic", false, false);
            AssetDatabase.SaveAssets();
        }

        private static void Keep(string name, string shaderName, bool emission, bool additive)
        {
            string path = ShaderKeepFolder + "/" + name + ".mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) return;
            Shader shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogWarning("HEXBOUND: shader " + shaderName + " not found; " + name + " was not created.");
                return;
            }
            var m = new Material(shader);
            if (emission)
            {
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                m.SetColor("_EmissionColor", Color.white);
            }
            if (additive)
            {
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", 2f);
                m.SetOverrideTag("RenderType", "Transparent");
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)BlendMode.One);
                m.SetFloat("_ZWrite", 0f);
                m.SetFloat("_Cull", 0f);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = (int)RenderQueue.Transparent;
            }
            AssetDatabase.CreateAsset(m, path);
        }
        private const string OutputFolder = "Builds/Windows";
        private const string ZipPath = "Builds/WUSHEN-Windows.zip";

        [MenuItem("HEXBOUND/Build Windows Game (.exe + zip)", priority = 40)]
        public static void Build()
        {
            if (!File.Exists(HexboundEditorSetup.MainScenePath))
            {
                EditorUtility.DisplayDialog("HEXBOUND", "Main scene not found. Use HEXBOUND > Recreate Main Scene first.", "OK");
                return;
            }

            EnsureShaderKeepMaterials();

            PlayerSettings.productName = "WUSHEN";
            PlayerSettings.companyName = "Pixtel Studios";
            PlayerSettings.runInBackground = true;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.visibleInBackground = true;

            if (Directory.Exists(OutputFolder)) Directory.Delete(OutputFolder, true);
            Directory.CreateDirectory(OutputFolder);

            var options = new BuildPlayerOptions
            {
                scenes = new[] { HexboundEditorSetup.MainScenePath },
                locationPathName = Path.Combine(OutputFolder, "WUSHEN.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                Debug.LogError("HEXBOUND: Windows build failed (" + report.summary.result + "). See the errors above.");
                EditorUtility.DisplayDialog("HEXBOUND", "Build failed. Check the Console for errors.", "OK");
                return;
            }

            // Debug symbols are not needed by players.
            foreach (string dir in Directory.GetDirectories(OutputFolder, "*DoNotShip", SearchOption.TopDirectoryOnly))
            {
                Directory.Delete(dir, true);
            }

            try
            {
                if (File.Exists(ZipPath)) File.Delete(ZipPath);
                ZipFile.CreateFromDirectory(OutputFolder, ZipPath, System.IO.Compression.CompressionLevel.Optimal, true);
            }
            catch (Exception e)
            {
                Debug.LogWarning("HEXBOUND: build succeeded but the zip could not be made: " + e.Message);
            }

            double mb = report.summary.totalSize / (1024.0 * 1024.0);
            Debug.Log("HEXBOUND: Windows build done (" + mb.ToString("0") + " MB). Send " + Path.GetFullPath(ZipPath));
            EditorUtility.RevealInFinder(File.Exists(ZipPath) ? ZipPath : OutputFolder);
        }
    }
}
