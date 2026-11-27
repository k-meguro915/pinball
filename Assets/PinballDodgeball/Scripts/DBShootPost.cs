using System;
using UnityEngine;

namespace PinballDodgeball
{
    /// <summary>
    /// シュートポスト。ガイドと外壁の角を埋める三角柱で、速い球を撃つ装置。
    /// 盤面側の面（faceA = 壁の上の点 → faceB = ガイドの上の点）は、壁から内側へ下る斜面なので球は引っかからずガイドへ流れる。
    /// 持ち主（owner）は、この角がある陣地（territory）の相手。相手の選手を斜め後ろからねらう。
    /// 球がセットされる条件：territory の選手（のフリッパー）の真ん中の隙間から球が落ちた（GameManager が調べる）。
    /// 撃ち方：
    ///   全自動（autoFire）：autoFireDelay 秒後に撃つ。ふだんは相手の残っている選手のうちいちばん近い選手、
    ///                       スティックを左右に倒していれば、その側（画面の向き）にいる選手をねらう（打ち分け）
    ///   ボタン：持ち主が B で撃つ（buttonAutoFireTime 秒で自動）。スティックの左右で、相手の選手の範囲の左寄り〜右寄りをねらう
    /// ポストそのものは、ただの障害物（球が当たっても何も起きない）。
    /// </summary>
    public class DBShootPost : MonoBehaviour
    {
        public int owner;
        [Tooltip("この角がある陣地（この陣地の選手をねらう）")]
        public int territory;
        [Tooltip("画面の左の角 = -1、右 = +1")]
        public int cornerSide = 1;

        [Header("撃ち方")]
        public bool autoFire = true;
        public float autoFireDelay = 1.5f;
        public float buttonAutoFireTime = 3f;
        public float shotSpeed = 26f;
        [Tooltip("ボタン：スティックを倒しきったときにねらう X（相手の選手の範囲の外形の端からこれだけ内側）")]
        public float aimEdgeInset = 0.5f;
        [Tooltip("全自動：スティックをこれ以上倒していたら、その側の選手をねらう")]
        public float autoStickThreshold = 0.3f;
        public float graceTime = 0.3f;

        [Header("形（台を作ると自動で入る。ワールド座標の XZ）")]
        public Vector3 faceA;
        public Vector3 faceB;
        public Vector3 corner;
        [Tooltip("撃つ向きは、盤面側の面の向きからこの角度まで")]
        public float maxAngleFromFace = 75f;
        public Renderer lamp;

        /// <summary>盤面側の面の真ん中</summary>
        public Vector3 FaceMid => DBUtil.Flat((faceA + faceB) * 0.5f);
        /// <summary>盤面側の面の向き（角から離れる向き）</summary>
        public Vector3 FaceNormal
        {
            get
            {
                Vector3 d = DBUtil.Flat(faceB - faceA);
                Vector3 n = new Vector3(-d.z, 0f, d.x).normalized;
                if (Vector3.Dot(n, FaceMid - DBUtil.Flat(corner)) < 0f) n = -n;
                return n;
            }
        }

        public bool IsLoaded { get; private set; }
        /// <summary>いま撃ったときの向き</summary>
        public Vector3 AimDir => aimDir.sqrMagnitude > 0.5f ? aimDir : BaseDir;
        public float LoadRemaining01 => IsLoaded
            ? Mathf.Clamp01(1f - (Time.time - loadedAt) / (autoFire ? autoFireDelay : buttonAutoFireTime)) : 0f;
        /// <summary>基準の向き：相手の選手の範囲の真ん中へ</summary>
        public Vector3 BaseDir
        {
            get
            {
                var target = new Vector3(0f, 0f, -DBConst.Sign(territory) * (DBConst.BlockerAreaNearZ + DBConst.BlockerAreaGoalZ) * 0.5f);
                Vector3 d = DBUtil.Flat(target - FaceMid);
                return d.sqrMagnitude < 1e-4f ? FaceNormal : ClampToFace(d.normalized);
            }
        }

        public event Action<DBShootPost> Fired;

        Collider col;
        Material lampMat;
        Color lampBase;
        float loadedAt, graceUntil;
        bool fireRequested, ignoring;
        Vector3 aimDir;

        void Awake()
        {
            col = GetComponentInChildren<Collider>();
            if (lamp != null)
            {
                lampMat = lamp.material;
                lampBase = lampMat.color;
            }
        }

        /// <summary>球をセットする（GameManager から）</summary>
        public void Load(DBBall ball)
        {
            IsLoaded = true;
            loadedAt = Time.time;
            fireRequested = false;
            aimDir = BaseDir;
            ball.ClearOwner();
            ball.Hold(HoldPoint());
            ball.LastHitter = owner;
        }

