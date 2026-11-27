using System;
using UnityEngine;

namespace PinballDodgeball
{
    /// <summary>
    /// ドロップターゲットの列（3 つ）。各陣地の左右の壁ぎわに 1 列ずつ。
    /// 攻める側（attacker = この陣地の相手）の球が当たると倒れる。それ以外の球（守る側の球・誰の球でもない球）では倒れず、壁と同じく跳ね返る。
    /// 3 つとも倒すと Completed。GameManager が、同じ側の角にある攻める側のシュートポストに球をセットする。的はすぐ立ち直る（球が離れてから）。
    /// </summary>
    public class DBDropTargetBank : MonoBehaviour
    {
        [Tooltip("この列がある陣地")]
        public int territory;
        [Tooltip("画面の左の壁 = -1、右 = +1")]
        public int side = 1;
        [Tooltip("この速さ以上で当たると倒れる")]
        public float minHitSpeed = 2f;
        [Tooltip("3 つ倒してから立ち直るまで")]
        public float resetDelay = 0.6f;
        [Tooltip("倒れた的を沈める深さ（見た目）")]
        public float dropDepth = 0.35f;

        [Header("部品（台を作ると自動で入る）")]
        public Transform[] targets;

        public int Attacker => DBConst.Opponent(territory);
        public int Count => targets != null ? targets.Length : 0;
        public bool IsDown(int i) => down != null && down[i];
        public int DownCount
        {
            get { int n = 0; for (int i = 0; i < Count; i++) if (down[i]) n++; return n; }
        }

        public event Action<DBDropTargetBank, int> TargetDropped;
        public event Action<DBDropTargetBank> Completed;

        bool[] down;
        Collider[] cols;
        Vector3[] upPos;
        float resetAt = -1f;

        void Awake()
        {
            int n = Count;
            down = new bool[n];
            cols = new Collider[n];
            upPos = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                cols[i] = targets[i].GetComponent<Collider>();
                upPos[i] = targets[i].localPosition;
                var t = targets[i].GetComponent<DBDropTarget>();
                if (t == null) t = targets[i].gameObject.AddComponent<DBDropTarget>();
                t.bank = this;
                t.index = i;
            }
        }

        /// <summary>試合開始：全部立てる</summary>
        public void ResetAll()
        {
            resetAt = -1f;
            for (int i = 0; i < Count; i++) SetDown(i, false);
        }

        void SetDown(int i, bool d)
        {
            down[i] = d;
            if (cols[i] != null) cols[i].enabled = !d;
            targets[i].localPosition = upPos[i] + (d ? Vector3.down * dropDepth : Vector3.zero);
        }

        public void OnTargetHit(int i, Collision c)
        {
            var gm = DBGameManager.Instance;
            if (gm == null || !gm.IsPlaying || down[i] || resetAt >= 0f) return;
            if (c.rigidbody == null) return;
            var ball = c.rigidbody.GetComponent<DBBall>();
            if (ball == null || ball.LastHitter != Attacker) return;
            if (c.relativeVelocity.magnitude < minHitSpeed) return;

            SetDown(i, true);
            TargetDropped?.Invoke(this, i);
            if (DownCount >= Count)
            {
                resetAt = Time.time + resetDelay;
                Completed?.Invoke(this);
            }
        }

        void Update()
        {
            if (resetAt < 0f || Time.time < resetAt) return;
            // 球が的に重なっていたら、離れるまで待つ
            var gm = DBGameManager.Instance;
            if (gm != null && gm.ball != null && !gm.ball.IsHeld)
            {
                Vector3 bp = DBUtil.Flat(gm.ball.Position);
                for (int i = 0; i < Count; i++)
                    if ((bp - DBUtil.Flat(targets[i].position)).magnitude < 0.6f) return;
            }
            resetAt = -1f;
            for (int i = 0; i < Count; i++) SetDown(i, false);
        }
    }
}
