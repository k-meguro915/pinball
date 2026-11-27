using UnityEngine;

namespace PinballDodgeball
{
    /// <summary>
    /// 一方通行のふた（2026-11-27。背面の加速レーンの出口）。レーンの中から出る球は押し開けて通れる。外からは入れない。
    /// 当たり判定は板 1 枚。球が内側（allowed の向きの手前）にいる間だけ、球と板の当たりを切る。
    /// 見た目の板（flap）は、球が通るときに hinge を軸に開く。
    /// </summary>
    public class DBOneWayGate : MonoBehaviour
    {
        [Tooltip("通してよい向き（レーンの中 → 外）。台を作ると自動で入る")]
        public Vector3 allowed = Vector3.forward;
        public Collider gateCollider;
        [Tooltip("見た目の板の軸（開くときに Y まわりに回す）")]
        public Transform hinge;
        [Tooltip("開いたときの角度（hinge の向きで + か - か）")]
        public float openAngle = 75f;
        public float openSpeed = 720f;
        public float closeDelay = 0.25f;
        [Tooltip("球がふたからこの距離より近いと、開き始める")]
        public float openReach = 0.9f;

        float angle;
        float openUntil;
        bool ignoring;

        void FixedUpdate()
        {
            var gm = DBGameManager.Instance;
            if (gm == null || gm.ball == null || gm.ball.Col == null || gateCollider == null) return;
            var ball = gm.ball;
            Vector3 d = DBUtil.Flat(ball.Position - transform.position);
            Vector3 a = DBUtil.Flat(allowed).normalized;
            float along = Vector3.Dot(d, a);            // - = レーンの中、+ = 外
            bool inside = along < DBConst.BallRadius * 0.5f;
            if (inside != ignoring)
            {
                Physics.IgnoreCollision(ball.Col, gateCollider, inside);
                ignoring = inside;
            }
            if (!ball.IsHeld && inside && d.magnitude < openReach && Vector3.Dot(ball.Velocity, a) > 0.2f)
                openUntil = Time.time + closeDelay;
        }

        void Update()
        {
            if (hinge == null) return;
            float target = Time.time < openUntil ? openAngle : 0f;
            angle = Mathf.MoveTowards(angle, target, openSpeed * Time.deltaTime);
            hinge.localRotation = Quaternion.Euler(0f, angle, 0f);
        }
    }
}
