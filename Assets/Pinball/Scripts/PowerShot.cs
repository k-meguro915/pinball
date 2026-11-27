using System;
using UnityEngine;

namespace Pinball
{
    /// <summary>
    /// パワーショット。
    ///   自分が最後に打った球がバンパー・ターンテーブル・サイドレール・ロールオーバーに当たると、自分のゲージが溜まる。
    ///   （スリングショットはステップ28で廃止。ロールオーバーは同じ陣地の左右を全部点灯させるとボーナス）
    ///   ゲージが満タンのとき、自分のフリッパーで相手側へ打ち出した球が「パワーショット」になる（自動で発動）。
    ///   パワーショットは速くなり、相手のフリッパーへ向かって曲がっていく（誘導）。
    ///   相手のブロッカーはすり抜け、すり抜けたブロッカーは HP が 0 になる（跳ね返し・キャッチもされない）。
    ///   相手のフリッパーに当たったとき：
    ///     振り上げた瞬間（タイミングが合った）なら打ち返される（パワーショットは終わり）
    ///     構えているだけ・下げているだけなら、球は跳ね返るが、そのフリッパーは stunTime 秒（2 秒）下がったまま使えなくなる
    ///   点が入る・効果時間が切れると終わる。
    /// GameManager と同じオブジェクトに自動で付く（無ければ）。数値は Inspector で変更できる。
    /// </summary>
    public enum ChargeSource { Bumper, Slingshot, Turntable, Rail, Rollover, RolloverBonus }

    public class PowerShot : MonoBehaviour
    {
        public static PowerShot Instance { get; private set; }

        [Header("ゲージ")]
        [Tooltip("満タンになる量")]
        public float maxCharge = 6f;
        [Tooltip("バンパーに当たったとき")]
        public float bumperCharge = 1f;
        [Tooltip("ロールオーバーを通ったとき")]
        public float rolloverCharge = 0.5f;
        [Tooltip("同じ陣地のロールオーバーを全部点灯させたときのボーナス")]
        public float rolloverBonus = 2f;
        [Tooltip("スリングショットに当たったとき（ステップ28で台から廃止。置いた場合だけ使う）")]
        public float slingshotCharge = 1f;
        [Tooltip("ターンテーブルに乗ったとき（1回の通過につき）")]
        public float turntableCharge = 0.5f;
        [Tooltip("サイドレールに乗ったとき")]
        public float railCharge = 2f;
        [Tooltip("同じギミックで続けて溜まらない時間（秒）")]
        public float sameSourceCooldown = 0.25f;
        [Tooltip("ON = 点が入ってもゲージを残す（試合開始で 0 に戻る）")]
        public bool keepChargeOnPoint = true;

        [Header("発動")]
        [Tooltip("フリッパーに触れてからこの秒数のうちに、相手側へこの速さ以上で飛び出したら発動")]
        public float flipperWindow = 0.15f;
        public float fireMinForwardSpeed = 6f;
        [Tooltip("発動したときの速さ = 今の速さ × この倍率（下限 minPowerSpeed、上限は球の maxSpeed）")]
        public float speedMultiplier = 1.35f;
        public float minPowerSpeed = 22f;
        [Tooltip("効果が続く最大の時間（秒）")]
        public float duration = 4f;

        [Header("誘導（相手のフリッパーへ曲がる）")]
        public bool homing = true;
        [Tooltip("1 秒あたりに曲がれる角度（度）。大きいほど強く曲がる")]
        public float homingTurnRate = 90f;
        [Tooltip("ねらうフリッパーの「当たる面」をどこにするか（0 = 付け根、1 = 先端）")]
        [Range(0f, 1f)] public float homingAimAlong = 0.55f;

        [Header("相手のブロッカー")]
        [Tooltip("ON = すり抜けたブロッカーの HP を 0 にする（ダウンさせる）")]
        public bool breakBlockers = true;

        [Header("相手のフリッパーに当たったとき")]
        [Tooltip("ON = 振り上げた瞬間に当てたときだけ打ち返せる（デメリットなし）。何もせずに受けるとフリッパーが使えなくなる")]
        public bool counterBySwing = true;
        [Tooltip("何もせずに受けたフリッパーが使えなくなる時間（秒）")]
        public float stunTime = 2.0f;

