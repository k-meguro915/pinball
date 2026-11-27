using UnityEngine;

namespace Pinball
{
    /// <summary>
    /// 自陣のブロッカーの「列」。子のブロッカー（今は2つ）をまとめて、決められた範囲の中で上下左右に動かす。
    /// 入力は PinballInput（画面基準：右 = +X、上 = +Z）。CPU 担当なら CpuPlayer が動かす。
    /// キネマティック Rigidbody を MovePosition で動かすので、当たったボールは押し返される。
    ///
    /// アクション（ラリー中のみ）
    ///   跳ね返し（BlockerBounce）：押した瞬間から少しの間、ブロッカーに触れた／近くの球を強く弾き返す
    ///   キャッチ（BlockerCatch）  ：押している間、触れた球がブロッカーにくっつく。離すと相手側へ発射
    ///                               （発射の向きは、離した瞬間の左右の入力で少し曲げられる）
    ///
    /// HP（ブロッカー1つずつ）
    ///   跳ね返し・キャッチを使わずに球が当たると HP が 1 減る（速さの条件つき・オンオフ可）
    ///   HP が 0 になると、そのブロッカーは downTime 秒すり抜ける（当たり判定なし・暗くなる）。戻ると HP は満タン
    ///   背面（自分のゴール側）にはガードがあり、後ろから当たっても HP は減らない（自分のフリッパーで打った球など）
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class BlockerRow : MonoBehaviour
    {
        [Tooltip("0 = P1, 1 = P2/CPU")]
        public int playerIndex = 0;

        [Tooltip("球の参照（台の生成で自動設定）")]
        public Ball ball;

        [Header("移動")]
        [Tooltip("動く速さ（ユニット/秒）")]
        public float speed = 6f;

        [Tooltip("入力に追いつくまでの速さ（大きいほどキビキビ動く）")]
        public float responsiveness = 20f;

        [Tooltip("移動範囲（列の中心が動ける範囲。ワールド座標の X / Z）")]
        public Vector2 minXZ = new Vector2(-1.75f, -4.75f);
        public Vector2 maxXZ = new Vector2(1.75f, -0.75f);

        [Tooltip("列に並んでいるブロッカー")]
        public Transform[] blockers;

        [Tooltip("ブロッカー（横向きカプセル）の長さと半径")]
        public float blockerLength = 1.0f;
        public float blockerRadius = 0.25f;

        [Header("跳ね返し")]
        [Tooltip("弾き返す速さ")]
        public float bounceSpeed = 15f;
        [Tooltip("押してから効いている時間（秒）")]
        public float bounceWindow = 0.15f;
        [Tooltip("次に使えるまでの時間（秒）")]
        public float bounceCooldown = 0.4f;
        [Tooltip("ブロッカーの表面からこの距離までの球を弾く")]
        public float bounceReach = 0.2f;
        [Tooltip("相手側へ向ける強さ（0 = 当たった向きのまま）")]
        [Range(0f, 1f)] public float bounceForwardBias = 0.4f;

        [Header("キャッチ → 発射")]
        [Tooltip("ブロッカーの表面からこの距離までの球をつかむ")]
        public float catchReach = 0.08f;
        [Tooltip("発射の速さ")]
        public float launchSpeed = 16f;
        [Tooltip("左右の入力で曲げられる最大の角度（度）")]
        public float maxAimAngle = 35f;
        [Tooltip("これより長く持つと自動で発射（秒）")]
        public float maxHoldTime = 2.5f;

        [Header("HP")]
        [Tooltip("ブロッカー1つあたりの HP")]
        public int maxHp = 3;
        [Tooltip("ON = 一定以上の速さで当たったときだけ HP が減る")]
        public bool useSpeedThreshold = true;
        [Tooltip("HP が減る速さ（ブロッカーの表面に向かう速さ）")]
        public float damageSpeed = 8f;
        [Tooltip("同じブロッカーが続けて減らない時間（秒）")]
        public float damageCooldown = 0.3f;
        [Tooltip("HP 0 で当たり判定がなくなる時間（秒）")]
        public float downTime = 4f;
        [Tooltip("ON = 背面（自分のゴール側）から当たった球では HP が減らない")]
        public bool backGuard = true;
        [Tooltip("ガードが守る範囲。0 = 背面の半分すべて、0.3 = 真後ろ寄りだけ（-1〜1）")]
        [Range(-1f, 1f)] public float guardCoverage = 0f;

        [Header("一方通行（自分の打った球はすり抜ける）")]
        [Tooltip("ON = 相手側へ向かって進む球はブロッカーをすり抜ける（自分のフリッパーで打った球が自陣のブロッカーに当たらない）。" +
                 "自分のゴールへ向かってくる球はこれまでどおり止める")]
        public bool passOwnShots = true;
        [Tooltip("この速さ以上で相手側へ進んでいる球をすり抜けさせる")]
        public float passMinForwardSpeed = 0.5f;
        [Tooltip("キャッチから発射した直後、この秒数は自分のブロッカーをすり抜け、HP も減らない")]
        public float launchGraceTime = 0.3f;

        [Header("HP の色")]
        public Color fullColor = new Color(0.25f, 0.6f, 1f);
        public Color hurtColor = new Color(1f, 0.85f, 0.25f);
        public Color dangerColor = new Color(1f, 0.3f, 0.25f);
        public Color downColor = new Color(0.15f, 0.15f, 0.2f);

        public Vector3 Home { get; private set; }
        public bool IsHolding => holding;
        /// <summary>キャッチのボタンを押していて、触れた球をつかめる状態（つかんでいる間は false）</summary>
        public bool IsCatchArmed => InRally && !holding && PinballInput.BlockerCatchHeld(playerIndex);
        /// <summary>相手のパワーショット中（球がすり抜け、跳ね返し・キャッチもできない）</summary>
        public bool IsPierced => PowerShot.Pierces(playerIndex);
        /// <summary>パワーショットにすり抜けられて壊れた（ブロッカー、場所）。表示用</summary>
        public static event System.Action<BlockerRow, Vector3> BrokenByPower;
        /// <summary>最後に球をつかんだ時刻（エフェクト用）</summary>
        public float CatchStartTime => catchStartTime;
        float catchStartTime = -10f;

        // ===== 外部 UI 向けの公開情報 =====
        /// <summary>跳ね返しが効いている（押してから bounceWindow 秒）</summary>
        public bool IsBounceActive => BounceActive;
        /// <summary>跳ね返しを使える（クールダウンが終わっている）</summary>
        public bool IsBounceReady => Time.time >= bounceReadyAt && !holding;
        /// <summary>キャッチしてからの経過（0〜1、1 で自動発射）</summary>
        public float HoldProgress => holding ? Mathf.Clamp01((Time.time - holdStart) / maxHoldTime) : 0f;
        /// <summary>今離したときに飛ぶ向き（左右の入力で曲がる）</summary>
        public Vector3 AimDirection
        {
            get
            {
                Vector2 input = PinballInput.Move(playerIndex);
                float angle = input.x * maxAimAngle * (playerIndex == 0 ? 1f : -1f);
                return Quaternion.Euler(0f, angle, 0f) * Forward;
            }
        }

        /// <summary>相手側の方向（P1 は +Z、P2 は -Z）</summary>
        public Vector3 Forward => playerIndex == 0 ? Vector3.forward : Vector3.back;

        Rigidbody rb;
        Vector3 velocity;

        bool bounceRequested;
        float bounceUntil = -1f;
        float bounceReadyAt;

        bool holding;
        float holdStart;
        Vector3 holdOffset;
        bool catchWasHeld;

        HitFlash[] flashes;

        Collider ballCollider;
        bool ignoringBall;
        float launchGraceUntil = -1f;

        /// <summary>今、球がこの列のブロッカーをすり抜ける状態か（表示用）</summary>
        public bool IsPassingBall => ignoringBall;

        /// <summary>ブロッカー1つ分の状態</summary>
        public class Piece
        {
            public Transform transform;
            public Collider collider;
            public HitFlash flash;
            public Renderer guard;    // 背面のガード（見た目）
            public int hp;
            public float downUntil;   // この時刻まですり抜け
            public float lastDamage = -10f;
            public bool IsDown => !collider.enabled;
        }

        Piece[] pieces = new Piece[0];
        public Piece[] Pieces => pieces;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            Home = transform.position;
            flashes = GetComponentsInChildren<HitFlash>();

            int n = blockers != null ? blockers.Length : 0;
            pieces = new Piece[n];
            for (int i = 0; i < n; i++)
            {
                pieces[i] = new Piece
                {
                    transform = blockers[i],
                    collider = blockers[i] != null ? blockers[i].GetComponent<Collider>() : null,
                    flash = blockers[i] != null ? blockers[i].GetComponent<HitFlash>() : null,
                    guard = blockers[i] != null ? FindGuard(blockers[i]) : null,
                    hp = maxHp,
                };
            }
        }

