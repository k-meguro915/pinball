using System.Collections.Generic;
using UnityEngine;

namespace Pinball
{
    /// <summary>
    /// プランジャー（サーブ用の発射装置）。各プレイヤーの発射レーンの根元にある。
    /// 自分のサーブ番のときだけ、長押しで溜めて離すとトリガー内のボールを打ち出す。
    /// 打ち出す向きはこのオブジェクトの forward。
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class Plunger : MonoBehaviour
    {
        [Tooltip("0 = P1, 1 = P2/CPU")]
        public int playerIndex = 0;

        public float minLaunchSpeed = 8f;
        public float maxLaunchSpeed = 22f;

        [Tooltip("最大まで溜めるのにかかる秒数")]
        public float chargeTime = 1.0f;

        [Tooltip("見た目用（引くと後ろに動く）")]
        public Transform visual;
        public float pullDistance = 0.6f;

        /// <summary>溜め具合 0〜1</summary>
        public float Charge { get; private set; }

        /// <summary>サーブ時にボールを置く位置</summary>
        public Vector3 SpawnPoint => transform.position + transform.forward * 0.2f;

        /// <summary>レーンの根元でボールが止まっているか</summary>
        public bool HasBallReady
        {
            get
            {
                foreach (var b in ballsInZone)
                {
                    if (IsUsable(b) && b.Body.GetVelocity().sqrMagnitude < 0.5f * 0.5f)
                        return true;
                }
                return false;
            }
        }

        readonly List<Ball> ballsInZone = new List<Ball>();
        Vector3 visualRest;
        Collider zone;

        void Awake()
        {
            zone = GetComponent<Collider>();
        }

        void Start()
        {
            if (visual != null) visualRest = visual.localPosition;
        }

        // ワープ（リセット）でトリガーを出たことが検知されない場合に備え、位置でも確認する
        bool IsUsable(Ball b)
        {
            return b != null && b.isActiveAndEnabled && !b.IsFrozen &&
                   zone.bounds.Contains(b.transform.position);
        }

        void Update()
        {
            var gm = GameManager.Instance;
            bool allowed = gm != null && gm.CanPlunge(playerIndex);

            if (!allowed)
            {
                Charge = 0f;
            }
            else if (PinballInput.Plunger(playerIndex))
            {
                Charge = Mathf.MoveTowards(Charge, 1f, Time.deltaTime / chargeTime);
            }
            else if (Charge > 0f)
            {
                float charge = Charge;
                Charge = 0f;
                if (Launch(Mathf.Lerp(minLaunchSpeed, maxLaunchSpeed, charge)))
                {
                    gm.OnPlunged(playerIndex, charge);
                    foreach (var b in ballsInZone) if (b != null) b.LastHitter = playerIndex;
                    PinballAudio.Play(PinballSound.Launch, 0.5f + 0.5f * charge);
                }
            }

            if (visual != null)
                visual.localPosition = visualRest + Vector3.back * (pullDistance * Charge);
        }

        bool Launch(float speed)
        {
            bool launched = false;
            foreach (var ball in ballsInZone)
            {
                if (IsUsable(ball))
                {
                    ball.Body.SetVelocity(transform.forward * speed);
                    launched = true;
                }
            }
            return launched;
        }

        void OnTriggerEnter(Collider other)
        {
            var ball = other.GetComponentInParent<Ball>();
            if (ball != null && !ballsInZone.Contains(ball)) ballsInZone.Add(ball);
        }

        void OnTriggerExit(Collider other)
        {
            var ball = other.GetComponentInParent<Ball>();
            if (ball != null) ballsInZone.Remove(ball);
        }

        void OnDisable()
        {
            ballsInZone.Clear();
        }
    }
}
