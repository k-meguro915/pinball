using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PinballFrontend
{
    /// <summary>
    /// タイトルの「せってい」で開く画面（タイトルのシーンの上に重ねる。シーンは増やさない）。
    /// 行を変えるとすぐ FEGameSettings に残す。「もどる」と Esc／パッドの B で閉じる。
    /// </summary>
    public class FESettingsPanel : MonoBehaviour
    {
        const float RowHeight = 72f;

        TMP_FontAsset font;
        Action onClose;
        EventSystem es;
        RectTransform root;
        FEOptionRow splitRow;
        TextMeshProUGUI description, hint;
        bool hintForPad;
        int openedFrame;

        public static FESettingsPanel Open(TMP_FontAsset font, Action onClose)
        {
            var p = new GameObject("FE Settings").AddComponent<FESettingsPanel>();
            p.font = font;
            p.onClose = onClose;
            p.Build();
            return p;
        }

        void Build()
        {
            es = FEUI.EnsureEventSystem();
            openedFrame = Time.frameCount;
            root = FEUI.CreateCanvas("FE Settings Canvas", true, 10);
            root.SetParent(transform, false);

            var kicker = FEUI.CreateText(root, "Kicker", "SETTINGS", 26f, FEUI.Accent, TextAlignmentOptions.Center, font);
            Place(kicker.rectTransform, 0.5f, 0.945f, 800f, 40f);
            kicker.characterSpacing = 12f;
            var header = FEUI.CreateText(root, "Header", "せってい", 60f, FEUI.TextColor, TextAlignmentOptions.Center, font);
            Place(header.rectTransform, 0.5f, 0.895f, 1200f, 80f);
            header.fontStyle = FontStyles.Bold;

            var col = FEUI.CreateColumn(root, "Body", 1100f, 22f, new Vector2(0.5f, 0.835f), new Vector2(0.5f, 1f));
            var panel = FEUI.CreatePanelColumn(col, "Options", 1100f, 10f, 24f, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));

            var gs = FEGameSettings.Current;
            splitRow = FEOptionRow.Create(panel, "SplitView", "1 VS 1 の画面", new[] { "台全体", "自陣だけ" },
                () => (int)gs.splitView, i => gs.splitView = (FESplitView)i, OnChanged, font, RowHeight);

            description = FEUI.CreateText(col, "Description", "", 30f, FEUI.SubTextColor, TextAlignmentOptions.Center, font, 50f);

            var back = FEUI.CreateButton(col, "Back", "もどる", font, 84f, Close);

            splitRow.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnDown = back };
            back.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = splitRow };

            var bar = FEUI.CreateImage(root, "Footer", new Color(0f, 0f, 0f, 0.35f));
            bar.raycastTarget = false;
            var brt = bar.rectTransform;
            brt.anchorMin = new Vector2(0f, 0f);
            brt.anchorMax = new Vector2(1f, 0f);
            brt.pivot = new Vector2(0.5f, 0f);
            brt.sizeDelta = new Vector2(0f, 72f);
            hint = FEUI.CreateText(bar.transform, "Hint", "", 28f, FEUI.SubTextColor, TextAlignmentOptions.Center, font);
            FEUI.Stretch(hint.rectTransform);
            UpdateHint(true);

            UpdateDescription();
            es.SetSelectedGameObject(splitRow.gameObject);
            StartCoroutine(FEUI.FadeIn(root.GetComponent<CanvasGroup>(), 0.2f));
        }

        void OnChanged()
        {
            FEGameSettings.Current.Save();
            UpdateDescription();
        }

        void UpdateDescription()
        {
            description.text = FEGameSettings.Current.splitView == FESplitView.OwnHalf
                ? "左右に分けた画面で、自分の陣地を大きく映す。"
                : "左右に分けた画面で、台全体を映す。";
        }

        static void Place(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(x, y);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(w, h);
        }

        void UpdateHint(bool force)
        {
            bool pad = FEUI.HasGamepad();
            if (!force && pad == hintForPad) return;
            hintForPad = pad;
            hint.text = pad
                ? "上下：えらぶ　　　左右：かえる　　　A：決定　　　B：もどる"
                : "↑↓：えらぶ　　　←→：かえる　　　Enter：決定　　　Esc：もどる";
        }

        void Update()
        {
            if (splitRow != null) FEUI.KeepSelection(es, splitRow.gameObject);
            if (hint != null) UpdateHint(false);
            // 開いたフレームの決定・もどるは読まない
            if (Time.frameCount != openedFrame && FEUI.CancelPressed(es)) Close();
        }

        void Close()
        {
            FEGameSettings.Current.Save();
            var cb = onClose;
            onClose = null;
            Destroy(gameObject);
            cb?.Invoke();
        }
    }
}