        void Start()
        {
            // HitFlash の Awake の後で色を合わせる
            foreach (var p in pieces) UpdatePieceColor(p);
        }

        /// <summary>試合開始時などに元の位置へ戻す</summary>
        public void ResetPosition()
        {
            velocity = Vector3.zero;
            rb.position = Home;
            transform.position = Home;
            holding = false;
            RestoreAll();
        }

        /// <summary>すべてのブロッカーを HP 満タン・当たり判定ありに戻す（試合開始・点が入った後）</summary>
        public void RestoreAll()
        {
            if (holding) Drop();
            holding = false;
            launchGraceUntil = -1f;
            bounceUntil = -1f;
            foreach (var p in pieces)
            {
                if (p.collider == null) continue;
                p.hp = maxHp;
                p.collider.enabled = true;
                p.downUntil = 0f;
                var r = p.transform.GetComponent<Renderer>();
                if (r != null) r.enabled = true;
                if (p.guard != null) p.guard.enabled = true;
                UpdatePieceColor(p);
            }

            // 当たり判定を有効にし直すと「すり抜け」の設定は消えるので、状態をそろえておく
            if (ball != null && ballCollider == null) ballCollider = ball.GetComponent<Collider>();
            if (ballCollider != null)
                foreach (var p in pieces)
                    if (p.collider != null) Physics.IgnoreCollision(ballCollider, p.collider, false);
            ignoringBall = false;
        }

