using UnityEngine;

namespace PinballDodgeball
{
    /// <summary>
    /// バンパー。当たった球を、バンパーの中心から外へはじく。
    /// ・壁ぎわのバンパー（useInwardRule = true）：はじく向きは必ずコートの内側へ（X の成分が最低 minInward）
    /// ・背面のバンパー（useInwardRule = false、2026-11-27）：はじく向きに、必ず biasDirection（中央の線の方）を minBias 以上入れる。
    ///   向かい合ったバンパーどうしで、球が行ったり来たり（ラリー）しないように
    /// </summary>
    public class DBBumper : MonoBehaviour
    {
        public float kickSpeed = 12f;
        [Tooltip("ON：壁ぎわのバンパー。はじく向きを、必ずコートの内側（X = 0 の方）へ向ける")]
        public bool useInwardRule = true;
        [Range(0f, 1f)] public float minInward = 0.4f;
        [Tooltip("useInwardRule が OFF のとき：はじく向きに必ず入れる向き（背面のバンパーは中央の線の方）")]
        public Vector3 biasDirection = Vector3.forward;
        [Range(0f, 1f)] public float minBias = 0.45f;
        public Renderer flashRenderer;
        public Color flashColor = Color.white;
        public float flashTime = 0.12f;

        Color baseColor;
        float flashUntil;
        Material mat;

        void Start()
        {
            if (flashRenderer != null)
            {
                mat = flashRenderer.material;
                baseColor = mat.color;
            }
        }

        void OnCollisionEnter(Collision c)
        {
            if (c.rigidbody == null) return;
            var ball = c.rigidbody.GetComponent<DBBall>();
            if (ball == null || ball.IsHeld) return;

            Vector3 d = DBUtil.Flat(ball.Position - transform.position).normalized;
            if (useInwardRule)
            {
                float inward = -Mathf.Sign(transform.position.x);
                if (d.x * inward < minInward)
                {
                    float zs = Mathf.Abs(d.z) < 1e-3f ? 1f : Mathf.Sign(d.z);
                    d = new Vector3(inward * minInward, 0f, zs * Mathf.Sqrt(1f - minInward * minInward));
                }
            }
            else
            {
                Vector3 b = DBUtil.Flat(biasDirection).normalized;
                if (Vector3.Dot(d, b) < minBias)
                {
                    // 向きの、b に直角な成分（横）は残し、b の成分を minBias にそろえる
                    Vector3 side = d - b * Vector3.Dot(d, b);
                    if (side.sqrMagnitude < 1e-6f) side = new Vector3(b.z, 0f, -b.x);
                    d = side.normalized * Mathf.Sqrt(1f - minBias * minBias) + b * minBias;
                }
            }
            float speed = Mathf.Max(kickSpeed, c.relativeVelocity.magnitude * 0.8f);
            ball.Body.SetVelocity(d * speed);
            flashUntil = Time.time + flashTime;
        }

        void Update()
        {
            if (mat == null) return;
            mat.color = Time.time < flashUntil ? flashColor : baseColor;
        }
    }
}
