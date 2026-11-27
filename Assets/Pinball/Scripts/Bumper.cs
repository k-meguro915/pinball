using UnityEngine;

namespace Pinball
{
    /// <summary>
    /// バンパー。当たったボールを外向きに弾いて光る。
    /// </summary>
    public class Bumper : MonoBehaviour
    {
        public float kickSpeed = 13f;

        [Tooltip("壁際のバンパー用：弾く向きを必ずこの向き（盤面の内側）へ寄せる。ゼロなら補正しない")]
        public Vector3 inwardDirection = Vector3.zero;
        [Tooltip("内側への向きの最低限（0〜1。0.4 なら最低でも約 24° は内側へ向ける）")]
        [Range(0f, 1f)] public float minInward = 0.4f;
        public Color flashColor = Color.white;
        public float flashTime = 0.1f;

        Renderer rend;
        MaterialPropertyBlock mpb;
        Color baseColor = Color.red;
        float flashTimer;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        void Awake()
        {
            rend = GetComponent<Renderer>();
            mpb = new MaterialPropertyBlock();
            if (rend != null && rend.sharedMaterial != null)
            {
                var m = rend.sharedMaterial;
                if (m.HasProperty(BaseColorId)) baseColor = m.GetColor(BaseColorId);
                else if (m.HasProperty(ColorId)) baseColor = m.GetColor(ColorId);
            }
        }

        void OnCollisionEnter(Collision collision)
        {
            var ball = collision.collider.GetComponentInParent<Ball>();
            if (ball == null || ball.IsFrozen) return;

            Vector3 dir = ball.transform.position - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;
            dir.Normalize();

            // 壁際のバンパー：壁へ向かって弾くと壁との間で往復し続けるので、必ず内側へ向ける
            if (inwardDirection.sqrMagnitude > 0.0001f)
            {
                Vector3 inward = new Vector3(inwardDirection.x, 0f, inwardDirection.z).normalized;
                float d = Vector3.Dot(dir, inward);
                if (d < minInward)
                {
                    Vector3 along = dir - inward * d;
                    if (along.sqrMagnitude < 0.0001f) along = Vector3.Cross(Vector3.up, inward);
                    along.Normalize();
                    dir = (inward * minInward + along * Mathf.Sqrt(1f - minInward * minInward)).normalized;
                }
            }

            Vector3 v = ball.Body.GetVelocity();
            float speed = Mathf.Max(kickSpeed, new Vector3(v.x, 0f, v.z).magnitude);
            ball.Body.SetVelocity(new Vector3(dir.x * speed, v.y, dir.z * speed));

            flashTimer = flashTime;
            PinballAudio.Play(PinballSound.Bumper);
            PowerShot.AddCharge(ball, ChargeSource.Bumper, this);
        }

        void Update()
        {
            if (rend == null) return;
            if (flashTimer > 0f) flashTimer -= Time.deltaTime;

            Color c = flashTimer > 0f ? flashColor : baseColor;
            rend.GetPropertyBlock(mpb);
            mpb.SetColor(BaseColorId, c);
            mpb.SetColor(ColorId, c);
            rend.SetPropertyBlock(mpb);
        }
    }
}
