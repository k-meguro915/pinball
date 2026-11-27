using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PinballDodgeball
{
    /// <summary>
    /// ドッジボールルールの進行。
    /// ・ゴールと点はない。相手の選手（ブロッカー 3 人）を全員アウトにしたら勝ち
    /// ・アウトになった選手は、相手陣地の左右の壁（ランダム）に外野ブロッカーとして出る（動かない・自動でキャッチ・アウトにならない）
    /// ・外野が最後に投げた球で相手をアウトにすると、その外野は元の選手に戻る
    /// ・サーブは試合開始のときだけ。ダブルフォルトはサーブ権が相手へ移る
    /// ・内野のジャストキャッチ（B を押し始めてから 3F 以内につかむ）→ 投げる球がパワーショット（速い球。貫通はしない）
    /// ・フリッパーはない（2026-11-27）。コートの奥は壁
    /// ・相手陣地のドロップターゲットを 3 つ倒すと、次に投げる球がパワーショットになる（2026-11-27：ターゲットは一度台から外した）
    /// ・背面の加速レーン（2026-11-27）：奥へ転がった球を、半円のレーンで中央の線の手前まで運ぶ（出た球は誰の球でもない）
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class DBGameManager : MonoBehaviour
    {
        public enum Mode { VsCpu, Versus }
        public enum CpuLevel { Easy, Normal, Hard }
        public enum OutfieldControl { Auto, Buttons }
        public enum State { Title, Serve, Rally, Over }

        public static DBGameManager Instance { get; private set; }

        [Header("ルール")]
        public Mode mode = Mode.VsCpu;
        public CpuLevel cpuLevel = CpuLevel.Normal;
        [Tooltip("外野ブロッカーの投げ方：全自動 = キャッチして一定時間で、相手のいちばん近い選手をねらって投げる／ボタン = 持ち主が B で投げる（2.5 秒で自動）、向きはスティックの上下")]
        public OutfieldControl outfieldControl = OutfieldControl.Auto;
        [Tooltip("タイトルを出さずにすぐ試合を始める（確認用）")]
        public bool skipTitle;

        [Header("球の速さ（まとめて）")]
        [Tooltip("試合の始まりに、球にかかわる速さをまとめてこの倍率にする（投げる・撃つ速さ、パワーショット、バンパー、背面の加速レーン、" +
                 "スピナー、射出装置、台の傾き、速さの上限、サーブの強さ）。各部品のインスペクターの値は 1 倍のときの値")]
        public float ballSpeedScale = 1.25f;

        [Header("外野ブロッカー")]
        [Tooltip("中心の X（の大きさ）。ステップ19：壁ぎわ → 選手の範囲のすぐ外")]
        public float outfieldX = DBConst.OutfieldX;
        [Tooltip("中心の Z（相手陣地側、中央からの距離）。1 人目・2 人目の置き場所")]
        public float[] outfieldZ = { DBConst.OutfieldZ[0], DBConst.OutfieldZ[1] };
        public float outfieldSpawnDelay = 0.4f;
        [Tooltip("出る場所からこの距離に球がある間は、出るのを待つ")]
        public float outfieldClearance = 0.9f;
        [Tooltip("全自動：キャッチしてから投げるまでの時間")]
        public float outfieldAutoThrowDelay = 1f;
        [Tooltip("ボタン：B を押さなかったときに自動で投げるまでの時間")]
        public float outfieldButtonAutoThrowTime = 2.5f;
        public float outfieldLaunchSpeed = 16f;

        [Header("サーブ")]
        public float minPower = 9f;
        public float maxPower = 25f;
        public float chargeTime = 1.0f;
        public int faultsForDouble = 2;
        [Tooltip("サーブ完了の判定：|X| < 外壁の内面 - この値")]
        public float serveCompleteInset = 0.3f;
        [Tooltip("サーブのルートの初期値：ON = 相手陣地へ")]
        public bool defaultRouteOpponent;

        [Header("見た目")]
        public Color[] playerColors = { new Color(0.25f, 0.6f, 1f), new Color(1f, 0.4f, 0.3f) };
        public Material[] playerMaterials = new Material[2];
        public float messageTime = 1.4f;

        [Header("CPU")]
        public DBCpu cpu = new DBCpu();

        [Header("参照（台を作ると自動で入る）")]
        public DBBall ball;
        public DBBlockerRow[] rows = new DBBlockerRow[2];
        public DBServeLane[] lanes = new DBServeLane[2];
        [Tooltip("4 列（各陣地の左右の壁ぎわ）")]
        public DBDropTargetBank[] dropTargetBanks = new DBDropTargetBank[0];
        [Tooltip("2 本（各陣地に 1 本）")]
        public DBCoaster[] coasters = new DBCoaster[0];
        public Transform outfieldRoot;

        // ---- 状態 ----
        public State CurrentState { get; private set; } = State.Title;
        public bool IsPlaying => CurrentState == State.Serve || CurrentState == State.Rally;
        public bool CanKnock => IsPlaying;
        public int Server { get; private set; }
        public bool RouteOpponent { get; private set; }
        public bool ServeLaunched { get; private set; }
        public bool Charging { get; private set; }
        public float CurrentPower { get; private set; }
        public float Charge01 => Mathf.InverseLerp(minPower, maxPower, CurrentPower);
        public int Faults { get; private set; }
        public int Winner { get; private set; } = -1;
        public int ServeToken { get; private set; }

        /// <summary>ドロップターゲットを 3 つ倒して、次に投げる球がパワーショットになるか（[P1, P2]）</summary>
        readonly bool[] powerReady = new bool[2];
        public bool PowerReady(int p) => p >= 0 && p < 2 && powerReady[p];
        /// <summary>選手が投げるときに呼ぶ。パワーショットの権利があれば使って true</summary>
        public bool ConsumePowerReady(int p)
        {
            if (!PowerReady(p)) return false;
            powerReady[p] = false;
            return true;
        }

        public event Action<int> MatchOver;

        public class Message
        {
            public string text;
            public Color color;
            public bool hasWorld;
            public Vector3 world;
            public float start, until;
        }
        public readonly List<Message> messages = new List<Message>();

        class Slot
        {
            public DBOutfieldBlocker outfield;
            public bool reserved;
            public bool Free => outfield == null && !reserved;
        }
        readonly Slot[,,] slots = new Slot[2, 2, 2]; // [持ち主, 壁（0 = 画面の左 / 1 = 右）, 置き場所]

        readonly DBInputState[] inputs = new DBInputState[2];
        readonly bool[] prevB = new bool[2], prevY = new bool[2];
        int nextStarter;
        bool redirected;
        float launchTime, chargeStart;

        public DBInputState GetInput(int p) => inputs[p];
        public bool IsCpu(int p) => mode == Mode.VsCpu && p == 1;

        /// <summary>
        /// 1 VS 1 の画面分け（2026-11-27）をしているか。左半分 = P1、右半分 = P2 の画面（180 度回す）。
        /// 分けてよいかは Pinball.SplitScreen.Allowed（FE のあそびかたでは分けない）
        /// </summary>
        public static bool SplitActive
        {
            get
            {
                var g = Instance;
                return Pinball.SplitScreen.Allowed && Application.isPlaying && g != null &&
                       g.mode == Mode.Versus && g.CurrentState != State.Title;
            }
        }
        public string PlayerName(int p) => DBConst.PlayerName(p, IsCpu(p));
        public Color PlayerColor(int p) => playerColors != null && p < playerColors.Length ? playerColors[p] : Color.white;

        // ---------------------------------------------------------------

        void Awake()
        {
            Instance = this;
            Time.fixedDeltaTime = 1f / 120f;
            for (int p = 0; p < 2; p++)
                for (int w = 0; w < 2; w++)
                    for (int k = 0; k < 2; k++)
                        slots[p, w, k] = new Slot();
            FindMissingRefs();
            ApplyPhysicsMaterials();
            if (GetComponent<DBBoardIndicators>() == null) gameObject.AddComponent<DBBoardIndicators>();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Start()
        {
            foreach (var row in rows)
            {
                if (row == null) continue;
                row.Bind(this);
                row.Knocked += OnKnocked;
                row.Revived += OnRevived;
                row.JustCaught += (r, i) => Post("ジャストキャッチ！", PlayerColor(r.owner), r.blockers[i].position);
                row.PowerShotThrown += (r, i) => Post("パワーショット！", PlayerColor(r.owner), r.blockers[i].position);
            }
            foreach (var bank in dropTargetBanks)
            {
                if (bank == null) continue;
                bank.TargetDropped += (bk, i) => Post("ターゲット " + bk.DownCount + "/" + bk.Count, PlayerColor(bk.Attacker), bk.targets[i].position);
                bank.Completed += OnTargetsCompleted;
            }
            foreach (var co in coasters)
            {
                if (co == null) continue;
                co.Entered += c => Post("コースター！", PlayerColor(c.owner), c.transform.position);
                co.Launched += c => Post("発射！", PlayerColor(c.owner), c.LaunchPoint);
            }
            ApplySpeedScale();
            if (skipTitle) StartMatch();
            else GoTitle();
        }

        void FindMissingRefs()
        {
            if (ball == null) ball = FindFirstObjectByType<DBBall>();
            foreach (var r in FindObjectsByType<DBBlockerRow>(FindObjectsSortMode.None))
                if (rows[r.owner] == null) rows[r.owner] = r;
            foreach (var l in FindObjectsByType<DBServeLane>(FindObjectsSortMode.None))
                if (lanes[l.owner] == null) lanes[l.owner] = l;
            if (dropTargetBanks == null || dropTargetBanks.Length == 0)
                dropTargetBanks = FindObjectsByType<DBDropTargetBank>(FindObjectsSortMode.None);
            if (coasters == null || coasters.Length == 0)
                coasters = FindObjectsByType<DBCoaster>(FindObjectsSortMode.None);
        }

        void ApplyPhysicsMaterials()
        {
            foreach (var c in FindObjectsByType<Collider>(FindObjectsSortMode.None))
            {
                if (c.isTrigger) continue;
                if (c.GetComponentInParent<DBBall>() != null) DBUtil.ApplyPhysics(c, 0.35f, 0f);
                else if (c.GetComponentInParent<DBBumper>() != null) DBUtil.ApplyPhysics(c, 0.6f, 0f);
                else DBUtil.ApplyPhysics(c, 0.45f, 0f);
            }
        }

        // ---------------------------------------------------------------
        // 試合の流れ

        void ResetDropTargets()
        {
            foreach (var bank in dropTargetBanks) if (bank != null) bank.ResetAll();
            foreach (var co in coasters) if (co != null) co.Cancel();
        }

        /// <summary>ドロップターゲットを 3 つ倒した：攻める側の次の球がパワーショットになる</summary>
        void OnTargetsCompleted(DBDropTargetBank bank)
        {
            Post("ターゲット クリア！ 次の球はパワー", PlayerColor(bank.Attacker), bank.transform.position);
            if (IsPlaying) powerReady[bank.Attacker] = true;
        }

        float SpeedScale => Mathf.Max(0.1f, ballSpeedScale);
        bool speedScaled;

        /// <summary>
        /// 球にかかわる速さをまとめて ballSpeedScale 倍にする（1 回だけ。再生中の値だけを変えるので、シーンの値は 1 倍のまま）。
        /// 台の傾きも同じ倍率にするので、サーブの強さも同じ倍率にする（届く距離 = 速さ² ÷ 2×傾き が 1.25 倍になる）
        /// </summary>
        void ApplySpeedScale()
        {
            if (speedScaled) return;
            speedScaled = true;
            float k = SpeedScale;
            if (Mathf.Approximately(k, 1f)) return;

            if (ball != null)
            {
                ball.maxSpeed *= k;
                ball.slopeAcceleration *= k;
                ball.powerEndSpeed *= k;
            }
            foreach (var row in rows)
            {
                if (row == null) continue;
                row.launchSpeed *= k;
                row.powerShotSpeed *= k;
            }
            foreach (var b in FindObjectsByType<DBBumper>(FindObjectsSortMode.None)) b.kickSpeed *= k;
            foreach (var s in FindObjectsByType<DBSpinner>(FindObjectsSortMode.None))
            {
                s.minBoostSpeed *= k;
                s.maxBoostSpeed *= k;
            }
            foreach (var bp in FindObjectsByType<DBBoostPad>(FindObjectsSortMode.None))
            {
                bp.acceleration *= k;
                bp.maxAlongSpeed *= k;
            }
            foreach (var co in coasters)
            {
                if (co == null) continue;
                co.launchSpeed *= k;
                co.topSpeed *= k;
                co.dropBoost *= k;
            }
            minPower *= k;
            maxPower *= k;
            // 外野の速さは、作るときに k を掛けて入れる
        }

        public void GoTitle()
        {
            StopAllCoroutines();
            ClearOutfields();
            ResetDropTargets();
            ResetPowerReady();
            foreach (var row in rows) if (row != null) row.ResetAll();
            foreach (var l in lanes) if (l != null) l.SetDoors(false, false);
            if (ball != null) ball.Hold(Vector3.zero);
            Winner = -1;
            messages.Clear();
            CurrentState = State.Title;
        }

        public void StartMatch()
        {
            StopAllCoroutines();
            ClearOutfields();
            CacheOutfieldMarks(); // 床の目印の位置を読み直す（シーンで目印を動かしていても、その上に出る）
            Winner = -1;
            messages.Clear();
            foreach (var row in rows) if (row != null) row.ResetAll();
            ResetDropTargets();
            ResetPowerReady();
            cpu.ResetState();
            BeginServe(nextStarter);
            nextStarter = 1 - nextStarter;
        }

        void BeginServe(int p)
        {
            CurrentState = State.Serve;
            Server = p;
            Faults = 0;
            RouteOpponent = defaultRouteOpponent;
            PrepareServeBall();
            Post(PlayerName(p) + " のサーブ", PlayerColor(p));
        }

        void PrepareServeBall()
        {
            ServeToken++;
            ServeLaunched = false;
            Charging = false;
            redirected = false;
            CurrentPower = minPower;
            ball.Hold(lanes[Server].plungerPosition);
            ball.ClearOwner();
            UpdateDoors();
        }

        void UpdateDoors()
        {
            for (int p = 0; p < 2; p++)
            {
                if (lanes[p] == null) continue;
                bool serving = CurrentState == State.Serve && p == Server;
                lanes[p].SetDoors(serving && !RouteOpponent, serving && RouteOpponent);
            }
        }

        void EndMatch(int winner)
        {
            Winner = winner;
            CurrentState = State.Over;
            StopAllCoroutines();
            if (ball != null) ball.Hold(ball.Position);
            foreach (var l in lanes) if (l != null) l.SetDoors(false, false);
            MatchOver?.Invoke(winner);
        }

        // ---------------------------------------------------------------

        void Update()
        {
            for (int p = 0; p < 2; p++)
            {
                DBInputState h = IsCpu(p) ? cpu.Tick(this, p) : DBInput.ReadHeld(p);
                if (p == 1 && !IsCpu(p) && SplitActive)
                {
                    // 画面分けでは P2 の画面が 180 度回っている。P2 の画面の向きで動くよう、スティックを逆にする
                    h.move = -h.move;
                }
                h.bDown = h.b && !prevB[p];
                h.bUp = !h.b && prevB[p];
                h.yDown = h.y && !prevY[p];
                prevB[p] = h.b;
                prevY[p] = h.y;
                inputs[p] = h;
            }

            if (CurrentState == State.Serve) UpdateServeInput();

            float t = Time.time;
            messages.RemoveAll(m => t > m.until);
        }

        void UpdateServeInput()
        {
            if (ServeLaunched) return;
            var s = inputs[Server];
            if (s.yDown && !Charging)
            {
                RouteOpponent = !RouteOpponent;
                UpdateDoors();
            }
            if (s.bDown && !Charging)
            {
                Charging = true;
                chargeStart = Time.time;
            }
            if (Charging)
            {
                float k = chargeTime > 0f ? Mathf.Clamp01((Time.time - chargeStart) / chargeTime) : 1f;
                CurrentPower = Mathf.Lerp(minPower, maxPower, k);
                if (!s.b) Launch();
            }
        }

        void Launch()
        {
            ServeLaunched = true;
            Charging = false;
            launchTime = Time.time;
            redirected = false;
            ball.Release(new Vector3(0f, 0f, DBConst.Sign(Server) * CurrentPower));
            ball.LastHitter = Server;
            ball.LastOutfield = null;
            ball.FromServe = true; // サーブの球は外野をすり抜ける（選手に触れるまで）
        }

        void FixedUpdate()
        {
            if (ball == null) return;
            if (CurrentState != State.Serve || !ServeLaunched) return;
            var lane = lanes[Server];
            Vector3 bp = ball.Position;
            Vector3 v = ball.Velocity;
            float sg = DBConst.Sign(Server);

            if (!redirected)
            {
                bool inLane = Mathf.Abs(bp.x - lane.laneX) < 0.6f;
                float exitZ = RouteOpponent ? lane.oppExitZ : lane.ownExitZ;
                float past = (bp.z - exitZ) * sg;
                if (inLane && v.z * sg > 0f && past >= 0f && past < 0.8f)
                {
                    Redirect(lane, exitZ, v.magnitude);
                }
                else if (inLane && Time.time - launchTime > 0.3f &&
                         bp.z * sg <= lane.plungerPosition.z * sg + 0.5f && v.z * sg <= 0.05f)
                {
                    Fault();
                }
            }
            else if (Mathf.Abs(bp.x) < DBConst.PlayHalfWidth - serveCompleteInset)
            {
                CurrentState = State.Rally;
                UpdateDoors();
            }
        }

        void ResetPowerReady()
        {
            powerReady[0] = powerReady[1] = false;
        }

        void Redirect(DBServeLane lane, float exitZ, float speed)
        {
            speed = Mathf.Clamp(speed, 5f, 14f);
            var dir = new Vector3(-Mathf.Sign(lane.laneX), 0f, DBConst.Sign(Server) * 0.35f).normalized;
            ball.Teleport(new Vector3(lane.laneX, DBConst.BallY, exitZ));
            ball.Body.SetVelocity(dir * speed);
            redirected = true;
        }

        void Fault()
        {
            Faults++;
            if (Faults >= faultsForDouble)
            {
                Post("ダブルフォルト！ サーブ交代", Color.white);
                BeginServe(DBConst.Opponent(Server));
            }
            else
            {
                Post("フォルト", Color.white);
                PrepareServeBall();
            }
        }

        // ---------------------------------------------------------------
        // アウト・外野・復活

        void OnKnocked(DBBlockerRow row, int index, DBBall b)
        {
            int victim = row.owner;
            int attacker = DBConst.Opponent(victim);
            DBOutfieldBlocker of = b.LastOutfield;
            b.ClearOwner(); // 当てた球は誰の球でもなくなる（続けて 2 人目は倒せない）

            Post("アウト！", PlayerColor(victim), row.blockers[index].position);

            if (of != null && of.owner == attacker) ReturnOutfield(of);

            if (!row.HasPlayers)
            {
                EndMatch(attacker);
                return;
            }
            StartCoroutine(SpawnOutfield(victim, index));
        }

        void OnRevived(DBBlockerRow row, int index)
        {
            Post("復活！", PlayerColor(row.owner), row.blockers[index].position);
        }

        void ReturnOutfield(DBOutfieldBlocker ob)
        {
            for (int w = 0; w < 2; w++)
                for (int k = 0; k < 2; k++)
                    if (slots[ob.owner, w, k].outfield == ob) slots[ob.owner, w, k].outfield = null;
            Post("外野から戻る", PlayerColor(ob.owner), ob.Center);
            var row = rows[ob.owner];
            if (row != null) row.RequestRevive(ob.blockerIndex);
            ob.Despawn();
        }

        IEnumerator SpawnOutfield(int p, int blockerIndex)
        {
            int w = UnityEngine.Random.value < 0.5f ? 0 : 1;
            int k = FreeSlot(p, w);
            if (k < 0) { w = 1 - w; k = FreeSlot(p, w); }
            if (k < 0) yield break;

            var slot = slots[p, w, k];
            slot.reserved = true;
            Vector3 center = SlotCenter(p, w, k);

            yield return new WaitForSeconds(outfieldSpawnDelay);
            while (IsPlaying && ball != null && !ball.IsHeld &&
                   (DBUtil.Flat(ball.Position) - DBUtil.Flat(center)).magnitude < outfieldClearance + DBConst.BlockerLength * 0.5f)
                yield return null;

            slot.reserved = false;
            if (!IsPlaying) yield break;

            var ob = CreateOutfield(p, w == 0 ? -1 : 1, center, blockerIndex);
            slot.outfield = ob;
            ob.PlaySpawn();
            Post("外野へ", PlayerColor(p), center);
        }

        int FreeSlot(int p, int w)
        {
            for (int k = 0; k < 2; k++) if (slots[p, w, k].Free) return k;
            return -1;
        }

        /// <summary>
        /// 外野ブロッカーの置き場所の中心。p = 持ち主、w = 側（0 = 画面の左 / 1 = 右）、k = 1 人目・2 人目。
        /// 台にある床の目印（「P1 Outfield Mark」「P2 Outfield Mark」）の上に出す。目印が見つからないときだけ outfieldX / outfieldZ から計算する
        /// </summary>
        public Vector3 SlotCenter(int p, int w, int k)
        {
            if (outfieldMarks == null) CacheOutfieldMarks();
            if (p >= 0 && p < 2 && w >= 0 && w < 2 && k >= 0 && k < 2 && outfieldMarkFound[p, w, k])
            {
                Vector3 m = outfieldMarks[p, w, k];
                return new Vector3(m.x, DBConst.BallY, m.z);
            }
            float wallSign = w == 0 ? -1f : 1f;
            float z = outfieldZ != null && k < outfieldZ.Length ? outfieldZ[k] : DBConst.OutfieldZ[Mathf.Min(k, 1)];
            return new Vector3(wallSign * outfieldX, DBConst.BallY, DBConst.Sign(p) * z);
        }

        // 外野の床の目印（ワールド座標）。[持ち主, 側（0 = 左 / 1 = 右）, 1 人目・2 人目]
        Vector3[,,] outfieldMarks;
        bool[,,] outfieldMarkFound;

        /// <summary>
        /// 床の目印を探して、外野の置き場所にする。持ち主は名前（P1 / P2 Outfield Mark）、側は X の符号、
        /// 1 人目・2 人目は中央の線に近い順（|Z| の小さい順）
        /// </summary>
        void CacheOutfieldMarks()
        {
            outfieldMarks = new Vector3[2, 2, 2];
            outfieldMarkFound = new bool[2, 2, 2];
            var found = new List<Vector3>[2, 2];
            for (int p = 0; p < 2; p++)
                for (int w = 0; w < 2; w++)
                    found[p, w] = new List<Vector3>();

            foreach (var tr in FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (tr == null || !tr.gameObject.activeInHierarchy) continue;
                int p = tr.name == "P1 Outfield Mark" ? 0 : tr.name == "P2 Outfield Mark" ? 1 : -1;
                if (p < 0) continue;
                Vector3 pos = tr.position;
                found[p, pos.x < 0f ? 0 : 1].Add(pos);
            }

            for (int p = 0; p < 2; p++)
                for (int w = 0; w < 2; w++)
                {
                    var list = found[p, w];
                    list.Sort(CompareAbsZ);
                    for (int k = 0; k < 2 && k < list.Count; k++)
                    {
                        outfieldMarks[p, w, k] = list[k];
                        outfieldMarkFound[p, w, k] = true;
                    }
                }
        }

        static int CompareAbsZ(Vector3 a, Vector3 b) => Mathf.Abs(a.z).CompareTo(Mathf.Abs(b.z));

        DBOutfieldBlocker CreateOutfield(int p, int wallSide, Vector3 center, int blockerIndex)
        {
            Material mat = playerMaterials != null && p < playerMaterials.Length ? playerMaterials[p] : null;
            var ob = DBOutfieldBlocker.Build("Outfield " + PlayerName(p) + (wallSide < 0 ? " L" : " R"),
                outfieldRoot != null ? outfieldRoot : transform, center, p, wallSide,
                DBConst.BlockerLength, DBConst.BlockerRadius, mat);
            ob.blockerIndex = blockerIndex;
            ob.autoThrow = outfieldControl == OutfieldControl.Auto || IsCpu(p);
            ob.autoThrowDelay = outfieldAutoThrowDelay;
            ob.buttonAutoThrowTime = outfieldButtonAutoThrowTime;
            ob.launchSpeed = outfieldLaunchSpeed * SpeedScale;
            foreach (var c in ob.GetComponentsInChildren<Collider>()) DBUtil.ApplyPhysics(c, 0.3f, 0f);
            return ob;
        }

        void ClearOutfields()
        {
            for (int p = 0; p < 2; p++)
                for (int w = 0; w < 2; w++)
                    for (int k = 0; k < 2; k++)
                    {
                        var s = slots[p, w, k];
                        if (s == null) continue;
                        if (s.outfield != null) Destroy(s.outfield.gameObject);
                        s.outfield = null;
                        s.reserved = false;
                    }
            if (ball != null) ball.LastOutfield = null;
        }

        public int OutfieldCount(int p)
        {
            int n = 0;
            for (int w = 0; w < 2; w++)
                for (int k = 0; k < 2; k++)
                    if (slots[p, w, k] != null && slots[p, w, k].outfield != null) n++;
            return n;
        }

        /// <summary>球を持っている外野（いなければ null）</summary>
        public DBOutfieldBlocker HoldingOutfield()
        {
            for (int p = 0; p < 2; p++)
                for (int w = 0; w < 2; w++)
                    for (int k = 0; k < 2; k++)
                    {
                        var o = slots[p, w, k] != null ? slots[p, w, k].outfield : null;
                        if (o != null && o.IsHolding) return o;
                    }
            return null;
        }

        /// <summary>持ち主の外野のどれかが球を持っているか</summary>
        public bool OutfieldHolding(int p)
        {
            for (int w = 0; w < 2; w++)
                for (int k = 0; k < 2; k++)
                {
                    var o = slots[p, w, k] != null ? slots[p, w, k].outfield : null;
                    if (o != null && o.IsHolding) return true;
                }
            return false;
        }

        // ---------------------------------------------------------------

        public void Post(string text, Color color, Vector3? world = null)
        {
            float t = Time.time;
            messages.Add(new Message
            {
                text = text,
                color = color,
                hasWorld = world.HasValue,
                world = world ?? Vector3.zero,
                start = t,
                until = t + messageTime,
            });
        }
    }
}
