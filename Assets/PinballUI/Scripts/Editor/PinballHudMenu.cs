using Pinball;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace PinballUI.EditorTools
{
    /// <summary>
    /// メニュー「Tools > Pinball UI > Create HUD In Scene」
    ///   ・見た目の設定 Assets/PinballUI/Data/PinballHudTheme.asset が無ければ作る
    ///   ・シーンに HUD（左右パネル・中央の知らせ）と盤面上の表示を置く
    ///   ・GameManager の内蔵 HUD をオフにする（タイトル画面はそのまま）
    /// 置いたあとは、各パネルを Scene / Game ビューで動かして位置・大きさを調整できる。
    /// </summary>
    public static class PinballHudMenu
    {
        const string DataDir = "Assets/PinballUI/Data";
        const string ThemePath = DataDir + "/PinballHudTheme.asset";

        [MenuItem("Tools/Pinball UI/Create HUD In Scene")]
        public static void Create()
        {
            var theme = GetOrCreateTheme();
            if (theme.font == null || theme.font == SafeDefaultFont())
            {
                var jp = PinballFontSetup.EnsureFontAsset(false); // 同梱の日本語フォントを使う
                if (jp != null) { theme.font = jp; EditorUtility.SetDirty(theme); }
            }

            var old = GameObject.Find("PinballHUD");
            if (old != null && !EditorUtility.DisplayDialog("Pinball UI",
                    "シーンにすでに PinballHUD があります。作り直しますか？（位置の調整は元に戻ります）", "作り直す", "やめる"))
                return;
            if (old != null) Undo.DestroyObjectImmediate(old);

            // ---- Canvas ----
            var root = new GameObject("PinballHUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            Undo.RegisterCreatedObjectUndo(root, "Create Pinball HUD");
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;
            var group = root.GetComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;

            var hud = root.AddComponent<PinballHud>();
            hud.theme = theme;
            hud.scaler = scaler;
            hud.group = group;

            // ---- プレイヤーのパネル（P1 = 左下、P2 = 右上） ----
            hud.players = new[]
            {
                BuildPanel(root.transform, "P1Panel", theme, 0, new Vector2(0f, 0f), new Vector2(40f, 40f), false),
                BuildPanel(root.transform, "P2Panel", theme, 1, new Vector2(1f, 1f), new Vector2(-40f, -40f), true),
            };

            // ---- 共通（左上） ----
            var common = NewPanel(root.transform, "CommonPanel", theme, new Vector2(0f, 1f), new Vector2(40f, -40f), TextAnchor.UpperLeft);
            hud.commonPanel = common;
            hud.commonText = UIUtil.NewText("Text", common, "5点先取", theme.commonFontSize, theme.dimTextColor,
                TextAlignmentOptions.TopLeft, theme.font);

            // ---- 中央の知らせ ----
            var center = UIUtil.NewRect("Banner", root.transform);
            center.anchorMin = center.anchorMax = new Vector2(0.5f, 0.5f);
            center.sizeDelta = new Vector2(1200f, 300f);
            var v = center.gameObject.AddComponent<VerticalLayoutGroup>();
            v.childAlignment = TextAnchor.MiddleCenter;
            v.childControlWidth = v.childControlHeight = true;
            v.childForceExpandWidth = v.childForceExpandHeight = false;
            hud.banner = UIUtil.NewText("Main", center, "", theme.bannerFontSize, Color.white, TextAlignmentOptions.Center, theme.font);
            hud.banner.fontStyle = FontStyles.Bold;
            hud.banner.outlineWidth = 0.2f;
            hud.banner.outlineColor = new Color32(0, 0, 0, 200);
            hud.bannerSub = UIUtil.NewText("Sub", center, "", theme.bannerSubFontSize, Color.white, TextAlignmentOptions.Center, theme.font);
            hud.bannerSub.outlineWidth = 0.2f;
            hud.bannerSub.outlineColor = new Color32(0, 0, 0, 200);

            // ---- 盤面上の表示 ----
            var board = new GameObject("BoardIndicators");
            board.transform.SetParent(root.transform, false);
            board.AddComponent<BoardIndicators>().theme = theme;

            // ---- GameManager の内蔵 HUD をオフ ----
#if UNITY_2023_1_OR_NEWER
            var gm = Object.FindFirstObjectByType<GameManager>();
#else
            var gm = Object.FindObjectOfType<GameManager>();
#endif
            if (gm != null)
            {
                Undo.RecordObject(gm, "Disable built-in HUD");
                gm.useBuiltInHud = false;
                EditorUtility.SetDirty(gm);
            }

            PinballFontSetup.EnsureFontAsset(true); // テーマ・TextMeshPro の予備フォント・シーンの文字に日本語フォントを反映

            EditorSceneManager.MarkSceneDirty(root.scene);
            Selection.activeGameObject = root;
            Debug.Log($"[Pinball UI] HUD を配置しました。シーンを保存してください（Ctrl+S）。見た目と文言は {ThemePath} で編集できます。");
        }

        [MenuItem("Tools/Pinball UI/Use Pad Button Names (L R A X)")]
        public static void UsePadButtonNames()
        {
            var t = GetOrCreateTheme();
            Undo.RecordObject(t, "Use Pad Button Names");
            t.ApplyDefaultPadLabels();
            EditorUtility.SetDirty(t);
            AssetDatabase.SaveAssets();
            Selection.activeObject = t;
            Debug.Log("[Pinball UI] ボタンの表示名を L / R / A / X / Lスティック にしました（テーマの Key Labels で変更できます）。");
        }

        [MenuItem("Tools/Pinball UI/Select HUD Theme")]
        public static void SelectTheme()
        {
            var t = GetOrCreateTheme();
            Selection.activeObject = t;
            EditorGUIUtility.PingObject(t);
        }

        static TMP_FontAsset SafeDefaultFont()
        {
            try { return TMP_Settings.defaultFontAsset; } catch { return null; }
        }

        static PinballHudTheme GetOrCreateTheme()
        {
            var theme = AssetDatabase.LoadAssetAtPath<PinballHudTheme>(ThemePath);
            if (theme != null) return theme;

            if (!AssetDatabase.IsValidFolder("Assets/PinballUI")) AssetDatabase.CreateFolder("Assets", "PinballUI");
            if (!AssetDatabase.IsValidFolder(DataDir)) AssetDatabase.CreateFolder("Assets/PinballUI", "Data");

            theme = ScriptableObject.CreateInstance<PinballHudTheme>();
            theme.circleSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            try { theme.font = TMP_Settings.defaultFontAsset; } catch { theme.font = null; }
            AssetDatabase.CreateAsset(theme, ThemePath);
            AssetDatabase.SaveAssets();
            return theme;
        }

        // ===================== 部品 =====================

        static RectTransform NewPanel(Transform parent, string name, PinballHudTheme theme, Vector2 corner, Vector2 pos, TextAnchor align)
        {
            var rt = UIUtil.NewRect(name, parent);
            rt.anchorMin = rt.anchorMax = corner;
            rt.pivot = corner;
            rt.anchoredPosition = pos;

            var img = rt.gameObject.AddComponent<Image>();
            img.color = theme.panelColor;
            img.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            img.type = Image.Type.Sliced;
            img.raycastTarget = false;

            var v = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(28, 28, 20, 22);
            v.spacing = 10f;
            v.childAlignment = align;
            v.childControlWidth = v.childControlHeight = true;
            v.childForceExpandWidth = v.childForceExpandHeight = false;

            var fit = rt.gameObject.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return rt;
        }

        static PinballHud.PlayerPanel BuildPanel(Transform parent, string name, PinballHudTheme theme, int player,
            Vector2 corner, Vector2 pos, bool right)
        {
            var align = right ? TextAnchor.UpperRight : TextAnchor.UpperLeft;
            var textAlign = right ? TextAlignmentOptions.TopRight : TextAlignmentOptions.TopLeft;
            var panel = new PinballHud.PlayerPanel();

            panel.root = NewPanel(parent, name, theme, corner, pos, align);
            panel.background = panel.root.GetComponent<Image>();

            // 名前・点数 ＋ サーブの印
            var head = UIUtil.NewRect("Header", panel.root);
            var hh = head.gameObject.AddComponent<HorizontalLayoutGroup>();
            hh.spacing = 16f;
            hh.childAlignment = right ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
            hh.childControlWidth = hh.childControlHeight = true;
            hh.childForceExpandWidth = hh.childForceExpandHeight = false;
            panel.header = UIUtil.NewText("Score", head, player == 0 ? "P1  0" : "CPU  0", theme.headerFontSize,
                theme.PlayerColor(player), textAlign, theme.font);
            panel.header.fontStyle = FontStyles.Bold;
            panel.serveBadge = UIUtil.NewText("ServeBadge", head, theme.serveBadge, theme.badgeFontSize,
                theme.PlayerColor(player), textAlign, theme.font);
            if (right) panel.serveBadge.transform.SetAsFirstSibling();

            // ブロッカーの HP（実行時に点を並べる）
            panel.hpRow = UIUtil.NewRect("BlockerHP", panel.root);
            var hp = panel.hpRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            hp.spacing = 22f;
            hp.childAlignment = align;
            hp.childControlWidth = hp.childControlHeight = true;
            hp.childForceExpandWidth = hp.childForceExpandHeight = false;

            // 状態の一言
            panel.status = UIUtil.NewText("Status", panel.root, "", theme.statusFontSize, theme.dimTextColor, textAlign, theme.font);

            // 今押せるボタン
            panel.prompts = UIUtil.NewRect("Prompts", panel.root);
            var pv = panel.prompts.gameObject.AddComponent<VerticalLayoutGroup>();
            pv.spacing = 8f;
            pv.childAlignment = align;
            pv.childControlWidth = pv.childControlHeight = true;
            pv.childForceExpandWidth = pv.childForceExpandHeight = false;

            panel.chipTemplate = BuildChip(panel.prompts, theme, right);
            panel.chipTemplate.SetActive(false);
            return panel;
        }

        /// <summary>操作の案内1行のひな形：[ボタン名] 説明</summary>
        static GameObject BuildChip(Transform parent, PinballHudTheme theme, bool right)
        {
            var chip = UIUtil.NewRect("ChipTemplate", parent);
            var h = chip.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 14f;
            h.childAlignment = right ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
            h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = h.childForceExpandHeight = false;

            var key = UIUtil.NewImage("Key", chip, AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"), theme.keyColor);
            key.type = Image.Type.Sliced;
            var kh = key.gameObject.AddComponent<HorizontalLayoutGroup>();
            kh.padding = new RectOffset(14, 14, 4, 4);
            kh.childAlignment = TextAnchor.MiddleCenter;
            kh.childControlWidth = kh.childControlHeight = true;
            kh.childForceExpandWidth = kh.childForceExpandHeight = false;
            var kle = key.gameObject.AddComponent<LayoutElement>();
            kle.minWidth = 64f;
            var keyText = UIUtil.NewText("KeyText", key.transform, "A", theme.keyFontSize, theme.keyTextColor,
                TextAlignmentOptions.Center, theme.font);
            keyText.fontStyle = FontStyles.Bold;

            UIUtil.NewText("Text", chip, "説明", theme.promptFontSize, theme.textColor,
                right ? TextAlignmentOptions.Right : TextAlignmentOptions.Left, theme.font);

            if (right) key.transform.SetAsLastSibling(); // 右側パネルは「説明 [ボタン]」の順
            return chip.gameObject;
        }
    }
}
