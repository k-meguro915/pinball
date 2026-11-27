using UnityEngine;

namespace Pinball
{
    /// <summary>
    /// ターンテーブル（床の回転する円盤）。上を通ったボールを回転の向きに引っぱり、進路を曲げる。
    /// 当たり判定（壁）は無いので、引っかかることはない。
    /// 点対称の台の中央に置くと、P1 / P2 どちらから見ても同じ効き方になる（公平）。
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class Turntable : MonoBehaviour
    {
        [Tooltip("円盤の半径")]
        public float radius = 1.3f;

        [Tooltip("回転の速さ（度/秒）。正 = 上から見て時計回り")]
        public float angularSpeed = 200f;

        [Tooltip("ボールを円盤の動きに合わせる強さ（大きいほど強く曲がる）")]
        public float grip = 5f;

        [Tooltip("この秒数ごとに回転の向きを逆にする（0 = 逆にしない）")]
        public float reverseInterval = 0f;

        [Tooltip("回して見せる部分")]
        public Transform visual;

        float direction = 1f;
        float timer;

        void Update()
        {
            if (reverseInterval > 0f)
            {
                timer += Time.deltaTime;
                if (timer >= reverseInterval)
                {
                    timer = 0f;
                    direction = -direction;
                }
            }

            if (visual != null)
                visual.Rotate(0f, angularSpeed * direction * Time.deltaTime, 0f, Space.World);
        }

        void OnTriggerEnter(Collider other)
        {
            var ball = other.GetComponentInParent<Ball>();
            if (ball == null || ball.IsFrozen) return;
            PowerShot.AddCharge(ball, ChargeSource.Turntable, this);
        }

        void OnTriggerStay(Collider other)
        {
            var ball = other.GetComponentInParent<Ball>();
            if (ball == null || ball.IsFrozen) return;

            Vector3 r = ball.transform.position - transform.position;
            r.y = 0f;
            if (r.sqrMagnitude > radius * radius) return;

            // 円盤のその場所の速さ（Y軸まわりの回転 ω × r）
            float w = angularSpeed * Mathf.Deg2Rad * direction;
            var surface = new Vector3(w * r.z, 0f, -w * r.x);

            Vector3 v = ball.Body.GetVelocity();
            Vector3 diff = surface - new Vector3(v.x, 0f, v.z);
            ball.Body.AddForce(diff * grip, ForceMode.Acceleration);
        }
    }
}
