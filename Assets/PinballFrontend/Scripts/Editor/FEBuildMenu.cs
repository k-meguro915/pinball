using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace PinballFrontend.EditorTools
{
    /// <summary>
    /// Build Settings の並びと、Windows 向けのビルド。
    /// ・Select Config：Data/FEConfig.asset を開く（無ければ作る）
    /// ・Apply Build Settings：FEConfig の順番（タイトル → モード選択 → 対戦 → ドッジボール）で Build Settings を並べ直し、
    ///   FEConfig の Product Name / Company Name を Player Settings に入れる
    /// ・Build Windows (x64)：上と同じことをしてから、Builds/Windows/ にビルドする
    /// </summary>
    public static class FEBuildMenu
    {
        const string ConfigPath = "Assets/PinballFrontend/Data/FEConfig.asset";
        const string BuildDir = "Builds/Windows";

        /// <summary>本編が Shader.Find で探すシェーダー。マテリアルから使われていないとビルドから落ちる</summary>
        static readonly string[] ShadersFoundByName = { "Sprites/Default", "Universal Render Pipeline/Lit" };

        [MenuItem("Tools/Pinball Frontend/Select Config", priority = 0)]
        static void SelectConfig()
        {
            Selection.activeObject = LoadOrCreateConfig();
            EditorGUIUtility.PingObject(Selection.activeObject);
        }

        [MenuItem("Tools/Pinball Frontend/Apply Build Settings", priority = 20)]
        static void ApplyBuildSettingsMenu()
        {
            var config = LoadOrCreateConfig();
            var scenes = SceneList(config, out var notes);
            if (scenes.Count == 0) return;
            string msg = "次の順番で Build Settings を並べ直します。\n\n" + Numbered(scenes) +
                         "\n\nProduct Name：" + config.productName + "\nCompany Name：" + config.companyName + Notes(notes);
            if (!EditorUtility.DisplayDialog("Pinball Frontend", msg, "並べ直す", "やめる")) return;
            Apply(scenes, config);
        }

        [MenuItem("Tools/Pinball Frontend/Build Windows (x64)", priority = 21)]
        static void BuildWindows()
        {
            var config = LoadOrCreateConfig();
            var scenes = SceneList(config, out var notes);
            if (scenes.Count == 0) return;

            string product = string.IsNullOrWhiteSpace(config.productName) ? PlayerSettings.productName : config.productName;
            string exe = Path.Combine(BuildDir, SafeFileName(product) + ".exe").Replace('\\', '/');
            var shaderWarnings = CheckShaders(scenes);
            string msg = "Windows (x64) 向けにビルドします。\n\n出力先：" + exe +
                         "\nProduct Name：" + product + "　Company Name：" + config.companyName +
                         "\n\nシーン：\n" + Numbered(scenes) +
                         Notes(notes.Concat(shaderWarnings).ToList());
            if (!EditorUtility.DisplayDialog("Pinball Frontend", msg, "ビルドする", "やめる")) return;

            Apply(scenes, config);
            var options = new BuildPlayerOptions
            {
                scenes = scenes.ToArray(),
                locationPathName = exe,
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(options);
            var s = report.summary;
            if (s.result == BuildResult.Succeeded)
            {
                Debug.Log($"[PinballFrontend] ビルドできました：{exe}（{s.totalSize / (1024f * 1024f):0.0} MB、{s.totalTime.TotalSeconds:0} 秒、警告 {s.totalWarnings} 件）");
                EditorUtility.RevealInFinder(exe);
            }
            else
            {
                Debug.LogError($"[PinballFrontend] ビルドできませんでした：{s.result}（エラー {s.totalErrors} 件）。Console のエラーを見てください");
            }
        }

        // ---------------------------------------------------------------

        /// <summary>確認の画面を出さずに Build Settings を並べ直す（シーンを作ったときなど）</summary>
        internal static void ApplyBuildSettingsSilently()
        {
            var config = LoadOrCreateConfig();
            var scenes = SceneList(config, out var notes);
            if (scenes.Count == 0) return;
            Apply(scenes, config);
            foreach (var n in notes) Debug.Log("[PinballFrontend] " + n);
        }

        internal static FEConfig LoadOrCreateConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<FEConfig>(ConfigPath);
            if (config != null) return config;
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath));
            config = ScriptableObject.CreateInstance<FEConfig>();
            AssetDatabase.CreateAsset(config, ConfigPath);
            AssetDatabase.SaveAssets();
            Debug.Log("[PinballFrontend] " + ConfigPath + " を作りました");
            return config;
        }

        /// <summary>FEConfig の順番でシーンを並べる。無いシーンは飛ばし、そのことを notes に書く</summary>
        static List<string> SceneList(FEConfig c, out List<string> notes)
        {
            notes = new List<string>();
            var list = new List<string>();

            if (Exists(c.titleScenePath)) list.Add(c.titleScenePath);
            else notes.Add("FE_Title がありません。Tools > Pinball Frontend > Create FE Scenes で作ってください");

            if (Exists(c.modeSelectScenePath)) list.Add(c.modeSelectScenePath);
            else notes.Add("FE_ModeSelect がまだ無いので飛ばしました");

            foreach (var p in new[] { c.versusScenePath, c.dodgeballScenePath })
            {
                if (Exists(p)) list.Add(p);
                else notes.Add("本編のシーンが見つかりません：" + p);
            }

            if (list.Count == 0)
                EditorUtility.DisplayDialog("Pinball Frontend", "並べるシーンがありません。\n\n" + string.Join("\n", notes), "OK");
            return list;
        }

        static void Apply(List<string> scenes, FEConfig config)
        {
            EditorBuildSettings.scenes = scenes.Select(p => new EditorBuildSettingsScene(p, true)).ToArray();
            ApplyPlayerSettings(config);
            AssetDatabase.SaveAssets(); // ProjectSettings にすぐ書き出す
            Debug.Log("[PinballFrontend] Build Settings を並べ直しました：\n" + Numbered(scenes));
        }

        /// <summary>FEConfig の Product Name / Company Name を Player Settings に入れる（空なら今のまま）</summary>
        static void ApplyPlayerSettings(FEConfig config)
        {
            if (config == null) return;
            var changed = new List<string>();
            if (!string.IsNullOrWhiteSpace(config.productName) && PlayerSettings.productName != config.productName)
            {
                changed.Add("Product Name：" + PlayerSettings.productName + " → " + config.productName);
                PlayerSettings.productName = config.productName;
            }
            if (!string.IsNullOrWhiteSpace(config.companyName) && PlayerSettings.companyName != config.companyName)
            {
                changed.Add("Company Name：" + PlayerSettings.companyName + " → " + config.companyName);
                PlayerSettings.companyName = config.companyName;
            }
            if (changed.Count > 0) Debug.Log("[PinballFrontend] Player Settings を変えました：" + string.Join("、", changed));
        }

        /// <summary>
        /// Shader.Find で探すシェーダーが、Always Included Shaders か、ビルドに入るシーンのマテリアルで使われているかを見る。
        /// どちらでもなければビルドから落ちて、実行時に見つからない
        /// </summary>
        static List<string> CheckShaders(List<string> scenes)
        {
            var warnings = new List<string>();
            var included = new HashSet<Shader>();

            var gs = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset").FirstOrDefault();
            if (gs != null)
            {
                var prop = new SerializedObject(gs).FindProperty("m_AlwaysIncludedShaders");
                if (prop != null)
                    for (int i = 0; i < prop.arraySize; i++)
                        if (prop.GetArrayElementAtIndex(i).objectReferenceValue is Shader sh) included.Add(sh);
            }
            foreach (var dep in AssetDatabase.GetDependencies(scenes.ToArray(), true))
                if (dep.EndsWith(".mat"))
                {
                    var m = AssetDatabase.LoadAssetAtPath<Material>(dep);
                    if (m != null && m.shader != null) included.Add(m.shader);
                }

            foreach (var name in ShadersFoundByName)
            {
                var sh = Shader.Find(name);
                if (sh == null) warnings.Add("シェーダー「" + name + "」が Editor でも見つかりません");
                else if (!included.Contains(sh))
                    warnings.Add("シェーダー「" + name + "」がビルドに入らないかもしれません（Project Settings > Graphics > Always Included Shaders に入れるか確かめてください）");
            }
            return warnings;
        }

        static bool Exists(string path) =>
            !string.IsNullOrEmpty(path) && AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null;

        static string Numbered(List<string> scenes) =>
            string.Join("\n", scenes.Select((p, i) => $"{i}: {p}"));

        static string Notes(List<string> notes) =>
            notes.Count == 0 ? "" : "\n\n注意：\n・" + string.Join("\n・", notes);

        static string SafeFileName(string name)
        {
            foreach (var ch in Path.GetInvalidFileNameChars()) name = name.Replace(ch, '_');
            return string.IsNullOrWhiteSpace(name) ? "Game" : name;
        }
    }
}
