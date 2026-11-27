using UnityEngine;

namespace Pinball
{
    /// <summary>
    /// CPUプレイヤー。自分のフリッパーの前にボールが来たら、反応の遅れと空振り率つきで弾く。
    /// </summary>
    public class CpuPlayer : MonoBehaviour
    {
        public enum Level { Easy, Normal, Hard }

        public int playerIndex = 1;
        public Level level = Level.Normal;
        public Ball ball;
        public Flipper[] flippers;
        public Plunger plunger;
        public BlockerRow blockerRow;

        [Tooltip("1回押したらこの秒数だけ押し続ける")]
        public float holdTime = 0.2f;

        class FlipperState
        {
            public float pressAt = -1f;     // この時刻に押す予定（-1 = 予定なし）
            public float pressUntil;        // この時刻まで押し続ける
            public float cooldownUntil;     // この時刻までは次の判定をしない
        }

        FlipperState[] states;
        float nextBounceThink;

        // サーブ（プランジャー）の状態
        enum ServeStep { Idle, Waiting, Charging }
        ServeStep serveStep;
        float serveStartAt;
        float targetCharge;

        void OnEnable()
        {
            int n = flippers != null ? flippers.Length : 0;
            states = new FlipperState[n];
            for (int i = 0; i < n; i++) states[i] = new FlipperState();
            serveStep = ServeStep.Idle;
        }

        void OnDisable()
        {
            ReleaseAll();
            PinballInput.SetCpuPlunger(playerIndex, false);
            PinballInput.SetCpuMove(playerIndex, Vector2.zero);
        }

        /// <summary>
        /// 障害物の列：ボールが自陣にあるときは、近いほうの障害物をボールの X に合わせて盾にする。
        /// それ以外は列を中央に戻して待つ。強さで動きの速さが変わる。
        /// </summary>
        void UpdateBlockers()
        {
            if (blockerRow == null) return;

            Vector2 move = Vector2.zero;
            if (ball != null && ball.isActiveAndEnabled && !ball.IsFrozen)
            {
                float goalDir = playerIndex == 0 ? -1f : 1f;
                Vector3 bp = ball.transform.position;
                Vector3 rp = blockerRow.transform.position;

                float targetX = (blockerRow.minXZ.x + blockerRow.maxXZ.x) / 2f;
                if (bp.z * goalDir > 0f && blockerRow.blockers != null)
                {
                    float best = float.MaxValue;
                    foreach (var b in blockerRow.blockers)
                    {
                        if (b == null) continue;
                        float offset = b.position.x - rp.x;
                        float candidate = bp.x - offset;
                        float d = Mathf.Abs(candidate - rp.x);
                        if (d < best) { best = d; targetX = candidate; }
                    }
                }
                float targetZ = (blockerRow.minXZ.y + blockerRow.maxXZ.y) / 2f;

                float scale = level == Level.Easy ? 0.5f : level == Level.Hard ? 1f : 0.75f;
                move = new Vector2(Mathf.Clamp((targetX - rp.x) * 3f, -1f, 1f),
                                   Mathf.Clamp((targetZ - rp.z) * 3f, -1f, 1f)) * scale;

                // 跳ね返し：自陣のゴールへ向かう球がブロッカーのすぐ前まで来たら、確率で使う
                float toward = ball.Body.GetVelocity().z * goalDir;
                if (toward > 1f && Time.time >= nextBounceThink && blockerRow.blockers != null)
                {
                    foreach (var b in blockerRow.blockers)
                    {
                        if (b == null) continue;
                        Vector3 d = bp - b.position;
                        d.y = 0f;
                        bool inFront = d.z * goalDir < 0f; // ブロッカーより相手側にいる
                        if (inFront && Mathf.Abs(d.x) < blockerRow.blockerLength / 2f + 0.3f && Mathf.Abs(d.z) < 0.9f)
                        {
                            float chance = level == Level.Easy ? 0.15f : level == Level.Hard ? 0.7f : 0.4f;
                            if (Random.value < chance) PinballInput.RequestCpuBounce(playerIndex);
                            nextBounceThink = Time.time + 0.6f;
                            break;
                        }
                    }
                }
            }
            PinballInput.SetCpuMove(playerIndex, move);
        }

        void ReleaseAll()
        {
            if (flippers == null) return;
            foreach (var f in flippers)
                if (f != null) PinballInput.SetCpuPress(playerIndex, f.side, false);
        }

        /// <summary>溜める量の範囲。弱すぎるとフォルトになる（EASYはときどきフォルトする）</summary>
        void GetServeRange(out float min, out float max)
        {
            switch (level)
            {
                case Level.Easy: min = 0.30f; max = 0.90f; break;
                case Level.Hard: min = 0.60f; max = 0.85f; break;
                default: min = 0.48f; max = 0.95f; break;
            }
        }

