using System;
using System.Collections.Generic;
using UnityEngine;

namespace Pinball
{
    /// <summary>
    /// ロールオーバー（インレーンの床の通過スイッチ）。球が上を通ると、最後に打った人の色で点灯する。
    ///   通過：最後に打った人のパワーゲージ +（PowerShot.rolloverCharge）
    ///   同じ陣地のロールオーバー（左右のインレーン）を同じ人が全部点灯させると、その人のゲージにボーナス
    ///   （PowerShot.rolloverBonus）。少し点滅してから全部消える
    /// 別の人が通ると、その人の色に塗り替わる。点が入った後と試合開始で全部消える。
    /// 当たり判定はトリガーだけなので、球の動きは変えない。
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class Rollover : MonoBehaviour
    {
        [Tooltip("どちらの陣地のロールオーバーか（0 = P1, 1 = P2）")]
        public int fieldOwner = 0;

        [Tooltip("床のランプ（光らせる）")]
        public HitFlash lamp;

        [Tooltip("消えているときの色")]
        public Color offColor = new Color(0.25f, 0.28f, 0.38f);

        [Tooltip("同じスイッチで続けて数えない時間（秒）")]
        public float cooldown = 0.3f;

        /// <summary>全部そろった（陣地、そろえた人、場所）。表示用</summary>
        public static event Action<int, int, Vector3> Completed;

        /// <summary>点灯させた人（-1 = 消えている）</summary>
        public int LitBy { get; private set; } = -1;

        static readonly List<Rollover> all = new List<Rollover>();
        float lastHit = -10f;
        float blinkUntil = -1f;

        void OnEnable() { all.Add(this); }
        void OnDisable() { all.Remove(this); }

        void Start()
        {
            GetComponent<Collider>().isTrigger = true;
            ApplyColor();
        }

        void OnTriggerEnter(Collider other)
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.State != MatchState.Playing) return;
            var ball = other.GetComponentInParent<Ball>();
            if (ball == null || ball.IsFrozen) return;
            if (Time.time - lastHit < cooldown || blinkUntil > Time.time) return;
            lastHit = Time.time;

            int p = ball.LastHitter;
            if (p < 0 || p > 1) return;

            LitBy = p;
            ApplyColor();
            if (lamp != null) lamp.Play();
            PinballAudio.Play(PinballSound.FlipperUp, 0.5f);
            PowerShot.AddCharge(ball, ChargeSource.Rollover, this);

            // 同じ陣地のスイッチが全部、同じ人の色になったらボーナス
            bool complete = true;
            foreach (var r in all)
                if (r.fieldOwner == fieldOwner && r.LitBy != p) complete = false;
            if (complete)
            {
                PowerShot.AddBonus(p, ChargeSource.RolloverBonus, transform.position);
                PinballAudio.Play(PinballSound.Bumper, 0.9f);
                foreach (var r in all)
                    if (r.fieldOwner == fieldOwner) r.blinkUntil = Time.time + 0.8f;
                Completed?.Invoke(fieldOwner, p, transform.position);
            }
        }

        void Update()
        {
            if (blinkUntil < 0f) return;
            if (Time.time >= blinkUntil)
            {
                // 点滅が終わったら消す
                blinkUntil = -1f;
                LitBy = -1;
                ApplyColor();
                return;
            }
            if (lamp != null)
            {
                bool on = Mathf.Repeat(Time.time, 0.16f) < 0.08f;
                lamp.SetBaseColor(on && LitBy >= 0 ? Color.white : offColor);
            }
        }

        void ApplyColor()
        {
            if (lamp == null) return;
            lamp.SetBaseColor(LitBy >= 0 ? GameManager.PlayerColor(LitBy) : offColor);
        }

        /// <summary>全部消す（点が入った後・試合開始）</summary>
        public static void ResetAll()
        {
            foreach (var r in all)
            {
                r.LitBy = -1;
                r.blinkUntil = -1f;
                r.ApplyColor();
            }
        }

        public static IReadOnlyList<Rollover> All => all;
    }
}
