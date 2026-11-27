using System;
using UnityEngine;

namespace Pinball
{
    /// <summary>
    /// アウトレーンの入口のふた。ふだんは閉じていて（当たると内側へ転がる斜めの板）、
    /// 持ち主のブロッカーのうち「同じ側」のものがダウンしている間だけ開く。
    ///   左のアウトレーン ⇔ 画面で左にあるブロッカー、右 ⇔ 右
    /// 開いている間に入った球は、フリッパーを通らずに持ち主のゴールへ落ちる。
    /// 閉じるとき、球がふたに重なっていたら離れるまで待つ（めり込み防止）。
    /// </summary>
    public class Outlane : MonoBehaviour
    {
        [Tooltip("このアウトレーンの持ち主（落ちるとこの人の失点）")]
        public int ownerPlayer = 0;

        [Tooltip("盤面のどちら側か（ワールドの X の符号：-1 = 左、+1 = 右）")]
        public float sideSign = -1f;

        [Tooltip("入口のふた（当たり判定）")]
        public Collider gate;
        [Tooltip("ふたの見た目（開くと消える）")]
        public Renderer gateRenderer;

        [Tooltip("入口の中心と、レーンの下の端（表示用。台の生成で自動設定）")]
        public Vector3 entrance;
        public Vector3 laneEnd;
        public float laneWidth = 0.8f;

        [Tooltip("ON = ブロッカーのダウンで開く。OFF = いつも開いている")]
        public bool openOnBlockerDown = true;

        /// <summary>開いた（アウトレーン）。表示・効果音用</summary>
        public static event Action<Outlane> Opened;

        public bool IsOpen { get; private set; }
        /// <summary>閉じるまでの残り（0〜1。開いていなければ 0）</summary>
        public float Remaining01 { get; private set; }
        public float OpenedTime { get; private set; } = -10f;

        BlockerRow row;
        Ball ball;
        float nextSearch;

        void Start()
        {
            SetOpen(!openOnBlockerDown, true);
        }

        void FixedUpdate()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;
            if (ball == null) ball = gm.ball;

            bool want;
            if (!openOnBlockerDown) want = true;
            else
            {
                var piece = FindPiece(out float left, out float total);
                bool rally = gm.State == MatchState.Playing || gm.State == MatchState.Serve;
                want = rally && piece != null;
                Remaining01 = want && total > 0f ? Mathf.Clamp01(left / total) : 0f;
            }

            // 閉じるとき、球がふたに重なっていたら待つ
            if (!want && IsOpen && BallNearGate()) want = true;
            SetOpen(want, false);
        }

        BlockerRow.Piece FindPiece(out float left, out float total)
        {
            left = 0f;
            total = 1f;
            if (row == null && Time.time >= nextSearch)
            {
                nextSearch = Time.time + 1f;
#if UNITY_2023_1_OR_NEWER
                var rows = FindObjectsByType<BlockerRow>(FindObjectsSortMode.None);
#else
                var rows = FindObjectsOfType<BlockerRow>();
#endif
                foreach (var r in rows) if (r.playerIndex == ownerPlayer) row = r;
            }
            if (row == null) return null;

            float cx = row.transform.position.x;
            foreach (var p in row.Pieces)
            {
                if (p.transform == null || p.collider == null) continue;
                float side = Mathf.Sign(p.transform.position.x - cx);
                if (side != Mathf.Sign(sideSign) || !p.IsDown) continue;
                left = Mathf.Max(0f, p.downUntil - Time.time);
                total = Mathf.Max(0.01f, row.downTime);
                return p;
            }
            return null;
        }

        /// <summary>すぐに閉じる（点が入った後のリセット）</summary>
        public void ForceClose()
        {
            Remaining01 = 0f;
            SetOpen(false, true);
        }

        bool BallNearGate()
        {
            if (ball == null || !ball.isActiveAndEnabled || gate == null) return false;
            // ※ Collider.ClosestPoint は当たり判定を切っている（開いている）間は使えず、
            //   球の位置をそのまま返してしまう → いつも「重なっている」になって閉じなかった。
            //   ふたは箱なので、箱の形から自分で一番近い点を求める
            var t = gate.transform;
            Vector3 local = t.InverseTransformPoint(ball.transform.position);
            Vector3 clamped = new Vector3(Mathf.Clamp(local.x, -0.5f, 0.5f), Mathf.Clamp(local.y, -0.5f, 0.5f), Mathf.Clamp(local.z, -0.5f, 0.5f));
            Vector3 c = t.TransformPoint(clamped);
            Vector3 d = c - ball.transform.position;
            d.y = 0f;
            return d.magnitude < ball.transform.lossyScale.x * 0.5f + 0.1f;
        }

        void SetOpen(bool open, bool force)
        {
            if (!force && open == IsOpen) return;
            bool wasOpen = IsOpen;
            IsOpen = open;
            if (gate != null) gate.enabled = !open;
            if (gateRenderer != null) gateRenderer.enabled = !open;
            if (open && !wasOpen && !force)
            {
                OpenedTime = Time.time;
                PinballAudio.Play(PinballSound.Launch, 0.8f);
                Opened?.Invoke(this);
            }
        }
    }
}
