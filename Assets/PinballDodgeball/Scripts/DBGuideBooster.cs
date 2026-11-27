using UnityEngine;

namespace PinballDodgeball
{
    /// <summary>
    /// ガイドの加速レーン。ガイドの上の細長い帯（トリガー）にいる球を、フリッパーの方へ押して加速する。
    /// 台の幅を広げてガイドがゆるく長くなり、球がガイドを転がり落ちるのに時間がかかるようになったための対策。
    /// ・ガイドに沿った向き（direction）の速さが maxAlongSpeed になるまで、acceleration で足す
    /// ・外へ向かって速く動いている球（フリッパーで外側へ打った球など）は押さない
    /// ・帯はフリッパーの手前で終わる（速いままフリッパーに突っ込まないように）
    /// </summary>
    public class DBGuideBooster : MonoBehaviour
    {
        [Tooltip("押す向き（ガイドに沿ってフリッパーの方へ）。台を作ると自動で入る")]
        public Vector3 direction = Vector3.back;
        [Tooltip("足す加速（1 秒あたり）")]
        public float acceleration = 12f;
        [Tooltip("ガイドに沿った向きの速さがこれになったら、それ以上は押さない")]
        public float maxAlongSpeed = 8f;
        [Tooltip("外へ向かう速さがこれより大きい球は押さない")]
        public float ignoreOutwardSpeed = 1f;

        void OnTriggerStay(Collider other)
        {
            if (other.attachedRigidbody == null) return;
            var ball = other.attachedRigidbody.GetComponent<DBBall>();
            if (ball == null || ball.IsHeld) return;

            Vector3 d = DBUtil.Flat(direction).normalized;
            Vector3 v = ball.Body.GetVelocity();
            float along = Vector3.Dot(v, d);
            if (along < -ignoreOutwardSpeed || along >= maxAlongSpeed) return;
            float add = Mathf.Min(acceleration * Time.fixedDeltaTime, maxAlongSpeed - along);
            ball.Body.SetVelocity(v + d * add);
        }
    }
}
