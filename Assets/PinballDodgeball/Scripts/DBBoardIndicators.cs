using System.Collections.Generic;
using UnityEngine;

namespace PinballDodgeball
{
    /// <summary>
    /// 盤面の上に描く表示（LineRenderer）。同じ画面を見ている 2 人の両方に見える。
    /// ・ねらいの矢印：球を持っている間（内野のキャッチ中・外野・シュートポスト・コースターの射出装置）、投げる向きへ持ち主の色の矢印。パワーショットになる球は長く太く
    /// ・投げるまでの残り時間：球の周りの円が、自動で投げる（撃つ）までの残りに合わせて欠けていく
    /// ・動ける範囲：選手の動ける範囲の枠。動かしている間は明るく
    /// ・キャッチの構え：B を押している間、残っている選手の周りが脈打つ
    /// ・サーブ：選んだ出入り口が光る。レーンに「いまの強さで届くところ」までの線（届くなら持ち主の色、届かないなら赤）
    /// GameManager と同じオブジェクトに自動で付く。
    /// </summary>
    public class DBBoardIndicators : MonoBehaviour
    {
        [Header("全体")]
        public bool showAim = true;
        public bool showTimer = true;
        public bool showArea = true;
        public bool showCatchStance = true;
        public bool showServe = true;
        [Tooltip("床からの高さ（床の目印より少し上）")]
        public float height = 0.03f;

        [Header("ねらいの矢印")]
        public float arrowLength = 2.0f;
        public float powerArrowLength = 3.0f;
        public float arrowWidth = 0.1f;
        public float powerArrowWidth = 0.16f;

        [Header("残り時間の円")]
        public float timerRadius = 0.42f;
        public float timerWidth = 0.06f;

        [Header("動ける範囲・キャッチの構え")]
        [Range(0f, 1f)] public float areaAlpha = 0.22f;
        [Range(0f, 1f)] public float areaAlphaMoving = 0.6f;
        public float areaWidth = 0.05f;
        public float stanceMargin = 0.12f;
        public float stanceWidth = 0.06f;

        [Header("サーブ")]
        public float laneLineWidth = 0.18f;
        public Color faultColor = new Color(1f, 0.3f, 0.3f);

        DBGameManager gm;
        Material lineMat;
        readonly List<LineRenderer> pool = new List<LineRenderer>();
        int used;

        void Start()
        {
            gm = GetComponent<DBGameManager>();
            if (gm == null) gm = DBGameManager.Instance;
            var sh = Shader.Find("Sprites/Default");
            lineMat = sh != null ? new Material(sh) : null;
        }

        void LateUpdate()
        {
            used = 0;
            if (gm != null && gm.ball != null && gm.IsPlaying)
            {
                if (showArea || showCatchStance) DrawRows();
                if (showServe && gm.CurrentState == DBGameManager.State.Serve) DrawServe();
                if (showAim || showTimer) DrawHolder();
            }
            for (int i = used; i < pool.Count; i++) pool[i].enabled = false;
        }

        // ------------------------------------------------------------------

