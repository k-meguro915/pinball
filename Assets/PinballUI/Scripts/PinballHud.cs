using System.Collections.Generic;
using Pinball;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PinballUI
{
    /// <summary>
    /// 試合中の画面表示（uGUI）。台の左右の空きに、各プレイヤーのパネルを出す。
    ///   P1 = 左下、P2 / CPU = 右上（自分の陣地の横）、共通 = 左上、中央 = 知らせ（ゴール・フォルトなど）
    /// パネルには「今の場面で押せるボタン」だけを出す（サーブ中・打ち合い中・キャッチ中で切り替わる）。
    ///
    /// 位置や大きさは、シーンの各パネル（RectTransform）を動かして調整できる。色・文字・文言は PinballHudTheme。
    /// 縦長の画面では、パネルを台の上下へ自動で移す（adaptToPortrait）。
    /// 1 VS 1 の画面分け（SplitScreen）の間は、P2 のパネルを右下（P2 の画面の手前）へ移す。
    /// </summary>
    public class PinballHud : MonoBehaviour
    {
        public PinballHudTheme theme;

        [System.Serializable]
        public class PlayerPanel
        {
            public RectTransform root;
            public Image background;
            public TextMeshProUGUI header;
            public TextMeshProUGUI serveBadge;
            public RectTransform hpRow;
            public TextMeshProUGUI status;
            public RectTransform prompts;
            [Tooltip("操作の案内1行分のひな形（Key / KeyText / Text を子に持つ）。見た目はこれを編集")]
            public GameObject chipTemplate;

            [System.NonSerialized] public List<Chip> chips = new List<Chip>();
            [System.NonSerialized] public List<PieceHp> hp = new List<PieceHp>();
            [System.NonSerialized] public BlockerRow builtFor;
            [System.NonSerialized] public PowerUi power;
            [System.NonSerialized] public Vector2 anchorMin, anchorMax, pivot, anchoredPos;
        }

        public class Chip
        {
            public GameObject go;
            public Image key;
            public TextMeshProUGUI keyText;
            public TextMeshProUGUI text;
        }

        /// <summary>パワーショットのゲージ（実行時に HP の行のすぐ下へ作る）</summary>
        public class PowerUi
        {
            public GameObject root;
            public TextMeshProUGUI label;
            public Image fill;
            public TextMeshProUGUI badge;
        }

        public class PieceHp
        {
            public GameObject group;
            public List<Image> pips = new List<Image>();
            public TextMeshProUGUI down;
        }

        public PlayerPanel[] players = new PlayerPanel[2];

        public RectTransform commonPanel;
        public TextMeshProUGUI commonText;
        public TextMeshProUGUI banner;
        public TextMeshProUGUI bannerSub;
        public CanvasScaler scaler;
        public CanvasGroup group;

        [Tooltip("縦長の画面では、パネルを台の上下に移す")]
        public bool adaptToPortrait = true;

        bool? lastPortrait;
        bool lastSplit;
        BlockerRow[] rows = new BlockerRow[0];
        float nextRowSearch;

        struct Prompt
        {
            public string key;
            public string text;
            public bool dim;
            public Prompt(string key, string text, bool dim = false) { this.key = key; this.text = text; this.dim = dim; }
        }
        readonly List<Prompt> buffer = new List<Prompt>();

        bool diagnosed;

        void OnEnable() { GameManager.ExternalHudActive = true; }
        void OnDisable() { GameManager.ExternalHudActive = false; }

        void Awake()
        {
            if (theme == null)
            {
                // テーマが無くても表示できるよう、初期値のテーマを使う
                theme = ScriptableObject.CreateInstance<PinballHudTheme>();
                Debug.LogWarning("[Pinball UI] PinballHUD にテーマ（PinballHudTheme）が設定されていません。初期値で表示します。");
            }
            foreach (var p in players)
            {
                if (p == null || p.root == null) continue;
                p.anchorMin = p.root.anchorMin;
                p.anchorMax = p.root.anchorMax;
                p.pivot = p.root.pivot;
                p.anchoredPos = p.root.anchoredPosition;
                if (p.chipTemplate != null) p.chipTemplate.SetActive(false);
            }
            ApplyTheme();
        }

        /// <summary>テーマの色・フォントを全体に反映する（実行中にテーマを変えたら呼ぶ）</summary>
        public void ApplyTheme()
        {
            if (theme == null) return;
            foreach (var t in GetComponentsInChildren<TextMeshProUGUI>(true))
                if (theme.font != null) t.font = theme.font;
            for (int i = 0; i < players.Length; i++)
            {
                var p = players[i];
                if (p == null) continue;
                if (p.background != null) p.background.color = theme.panelColor;
                if (p.header != null) { p.header.fontSize = theme.headerFontSize; p.header.color = theme.PlayerColor(i); }
                if (p.serveBadge != null) { p.serveBadge.fontSize = theme.badgeFontSize; p.serveBadge.color = theme.PlayerColor(i); }
                if (p.status != null) { p.status.fontSize = theme.statusFontSize; p.status.color = theme.dimTextColor; }
            }
            if (commonText != null) { commonText.fontSize = theme.commonFontSize; commonText.color = theme.dimTextColor; }
            if (banner != null) banner.fontSize = theme.bannerFontSize;
            if (bannerSub != null) { bannerSub.fontSize = theme.bannerSubFontSize; bannerSub.color = theme.textColor; }
            var commonBg = commonPanel != null ? commonPanel.GetComponent<Image>() : null;
            if (commonBg != null) commonBg.color = theme.panelColor;
        }

        void LateUpdate()
        {
            var gm = GameManager.Instance;
            if (gm == null || theme == null) return;

            bool inMatch = gm.State != MatchState.Title;
            if (group != null) group.alpha = inMatch ? 1f : 0f;
            if (!inMatch) return;

            if (!diagnosed) Diagnose();

            UpdateOrientation();
            FindRows();

            for (int p = 0; p < 2; p++) UpdatePlayer(gm, p);
            UpdateCommon(gm);
            UpdateBanner(gm);
        }

        /// <summary>最初の試合のときに、表示できない原因になりそうなものを Console に出す</summary>
        void Diagnose()
        {
            diagnosed = true;
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null || !canvas.enabled) Debug.LogWarning("[Pinball UI] PinballHUD の Canvas が無いか、無効になっています。");
            for (int i = 0; i < players.Length; i++)
            {
                var p = players[i];
                if (p == null || p.root == null) Debug.LogWarning($"[Pinball UI] P{i + 1} のパネルが設定されていません。Create HUD In Scene で作り直してください。");
                else if (!p.root.gameObject.activeInHierarchy) Debug.LogWarning($"[Pinball UI] P{i + 1} のパネルが非表示（非アクティブ）になっています。");
            }
            if (theme.font == null) Debug.LogWarning("[Pinball UI] テーマにフォントがありません。Tools > Pinball UI > Setup Japanese Font を実行してください。");
            Debug.Log($"[Pinball UI] HUD 表示中（画面 {Screen.width}×{Screen.height}）");
        }

        // ===================== 向き =====================

        void UpdateOrientation()
        {
            bool split = SplitScreen.VersusActive;
            bool portrait = !split && adaptToPortrait && Screen.height > Screen.width;
            if (lastPortrait == portrait && lastSplit == split) return;
            lastPortrait = portrait;
            lastSplit = split;

            if (scaler != null) scaler.matchWidthOrHeight = portrait ? 0f : 1f;

            for (int i = 0; i < players.Length; i++)
            {
                var p = players[i];
                if (p == null || p.root == null) continue;
                if (split && i == 1)
                {
                    // 1 VS 1 の画面分け：P2 の画面は 180 度回っていて、P2 の陣地が下に来る。P2 のパネルは右下へ
                    var a = new Vector2(1f, 0f);
                    p.root.anchorMin = a;
                    p.root.anchorMax = a;
                    p.root.pivot = a;
                    p.root.anchoredPosition = new Vector2(-Mathf.Abs(p.anchoredPos.x), Mathf.Abs(p.anchoredPos.y));
                }
                else if (!portrait)
                {
                    // シーンで置いた位置に戻す
                    p.root.anchorMin = p.anchorMin;
                    p.root.anchorMax = p.anchorMax;
                    p.root.pivot = p.pivot;
                    p.root.anchoredPosition = p.anchoredPos;
                }
                else
                {
                    // 縦長：P2 は上の帯、P1 は下の帯（左寄せ）
                    var a = i == 0 ? new Vector2(0f, 0f) : new Vector2(0f, 1f);
                    p.root.anchorMin = a;
                    p.root.anchorMax = a;
                    p.root.pivot = a;
                    p.root.anchoredPosition = new Vector2(16f, i == 0 ? 16f : -16f);
                }
            }
        }

        // ===================== 各プレイヤー =====================

        void FindRows()
        {
            if (Time.unscaledTime < nextRowSearch && rows.Length > 0) return;
            nextRowSearch = Time.unscaledTime + 1f;
#if UNITY_2023_1_OR_NEWER
            rows = FindObjectsByType<BlockerRow>(FindObjectsSortMode.None);
#else
            rows = FindObjectsOfType<BlockerRow>();
#endif
        }

        BlockerRow RowOf(int player)
        {
            foreach (var r in rows) if (r != null && r.playerIndex == player) return r;
            return null;
        }

        string Name(GameManager gm, int p)
        {
            if (p == 0) return theme.p1Name;
            return gm.mode == MatchMode.VsCpu ? theme.cpuName : theme.p2Name;
        }

        void UpdatePlayer(GameManager gm, int p)
        {
            var panel = players[p];
            if (panel == null || panel.root == null) return;

            bool isCpu = PinballInput.IsCpu(p);
            bool serving = (gm.State == MatchState.Serve || gm.State == MatchState.Playing) && gm.Server == p;

            if (panel.header != null) panel.header.text = $"{Name(gm, p)}  {gm.Scores[p]}";
            if (panel.serveBadge != null)
            {
                panel.serveBadge.gameObject.SetActive(serving);
                panel.serveBadge.text = theme.serveBadge;
            }

            UpdateHp(panel, RowOf(p), p);
            UpdatePower(gm, panel, p);

            // 状態の一言と、今押せるボタン
            buffer.Clear();
            string status = "";
            var row = RowOf(p);

            if (isCpu)
            {
                status = string.Format(theme.cpuStatus, gm.cpuLevel);
            }
            else if (gm.State == MatchState.Serve)
            {
                if (gm.Server == p)
                {
                    string route = gm.CurrentRoute == ServeRoute.OwnField ? theme.routeOwn : theme.routeOpp;
                    buffer.Add(new Prompt(Label(p, "Plunger"), theme.launchText));
                    buffer.Add(new Prompt(Label(p, "RouteToggle"), string.Format(theme.routeText, route), gm.ServeLaunched));
                }
                else
                {
                    status = theme.waitServeText;
                }
                buffer.Add(new Prompt(Label(p, "MoveBlockers"), theme.moveText));
            }
            else if (gm.State == MatchState.Playing)
            {
                var ps = PowerShot.Instance;
                if (ps != null && ps.isActiveAndEnabled && ps.IsReady(p)) status = theme.powerReadyText;

                // アウトレーン：相手のが開いていれば「狙え」、自分のが開いていれば「注意」
                bool oppOpen = false, ownOpen = false;
                foreach (var ol in Outlanes())
                {
                    if (ol == null || !ol.IsOpen || !ol.openOnBlockerDown) continue;
                    if (ol.ownerPlayer == p) ownOpen = true; else oppOpen = true;
                }
                string olText = oppOpen ? $"<color=#{ColorUtility.ToHtmlStringRGB(theme.PlayerColor(p))}>{theme.outlaneAttackText}</color>"
                              : ownOpen ? $"<color=#{ColorUtility.ToHtmlStringRGB(theme.stunColor)}>{theme.outlaneDefendText}</color>" : "";
                if (olText != "") status = status == "" ? olText : status + "\n" + olText;

                if (row != null && row.IsHolding)
                {
                    buffer.Add(new Prompt(Label(p, "BlockerCatch"), theme.catchReleaseText));
                    buffer.Add(new Prompt(Label(p, "MoveBlockers"), theme.aimText));
                }
                else
                {
                    buffer.Add(new Prompt($"{Label(p, "FlipperTop")} / {Label(p, "FlipperBottom")}", theme.flipperText));
                    buffer.Add(new Prompt(Label(p, "BlockerBounce"), theme.bounceText, row != null && !row.IsBounceReady));
                    buffer.Add(new Prompt(Label(p, "BlockerCatch"), theme.catchHoldText));
                    buffer.Add(new Prompt(Label(p, "MoveBlockers"), theme.moveText));
                }
            }

            if (panel.status != null)
            {
                panel.status.gameObject.SetActive(status != "");
                panel.status.text = status;
            }
            ShowPrompts(panel, buffer);
        }

        string Label(int p, string action)
        {
            bool pad = PinballInput.HasGamepad(p);
            string o = theme.OverrideLabel(action, pad);
            if (o != null) return o;
            string s = PinballInput.GetActionLabel(p, action);
            return string.IsNullOrEmpty(s) ? "?" : s;
        }

        void ShowPrompts(PlayerPanel panel, List<Prompt> list)
        {
            if (panel.prompts == null || panel.chipTemplate == null) return;

            while (panel.chips.Count < list.Count)
            {
                var go = Instantiate(panel.chipTemplate, panel.prompts);
                go.name = "Chip";
                var chip = new Chip
                {
                    go = go,
                    key = FindChild<Image>(go.transform, "Key"),
                    keyText = FindChild<TextMeshProUGUI>(go.transform, "KeyText"),
                    text = FindChild<TextMeshProUGUI>(go.transform, "Text"),
                };
                if (theme.font != null)
                {
                    if (chip.keyText != null) chip.keyText.font = theme.font;
                    if (chip.text != null) chip.text.font = theme.font;
                }
                panel.chips.Add(chip);
            }

            for (int i = 0; i < panel.chips.Count; i++)
            {
                var c = panel.chips[i];
                bool on = i < list.Count;
                if (c.go.activeSelf != on) c.go.SetActive(on);
                if (!on) continue;

                var pr = list[i];
                float alpha = pr.dim ? 0.4f : 1f;
                if (c.key != null) { var k = theme.keyColor; k.a *= alpha; c.key.color = k; }
                if (c.keyText != null)
                {
                    c.keyText.text = pr.key;
                    c.keyText.fontSize = theme.keyFontSize;
                    var kt = theme.keyTextColor; kt.a *= alpha; c.keyText.color = kt;
                }
                if (c.text != null)
                {
                    c.text.text = pr.text;
                    c.text.fontSize = theme.promptFontSize;
                    var t = theme.textColor; t.a *= alpha; c.text.color = t;
                }
            }
        }

        static T FindChild<T>(Transform root, string name) where T : Component
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t.GetComponent<T>();
            return null;
        }

        Outlane[] outlanes = new Outlane[0];
        float nextOutlaneSearch;

        Outlane[] Outlanes()
        {
            if (Time.unscaledTime >= nextOutlaneSearch)
            {
                nextOutlaneSearch = Time.unscaledTime + 1f;
#if UNITY_2023_1_OR_NEWER
                outlanes = FindObjectsByType<Outlane>(FindObjectsSortMode.None);
#else
                outlanes = FindObjectsOfType<Outlane>();
#endif
            }
            return outlanes;
        }

        // ===================== パワーショット =====================

        void UpdatePower(GameManager gm, PlayerPanel panel, int p)
        {
            var ps = PowerShot.Instance;
            bool show = ps != null && ps.isActiveAndEnabled && gm.State != MatchState.Title;
            if (!show)
            {
                if (panel.power != null) panel.power.root.SetActive(false);
                return;
            }
            if (panel.power == null) panel.power = BuildPower(panel, p);
            var ui = panel.power;
            ui.root.SetActive(true);

            bool ready = ps.IsReady(p);
            Color pc = theme.PlayerColor(p);
            ui.fill.fillAmount = ps.Charge01(p);
            if (ready)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 8f);
                ui.fill.color = Color.Lerp(theme.powerReadyColor, Color.white, 0.4f * pulse);
            }
            else ui.fill.color = pc;
            ui.badge.gameObject.SetActive(ready);
            ui.badge.color = theme.powerReadyColor;
            ui.badge.text = theme.powerReadyBadge;
            ui.label.text = theme.powerLabel;
        }

        PowerUi BuildPower(PlayerPanel panel, int p)
        {
            var ui = new PowerUi();
            bool right = p == 1;
            ui.root = new GameObject("PowerRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            ui.root.transform.SetParent(panel.root, false);
            // HP の行のすぐ下に置く
            if (panel.hpRow != null) ui.root.transform.SetSiblingIndex(panel.hpRow.GetSiblingIndex() + 1);
            var h = ui.root.GetComponent<HorizontalLayoutGroup>();
            h.spacing = 10f;
            h.childAlignment = right ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
            h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = h.childForceExpandHeight = false;

            ui.label = UIUtil.NewText("Label", ui.root.transform, theme.powerLabel, theme.statusFontSize, theme.dimTextColor,
                right ? TMPro.TextAlignmentOptions.Right : TMPro.TextAlignmentOptions.Left, theme.font);

            var bg = UIUtil.NewImage("Bar", ui.root.transform, UIUtil.WhiteSprite, theme.powerBarBgColor);
            var le = bg.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = theme.powerBarSize.x;
            le.preferredHeight = theme.powerBarSize.y;
            ui.fill = UIUtil.NewImage("Fill", bg.transform, UIUtil.WhiteSprite, theme.PlayerColor(p));
            var frt = ui.fill.rectTransform;
            frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one;
            frt.offsetMin = new Vector2(2f, 2f); frt.offsetMax = new Vector2(-2f, -2f);
            ui.fill.type = Image.Type.Filled;
            ui.fill.fillMethod = Image.FillMethod.Horizontal;
            ui.fill.fillOrigin = right ? (int)Image.OriginHorizontal.Right : (int)Image.OriginHorizontal.Left;

            ui.badge = UIUtil.NewText("Max", ui.root.transform, theme.powerReadyBadge, theme.statusFontSize, theme.powerReadyColor,
                TMPro.TextAlignmentOptions.Center, theme.font);
            ui.badge.fontStyle = TMPro.FontStyles.Bold;
            ui.badge.gameObject.SetActive(false);

            // P2（右寄せ）は並びを逆に：MAX・ゲージ・ラベル
            if (right)
            {
                ui.badge.transform.SetAsFirstSibling();
                ui.label.transform.SetAsLastSibling();
            }
            return ui;
        }

        // ===================== HP =====================

        void UpdateHp(PlayerPanel panel, BlockerRow row, int player)
        {
            if (panel.hpRow == null) return;
            if (row == null || row.Pieces.Length == 0)
            {
                panel.hpRow.gameObject.SetActive(false);
                return;
            }
            panel.hpRow.gameObject.SetActive(true);

            if (panel.builtFor != row) BuildHp(panel, row);

            for (int i = 0; i < row.Pieces.Length && i < panel.hp.Count; i++)
            {
                var piece = row.Pieces[i];
                var ui = panel.hp[i];
                bool down = piece.collider != null && piece.IsDown;
                foreach (var pip in ui.pips) pip.gameObject.SetActive(!down);
                ui.down.gameObject.SetActive(down);
                if (down)
                {
                    ui.down.text = string.Format(theme.downText, Mathf.Max(0f, piece.downUntil - Time.time));
                }
                else
                {
                    for (int k = 0; k < ui.pips.Count; k++)
                        ui.pips[k].color = k < piece.hp ? theme.PlayerColor(player) : theme.hpEmptyColor;
                }
            }
        }

        void BuildHp(PlayerPanel panel, BlockerRow row)
        {
            for (int i = panel.hpRow.childCount - 1; i >= 0; i--) Destroy(panel.hpRow.GetChild(i).gameObject);
            panel.hp.Clear();
            panel.builtFor = row;

            foreach (var piece in row.Pieces)
            {
                var ui = new PieceHp();
                ui.group = new GameObject("Blocker", typeof(RectTransform), typeof(HorizontalLayoutGroup));
                ui.group.transform.SetParent(panel.hpRow, false);
                var h = ui.group.GetComponent<HorizontalLayoutGroup>();
                h.spacing = 6f;
                h.childControlWidth = h.childControlHeight = true;
                h.childForceExpandWidth = h.childForceExpandHeight = false;
                h.childAlignment = TextAnchor.MiddleLeft;

                for (int k = 0; k < row.maxHp; k++)
                {
                    var pip = new GameObject("Pip", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                    pip.transform.SetParent(ui.group.transform, false);
                    var img = pip.GetComponent<Image>();
                    img.sprite = theme.circleSprite;
                    img.raycastTarget = false;
                    var le = pip.GetComponent<LayoutElement>();
                    le.preferredWidth = le.preferredHeight = 22f;
                    ui.pips.Add(img);
                }

                var dgo = new GameObject("Down", typeof(RectTransform), typeof(TextMeshProUGUI));
                dgo.transform.SetParent(ui.group.transform, false);
                ui.down = dgo.GetComponent<TextMeshProUGUI>();
                if (theme.font != null) ui.down.font = theme.font;
                ui.down.fontSize = theme.statusFontSize;
                ui.down.color = theme.warnColor;
                ui.down.raycastTarget = false;
                UIUtil.NoWrap(ui.down);
                dgo.SetActive(false);

                panel.hp.Add(ui);
            }
        }

        // ===================== 共通・中央 =====================

        void UpdateCommon(GameManager gm)
        {
            if (commonText == null) return;
            commonText.text = $"{string.Format(theme.firstToText, gm.targetScore)}\n{theme.guideText}\n{theme.backText}";
        }

        void UpdateBanner(GameManager gm)
        {
            if (banner == null) return;

            string main = "", sub = "";
            Color color = theme.textColor;

            if (gm.State == MatchState.MatchOver)
            {
                main = string.Format(theme.winBanner, Name(gm, gm.Winner));
                sub = theme.winSub;
                color = theme.PlayerColor(gm.Winner);
            }
            else
            {
                int bp = gm.BannerPlayer;
                switch (gm.CurrentBanner)
                {
                    case BannerKind.Serve: main = string.Format(theme.serveBanner, Name(gm, bp)); color = theme.PlayerColor(bp); break;
                    case BannerKind.Goal: main = string.Format(theme.goalBanner, Name(gm, bp)); color = theme.PlayerColor(bp); break;
                    case BannerKind.Fault: main = theme.faultBanner; sub = theme.faultSub; color = theme.warnColor; break;
                    case BannerKind.DoubleFault: main = theme.doubleFaultBanner; color = theme.warnColor; break;
                    case BannerKind.Let: main = theme.letBanner; break;
                }
            }

            banner.gameObject.SetActive(main != "");
            banner.text = main;
            banner.color = color;
            if (bannerSub != null)
            {
                bannerSub.gameObject.SetActive(sub != "");
                bannerSub.text = sub;
            }
        }
    }
}