        bool InRally => GameManager.Instance != null && GameManager.Instance.State == MatchState.Playing;
        bool BallUsable => ball != null && ball.isActiveAndEnabled;

        void Update()
        {
            // 「押した瞬間」は Update でしか正しく取れないので、ここで受けて FixedUpdate で処理する
            if (GameManager.Instance != null && GameManager.Instance.GuideBlocking) return; // 操作説明の操作を拾わない
            if (PinballInput.BlockerBouncePressed(playerIndex)) bounceRequested = true;
        }

        void FixedUpdate()
        {
            Move();
            UpdateDowns();

            if (!InRally || !BallUsable)
            {
                if (holding) Drop();
                bounceRequested = false;
                catchWasHeld = false;
                SetIgnoreBall(false);
                return;
            }

            UpdatePassThrough();
            UpdatePowerBreak();

            UpdateCatch();
            UpdateBounce();
        }

        void Move()
        {
            Vector2 input = PinballInput.Move(playerIndex);
            Vector3 targetVel = new Vector3(input.x, 0f, input.y) * speed;
            velocity = Vector3.MoveTowards(velocity, targetVel, responsiveness * speed * Time.fixedDeltaTime);

            Vector3 next = rb.position + velocity * Time.fixedDeltaTime;
            float cx = Mathf.Clamp(next.x, minXZ.x, maxXZ.x);
            float cz = Mathf.Clamp(next.z, minXZ.y, maxXZ.y);
            if (cx != next.x) velocity.x = 0f;
            if (cz != next.z) velocity.z = 0f;
            var pos = new Vector3(cx, rb.position.y, cz);
            rb.MovePosition(pos);

            // つかんでいる球も一緒に動かす
            if (holding && BallUsable) ball.Body.MovePosition(pos + holdOffset);
        }

