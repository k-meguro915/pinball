using System.Collections.Generic;
using Pinball;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PinballUI
{
    /// <summary>
    /// 盤面の上に出す表示（台の床に寝かせた World Space の Canvas）。
    ///   サーブ中     ：プランジャーのレーンにパワーゲージ（届く目安の線つき）、選んだルートの入口に矢印
    ///   ブロッカー   ：HP の点、ダウン中は戻るまでの時間の円、跳ね返しが効いている間は光る輪
    ///   キャッチ     ：ボタンを押している間はブロッカーの周りが脈打つ（つかめる状態）、つかんだ瞬間に輪が広がる、
    ///                  つかんでいる間は球の周りに光る輪と回る点、飛ぶ向きの矢印と自動発射までの残り時間
    ///   サイドレール ：入口に流れる矢印と「近道 → 相手陣地」、乗っている間は出口が点滅して「近道から来る！」
    ///   パワーショット：ゲージが溜まった場所に「+1」、満タンの間は自分のフリッパーが光る、
    ///                  発動中は球の周りが光り、発動した瞬間に「パワーショット！」
    ///                  何もせずに受けたフリッパーは「押し負け！」と、使えない間の赤い光（残り時間で欠けていく）、打ち返すと「カウンター！」
    ///   ロールオーバー：全部そろえると「ロールオーバー達成！」（ゲージの「+」は通過のたびに出る）
    ///   アウトレーン ：開いている間はレーン全体が攻める側の色で光り、ゴールへ流れる矢印・閉じるまでの円・「アウトレーン開放！」、
    ///                  開いた瞬間は入口から輪が広がる
    /// 必要な物（プランジャー・ブロッカーなど）は実行時にシーンから探すので、台を作り直しても動く。
    /// 色・文言・大きさは PinballHudTheme。
    /// </summary>
    public class BoardIndicators : MonoBehaviour
    {
        public PinballHudTheme theme;

        [Tooltip("床からの高さ（床の模様より少し上）")]
        public float height = 0.03f;

        [Header("大きさ（ワールドの長さ）")]
        public float gaugeLength = 3.0f;
        public float gaugeWidth = 0.35f;
        public float pipSize = 0.16f;
        public float pipSpacing = 0.22f;
        public float pipOffset = 0.42f;
        public float aimLength = 2.0f;

        const float PPU = 100f; // 1 ワールド単位 = Canvas の 100

        RectTransform canvasRoot;

        class Gauge { public GameObject root; public Image fill; public RectTransform tick; public TextMeshProUGUI label; }
        class Arrow { public RectTransform root; public List<Image> parts = new List<Image>(); }
        class RowUi
        {
            public BlockerRow row;
            public List<List<Image>> pips = new List<List<Image>>();
            public List<Image> downTimers = new List<Image>();
            public List<Image> rings = new List<Image>();
            public Arrow aim;
            public Image holdBg, holdFill;
            public List<Image> armedRings = new List<Image>();   // つかめる状態（ボタン押下中）
            public Image aura;                                   // つかんでいる球の周りの光
            public List<Image> orbit = new List<Image>();        // 球の周りを回る点
            public Image burst;                                  // つかんだ瞬間に広がる輪
        }

        readonly Gauge[] gauges = new Gauge[2];
        readonly Arrow[] routeArrows = new Arrow[2];
        readonly TextMeshProUGUI[] routeLabels = new TextMeshProUGUI[2];
        readonly List<RowUi> rowUis = new List<RowUi>();

        class RailUi
        {
            public SideRail rail;
            public List<Arrow> chevrons = new List<Arrow>();
            public TextMeshProUGUI entranceLabel;
            public Image exitGlow;
            public TextMeshProUGUI exitLabel;
        }
        readonly List<RailUi> railUis = new List<RailUi>();
        float nextSearch;

        // ---- パワーショット ----
        class Popup { public TextMeshProUGUI text; public Vector3 pos; public float start = -10f; }
        readonly List<Popup> popups = new List<Popup>();
        int nextPopup;
        class FlipperGlow { public Flipper flipper; public Renderer rend; public Image glow; public Image stun; }
        readonly List<FlipperGlow> flipperGlows = new List<FlipperGlow>();
        class OutlaneUi
        {
            public Outlane lane;
            public Image glow;
            public Image burst;
            public Image timer;
            public TextMeshProUGUI label;
            public List<Arrow> chevrons = new List<Arrow>();
        }
        readonly List<OutlaneUi> outlaneUis = new List<OutlaneUi>();

        Image powerAura;
        TextMeshProUGUI powerLabel;
        const float PopupTime = 0.7f;

        // 台の上の文字は、1 VS 1 の画面分けの P2 の画面には映さない（逆さまになるため。SplitScreen）
        P1OnlyText textLayer;

        void Start()
        {
            BuildCanvas();
            textLayer = new P1OnlyText(canvasRoot);
        }

        void OnEnable()
        {
            PowerShot.Charged += OnPowerCharged;
            PowerShot.FlipperBroken += OnFlipperBroken;
            PowerShot.Countered += OnCountered;
            BlockerRow.BrokenByPower += OnBlockerBroken;
            Rollover.Completed += OnRolloverCompleted;
        }

        void OnDisable()
        {
            PowerShot.Charged -= OnPowerCharged;
            PowerShot.FlipperBroken -= OnFlipperBroken;
            PowerShot.Countered -= OnCountered;
            BlockerRow.BrokenByPower -= OnBlockerBroken;
            Rollover.Completed -= OnRolloverCompleted;
        }

        void OnRolloverCompleted(int field, int player, Vector3 pos)
        {
            if (popups.Count == 0) return;
            var pp = popups[nextPopup];
            nextPopup = (nextPopup + 1) % popups.Count;
            // 盤面の中央側へ寄せて出す（「+2」と重ならないように）
            pp.pos = pos + (pos.z < 0f ? Vector3.forward : Vector3.back) * 0.8f + new Vector3(-Mathf.Sign(pos.x) * 1.2f, 0f, 0f);
            pp.start = Time.time;
            pp.text.text = theme.rolloverCompleteText;
            pp.text.color = theme.PlayerColor(player);
        }

        void OnBlockerBroken(BlockerRow row, Vector3 pos)
        {
            if (popups.Count == 0) return;
            var pp = popups[nextPopup];
            nextPopup = (nextPopup + 1) % popups.Count;
            pp.pos = pos;
            pp.start = Time.time;
            pp.text.text = theme.blockerBreakText;
            pp.text.color = theme.stunColor;
        }

        void OnFlipperBroken(Flipper f) => ShowWord(f, theme.flipperBrokenText, theme.stunColor);
        void OnCountered(Flipper f) => ShowWord(f, theme.counterText, theme.PlayerColor(f.playerIndex));

        void ShowWord(Flipper f, string text, Color color)
        {
            if (popups.Count == 0 || f == null) return;
            var pp = popups[nextPopup];
            nextPopup = (nextPopup + 1) % popups.Count;
            var r = f.GetComponentInChildren<Renderer>();
            Vector3 pos = r != null ? r.bounds.center : f.transform.position;
            // 盤面の中央側へ少し寄せる（ゴールの外に文字が出ないように）
            pp.pos = pos + (pos.z < 0f ? Vector3.forward : Vector3.back) * 0.6f;
            pp.start = Time.time;
            pp.text.text = text;
            pp.text.color = color;
        }

        void OnPowerCharged(int player, float amount, Vector3 pos)
        {
            if (popups.Count == 0) return;
            var pp = popups[nextPopup];
            nextPopup = (nextPopup + 1) % popups.Count;
            pp.pos = pos;
            pp.start = Time.time;
            pp.text.text = string.Format(theme.powerChargeText, amount);
            pp.text.color = theme.PlayerColor(player);
        }

        // ===================== 組み立て =====================

        GameObject canvasObject;

        void OnDestroy()
        {
            if (canvasObject != null) Destroy(canvasObject);
            textLayer?.Destroy();
        }

        void BuildCanvas()
        {
            // 画面の HUD（Screen Space Overlay）の子にすると表示が混ざるので、独立したオブジェクトとして作る
            var go = new GameObject("PinballBoardCanvas", typeof(RectTransform));
            canvasObject = go;
            go.transform.position = new Vector3(0f, height, 0f);
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // 床に寝かせる（Canvas の上 = ワールドの +Z）
            go.transform.localScale = Vector3.one / PPU;

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvasRoot = (RectTransform)go.transform;
            canvasRoot.sizeDelta = new Vector2(20f * PPU, 30f * PPU);

            for (int p = 0; p < 2; p++)
            {
                gauges[p] = BuildGauge(p);
                routeArrows[p] = BuildArrow($"RouteArrow_{p}", theme.routeColor, 1.2f, 0.12f);
                routeLabels[p] = UIUtil.NewText($"RouteLabel_{p}", canvasRoot, theme.routeHere, theme.boardFontSize,
                    theme.routeColor, TextAlignmentOptions.Center, theme.font);
                routeLabels[p].rectTransform.sizeDelta = new Vector2(300f, 50f);
                routeArrows[p].root.gameObject.SetActive(false);
                routeLabels[p].gameObject.SetActive(false);
            }

            // パワーショット
            powerAura = UIUtil.NewImage("PowerAura", canvasRoot, Circle, Color.white);
            powerAura.rectTransform.sizeDelta = Vector2.one * 1.0f * PPU;
            powerAura.gameObject.SetActive(false);
            powerLabel = UIUtil.NewText("PowerShotLabel", canvasRoot, theme.powerShotText, theme.boardFontSize * 1.3f,
                theme.powerReadyColor, TextAlignmentOptions.Center, theme.font);
            powerLabel.fontStyle = FontStyles.Bold;
            powerLabel.rectTransform.sizeDelta = new Vector2(500f, 60f);
            powerLabel.gameObject.SetActive(false);
            for (int i = 0; i < 8; i++)
            {
                var t = UIUtil.NewText("PowerPopup", canvasRoot, "", theme.boardFontSize, Color.white, TextAlignmentOptions.Center, theme.font);
                t.fontStyle = FontStyles.Bold;
                t.rectTransform.sizeDelta = new Vector2(200f, 50f);
                t.gameObject.SetActive(false);
                popups.Add(new Popup { text = t });
            }
        }

        Gauge BuildGauge(int p)
        {
            var g = new Gauge();
            var root = UIUtil.NewRect($"Gauge_{p}", canvasRoot);
            root.pivot = new Vector2(0.5f, 0f);
            root.sizeDelta = new Vector2(gaugeWidth * PPU, gaugeLength * PPU);
            g.root = root.gameObject;

            var bg = UIUtil.NewImage("Bg", root, null, new Color(0f, 0f, 0f, 0.5f));
            Stretch(bg.rectTransform);

            g.fill = UIUtil.NewImage("Fill", root, UIUtil.WhiteSprite, theme.gaugeOkColor);
            Stretch(g.fill.rectTransform, 4f);
            g.fill.type = Image.Type.Filled;
            g.fill.fillMethod = Image.FillMethod.Vertical;
            g.fill.fillOrigin = (int)Image.OriginVertical.Bottom;

            var tick = UIUtil.NewImage("Tick", root, null, Color.white);
            g.tick = tick.rectTransform;
            g.tick.anchorMin = new Vector2(0f, 0f);
            g.tick.anchorMax = new Vector2(1f, 0f);
            g.tick.pivot = new Vector2(0.5f, 0.5f);
            g.tick.sizeDelta = new Vector2(16f, 5f);

            g.label = UIUtil.NewText("Label", root, theme.gaugeLabel, theme.boardFontSize * 0.8f, theme.textColor,
                TextAlignmentOptions.Center, theme.font);
            g.label.rectTransform.anchorMin = g.label.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            g.label.rectTransform.pivot = new Vector2(0.5f, 0f);
            g.label.rectTransform.anchoredPosition = new Vector2(0f, 6f);
            g.label.rectTransform.sizeDelta = new Vector2(200f, 40f);

            g.root.SetActive(false);
            return g;
        }

        /// <summary>矢印（軸＋2本の羽根）。root の右（+X）向き、長さ length。</summary>
        Arrow BuildArrow(string name, Color color, float length, float thickness)
        {
            var a = new Arrow();
            a.root = UIUtil.NewRect(name, canvasRoot);
            a.root.sizeDelta = Vector2.zero;

            var shaft = UIUtil.NewImage("Shaft", a.root, null, color);
            shaft.rectTransform.pivot = new Vector2(0f, 0.5f);
            shaft.rectTransform.sizeDelta = new Vector2(length * PPU, thickness * PPU);
            a.parts.Add(shaft);

            for (int s = -1; s <= 1; s += 2)
            {
                var wing = UIUtil.NewImage("Wing", a.root, null, color);
                wing.rectTransform.pivot = new Vector2(1f, 0.5f);
                wing.rectTransform.anchoredPosition = new Vector2(length * PPU, 0f);
                wing.rectTransform.sizeDelta = new Vector2(0.4f * PPU, thickness * PPU);
                wing.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 35f * s);
                a.parts.Add(wing);
            }
            return a;
        }

        RowUi BuildRow(BlockerRow row)
        {
            var ui = new RowUi { row = row };
            Color pc = theme.PlayerColor(row.playerIndex);

            foreach (var piece in row.Pieces)
            {
                var list = new List<Image>();
                for (int k = 0; k < row.maxHp; k++)
                {
                    var pip = UIUtil.NewImage("Pip", canvasRoot, Circle, pc);
                    pip.rectTransform.sizeDelta = Vector2.one * pipSize * PPU;
                    list.Add(pip);
                }
                ui.pips.Add(list);

                var timer = UIUtil.NewImage("DownTimer", canvasRoot, Circle, new Color(pc.r, pc.g, pc.b, 0.6f));
                timer.rectTransform.sizeDelta = Vector2.one * 0.7f * PPU;
                timer.type = Image.Type.Filled;
                timer.fillMethod = Image.FillMethod.Radial360;
                timer.fillOrigin = (int)Image.Origin360.Top;
                timer.gameObject.SetActive(false);
                ui.downTimers.Add(timer);

                var ring = UIUtil.NewImage("BounceRing", canvasRoot, Circle, new Color(1f, 1f, 1f, 0.35f));
                ring.rectTransform.sizeDelta = new Vector2((row.blockerLength + 0.5f) * PPU, 0.9f * PPU);
                ring.gameObject.SetActive(false);
                ui.rings.Add(ring);
            }

            // ---- キャッチのエフェクト ----
            foreach (var piece in row.Pieces)
            {
                var armed = UIUtil.NewImage("CatchArmed", canvasRoot, Circle, new Color(pc.r, pc.g, pc.b, 0.3f));
                armed.rectTransform.sizeDelta = new Vector2((row.blockerLength + 0.7f) * PPU, 1.1f * PPU);
                armed.gameObject.SetActive(false);
                ui.armedRings.Add(armed);
            }
            ui.burst = UIUtil.NewImage("CatchBurst", canvasRoot, Circle, pc);
            ui.burst.rectTransform.sizeDelta = Vector2.one * 0.6f * PPU;
            ui.burst.gameObject.SetActive(false);
            ui.aura = UIUtil.NewImage("CatchAura", canvasRoot, Circle, pc);
            ui.aura.rectTransform.sizeDelta = Vector2.one * 0.95f * PPU;
            ui.aura.gameObject.SetActive(false);
            for (int k = 0; k < 3; k++)
            {
                var dot = UIUtil.NewImage("CatchOrbit", canvasRoot, Circle, Color.white);
                dot.rectTransform.sizeDelta = Vector2.one * 0.12f * PPU;
                dot.gameObject.SetActive(false);
                ui.orbit.Add(dot);
            }

            ui.aim = BuildArrow($"Aim_{row.playerIndex}", pc, aimLength, 0.08f);
            ui.aim.root.gameObject.SetActive(false);

            ui.holdBg = UIUtil.NewImage("HoldBg", canvasRoot, null, new Color(0f, 0f, 0f, 0.55f));
            ui.holdBg.rectTransform.sizeDelta = new Vector2(0.9f * PPU, 0.12f * PPU);
            ui.holdFill = UIUtil.NewImage("HoldFill", ui.holdBg.rectTransform, UIUtil.WhiteSprite, pc);
            Stretch(ui.holdFill.rectTransform, 2f);
            ui.holdFill.type = Image.Type.Filled;
            ui.holdFill.fillMethod = Image.FillMethod.Horizontal;
            ui.holdBg.gameObject.SetActive(false);

            return ui;
        }

        Sprite Circle => theme.circleSprite != null ? theme.circleSprite : UIUtil.WhiteSprite;

        static void Stretch(RectTransform rt, float inset = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
        }

        static Vector2 ToCanvas(Vector3 world) => new Vector2(world.x * PPU, world.z * PPU);

        static float AngleOf(Vector3 dir) => Mathf.Atan2(dir.z, dir.x) * Mathf.Rad2Deg;

        // ===================== 更新 =====================

        void LateUpdate()
        {
            var gm = GameManager.Instance;
            if (gm == null || theme == null || canvasRoot == null) return;

            FindRows();
            for (int p = 0; p < 2; p++) UpdateServe(gm, p);
            foreach (var ui in rowUis) UpdateRow(gm, ui);
            foreach (var ui in railUis) UpdateRail(gm, ui);
            UpdatePower(gm);
            foreach (var ui in outlaneUis) UpdateOutlane(gm, ui);
            textLayer?.Sync();
        }

        // ===================== アウトレーン =====================

        OutlaneUi BuildOutlane(Outlane lane)
        {
            var ui = new OutlaneUi { lane = lane };
            Color ac = theme.PlayerColor(1 - lane.ownerPlayer); // 攻める側の色

            float len = Mathf.Abs(lane.entrance.z - lane.laneEnd.z);
            ui.glow = UIUtil.NewImage($"OutlaneGlow_{lane.name}", canvasRoot, UIUtil.WhiteSprite, ac);
            ui.glow.rectTransform.sizeDelta = new Vector2(lane.laneWidth * PPU, len * PPU);
            ui.glow.rectTransform.anchoredPosition = ToCanvas((lane.entrance + lane.laneEnd) / 2f);
            ui.glow.transform.SetAsFirstSibling();

            for (int i = 0; i < 4; i++)
            {
                var a = BuildArrow($"OutlaneChevron_{i}", ac, 0.4f, 0.09f);
                ui.chevrons.Add(a);
            }

            ui.burst = UIUtil.NewImage("OutlaneBurst", canvasRoot, Circle, ac);
            ui.burst.rectTransform.sizeDelta = Vector2.one * 0.8f * PPU;

            ui.timer = UIUtil.NewImage("OutlaneTimer", canvasRoot, Circle, ac);
            ui.timer.rectTransform.sizeDelta = Vector2.one * 0.6f * PPU;
            ui.timer.type = Image.Type.Filled;
            ui.timer.fillMethod = Image.FillMethod.Radial360;

            ui.label = UIUtil.NewText($"OutlaneLabel_{lane.name}", canvasRoot, theme.outlaneOpenText, theme.boardFontSize,
                ac, TextAlignmentOptions.Center, theme.font);
            ui.label.fontStyle = FontStyles.Bold;
            ui.label.rectTransform.sizeDelta = new Vector2(400f, 50f);
            return ui;
        }

        void UpdateOutlane(GameManager gm, OutlaneUi ui)
        {
            var lane = ui.lane;
            bool open = lane != null && lane.isActiveAndEnabled && lane.IsOpen && lane.openOnBlockerDown
                        && gm.State != MatchState.Title;
            ui.glow.gameObject.SetActive(open);
            ui.timer.gameObject.SetActive(open);
            ui.label.gameObject.SetActive(open);
            foreach (var a in ui.chevrons) a.root.gameObject.SetActive(open);

            // 開いた瞬間：入口から輪が広がる（0.5 秒）
            float since = lane != null ? Time.time - lane.OpenedTime : 99f;
            bool burstOn = lane != null && since >= 0f && since < 0.5f && gm.State != MatchState.Title;
            ui.burst.gameObject.SetActive(burstOn);
            Color ac = theme.PlayerColor(lane != null ? 1 - lane.ownerPlayer : 0);
            if (burstOn)
            {
                float t = since / 0.5f;
                ui.burst.rectTransform.anchoredPosition = ToCanvas(lane.entrance);
                ui.burst.rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, 4f, t);
                var bc = ac; bc.a = 0.7f * (1f - t); ui.burst.color = bc;
            }
            if (!open) return;

            // レーン全体がゆっくり脈打つ
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 6f);
            var gc = ac; gc.a = 0.15f + 0.2f * pulse; ui.glow.color = gc;

            // ゴールへ流れる矢印
            Vector3 dir = (lane.laneEnd - lane.entrance); dir.y = 0f;
            float len = dir.magnitude;
            dir = len > 0.01f ? dir / len : Vector3.back;
            for (int i = 0; i < ui.chevrons.Count; i++)
            {
                var a = ui.chevrons[i];
                float phase = Mathf.Repeat(Time.time * 0.8f + i / (float)ui.chevrons.Count, 1f);
                Vector3 p = lane.entrance + dir * (phase * Mathf.Min(len, 5f));
                a.root.anchoredPosition = ToCanvas(p - dir * 0.2f);
                a.root.localRotation = Quaternion.Euler(0f, 0f, AngleOf(dir));
                float alpha = 0.9f * Mathf.Sin(phase * Mathf.PI);
                foreach (var part in a.parts) { var c = ac; c.a = alpha; part.color = c; }
            }

            // 閉じるまでの円（入口）
            ui.timer.rectTransform.anchoredPosition = ToCanvas(lane.entrance);
            ui.timer.fillAmount = lane.Remaining01;
            var tc = ac; tc.a = 0.8f; ui.timer.color = tc;

            // 文字：入口の盤面側（内側）、正立
            Vector3 inward = new Vector3(-Mathf.Sign(lane.entrance.x), 0f, 0f);
            Vector3 toward = new Vector3(0f, 0f, -Mathf.Sign(lane.entrance.z));
            ui.label.rectTransform.anchoredPosition = ToCanvas(lane.entrance + inward * 2.0f + toward * 0.6f);
            ui.label.rectTransform.localRotation = Quaternion.identity;
            ui.label.text = theme.outlaneOpenText;
            var lc = ac; lc.a = 0.75f + 0.25f * pulse; ui.label.color = lc;
            ui.label.rectTransform.localScale = Vector3.one * (since < 0.3f ? Mathf.Lerp(1.4f, 1f, since / 0.3f) : 1f);
        }

        // ===================== パワーショット =====================

        void UpdatePower(GameManager gm)
        {
            var ps = PowerShot.Instance;
            bool on = ps != null && ps.isActiveAndEnabled && gm.State != MatchState.Title;

            // 満タン：自分のフリッパーが脈打つ
            foreach (var fg in flipperGlows)
            {
                // 押し負けて動けない間：赤く光る
                bool stunned = on && fg.flipper != null && fg.flipper.IsStunned;
                fg.stun.gameObject.SetActive(stunned);
                if (stunned)
                {
                    Vector3 sc = fg.rend != null ? fg.rend.bounds.center : fg.flipper.transform.position;
                    fg.stun.rectTransform.anchoredPosition = ToCanvas(sc);
                    float blink = 0.5f + 0.5f * Mathf.Sin(Time.time * 18f);
                    var scol = theme.stunColor; scol.a = 0.3f + 0.3f * blink; fg.stun.color = scol;
                    fg.stun.fillAmount = fg.flipper.StunRemaining01; // 残り時間で欠けていく
                }

                bool ready = on && !stunned && fg.flipper != null && fg.flipper.isActiveAndEnabled && ps.IsReady(fg.flipper.playerIndex) && !ps.IsActive;
                fg.glow.gameObject.SetActive(ready);
                if (!ready) continue;
                Vector3 c = fg.rend != null ? fg.rend.bounds.center : fg.flipper.transform.position;
                fg.glow.rectTransform.anchoredPosition = ToCanvas(c);
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 8f);
                fg.glow.rectTransform.localScale = Vector3.one * (1f + 0.1f * pulse);
                var col = theme.powerReadyColor; col.a = 0.2f + 0.25f * pulse; fg.glow.color = col;
            }

            // 発動中：球の周りが持ち主の色で光る
            var ball = gm.ball;
            bool active = on && ps.IsActive && ball != null && ball.isActiveAndEnabled;
            powerAura.gameObject.SetActive(active);
            if (active)
            {
                float glow = 0.5f + 0.5f * Mathf.Sin(Time.time * 20f);
                powerAura.rectTransform.anchoredPosition = ToCanvas(ball.transform.position);
                powerAura.rectTransform.localScale = Vector3.one * (1f + 0.2f * glow);
                var ac = theme.PlayerColor(ps.ActiveOwner); ac.a = 0.35f + 0.3f * glow; powerAura.color = ac;
            }

            // 発動した瞬間：「パワーショット！」（0.8 秒、発動した場所から少し浮き上がる）
            float since = on ? Time.time - ps.FiredTime : 99f;
            bool labelOn = since >= 0f && since < 0.8f && ball != null;
            powerLabel.gameObject.SetActive(labelOn);
            if (labelOn)
            {
                int owner = Mathf.Max(0, ps.ActiveOwner);
                Vector3 fwd = owner == 0 ? Vector3.forward : Vector3.back;
                powerLabel.text = theme.powerShotText;
                powerLabel.rectTransform.anchoredPosition = ToCanvas(ball.transform.position - fwd * (0.8f + since));
                var c = theme.powerReadyColor; c.a = 1f - since / 0.8f; powerLabel.color = c;
                powerLabel.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.3f, 1f, since / 0.8f);
            }

            // 「+1」：その場所から少し浮いて消える
            foreach (var pp in popups)
            {
                float t = (Time.time - pp.start) / PopupTime;
                bool vis = on && t >= 0f && t < 1f;
                pp.text.gameObject.SetActive(vis);
                if (!vis) continue;
                pp.text.rectTransform.anchoredPosition = ToCanvas(pp.pos) + new Vector2(0f, 0.3f * PPU + t * 0.5f * PPU);
                var c = pp.text.color; c.a = 1f - t; pp.text.color = c;
            }
        }

        void FindRows()
        {
            if (Time.unscaledTime < nextSearch) return;
            nextSearch = Time.unscaledTime + 1f;
#if UNITY_2023_1_OR_NEWER
            var rows = FindObjectsByType<BlockerRow>(FindObjectsSortMode.None);
#else
            var rows = FindObjectsOfType<BlockerRow>();
#endif
            foreach (var r in rows)
            {
                bool known = false;
                foreach (var ui in rowUis) if (ui.row == r) known = true;
                if (!known && r.Pieces.Length > 0) rowUis.Add(BuildRow(r));
            }

#if UNITY_2023_1_OR_NEWER
            var rails = FindObjectsByType<SideRail>(FindObjectsSortMode.None);
#else
            var rails = FindObjectsOfType<SideRail>();
#endif
            foreach (var r in rails)
            {
                bool known = false;
                foreach (var ui in railUis) if (ui.rail == r) known = true;
                if (!known && r.path != null && r.path.Length >= 2) railUis.Add(BuildRail(r));
            }

#if UNITY_2023_1_OR_NEWER
            var lanes = FindObjectsByType<Outlane>(FindObjectsSortMode.None);
#else
            var lanes = FindObjectsOfType<Outlane>();
#endif
            foreach (var l in lanes)
            {
                bool known = false;
                foreach (var ui in outlaneUis) if (ui.lane == l) known = true;
                if (!known) outlaneUis.Add(BuildOutlane(l));
            }

#if UNITY_2023_1_OR_NEWER
            var flippers = FindObjectsByType<Flipper>(FindObjectsSortMode.None);
#else
            var flippers = FindObjectsOfType<Flipper>();
#endif
            foreach (var f in flippers)
            {
                bool known = false;
                foreach (var fg in flipperGlows) if (fg.flipper == f) known = true;
                if (known) continue;
                var img = UIUtil.NewImage($"PowerReady_{f.playerIndex}", canvasRoot, Circle, theme.powerReadyColor);
                img.rectTransform.sizeDelta = Vector2.one * (f.length + 0.5f) * PPU;
                img.transform.SetAsFirstSibling(); // フリッパーの下に敷く感じで、他の表示より奥
                img.gameObject.SetActive(false);
                var stun = UIUtil.NewImage($"Stunned_{f.playerIndex}", canvasRoot, Circle, theme.stunColor);
                stun.rectTransform.sizeDelta = Vector2.one * (f.length + 0.3f) * PPU;
                stun.type = Image.Type.Filled;
                stun.fillMethod = Image.FillMethod.Radial360;
                stun.transform.SetAsFirstSibling();
                stun.gameObject.SetActive(false);
                flipperGlows.Add(new FlipperGlow { flipper = f, rend = f.GetComponentInChildren<Renderer>(), glow = img, stun = stun });
            }
        }

        // ===================== サイドレール =====================

        RailUi BuildRail(SideRail rail)
        {
            var ui = new RailUi { rail = rail };
            Color pc = theme.PlayerColor(rail.ownerPlayer);

            // 入口：入る向きに流れる 3 つの矢印
            for (int i = 0; i < 3; i++)
            {
                var a = BuildArrow($"RailChevron_{rail.ownerPlayer}_{i}", pc, 0.35f, 0.09f);
                ui.chevrons.Add(a);
            }
            ui.entranceLabel = UIUtil.NewText($"RailEntranceLabel_{rail.ownerPlayer}", canvasRoot, theme.railEntranceText,
                theme.boardFontSize * 0.85f, pc, TextAlignmentOptions.Center, theme.font);
            ui.entranceLabel.rectTransform.sizeDelta = new Vector2(400f, 50f);

            // 出口：点滅する円と知らせ
            ui.exitGlow = UIUtil.NewImage($"RailExitGlow_{rail.ownerPlayer}", canvasRoot, Circle, pc);
            ui.exitGlow.rectTransform.sizeDelta = Vector2.one * 1.2f * PPU;
            ui.exitLabel = UIUtil.NewText($"RailExitLabel_{rail.ownerPlayer}", canvasRoot, theme.railIncomingText,
                theme.boardFontSize, pc, TextAlignmentOptions.Center, theme.font);
            ui.exitLabel.fontStyle = FontStyles.Bold;
            ui.exitLabel.rectTransform.sizeDelta = new Vector2(400f, 60f);
            return ui;
        }

        void UpdateRail(GameManager gm, RailUi ui)
        {
            var rail = ui.rail;
            bool show = rail != null && rail.isActiveAndEnabled && gm.State != MatchState.Title;
            Color pc = theme.PlayerColor(rail != null ? rail.ownerPlayer : 0);

            Vector3 entrance = show ? rail.EntrancePoint : Vector3.zero;
            Vector3 dir = show ? rail.entryDirection.normalized : Vector3.forward;
            Vector2 size = show ? rail.EntranceSize : Vector2.one;
            // 持ち主が最後に打った球のときだけ明るく（乗れない球のときは薄く）
            bool active = show && gm.State == MatchState.Playing && !rail.IsRiding && rail.CanRide(gm.ball);

            // 入口の矢印：入口の手前から奥へ流れる（使えないときは薄く）
            for (int i = 0; i < ui.chevrons.Count; i++)
            {
                var a = ui.chevrons[i];
                a.root.gameObject.SetActive(show);
                if (!show) continue;
                float phase = Mathf.Repeat(Time.time * 1.2f + i / (float)ui.chevrons.Count, 1f);
                Vector3 p = entrance + dir * Mathf.Lerp(-size.y * 1.6f, size.y * 0.4f, phase);
                a.root.anchoredPosition = ToCanvas(p - dir * 0.35f);
                a.root.localRotation = Quaternion.Euler(0f, 0f, AngleOf(dir));
                float alpha = (active ? 0.9f : 0.3f) * Mathf.Sin(phase * Mathf.PI);
                foreach (var part in a.parts) { var c = pc; c.a = alpha; part.color = c; }
            }

            ui.entranceLabel.gameObject.SetActive(show);
            if (show)
            {
                // 文字は入口の盤面側（内側）に
                Vector3 inward = new Vector3(-Mathf.Sign(entrance.x), 0f, 0f);
                ui.entranceLabel.rectTransform.anchoredPosition = ToCanvas(entrance + inward * (size.x * 0.5f + 1.3f));
                ui.entranceLabel.rectTransform.localRotation = Quaternion.identity; // 画面は2人で共有なので文字は常に正立
                var lc = pc; lc.a = active ? 0.9f : 0.35f; ui.entranceLabel.color = lc;
                ui.entranceLabel.text = theme.railEntranceText;
            }

            // 出口：乗っている間だけ強く点滅し、知らせを出す（相手にも分かるように）
            bool riding = show && rail.IsRiding;
            ui.exitGlow.gameObject.SetActive(show);
            ui.exitLabel.gameObject.SetActive(riding);
            if (show)
            {
                Vector3 exit = rail.ExitPoint;
                ui.exitGlow.rectTransform.anchoredPosition = ToCanvas(exit);
                float pulse = riding ? 0.35f + 0.35f * Mathf.Abs(Mathf.Sin(Time.time * 12f)) : 0.12f;
                var gc = pc; gc.a = pulse; ui.exitGlow.color = gc;
                float scale = riding ? 1f + 0.3f * rail.RideProgress : 1f;
                ui.exitGlow.rectTransform.localScale = Vector3.one * scale;
            }
            if (riding)
            {
                Vector3 exit = rail.ExitPoint;
                Vector3 inward = new Vector3(-Mathf.Sign(exit.x), 0f, 0f);
                ui.exitLabel.rectTransform.anchoredPosition = ToCanvas(exit + inward * 1.6f);
                ui.exitLabel.rectTransform.localRotation = Quaternion.identity;
                ui.exitLabel.text = theme.railIncomingText;
            }
        }

        void UpdateServe(GameManager gm, int p)
        {
            bool show = gm.State == MatchState.Serve && gm.Server == p &&
                        gm.plungers != null && p < gm.plungers.Length && gm.plungers[p] != null;

            // ---- パワーゲージ ----
            var g = gauges[p];
            g.root.SetActive(show);
            if (show)
            {
                var plunger = gm.plungers[p];
                var rt = (RectTransform)g.root.transform;
                rt.anchoredPosition = ToCanvas(plunger.transform.position + plunger.transform.forward * 0.5f);
                rt.localRotation = Quaternion.Euler(0f, 0f, p == 0 ? 0f : 180f);
                g.label.rectTransform.localRotation = Quaternion.Euler(0f, 0f, p == 0 ? 0f : 180f);

                float need = gm.CurrentRoute == ServeRoute.OwnField ? theme.ownRouteNeedSpeed : theme.oppRouteNeedSpeed;
                float t = Mathf.InverseLerp(plunger.minLaunchSpeed, plunger.maxLaunchSpeed, need);
                g.tick.anchoredPosition = new Vector2(0f, t * gaugeLength * PPU);

                g.fill.fillAmount = plunger.Charge;
                g.fill.color = plunger.Charge >= t ? theme.gaugeOkColor : theme.gaugeLowColor;
            }

            // ---- ルートの入口の矢印 ----
            bool showRoute = show && gm.diverters != null && p < gm.diverters.Length && gm.diverters[p] != null;
            var arrow = routeArrows[p];
            var label = routeLabels[p];
            arrow.root.gameObject.SetActive(showRoute);
            label.gameObject.SetActive(showRoute);
            if (!showRoute) return;

            var d = gm.diverters[p];
            var gate = gm.CurrentRoute == ServeRoute.OwnField ? d.ownExitGate : d.opponentExitGate;
            if (gate == null) return;

            Vector3 gp = gate.transform.position;
            Vector3 inward = new Vector3(-Mathf.Sign(gp.x), 0f, 0f);
            Vector3 start = new Vector3(gp.x, 0f, gp.z) + inward * 0.35f;
            arrow.root.anchoredPosition = ToCanvas(start);
            arrow.root.localRotation = Quaternion.Euler(0f, 0f, AngleOf(inward));

            // 点滅させて目立たせる（打った後は薄く）
            float a = gm.ServeLaunched ? 0.35f : 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.time * 5f));
            foreach (var part in arrow.parts) { var c = theme.routeColor; c.a = a; part.color = c; }

            label.rectTransform.anchoredPosition = ToCanvas(start + inward * 2.2f);
            label.text = theme.routeHere;
            var lc = theme.routeColor; lc.a = a; label.color = lc;
        }

        void UpdateRow(GameManager gm, RowUi ui)
        {
            var row = ui.row;
            bool alive = row != null && row.isActiveAndEnabled && gm.State != MatchState.Title;

            for (int i = 0; i < ui.pips.Count; i++)
            {
                var piece = alive && i < row.Pieces.Length ? row.Pieces[i] : null;
                bool down = piece != null && piece.collider != null && piece.IsDown;
                Vector3 bpos = piece != null && piece.transform != null ? piece.transform.position : Vector3.zero;

                // HP の点：ブロッカーの前（盤面の中央側）に並べる
                float toward = -Mathf.Sign(bpos.z == 0f ? 1f : bpos.z);
                var list = ui.pips[i];
                for (int k = 0; k < list.Count; k++)
                {
                    bool on = piece != null && !down;
                    list[k].gameObject.SetActive(on);
                    if (!on) continue;
                    float x = (k - (list.Count - 1) / 2f) * pipSpacing;
                    list[k].rectTransform.anchoredPosition = ToCanvas(bpos + new Vector3(x, 0f, toward * pipOffset));
                    list[k].color = k < piece.hp ? theme.PlayerColor(row.playerIndex) : theme.hpEmptyColor;
                }

                // ダウン中：戻るまでの時間の円
                var timer = ui.downTimers[i];
                timer.gameObject.SetActive(down);
                if (down)
                {
                    timer.rectTransform.anchoredPosition = ToCanvas(bpos);
                    timer.fillAmount = Mathf.Clamp01((piece.downUntil - Time.time) / Mathf.Max(0.01f, row.downTime));
                }

                // 跳ね返しが効いている間の輪
                var ring = ui.rings[i];
                bool ringOn = alive && piece != null && !down && row.IsBounceActive;
                ring.gameObject.SetActive(ringOn);
                if (ringOn) ring.rectTransform.anchoredPosition = ToCanvas(bpos);
            }

            // ---- キャッチ：つかめる状態（ボタン押下中）はブロッカーの周りが脈打つ ----
            Color pcol = theme.PlayerColor(row.playerIndex);
            bool armed = alive && row.IsCatchArmed;
            for (int i = 0; i < ui.armedRings.Count; i++)
            {
                var piece = i < row.Pieces.Length ? row.Pieces[i] : null;
                bool on = armed && piece != null && piece.collider != null && !piece.IsDown;
                var r = ui.armedRings[i];
                r.gameObject.SetActive(on);
                if (!on) continue;
                r.rectTransform.anchoredPosition = ToCanvas(piece.transform.position);
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 10f);
                r.rectTransform.localScale = Vector3.one * (1f + 0.12f * pulse);
                var c = pcol; c.a = 0.18f + 0.2f * pulse; r.color = c;
            }

            // ---- つかんだ瞬間：輪が広がって消える（0.35 秒）----
            float since = Time.time - row.CatchStartTime;
            bool burstOn = alive && since >= 0f && since < 0.35f && row.ball != null;
            ui.burst.gameObject.SetActive(burstOn);
            if (burstOn)
            {
                float t = since / 0.35f;
                ui.burst.rectTransform.anchoredPosition = ToCanvas(row.ball.transform.position);
                ui.burst.rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, 3.2f, t);
                var c = pcol; c.a = 0.6f * (1f - t); ui.burst.color = c;
            }

            // キャッチ中：狙いの矢印と残り時間
            bool holding = alive && row.IsHolding && row.ball != null;

            // ---- つかんでいる間：球の周りの光と、回る点 ----
            ui.aura.gameObject.SetActive(holding);
            foreach (var d in ui.orbit) d.gameObject.SetActive(holding);
            if (holding)
            {
                Vector3 bpos = row.ball.transform.position;
                float glow = 0.5f + 0.5f * Mathf.Sin(Time.time * 14f);
                ui.aura.rectTransform.anchoredPosition = ToCanvas(bpos);
                ui.aura.rectTransform.localScale = Vector3.one * (1f + 0.15f * glow);
                var ac = pcol; ac.a = 0.25f + 0.25f * glow; ui.aura.color = ac;

                // 残り時間が少ないほど速く回る
                float speed = Mathf.Lerp(4f, 14f, row.HoldProgress);
                for (int k = 0; k < ui.orbit.Count; k++)
                {
                    float ang = Time.time * speed + k * Mathf.PI * 2f / ui.orbit.Count;
                    Vector3 o = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * 0.42f;
                    ui.orbit[k].rectTransform.anchoredPosition = ToCanvas(bpos + o);
                }
            }
            ui.aim.root.gameObject.SetActive(holding);
            ui.holdBg.gameObject.SetActive(holding);
            if (holding)
            {
                Vector3 bp = row.ball.transform.position;
                Vector3 dir = row.AimDirection;
                ui.aim.root.anchoredPosition = ToCanvas(bp + dir * 0.35f);
                ui.aim.root.localRotation = Quaternion.Euler(0f, 0f, AngleOf(dir));

                ui.holdBg.rectTransform.anchoredPosition = ToCanvas(bp - row.Forward * 0.45f);
                ui.holdFill.fillAmount = 1f - row.HoldProgress;
            }
        }
    }
}
