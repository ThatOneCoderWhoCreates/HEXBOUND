using System.IO;
using System.Linq;
using HEXBOUND.App;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HEXBOUND.Editor
{
    /// <summary>
    /// Creates the main scene (one GameObject with HexboundGame) the first time the project loads,
    /// adds it to the build, and makes Play start from it. Menu: HEXBOUND.
    /// </summary>
    [InitializeOnLoad]
    public static class HexboundEditorSetup
    {
        public const string MainScenePath = "Assets/_Project/Scenes/Core/Main.unity";
        private const string PlayFromMainKey = "HEXBOUND.PlayFromMain";

        static HexboundEditorSetup()
        {
            EditorApplication.delayCall += Initialize;
        }

        private static bool PlayFromMain
        {
            get { return EditorPrefs.GetBool(PlayFromMainKey, true); }
            set { EditorPrefs.SetBool(PlayFromMainKey, value); }
        }

        private static void Initialize()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!File.Exists(MainScenePath)) CreateMainScene(false);
            EnsureBuildSettings();
            ApplyPlayModeStartScene();
        }

        [MenuItem("HEXBOUND/Open Main Scene", priority = 1)]
        public static void OpenMainScene()
        {
            if (!File.Exists(MainScenePath)) CreateMainScene(false);
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
            }
        }

        [MenuItem("HEXBOUND/Recreate Main Scene", priority = 2)]
        public static void RecreateMainScene()
        {
            CreateMainScene(true);
        }

        [MenuItem("HEXBOUND/Play From Main Scene", priority = 20)]
        public static void TogglePlayFromMain()
        {
            PlayFromMain = !PlayFromMain;
            ApplyPlayModeStartScene();
        }

        [MenuItem("HEXBOUND/Play From Main Scene", true)]
        public static bool TogglePlayFromMainValidate()
        {
            Menu.SetChecked("HEXBOUND/Play From Main Scene", PlayFromMain);
            return true;
        }

        private static void CreateMainScene(bool overwrite)
        {
            if (File.Exists(MainScenePath) && !overwrite) return;
            string folder = Path.GetDirectoryName(MainScenePath);
            if (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder)) Directory.CreateDirectory(folder);

            try
            {
                Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                var root = new GameObject("HEXBOUND");
                SceneManager.MoveGameObjectToScene(root, scene);
                root.AddComponent<HexboundGame>();
                EditorSceneManager.SaveScene(scene, MainScenePath);
                EditorSceneManager.CloseScene(scene, true);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("HEXBOUND: could not create the main scene automatically (" + e.Message + "). Save your open scene, then use HEXBOUND > Recreate Main Scene.");
                return;
            }
            AssetDatabase.Refresh();
            EnsureBuildSettings();
            ApplyPlayModeStartScene();
            Debug.Log("HEXBOUND: created " + MainScenePath + ". Press Play to start the game.");
        }

        private static void EnsureBuildSettings()
        {
            if (!File.Exists(MainScenePath)) return;
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            if (scenes.Length > 0 && scenes[0].path == MainScenePath) return;
            var list = scenes.Where(s => s.path != MainScenePath).ToList();
            list.Insert(0, new EditorBuildSettingsScene(MainScenePath, true));
            EditorBuildSettings.scenes = list.ToArray();
        }

        private static void ApplyPlayModeStartScene()
        {
            if (PlayFromMain && File.Exists(MainScenePath))
            {
                EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MainScenePath);
            }
            else
            {
                EditorSceneManager.playModeStartScene = null;
            }
        }
    }
}
