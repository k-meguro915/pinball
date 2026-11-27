using System;
using UnityEngine;

namespace PinballDodgeball
{
    /// <summary>
    /// ジェットコースターのようなレーン。各プレイヤーに 1 本。ステップ19から、レーンは相手の陣地にある（入口も相手の陣地）。
    /// P1 は画面の左の帯、P2 は点対称で右の帯。path[0] → path[1] は、相手の陣地の入口の斜めの通路。
    /// 持ち主の内野の選手が投げた球（Ball.FromThrow）が、その通路の向きに入口を通ると乗る（2026-11-27。前はフリッパーで打った球）。
    /// 乗っている間は球を path に沿って運ぶ（高さも変わる。下るほど速い）。
    /// 終点（相手の陣地のゴール側の壁ぎわ）の射出装置で 0.7〜1 秒（ランダム）ためてから、相手の内野の選手へ横・後ろから撃ち出す。
    /// 撃った球は持ち主の外野をすり抜ける（Ball.FromLauncher）。
    /// レールは見た目だけ（当たり判定なし）。入口の判定はトリガー。
    /// 終点の射出装置には丸いドームの籠がかぶさる（台を作るときに組み立てる。見た目だけ）。籠の中のレールは描かない。
    /// </summary>
    public class DBCoaster : MonoBehaviour
    {
        public enum TargetMode { Random, Nearest }

        public int owner;

        [Header("通り道（ワールド座標。y は高さ）。台を作ると自動で入る")]
        public Vector3[] path = new Vector3[0];

        [Header("運ぶ速さ")]
        [Tooltip("いちばん高いところでの運ぶ速さ")]
        public float topSpeed = 7f;
        [Tooltip("高さ 1 下がるごとに足す速さ（下るほど速い）")]
        public float dropBoost = 9f;

        [Header("射出装置（終点）")]
        [Tooltip("ためる時間（この間のランダム）")]
        public float launchDelayMin = 0.7f;
        public float launchDelayMax = 1.0f;
        public float launchSpeed = 24f;
        [Tooltip("ねらう相手の内野の選手：ランダム／いちばん近い。ためている間に倒れたら、ほかの選手に切り替える")]
        public TargetMode targetMode = TargetMode.Random;
        [Tooltip("撃った球をパワーショット（軌跡）にする")]
        public bool powerShot = true;
        public Renderer launcherLamp;

        [Header("乗る条件")]
        [Tooltip("入口の通路の向き（path[0] → path[1]）へ向かう速さがこれ以上")]
        public float minEnterSpeed = 3f;
        [Tooltip("持ち主の球だけ乗る")]
        public bool ownerOnly = true;
        [Tooltip("持ち主の内野の選手が投げた球（Ball.FromThrow）だけ乗る。外野が投げた球・選手に当たった球は乗らない")]
        public bool throwsOnly = true;
        [Tooltip("撃ったあと、続けて乗らないまでの時間")]
        public float cooldown = 0.6f;

        [Header("見た目")]
        public float railWidth = 0.06f;
        public float railGap = 0.32f;
        [Tooltip("終点の籠の中（籠の半径の内側）はレールを描かない（レールの終わりの向きを隠す）")]
        public bool hideRailInCage = true;
        public float cageRadius = DBConst.CoasterCageRadius;

        public bool IsRiding { get; private set; }
        public bool IsCharging { get; private set; }
        public bool IsBusy => IsRiding || IsCharging;
        /// <summary>撃ち出すまでの残り（1 → 0）</summary>
        public float ChargeRemaining01 => IsCharging && chargeTotal > 0f ? Mathf.Clamp01((launchAt - Time.time) / chargeTotal) : 0f;
        /// <summary>撃ち出す位置（終点の床）</summary>
        public Vector3 LaunchPoint => path != null && path.Length > 0
            ? new Vector3(path[path.Length - 1].x, DBConst.BallY, path[path.Length - 1].z) : transform.position;
        /// <summary>いま撃ったときの向き</summary>
        public Vector3 AimDir => ComputeAimDir();

        public event Action<DBCoaster> Entered;
        public event Action<DBCoaster> Launched;

        float traveled, totalLength, readyAt, maxHeight;
        float launchAt, chargeTotal;
        int targetIndex = -1;
        float[] segLen;
        DBBall ridingBall;
        Material lampMat;
        Color lampBase;

        void Start()
        {
            ComputeLengths();
            BuildRails();
            if (launcherLamp != null)
            {
                lampMat = launcherLamp.material;
                lampBase = lampMat.color;
            }
        }

