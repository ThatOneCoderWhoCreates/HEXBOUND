using System;
using System.IO;
using System.IO.Compression;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace HEXBOUND.Editor
{
    /// <summary>
    /// Menu: HEXBOUND > Build Windows Game. Builds HEXBOUND.exe into Builds/Windows and packs the whole
    /// folder into Builds/HEXBOUND-Windows.zip, ready to send to a friend.
    /// </summary>
    public static class HexboundWindowsBuild
    {
        private const string OutputFolder = "Builds/Windows";
        private const string ZipPath = "Builds/HEXBOUND-Windows.zip";

        [MenuItem("HEXBOUND/Build Windows Game (.exe + zip)", priority = 40)]
        public static void Build()
        {
            if (!File.Exists(HexboundEditorSetup.MainScenePath))
            {
                EditorUtility.DisplayDialog("HEXBOUND", "Main scene not found. Use HEXBOUND > Recreate Main Scene first.", "OK");
                return;
            }

            PlayerSettings.productName = "HEXBOUND";
            PlayerSettings.runInBackground = true;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.visibleInBackground = true;

            if (Directory.Exists(OutputFolder)) Directory.Delete(OutputFolder, true);
            Directory.CreateDirectory(OutputFolder);

            var options = new BuildPlayerOptions
            {
                scenes = new[] { HexboundEditorSetup.MainScenePath },
                locationPathName = Path.Combine(OutputFolder, "HEXBOUND.exe"),
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
