using System;
using UnityEngine;

namespace PinballDodgeball
{
    /// <summary>
    /// 自陣のフリッパー。キネマティック Rigidbody ＋ MoveRotation。restYaw ↔ activeYaw の間を回る。
    /// 球に触れると、球の LastHitter を持ち主にする（外野が投げた球の LastOutfield は消える）。
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class DBFlipper : MonoBehaviour
    {
        public int owner;
        [Tooltip("画面の左（X < 0）= -1、右 = +1。ボタン：左 = FlipperTop、右 = FlipperBottom")]
        public int screenSide = -1;

        [Header("角度（Y 回転）")]
        public float restYaw;
        public float activeYaw;
        public float swingSpeed = 1500f;
        public float returnSpeed = 900f;

        [Header("形")]
        public float length = DBConst.FlipperLength;
        public float radius = DBConst.FlipperRadius;

        [Header("自動で振る（CPU）")]
        public bool autoSwing;
        [Tooltip("フリッパーの表面からこの距離まで球が来たら振る")]
        public float autoReach = 0.5f;
        [Tooltip("球が来てから振るまでの遅れ（秒）")]
        public float autoReaction = 0f;
        [Range(0f, 1f)] public float autoMissChance = 0f;
        public float autoHoldTime = 0.2f;
        public float autoCooldown = 0.25f;

        /// <summary>ボタンで動かすときの入力（GameManager が入れる）</summary>
        [NonSerialized] public Func<bool> pressed;

        public Vector3 Pivot => rb != null ? rb.position : transform.position;
        public Vector3 RestTip => Pivot + Quaternion.Euler(0f, restYaw, 0f) * Vector3.forward * length;

        Rigidbody rb;
        float yaw;
        float autoUntil = -1f, autoReady;
        bool inZone, missThisTime;
        float zoneEnterTime;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            ResetPose();
        }

        public void ResetPose()
        {
            if (rb == null) rb = GetComponent<Rigidbody>();
            yaw = restYaw;
            var q = Quaternion.Euler(0f, yaw, 0f);
            transform.rotation = q;
            if (rb != null && Application.isPlaying) rb.rotation = q;
            autoUntil = -1f;
            inZone = false;
        }

        void FixedUpdate()
        {
            bool want = false;
            if (autoSwing) want = AutoDecide();
            else if (pressed != null) want = pressed();
            float target = want ? activeYaw : restYaw;
            yaw = Mathf.MoveTowardsAngle(yaw, target, (want ? swingSpeed : returnSpeed) * Time.fixedDeltaTime);
            rb.MoveRotation(Quaternion.Euler(0f, yaw, 0f));
        }

        bool AutoDecide()
        {
            var gm = DBGameManager.Instance;
            if (gm == null || gm.ball == null || !gm.IsPlaying) return false;
            float t = Time.time;
            if (t < autoUntil) return true;

            var ball = gm.ball;
            bool zone = false;
            if (!ball.IsHeld)
            {
                Vector3 p = DBUtil.Flat(ball.Position);
                Vector3 c = DBUtil.ClosestOnSegment(DBUtil.Flat(Pivot), DBUtil.Flat(RestTip), p);
                float dist = (p - c).magnitude - radius - DBConst.BallRadius;
                // 振る向きの側にいる球だけ
                Vector3 sweep = Quaternion.Euler(0f, activeYaw, 0f) * Vector3.forward
                              - Quaternion.Euler(0f, restYaw, 0f) * Vector3.forward;
                zone = dist <= autoReach && Vector3.Dot(p - c, sweep) > -0.05f;
            }

            if (zone && !inZone)
            {
                zoneEnterTime = t;
                missThisTime = UnityEngine.Random.value < autoMissChance;
            }
            inZone = zone;

            if (!zone || missThisTime || t < autoReady || t < zoneEnterTime + autoReaction) return false;
            autoUntil = t + autoHoldTime;
            autoReady = autoUntil + autoCooldown;
            return true;
        }

        void OnCollisionEnter(Collision c) => Touch(c);
        void OnCollisionStay(Collision c) => Touch(c);

        void Touch(Collision c)
        {
            if (c.rigidbody == null) return;
            var ball = c.rigidbody.GetComponent<DBBall>();
            if (ball == null) return;
            ball.LastHitter = owner;
            ball.LastOutfield = null;
            ball.FromFlipper = true; // 味方の選手をすり抜ける
        }

        /// <summary>フリッパーを作る（台のツールで使う）</summary>
        public static DBFlipper Build(string name, Transform parent, Vector3 pivot, int owner, int screenSide,
            float restYaw, float activeYaw, float length, float radius, Material mat)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.position = new Vector3(pivot.x, DBConst.BallY, pivot.z);
            root.transform.rotation = Quaternion.Euler(0f, restYaw, 0f);

            // 本体（カプセル。Y 向きのカプセルを X に 90° 倒して Z 向きに）
            DBUtil.Primitive(PrimitiveType.Capsule, "Body", root.transform,
                new Vector3(0f, 0f, length * 0.5f), Quaternion.Euler(90f, 0f, 0f),
                new Vector3(radius * 2f, (length + radius * 2f) * 0.5f, radius * 2f), mat, true);
            // 付け根の飾り（当たり判定なし）
            DBUtil.Primitive(PrimitiveType.Cylinder, "Pivot", root.transform,
                new Vector3(0f, 0.05f, 0f), Quaternion.identity,
                new Vector3(radius * 1.4f, 0.22f, radius * 1.4f), mat, false);

            var rb = root.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            var f = root.AddComponent<DBFlipper>();
            f.owner = owner;
            f.screenSide = screenSide;
            f.length = length;
            f.radius = radius;
            f.restYaw = restYaw;
            f.activeYaw = activeYaw;
            f.ResetPose();
            return f;
        }

        /// <summary>自陣のフリッパーの向き（φ：内側の水平から相手側へ正）</summary>
        public static float MainYaw(int player, int screenSide, float angleDeg)
        {
            float inward = -screenSide;
            float a = angleDeg * Mathf.Deg2Rad;
            var dir = new Vector3(inward * Mathf.Cos(a), 0f, DBConst.Sign(player) * Mathf.Sin(a));
            return DBUtil.YawOf(dir);
        }
    }
}
