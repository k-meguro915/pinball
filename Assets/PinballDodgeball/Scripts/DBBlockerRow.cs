using System;
using UnityEngine;

namespace PinballDodgeball
{
    /// <summary>
    /// 内野のブロッカー（選手）3 人の列。HP 1：相手の球に当たるとアウト（外野から戻るまで復活しない）。
    /// できる動きはキャッチだけ（B を押している間に触れた球をつかむ、離すと投げる、2.5 秒で自動）。
    /// キャッチの構えは B を押し始めてから maxCatchHoldTime（3 秒）まで。過ぎたら押し直す。
    /// ジャストキャッチ：B を押し始めてから justCatchFrames（60fps 換算）以内につかむと、次に投げる球がパワーショット（速い球）になる。
    /// 跳ね返し・背面ガード・一方通行はない。味方の投げた球ではアウトにならない（当たって跳ね返るだけ）。
    /// ただし自分のフリッパーで打った球（Ball.FromFlipper）は、味方の選手をすり抜ける。
    /// 動ける範囲は、残っている選手だけで決める（いない選手の分ははみ出してよい）。
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class DBBlockerRow : MonoBehaviour
    {
        public int owner;

        [Header("動き")]
        public float moveSpeed = 7f;
        public Vector3 startPosition;
        [Tooltip("ブロッカーの範囲（外形）")]
        public float areaHalfX = DBConst.BlockerAreaHalfX;
        public float areaNearZ = DBConst.BlockerAreaNearZ;
        public float areaGoalZ = DBConst.BlockerAreaGoalZ;

        [Tooltip("外野から戻った選手が範囲の外にはみ出したとき、範囲へ戻る速さ")]
        public float reenterSpeed = 6f;
        [Tooltip("自分のフリッパーで打った球は、味方の選手をすり抜ける")]
        public bool passOwnFlipperShots = true;

        [Header("当たり（HP 1）")]
        [Tooltip("相手の球がこの速さ以上で当たるとアウト")]
        public float knockSpeed = 3f;

        [Header("キャッチ（B を押している間）")]
        public float catchRange = 0.05f;
        [Tooltip("キャッチ入力の最大秒数：B を押し始めてからこの秒数を過ぎると、押したままでもキャッチの構えが解ける（もう一度押し直すと構える）。0 で制限なし")]
        public float maxCatchHoldTime = 3f;
        public float launchSpeed = 16f;
        public float maxLaunchAngle = 35f;
        public float autoLaunchTime = 2.5f;
        public float launchGraceTime = 0.3f;

        [Header("ジャストキャッチ → パワーショット")]
        [Tooltip("B を押し始めてから、このフレーム数以内（60fps 換算）につかむとジャストキャッチ")]
        public int justCatchFrames = 3;
        [Tooltip("フレーム数を秒に直すときの基準")]
        public float justCatchFrameRate = 60f;
        [Tooltip("パワーショットの速さ（ふつうの投げる速さは launchSpeed）")]
        public float powerShotSpeed = 26f;

        [Header("復活")]
        [Tooltip("戻る場所からこの距離に球がある間は、戻るのを待つ")]
        public float reviveClearance = 0.9f;

        [Header("部品（台を作ると自動で入る）")]
        public Transform[] blockers;
        public float blockerLength = DBConst.BlockerLength;
        public float blockerRadius = DBConst.BlockerRadius;

        public event Action<DBBlockerRow, int, DBBall> Knocked;
        public event Action<DBBlockerRow, int> Revived;
        public event Action<DBBlockerRow, int> JustCaught;
        public event Action<DBBlockerRow, int> PowerShotThrown;

        public Vector3 Forward => new Vector3(0f, 0f, DBConst.Sign(owner));
        public int Count => blockers != null ? blockers.Length : 0;
        public bool IsAlive(int i) => alive[i];
        public bool IsPendingRevive(int i) => pending[i];
        public bool IsCatchArmed { get; private set; }
        /// <summary>キャッチの構えが解けるまでの残り（1 → 0）。構えていないときは 0</summary>
        public float CatchHoldRemaining01 => IsCatchArmed && maxCatchHoldTime > 0f
            ? Mathf.Clamp01(1f - (Time.time - catchPressTime) / maxCatchHoldTime) : 0f;
        public bool IsCatching => caughtIndex >= 0;
        /// <summary>いま持っている球がジャストキャッチか（離すとパワーショット）</summary>
        public bool IsJustCatch => caughtIndex >= 0 && justCaught;
        public float JustCatchWindow => justCatchFrames / Mathf.Max(1f, justCatchFrameRate);
        /// <summary>いま離したときに投げる向き（スティックの左右で決まる）</summary>
        public Vector3 AimDir => LaunchDir(gm != null ? gm.GetInput(owner).move.x : 0f);
        /// <summary>動ける範囲（外形）の X の半分と、Z の 2 つの端（ワールド座標）</summary>
        public void GetArea(out float halfX, out float z1, out float z2)
        {
            float zs = -DBConst.Sign(owner);
            halfX = areaHalfX;
            z1 = zs * areaNearZ;
            z2 = zs * areaGoalZ;
        }
        public int CaughtIndex => caughtIndex;
        public float CatchRemaining01 => IsCatching ? Mathf.Clamp01(1f - (Time.time - caughtAt) / autoLaunchTime) : 0f;
        public Vector3 Center => rb != null ? rb.position : transform.position;

        public int AliveCount
        {
            get { int n = 0; for (int i = 0; i < Count; i++) if (alive[i]) n++; return n; }
        }
        public int PendingCount
        {
            get { int n = 0; for (int i = 0; i < Count; i++) if (pending[i]) n++; return n; }
        }
        /// <summary>まだ選手が残っているか（戻るのを待っている選手も数える）</summary>
        public bool HasPlayers => AliveCount + PendingCount > 0;

        Rigidbody rb;
        Collider[] cols;
        Renderer[] rends;
        bool[] alive, pending, ignoring;
        float[] graceUntil;
        int caughtIndex = -1;
        float caughtAt;
        bool justCaught;
        float catchPressTime = -10f;
        DBGameManager gm;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            int n = Count;
            cols = new Collider[n];
            rends = new Renderer[n];
            alive = new bool[n];
            pending = new bool[n];
            ignoring = new bool[n];
            graceUntil = new float[n];
            for (int i = 0; i < n; i++)
            {
                cols[i] = blockers[i].GetComponent<Collider>();
                rends[i] = blockers[i].GetComponent<Renderer>();
                alive[i] = true;
            }
        }

