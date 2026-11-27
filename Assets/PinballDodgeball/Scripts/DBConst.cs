using UnityEngine;

namespace PinballDodgeball
{
    /// <summary>
    /// ドッジボールルールの台の寸法。
    /// プレイヤー番号：P1 = 0（-Z 側が自陣）、P2 = 1（+Z 側。P1 の点対称）。
    /// 画面は真上から見下ろし（画面の上 = +Z、右 = +X）。
    /// </summary>
    public static class DBConst
    {
        // 盤面
        // 台を横に広げた倍率（ステップ11：盤面の幅 12 → 18。選手の動ける範囲 BlockerAreaHalfX は広げない）
        public const float WidthScale = 1.5f;
        public const float PlayHalfWidth = 6f * WidthScale;      // 外壁の内面 X ±9
        // 台を縦に広げた量（ステップ8。ゴール側の壁までの長さ）
        public const float ExtraLength = 1.5f;
        public const float PlayHalfLength = 12f + ExtraLength;    // ゴール側の壁の内面 Z ±13.5
        public const float WallThickness = 0.3f;
        public const float WallHeight = 0.6f;

        // 球
        public const float BallRadius = 0.25f;
        public const float BallY = 0.25f;

        // ---- 背面の加速レーン（2026-11-27。図面：Docs/図面_ドッジボールの背面レーン.svg。どれも点対称） ----
        // 半円の中心は (0, 自陣の向き × BackLaneCenterZ)。外側の半径 = 横の壁と奥の壁に接する、内側の半径との差がレーンの幅
        public const float BackLaneCenterZ = PlayHalfLength - BackLaneOuterR;   // 4.5
        public const float BackLaneOuterR = PlayHalfWidth;                      // 9.0
        public const float BackLaneInnerR = BackLaneOuterR - 0.9f;              // 8.1（幅 0.9）
        public const float BackLaneWallThickness = 0.15f;
        public const float BackLaneExitZ = 2.6f;          // まっすぐな部分の終わり（出口。中央の線から）
        public const float BackLaneGapHalf = 0.9f;        // 入口（内側の壁の、奥の真ん中のすき間）の幅の半分
        public const float BackLaneSegmentDeg = 7.5f;     // 加速の帯・壁を分ける角度
        public const float BackLaneExitAngle = 25f;       // 出口で内側へ向ける角度
        public static float BackLaneMidR => (BackLaneOuterR + BackLaneInnerR) * 0.5f;

        /// <summary>
        /// 背面の加速レーンの真ん中の線の上の点。territory = どちらの陣地か、phi = 奥の真ん中から横への角度（-90〜90°。+ は +X 側）、r = 半径
        /// </summary>
        public static Vector3 BackLanePoint(int territory, float phiDeg, float r)
        {
            float zs = -Sign(territory);
            float a = phiDeg * Mathf.Deg2Rad;
            return new Vector3(r * Mathf.Sin(a), BallY, zs * (BackLaneCenterZ + r * Mathf.Cos(a)));
        }

        /// <summary>背面の加速レーンで、出口へ向かう向き（phi の点で）</summary>
        public static Vector3 BackLaneTangent(int territory, float phiDeg)
        {
            float zs = -Sign(territory);
            float a = phiDeg * Mathf.Deg2Rad;
            float side = phiDeg >= 0f ? 1f : -1f;
            return new Vector3(side * Mathf.Cos(a), 0f, -zs * Mathf.Abs(Mathf.Sin(a))).normalized;
        }

        // 背面のバンパー（入口の左右に 2 つずつ）。レーンの半円の中心から見た角度（奥の真ん中から ±）と、内側の壁とのすき間で決める
        // 2026-11-27：すき間 約 0.7 → 2.0（狭すぎたため。前の位置は (±2.4, 11.0)、(±4.8, 9.6)）
        public static readonly float[] BackBumperAngles = { 20f, 43f };
        public const float BackBumperWallGap = 2.0f;

        /// <summary>背面のバンパーの中心。territory = どちらの陣地か、side = 画面の左 -1 / 右 +1、k = 何番目（0 = 入口に近い方）</summary>
        public static Vector3 BackBumperPosition(int territory, int side, int k)
        {
            float r = BackLaneInnerR - BackLaneWallThickness - BumperRadius - BackBumperWallGap;
            return BackLanePoint(territory, side * BackBumperAngles[k], r);
        }

