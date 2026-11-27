using UnityEngine;

namespace Pinball
{
    /// <summary>
    /// スリングショット（三角形の弾き台）の「弾く面」。
    /// ボールがある程度の勢いで当たると、面の外向きに強く弾き返す。
    /// 面に沿ってゆっくり転がるだけのときは弾かない（フリッパーへ自然に転がり落ちる）。
    /// 弾く向きは、このオブジェクトの左右方向（transform.right）のうちボールがある側。
    /// </summary>
    public class Slingshot : MonoBehaviour
    {
        [Tooltip("弾き出す速さ")]
        public float kickSpeed = 11f;

        [Tooltip("この速さ以上で面に当たったときだけ弾く")]
        public float minImpact = 1.2f;

        [Tooltip("面に沿った方向の速さをどれだけ残すか")]
        [Range(0f, 1f)] public float keepTangent = 0.6f;

        [Tooltip("連続で弾かない時間（秒）")]
        public float cooldown = 0.08f;

        HitFlash flash;
        float lastKick = -1f;

        void Awake()
        {
            flash = GetComponent<HitFlash>();
        }

        void OnCollisionEnter(Collision collision)
        {
            var ball = collision.collider.GetComponentInParent<Ball>();
            if (ball == null || ball.IsFrozen) return;
            if (Time.time - lastKick < cooldown) return;

            Vector3 n = transform.right;
            n.y = 0f;
            n.Normalize();
            if (Vector3.Dot(ball.transform.position - transform.position, n) < 0f) n = -n;

            float impact = Mathf.Abs(Vector3.Dot(collision.relativeVelocity, n));
            if (impact < minImpact) return;

            Vector3 v = ball.Body.GetVelocity();
            Vector3 tangent = v - Vector3.Dot(v, n) * n;
            tangent.y = 0f;
            Vector3 kicked = tangent * keepTangent + n * kickSpeed;
            ball.Body.SetVelocity(new Vector3(kicked.x, v.y, kicked.z));

            lastKick = Time.time;
            if (flash != null) flash.Play();
            PinballAudio.Play(PinballSound.Slingshot);
            PowerShot.AddCharge(ball, ChargeSource.Slingshot, this);
        }
    }
}
