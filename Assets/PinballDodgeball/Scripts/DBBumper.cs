using UnityEngine;

namespace PinballDodgeball
{
    /// <summary>壁際のバンパー。弾く向きは必ず内側へ（最低 minInward）</summary>
    public class DBBumper : MonoBehaviour
    {
        public float kickSpeed = 12f;
        [Range(0f, 1f)] public float minInward = 0.4f;
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
            float inward = -Mathf.Sign(transform.position.x);
            if (d.x * inward < minInward)
            {
                float zs = Mathf.Abs(d.z) < 1e-3f ? 1f : Mathf.Sign(d.z);
                d = new Vector3(inward * minInward, 0f, zs * Mathf.Sqrt(1f - minInward * minInward));
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