        // ===================== キャッチ → 発射 =====================

        void UpdateCatch()
        {
            bool held = PinballInput.BlockerCatchHeld(playerIndex);

            if (holding)
            {
                bool released = catchWasHeld && !held;
                if (released || Time.time - holdStart > maxHoldTime) Launch();
            }
            else if (held && !ball.IsFrozen && !IsPierced)
            {
                if (TryGetContact(catchReach, out Vector3 surfacePoint, out Vector3 normal))
                {
                    // 球をブロッカーの表面にくっつける
                    holding = true;
                    ball.LastHitter = playerIndex;
                    catchStartTime = Time.time;
                    holdStart = Time.time;
                    ball.Freeze(true);
                    Vector3 ballPos = surfacePoint + normal * (BallRadius + 0.01f);
                    ballPos.y = ball.transform.position.y;
                    holdOffset = ballPos - rb.position;
                    ball.ResetTo(ballPos);
                    FlashAll();
                }
            }
            catchWasHeld = held;
        }

        void Launch()
        {
            holding = false;
            // 画面の左右の入力で向きを曲げる（P1 は右入力で右へ、P2 も画面基準で右へ）
            ball.Launch(AimDirection * launchSpeed);
            // 発射直後はつかんでいたブロッカー（と隣のブロッカー）をすり抜けさせ、HP も減らさない
            launchGraceUntil = Time.time + launchGraceTime;
            SetIgnoreBall(true);
            PinballAudio.Play(PinballSound.Launch);
            FlashAll();
            bounceReadyAt = Time.time + 0.2f; // 発射直後に跳ね返しが暴発しないように
        }

        /// <summary>試合の流れで球が消えたときなど、発射せずに放す</summary>
        void Drop()
        {
            holding = false;
            if (BallUsable && ball.IsFrozen && InRally) ball.Freeze(false);
        }

        // ===================== 跳ね返し =====================

        void UpdateBounce()
        {
            if (bounceRequested)
            {
                bounceRequested = false;
                if (!holding && Time.time >= bounceReadyAt)
                {
                    bounceUntil = Time.time + bounceWindow;
                    bounceReadyAt = Time.time + bounceCooldown;
                    FlashAll();
                }
            }

            if (Time.time > bounceUntil || holding || ball.IsFrozen || IsPierced) return;

            if (TryGetContact(bounceReach, out _, out Vector3 normal)) Kick(normal);
        }

        bool BounceActive => Time.time <= bounceUntil && !holding;

        void Kick(Vector3 normal)
        {
            ball.LastHitter = playerIndex;
            Vector3 dir = (normal + Forward * bounceForwardBias).normalized;
            Vector3 v = ball.Body.GetVelocity();
            float s = Mathf.Max(bounceSpeed, new Vector3(v.x, 0f, v.z).magnitude);
            ball.Body.SetVelocity(new Vector3(dir.x * s, v.y, dir.z * s));
            bounceUntil = -1f; // 1回押すと1回だけ
            PinballAudio.Play(PinballSound.Slingshot);
        }

        // ===================== HP =====================

        void OnCollisionEnter(Collision collision)
        {
            if (!InRally || !BallUsable || ball.IsFrozen) return;
            if (collision.collider.GetComponentInParent<Ball>() != ball) return;
            if (Time.time < launchGraceUntil) return; // キャッチから発射した直後は HP を減らさない

            var piece = FindPiece(collision.GetContact(0).thisCollider);
            if (piece == null || piece.IsDown) return;
            ball.LastHitter = playerIndex;

            // 跳ね返しが効いている → 跳ね返す（HP は減らない）
            if (BounceActive)
            {
                Vector3 n = ball.transform.position - piece.transform.position;
                n.y = 0f;
                Kick(n.sqrMagnitude > 0.0001f ? n.normalized : Forward);
                return;
            }

            // キャッチを押している → くっつく（HP は減らない。次の FixedUpdate でつかむ）
            if (PinballInput.BlockerCatchHeld(playerIndex)) return;

            // 背面のガードに当たった（自分のゴール側から来た球）→ HP は減らない
            if (backGuard && HitFromBehind(piece)) return;

            if (useSpeedThreshold)
            {
                float impact = Mathf.Abs(Vector3.Dot(collision.relativeVelocity, collision.GetContact(0).normal));
                if (impact < damageSpeed) return;
            }

            if (Time.time - piece.lastDamage < damageCooldown) return;
            piece.lastDamage = Time.time;
            Damage(piece);
        }