        // 古い設定の引き継ぎ用（台の作り直しで前の値が残るため、仕様が変わった値だけ直す）
        [SerializeField, HideInInspector] int dataVersion = 0; // 保存されていない古いデータは 0 として読まれる

        void Migrate()
        {
            if (dataVersion < 2)
            {
                stunTime = 2f; // ステップ26：押し負け（1 秒すり抜け）→ 2 秒使用不可
                dataVersion = 2;
            }
        }

        void OnValidate() { Migrate(); }

        [Header("見た目（球の軌跡）")]
        public bool showTrail = true;
        public float trailTime = 0.25f;
        public float trailWidth = 0.45f;

        /// <summary>ゲージが溜まった（プレイヤー、量、場所）。表示用</summary>
        public static event Action<int, float, Vector3> Charged;
        /// <summary>パワーショットが発動した（プレイヤー、場所）</summary>
        public static event Action<int, Vector3> Fired;
        /// <summary>相手のフリッパーを押し負かした（フリッパー）</summary>
        public static event Action<Flipper> FlipperBroken;
        /// <summary>相手のフリッパーに打ち返された（フリッパー）</summary>
        public static event Action<Flipper> Countered;

        readonly float[] charge = new float[2];
        readonly float[] lastFlipperTouch = { -10f, -10f };
        readonly float[] lastChargeTime = { -10f, -10f };
        readonly UnityEngine.Object[] lastSource = new UnityEngine.Object[2];

        int activeOwner = -1;
        float activeUntil;
        float firedTime = -10f;

        GameManager gm;
        TrailRenderer trail;
        MatchState lastState = MatchState.Title;

        // ===== 表示用 =====
        public float Charge(int p) => charge[Mathf.Clamp(p, 0, 1)];
        public float Charge01(int p) => maxCharge > 0f ? Mathf.Clamp01(Charge(p) / maxCharge) : 0f;
        public bool IsReady(int p) => Charge(p) >= maxCharge - 0.0001f;
        /// <summary>今パワーショット中か</summary>
        public bool IsActive => activeOwner >= 0;
        /// <summary>パワーショット中の持ち主（-1 = なし）</summary>
        public int ActiveOwner => activeOwner;
        public float FiredTime => firedTime;

        /// <summary>すり抜けたブロッカーを壊す（HP 0）か</summary>
        public static bool BreaksBlockers => Instance != null && Instance.breakBlockers;
        /// <summary>誘導でねらっている相手のフリッパー（無ければ null）</summary>
        public Flipper HomingTarget => homingTarget;

        /// <summary>player のブロッカーを、今の球がすり抜けるべきか（相手のパワーショット中）</summary>
        public static bool Pierces(int blockerOwner) =>
            Instance != null && Instance.isActiveAndEnabled && Instance.activeOwner >= 0 && Instance.activeOwner != blockerOwner;

        // ===== ギミックから呼ぶ =====

        public static void AddCharge(Ball ball, ChargeSource kind, UnityEngine.Object source)
        {
            if (Instance == null || !Instance.isActiveAndEnabled || ball == null) return;
            Instance.Add(ball, Instance.AmountOf(kind), source);
        }

        /// <summary>球ではなく人を指定して溜める（ロールオーバーのボーナスなど）</summary>
        public static void AddBonus(int player, ChargeSource kind, Vector3 pos)
        {
            if (Instance == null || !Instance.isActiveAndEnabled) return;
            Instance.AddTo(player, Instance.AmountOf(kind), pos);
        }

        float AmountOf(ChargeSource kind)
        {
            switch (kind)
            {
                case ChargeSource.Bumper: return bumperCharge;
                case ChargeSource.Slingshot: return slingshotCharge;
                case ChargeSource.Turntable: return turntableCharge;
                case ChargeSource.Rail: return railCharge;
                case ChargeSource.Rollover: return rolloverCharge;
                case ChargeSource.RolloverBonus: return rolloverBonus;
                default: return 0f;
            }
        }