        void DrawHolder()
        {
            var ball = gm.ball;
            if (!ball.IsHeld) return;
            Vector3 bp = DBUtil.Flat(ball.Position);

            int owner = -1;
            Vector3 dir = Vector3.zero;
            float remain = 0f;
            bool power = false;

            foreach (var row in gm.rows)
            {
                if (row == null || !row.IsCatching) continue;
                owner = row.owner; dir = row.AimDir; remain = row.CatchRemaining01; power = row.IsJustCatch;
            }
            if (owner < 0)
            {
                var of = gm.HoldingOutfield();
                if (of != null) { owner = of.owner; dir = of.AimDir; remain = of.HoldRemaining01; }
            }
            if (owner < 0)
            {
                foreach (var sp in gm.shootPosts)
                {
                    if (sp == null || !sp.IsLoaded) continue;
                    owner = sp.owner; dir = sp.AimDir; remain = sp.LoadRemaining01; power = true;
                }
            }
            if (owner < 0 && gm.coasters != null)
            {
                foreach (var co in gm.coasters)
                {
                    if (co == null || !co.IsCharging) continue;
                    owner = co.owner; dir = co.AimDir; remain = co.ChargeRemaining01; power = co.powerShot;
                }
            }
            if (owner < 0 || dir.sqrMagnitude < 1e-4f) return;

            Color c = gm.PlayerColor(owner);
            if (showAim)
            {
                float len = power ? powerArrowLength : arrowLength;
                float w = power ? powerArrowWidth : arrowWidth;
                Vector3 from = bp + dir * (DBConst.BallRadius + 0.05f);
                Vector3 to = bp + dir * len;
                Line(new[] { from, to }, w, c, Alpha(c, 0.25f));
                Vector3 side = new Vector3(-dir.z, 0f, dir.x);
                float head = power ? 0.4f : 0.3f;
                Line(new[] { to - dir * head + side * head * 0.7f, to, to - dir * head - side * head * 0.7f }, w, c, c);
            }
            if (showTimer && remain > 0f)
            {
                Arc(bp, timerRadius, remain, timerWidth, Color.Lerp(Color.white, c, 0.5f));
            }
        }

        void DrawRows()
        {
            float t = Time.time;
            foreach (var row in gm.rows)
            {
                if (row == null) continue;
                Color c = gm.PlayerColor(row.owner);

                if (showArea)
                {
                    row.GetArea(out float hx, out float z1, out float z2);
                    bool moving = gm.GetInput(row.owner).move.sqrMagnitude > 0.01f;
                    var col = Alpha(c, moving ? areaAlphaMoving : areaAlpha);
                    Line(new[]
                    {
                        new Vector3(-hx, 0f, z1), new Vector3(hx, 0f, z1), new Vector3(hx, 0f, z2),
                        new Vector3(-hx, 0f, z2), new Vector3(-hx, 0f, z1),
                    }, areaWidth, col, col);
                }

                if (showCatchStance && row.IsCatchArmed)
                {
                    float pulse = 0.55f + 0.45f * Mathf.Sin(t * 18f);
                    var col = Alpha(Color.Lerp(c, Color.white, 0.3f), pulse);
                    float half = row.blockerLength * 0.5f - row.blockerRadius;
                    float r = row.blockerRadius + DBConst.BallRadius * 0.5f + stanceMargin * (0.8f + 0.2f * pulse);
                    for (int i = 0; i < row.Count; i++)
                    {
                        if (!row.IsAlive(i)) continue;
                        Stadium(DBUtil.Flat(row.blockers[i].position), half, r, stanceWidth, col);
                    }
                }
            }
        }

        void DrawServe()
        {
            int p = gm.Server;
            var lane = gm.lanes[p];
            if (lane == null) return;
            Color c = gm.PlayerColor(p);
            float sg = DBConst.Sign(p);
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 8f);

            // 出入り口：選んだ方が光る、選んでいない方は暗く
            float wallX = Mathf.Sign(lane.laneX) * (DBConst.PlayHalfWidth + DBConst.WallThickness * 0.5f);
            DoorBox(wallX, lane.ownExitZ, !gm.RouteOpponent ? Alpha(c, 0.5f + 0.5f * pulse) : new Color(1f, 1f, 1f, 0.2f));
            DoorBox(wallX, lane.oppExitZ, gm.RouteOpponent ? Alpha(c, 0.5f + 0.5f * pulse) : new Color(1f, 1f, 1f, 0.2f));

            if (gm.ServeLaunched) return;

