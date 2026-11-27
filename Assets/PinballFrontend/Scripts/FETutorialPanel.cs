using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace PinballFrontend
{
    /// <summary>
    /// あそびかたの右の説明の画面（uGUI）。左は本編の台をそのまま映す。
    /// ・上にタブ（ページ）。十字キーの左右／キーボードの Q・E／タブをクリックで切り替える（遊ぶ操作とは重ならない）
    /// ・ページごとに画像と文（FETutorialContent）。文の中の {L} などは、つないでいる機器のボタン名に置き換える
    /// ・下に試合の様子（HUD の代わり）と操作の案内
    /// フォントは TMP の既定（TMP Settings。日本語は BIZ UDP ゴシックのフォールバックで出る）。
    /// </summary>
    public class FETutorialPanel : MonoBehaviour
    {
        static readonly Regex Token = new Regex(@"\{(\w+)\}");

        FEMatchSettings settings;
        IFETutorialRule rule;
        FETutorialContent.Page[] pages;
        int pageIndex;

        readonly List<(Image bg, TextMeshProUGUI label)> tabs = new List<(Image, TextMeshProUGUI)>();
        GameObject imageBox;
        RawImage image;
        AspectRatioFitter imageFit;
        TextMeshProUGUI body, status, hint;
        string lastStatus;
        int lastPadCount = -1;

        public void Init(FEMatchSettings matchSettings, float gameWidth, FETutorialContent content, IFETutorialRule tutorialRule)
        {
            settings = matchSettings;
            rule = tutorialRule;
            pages = content != null && content.pages != null && content.pages.Length > 0 ? content.pages : FallbackPages();

            FEUI.EnsureEventSystem(); // タブをマウスで押すため（タブは十字キーやスティックでは選ばれない）
            Build(gameWidth);
            ShowPage(0);
        }

        static FETutorialContent.Page[] FallbackPages() => new[]
        {
            new FETutorialContent.Page
            {
                title = "操作",
                body = "（説明の中身がまだありません。Tools > Pinball Frontend > Create Tutorial Content で作れます）\n\n{ControlsTable}",
            },
        };

        // ---------------------------------------------------------------
        // 組み立て

        void Build(float gameWidth)
        {
            // 本編の HUD や操作説明（sortingOrder 100）より下、本編の台より上
            var root = FEUI.CreateCanvas("FE Tutorial Canvas", false, 50);

            var side = new GameObject("Panel", typeof(RectTransform));
            var srt = (RectTransform)side.transform;
            srt.SetParent(root, false);
            srt.anchorMin = new Vector2(gameWidth, 0f);
            srt.anchorMax = Vector2.one;
            srt.offsetMin = srt.offsetMax = Vector2.zero;
            FEUI.CreateBackground(srt);
            side.AddComponent<RectMask2D>(); // 背景の光が台の側へはみ出さないように

            // 台との境目の線
            var edge = FEUI.CreateImage(srt, "Edge", new Color(FEUI.Accent.r, FEUI.Accent.g, FEUI.Accent.b, 0.5f));
            edge.raycastTarget = false;
            var ert = edge.rectTransform;
            ert.anchorMin = new Vector2(0f, 0f);
            ert.anchorMax = new Vector2(0f, 1f);
            ert.pivot = new Vector2(0f, 0.5f);
            ert.sizeDelta = new Vector2(4f, 0f);

            // 中身は縦に並べる（下の帯の分を空ける）
            var col = new GameObject("Content", typeof(RectTransform));
            var crt = (RectTransform)col.transform;
            crt.SetParent(srt, false);
            crt.anchorMin = Vector2.zero;
            crt.anchorMax = Vector2.one;
            crt.offsetMin = new Vector2(48f, 72f + 20f);
            crt.offsetMax = new Vector2(-40f, -26f);
            var v = col.AddComponent<VerticalLayoutGroup>();
            v.spacing = 14f;
            v.childControlWidth = v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;

            bool versus = settings.rule == FERule.Versus;
            var kicker = FEUI.CreateText(crt, "Kicker", "HOW TO PLAY", 22f, FEUI.Accent, TextAlignmentOptions.MidlineLeft, null, 30f);
            kicker.characterSpacing = 10f;
            var title = FEUI.CreateText(crt, "Title", "あそびかた　—　" + (versus ? "ピンボール対戦" : "ドッジボール"),
                                        44f, FEUI.TextColor, TextAlignmentOptions.MidlineLeft, null, 58f);
            title.fontStyle = FontStyles.Bold;
            title.textWrappingMode = TextWrappingModes.NoWrap;

            BuildTabs(crt);

            // 画像（16:9。無いページでは隠す）
            imageBox = new GameObject("ImageBox", typeof(RectTransform));
            imageBox.transform.SetParent(crt, false);
            imageBox.AddComponent<LayoutElement>().preferredHeight = 420f;
            var frame = FEUI.CreateImage(imageBox.transform, "Frame", Color.white);
            FEUI.SetRounded(frame, FEUI.PanelColor);
            frame.raycastTarget = false;
            FEUI.Stretch(frame.rectTransform);
            image = new GameObject("Image", typeof(RectTransform)).AddComponent<RawImage>();
            image.transform.SetParent(imageBox.transform, false);
            image.raycastTarget = false;
            FEUI.Stretch(image.rectTransform);
            imageFit = image.gameObject.AddComponent<AspectRatioFitter>();
            imageFit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            imageFit.aspectRatio = 16f / 9f;

            // 文（1〜2 行の短い文は大きく。操作の一覧のように長いときは小さくして収める）
            body = FEUI.CreateText(crt, "Body", "", 36f, FEUI.TextColor, TextAlignmentOptions.TopLeft, null);
            body.enableAutoSizing = true;
            body.fontSizeMin = 18f;
            body.fontSizeMax = 36f;
            body.lineSpacing = 8f;
            var ble = body.gameObject.AddComponent<LayoutElement>();
            ble.flexibleHeight = 1f;
            ble.minHeight = 120f;

            // 試合の様子（HUD の代わり）
            var box = FEUI.CreateImage(crt, "StatusBox", Color.white);
            FEUI.SetRounded(box, FEUI.PanelColor);
            box.raycastTarget = false;
            box.gameObject.AddComponent<LayoutElement>().preferredHeight = 96f;
            status = FEUI.CreateText(box.transform, "Status", "", 26f, FEUI.TextColor, TextAlignmentOptions.MidlineLeft, null);
            FEUI.Stretch(status.rectTransform);
            status.rectTransform.offsetMin = new Vector2(24f, 8f);
            status.rectTransform.offsetMax = new Vector2(-24f, -8f);

            // 操作の案内
            var bar = FEUI.CreateImage(srt, "Footer", new Color(0f, 0f, 0f, 0.35f));
            bar.raycastTarget = false;
            var brt = bar.rectTransform;
            brt.anchorMin = new Vector2(0f, 0f);
            brt.anchorMax = new Vector2(1f, 0f);
            brt.pivot = new Vector2(0.5f, 0f);
            brt.sizeDelta = new Vector2(0f, 72f);
            hint = FEUI.CreateText(bar.transform, "Hint", "", 24f, FEUI.SubTextColor, TextAlignmentOptions.Center, null);
            FEUI.Stretch(hint.rectTransform);
        }

        void BuildTabs(RectTransform parent)
        {
            var row = new GameObject("Tabs", typeof(RectTransform));
            row.transform.SetParent(parent, false);
            row.AddComponent<LayoutElement>().preferredHeight = 58f;
            var h = row.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 10f;
            h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = true;
            h.childForceExpandHeight = true;

            for (int i = 0; i < pages.Length; i++)
            {
                int index = i;
                var bg = FEUI.CreateImage(row.transform, "Tab" + i, Color.white);
                FEUI.SetRounded(bg, FEUI.ItemNormal);
                var b = bg.gameObject.AddComponent<Button>();
                b.transition = Selectable.Transition.None;
                b.navigation = new Navigation { mode = Navigation.Mode.None }; // 十字キーやスティックでは選ばない
                b.onClick.AddListener(() => ShowPage(index));
                var label = FEUI.CreateText(bg.transform, "Label", pages[i].title, 26f, FEUI.TextColor,
                                            TextAlignmentOptions.Center, null);
                FEUI.Stretch(label.rectTransform);
                label.textWrappingMode = TextWrappingModes.NoWrap;
                tabs.Add((bg, label));
            }
        }

        // ---------------------------------------------------------------
        // ページ

        void ShowPage(int index)
        {
            if (pages.Length == 0) return;
            pageIndex = ((index % pages.Length) + pages.Length) % pages.Length;
            var page = pages[pageIndex];

            for (int i = 0; i < tabs.Count; i++)
            {
                bool on = i == pageIndex;
                tabs[i].bg.color = on ? FEUI.ItemSelected : FEUI.ItemNormal;
                tabs[i].label.color = on ? FEUI.Accent : FEUI.SubTextColor;
                tabs[i].label.fontStyle = on ? FontStyles.Bold : FontStyles.Normal;
            }

            imageBox.SetActive(page.image != null);
            if (page.image != null)
            {
                image.texture = page.image;
                imageFit.aspectRatio = (float)page.image.width / Mathf.Max(1, page.image.height);
            }
            RenderBody();
        }

        void RenderBody()
        {
            var page = pages[pageIndex];
            body.text = Token.Replace(page.body ?? "", m => Expand(m.Groups[1].Value));
            UpdateHint();
        }

        string Expand(string token)
        {
            if (token == "ControlsTable") return ControlsTable();
            string label = Label(token);
            return label ?? "{" + token + "}";
        }

        /// <summary>ボタン名（1 VS 1 で P2 の名前がちがうときは「P2：…」を添える）</summary>
        string Label(string token)
        {
            string p1 = rule?.ButtonLabel(token, 0);
            if (p1 == null) return null;
            string s = Key(p1);
            if (!settings.IsVsCpu)
            {
                string p2 = rule.ButtonLabel(token, 1);
                if (!string.IsNullOrEmpty(p2) && p2 != p1) s += "（P2：" + Key(p2) + "）";
            }
            return s;
        }

        static string Key(string name) =>
            "<color=#" + ColorUtility.ToHtmlStringRGB(FEUI.Accent) + "><b>" + (string.IsNullOrEmpty(name) ? "―" : name) + "</b></color>";

        /// <summary>操作の一覧（VS CPU なら P1 だけ、1 VS 1 なら P1 と P2）</summary>
        string ControlsTable()
        {
            if (rule == null) return "";
            bool two = !settings.IsVsCpu;
            string sub = "#" + ColorUtility.ToHtmlStringRGB(FEUI.SubTextColor);
            var sb = new StringBuilder();
            sb.Append("<color=").Append(sub).Append(">やること");
            if (two) sb.Append("<pos=60%>P1<pos=80%>P2");
            else sb.Append("<pos=66%>ボタン");
            sb.Append("</color>\n");
            foreach (var (action, token) in rule.ControlRows)
            {
                sb.Append(action);
                if (two)
                    sb.Append("<pos=60%>").Append(Key(rule.ButtonLabel(token, 0)))
                      .Append("<pos=80%>").Append(Key(rule.ButtonLabel(token, 1)));
                else
                    sb.Append("<pos=66%>").Append(Key(rule.ButtonLabel(token, 0)));
                sb.Append("\n");
            }
            return sb.ToString().TrimEnd('\n');
        }

        void UpdateHint()
        {
            bool pad = FEUI.HasGamepad();
            hint.text = pad
                ? "十字キー ←→：ページ　　Start：この設定で試合をはじめる　　Select：やめる"
                : "Q／E：ページ　　Enter：この設定で試合をはじめる　　Esc：やめる";
        }

        // ---------------------------------------------------------------

        public void SetStatus(string text)
        {
            if (status == null || text == lastStatus) return;
            lastStatus = text;
            status.text = text;
        }

        void Update()
        {
            int step = PageStep();
            if (step != 0) ShowPage(pageIndex + step);

            // パッドをつないだり外したりしたら、ボタン名と案内を描き直す
            int pads = PadCount();
            if (pads != lastPadCount)
            {
                lastPadCount = pads;
                if (pages != null && pages.Length > 0) RenderBody();
            }
        }

        static int PageStep()
        {
#if ENABLE_INPUT_SYSTEM
            int step = 0;
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.qKey.wasPressedThisFrame) step--;
                if (kb.eKey.wasPressedThisFrame) step++;
            }
            foreach (var g in Gamepad.all)
            {
                if (g.dpad.left.wasPressedThisFrame) step--;
                if (g.dpad.right.wasPressedThisFrame) step++;
            }
            return step;
#else
            return (Input.GetKeyDown(KeyCode.E) ? 1 : 0) - (Input.GetKeyDown(KeyCode.Q) ? 1 : 0);
#endif
        }

        static int PadCount()
        {
#if ENABLE_INPUT_SYSTEM
            return Gamepad.all.Count;
#else
            return 0;
#endif
        }
    }
}