        /// <summary>球がブロッカーの背面側（自分のゴール側）にいるか</summary>
        bool HitFromBehind(Piece piece)
        {
            Vector3 d = ball.transform.position - piece.transform.position;
            d.y = 0f;
            if (d.sqrMagnitude < 0.0001f) return false;
            return Vector3.Dot(d.normalized, Forward) < -guardCoverage;
        }

        static Renderer FindGuard(Transform blocker)
        {
            var t = blocker.Find("Guard");
            return t != null ? t.GetComponent<Renderer>() : null;
        }

        Piece FindPiece(Collider c)
        {
            foreach (var p in pieces) if (p.collider == c) return p;
            return null;
        }

        void Damage(Piece p)
        {
            p.hp = Mathf.Max(0, p.hp - 1);
            if (p.flash != null) p.flash.Play();

            if (p.hp <= 0)
            {
                p.collider.enabled = false;  // すり抜ける
                p.downUntil = Time.time + downTime;
                PinballAudio.Play(PinballSound.Fault, 0.6f);
            }
            else
            {
                PinballAudio.Play(PinballSound.Bumper, 0.6f);
            }
            UpdatePieceColor(p);
        }

        void UpdateDowns()
        {
            foreach (var p in pieces)
            {
                if (p.collider == null || !p.IsDown) continue;

                // 最後の1秒は点滅して、もうすぐ戻ることを知らせる
                float left = p.downUntil - Time.time;
                var r = p.transform.GetComponent<Renderer>();
                bool visible = left > 1f || Mathf.Repeat(left, 0.2f) > 0.1f;
                if (r != null) r.enabled = visible;
                if (p.guard != null) p.guard.enabled = false; // ダウン中はガードも無し

                // 時間が来ても、球と重なっている間は戻さない（めり込み防止）
                if (left <= 0f && !OverlapsBall(p))
                {
                    p.hp = maxHp;
                    p.collider.enabled = true;
                    // 当たり判定を切ると「すり抜け」の設定が消えるので、すり抜け中ならかけ直す
                    if (ignoringBall && ballCollider != null) Physics.IgnoreCollision(ballCollider, p.collider, true);
                    if (r != null) r.enabled = true;
                    if (p.guard != null) p.guard.enabled = true;
                    UpdatePieceColor(p);
                }
            }
        }

        // ===================== 一方通行・発射直後のすり抜け =====================

        void UpdatePassThrough()
        {
            bool want = Time.time < launchGraceUntil || IsPierced; // 相手のパワーショットはすり抜ける

            if (passOwnShots && !ball.IsFrozen && !holding)
            {
                // 相手側へ向かって進んでいる球はすり抜ける（自分のフリッパーで打った球など）
                float forward = Vector3.Dot(ball.Body.GetVelocity(), Forward);
                if (forward > passMinForwardSpeed) want = true;
            }

            // すり抜け中に向きが変わっても、重なっている間は当たり判定を戻さない（めり込んで弾け飛ぶのを防ぐ）
            if (!want && ignoringBall && OverlapsAnyPiece()) want = true;

            SetIgnoreBall(want);
        }

