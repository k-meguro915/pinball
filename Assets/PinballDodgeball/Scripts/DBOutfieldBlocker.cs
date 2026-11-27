using UnityEngine;

namespace PinballDodgeball
{
    /// <summary>
    /// 外野ブロッカー。相手陣地の左右、選手の範囲のすぐ外に出る（ステップ19。前は壁ぎわ）。動かない・アウトにならない。
    /// 触れた球はどれでも自動でキャッチする（自分の内野の球・相手の球・相手の外野の球）。
    /// ただしサーブの球と、持ち主のコースターの射出装置が撃った球はすり抜ける。
    /// 投げ方：
    ///   全自動（autoThrow）：autoThrowDelay 秒後に、相手の残っている選手のうちいちばん近い選手をねらって投げる
    ///   ボタン：持ち主が B で投げる（buttonAutoThrowTime 秒で自動）。向きはスティックの上下（画面の向き、最大 maxLaunchAngle）
    /// 投げた球は LastOutfield = この外野。その球で相手をアウトにすると、この外野は元の選手に戻る。
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class DBOutfieldBlocker : MonoBehaviour
    {
        public int owner;
        [Tooltip("いる側：画面の左（X < 0）= -1、右 = +1。キャッチした球は中央側に持つ")]
        public int wallSide = -1;
        [Tooltip("元になった内野の選手の番号")]
        public int blockerIndex = -1;

        [Header("投げ方")]
        public bool autoThrow = true;
        public float autoThrowDelay = 1f;
        [Tooltip("全自動のとき、内側の向きからこの角度までねらいを曲げる")]
        public float maxAutoAimAngle = 70f;
        public float buttonAutoThrowTime = 2.5f;
        public float launchSpeed = 16f;
        public float maxLaunchAngle = 35f;
        public float launchGraceTime = 0.3f;

        [Header("キャッチ")]
        public float catchRange = 0.05f;

        [Header("形")]
        public float length = DBConst.BlockerLength;
        public float radius = DBConst.BlockerRadius;

        public bool IsHolding { get; private set; }
        public bool Operational => scaleDir == 0 && !despawning;
        public Vector3 Center => transform.position;
        public Vector3 Inward => new Vector3(-wallSide, 0f, 0f);
        /// <summary>いま投げたときの向き</summary>
        public Vector3 AimDir
        {
            get
            {
                var gm = DBGameManager.Instance;
                if (gm == null) return Inward;
                return autoThrow ? AutoAimDir(gm) : StickAimDir(gm.GetInput(owner).move);
            }
        }
        public float HoldRemaining01 => IsHolding
            ? Mathf.Clamp01(1f - (Time.time - caughtAt) / (autoThrow ? autoThrowDelay : buttonAutoThrowTime)) : 0f;

        Collider col;
        float caughtAt, graceUntil;
        bool ignoring, throwRequested, despawning;
        int scaleDir;
        float scaleT = 1f;
        const float ScaleTime = 0.25f;

        void Awake()
        {
            var rb = GetComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            col = GetComponentInChildren<Collider>(true);
        }

        void Update()
        {
            if (scaleDir != 0)
            {
                scaleT += scaleDir * Time.deltaTime / ScaleTime;
                if (scaleDir > 0 && scaleT >= 1f)
                {
                    scaleT = 1f;
                    scaleDir = 0;
                    if (col != null) col.enabled = true;
                    ignoring = false; // 当たり判定を有効にし直すと IgnoreCollision が消えるので、次の FixedUpdate でそろえ直す
                }
                else if (scaleDir < 0 && scaleT <= 0f)
                {
                    Destroy(gameObject);
                    return;
                }
                transform.localScale = Vector3.one * Mathf.Max(0.01f, scaleT);
            }

            var gm = DBGameManager.Instance;
            if (gm != null && IsHolding && !autoThrow && gm.GetInput(owner).bDown) throwRequested = true;
        }

        void FixedUpdate()
        {
            var gm = DBGameManager.Instance;
            if (gm == null || gm.ball == null) return;
            var ball = gm.ball;
            float t = Time.time;

            bool pass = PassesThrough(ball);
            bool wantIgnore = pass || t < graceUntil;
            if (wantIgnore != ignoring && col != null && col.enabled) SetIgnore(ball, wantIgnore);

            if (IsHolding)
            {
                ball.MoveHeld(HoldPoint());
                if (gm.CurrentState != DBGameManager.State.Rally) return;
                if (autoThrow)
                {
                    if (t - caughtAt >= autoThrowDelay) Throw(gm, AutoAimDir(gm));
                }
                else if (throwRequested || t - caughtAt >= buttonAutoThrowTime)
                {
                    Throw(gm, StickAimDir(gm.GetInput(owner).move));
                }
                return;
            }

            if (!Operational || ball.IsHeld || !gm.IsPlaying || t < graceUntil || pass) return;
            if (Distance(ball) <= catchRange) Catch(ball);
        }

        void OnCollisionEnter(Collision c)
        {
            var gm = DBGameManager.Instance;
            if (gm == null || gm.ball == null || c.rigidbody == null) return;
            if (c.rigidbody.gameObject != gm.ball.gameObject) return;
            if (!Operational || IsHolding || gm.ball.IsHeld || !gm.IsPlaying || Time.time < graceUntil || PassesThrough(gm.ball)) return;
            Catch(gm.ball);
        }

        /// <summary>
        /// この外野がすり抜ける球（キャッチしない）：
        /// サーブの球（選手に触れるまで）、持ち主のコースターの射出装置が撃った球（自分の外野が取ってしまわないように）
        /// </summary>
        bool PassesThrough(DBBall ball)
        {
            if (ball == null || ball.IsHeld) return false;
            return ball.FromServe || (ball.FromLauncher && ball.LastHitter == owner);
        }

        float Distance(DBBall ball)
        {
            float half = Mathf.Max(0f, length * 0.5f - radius);
            Vector3 c = DBUtil.Flat(Center);
            Vector3 p = DBUtil.Flat(ball.Position);
            Vector3 q = DBUtil.ClosestOnSegment(c - Vector3.forward * half, c + Vector3.forward * half, p);
            return (p - q).magnitude - radius - DBConst.BallRadius;
        }

        Vector3 HoldPoint() => Center + Inward * (radius + DBConst.BallRadius + 0.03f);

        void Catch(DBBall ball)
        {
            IsHolding = true;
            caughtAt = Time.time;
            throwRequested = false;
            ball.Hold(HoldPoint());
            ball.LastHitter = owner;
            ball.LastOutfield = null;
        }

        void Throw(DBGameManager gm, Vector3 dir)
        {
            var ball = gm.ball;
            IsHolding = false;
            throwRequested = false;
            graceUntil = Time.time + launchGraceTime;
            SetIgnore(ball, true);
            ball.Release(dir * launchSpeed);
            ball.LastHitter = owner;
            ball.LastOutfield = this;
        }

        /// <summary>スティックの上下でねらう（画面の上 = +Z）</summary>
        Vector3 StickAimDir(Vector2 stick)
        {
            float a = Mathf.Clamp(stick.y, -1f, 1f) * maxLaunchAngle * Mathf.Deg2Rad;
            return (Inward * Mathf.Cos(a) + Vector3.forward * Mathf.Sin(a)).normalized;
        }

        /// <summary>相手の残っている選手のうち、いちばん近い選手へ</summary>
        Vector3 AutoAimDir(DBGameManager gm)
        {
            var row = gm.rows[DBConst.Opponent(owner)];
            Vector3 from = DBUtil.Flat(HoldPoint());
            Vector3 best = Vector3.zero;
            float bestD = float.MaxValue;
            if (row != null)
            {
                for (int i = 0; i < row.Count; i++)
                {
                    if (!row.IsAlive(i)) continue;
                    Vector3 d = DBUtil.Flat(row.blockers[i].position) - from;
                    if (d.magnitude < bestD) { bestD = d.magnitude; best = d; }
                }
            }
            if (bestD == float.MaxValue || best.sqrMagnitude < 1e-4f) return Inward;
            Vector3 dir = best.normalized;
            // 壁の外側へは投げない
            float ang = Mathf.Atan2(dir.z, Vector3.Dot(dir, Inward)) * Mathf.Rad2Deg;
            ang = Mathf.Clamp(ang, -maxAutoAimAngle, maxAutoAimAngle) * Mathf.Deg2Rad;
            return (Inward * Mathf.Cos(ang) + Vector3.forward * Mathf.Sin(ang)).normalized;
        }

        void SetIgnore(DBBall ball, bool on)
        {
            if (col != null && ball.Col != null) Physics.IgnoreCollision(ball.Col, col, on);
            ignoring = on;
        }

        // ---- 出る・消える ----

        public void PlaySpawn()
        {
            scaleDir = 1;
            scaleT = 0f;
            if (col != null) col.enabled = false;
            transform.localScale = Vector3.one * 0.01f;
        }

        public void Despawn()
        {
            despawning = true;
            scaleDir = -1;
            if (col != null) col.enabled = false;
            var gm = DBGameManager.Instance;
            if (gm != null && gm.ball != null && gm.ball.LastOutfield == this) gm.ball.LastOutfield = null;
        }

        /// <summary>外野ブロッカーを作る（壁に沿った Z 向きのカプセル）</summary>
        public static DBOutfieldBlocker Build(string name, Transform parent, Vector3 center, int owner, int wallSide,
            float length, float radius, Material mat)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.position = new Vector3(center.x, DBConst.BallY, center.z);

            // Y 向きのカプセルを X に 90° 倒して Z 向きに
            DBUtil.Primitive(PrimitiveType.Capsule, "Body", root.transform, Vector3.zero, Quaternion.Euler(90f, 0f, 0f),
                new Vector3(radius * 2f, length * 0.5f, radius * 2f), mat, true);
            // キャッチする側（内側）の目印（当たり判定なし）
            DBUtil.Primitive(PrimitiveType.Cube, "Front", root.transform,
                new Vector3(-wallSide * radius * 0.6f, radius * 0.9f, 0f), Quaternion.identity,
                new Vector3(0.08f, 0.04f, length * 0.6f), null, false);

            var rb = root.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            var ob = root.AddComponent<DBOutfieldBlocker>();
            ob.owner = owner;
            ob.wallSide = wallSide;
            ob.length = length;
            ob.radius = radius;
            return ob;
        }
    }
}
