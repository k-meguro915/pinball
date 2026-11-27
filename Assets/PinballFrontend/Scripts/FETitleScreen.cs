using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PinballFrontend
{
    /// <summary>
    /// タイトル画面（FE_Title）。「スタート」でモード選択へ、「終了」でアプリを終える。
    /// 画面は Start() でコードから組み立てる。
    /// </summary>
    public class FETitleScreen : MonoBehaviour
    {
        public FEConfig config;
        [Tooltip("空なら TMP の既定のフォント（TMP Settings。日本語は BIZ UDP ゴシックのフォールバックで出る）")]
        public TMP_FontAsset font;

        EventSystem es;
        Button startButton;
        TextMeshProUGUI hint;
        bool hintForPad;

        void Start()
        {
            Time.timeScale = 1f;
            es = FEUI.EnsureEventSystem();
            var root = Build();
            es.SetSelectedGameObject(startButton.gameObject);
            StartCoroutine(FEUI.FadeIn(root.GetComponent<CanvasGroup>(), 0.35f));
        }

        RectTransform Build()
        {
            var root = FEUI.CreateCanvas("FE Title Canvas");

            // ---- ゲームの名前 ----
            var title = FEUI.CreateText(root, "Title", config != null ? config.gameTitle : "PINBALL",
                                        168f, FEUI.TextColor, TextAlignmentOptions.Center, font);
            Place(title.rectTransform, 0.5f, 0.66f, 1600f, 220f);
            title.fontStyle = FontStyles.Bold;
            title.characterSpacing = 14f;
            title.textWrappingMode = TextWrappingModes.NoWrap;
            FEUI.AddGlow(title, new Color(FEUI.Accent.r, FEUI.Accent.g, FEUI.Accent.b, 0.45f), 0.8f, 0.35f);

            var line = FEUI.CreateImage(root, "Underline", FEUI.Accent);
            line.raycastTarget = false;
            Place(line.rectTransform, 0.5f, 0.555f, 420f, 4f);

            // 遊べる 2 つのルール（本編の P1 / P2 の色の点を添える）
            var sub = FEUI.CreateText(root, "Rules",
                "<color=#" + ColorUtility.ToHtmlStringRGB(FEUI.P1Color) + ">●</color>　ピンボール対戦" +
                "　　　<color=#" + ColorUtility.ToHtmlStringRGB(FEUI.P2Color) + ">●</color>　ドッジボール",
                36f, FEUI.SubTextColor, TextAlignmentOptions.Center, font);
            Place(sub.rectTransform, 0.5f, 0.505f, 1200f, 60f);
            sub.characterSpacing = 3f;

            // ---- メニュー ----
            var col = FEUI.CreateColumn(root, "Menu", 520f, 18f, new Vector2(0.5f, 0.42f), new Vector2(0.5f, 1f));
            startButton = FEUI.CreateButton(col, "Start", "スタート", font, 88f, OnStart);
            FEUI.CreateButton(col, "Quit", "終了", font, 72f, FEUI.Quit);

            // ---- 下の帯：操作の案内と版 ----
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

            var ver = FEUI.CreateText(bar.transform, "Version", "ver " + Application.version, 24f,
                                      new Color(1f, 1f, 1f, 0.35f), TextAlignmentOptions.MidlineRight, font);
            FEUI.Stretch(ver.rectTransform);
            ver.rectTransform.offsetMax = new Vector2(-32f, 0f);
            return root;
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
            hint.text = pad ? "十字キー／スティック：えらぶ　　　A：決定" : "↑↓：えらぶ　　　Enter：決定";
        }

        void Update()
        {
            if (startButton != null) FEUI.KeepSelection(es, startButton.gameObject);
            if (hint != null) UpdateHint(false);
        }

        void OnStart()
        {
            if (config == null)
            {
                Debug.LogError("[PinballFrontend] FETitleScreen に FEConfig が入っていません");
                return;
            }
            FESession.LoadScene(config.modeSelectScenePath);
        }
    }
}