        /// <summary>相手のパワーショットが重なったブロッカーは HP 0（ダウン）</summary>
        void UpdatePowerBreak()
        {
            if (!IsPierced || !PowerShot.BreaksBlockers) return;
            foreach (var p in pieces)
            {
                if (p.collider == null || p.transform == null || p.IsDown) continue;
                if (!OverlapsBall(p)) continue;
                p.hp = 1;
                Damage(p); // HP 0 → ダウン（音・光・色も通常と同じ）
                PinballAudio.Play(PinballSound.Bumper, 0.9f);
                BrokenByPower?.Invoke(this, p.transform.position);
            }
        }

        void SetIgnoreBall(bool ignore)
        {
            if (ignore == ignoringBall || ball == null) return;
            if (ballCollider == null) ballCollider = ball.GetComponent<Collider>();
            if (ballCollider == null) return;
            foreach (var p in pieces)
                if (p.collider != null) Physics.IgnoreCollision(ballCollider, p.collider, ignore);
            ignoringBall = ignore;
        }

        bool OverlapsAnyPiece()
        {
            foreach (var p in pieces)
                if (p.collider != null && p.transform != null && OverlapsBall(p)) return true;
            return false;
        }

        bool OverlapsBall(Piece p)
        {
            if (!BallUsable) return false;
            Vector3 c = p.transform.position;
            Vector3 bp = ball.transform.position;
            float half = Mathf.Max(0f, blockerLength / 2f - blockerRadius);
            var axis = new Vector3(Mathf.Clamp(bp.x, c.x - half, c.x + half), bp.y, c.z);
            Vector3 d = bp - axis;
            d.y = 0f;
            return d.magnitude < blockerRadius + BallRadius + 0.05f;
        }

        void UpdatePieceColor(Piece p)
        {
            if (p.flash == null || p.collider == null) return;
            Color c;
            if (p.IsDown) c = downColor;
            else if (p.hp >= maxHp) c = fullColor;
            else if (p.hp == 1) c = dangerColor;
            else c = hurtColor;
            p.flash.SetBaseColor(c);
        }

        // ===================== 判定 =====================

        float BallRadius => ball != null ? ball.transform.lossyScale.x * 0.5f : 0.25f;

        /// <summary>
        /// いずれかのブロッカー（横向きカプセル）の表面から reach 以内に球があるか。
        /// あれば、表面上のいちばん近い点と、そこから球へ向かう向き（水平）を返す。
        /// </summary>
        bool TryGetContact(float reach, out Vector3 surfacePoint, out Vector3 normal)
        {
            surfacePoint = Vector3.zero;
            normal = Vector3.zero;
            if (blockers == null) return false;

            Vector3 bp = ball.transform.position;
            float best = float.MaxValue;
            bool found = false;
            float half = Mathf.Max(0f, blockerLength / 2f - blockerRadius);

            foreach (var p in pieces)
            {
                if (p.transform == null || p.collider == null || p.IsDown) continue;
                Vector3 c = p.transform.position;
                // カプセルの軸（X 方向）上でいちばん近い点
                Vector3 axisPoint = new Vector3(Mathf.Clamp(bp.x, c.x - half, c.x + half), bp.y, c.z);
                Vector3 d = bp - axisPoint;
                d.y = 0f;
                float dist = d.magnitude;
                float gap = dist - blockerRadius - BallRadius;
                if (gap <= reach && gap < best)
                {
                    best = gap;
                    found = true;
                    normal = dist > 0.0001f ? d / dist : Forward;
                    surfacePoint = axisPoint + normal * blockerRadius;
                }
            }
            return found;
        }

        void FlashAll()
        {
            if (flashes == null) return;
            foreach (var f in flashes) if (f != null) f.Play();
        }

        void OnDrawGizmosSelected()
        {
            // 移動範囲（列の中心）を Scene ビューに表示
            Gizmos.color = Color.cyan;
            var c = new Vector3((minXZ.x + maxXZ.x) / 2f, transform.position.y, (minXZ.y + maxXZ.y) / 2f);
            var size = new Vector3(maxXZ.x - minXZ.x, 0.05f, maxXZ.y - minXZ.y);
            Gizmos.DrawWireCube(c, size);
        }
    }
}
