using System;
using UnityEngine;

namespace PinballDodgeball
{
    /// <summary>
    /// CPU（VS CPU のとき P2）。強さごとの値は配列で [EASY, NORMAL, HARD]。
    /// ・選手：飛んでくる相手の球をよける。強さに合わせた割合で、よけずにキャッチする（2026-11-27。拾うのをなくしたため）
    /// ・キャッチした球：少し持ってから、相手のいちばん近い選手をねらって投げる（B を離す）
    /// ・サーブ：ルートと強さを選んで打つ
    /// ・外野ブロッカー：いつも全自動で投げる
    /// </summary>
    [Serializable]
    public class DBCpu
    {
        [Tooltip("キャッチしてから投げるまでの時間")]
        public float[] throwDelay = { 1.0f, 0.6f, 0.35f };
        [Tooltip("飛んでくる球を、よけずにキャッチしにいく割合")]
        public float[] catchChance = { 0.35f, 0.6f, 0.85f };
        [Tooltip("球が列に着く何秒前に、キャッチのボタンを押すか")]
        public float[] catchLead = { 0.35f, 0.22f, 0.12f };
        [Tooltip("ねらいのずれ（スティックの左右）")]
        public float[] aimNoise = { 0.5f, 0.25f, 0.08f };
        [Tooltip("よけるときの読みのずれ")]
        public float[] dodgeNoise = { 1.2f, 0.6f, 0.2f };
        public float[] serveNoise = { 2.5f, 1.5f, 0.6f };
        [Range(0f, 1f)] public float opponentRouteChance = 0.6f;

        [NonSerialized] int serveToken = -1;
        [NonSerialized] float serveStart;
        [NonSerialized] bool wantOpp;
        [NonSerialized] float targetPower;
        [NonSerialized] bool threatActive;
        [NonSerialized] float threatNoise;
        [NonSerialized] bool willCatch;
        [NonSerialized] bool holding;
        [NonSerialized] float holdStart;
        [NonSerialized] float holdAimNoise;

        public void ResetState()
        {
            serveToken = -1;
            threatActive = false;
            holding = false;
        }

        public DBInputState Tick(DBGameManager gm, int p)
        {
            var s = new DBInputState();
            int lv = (int)gm.cpuLevel;
            float t = Time.time;
            var ball = gm.ball;
            var row = gm.rows[p];
            if (ball == null || row == null) return s;

            // サーブ
            if (gm.CurrentState == DBGameManager.State.Serve && gm.Server == p && !gm.ServeLaunched)
            {
                if (serveToken != gm.ServeToken)
                {
                    serveToken = gm.ServeToken;
                    serveStart = t;
                    wantOpp = UnityEngine.Random.value < opponentRouteChance;
                    // 届く最低の強さ（台の傾きで減速）：自陣の出入り口まで／中央の山まで。台の長さが変わっても合うように計算
                    float a = ball.slopeAcceleration;
                    float ownMin = Mathf.Sqrt(2f * a * (DBConst.PlungerZ - DBConst.ServeOwnExitZ));
                    float oppMin = Mathf.Sqrt(2f * a * DBConst.PlungerZ);
                    targetPower = wantOpp ? UnityEngine.Random.Range(oppMin + 1f, oppMin + 4.5f) : UnityEngine.Random.Range(ownMin + 0.6f, ownMin + 2.5f);
                    targetPower += UnityEngine.Random.Range(-1f, 1f) * serveNoise[lv] * 0.5f;
                }
                if (t - serveStart < 0.8f) return s;
                if (gm.RouteOpponent != wantOpp && !gm.Charging) { s.y = true; return s; }
                s.b = !gm.Charging || gm.CurrentPower < targetPower;
                return s;
            }

            if (!gm.IsPlaying) return s;

            // キャッチした球を持っている：少し持ってから、ねらって投げる（B を離す）
            if (row.IsCatching)
            {
                if (!holding)
                {
                    holding = true;
                    holdStart = t;
                    holdAimNoise = UnityEngine.Random.Range(-1f, 1f) * aimNoise[lv];
                }
                s.move.x = Mathf.Clamp(AimX(gm, row, p) + holdAimNoise, -1f, 1f);
                float since = t - holdStart;
                s.b = since < throwDelay[lv];
                return s;
            }
            holding = false;

            // 選手：よける
            Vector3 rp = row.Center;
            Vector3 bp = ball.Position;
            Vector3 v = ball.Velocity;
            Vector3 fwd = row.Forward;
            float ahead = Vector3.Dot(bp - rp, fwd);        // 列の前にどれだけ離れているか
            float approach = -Vector3.Dot(v, fwd);          // こちらへ向かう速さ
            // 来る球：相手の球（よけるかキャッチ）と、誰の球でもない球（キャッチ）
            bool incoming = !ball.IsHeld && ball.LastHitter != p && approach > 1f && ahead > -0.3f;
            bool opponentBall = ball.LastHitter == DBConst.Opponent(p);

            float targetX;
            if (incoming)
            {
                if (!threatActive)
                {
                    threatActive = true;
                    threatNoise = UnityEngine.Random.Range(-1f, 1f) * dodgeNoise[lv];
                    willCatch = !opponentBall || UnityEngine.Random.value < catchChance[lv];
                }
                float tt = Mathf.Max(0f, ahead) / approach;
                float px = Fold(bp.x + v.x * tt);
                if (willCatch)
                {
                    targetX = CatchX(row, px + threatNoise * 0.5f);
                    s.b = tt < catchLead[lv];
                }
                else
                {
                    targetX = DodgeX(row, px + threatNoise);
                }
            }
            else
            {
                threatActive = false;
                willCatch = false;
                targetX = 0f;
            }

            s.move.x = Mathf.Clamp((targetX - rp.x) * 2f, -1f, 1f);
            float zTarget = row.startPosition.z;
            s.move.y = Mathf.Clamp((zTarget - rp.z) * 1f, -1f, 1f) * 0.5f;
            return s;
        }