        void UpdateServe()
        {
            var gm = GameManager.Instance;
            bool myServe = gm != null && plunger != null && gm.CanPlunge(playerIndex);
            if (!myServe)
            {
                serveStep = ServeStep.Idle;
                PinballInput.SetCpuPlunger(playerIndex, false);
                return;
            }

            float now = Time.time;
            bool hold = false;

            switch (serveStep)
            {
                case ServeStep.Idle:
                    if (plunger.HasBallReady)
                    {
                        // ルートを決める（強いほど相手陣地へ送る割合が高い）
                        float oppChance = level == Level.Easy ? 0.2f : level == Level.Hard ? 0.5f : 0.35f;
                        bool opp = Random.value < oppChance;
                        gm.SetRoute(playerIndex, opp ? ServeRoute.OpponentField : ServeRoute.OwnField);

                        // 相手陣地ルートはレーンが長いので強めに溜める
                        GetServeRange(out float min, out float max);
                        if (opp) { min = Mathf.Min(min + 0.15f, 0.95f); max = 1f; }
                        targetCharge = Random.Range(min, max);
                        serveStartAt = now + Random.Range(0.6f, 1.3f);
                        serveStep = ServeStep.Waiting;
                    }
                    break;

                case ServeStep.Waiting:
                    if (!plunger.HasBallReady) serveStep = ServeStep.Idle;
                    else if (now >= serveStartAt) { serveStep = ServeStep.Charging; hold = true; }
                    break;

                case ServeStep.Charging:
                    hold = plunger.Charge < targetCharge;
                    if (!hold) serveStep = ServeStep.Idle; // 離す → 発射。戻ってきたら次の判定へ
                    break;
            }

            PinballInput.SetCpuPlunger(playerIndex, hold);
        }

        void GetParams(out float reaction, out float missChance, out float reachBonus)
        {
            switch (level)
            {
                case Level.Easy: reaction = 0.10f; missChance = 0.30f; reachBonus = -0.15f; break;
                case Level.Hard: reaction = 0.00f; missChance = 0.03f; reachBonus = 0.10f; break;
                default: reaction = 0.05f; missChance = 0.12f; reachBonus = 0f; break;
            }
        }

        void Update()
        {
            UpdateServe();
            UpdateBlockers();

            if (states == null || flippers == null) return;
            if (ball == null || !ball.isActiveAndEnabled || ball.IsFrozen)
            {
                ReleaseAll();
                return;
            }

            GetParams(out float reaction, out float missChance, out float reachBonus);

            float now = Time.time;
            Vector3 bp = ball.transform.position;
            Vector3 v = ball.Body.GetVelocity();
            float goalDir = playerIndex == 0 ? -1f : 1f; // 自分のゴールの方向（Z）

            for (int i = 0; i < flippers.Length; i++)
            {
                var f = flippers[i];
                var s = states[i];
                if (f == null) continue;

                bool pressing = now < s.pressUntil;

                if (!pressing && s.pressAt >= 0f && now >= s.pressAt)
                {
                    s.pressAt = -1f;
                    s.pressUntil = now + holdTime;
                    s.cooldownUntil = s.pressUntil + 0.12f;
                    pressing = true;
                }
                else if (!pressing && s.pressAt < 0f && now >= s.cooldownUntil &&
                         ShouldFlip(f, bp, v, goalDir, reachBonus))
                {
                    if (Random.value < missChance)
                        s.cooldownUntil = now + 0.5f;      // 空振り（見逃し）
                    else
                        s.pressAt = now + reaction;
                }

                PinballInput.SetCpuPress(playerIndex, f.side, pressing);
            }
        }

        bool ShouldFlip(Flipper f, Vector3 ballPos, Vector3 v, float goalDir, float reachBonus)
        {
            Vector3 pivot = f.transform.position;

            // フリッパーの付け根から中央方向へ、どれだけ離れているか（フリッパーに沿った方向）
            float lateral = f.side == FlipperSide.Top ? ballPos.x - pivot.x : pivot.x - ballPos.x;
            if (lateral < -0.4f || lateral > f.length + 0.3f + reachBonus) return false;

            // 付け根より中央側（前方）に何ユニットいるか
            float front = (pivot.z - ballPos.z) * goalDir;

            // ゴール方向への速度。速いほど早めに振る
            float toward = v.z * goalDir;
            if (toward < -0.5f) return false; // 遠ざかっている

            float trigger = 0.35f + Mathf.Max(toward, 0f) * 0.05f + reachBonus;
            return front >= -0.8f && front <= trigger;
        }
    }
}