        public void Bind(DBGameManager manager) => gm = manager;

        /// <summary>試合開始：全員復活、元の位置へ</summary>
        public void ResetAll()
        {
            caughtIndex = -1;
            justCaught = false;
            IsCatchArmed = false;
            catchPressTime = -10f;
            for (int i = 0; i < Count; i++)
            {
                pending[i] = false;
                graceUntil[i] = 0f;
                SetAlive(i, true);
            }
            rb.position = startPosition;
            transform.position = startPosition;
        }

        /// <summary>外野から戻る。球が離れたら戻る</summary>
        public void RequestRevive(int i)
        {
            if (i < 0 || i >= Count || alive[i]) return;
            pending[i] = true;
        }

        void SetAlive(int i, bool on)
        {
            alive[i] = on;
            if (cols[i] != null) cols[i].enabled = on;
            if (rends[i] != null) rends[i].enabled = on;
            // 当たり判定を有効にし直すと IgnoreCollision が消えるので、状態をそろえ直す
            if (on && gm != null && gm.ball != null && gm.ball.Col != null && cols[i] != null)
                Physics.IgnoreCollision(gm.ball.Col, cols[i], false);
            ignoring[i] = false;
        }

        void Update()
        {
            if (gm == null) return;
            var st = gm.GetInput(owner);
            if (st.bDown) catchPressTime = Time.time;
            bool withinHold = maxCatchHoldTime <= 0f || Time.time - catchPressTime < maxCatchHoldTime;
            IsCatchArmed = st.b && withinHold && gm.CurrentState == DBGameManager.State.Rally && caughtIndex < 0;
        }

