using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace PinballUI.EditorTools
{
    /// <summary>
    /// メニュー「Tools > Pinball UI > Setup Japanese Font」
    /// 同梱の日本語フォント（BIZ UDPゴシック、SIL Open Font License）から TextMeshPro の Font Asset（Dynamic）を作り、
    ///   ・HUD のテーマ（PinballHudTheme）のフォントに設定
    ///   ・TextMeshPro 全体の予備フォント（Fallback）に追加
    ///   ・シーンにある HUD の文字にも反映
    /// Font Asset は TextMeshPro 標準のメニュー（Create > TextMeshPro > Font Asset）と同じ手順で作る。
    /// HUD を作るとき（Create HUD In Scene）にも自動で呼ばれる。
    /// </summary>
    public static class PinballFontSetup
    {
        const string FontDir = "Assets/PinballUI/Fonts";
        const string SourceFontPath = FontDir + "/BIZUDPGothic-Regular.ttf";
        const string FontAssetPath = FontDir + "/BIZUDPGothic-Regular SDF.asset";

        [MenuItem("Tools/Pinball UI/Setup Japanese Font")]
        public static void SetupFromMenu()
        {
            Apply(EnsureFontAsset(false));
        }

        [MenuItem("Tools/Pinball UI/Recreate Japanese Font")]
        public static void RecreateFromMenu()
        {
            if (AssetDatabase.LoadAssetAtPath<Object>(FontAssetPath) != null) AssetDatabase.DeleteAsset(FontAssetPath);
            Apply(EnsureFontAsset(false));
        }

        static void Apply(TMP_FontAsset fa)
        {
            if (fa == null) return;
            ApplyToThemes(fa);
            AddToFallback(fa);
            ApplyToScene(fa);
            Selection.activeObject = fa;
            EditorGUIUtility.PingObject(fa);
            Debug.Log($"[Pinball UI] 日本語フォントを設定しました：{AssetDatabase.GetAssetPath(fa)}");
        }

        /// <summary>日本語の Font Asset を用意して返す（無ければ作る）。applyEverywhere ならテーマ・予備フォント・シーンにも反映</summary>
        public static TMP_FontAsset EnsureFontAsset(bool applyEverywhere)
        {
            var fa = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (fa == null) fa = CreateWithTmpMenu() ?? CreateManually();
            if (fa == null) return null;

            if (fa.atlasPopulationMode != AtlasPopulationMode.Dynamic)
            {
                fa.atlasPopulationMode = AtlasPopulationMode.Dynamic;
                EditorUtility.SetDirty(fa);
                AssetDatabase.SaveAssets();
            }

            if (applyEverywhere)
            {
                ApplyToThemes(fa);
                AddToFallback(fa);
                ApplyToScene(fa);
            }
            return fa;
        }

        /// <summary>TextMeshPro 標準の「Font Asset を作る」メニューを、同梱の ttf を選んだ状態で実行する</summary>
        static TMP_FontAsset CreateWithTmpMenu()
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
            if (font == null)
            {
                Debug.LogError($"[Pinball UI] {SourceFontPath} が見つかりません。zip の Assets/PinballUI/Fonts をコピーしてください。");
                return null;
            }

            var prev = Selection.objects;
            Selection.activeObject = font;
            bool ok = EditorApplication.ExecuteMenuItem("Assets/Create/TextMeshPro/Font Asset/SDF")
                      || EditorApplication.ExecuteMenuItem("Assets/Create/TextMeshPro/Font Asset");
            Selection.objects = prev;
            if (!ok) return null;

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // 作られた Font Asset（ttf と同じフォルダ、名前は「BIZUDPGothic-Regular SDF」）を探す
            var fa = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (fa != null) return fa;
            foreach (var guid in AssetDatabase.FindAssets("t:TMP_FontAsset", new[] { FontDir }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileName(path).StartsWith("BIZUDPGothic-Regular"))
                    return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            }
            return null;
        }

        /// <summary>標準メニューが使えないときの予備（スクリプトで作る）</summary>
        static TMP_FontAsset CreateManually()
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
            if (font == null) return null;

            var fa = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024,
                AtlasPopulationMode.Dynamic, true);
            if (fa == null)
            {
                Debug.LogError("[Pinball UI] Font Asset を作れませんでした。TextMeshPro が使える状態か確認してください。");
                return null;
            }
            fa.name = Path.GetFileNameWithoutExtension(FontAssetPath);

            AssetDatabase.CreateAsset(fa, FontAssetPath);
            if (fa.atlasTextures != null)
            {
                for (int i = 0; i < fa.atlasTextures.Length; i++)
                {
                    var tex = fa.atlasTextures[i];
                    if (tex == null) continue;
                    tex.name = $"{fa.name} Atlas {i}";
                    AssetDatabase.AddObjectToAsset(tex, fa);
                }
            }
            if (fa.material != null)
            {
                fa.material.name = $"{fa.name} Material";
                AssetDatabase.AddObjectToAsset(fa.material, fa);
            }
            EditorUtility.SetDirty(fa);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(FontAssetPath);
            return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
        }

        static void ApplyToThemes(TMP_FontAsset fa)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:PinballHudTheme"))
            {
                var theme = AssetDatabase.LoadAssetAtPath<PinballHudTheme>(AssetDatabase.GUIDToAssetPath(guid));
                if (theme == null) continue;
                theme.font = fa;
                EditorUtility.SetDirty(theme);
            }
            AssetDatabase.SaveAssets();
        }

        static void AddToFallback(TMP_FontAsset fa)
        {
            try
            {
                var list = TMP_Settings.fallbackFontAssets;
                if (list != null && !list.Contains(fa))
                {
                    list.RemoveAll(f => f == null);
                    list.Add(fa);
                    if (TMP_Settings.instance != null) EditorUtility.SetDirty(TMP_Settings.instance);
                    AssetDatabase.SaveAssets();
                }
            }
            catch
            {
                Debug.LogWarning("[Pinball UI] TextMeshPro の設定が見つからないため、予備フォントへの追加はしませんでした（Import TMP Essential Resources を先に）。");
            }
        }

        static void ApplyToScene(TMP_FontAsset fa)
        {
#if UNITY_2023_1_OR_NEWER
            var huds = Object.FindObjectsByType<PinballHud>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
            var huds = Object.FindObjectsOfType<PinballHud>(true);
#endif
            foreach (var hud in huds)
            {
                foreach (var t in hud.GetComponentsInChildren<TextMeshProUGUI>(true))
                {
                    Undo.RecordObject(t, "Set Japanese Font");
                    t.font = fa;
                    EditorUtility.SetDirty(t);
                }
            }
        }
    }
}
