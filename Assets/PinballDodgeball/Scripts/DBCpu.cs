using System;
using UnityEngine;

namespace PinballDodgeball
{
    /// <summary>
    /// CPU（VS CPU のとき P2）。強さごとの値は配列で [EASY, NORMAL, HARD]。
    /// ・フリッパー：自動で振る（反応の遅れ・空振り率・届く範囲）
    /// ・選手：飛んでくる相手の球をよける。キャッチは使わない
    /// ・サーブ：ルートと強さを選んで打つ
    /// ・外野ブロッカー：いつも全自動で投げる
    /// </summary>
    [Serializable]
    public class DBCpu
    {
        public float[] flipperReaction = { 0.12f, 0.06f, 0.02f };
        public float[] flipperMissChance = { 0.25f, 0.1f, 0.03f };
        public float[] flipperReach = { 0.35f, 0.5f, 0.6f };
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

        public void ResetState()
        {
            serveToken = -1;
            threatActive = false;
        }

        public void ConfigureFlipper(DBFlipper f, int level)
        {
            f.autoSwing = true;
            f.pressed = null;
            f.autoReaction = flipperReaction[level];
            f.autoMissChance = flipperMissChance[level];
            f.autoReach = flipperReach[level];
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

            // 選手：よける
            Vector3 rp = row.Center;
            Vector3 bp = ball.Position;
            Vector3 v = ball.Velocity;
            Vector3 fwd = row.Forward;
            float ahead = Vector3.Dot(bp - rp, fwd);        // 列の前にどれだけ離れているか
            float approach = -Vector3.Dot(v, fwd);          // こちらへ向かう速さ
            bool threat = !ball.IsHeld && ball.LastHitter == DBConst.Opponent(p) && approach > 1f && ahead > -0.3f;

            float targetX;
            if (threat)
            {
                if (!threatActive)
                {
                    threatActive = true;
                    threatNoise = UnityEngine.Random.Range(-1f, 1f) * dodgeNoise[lv];
                }
                float tt = Mathf.Max(0f, ahead) / approach;
                float px = Fold(bp.x + v.x * tt) + threatNoise;
                targetX = DodgeX(row, px);
            }
            else
            {
                threatActive = false;
                targetX = 0f;
            }

            s.move.x = Mathf.Clamp((targetX - rp.x) * 2f, -1f, 1f);
            float zTarget = row.startPosition.z;
            s.move.y = Mathf.Clamp((zTarget - rp.z) * 1f, -1f, 1f) * 0.5f;
            return s;
        }

        static float Fold(float x)
        {
            float w = DBConst.PlayHalfWidth - DBConst.BallRadius;
            for (int k = 0; k < 4 && Mathf.Abs(x) > w; k++) x = Mathf.Sign(x) * 2f * w - x;
            return x;
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
