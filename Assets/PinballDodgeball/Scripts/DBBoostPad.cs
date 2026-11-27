using System;
using UnityEngine;

namespace PinballDodgeball
{
    /// <summary>
    /// 加速の帯（2026-11-27）。床の矢印の帯（トリガー）の上にいる球を、矢印の向きへ押して速くする。
    /// 背面の加速レーンは、この帯をいくつもつないで作る（半円にそって向きを変える）。
    /// ・clearOwner：帯に入った球を、誰の球でもなくする（背面の加速レーン）
    /// ・chooseSideByVelocity：入口の真ん中の帯。入ったときの球の左右の向き（遅ければ位置）で、左右どちらへ押すかを決める
    /// </summary>
    public class DBBoostPad : MonoBehaviour
    {
        [Tooltip("押す向き（台を作ると自動で入る）")]
        public Vector3 direction = Vector3.forward;
        [Tooltip("足す加速（1 秒あたり）")]
        public float acceleration = 30f;
        [Tooltip("向きに沿った速さがこれになったら、それ以上は押さない")]
        public float maxAlongSpeed = 14f;
        [Tooltip("帯に入った球を、誰の球でもなくする")]
        public bool clearOwner;
        [Tooltip("入ったときの球の左右の向きで、押す向きを ±X に決める（direction は使わない）")]
        public bool chooseSideByVelocity;
        public Renderer[] arrows = new Renderer[0];
        public Color glowColor = Color.white;
        public float glowTime = 0.25f;

        float glowUntil;
        float chosenSide;
        Material[] mats;
        Color[] baseColors;

        void Start()
        {
            mats = new Material[arrows.Length];
            baseColors = new Color[arrows.Length];
            for (int i = 0; i < arrows.Length; i++)
            {
                if (arrows[i] == null) continue;
                mats[i] = arrows[i].material;
                baseColors[i] = mats[i].color;
            }
        }

        static DBBall BallOf(Collider other)
        {
            if (other.attachedRigidbody == null) return null;
            var ball = other.attachedRigidbody.GetComponent<DBBall>();
            return ball != null && !ball.IsHeld ? ball : null;
        }

        void OnTriggerEnter(Collider other)
        {
            var ball = BallOf(other);
            if (ball == null) return;
            if (clearOwner && ball.LastHitter >= 0) ball.ClearOwner();
            if (chooseSideByVelocity)
            {
                Vector3 v = ball.Velocity;
                float x = ball.Position.x - transform.position.x;
                chosenSide = Mathf.Abs(v.x) > 0.3f ? Mathf.Sign(v.x)
                           : Mathf.Abs(x) > 0.05f ? Mathf.Sign(x)
                           : (UnityEngine.Random.value < 0.5f ? -1f : 1f);
            }
        }

        void OnTriggerExit(Collider other)
        {
            if (BallOf(other) != null) chosenSide = 0f;
        }

        void OnTriggerStay(Collider other)
        {
            var ball = BallOf(other);
            if (ball == null) return;
            if (clearOwner && ball.LastHitter >= 0) ball.ClearOwner();
            if (chooseSideByVelocity && chosenSide == 0f) chosenSide = ball.Position.x >= transform.position.x ? 1f : -1f;
            Vector3 d = chooseSideByVelocity ? new Vector3(chosenSide, 0f, 0f) : DBUtil.Flat(direction).normalized;
            Vector3 v = ball.Body.GetVelocity();
            float along = Vector3.Dot(v, d);
            if (along >= maxAlongSpeed) return;
            float add = Mathf.Min(acceleration * Time.fixedDeltaTime, maxAlongSpeed - along);
            ball.Body.SetVelocity(v + d * add);
            glowUntil = Time.time + glowTime;
        }

        void Update()
        {
            if (mats == null) return;
            // 球が乗っている間は光る。いないときは、矢印が順に光って向きを見せる
            bool on = Time.time < glowUntil;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == null) continue;
                float wave = 0.5f + 0.5f * Mathf.Sin(Time.time * 6f - i * 1.2f);
                mats[i].color = on ? glowColor : Color.Lerp(baseColors[i] * 0.6f, baseColors[i], wave);
            }
        }
    }
}
