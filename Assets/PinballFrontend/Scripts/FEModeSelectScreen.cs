using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PinballFrontend
{
    /// <summary>
    /// モード選択（FE_ModeSelect）。ルールと、そのルールの設定を選んで「スタート」で本編へ。
    /// 「もどる」と Esc／パッドの B でタイトルへ。本編の「タイトルへ」はここに戻ってくる。
    /// 選んだ設定は PlayerPrefs に残し、次に開いたときの初期値にする。
    /// </summary>
    public class FEModeSelectScreen : MonoBehaviour
    {
        public FEConfig config;
        [Tooltip("空なら TMP の既定のフォント（TMP Settings。日本語は BIZ UDP ゴシックのフォールバックで出る）")]
        public TMP_FontAsset font;

        const float RowHeight = 72f;

        FEMatchSettings settings;
        EventSystem es;

        readonly List<FEOptionRow> rows = new List<FEOptionRow>();
        FEOptionRow ruleRow, cpuRow, targetRow, outfieldRow;
        TextMeshProUGUI description, hint;
        Button startButton, tutorialButton, backButton;
        RectTransform root;
        bool hintForPad;

        void Start()
        {
            Time.timeScale = 1f;
            settings = FEMatchSettings.LoadLast();
            es = FEUI.EnsureEventSystem();
            Build();
            UpdateVisibility();
            es.SetSelectedGameObject(ruleRow.gameObject);
            StartCoroutine(FEUI.FadeIn(root.GetComponent<CanvasGroup>(), 0.25f));
        }

        void Build()
        {
            root = FEUI.CreateCanvas("FE Mode Select Canvas");

            // ---- 見出し ----
            var kicker = FEUI.CreateText(root, "Kicker", "MODE SELECT", 26f, FEUI.Accent, TextAlignmentOptions.Center, font);
            Place(kicker.rectTransform, 0.5f, 0.945f, 800f, 40f);
            kicker.characterSpacing = 12f;
            var header = FEUI.CreateText(root, "Header", "モードをえらぶ", 60f, FEUI.TextColor, TextAlignmentOptions.Center, font);
            Place(header.rectTransform, 0.5f, 0.895f, 1200f, 80f);
            header.fontStyle = FontStyles.Bold;

            var col = FEUI.CreateColumn(root, "Body", 1100f, 22f, new Vector2(0.5f, 0.835f), new Vector2(0.5f, 1f));

            // ---- 設定の行（角丸の板の中） ----
            var panel = FEUI.CreatePanelColumn(col, "Options", 1100f, 10f, 24f, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));

            ruleRow = Row(panel, "Rule", "ルール", new[] { "ピンボール対戦", "ドッジボール" },
                () => (int)settings.rule, i => settings.rule = (FERule)i);
            Row(panel, "Mode", "モード", new[] { "VS CPU", "1 VS 1" },
                () => (int)settings.playMode, i => settings.playMode = (FEPlayMode)i);
            cpuRow = Row(panel, "Cpu", "CPU の強さ", new[] { "EASY", "NORMAL", "HARD" },
                () => (int)settings.cpuLevel, i => settings.cpuLevel = (FECpuLevel)i);

            // ピンボール対戦
            targetRow = Row(panel, "Target", "先取点", new[] { "3 点", "5 点", "10 点" },
                () => Mathf.Max(0, System.Array.IndexOf(FEMatchSettings.TargetScoreOptions, settings.targetScore)),
                i => settings.targetScore = FEMatchSettings.TargetScoreOptions[i]);

            // ドッジボール
            outfieldRow = Row(panel, "Outfield", "外野の投げ方", new[] { "全自動", "ボタン" },
                () => (int)settings.outfieldControl, i => settings.outfieldControl = (FEControl)i);

            // ---- ルールの説明 ----
            description = FEUI.CreateText(col, "Description", "", 30f, FEUI.SubTextColor, TextAlignmentOptions.Center, font, 50f);

            // ---- ボタン ----
            var buttons = new GameObject("Buttons", typeof(RectTransform));
            buttons.transform.SetParent(col, false);
            buttons.AddComponent<LayoutElement>().preferredHeight = 84f;
            var h = buttons.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 24f;
            h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = h.childForceExpandHeight = true;
            startButton = FEUI.CreateButton(buttons.transform, "Start", "スタート", font, 84f, OnStart);
            startButton.GetComponentInChildren<TextMeshProUGUI>().fontStyle = FontStyles.Bold;
            tutorialButton = FEUI.CreateButton(buttons.transform, "Tutorial", "あそびかた", font, 84f, OnTutorial);
            backButton = FEUI.CreateButton(buttons.transform, "Back", "もどる", font, 84f, OnBack);

            // ---- 下の帯：操作の案内 ----
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

        FEOptionRow Row(Transform parent, string name, string label, string[] options,
                        System.Func<int> get, System.Action<int> set)
        {
            var r = FEOptionRow.Create(parent, name, label, options, get, set, UpdateVisibility, font, RowHeight);
            rows.Add(r);
            return r;
        }

        /// <summary>ルールとモードに合わせて、使わない行を隠す</summary>
        void UpdateVisibility()
        {
            bool versus = settings.rule == FERule.Versus;
            bool vsCpu = settings.IsVsCpu;

            cpuRow.gameObject.SetActive(vsCpu);
            targetRow.gameObject.SetActive(versus);
            outfieldRow.gameObject.SetActive(!versus);

            description.text = versus
                ? "<color=#" + ColorUtility.ToHtmlStringRGB(FEUI.P1Color) + ">●</color>　ゴールに球を入れて点を取る。先に決めた点数を取った方の勝ち。"
                : "<color=#" + ColorUtility.ToHtmlStringRGB(FEUI.P2Color) + ">●</color>　相手の選手 3 人を、全員アウトにしたら勝ち。";

            foreach (var r in rows) r.Refresh();
            UpdateNavigation();
        }

        /// <summary>
        /// 上下の移り先を決めておく。自動に任せると、最後の行から下へ行ったときに、
        /// 位置の近い「もどる」が先に選ばれることがあるため（スタートを先にする）。
        /// 行 ⇔ 行は上下、最後の行 → スタート、スタート ⇔ あそびかた ⇔ もどる は左右、どのボタンからも上は最後の行
        /// </summary>
        void UpdateNavigation()
        {
            var visible = rows.FindAll(r => r.gameObject.activeSelf);
            for (int i = 0; i < visible.Count; i++)
            {
                var nav = new Navigation { mode = Navigation.Mode.Explicit };
                nav.selectOnUp = i > 0 ? visible[i - 1] : null;
                nav.selectOnDown = i < visible.Count - 1 ? visible[i + 1] : (Selectable)startButton;
                visible[i].navigation = nav; // 左右は FEOptionRow が値の切り替えに使う
            }
            Selectable last = visible.Count > 0 ? visible[visible.Count - 1] : null;
            startButton.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit, selectOnUp = last, selectOnRight = tutorialButton,
            };
            tutorialButton.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit, selectOnUp = last, selectOnLeft = startButton, selectOnRight = backButton,
            };
            backButton.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit, selectOnUp = last, selectOnLeft = tutorialButton,
            };
        }

        void Update()
        {
            if (ruleRow != null) FEUI.KeepSelection(es, ruleRow.gameObject);
            if (hint != null) UpdateHint(false);
            if (FEUI.CancelPressed(es)) OnBack();
        }

        void OnStart()
        {
            if (config == null)
            {
                Debug.LogError("[PinballFrontend] FEModeSelectScreen に FEConfig が入っていません");
                return;
            }
            settings.SaveAsLast();
            FESession.StartMatch(settings, config.MatchScenePath(settings.rule), config.modeSelectScenePath);
        }

        /// <summary>あそびかた：いま選んでいるルールと設定で、左で試し打ち・右に説明</summary>
        void OnTutorial()
        {
            if (config == null)
            {
                Debug.LogError("[PinballFrontend] FEModeSelectScreen に FEConfig が入っていません");
                return;
            }
            settings.SaveAsLast();
            FESession.StartTutorial(settings, config.MatchScenePath(settings.rule), config.modeSelectScenePath,
                                    config.tutorialGameWidth, config.TutorialContent(settings.rule));
        }

        void OnBack()
        {
            if (config == null) return;
            FESession.LoadScene(config.titleScenePath);
        }
    }
}