            // いまの強さで届くところ（台の傾きで減速：届く距離 = v² / 2a。中央の山を越えたら奥まで）
            float power = gm.Charging ? gm.CurrentPower : gm.minPower;
            float a = Mathf.Max(0.01f, gm.ball.slopeAcceleration);
            float dist = power * power / (2f * a);
            float startZ = lane.plungerPosition.z;
            float crest = Mathf.Abs(startZ);
            float endZ = dist >= crest ? sg * (DBConst.PlayHalfLength - DBConst.BallRadius) : startZ + sg * dist;
            float exitZ = gm.RouteOpponent ? lane.oppExitZ : lane.ownExitZ;
            bool reaches = (endZ - exitZ) * sg >= 0f;
            Color lc = reaches ? c : faultColor;
            if (!gm.Charging) lc = Alpha(lc, 0.35f);
            Line(new[] { new Vector3(lane.laneX, 0f, startZ), new Vector3(lane.laneX, 0f, endZ) }, laneLineWidth, Alpha(lc, lc.a * 0.4f), lc);
            // 届く先の横線
            Line(new[] { new Vector3(lane.laneX - 0.35f, 0f, endZ), new Vector3(lane.laneX + 0.35f, 0f, endZ) }, 0.08f, lc, lc);
        }

        void DoorBox(float x, float z, Color col)
        {
            float hx = 0.3f, hz = DBConst.DoorWidth * 0.5f;
            Line(new[]
            {
                new Vector3(x - hx, 0f, z - hz), new Vector3(x + hx, 0f, z - hz), new Vector3(x + hx, 0f, z + hz),
                new Vector3(x - hx, 0f, z + hz), new Vector3(x - hx, 0f, z - hz),
            }, 0.07f, col, col);
        }

        // ------------------------------------------------------------------
        // 描く部品

        static Color Alpha(Color c, float a) => new Color(c.r, c.g, c.b, a);

        LineRenderer Next()
        {
            if (used >= pool.Count)
            {
                var go = new GameObject("Indicator Line");
                go.transform.SetParent(transform, false);
                var lr = go.AddComponent<LineRenderer>();
                lr.useWorldSpace = true;
                lr.numCapVertices = 2;
                lr.numCornerVertices = 2;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.receiveShadows = false;
                if (lineMat != null) lr.sharedMaterial = lineMat;
                pool.Add(lr);
            }
            var r = pool[used++];
            r.enabled = true;
            return r;
        }

        void Line(Vector3[] pts, float width, Color start, Color end)
        {
            var lr = Next();
            lr.positionCount = pts.Length;
            for (int i = 0; i < pts.Length; i++) lr.SetPosition(i, new Vector3(pts[i].x, height, pts[i].z));
            lr.startWidth = width;
            lr.endWidth = width;
            lr.startColor = start;
            lr.endColor = end;
        }

        /// <summary>中心 c、半径 r の円のうち、真上（画面の上）から時計回りに fraction だけ</summary>
        void Arc(Vector3 c, float r, float fraction, float width, Color col)
        {
            int n = Mathf.Max(2, Mathf.CeilToInt(48 * Mathf.Clamp01(fraction)) + 1);
            var pts = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                float a = (i / (float)(n - 1)) * fraction * Mathf.PI * 2f;
                pts[i] = c + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * r;
            }
            Line(pts, width, col, col);
        }

        /// <summary>X 向きのカプセルの輪郭（中心 c、まっすぐな部分の半分 half、半径 r）</summary>
        void Stadium(Vector3 c, float half, float r, float width, Color col)
        {
            const int seg = 10;
            var pts = new List<Vector3>();
            for (int i = 0; i <= seg; i++)
            {
                float a = -Mathf.PI * 0.5f + Mathf.PI * i / seg;          // 右の半円
                pts.Add(c + new Vector3(half + Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r));
            }
            for (int i = 0; i <= seg; i++)
            {
                float a = Mathf.PI * 0.5f + Mathf.PI * i / seg;           // 左の半円
                pts.Add(c + new Vector3(-half + Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r));
            }
            pts.Add(pts[0]);
            Line(pts.ToArray(), width, col, col);
        }
    }
}