        void ComputeLengths()
        {
            int n = path != null ? path.Length : 0;
            segLen = new float[Mathf.Max(0, n - 1)];
            totalLength = 0f;
            maxHeight = 0f;
            for (int i = 0; i < n; i++) maxHeight = Mathf.Max(maxHeight, path[i].y);
            for (int i = 0; i < n - 1; i++)
            {
                segLen[i] = Vector3.Distance(path[i], path[i + 1]);
                totalLength += segLen[i];
            }
        }

        void OnTriggerEnter(Collider other)
        {
            if (IsBusy || Time.time < readyAt || other.attachedRigidbody == null) return;
            var ball = other.attachedRigidbody.GetComponent<DBBall>();
            var gm = DBGameManager.Instance;
            if (ball == null || ball.IsHeld || gm == null || gm.CurrentState != DBGameManager.State.Rally) return;
            if (ownerOnly && ball.LastHitter != owner) return;
            if (throwsOnly && !ball.FromThrow) return;
            if (path == null || path.Length < 2) return;
            Vector3 ramp = DBUtil.Flat(path[1] - path[0]);
            if (Vector3.Dot(ball.Velocity, ramp.normalized) < minEnterSpeed) return;

            IsRiding = true;
            ridingBall = ball;
            // 入口の通路のどこから入ったか（通路の向きに投影）。そこから運び始める
            float along = Vector3.Dot(DBUtil.Flat(ball.Position - path[0]), ramp.normalized);
            traveled = Mathf.Clamp(along, 0f, segLen.Length > 0 ? segLen[0] : 0f);
            ball.Hold(PointAt(traveled));
            ball.LastHitter = owner;
            Entered?.Invoke(this);
        }

        void FixedUpdate()
        {
            if (ridingBall == null) return;
            if (!ridingBall.IsHeld) { Cancel(); return; } // ほかの処理が球を放した（試合のやり直しなど）

            if (IsRiding)
            {
                float h = PointAt(traveled).y;
                float speed = topSpeed + Mathf.Max(0f, maxHeight - h) * dropBoost;
                traveled += speed * Time.fixedDeltaTime;
                if (traveled >= totalLength) StartCharge();
                else ridingBall.MoveHeldRaw(PointAt(traveled));
                return;
            }

            if (IsCharging)
            {
                ridingBall.MoveHeld(LaunchPoint);
                var gm = DBGameManager.Instance;
                if (gm != null && gm.CurrentState == DBGameManager.State.Rally && Time.time >= launchAt) Launch();
            }
        }

        void StartCharge()
        {
            IsRiding = false;
            IsCharging = true;
            chargeTotal = UnityEngine.Random.Range(launchDelayMin, Mathf.Max(launchDelayMin, launchDelayMax));
            launchAt = Time.time + chargeTotal;
            targetIndex = PickTarget(-1);
            ridingBall.Teleport(LaunchPoint);
        }

        void Launch()
        {
            var ball = ridingBall;
            Vector3 dir = ComputeAimDir();
            IsCharging = false;
            ridingBall = null;
            targetIndex = -1;
            readyAt = Time.time + cooldown;
            ball.LastHitter = owner;
            ball.LastOutfield = null;
            ball.Release(dir * launchSpeed);
            ball.FromLauncher = true; // 持ち主の外野をすり抜ける（LastHitter のあとに立てる）
            if (powerShot) ball.StartPowerShot();
            Launched?.Invoke(this);
        }

        /// <summary>試合のやり直し：乗っていたら降ろす（球は GameManager が置き直す）</summary>
        public void Cancel()
        {
            IsRiding = false;
            IsCharging = false;
            ridingBall = null;
            targetIndex = -1;
        }

        // ---- ねらい ----

        DBBlockerRow OpponentRow()
        {
            var gm = DBGameManager.Instance;
            return gm != null ? gm.rows[DBConst.Opponent(owner)] : null;
        }

        int PickTarget(int except)
        {
            var row = OpponentRow();
            if (row == null) return -1;
            Vector3 from = DBUtil.Flat(LaunchPoint);
            int pick = -1, seen = 0;
            float best = float.MaxValue;
            for (int i = 0; i < row.Count; i++)
            {
                if (!row.IsAlive(i) || i == except) continue;
                if (targetMode == TargetMode.Random)
                {
                    seen++;
                    if (UnityEngine.Random.Range(0, seen) == 0) pick = i; // 当てはまるものから 1 つをランダムに
                }
                else
                {
                    float d = (DBUtil.Flat(row.blockers[i].position) - from).magnitude;
                    if (d < best) { best = d; pick = i; }
                }
            }
            if (pick < 0 && except >= 0 && row.IsAlive(except)) pick = except;
            return pick;
        }