        // バンパー（中央の線の左右の壁にくっつけて 1 つずつ）
        public const float BumperRadius = 0.45f;
        public const float BumperX = PlayHalfWidth - BumperRadius;
        public const float BumperZ = 0f;

        // ドロップターゲット（各陣地の左右の壁ぎわに 3 つずつ。中央の線のバンパーのすぐゴール側）
        public const int DropTargetCount = 3;
        public const float DropTargetWidth = 0.45f;     // Z 方向
        public const float DropTargetThickness = 0.25f; // X 方向（壁から）
        public const float DropTargetHeight = 0.4f;
        public const float DropTargetGap = 0.1f;
        public const float DropTargetFirstZ = 0.95f;    // 1 つ目の中心（中央の線からの距離）

        // コースターのレーンがある側（P1 のレーンが画面の左 = -1。P2 のレーンは点対称で反対側）
        public const int CoasterSide = -1;

        // スピナー（ステップ19：中立地帯に 1 つ。真ん中は避け、コースターのない帯（画面の右）の壁寄り。
        // 両者のコースターへの打球の線（中立地帯で X ±5.3〜±6.4）と外野（X ±5.2）から離す。通った球の向きを少しランダムにぶらす）
        public const float SpinnerX = 7.5f;          // 中心の X（の大きさ。符号は -CoasterSide）
        public const float SpinnerZ = 0f;
        public const float SpinnerWidth = 0.7f;      // ステップ19：0.9 → 0.7（壁ぎわのバンパーとの間に収める）
        public const float SpinnerPostRadius = 0.08f;

        /// <summary>スピナーの中心（中立地帯に 1 つ）</summary>
        public static Vector3 SpinnerCenter() => new Vector3(-CoasterSide * SpinnerX, BallY, SpinnerZ);

        // コースターのレーン（ステップ19：自分のレーンは相手の陣地に置く。入口も相手の陣地）
        // P1 のレーン：右のフリッパーで左へ寄せて打った球（真上から 約31°。入るのは 約28〜32°）の線の先、
        // 相手の陣地（Z +1.3〜+2.3）の左の帯に入口。そこから壁ぞいに相手のゴール側へ上り、終点の射出装置から相手の内野の選手を横・後ろから撃つ。
        // P2 のレーンは点対称（X と Z を反転）：P2 の右のフリッパー（X -1.77）から、P1 の陣地の右の帯へ。
        // 打球の線（P1）：(0.05, -10) から 真上から左へ CoasterShotAngle°。入口はこの線の上に、同じ向きで置く
        // レールの輪（BaseFix03 → BaseFix04 で回る向きを逆に）：入口のあと、外側（壁・発射レーン側）へ平面で 1 回転する
        // （半径 1.35、X -10.1〜-7.4、Z 2.95〜4.83）。輪は壁と相手の発射レーン・扉の上を通る（レールと支柱は見た目だけなので、サーブの球はじゃましない）。
        // 上りながら回るので、交差するところは後から通る側（高さ 約 1.5）が行きのレール（約 0.8）の上を越える
        // 終点（BaseFix06）：レールは BaseFix04 の形に戻し、射出装置 (-7.3, 5.85) に丸いドームの籠をかぶせる（レールの終わりの向きを隠す）。
        // 籠の口は相手の内野の真ん中へ向ける。籠は見た目だけ（当たり判定なし）
        public const float CoasterShotAngle = 31f;
        public static readonly Vector3[] CoasterPathP1 =
        {
            new Vector3(-6.0f, BallY, 0.2f),    // 入口の始まり（中央側）。2026-11-27：投げた球で届くよう、中立地帯まで延ばした（前は (-6.74, 1.3)）
            new Vector3(-7.34f, 0.45f, 2.3f),   // 入口の終わり（ここから上る）
            new Vector3(-7.45f, 0.6f, 2.65f),
            new Vector3(-7.4f, 0.7f, 2.95f),   // 輪の始まり（高さ 0.7）。ここから外側（壁・発射レーン側）へ回る
            new Vector3(-7.5f, 0.76f, 3.58f),
            new Vector3(-7.8f, 0.81f, 4.14f),
            new Vector3(-8.23f, 0.87f, 4.55f),
            new Vector3(-8.75f, 0.92f, 4.77f),
            new Vector3(-9.27f, 0.98f, 4.79f),
            new Vector3(-9.7f, 1.04f, 4.61f),
            new Vector3(-10f, 1.09f, 4.29f),
            new Vector3(-10.1f, 1.15f, 3.89f),   // 輪のいちばん外側（発射レーンの上）
            new Vector3(-10f, 1.21f, 3.49f),
            new Vector3(-9.7f, 1.26f, 3.17f),
            new Vector3(-9.27f, 1.32f, 3f),
            new Vector3(-8.75f, 1.38f, 3.01f),
            new Vector3(-8.23f, 1.43f, 3.23f),
            new Vector3(-7.8f, 1.49f, 3.64f),
            new Vector3(-7.5f, 1.54f, 4.2f),
            new Vector3(-7.4f, 1.6f, 4.83f),   // 輪の終わり（高さ 1.6。行きのレールの上を越える）
            new Vector3(-7.2f, 1.25f, 5.3f),
            new Vector3(-6.8f, 0.65f, 5.9f),
            new Vector3(-6.4f, BallY, 6.5f),    // 終点：射出装置（籠の中）。2026-11-27：(-7.3, 5.85) → 内側へ（背面の加速レーンの内側の壁に重なるため）
        };
        public const float CoasterEntranceWidth = 2.2f; // 2026-11-27：1.4 → 2.2（投げた球で入れやすく）
        // 射出装置の籠（BaseFix06）：格子の半球。口（開いている側）は相手の内野の真ん中を向く
        public const float CoasterCageRadius = 0.85f;
        public const float CoasterCageMouthHalfAngle = 55f;  // 口の開き（片側の角度）
        public const float CoasterCageMouthTop = 55f;        // 口の高さ（仰角。これより上は格子でふさぐ）
        public const float CoasterCageBar = 0.045f;          // 格子の太さ