        /// <summary>フリッパーが球に触れた（OnCollisionEnter / Stay）</summary>
        public static void NotifyFlipperTouch(Flipper flipper, Ball ball)
        {
            if (Instance == null || flipper == null) return;
            int player = flipper.playerIndex;
            if (player < 0 || player > 1) return;
            Instance.lastFlipperTouch[player] = Time.time;
            if (Instance.activeOwner >= 0 && Instance.activeOwner != player) Instance.HitOpponentFlipper(flipper, ball);
        }

        void HitOpponentFlipper(Flipper flipper, Ball ball)
        {
            if (counterBySwing && !flipper.IsSwinging)
            {
                // 何もせずに受けた（構えているだけ・下げているだけ）：球は普通に当たって跳ね返るが、
                // そのフリッパーは stunTime 秒、下がったまま使えなくなる
                flipper.Stun(stunTime);
                ball.LastHitter = activeOwner; // 打ち返したわけではないので、打ったのは攻めた側のまま
                PinballAudio.Play(PinballSound.Fault, 0.8f);
                FlipperBroken?.Invoke(flipper);
                End();
                return;
            }

            // 振り上げて打ち返した → デメリットなし。パワーショットは終わり
            Countered?.Invoke(flipper);
            End();
        }

        public void ResetAll()
        {
            charge[0] = charge[1] = 0f;
            End();
        }

        // ===================== 内部 =====================