        /// <summary>ねらう選手へ。選手が倒れていたら選び直す。だれもいなければ相手の選手の範囲の真ん中へ。相手側へ少しでも進む向きにする</summary>
        Vector3 ComputeAimDir()
        {
            var row = OpponentRow();
            if (row != null && (targetIndex < 0 || targetIndex >= row.Count || !row.IsAlive(targetIndex)))
                targetIndex = PickTarget(-1);

            Vector3 from = DBUtil.Flat(LaunchPoint);
            Vector3 target = new Vector3(0f, 0f, DBConst.Sign(owner) * (DBConst.BlockerAreaNearZ + DBConst.BlockerAreaGoalZ) * 0.5f);
            if (row != null && targetIndex >= 0) target = DBUtil.Flat(row.blockers[targetIndex].position);

            Vector3 d = target - from;
            if (d.sqrMagnitude < 1e-4f) return new Vector3(0f, 0f, DBConst.Sign(owner));
            d = d.normalized;
            // 射出装置が自陣にあるときだけ、相手側へ少しでも進む向きにする（相手の陣地にあるときは、横・後ろからねらってよい）
            float minForward = 0.2f;
            bool inOwnTerritory = from.z * DBConst.Sign(owner) < 0f;
            if (inOwnTerritory && d.z * DBConst.Sign(owner) < minForward)
            {
                d.z = DBConst.Sign(owner) * minForward;
                d = d.normalized;
            }
            return d;
        }

        void Update()
        {
            if (lampMat == null) return;
            if (IsCharging)
            {
                float k = 0.5f + 0.5f * Mathf.Sin(Time.time * 20f);
                lampMat.color = Color.Lerp(lampBase, Color.white, k);
            }
            else lampMat.color = lampBase;
        }

        Vector3 PointAt(float d)
        {
            for (int i = 0; i < segLen.Length; i++)
            {
                if (d <= segLen[i] || i == segLen.Length - 1)
                    return Vector3.Lerp(path[i], path[i + 1], segLen[i] > 0f ? Mathf.Clamp01(d / segLen[i]) : 1f);
                d -= segLen[i];
            }
            return path[path.Length - 1];
        }

        // ---- レール（見た目のみ） ----

        void BuildRails()
        {
            if (path == null || path.Length < 2) return;
            var sh = Shader.Find("Sprites/Default");
            var mat = sh != null ? new Material(sh) : null;
            var gm = DBGameManager.Instance;
            Color c = gm != null ? gm.PlayerColor(owner) : Color.white;

            // 細かく分けた点（角を丸めて見せる）
            const int sub = 6;
            int n = (path.Length - 1) * sub + 1;
            var center = new Vector3[n];
            for (int i = 0; i < n; i++) center[i] = Smooth(i / (float)sub);

            // 終点の籠（BaseFix06）の中はレールを描かない：籠に入ったところ（1 点だけ中に入れる）で切る
            if (hideRailInCage && cageRadius > 0f)
            {
                Vector3 end = DBUtil.Flat(LaunchPoint);
                int last = n - 1;
                while (last > 1 && (DBUtil.Flat(center[last - 1]) - end).magnitude < cageRadius) last--;
                n = last + 1;
            }

            for (int r = -1; r <= 1; r += 2)
            {
                var go = new GameObject(r < 0 ? "Rail L" : "Rail R");
                go.transform.SetParent(transform, false);
                var lr = go.AddComponent<LineRenderer>();
                lr.useWorldSpace = true;
                lr.positionCount = n;
                lr.startWidth = railWidth;
                lr.endWidth = railWidth;
                lr.startColor = c;
                lr.endColor = Color.Lerp(c, Color.white, 0.5f);
                lr.numCornerVertices = 2;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                if (mat != null) lr.sharedMaterial = mat;
                for (int i = 0; i < n; i++)
                {
                    Vector3 fwd = center[Mathf.Min(n - 1, i + 1)] - center[Mathf.Max(0, i - 1)];
                    Vector3 side = new Vector3(-fwd.z, 0f, fwd.x).normalized;
                    lr.SetPosition(i, center[i] + side * (railGap * 0.5f * r) + Vector3.up * 0.1f);
                }
            }
        }

        /// <summary>折れ線を Catmull-Rom でなめらかにした点（t は 0〜path.Length-1）</summary>
        Vector3 Smooth(float t)
        {
            int i = Mathf.Clamp(Mathf.FloorToInt(t), 0, path.Length - 2);
            float u = t - i;
            Vector3 p0 = path[Mathf.Max(0, i - 1)], p1 = path[i], p2 = path[i + 1], p3 = path[Mathf.Min(path.Length - 1, i + 2)];
            float u2 = u * u, u3 = u2 * u;
            return 0.5f * (2f * p1 + (-p0 + p2) * u + (2f * p0 - 5f * p1 + 4f * p2 - p3) * u2 + (-p0 + 3f * p1 - 3f * p2 + p3) * u3);
        }
    }
}
