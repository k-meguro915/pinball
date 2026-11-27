using UnityEngine;

namespace Pinball
{
    /// <summary>
    /// フリッパー。キネマティックRigidbodyを MoveRotation で回すので、
    /// 動きが毎回同じになる（対戦・同期で扱いやすい）。
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Flipper : MonoBehaviour
    {
        [Tooltip("0 = P1（左ゴール側）, 1 = P2/CPU（右ゴール側）")]
        public int playerIndex = 0;

        public FlipperSide side = FlipperSide.Top;

        [Tooltip("フリッパーの長さ（CPUの判定にも使う）")]
        public float length = 1.4f;

        [Tooltip("離しているときのY回転（度）")]
        public float restAngle = 25f;

        [Tooltip("押しているときのY回転（度）")]
        public float activeAngle = -30f;

        [Tooltip("振り上げ速度（度/秒）")]
        public float upSpeed = 1000f;

        [Tooltip("戻り速度（度/秒）")]
        public float downSpeed = 700f;

        [Tooltip("押してからこの秒数の間を「振り上げ中」とみなす（パワーショットを打ち返せる時間）")]
        public float swingWindow = 0.1f;

        Rigidbody rb;
        float currentAngle;
        bool wasPressed;
        float pressTime = -10f;
        float stunnedUntil = -10f;
        Collider[] colliders;

        public bool IsPressed => PinballInput.Flipper(playerIndex, side) && !IsStunned;
        /// <summary>振り上げている最中か（押した直後）</summary>
        public bool IsSwinging => IsPressed && Time.time - pressTime <= swingWindow;
        /// <summary>パワーショットに押し負けて動かない（下がったまま）</summary>
        public bool IsStunned => Time.time < stunnedUntil;
        public float StunnedUntil => stunnedUntil;
        /// <summary>使えるようになるまでの残り（0〜1）。表示用</summary>
        public float StunRemaining01 => IsStunned && stunLength > 0f ? Mathf.Clamp01((stunnedUntil - Time.time) / stunLength) : 0f;
        float stunLength = 1f;
        public Collider[] Colliders => colliders;

        /// <summary>元の状態に戻す（使用不可を解除して、下げた位置へ。点が入った後のリセット）</summary>
        public void ResetState()
        {
            stunnedUntil = -10f;
            pressTime = -10f;
            wasPressed = false;
            currentAngle = restAngle;
            if (rb != null) rb.rotation = Quaternion.Euler(0f, currentAngle, 0f);
            transform.rotation = Quaternion.Euler(0f, currentAngle, 0f);
        }

        /// <summary>seconds 秒、下がったまま動かなくする</summary>
        public void Stun(float seconds)
        {
            stunnedUntil = Mathf.Max(stunnedUntil, Time.time + seconds);
            stunLength = Mathf.Max(0.01f, stunnedUntil - Time.time);
        }

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            currentAngle = restAngle;
            rb.rotation = Quaternion.Euler(0f, currentAngle, 0f);
            colliders = GetComponentsInChildren<Collider>();
        }

        void OnCollisionEnter(Collision collision)
        {
            var ball = collision.collider.GetComponentInParent<Ball>();
            if (ball == null) return;
            ball.LastHitter = playerIndex;
            PowerShot.NotifyFlipperTouch(this, ball);
        }

        void OnCollisionStay(Collision collision)
        {
            // 球を乗せたまま打つとき（Enter が来ない）にもパワーショットを発動できるように
            var ball = collision.collider.GetComponentInParent<Ball>();
            if (ball == null) return;
            ball.LastHitter = playerIndex;
            PowerShot.NotifyFlipperTouch(this, ball);
        }

        void FixedUpdate()
        {
            bool pressed = IsPressed;
            if (pressed && !wasPressed)
            {
                pressTime = Time.time;
                PinballAudio.Play(PinballSound.FlipperUp);
            }
            wasPressed = pressed;
            float target = pressed ? activeAngle : restAngle;
            float speed = pressed ? upSpeed : downSpeed;

            currentAngle = Mathf.MoveTowardsAngle(currentAngle, target, speed * Time.fixedDeltaTime);
            rb.MoveRotation(Quaternion.Euler(0f, currentAngle, 0f));
        }
    }
}