        /// <summary>相手のいちばん近い選手へのスティックの左右（-1〜1。投げる角度の最大で割る）</summary>
        static float AimX(DBGameManager gm, DBBlockerRow row, int p)
        {
            var opp = gm.rows[DBConst.Opponent(p)];
            int idx = row.CaughtIndex;
            if (opp == null || idx < 0) return 0f;
            Vector3 from = row.Center + row.blockers[idx].localPosition;
            float best = float.MaxValue, aim = 0f;
            for (int i = 0; i < opp.Count; i++)
            {
                if (!opp.IsAlive(i)) continue;
                Vector3 to = opp.Center + opp.blockers[i].localPosition;
                Vector3 d = DBUtil.Flat(to - from);
                if (d.sqrMagnitude >= best) continue;
                best = d.sqrMagnitude;
                float ang = Mathf.Atan2(d.x, Mathf.Abs(d.z)) * Mathf.Rad2Deg;
                aim = ang / Mathf.Max(1f, row.maxLaunchAngle);
            }
            return aim;
        }

        static float Fold(float x)
        {
            float w = DBConst.PlayHalfWidth - DBConst.BallRadius;
            for (int k = 0; k < 4 && Mathf.Abs(x) > w; k++) x = Mathf.Sign(x) * 2f * w - x;
            return x;
        }

        /// <summary>残っている選手のうち、いちばん近い選手が球の来る X に立つ列の位置</summary>
        static float CatchX(DBBlockerRow row, float px)
        {
            row.GetCenterRangeX(out float minX, out float maxX);
            float best = row.Center.x, bestD = float.MaxValue;
            for (int i = 0; i < row.Count; i++)
            {
                if (!row.IsAlive(i)) continue;
                float cx = Mathf.Clamp(px - row.blockers[i].localPosition.x, minX, maxX);
                float d = Mathf.Abs(cx - row.Center.x);
                if (d < bestD) { bestD = d; best = cx; }
            }
            return best;
        }

        /// <summary>残っている選手が球からいちばん離れる列の位置</summary>
        static float DodgeX(DBBlockerRow row, float px)
        {
            row.GetCenterRangeX(out float minX, out float maxX);
            float best = row.Center.x, bestScore = -1f;
            for (float cx = minX; cx <= maxX + 1e-3f; cx += 0.1f)
            {
                float m = float.MaxValue;
                for (int i = 0; i < row.Count; i++)
                {
                    if (!row.IsAlive(i)) continue;
                    m = Mathf.Min(m, Mathf.Abs(px - (cx + row.blockers[i].localPosition.x)));
                }
                float score = m - Mathf.Abs(cx - row.Center.x) * 0.05f;
                if (score > bestScore) { bestScore = score; best = cx; }
            }
            return best;
        }
    }
}