        void FixedUpdate()
        {
            if (gm == null || gm.ball == null) return;
            var ball = gm.ball;
            var st = gm.GetInput(owner);
            float t = Time.time;
            float dt = Time.fixedDeltaTime;

            // 動き
            Vector2 mv = gm.IsPlaying ? st.move : Vector2.zero;
            Vector3 desired = rb.position + new Vector3(mv.x, 0f, mv.y) * moveSpeed * dt;
            Vector3 clamped = ClampToArea(desired);
            // ふつうはそのまま範囲に収める。外野から戻った選手で大きくはみ出したときは、いきなり飛ばさずに範囲へ戻す
            Vector3 pos = (desired - clamped).magnitude <= moveSpeed * dt * 1.5f + 1e-4f
                ? clamped
                : Vector3.MoveTowards(desired, clamped, reenterSpeed * dt);
            rb.MovePosition(pos);

            // キャッチ中
            if (caughtIndex >= 0)
            {
                ball.MoveHeld(HoldPoint(caughtIndex, pos));
                bool rally = gm.CurrentState == DBGameManager.State.Rally;
                if (!st.b || t - caughtAt >= autoLaunchTime || !rally) Launch(st.move.x);
            }

            Vector3 bp = DBUtil.Flat(ball.Position);
            float halfSeg = Mathf.Max(0f, blockerLength * 0.5f - blockerRadius);

            for (int i = 0; i < Count; i++)
            {
                Vector3 center = DBUtil.Flat(pos + blockers[i].localPosition);
                if (!alive[i])
                {
                    if (pending[i] && (bp - center).magnitude > reviveClearance)
                    {
                        pending[i] = false;
                        SetAlive(i, true);
                        graceUntil[i] = t + 0.2f;
                        Revived?.Invoke(this, i);
                    }
                    continue;
                }

                Vector3 a = center - Vector3.right * halfSeg;
                Vector3 b = center + Vector3.right * halfSeg;
                float dist = (bp - DBUtil.ClosestOnSegment(a, b, bp)).magnitude - blockerRadius - DBConst.BallRadius;

                if (!ball.IsHeld && t >= graceUntil[i] && IsCatchArmed && dist <= catchRange) Catch(i, pos);

                // すり抜け：発射直後・復活直後、自分のフリッパーで打った球（重なっている間は続ける）
                bool ownFlipper = passOwnFlipperShots && !ball.IsHeld && ball.FromFlipper && ball.LastHitter == owner;
                bool overlap = dist < 0.02f;
                SetIgnore(i, t < graceUntil[i] || ownFlipper || (ignoring[i] && overlap));
            }
        }

        void SetIgnore(int i, bool ign)
        {
            if (ign == ignoring[i] || cols[i] == null || gm.ball.Col == null) return;
            Physics.IgnoreCollision(gm.ball.Col, cols[i], ign);
            ignoring[i] = ign;
        }

        /// <summary>
        /// 列の中心が動ける X の範囲。残っている選手（と、戻るのを待っている選手）が範囲の外形からはみ出さないように決める。
        /// 誰も残っていないときは、全員の分で決める
        /// </summary>
        public void GetCenterRangeX(out float minX, out float maxX)
        {
            float minL = float.MaxValue, maxL = float.MinValue;
            for (int i = 0; i < Count; i++)
            {
                if (alive != null && !alive[i]) continue;
                float lx = blockers[i].localPosition.x;
                minL = Mathf.Min(minL, lx);
                maxL = Mathf.Max(maxL, lx);
            }
            if (minL > maxL)
            {
                for (int i = 0; i < Count; i++)
                {
                    float lx = blockers[i].localPosition.x;
                    minL = Mathf.Min(minL, lx);
                    maxL = Mathf.Max(maxL, lx);
                }
            }
            float half = blockerLength * 0.5f;
            minX = -areaHalfX + half - minL;
            maxX = areaHalfX - half - maxL;
            if (minX > maxX) { float m = (minX + maxX) * 0.5f; minX = maxX = m; }
        }