        public void Unload()
        {
            IsLoaded = false;
            fireRequested = false;
        }

        void Update()
        {
            var gm = DBGameManager.Instance;
            if (gm != null && IsLoaded && !autoFire && gm.GetInput(owner).bDown) fireRequested = true;

            if (lampMat != null)
            {
                if (IsLoaded)
                {
                    float k = 0.5f + 0.5f * Mathf.Sin(Time.time * (autoFire ? 14f : 9f));
                    lampMat.color = Color.Lerp(lampBase, Color.white, k);
                }
                else lampMat.color = lampBase;
            }
        }

        void FixedUpdate()
        {
            var gm = DBGameManager.Instance;
            if (gm == null || gm.ball == null) return;
            var ball = gm.ball;
            float t = Time.time;
            if (ignoring && t >= graceUntil) SetIgnore(ball, false);
            if (!IsLoaded) return;

            aimDir = autoFire ? AutoAimDir(gm, gm.GetInput(owner).move.x) : StickAimDir(gm.GetInput(owner).move);
            ball.MoveHeld(HoldPoint());
            if (gm.CurrentState != DBGameManager.State.Rally) return;

            bool fire = autoFire ? t - loadedAt >= autoFireDelay
                                 : (fireRequested || t - loadedAt >= buttonAutoFireTime);
            if (fire) Fire(gm, aimDir);
        }

        /// <summary>球を持つ位置：盤面側の面の真ん中の前</summary>
        Vector3 HoldPoint() => FaceMid + FaceNormal * (DBConst.BallRadius + 0.06f) + Vector3.up * DBConst.BallY;

        /// <summary>面の向きから maxAngleFromFace を超えないようにする（面の中へ撃たない）</summary>
        Vector3 ClampToFace(Vector3 dir)
        {
            Vector3 n = FaceNormal;
            float ang = Vector3.SignedAngle(n, dir, Vector3.up);
            if (Mathf.Abs(ang) <= maxAngleFromFace) return dir;
            return (Quaternion.Euler(0f, Mathf.Sign(ang) * maxAngleFromFace, 0f) * n).normalized;
        }

        void Fire(DBGameManager gm, Vector3 dir)
        {
            var ball = gm.ball;
            IsLoaded = false;
            fireRequested = false;
            graceUntil = Time.time + graceTime;
            SetIgnore(ball, true);
            ball.LastHitter = owner;
            ball.LastOutfield = null;
            ball.Release(dir * shotSpeed);
            ball.StartPowerShot();
            Fired?.Invoke(this);
        }

        /// <summary>相手の選手の範囲の真ん中の Z</summary>
        float AreaMidZ => -DBConst.Sign(territory) * (DBConst.BlockerAreaNearZ + DBConst.BlockerAreaGoalZ) * 0.5f;

        /// <summary>ボタン：スティックの左右で、相手の選手の範囲の左寄り〜右寄りをねらう（画面の向き）</summary>
        Vector3 StickAimDir(Vector2 stick)
        {
            float x = Mathf.Clamp(stick.x, -1f, 1f) * Mathf.Max(0f, DBConst.BlockerAreaHalfX - aimEdgeInset);
            Vector3 d = new Vector3(x, 0f, AreaMidZ) - FaceMid;
            return d.sqrMagnitude < 1e-4f ? BaseDir : ClampToFace(DBUtil.Flat(d).normalized);
        }

        /// <summary>全自動：ふだんはいちばん近い選手。スティックを左右に倒していれば、その側（画面の向き）にいちばん寄っている選手</summary>
        Vector3 AutoAimDir(DBGameManager gm, float stickX)
        {
            var row = gm.rows[territory];
            if (row == null) return BaseDir;
            Vector3 from = FaceMid;
            int pick = -1;
            float best = float.MaxValue;
            bool bySide = Mathf.Abs(stickX) >= autoStickThreshold;
            for (int i = 0; i < row.Count; i++)
            {
                if (!row.IsAlive(i)) continue;
                Vector3 bp = DBUtil.Flat(row.blockers[i].position);
                float score = bySide ? -Mathf.Sign(stickX) * bp.x : (bp - from).magnitude;
                if (score < best) { best = score; pick = i; }
            }
            if (pick < 0) return BaseDir;
            Vector3 d = DBUtil.Flat(row.blockers[pick].position) - from;
            return d.sqrMagnitude < 1e-4f ? BaseDir : ClampToFace(d.normalized);
        }

        void SetIgnore(DBBall ball, bool on)
        {
            if (col != null && ball.Col != null) Physics.IgnoreCollision(ball.Col, col, on);
            ignoring = on;
        }
    }
}
