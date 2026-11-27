using UnityEngine;

namespace Pinball
{
    /// <summary>
    /// ボール。対戦台は「中央が山」になっていて、ボールは今いる側のゴールへ向かって加速する。
    /// （Z &lt; 0 なら P1 のゴール側 -Z へ、Z &gt;= 0 なら P2 のゴール側 +Z へ）
    /// 打ち返したボールは中央まで減速し、越えると相手側へ加速していく。
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Ball : MonoBehaviour
    {
        [Tooltip("ゴール方向への加速度（台の傾きの強さ）")]
        public float slopeAcceleration = 9f;

        [Tooltip("速度の上限（すり抜け防止）")]
        public float maxSpeed = 35f;

        public Rigidbody Body { get; private set; }

        /// <summary>最後にこの球を打ったプレイヤー（フリッパー・ブロッカー・サーブ。-1 = まだ誰も）。サイドレールの判定に使う</summary>
        public int LastHitter { get; set; } = -1;
        public bool IsFrozen => Body.isKinematic;

        void Awake()
        {
            Body = GetComponent<Rigidbody>();
            Body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            Body.interpolation = RigidbodyInterpolation.Interpolate;
            Body.maxAngularVelocity = 100f;
        }

        void FixedUpdate()
        {
            if (Body.isKinematic) return;

            float dir = Body.position.z < 0f ? -1f : 1f;
            Body.AddForce(new Vector3(0f, 0f, dir * slopeAcceleration), ForceMode.Acceleration);

            Vector3 v = Body.GetVelocity();
            if (v.sqrMagnitude > maxSpeed * maxSpeed)
                Body.SetVelocity(v.normalized * maxSpeed);
        }

        /// <summary>止めて動かなくする（カウントダウン中など）。</summary>
        public void Freeze(bool freeze)
        {
            if (freeze == Body.isKinematic) return;

            if (freeze)
            {
                Body.SetVelocity(Vector3.zero);
                Body.angularVelocity = Vector3.zero;
                Body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                Body.isKinematic = true;
            }
            else
            {
                Body.isKinematic = false;
                Body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            }
        }

        public void ResetTo(Vector3 position)
        {
            if (!Body.isKinematic)
            {
                Body.SetVelocity(Vector3.zero);
                Body.angularVelocity = Vector3.zero;
            }
            Body.position = position;
            transform.position = position;
        }

        public void Launch(Vector3 velocity)
        {
            Freeze(false);
            Body.SetVelocity(velocity);
        }
    }
}