        Vector3 ClampToArea(Vector3 p)
        {
            GetCenterRangeX(out float minX, out float maxX);
            float zs = -DBConst.Sign(owner); // 自陣の Z の符号
            float z1 = zs * (areaNearZ + blockerRadius);
            float z2 = zs * (areaGoalZ - blockerRadius);
            p.x = Mathf.Clamp(p.x, minX, maxX);
            p.z = Mathf.Clamp(p.z, Mathf.Min(z1, z2), Mathf.Max(z1, z2));
            p.y = startPosition.y;
            return p;
        }

        Vector3 HoldPoint(int i, Vector3 rowPos)
        {
            return rowPos + blockers[i].localPosition + Forward * (blockerRadius + DBConst.BallRadius + 0.03f);
        }

        void Catch(int i, Vector3 rowPos)
        {
            var ball = gm.ball;
            caughtIndex = i;
            caughtAt = Time.time;
            IsCatchArmed = false;
            // FixedUpdate の時刻はフレームの時刻より少し前になることがあるので、負の差は 0 とみなす
            float sincePress = Mathf.Max(0f, Time.time - catchPressTime);
            justCaught = sincePress <= JustCatchWindow + 1e-4f;
            ball.Hold(HoldPoint(i, rowPos));
            ball.LastHitter = owner;
            ball.LastOutfield = null;
            if (justCaught) JustCaught?.Invoke(this, i);
        }

        void Launch(float aimX)
        {
            var ball = gm.ball;
            int i = caughtIndex;
            caughtIndex = -1;
            var dir = LaunchDir(aimX);
            graceUntil[i] = Time.time + launchGraceTime;
            SetIgnore(i, true);
            bool power = justCaught;
            justCaught = false;
            ball.LastHitter = owner;
            ball.LastOutfield = null;
            if (gm.CurrentState == DBGameManager.State.Rally)
            {
                ball.Release(dir * (power ? powerShotSpeed : launchSpeed));
                if (power)
                {
                    ball.StartPowerShot();
                    PowerShotThrown?.Invoke(this, i);
                }
            }
        }

        Vector3 LaunchDir(float aimX)
        {
            float a = Mathf.Clamp(aimX, -1f, 1f) * maxLaunchAngle * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a) * DBConst.Sign(owner));
        }

        // 子のコライダーに当たったときの OnCollisionEnter は、Rigidbody を持つこの列（親）に届く。
        // どの選手に当たったかは、接触点の thisCollider で調べる。
        void OnCollisionEnter(Collision c)
        {
            if (cols == null || c.contactCount == 0) return;
            Collider hit = c.GetContact(0).thisCollider;
            for (int i = 0; i < Count; i++)
            {
                if (cols[i] == hit)
                {
                    OnContact(i, c);
                    return;
                }
            }
        }

        /// <summary>ブロッカー i に球が当たった</summary>
        void OnContact(int i, Collision c)
        {
            if (gm == null || gm.ball == null || c.rigidbody == null) return;
            var ball = gm.ball;
            if (c.rigidbody.gameObject != ball.gameObject) return;
            if (!alive[i] || Time.time < graceUntil[i]) return;

            if (caughtIndex < 0 && IsCatchArmed)
            {
                Catch(i, Center);
                return;
            }

            // 相手の球ならアウト。味方の球・誰の球でもない球ではアウトにならない
            if (ball.LastHitter == DBConst.Opponent(owner) && gm.CanKnock && c.relativeVelocity.magnitude >= knockSpeed)
            {
                SetAlive(i, false);
                Knocked?.Invoke(this, i, ball);
                return;
            }
            // アウトにならずに当たった：自分の球になる
            ball.LastHitter = owner;
            ball.LastOutfield = null;
        }
    }
}