        /// <summary>owner のレーンの通り道（ワールド座標）。P2 は P1 の点対称</summary>
        public static Vector3[] CoasterPath(int owner)
        {
            var src = CoasterPathP1;
            var dst = new Vector3[src.Length];
            float s = owner == 0 ? 1f : -1f;
            for (int i = 0; i < src.Length; i++) dst[i] = new Vector3(src[i].x * s, src[i].y, src[i].z * s);
            return dst;
        }

        // 外野ブロッカーの置き場所（ステップ19：壁ぎわ → 選手の範囲のすぐ外。相手陣地の左右に 2 か所ずつ）
        // Z は内野の奥行き（1.0〜6.05）を 3 等分した 2 点
        public const float OutfieldX = 5.2f;
        public static readonly float[] OutfieldZ = { 2.7f, 4.4f };

        // ブロッカー（選手 3 人）
        public const float BlockerAreaHalfX = 4.2f;
        // ステップ17：内野どうしの間（中立地帯）を 1.0 → 2.0 に。範囲をゴール側へ 0.5 ずつずらした（奥行き 5.05 はそのまま）
        public const float BlockerAreaNearZ = 1.0f;   // 中央側の端
        public const float BlockerAreaGoalZ = 6.05f;  // ゴール側の端
        public const float BlockerStartZ = 3.5f;
        public const int BlockerCount = 3;
        public const float BlockerSpacing = 2.0f;     // 中心どうし
        public const float BlockerLength = 1.0f;
        public const float BlockerRadius = 0.25f;

        // 発射レーン（P1 は +X、P2 は -X）
        public const float LaneInnerX = PlayHalfWidth + WallThickness;   // 外壁の外側の面
        public const float LaneOuterX = LaneInnerX + 0.8f;               // レーンの幅 0.8
        public const float ServeOwnExitZ = 3.6f;     // 自陣側の出入り口
        public const float ServeOppExitZ = 3.2f;     // 相手陣地側の出入り口（2026-11-27：2.64 → 3.2。背面の加速レーンのまっすぐな部分に開く）
        public const float DoorWidth = 1.0f;
        public const float PlungerZ = PlayHalfLength - BallRadius - 0.05f;

        public static float Sign(int player) => player == 0 ? 1f : -1f;
        public static int Opponent(int player) => 1 - player;
        public static float LaneCenterX(int player) => Sign(player) * (LaneInnerX + LaneOuterX) * 0.5f;
        public static float OuterHalfWidth => LaneOuterX + WallThickness;
        public static string PlayerName(int player, bool cpu) => cpu ? "CPU" : (player == 0 ? "P1" : "P2");
    }
}
