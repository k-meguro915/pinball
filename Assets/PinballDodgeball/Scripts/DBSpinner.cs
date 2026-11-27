using UnityEngine;

namespace PinballDodgeball
{
    /// <summary>
    /// スピナー（球が通り抜けると回る板）。中立地帯に 1 つ（ステップ19）。
    /// 速さ randomMinSpeed 以上で通った球は、向きを ±randomAngle° の中でランダムにぶらす。
    /// 速さ minBoostSpeed 以上なら、さらに boostMultiplier 倍（上限 maxBoostSpeed）に加速し、
    /// 持ち主がいる球ならパワーショットになる（軌跡が出る）。遅い球は板が少し回るだけ。
    /// 当たり判定はトリガーだけ（球の通り道はふさがない）。
    /// </summary>
    public class DBSpinner : MonoBehaviour
    {
        public float minBoostSpeed = 10f;
        public float boostMultiplier = 1.25f;
        public float maxBoostSpeed = 28f;
        [Tooltip("同じ球を続けて加速しないまでの時間")]
        public float cooldown = 0.4f;

        [Header("向きのランダム")]
        [Tooltip("通った球の向きを、この角度（±度）の中でランダムにぶらす。0 でぶらさない")]
        public float randomAngle = 15f;
        [Tooltip("これより遅い球はぶらさない")]
        public float randomMinSpeed = 3f;

        [Header("見た目")]
        [Tooltip("回る羽根（真上から見て回るように Y 軸まわりに回す）")]
        public Transform plate;
        [Tooltip("球の速さ 1 あたりの回る速さ（度/秒）")]
        public float spinPerSpeed = 120f;
        public float spinDecay = 2.5f;

        float spin;      // 度/秒
        float angle;
        float readyAt;

        void OnTriggerEnter(Collider other)
        {
            if (other.attachedRigidbody == null) return;
            var ball = other.attachedRigidbody.GetComponent<DBBall>();
            if (ball == null || ball.IsHeld) return;

            Vector3 v = ball.Body.GetVelocity();
            float s = v.magnitude;
            spin = Mathf.Max(spin, s * spinPerSpeed);

            var gm = DBGameManager.Instance;
            if (gm == null || !gm.IsPlaying || Time.time < readyAt || s < Mathf.Min(randomMinSpeed, minBoostSpeed)) return;
            readyAt = Time.time + cooldown;

            Vector3 dir = v / s;
            if (randomAngle > 0f && s >= randomMinSpeed)
                dir = Quaternion.Euler(0f, Random.Range(-randomAngle, randomAngle), 0f) * dir;
            bool boost = s >= minBoostSpeed;
            float ns = boost ? Mathf.Min(maxBoostSpeed, s * boostMultiplier) : s;
            ball.Body.SetVelocity(dir * ns);
            if (boost && ball.LastHitter >= 0)
            {
                ball.StartPowerShot();
                gm.Post("スピナー！", gm.PlayerColor(ball.LastHitter), transform.position);
            }
        }

        void Update()
        {
            if (plate == null) return;
            angle = (angle + spin * Time.deltaTime) % 360f;
            spin = Mathf.Max(0f, spin - spin * spinDecay * Time.deltaTime - 30f * Time.deltaTime);
            plate.localRotation = Quaternion.Euler(0f, angle, 0f);
        }
    }
}
