using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace PinballFrontend.EditorTools
{
    /// <summary>
    /// メニュー「Tools > Pinball Frontend > Create FE Scenes」。
    /// FE_Title と FE_ModeSelect を作る（カメラ、EventSystem、画面の部品だけ。画面の中身は実行時にコードで作る）。
    /// 作ったあと Build Settings を並べ直す（FE_Title が 0 番になる）。
    /// </summary>
    public static class FESceneMenu
    {
        const string FontPath = "Assets/PinballUI/Fonts/BIZUDPGothic-Regular SDF.asset";

        [MenuItem("Tools/Pinball Frontend/Create FE Scenes", priority = 10)]
        static void CreateScenes()
        {
            var config = FEBuildMenu.LoadOrCreateConfig();
            string[] paths = { config.titleScenePath, config.modeSelectScenePath };

            string existing = "";
            foreach (var p in paths) if (File.Exists(p)) existing += "\n" + p;
            if (existing.Length > 0 &&
                !EditorUtility.DisplayDialog("Pinball Frontend", "次のシーンはもうあります。作り直しますか？\n" + existing, "作り直す", "やめる"))
                return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font == null) Debug.LogWarning("[PinballFrontend] " + FontPath + " が見つからないので、TMP の既定のフォントを使います");

            CreateScene(config.titleScenePath, "FE Title", go =>
            {
                var s = go.AddComponent<FETitleScreen>();
                s.config = config;
                s.font = font;
            });
            CreateScene(config.modeSelectScenePath, "FE Mode Select", go =>
            {
                var s = go.AddComponent<FEModeSelectScreen>();
                s.config = config;
                s.font = font;
            });

            FEBuildMenu.ApplyBuildSettingsSilently();
            EditorSceneManager.OpenScene(config.titleScenePath);
            Debug.Log("[PinballFrontend] FE_Title と FE_ModeSelect を作りました。FE_Title で再生して確かめてください");
        }

        static void CreateScene(string path, string screenName, System.Action<GameObject> addScreen)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = FEUI.Background;

            var esGo = new GameObject("EventSystem");
            esGo.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            esGo.AddComponent<InputSystemUIInputModule>();
#else
            esGo.AddComponent<StandaloneInputModule>();
#endif

            addScreen(new GameObject(screenName));

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            EditorSceneManager.SaveScene(scene, path);
        }
    }
}