        void Awake()
        {
            Migrate();
            Instance = this;
            gm = GetComponent<GameManager>();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void OnDisable()
        {
            End();
        }

        bool InRally => gm != null && gm.State == MatchState.Playing;

        void Add(Ball ball, float amount, UnityEngine.Object source)
        {
            if (!InRally || amount <= 0f) return;
            int p = ball.LastHitter;
            if (p < 0 || p > 1) return;
            if (activeOwner == p) return; // パワーショット中は溜まらない
            if (source != null && lastSource[p] == source && Time.time - lastChargeTime[p] < sameSourceCooldown) return;

            bool wasReady = IsReady(p);
            charge[p] = Mathf.Min(maxCharge, charge[p] + amount);
            lastSource[p] = source;
            lastChargeTime[p] = Time.time;
            Charged?.Invoke(p, amount, ball.transform.position);

            if (!wasReady && IsReady(p)) PinballAudio.Play(PinballSound.Goal, 0.5f);
        }

        void AddTo(int p, float amount, Vector3 pos)
        {
            if (!InRally || amount <= 0f || p < 0 || p > 1) return;
            if (activeOwner == p) return;
            bool wasReady = IsReady(p);
            charge[p] = Mathf.Min(maxCharge, charge[p] + amount);
            Charged?.Invoke(p, amount, pos);
            if (!wasReady && IsReady(p)) PinballAudio.Play(PinballSound.Goal, 0.5f);
        }

        void Update()
        {
            if (gm == null) gm = GameManager.Instance;
            if (gm == null) return;

            // 試合開始（タイトル → サーブ）でゲージを 0 に
            if (lastState == MatchState.Title && gm.State != MatchState.Title) ResetAll();
            if (gm.State == MatchState.Point && lastState != MatchState.Point)
            {
                End();
                if (!keepChargeOnPoint) charge[0] = charge[1] = 0f;
            }
            lastState = gm.State;

            if (activeOwner >= 0 && (!InRally || Time.time > activeUntil)) End();

        }


        void FixedUpdate()
        {
            if (!InRally || gm.ball == null || !gm.ball.isActiveAndEnabled || gm.ball.IsFrozen) return;
            var ball = gm.ball;
            EnsureTrail(ball);
            if (activeOwner >= 0) UpdateHoming(ball);

            if (activeOwner >= 0) return;

            for (int p = 0; p < 2; p++)
            {
                if (!IsReady(p) || ball.LastHitter != p) continue;
                if (Time.time - lastFlipperTouch[p] > flipperWindow) continue;

                Vector3 forward = p == 0 ? Vector3.forward : Vector3.back;
                Vector3 v = ball.Body.GetVelocity();
                if (Vector3.Dot(v, forward) < fireMinForwardSpeed) continue;

                Fire(p, ball, v);
                break;
            }
        }

        // ===================== 誘導 =====================

        Flipper homingTarget;
        Flipper[] flippers = new Flipper[0];

        Vector3 AimPoint(Flipper f)
        {
            // 下げた位置での「付け根 → 先端」の途中をねらう
            float ang = f.restAngle * Mathf.Deg2Rad;
            var dir = new Vector3(Mathf.Cos(ang), 0f, -Mathf.Sin(ang));
            return f.transform.position + dir * (f.length * homingAimAlong);
        }

        Flipper PickTarget(int owner, Vector3 pos, Vector3 vel)
        {
            if (flippers.Length == 0 || flippers[0] == null)
            {
#if UNITY_2023_1_OR_NEWER
                flippers = FindObjectsByType<Flipper>(FindObjectsSortMode.None);
#else
                flippers = FindObjectsOfType<Flipper>();
#endif
            }
            Flipper best = null;
            float bestDot = -2f;
            Vector3 v = new Vector3(vel.x, 0f, vel.z).normalized;
            foreach (var f in flippers)
            {
                if (f == null || f.playerIndex == owner) continue;
                Vector3 to = AimPoint(f) - pos; to.y = 0f;
                float d = Vector3.Dot(v, to.normalized);
                if (d > bestDot) { bestDot = d; best = f; }
            }
            return best;
        }

        void UpdateHoming(Ball ball)
        {
            if (!homing || homingTarget == null) return;
            Vector3 pos = ball.transform.position;
            Vector3 to = AimPoint(homingTarget) - pos; to.y = 0f;
            Vector3 forward = activeOwner == 0 ? Vector3.forward : Vector3.back;
            // フリッパーを通り過ぎたら誘導は終わり
            if (Vector3.Dot(to, forward) < 0.2f) { homingTarget = null; return; }

            Vector3 v = ball.Body.GetVelocity();
            Vector3 flat = new Vector3(v.x, 0f, v.z);
            float speed = flat.magnitude;
            if (speed < 0.5f) return;
            Vector3 turned = Vector3.RotateTowards(flat, to.normalized * speed, homingTurnRate * Mathf.Deg2Rad * Time.fixedDeltaTime, 0f);
            ball.Body.SetVelocity(new Vector3(turned.x, v.y, turned.z));
        }

        void Fire(int p, Ball ball, Vector3 v)
        {
            Vector3 flat = new Vector3(v.x, 0f, v.z);
            float speed = Mathf.Clamp(flat.magnitude * speedMultiplier, minPowerSpeed, ball.maxSpeed);
            Vector3 dir = flat.normalized;
            ball.Body.SetVelocity(new Vector3(dir.x * speed, v.y, dir.z * speed));

            charge[p] = 0f;
            activeOwner = p;
            activeUntil = Time.time + duration;
            firedTime = Time.time;
            homingTarget = homing ? PickTarget(p, ball.transform.position, ball.Body.GetVelocity()) : null;

            if (trail != null && showTrail)
            {
                Color c = GameManager.PlayerColor(p);
                trail.startColor = new Color(c.r, c.g, c.b, 0.9f);
                trail.endColor = new Color(c.r, c.g, c.b, 0f);
                trail.Clear();
                trail.emitting = true;
            }
            PinballAudio.Play(PinballSound.Launch, 1f);
            PinballAudio.Play(PinballSound.Slingshot, 0.8f);
            Fired?.Invoke(p, ball.transform.position);
        }

        void End()
        {
            activeOwner = -1;
            homingTarget = null;
            if (trail != null) trail.emitting = false;
        }

        void EnsureTrail(Ball ball)
        {
            if (trail != null || !showTrail) return;
            trail = ball.GetComponent<TrailRenderer>();
            if (trail == null) trail = ball.gameObject.AddComponent<TrailRenderer>();
            trail.time = trailTime;
            trail.minVertexDistance = 0.05f;
            trail.widthCurve = new AnimationCurve(new Keyframe(0f, trailWidth), new Keyframe(1f, 0f));
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.alignment = LineAlignment.View;
            var shader = Shader.Find("Sprites/Default");
            if (shader != null) trail.material = new Material(shader);
            trail.emitting = false;
        }
    }
}
