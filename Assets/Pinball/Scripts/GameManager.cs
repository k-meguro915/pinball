using UnityEngine;
using UnityEngine.Events;

namespace Pinball
{
    public enum MatchMode { VsCpu, Versus }
    /// <summary>画面中央に出す知らせの種類（外部の UI が文言を差し替えられるように）</summary>
    public enum BannerKind { None, Serve, Goal, Fault, DoubleFault, Let }
    public enum MatchState { Title, Serve, Playing, Point, MatchOver }

    /// <summary>
    /// 試合の進行を管理する。
    /// タイトル → サーブ（プランジャー発射）→ ラリー → 得点 → … → 決着。
    ///
    /// サーブのルール（テニス風）
    /// - サーブ権のあるプレイヤーが自分の発射レーンからプランジャーで打ち出す
    /// - 弱すぎてレーンに戻ってきたら FAULT。2回続けると DOUBLE FAULT で相手に1点
    /// - 発射レーンは盤面の外。発射前にルートを選べる：
    ///   自陣ルート（レーン途中で曲がり自陣の横から入る）／相手陣地ルート（レーンを上りきって相手陣地の横から入る）
    /// - サーブは servesPerTurn 本ごとに交代
    /// UIはα版として OnGUI で表示。
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("参照")]
        public Ball ball;
        public CpuPlayer cpu;
        [Tooltip("[0] = P1, [1] = P2 のプランジャー")]
        public Plunger[] plungers = new Plunger[2];
        [Tooltip("[0] = P1, [1] = P2 のサーブルート切り替え装置（壁の切れ目の開け閉め）")]
        public ServeDiverter[] diverters = new ServeDiverter[2];

        [Header("表示")]
        [Tooltip("ON = この GameManager が試合中の画面表示（スコア等）を描く。PinballUI の HUD を使うならオフ（タイトル画面は常に描く）")]
        public bool useBuiltInHud = true;
        [Tooltip("画面下の簡易な操作表示（P1: GAMEPAD など）を出す。別の操作説明 UI を使うならオフ")]
        public bool showControlsHint = true;

        [Header("イベント（操作説明 UI などをつなぐ）")]
        [Tooltip("タイトル画面になったとき")]
        public UnityEvent onTitle = new UnityEvent();
        [Tooltip("試合が始まったとき（再戦を含む）")]
        public UnityEvent onMatchStart = new UnityEvent();
        [Tooltip("勝敗が決まったとき")]
        public UnityEvent onMatchOver = new UnityEvent();

        [Header("ルール")]
        public MatchMode mode = MatchMode.VsCpu;
        public int targetScore = 5;
        public CpuPlayer.Level cpuLevel = CpuPlayer.Level.Normal;
        [Tooltip("何本ごとにサーブ交代するか")]
        public int servesPerTurn = 2;
        [Tooltip("この回数フォルトすると相手に1点")]
        public int faultsForPoint = 2;

        [Header("進行")]
        public float pointPauseTime = 1.5f;
        [Header("サーブ判定")]
        [Tooltip("ボールが盤面の内側（|X| < この値）まで入ったらサーブ完了。レーンは盤面の外 |X| > 5.3")]
        public float serveExitX = 4.7f;
        [Tooltip("ボールがほぼ止まったままこの秒数経つとサーブし直す（得点なし）")]
        public float stallResetTime = 8f;

#if ENABLE_INPUT_SYSTEM
        [Header("入力（Input System）")]
        [Tooltip("空なら Project Settings > Input System Package の Project-wide Actions を使う。どちらも無ければキーボード固定")]
        public UnityEngine.InputSystem.InputActionAsset inputActions;
        public string p1ActionMap = "P1";
        public string p2ActionMap = "P2";
        [Tooltip("Submit（決定）/ Cancel（タイトルへ）を読むマップ")]
        public string uiActionMap = "UI";
#endif

        [Header("物理")]
        [Tooltip("物理の更新回数/秒。ピンボールは高めの方が安定する")]
        public float physicsHz = 120f;

        [Header("操作説明（ControlsGuide）との連携")]
        [Tooltip("ON = 操作説明が開いている間は、タイトル（モード選択）を出さずに待ち、試合中なら一時停止する。" +
                 "ControlsGuide が無いシーンでは何もしない")]
        public bool waitForGuide = true;

        public static readonly int[] TargetOptions = { 3, 5, 10 };

        public MatchState State { get; private set; } = MatchState.Title;
        public int[] Scores { get; } = new int[2];
        public int Winner { get; private set; } = -1;
        public int Server { get; private set; }
        public int Faults { get; private set; }
        public ServeRoute CurrentRoute => routes[Server];

        readonly ServeRoute[] routes = new ServeRoute[2];

        int firstServer;
        int lastScorer = -1;
        float timer;
        float stallTimer;
        float serveStartTime;
        bool serveLaunched;
        float launchTime;

        string bannerText;
        Color bannerColor = Color.white;
        float bannerUntil;
        BannerKind bannerKind;
        int bannerPlayer = -1;

        // ===== 外部 UI 向けの公開情報 =====
        /// <summary>外部の HUD（PinballUI など）が動いているときに true にする。false のままなら内蔵の表示を出す</summary>
        public static bool ExternalHudActive { get; set; }
        bool warnedNoExternalHud;

        public bool ServeLaunched => serveLaunched;
        public bool BannerVisible => Time.time < bannerUntil && bannerKind != BannerKind.None;
        public BannerKind CurrentBanner => BannerVisible ? bannerKind : BannerKind.None;
        public int BannerPlayer => bannerPlayer;
        public Color BannerColor => bannerColor;
        public static Color PlayerColor(int p) => p == 0 ? P1Color : P2Color;

        // タイトル画面の選択状態
        int modeIndex;
        int targetIndex = 1;
        int levelIndex = 1;

        static readonly Color P1Color = new Color(0.35f, 0.7f, 1f);
        static readonly Color P2Color = new Color(1f, 0.45f, 0.4f);

        void Awake()
        {
            Instance = this;
            Time.fixedDeltaTime = 1f / physicsHz;
            if (GetComponent<PinballAudio>() == null) gameObject.AddComponent<PinballAudio>();
            // パワーショット（無ければ自動で付ける。使わないときはコンポーネントを無効にする）
            if (GetComponent<PowerShot>() == null) gameObject.AddComponent<PowerShot>();

#if ENABLE_INPUT_SYSTEM
            var asset = inputActions != null ? inputActions : PinballInput.GetProjectWideActions();
            PinballInput.Configure(asset, p1ActionMap, p2ActionMap, uiActionMap);
#endif

            modeIndex = mode == MatchMode.VsCpu ? 0 : 1;
            targetIndex = Mathf.Max(0, System.Array.IndexOf(TargetOptions, targetScore));
            levelIndex = (int)cpuLevel;
            // 画面は縦で固定（スマホ等では端末の向きも縦に固定）
            Screen.orientation = ScreenOrientation.Portrait;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (pausedByGuide) Time.timeScale = timeScaleBeforeGuide;
#if ENABLE_INPUT_SYSTEM
            PinballInput.Shutdown();
#endif
        }

        void Start()
        {
            FindBlockers();
            ToTitle();
        }

        // ===================== 外部から呼ばれる =====================

        /// <summary>
        /// 外部（フロントエンドなど）から試合のルールを入れる。タイトル画面の選択状態も合わせる。
        /// 次の StartMatch() からこの値で始まる（再戦も同じ値）。
        /// targetScore が TargetOptions に無い値なら、Awake と同じく TargetOptions[0] になる。
        /// firstServer：最初のサーブ番（0 = P1、1 = P2）。-1 なら今までどおり試合ごとにランダム
        /// </summary>
        public void ApplyRules(MatchMode newMode, int newTargetScore, CpuPlayer.Level newCpuLevel, int firstServer = -1)
        {
            mode = newMode;
            targetScore = newTargetScore;
            cpuLevel = newCpuLevel;
            modeIndex = mode == MatchMode.VsCpu ? 0 : 1;
            targetIndex = Mathf.Max(0, System.Array.IndexOf(TargetOptions, targetScore));
            levelIndex = (int)cpuLevel;
            fixedFirstServer = firstServer == 0 || firstServer == 1 ? firstServer : -1;
        }

        /// <summary>ApplyRules で決めた最初のサーブ番（-1 = ランダム）</summary>
        int fixedFirstServer = -1;

        /// <summary>このプレイヤーが今プランジャーを使えるか</summary>
        public bool CanPlunge(int player)
        {
            return State == MatchState.Serve && player == Server && Time.time >= serveStartTime + 0.3f && !GuideBlocking;
        }

        /// <summary>今ルートを変えられるか（サーブ番で、まだ打っていない）</summary>
        public bool CanChangeRoute(int player)
        {
            return State == MatchState.Serve && player == Server && !serveLaunched;
        }

        public void SetRoute(int player, ServeRoute route)
        {
            if (!CanChangeRoute(player)) return;
            routes[player] = route;
            if (diverters[player] != null) diverters[player].OpenFor(route);
        }

        /// <summary>プランジャーでボールが打ち出された</summary>
        public void OnPlunged(int player, float charge)
        {
            if (State != MatchState.Serve || player != Server) return;
            serveLaunched = true;
            launchTime = Time.time;
        }

        /// <summary>ownerPlayer のゴールにボールが入った</summary>
        public void OnGoal(int ownerPlayer)
        {
            if (State != MatchState.Playing && State != MatchState.Serve) return;
            int scorer = 1 - ownerPlayer;
            AwardPoint(scorer, $"{PlayerName(scorer)} GOAL!");
        }

        // ===================== 進行 =====================

        public void StartMatch()
        {
            mode = modeIndex == 0 ? MatchMode.VsCpu : MatchMode.Versus;
            targetScore = TargetOptions[targetIndex];
            cpuLevel = (CpuPlayer.Level)levelIndex;

            bool vsCpu = mode == MatchMode.VsCpu;
            PinballInput.SetCpuControlled(1, vsCpu);
            if (cpu != null)
            {
                cpu.level = cpuLevel;
                cpu.enabled = vsCpu;
            }

            Scores[0] = 0;
            Scores[1] = 0;
            Winner = -1;
            ResetBlockers();
            if (PowerShot.Instance != null) PowerShot.Instance.ResetAll();
            Rollover.ResetAll();
            lastScorer = -1;
            firstServer = fixedFirstServer >= 0 ? fixedFirstServer : (Random.value < 0.5f ? 0 : 1);
            routes[0] = ServeRoute.OwnField;
            routes[1] = ServeRoute.OwnField;
            BeginServe();
            onMatchStart.Invoke();
        }

        public void ToTitle()
        {
            State = MatchState.Title;
            PinballInput.SetCpuControlled(1, false);
            if (cpu != null) cpu.enabled = false;
            CloseDiverters();
            ball.Freeze(true);
            ball.gameObject.SetActive(false);
            bannerUntil = 0f;
            onTitle.Invoke();
        }

        int ComputeServer()
        {
            int total = Scores[0] + Scores[1];
            int turn = total / Mathf.Max(1, servesPerTurn);
            return (firstServer + turn) % 2;
        }

        void BeginServe()
        {
            Server = ComputeServer();
            Faults = 0;
            serveLaunched = false;
            serveStartTime = Time.time;
            State = MatchState.Serve;
            CloseDiverters();
            if (diverters[Server] != null) diverters[Server].OpenFor(routes[Server]);

            ball.gameObject.SetActive(true);
            ball.Freeze(false);
            ball.ResetTo(plungers[Server].SpawnPoint);
            ball.LastHitter = Server;

            ShowBanner($"{PlayerName(Server)} SERVE", Server == 0 ? P1Color : P2Color, 1.2f, BannerKind.Serve, Server);
        }

        BlockerRow[] blockerRows = new BlockerRow[0];

        void FindBlockers()
        {
#if UNITY_2023_1_OR_NEWER
            blockerRows = FindObjectsByType<BlockerRow>(FindObjectsSortMode.None);
#else
            blockerRows = FindObjectsOfType<BlockerRow>();
#endif
        }

        void ResetBlockers()
        {
            FindBlockers();
            foreach (var r in blockerRows) r.ResetPosition();
        }

        BlockerRow RowOf(int player)
        {
            foreach (var r in blockerRows) if (r != null && r.playerIndex == player) return r;
            return null;
        }

        /// <summary>ブロッカーの HP 表示用の文字（例: "BLOCKER  3 | 2"、ダウン中は "DOWN 2.4"）</summary>
        string BlockerHpText(int player)
        {
            var row = RowOf(player);
            if (row == null || row.Pieces.Length == 0) return "";
            var parts = new string[row.Pieces.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                var p = row.Pieces[i];
                parts[i] = p.collider != null && p.IsDown
                    ? $"DOWN {Mathf.Max(0f, p.downUntil - Time.time):0.0}"
                    : $"HP {p.hp}";
            }
            return "BLOCKER  " + string.Join("  |  ", parts);
        }

        /// <summary>
        /// 点が入った後のリセット：ブロッカーを復帰させて HP 満タン、アウトレーンを閉じる、フリッパーを元の状態に戻す、ロールオーバーを消す。
        /// ブロッカーの位置とパワーショットのゲージはそのまま（ゲージは持ち越し）。
        /// </summary>
        void ResetAfterPoint()
        {
            FindBlockers();
            foreach (var r in blockerRows) if (r != null) r.RestoreAll();

#if UNITY_2023_1_OR_NEWER
            var lanes = FindObjectsByType<Outlane>(FindObjectsSortMode.None);
            var flippers = FindObjectsByType<Flipper>(FindObjectsSortMode.None);
#else
            var lanes = FindObjectsOfType<Outlane>();
            var flippers = FindObjectsOfType<Flipper>();
#endif
            foreach (var l in lanes) if (l != null) l.ForceClose();
            foreach (var f in flippers) if (f != null) f.ResetState();
            Rollover.ResetAll();
        }

        /// <summary>壁の切れ目をすべて閉じる（ラリー用の形）</summary>
        void CloseDiverters()
        {
            foreach (var d in diverters)
                if (d != null) d.Close();
        }

        void AwardPoint(int scorer, string message, bool playGoalSound = true, BannerKind kind = BannerKind.Goal)
        {
            if (playGoalSound) PinballAudio.Play(PinballSound.Goal);
            Scores[scorer]++;
            lastScorer = scorer;

            ball.Freeze(true);
            ball.gameObject.SetActive(false);
            CloseDiverters();
            ResetAfterPoint();

            State = MatchState.Point;
            timer = pointPauseTime;
            ShowBanner(message, scorer == 0 ? P1Color : P2Color, pointPauseTime, kind, scorer);
        }

        void ShowBanner(string text, Color color, float seconds, BannerKind kind = BannerKind.None, int player = -1)
        {
            bannerKind = kind;
            bannerPlayer = player;
            bannerText = text;
            bannerColor = color;
            bannerUntil = Time.time + seconds;
        }

        // ===================== 操作説明（ControlsGuide）との連携 =====================
        // ControlsGuide は Pinball とは別のフォルダ（消しても動くように）なので、型を直接参照せずリフレクションで読む。
        static bool guideLookedUp;
        static System.Reflection.PropertyInfo guideAnyVisible, guideLastClosed;

        static void LookUpGuide()
        {
            if (guideLookedUp) return;
            guideLookedUp = true;
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType("ControlsGuide.ControlsGuideView", false);
                if (t == null) continue;
                var flags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static;
                guideAnyVisible = t.GetProperty("AnyVisible", flags);
                guideLastClosed = t.GetProperty("LastClosedTime", flags);
                break;
            }
        }

        /// <summary>操作説明が開いている</summary>
        public bool GuideOpen
        {
            get
            {
                if (!waitForGuide) return false;
                LookUpGuide();
                return guideAnyVisible != null && (bool)guideAnyVisible.GetValue(null);
            }
        }

        /// <summary>操作説明が開いている、または閉じた直後（閉じたボタンがゲームに伝わらないよう少し待つ）</summary>
        public bool GuideBlocking
        {
            get
            {
                if (!waitForGuide) return false;
                if (GuideOpen) return true;
                if (guideLastClosed == null) return false;
                return Time.unscaledTime - (float)guideLastClosed.GetValue(null) < 0.35f;
            }
        }

        bool pausedByGuide;
        float timeScaleBeforeGuide = 1f;

        void Update()
        {
            // 操作説明が開いている間：タイトルでは待つ、試合中は一時停止
            bool blocking = GuideBlocking;
            if (blocking && (State == MatchState.Serve || State == MatchState.Playing || State == MatchState.Point))
            {
                if (!pausedByGuide)
                {
                    pausedByGuide = true;
                    timeScaleBeforeGuide = Time.timeScale;
                    Time.timeScale = 0f;
                }
            }
            else if (pausedByGuide && !blocking)
            {
                pausedByGuide = false;
                Time.timeScale = timeScaleBeforeGuide;
            }
            if (blocking) return;

            switch (State)
            {
                case MatchState.Title:
                    if (PinballInput.ConfirmPressed) StartMatch();
                    break;

                case MatchState.Serve:
                    if (PinballInput.QuitMatchPressed) { ToTitle(); break; }
                    UpdateServe();
                    break;

                case MatchState.Playing:
                    if (PinballInput.QuitMatchPressed) { ToTitle(); break; }
                    UpdateRally();
                    break;

                case MatchState.Point:
                    timer -= Time.deltaTime;
                    if (timer > 0f) break;
                    if (Scores[lastScorer] >= targetScore)
                    {
                        Winner = lastScorer;
                        State = MatchState.MatchOver;
                        if (cpu != null) cpu.enabled = false;
                        onMatchOver.Invoke();
                    }
                    else
                    {
                        BeginServe();
                    }
                    break;

                case MatchState.MatchOver:
                    if (PinballInput.ConfirmPressed) StartMatch();
                    else if (PinballInput.BackPressed) ToTitle();
                    break;
            }
        }

        void UpdateServe()
        {
            // 打つ前はルートを切り替えられる
            if (!serveLaunched && PinballInput.RouteTogglePressed(Server))
            {
                var next = routes[Server] == ServeRoute.OwnField ? ServeRoute.OpponentField : ServeRoute.OwnField;
                SetRoute(Server, next);
            }

            // サーブ側から見たX（P2の台は点対称なので符号を反転）
            float side = Server == 0 ? 1f : -1f;
            float x = ball.transform.position.x * side;

            if (x < serveExitX)
            {
                // 盤面に入った → ラリー開始。壁の切れ目を閉じる
                State = MatchState.Playing;
                stallTimer = 0f;
                CloseDiverters();
                return;
            }

            if (serveLaunched && Time.time - launchTime > 0.5f && plungers[Server].HasBallReady)
            {
                // 弱すぎてレーンに戻ってきた
                serveLaunched = false;
                Faults++;
                PinballAudio.Play(PinballSound.Fault);
                if (Faults >= faultsForPoint)
                    AwardPoint(1 - Server, "DOUBLE FAULT", false, BannerKind.DoubleFault);
                else
                    ShowBanner("FAULT", new Color(1f, 0.85f, 0.3f), 1.0f, BannerKind.Fault, Server);
            }

            // 打った後にレーン内で止まってしまった場合の保険
            if (serveLaunched && Time.time - launchTime > 6f) { LetServe(); return; }

            if (ball.transform.position.y < -3f) BeginServe();
        }

        void UpdateRally()
        {
            // 台の外に飛び出した場合の保険
            if (ball.transform.position.y < -3f)
            {
                LetServe();
                return;
            }

            if (ball.Body.GetVelocity().sqrMagnitude < 0.3f * 0.3f) stallTimer += Time.deltaTime;
            else stallTimer = 0f;

            if (stallTimer > stallResetTime) LetServe();
        }

        void LetServe()
        {
            BeginServe();
            ShowBanner("LET  (re-serve)", Color.white, 1.2f, BannerKind.Let);
        }

        // ===================== UI =====================

        GUIStyle titleStyle, labelStyle, buttonStyle, hudStyle, hudRightStyle, centerStyle, smallStyle, smallRightStyle, midStyle;
        Texture2D panelTex, whiteTex;

        public string PlayerName(int p) => p == 0 ? "P1" : (mode == MatchMode.VsCpu ? "CPU" : "P2");

        void EnsureStyles()
        {
            if (titleStyle != null) return;

            panelTex = new Texture2D(1, 1);
            panelTex.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.75f));
            panelTex.Apply();
            whiteTex = Texture2D.whiteTexture;

            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 48, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            titleStyle.normal.textColor = Color.white;
            labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold };
            labelStyle.normal.textColor = new Color(0.8f, 0.8f, 0.85f);
            midStyle = new GUIStyle(labelStyle) { alignment = TextAnchor.UpperCenter };
            buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 24, fontStyle = FontStyle.Bold };
            hudStyle = new GUIStyle(GUI.skin.label) { fontSize = 40, fontStyle = FontStyle.Bold };
            hudRightStyle = new GUIStyle(hudStyle) { alignment = TextAnchor.UpperRight };
            centerStyle = new GUIStyle(GUI.skin.label) { fontSize = 64, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            centerStyle.normal.textColor = Color.white;
            smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 18 };
            smallStyle.normal.textColor = new Color(0.85f, 0.85f, 0.9f);
            smallRightStyle = new GUIStyle(smallStyle) { alignment = TextAnchor.UpperRight };
        }

        void OnGUI()
        {
            EnsureStyles();

            if (State == MatchState.Title)
            {
                if (!GuideOpen) DrawTitle(); // 操作説明を閉じてからモード選択
                return;
            }

            if (!useBuiltInHud)
            {
                if (ExternalHudActive) return; // 試合中の表示は外部の HUD に任せる

                // 外部の HUD が見つからない → 何も出ないのを避けて内蔵の表示を出す
                if (!warnedNoExternalHud)
                {
                    warnedNoExternalHud = true;
                    Debug.LogWarning("[Pinball] Use Built In Hud がオフですが、外部の HUD（PinballHUD）が動いていません。内蔵の表示を出します。" +
                                     "Tools > Pinball UI > Create HUD In Scene でシーンに置き、シーンを保存してください。");
                }
            }

            DrawHud();

            if (State == MatchState.Serve) DrawServeInfo();

            if (State == MatchState.MatchOver)
            {
                GUI.DrawTexture(new Rect(0, Screen.height / 2f - 110, Screen.width, 220), panelTex);
                centerStyle.normal.textColor = Winner == 0 ? P1Color : P2Color;
                GUI.Label(new Rect(0, Screen.height / 2f - 100, Screen.width, 120), $"{PlayerName(Winner)} WINS!", centerStyle);
                centerStyle.normal.textColor = Color.white;
                GUI.Label(new Rect(0, Screen.height / 2f + 30, Screen.width, 60),
                    "Enter: REMATCH     Esc: TITLE", new GUIStyle(labelStyle) { alignment = TextAnchor.MiddleCenter, fontSize = 26 });
            }
            else if (Time.time < bannerUntil && !string.IsNullOrEmpty(bannerText))
            {
                centerStyle.normal.textColor = bannerColor;
                GUI.Label(new Rect(0, Screen.height / 2f - 90, Screen.width, 180), bannerText, centerStyle);
                centerStyle.normal.textColor = Color.white;
            }
        }

        // ---- HUD の配置（画面は縦向き固定。P1 = 下、P2 = 上） ----
        // 縦長の画面（9:16 など）: 台の上下の帯に出す。P2 = 上の帯、P1 = 下の帯。スコアは左、ゲージは右
        // 横長の画面（エディタの Game ビュー等）: 台の左右に出す。P1 = 左下、P2 = 右上
        struct PlayerHudLayout
        {
            public bool scoreRight; // スコア・操作説明を右寄せにするか
            public bool gaugeRight; // ゲージを右寄せにするか
            public float scoreY;    // スコアの上端
            public float hintY;     // 操作説明の上端
            public float gaugeY;    // パワーゲージの上端（ラベルはこの上に2行）
        }

        bool IsPortraitScreen => Screen.height > Screen.width;

        PlayerHudLayout Layout(int p)
        {
            float H = Screen.height;
            if (IsPortraitScreen)
            {
                // スコア → その下に HP（+46）→ 操作表示、の順に縦に並べる
                return p == 0
                    ? new PlayerHudLayout { scoreRight = false, gaugeRight = true, scoreY = H - 130f, hintY = H - 40f, gaugeY = H - 90f }
                    : new PlayerHudLayout { scoreRight = false, gaugeRight = true, scoreY = 8f, hintY = 86f, gaugeY = 70f };
            }
            return p == 0
                ? new PlayerHudLayout { scoreRight = false, gaugeRight = false, scoreY = H - 130f, hintY = H - 34f, gaugeY = H - 170f }
                : new PlayerHudLayout { scoreRight = true, gaugeRight = true, scoreY = 10f, hintY = 90f, gaugeY = 180f };
        }

        float GaugeWidth => Mathf.Min(320f, Screen.width * 0.4f);

        void DrawHud()
        {
            bool serving = State == MatchState.Serve || State == MatchState.Playing;

            for (int p = 0; p < 2; p++)
            {
                var l = Layout(p);
                var style = l.scoreRight ? hudRightStyle : hudStyle;
                style.normal.textColor = p == 0 ? P1Color : P2Color;
                string mark = serving && Server == p ? "●" : "";
                string text = l.scoreRight ? $"{(mark != "" ? mark + "  " : "")}{Scores[p]}  {PlayerName(p)}"
                                      : $"{PlayerName(p)}  {Scores[p]}{(mark != "" ? "  " + mark : "")}";
                float x = l.scoreRight ? Screen.width - 480f : 30f;
                GUI.Label(new Rect(x, l.scoreY, 450, 60), text, style);

                // ブロッカーの HP（スコアのすぐ下）
                string hp = BlockerHpText(p);
                if (hp != "")
                {
                    float hx2 = l.scoreRight ? Screen.width - 670f : 30f;
                    GUI.Label(new Rect(hx2, l.scoreY + 46f, 640, 26), hp, l.scoreRight ? smallRightStyle : smallStyle);
                }

                if (showControlsHint || (p == 1 && mode == MatchMode.VsCpu))
                {
                    string hint = p == 1 && mode == MatchMode.VsCpu ? $"CPU ({cpuLevel})" : ControlsHint(p);
                    if (p == 0 && mode == MatchMode.VsCpu && !PinballInput.UsesActions(0)) hint += "  (or ↑ ↓ ← →)";
                    float hx = l.scoreRight ? Screen.width - 670f : 30f;
                    GUI.Label(new Rect(hx, l.hintY, 640, 30), hint, l.scoreRight ? smallRightStyle : smallStyle);
                }
            }

            if (IsPortraitScreen)
            {
                // 下の帯の右下（ゲージの下）
                GUI.Label(new Rect(Screen.width - 430, Screen.height - 40, 400, 30), $"FIRST TO {targetScore}   Esc: TITLE", smallRightStyle);
            }
            else
            {
                // 台が中央を縦に占めるので、共通の情報は左側の中ほどに
                GUI.Label(new Rect(30, Screen.height / 2f - 30, 400, 30), $"FIRST TO {targetScore}", labelStyle);
                GUI.Label(new Rect(30, Screen.height / 2f + 5, 400, 30), "Esc: TITLE", smallStyle);
            }
        }

        string ControlsHint(int p)
        {
            string name = p == 0 ? "P1" : "P2";
            if (PinballInput.UsesActions(p))
                return PinballInput.HasGamepad(p) ? $"{name}: GAMEPAD / Input Actions" : $"{name}: Input Actions (no gamepad)";
            return p == 0 ? "P1: W / S   LAUNCH: D / Space   ROUTE: A" : "P2: ↑ / ↓   LAUNCH: ←   ROUTE: →";
        }

        void DrawServeInfo()
        {
            // サーブ側にパワーゲージ・フォルト数・ルート
            float w = GaugeWidth;
            const float h = 22f;
            var l = Layout(Server);
            float x = l.gaugeRight ? Screen.width - 30f - w : 30f;
            float y = l.gaugeY;

            var c = Server == 0 ? P1Color : P2Color;
            var label = l.gaugeRight ? new GUIStyle(labelStyle) { alignment = TextAnchor.UpperRight } : labelStyle;
            string faultText = Faults > 0 ? $"   FAULT {Faults}/{faultsForPoint}" : "";
            GUI.Label(new Rect(x, y - 30, w, 28), $"POWER{faultText}", label);

            string routeText = CurrentRoute == ServeRoute.OwnField ? "ROUTE: OWN FIELD" : "ROUTE: OPPONENT FIELD";
            string routeKey = PinballInput.IsCpu(Server) || PinballInput.UsesActions(Server) ? "" : (Server == 0 ? "  [A]" : "  [→]");
            GUI.Label(new Rect(x, y - 60, w, 28), routeText + routeKey, label);

            GUI.DrawTexture(new Rect(x, y, w, h), panelTex);
            float charge = plungers[Server] != null ? plungers[Server].Charge : 0f;
            var prev = GUI.color;
            GUI.color = c;
            float fill = (w - 4f) * charge;
            float fx = l.gaugeRight ? x + w - 2f - fill : x + 2f;
            GUI.DrawTexture(new Rect(fx, y + 2f, fill, h - 4f), whiteTex);
            GUI.color = prev;
        }

        void DrawTitle()
        {
            float w = Mathf.Min(560f, Screen.width - 40f), h = modeIndex == 0 ? 470f : 380f;
            var area = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);
            GUI.DrawTexture(area, panelTex);

            GUILayout.BeginArea(new Rect(area.x + 30, area.y + 20, w - 60, h - 40));
            GUILayout.Label("PINBALL VS", titleStyle, GUILayout.Height(70));

            GUILayout.Label("MODE", labelStyle);
            modeIndex = GUILayout.Toolbar(modeIndex, new[] { "VS CPU", "1 VS 1" }, buttonStyle, GUILayout.Height(48));

            GUILayout.Space(8);
            GUILayout.Label("FIRST TO", labelStyle);
            targetIndex = GUILayout.Toolbar(targetIndex, new[] { "3", "5", "10" }, buttonStyle, GUILayout.Height(48));

            if (modeIndex == 0)
            {
                GUILayout.Space(8);
                GUILayout.Label("CPU LEVEL", labelStyle);
                levelIndex = GUILayout.Toolbar(levelIndex, new[] { "EASY", "NORMAL", "HARD" }, buttonStyle, GUILayout.Height(48));
            }

            GUILayout.Space(18);
            if (GUILayout.Button("START  (Enter)", buttonStyle, GUILayout.Height(60))) StartMatch();
            GUILayout.EndArea();
        }
    }
}
