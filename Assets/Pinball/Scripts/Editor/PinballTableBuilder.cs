using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_6000_0_OR_NEWER
using PhysMat = UnityEngine.PhysicsMaterial;
using PhysCombine = UnityEngine.PhysicsMaterialCombine;
#else
using PhysMat = UnityEngine.PhysicMaterial;
using PhysCombine = UnityEngine.PhysicMaterialCombine;
#endif

namespace Pinball.EditorTools
{
    /// <summary>
    /// メニュー「Tools > Pinball > Build Versus Table」で、今開いているシーンに対戦台を丸ごと生成する。
    ///
    /// 座標系: 台は XZ 平面（幅 X: -5〜5、奥行 Z: -10〜10）。
    ///   -Z 端 = P1 のゴール（画面左）、+Z 端 = P2/CPU のゴール（画面右）。
    ///   カメラは真上から縦向きで見下ろす（P1 下・P2 上、縦で固定）。
    /// </summary>
    public static class PinballTableBuilder
    {
        const string RootName = "PinballTable";
        const string AssetDir = "Assets/Pinball/Generated";

        const float WallHeight = 1f;
        const float WallThickness = 0.3f;
        const float FlipperLength = 1.4f;
        // ステップ23：フリッパーの防御が強すぎたので、付け根を外へ 0.15 ずらして真ん中の隙間を広げた
        //   （1.7 → 1.85。下げているときの先端どうしの隙間 約 0.86 → 1.16、球の直径 0.5 の約 2.3 個分）。ガイド・スリングショットは自動で追従
        const float FlipperPivotX = 1.85f;  // 中心からの距離

        // ===== 台の大きさ（ステップ17で 幅10×長さ20 → 幅12×長さ24 に拡大。壁沿いのギミックを増やすため） =====
        const float PlayHalfWidth = 6.0f;    // 盤面は X: -6〜6
        const float PlayHalfLength = 12.0f;  // 盤面は Z: -12〜12（両端がゴール）
        const float FlipperPivotZ = PlayHalfLength - 3f;   // 中心からの距離（ゴールの 3 手前）

        // 外壁は盤面の端（厚み0.3、外側の面 = 盤面の端 + 0.3）
        const float SideWallX = PlayHalfWidth + 0.15f;

        // 発射レーン（P1 側の座標。P2 は点対称）。盤面の外、外壁と外枠の間を通る
        const float LaneInnerFace = PlayHalfWidth + 0.3f;   // 外壁の外側の面
        const float LaneOuterFace = PlayHalfWidth + 1.1f;   // 外枠の内側の面
        const float LaneCenterX = PlayHalfWidth + 0.7f;
        const float OwnExitZ = -PlayHalfLength * 0.3f;      // 自陣ルートのカーブの位置（自陣側）
        const float OppExitZ = PlayHalfLength * 0.22f;      // 相手陣地ルートのカーブの位置（レーン終点、相手陣地側）
        const float CurveRadius = 0.8f;     // カーブの内面の半径（中心は外壁の外側の面上）

        // 発射の強さ（レーンが長くなったので上げた。届く目安：自陣ルート ≈12.2、相手陣地ルート ≈14.8）
        const float LaunchMinSpeed = 9f;
        const float LaunchMaxSpeed = 25f;

        // アウトレーン（ステップ24）：外壁とスリングショットの間の細い通り道。入るとフリッパーを通らずにゴールへ落ちる
        const float OutlaneWidth = 0.8f;                                    // 外壁の内面 〜 仕切りの面（球の直径 0.5）
        const float OutlaneWallX = PlayHalfWidth - OutlaneWidth - 0.1f;     // 仕切り（厚み 0.2）の中心 = 5.1
        // インレーン（ステップ28）：ガイドに沿ってフリッパーへ下りる通路（スリングショットの代わり）
        const float InlaneWidth = 0.8f;

        // フリッパーへ導くガイド：アウトレーンの仕切り → フリッパーの付け根（傾きは以前と同じ 2.3/3.2）
        const float GuideSlope = 2.3f / 3.2f;
        static float GuideStartZ => -(FlipperPivotZ - 0.1f) + GuideSlope * (OutlaneWallX - (FlipperPivotX + 0.1f));

        // バンパー（線対称：各陣地の左右の壁沿いに1つずつ）
        // ステップ20：外壁との隙間（0.1）に球が挟まって壁との間で往復し続けたので、壁にくっつけて隙間をなくした
        //   （バンパーの端 6.05 が外壁の内面 6.0 に少し食い込む）。弾く向きも必ず盤面の内側へ寄せる（Bumper.inwardDirection）
        const float BumperX = PlayHalfWidth - 0.45f;
        const float BumperZ = -1.6f;

        // ターンテーブル（陣地ごとに1つ。P1 は左寄り、P2 は点対称で右寄り）
        const float TurntableX = -3.2f, TurntableZ = -3.0f, TurntableRadius = 0.9f;   // ステップ18で 1.3 → 0.9

        // ブロッカーの列（P1 側の座標。P2 は点対称）
        // ブロッカーそのものが動ける範囲（外形）。周りの部品との隙間がどこもボール直径 0.5 より広くなるようにしてある
        //   スリングショットまで 0.59、ガイドまで 0.56、バンパーまで 0.7、上げたフリッパーまで 1.85、相手の範囲まで 1.0
        //   （ステップ12の 6.5×4.5 → 8.4×5.8）
        //   ステップ24：アウトレーンでスリングショットが内側・下へ寄ったので、奥の端を -6.3 → -5.55（スリングショットまで 0.5 以上）。8.4×5.05
        //   ステップ28：スリングショット → インレーンの壁に置き換え（インレーンの壁まで 0.54）
        const float BlockerAreaXMin = -4.2f, BlockerAreaXMax = 4.2f;
        const float BlockerAreaZMin = -5.55f, BlockerAreaZMax = -0.5f;
        const float BlockerSpacing = 1.0f;  // 列の中心から各障害物の中心まで（2つの間はボールが通れる 1.0 の隙間）
        const float BlockerLength = 1.0f;   // 障害物（横向きのカプセル）の長さ
        const float BlockerRadius = 0.25f;

        static readonly Color P1Color = new Color(0.25f, 0.6f, 1f);
        static readonly Color P2Color = new Color(1f, 0.35f, 0.3f);

        [MenuItem("Tools/Pinball/Build Versus Table")]
        public static void Build()
        {
            // 作り直す前に、GameManager と効果音の Inspector 設定（イベント接続・入力・音量など）を控えておく
            string gmJson = null, audioJson = null, powerJson = null;
            var existing = GameObject.Find(RootName);
            if (existing != null)
            {
                var oldGm = existing.GetComponentInChildren<GameManager>(true);
                if (oldGm != null) gmJson = EditorJsonUtility.ToJson(oldGm);
                var oldAudio = existing.GetComponentInChildren<PinballAudio>(true);
                if (oldAudio != null) audioJson = EditorJsonUtility.ToJson(oldAudio);
                var oldPower = existing.GetComponentInChildren<PowerShot>(true);
                if (oldPower != null) powerJson = EditorJsonUtility.ToJson(oldPower);
                Undo.DestroyObjectImmediate(existing);
            }

            EnsureFolders();

            // --- マテリアル ---
            var mTable = Mat("Table", new Color(0.07f, 0.11f, 0.24f), 0f, 0.6f);
            var mWall = Mat("Wall", new Color(0.78f, 0.78f, 0.84f), 0.3f, 0.6f);
            var mFlipperP1 = Mat("FlipperP1", P1Color, 0f, 0.5f);
            var mFlipperP2 = Mat("FlipperP2", P2Color, 0f, 0.5f);
            var mBumper = Mat("Bumper", new Color(1f, 0.8f, 0.2f), 0f, 0.6f);
            var mBall = Mat("Ball", new Color(0.88f, 0.88f, 0.92f), 0.9f, 0.85f);
            var mLine = Mat("Line", new Color(0.5f, 0.55f, 0.75f), 0f, 0.3f);
            var mGate = Mat("Gate", new Color(0.6f, 1f, 0.7f), 0f, 0.5f);
            var mSlingFace = Mat("SlingFace", new Color(0.2f, 0.95f, 1f), 0f, 0.6f);
            var mSlingFill = Mat("SlingFill", new Color(0.12f, 0.3f, 0.45f), 0f, 0.4f);
            var mLamp = Mat("RolloverLamp", new Color(0.25f, 0.28f, 0.38f), 0f, 0.6f);
            var mTurn = Mat("Turntable", new Color(0.45f, 0.25f, 0.75f), 0.2f, 0.6f);
            var mTurnStripe = Mat("TurntableStripe", new Color(0.85f, 0.8f, 1f), 0f, 0.5f);
            var mPlunger = Mat("Plunger", new Color(0.3f, 0.85f, 0.45f), 0.2f, 0.5f);

            // --- 物理マテリアル ---
            var pFloor = PMat("Floor", 0f, 0.05f);
            var pWall = PMat("Wall", 0.5f, 0.05f);
            var pFlipper = PMat("Flipper", 0.2f, 0.4f);
            var pBumper = PMat("Bumper", 0.8f, 0f);
            var pBall = PMat("Ball", 0.3f, 0.05f);

            var root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Build Pinball Versus Table");
            var t = root.transform;

            // --- 床・装飾・見えない天井 ---
            Box("Floor", t, new Vector3(0f, -0.1f, 0f), new Vector3((LaneOuterFace + 0.4f) * 2f, 0.2f, PlayHalfLength * 2f), mTable, pFloor);
            Decal("CenterLine", t, new Vector3(0f, 0.005f, 0f), new Vector3(PlayHalfWidth * 2f, 0.01f, 0.06f), mLine);
            Decal("GoalLine_P1", t, new Vector3(0f, 0.005f, -PlayHalfLength + 0.1f), new Vector3(PlayHalfWidth * 2f, 0.01f, 0.2f), mFlipperP1);
            Decal("GoalLine_P2", t, new Vector3(0f, 0.005f, PlayHalfLength - 0.1f), new Vector3(PlayHalfWidth * 2f, 0.01f, 0.2f), mFlipperP2);

            var ceiling = new GameObject("GlassCeiling");
            ceiling.transform.SetParent(t, false);
            ceiling.transform.position = new Vector3(0f, 0.6f, 0f);
            ceiling.AddComponent<BoxCollider>().size = new Vector3((LaneOuterFace + 0.6f) * 2f, 0.1f, PlayHalfLength * 2f + 0.6f);

            // --- 各陣地（P2 は P1 を点対称に配置） ---
            var mats = new SideMaterials
            {
                wall = mWall, gate = mGate, plunger = mPlunger, bumper = mBumper,
                slingFace = mSlingFace, slingFill = mSlingFill, lamp = mLamp,
                turn = mTurn, turnStripe = mTurnStripe,
                pWall = pWall, pFlipper = pFlipper, pBumper = pBumper
            };
            var side1 = BuildSide(t, 0, mFlipperP1, mats);
            var side2 = BuildSide(t, 1, mFlipperP2, mats);

            // --- ボール ---
            var ballGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ballGo.name = "Ball";
            ballGo.transform.SetParent(t, false);
            ballGo.transform.position = side1.plunger.SpawnPoint;
            ballGo.transform.localScale = Vector3.one * 0.5f;
            ballGo.GetComponent<Renderer>().sharedMaterial = mBall;
            ballGo.GetComponent<Collider>().sharedMaterial = pBall;
            var ballRb = ballGo.AddComponent<Rigidbody>();
            ballRb.mass = 1f;
            var ball = ballGo.AddComponent<Ball>();
            side1.blockerRow.ball = ball;
            side2.blockerRow.ball = ball;

            // --- ゲーム管理 & CPU ---
            var gmGo = new GameObject("GameManager");
            gmGo.transform.SetParent(t, false);

            var cpu = gmGo.AddComponent<CpuPlayer>();
            cpu.playerIndex = 1;
            cpu.ball = ball;
            cpu.flippers = side2.flippers;
            cpu.plunger = side2.plunger;
            cpu.blockerRow = side2.blockerRow;
            cpu.enabled = false; // 試合開始時に GameManager が有効化する

            var audio = gmGo.AddComponent<PinballAudio>(); // 効果音（Inspector で音の差し替え・音量調整）
            if (audioJson != null) EditorJsonUtility.FromJsonOverwrite(audioJson, audio);

            var power = gmGo.AddComponent<PowerShot>(); // パワーショット（Inspector で溜まる量・効果を調整）
            if (powerJson != null) EditorJsonUtility.FromJsonOverwrite(powerJson, power);

            var gm = gmGo.AddComponent<GameManager>();
            if (gmJson != null) EditorJsonUtility.FromJsonOverwrite(gmJson, gm); // 前の設定を引き継ぐ（台の部品の参照は下で付け直す）
            gm.ball = ball;
            gm.cpu = cpu;
            gm.plungers = new[] { side1.plunger, side2.plunger };
            gm.diverters = new[] { side1.diverter, side2.diverter };
            gm.serveExitX = PlayHalfWidth - 0.3f; // 盤面の内側に入ったらサーブ完了

            SetupCamera();
            SetupLight();

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(root.scene);
            Selection.activeGameObject = root;
            Debug.Log("[Pinball] 対戦台を生成しました。Playでタイトル画面が出ます（P1: W/S・発射D・ルートA, P2: ↑/↓・発射←・ルート→, Esc: タイトル）");
        }

        // ===================== 陣地 =====================

        struct SideMaterials
        {
            public Material wall, gate, plunger, bumper, slingFace, slingFill, lamp, turn, turnStripe;
            public PhysMat pWall, pFlipper, pBumper;
        }

        struct SideResult
        {
            public BlockerRow blockerRow;
            public Flipper[] flippers;
            public Plunger plunger;
            public ServeDiverter diverter;
        }

        /// <summary>
        /// 1人分の陣地を作る。座標は P1（-Z 側）で書き、P2 は (x, z) → (-x, -z) の点対称にする。
        /// P1 は画面下側（+X）の外壁と、その外側の発射レーンを担当する。
        /// </summary>
        static SideResult BuildSide(Transform root, int player, Material flipperMat, SideMaterials m)
        {
            float s = player == 0 ? 1f : -1f;
            float yaw = player == 0 ? 0f : 180f;
            Vector2 P(float x, float z) => new Vector2(s * x, s * z);
            Vector3 P3(float x, float y, float z) => new Vector3(s * x, y, s * z);
            string tag = player == 0 ? "P1" : "P2";

            var side = new GameObject($"Side_{tag}").transform;
            side.SetParent(root, false);

            // ---- 外壁（盤面の端）。発射レーンの出口2か所だけ、開け閉めできる板にする ----
            // 切れ目は [カーブ位置 - 0.1, カーブ位置 + 1.3]。隣の壁は厚み分のびるので、実際に開くのはその内側
            float ownGapA = OwnExitZ - 0.1f, ownGapB = OwnExitZ + 1.3f;
            float oppGapA = OppExitZ - 0.1f, oppGapB = OppExitZ + 1.3f;
            var wall = new GameObject("SideWall").transform;
            wall.SetParent(side, false);
            Segment("Wall_1", wall, P(SideWallX, -PlayHalfLength), P(SideWallX, ownGapA), WallThickness, m.wall, m.pWall);
            Segment("Wall_2", wall, P(SideWallX, ownGapB), P(SideWallX, oppGapA), WallThickness, m.wall, m.pWall);
            Segment("Wall_3", wall, P(SideWallX, oppGapB), P(SideWallX, PlayHalfLength), WallThickness, m.wall, m.pWall);
            var ownGate = ExactSegment("Gate_OwnExit", wall, P(SideWallX, ownGapA), P(SideWallX, ownGapB), WallThickness, m.gate, m.pWall);
            var oppGate = ExactSegment("Gate_OppExit", wall, P(SideWallX, oppGapA), P(SideWallX, oppGapB), WallThickness, m.gate, m.pWall);

            // ---- 盤面の中：ガイドとフリッパー ----
            float px = FlipperPivotX, pz = FlipperPivotZ;
            Segment("Guide_A", side, P(-OutlaneWallX, GuideStartZ), P(-px - 0.1f, -pz + 0.1f), WallThickness, m.wall, m.pWall);
            Segment("Guide_B", side, P(OutlaneWallX, GuideStartZ), P(px + 0.1f, -pz + 0.1f), WallThickness, m.wall, m.pWall);

            // アウトレーン：入口のふた（自分の同じ側のブロッカーがダウンしている間だけ開く）＋床の目印
            CreateOutlane("Outlane_L", side, player, P, false, flipperMat, m);
            CreateOutlane("Outlane_R", side, player, P, true, flipperMat, m);

            // Y回転θで、ローカル+Xは (cosθ, 0, -sinθ) を向く
            // P1: 画面上側(-X)は 休止25°→押下-30°、画面下側(+X)は 155°→210°。P2 は 180° 回したもの。
            var f1 = CreateFlipper("Flipper_A", side, P3(-px, 0f, -pz), player,
                player == 0 ? FlipperSide.Top : FlipperSide.Bottom, 25f + yaw, -30f + yaw, flipperMat, m.pFlipper);
            var f2 = CreateFlipper("Flipper_B", side, P3(px, 0f, -pz), player,
                player == 0 ? FlipperSide.Bottom : FlipperSide.Top, 155f + yaw, 210f + yaw, flipperMat, m.pFlipper);

            // バンパー：左右の壁沿いに線対称で1つずつ（外壁との隙間 0.1 でボールは入らない）
            CreateBumper("Bumper_L", side, P3(-BumperX, 0f, BumperZ), m.bumper, m.pBumper);
            CreateBumper("Bumper_R", side, P3(BumperX, 0f, BumperZ), m.bumper, m.pBumper);

            // ターンテーブル：陣地ごとに1つ（P1 は左寄り。P2 は点対称なので右寄り）
            CreateTurntable(side, P3(TurntableX, 0f, TurntableZ), m.turn, m.turnStripe);

            // サイドレール：自陣の左の壁際の入口 → 壁の上を通って → 相手のブロッカーとフリッパーの間へ
            CreateSideRail($"SideRail_{tag}", side, player, P, flipperMat, m.wall);

            // インレーン（ガイドに沿ってフリッパーへ下りる細い通路）＋ロールオーバー。左右対称に2本
            // ※ステップ28：スリングショットは左右で球を打ち合ってラリーになるので廃止し、ここに置き換えた
            CreateInlane($"Inlane_{tag}_L", side, player, P, false, m);
            CreateInlane($"Inlane_{tag}_R", side, player, P, true, m);

            CreateGoal("Goal", side, P3(0f, 0f, -PlayHalfLength - 0.8f), player);

            // ---- 盤面の外：発射レーン ----
            var lane = new GameObject("Lane").transform;
            lane.SetParent(side, false);

            // 外枠（レーンの外側の壁）とストッパー
            Segment("Lane_Outer", lane, P(LaneOuterFace + 0.1f, -PlayHalfLength), P(LaneOuterFace + 0.1f, OppExitZ), 0.2f, m.wall, m.pWall);
            Segment("Lane_Stopper", lane, P(LaneInnerFace, -PlayHalfLength + 0.3f), P(LaneOuterFace, -PlayHalfLength + 0.3f), 0.2f, m.wall, m.pWall);

            // カーブ（90°）：上へ進むボールを盤面側（-X）へ曲げて、外壁の切れ目から入れる。
            // 中心は外壁の外側の面上なので、カーブが盤面の中に出っ張ることはない。
            var oppCurve = CreateCurve("Curve_Opp", lane, P, OppExitZ, m.wall, m.pWall);       // レーン終点（常に有効）
            var ownCurve = CreateCurve("Curve_Own", lane, P, OwnExitZ, m.gate, m.pWall);       // 自陣ルートのときだけ有効

            // プランジャー
            var plungerGo = new GameObject("Plunger");
            plungerGo.transform.SetParent(lane, false);
            plungerGo.transform.position = P3(LaneCenterX, 0.25f, -PlayHalfLength + 0.85f);
            plungerGo.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            var trigger = plungerGo.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(0.75f, 1f, 1.0f);
            var plunger = plungerGo.AddComponent<Plunger>();
            plunger.playerIndex = player;
            plunger.minLaunchSpeed = LaunchMinSpeed;
            plunger.maxLaunchSpeed = LaunchMaxSpeed;

            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "PlungerVisual";
            Object.DestroyImmediate(visual.GetComponent<Collider>());
            visual.transform.SetParent(plungerGo.transform, false);
            visual.transform.localPosition = new Vector3(0f, 0f, -0.95f);
            visual.transform.localScale = new Vector3(0.5f, 0.4f, 0.5f);
            visual.GetComponent<Renderer>().sharedMaterial = flipperMat;
            plunger.visual = visual.transform;

            // 切り替え装置
            var diverter = side.gameObject.AddComponent<ServeDiverter>();
            diverter.ownExitGate = ownGate.GetComponent<Collider>();
            diverter.opponentExitGate = oppGate.GetComponent<Collider>();
            diverter.ownRouteCurve = ownCurve;
            diverter.Close();

            // ---- 自陣の障害物（1列・2つ） ----
            var row = CreateBlockerRow($"BlockerRow_{tag}", side, player, P, P3, flipperMat, m);

            return new SideResult
            {
                blockerRow = row,
                flippers = new[] { f1, f2 },
                plunger = plunger,
                diverter = diverter,
            };
        }

        /// <summary>
        /// レーンの 90° カーブ。中心 (外壁の外側の面, exitZ)、0°（レーン外枠側）→ 90°（外壁の上）。
        /// ボールは半径 0.55 で回り、exitZ + 0.55 で -X 方向（盤面側）へ出る。
        /// </summary>
        /// <summary>
        /// インレーン。P1 側の左（-X）で座標を書き、mirrorX で右側にする。
        ///   外側（下）の壁 = 既存のガイド、内側（上）の壁 = ガイドと平行な「インレーンの壁」（中心どうしの間隔 1.05、通路の幅 0.8）
        ///   入口 = アウトレーンの仕切りの上端 W1 とインレーンの壁の上端 S の間（幅 約 1.07）、出口 = フリッパーの付け根の手前
        ///   インレーンの壁の上の面は、盤面側の球をフリッパーへ流す斜面も兼ねる
        ///   通路の途中の床にロールオーバー（トリガー＋ランプ）
        /// 確認値：ブロッカーの範囲まで 0.54、フリッパー（回る範囲）まで 1.3 以上
        /// </summary>
        static void CreateInlane(string name, Transform parent, int player, System.Func<float, float, Vector2> P, bool mirrorX, SideMaterials m)
        {
            float mx = mirrorX ? -1f : 1f;
            Vector2 Q(Vector2 v) => P(mx * v.x, v.y);

            float zs = GuideStartZ;
            var W = new Vector2(-OutlaneWallX, zs);                               // ガイドの始点（仕切りの下端）
            var W1 = new Vector2(-OutlaneWallX, zs + 0.9f);                       // 仕切りの上端
            var ge = new Vector2(-FlipperPivotX - 0.1f, -FlipperPivotZ + 0.1f);   // ガイドの終点
            var d = (ge - W).normalized;                                          // ガイドの向き（フリッパーへ）
            var n = new Vector2(-d.y, d.x);
            if (n.y < 0f) n = -n;                                                 // 盤面側（上）を向く法線
            const float offset = InlaneWidth + 0.25f;                             // ガイド（厚み 0.3）と壁（厚み 0.2）の中心どうし
            var S = W + n * offset + d * 0.7f;                                    // インレーンの壁の上端
            var E = W + n * offset + d * 2.8f;                                    // インレーンの壁の下端（フリッパーの手前）

            var root = new GameObject(name).transform;
            root.SetParent(parent, false);

            // アウトレーンとの仕切り（以前はスリングショットの外側の面が兼ねていた）
            Segment("OutlaneDivider", root, Q(W1), Q(W), 0.2f, m.wall, m.pWall);
            // インレーンの壁
            Segment("InlaneWall", root, Q(S), Q(E), 0.2f, m.wall, m.pWall);

            // 通路の床の目印（フリッパーへ向かう細い線）
            var mid = W + n * (0.15f + InlaneWidth / 2f);
            var a = Q(mid + d * 0.6f);
            var b = Q(mid + d * 3.2f);
            var line = Segment("InlaneLine", root, a, b, 0.06f, m.lamp, m.pWall);
            Object.DestroyImmediate(line.GetComponent<Collider>());
            line.transform.position = new Vector3(line.transform.position.x, 0.006f, line.transform.position.z);
            line.transform.localScale = new Vector3(0.06f, 0.01f, line.transform.localScale.z);

            // ロールオーバー（通路の途中）
            var rp = Q(mid + d * 1.7f);
            var go = new GameObject("Rollover");
            go.transform.SetParent(root, false);
            go.transform.position = new Vector3(rp.x, 0.25f, rp.y);
            var dir3 = new Vector3(Q(mid + d * 2f).x - rp.x, 0f, Q(mid + d * 2f).y - rp.y);
            go.transform.rotation = Quaternion.LookRotation(dir3.normalized, Vector3.up);
            var trig = go.AddComponent<BoxCollider>();
            trig.isTrigger = true;
            trig.size = new Vector3(InlaneWidth, 0.5f, 0.3f);

            var lamp = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            lamp.name = "Lamp";
            Object.DestroyImmediate(lamp.GetComponent<Collider>());
            lamp.transform.SetParent(go.transform, false);
            lamp.transform.localPosition = new Vector3(0f, -0.24f, 0f);
            lamp.transform.localScale = new Vector3(0.42f, 0.005f, 0.42f);
            lamp.GetComponent<Renderer>().sharedMaterial = m.lamp;
            var flash = lamp.AddComponent<HitFlash>();
            flash.flashColor = Color.white;
            flash.flashTime = 0.15f;

            var ro = go.AddComponent<Rollover>();
            ro.fieldOwner = player;
            ro.lamp = flash;
        }

        /// <summary>
        /// 床に置く凸多角形のメッシュ（上向きの片面）。シーンから消えないようアセットとして保存。
        /// ※ 表裏2面を同じ頂点で重ねると法線がゼロ → NaN → Bloom で画面が真っ白になるので、片面＋法線明示にしている。
        /// </summary>
        static Mesh PolygonMesh(string name, Vector2[] pts, float y)
        {
            // 上から見て（画面右 = +X、画面上 = +Z）時計回りにそろえる
            float area2 = 0f;
            for (int i = 0; i < pts.Length; i++)
            {
                var p0 = pts[i];
                var p1 = pts[(i + 1) % pts.Length];
                area2 += p0.x * p1.y - p1.x * p0.y;
            }
            if (area2 > 0f) System.Array.Reverse(pts);

            var verts = new Vector3[pts.Length];
            var normals = new Vector3[pts.Length];
            var uvs = new Vector2[pts.Length];
            for (int i = 0; i < pts.Length; i++)
            {
                verts[i] = new Vector3(pts[i].x, y, pts[i].y);
                normals[i] = Vector3.up;
                uvs[i] = pts[i];
            }
            var tris = new int[(pts.Length - 2) * 3];
            for (int i = 0; i < pts.Length - 2; i++)
            {
                tris[i * 3] = 0;
                tris[i * 3 + 1] = i + 1;
                tris[i * 3 + 2] = i + 2;
            }

            string path = $"{AssetDir}/Mesh_{name}.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool isNew = mesh == null;
            if (isNew) mesh = new Mesh { name = name };
            mesh.Clear();
            mesh.vertices = verts;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            if (isNew) AssetDatabase.CreateAsset(mesh, path);
            else EditorUtility.SetDirty(mesh);
            return mesh;
        }

        /// <summary>
        /// 自陣の障害物の列。2つの横向きカプセルをまとめて動かす。移動範囲の枠線も床に描く。
        /// </summary>
        static BlockerRow CreateBlockerRow(string name, Transform parent, int player,
            System.Func<float, float, Vector2> P, System.Func<float, float, float, Vector3> P3, Material mat, SideMaterials m)
        {
            float halfW = BlockerSpacing + BlockerLength / 2f;   // 列の半分の幅
            float halfD = BlockerRadius;                          // 列の半分の奥行
            float homeZ = (BlockerAreaZMin + BlockerAreaZMax) / 2f;

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = P3(0f, 0.25f, homeZ);

            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            var row = go.AddComponent<BlockerRow>();
            row.playerIndex = player;
            row.blockerLength = BlockerLength;
            row.blockerRadius = BlockerRadius;
            row.fullColor = player == 0 ? P1Color : P2Color; // HP 満タンの色 = プレイヤーの色

            // 列の中心が動ける範囲（P1 座標で求めてから P2 用に点対称へ）
            var c1 = P(BlockerAreaXMin + halfW, BlockerAreaZMin + halfD);
            var c2 = P(BlockerAreaXMax - halfW, BlockerAreaZMax - halfD);
            row.minXZ = Vector2.Min(c1, c2);
            row.maxXZ = Vector2.Max(c1, c2);

            var blockers = new Transform[2];
            for (int i = 0; i < 2; i++)
            {
                var cap = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                cap.name = $"Blocker_{i}";
                cap.transform.SetParent(go.transform, false);
                cap.transform.localPosition = new Vector3(i == 0 ? -BlockerSpacing : BlockerSpacing, 0f, 0f);
                cap.transform.localRotation = Quaternion.Euler(0f, 0f, 90f); // 軸を横（X）向きに
                cap.transform.localScale = new Vector3(BlockerRadius * 2f, BlockerLength / 2f, BlockerRadius * 2f);
                cap.GetComponent<Renderer>().sharedMaterial = mat;
                cap.GetComponent<Collider>().sharedMaterial = m.pWall;
                cap.AddComponent<HitFlash>().flashColor = Color.white; // 跳ね返し・キャッチで光る
                CreateBlockerGuard(cap.transform, player, m.wall);
                blockers[i] = cap.transform;
            }
            row.blockers = blockers;

            // 移動範囲の枠線（見た目だけ）
            var frame = new GameObject($"{name}_Area").transform;
            frame.SetParent(parent, false);
            var a = P(BlockerAreaXMin, BlockerAreaZMin);
            var b = P(BlockerAreaXMax, BlockerAreaZMin);
            var c = P(BlockerAreaXMax, BlockerAreaZMax);
            var d = P(BlockerAreaXMin, BlockerAreaZMax);
            AreaLine(frame, a, b, mat);
            AreaLine(frame, b, c, mat);
            AreaLine(frame, c, d, mat);
            AreaLine(frame, d, a, mat);

            return row;
        }

        /// <summary>
        /// ブロッカーの背面（自分のゴール側）のガード。見た目だけ（当たり判定なし）。
        /// 後ろから当たった球で HP が減らないことを示す（判定は BlockerRow.backGuard）。
        /// カプセルの子なので、カプセルの回転（Z 90°）と縮尺（0.5）を打ち消した値で置く。
        /// </summary>
        static void CreateBlockerGuard(Transform capsule, int player, Material mat)
        {
            float back = player == 0 ? -1f : 1f;                     // 自分のゴールの向き（Z）
            float s = capsule.localScale.x;                          // カプセルの縮尺（0.5）
            Vector3 worldOffset = new Vector3(0f, -0.06f, back * (BlockerRadius + 0.04f));
            Vector3 worldSize = new Vector3(BlockerLength * 0.9f, 0.18f, 0.06f);

            var guard = GameObject.CreatePrimitive(PrimitiveType.Cube);
            guard.name = "Guard";
            Object.DestroyImmediate(guard.GetComponent<Collider>());
            guard.transform.SetParent(capsule, false);
            // カプセルのローカル軸：X = ワールド上（+Y）、Y = ワールド左（-X）、Z = ワールド Z
            guard.transform.localPosition = new Vector3(worldOffset.y, -worldOffset.x, worldOffset.z) / s;
            guard.transform.localRotation = Quaternion.identity;
            guard.transform.localScale = new Vector3(worldSize.y, worldSize.x, worldSize.z) / s;
            guard.GetComponent<Renderer>().sharedMaterial = mat;
        }

        static void AreaLine(Transform parent, Vector2 a, Vector2 b, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Line";
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            var a3 = new Vector3(a.x, 0.006f, a.y);
            var b3 = new Vector3(b.x, 0.006f, b.y);
            go.transform.position = (a3 + b3) / 2f;
            go.transform.rotation = Quaternion.LookRotation(b3 - a3, Vector3.up);
            go.transform.localScale = new Vector3(0.05f, 0.004f, Vector3.Distance(a3, b3) + 0.05f);
            go.GetComponent<Renderer>().sharedMaterial = mat;
            go.isStatic = true;
        }

        // サイドレールの入口（P1 側の座標）。バンパー (-5.4, -1.6) の上端 -1.1 から 0.45 離し、内側へ広げた
        //   X -5.9〜-3.9（幅 2.0）、Z -0.65〜-0.05。バンパーの右を抜けた球・バンパーで上へ弾かれた球も入る
        const float RailEntranceX = -4.9f, RailEntranceZ = -0.35f;
        const float RailEntranceW = 2.0f, RailEntranceD = 0.6f;

        /// <summary>
        /// サイドレール。P1 側の座標で書き、P2 は点対称。
        ///   入口：自陣の左の壁際（相手側 +Z へ進む球だけが乗る）
        ///   通り道：左の外壁の上を通り、相手陣地で内側へ曲がる
        ///   出口 (-1.2, 6.3)：相手のブロッカーの範囲（〜5.55）とフリッパーの間で、相手のフリッパー（付け根 (-1.85, 9)）の真ん中の真上。
        ///     ステップ29：以前の出口 (-2.4, 7.1) はインレーンの壁・ロールオーバーと重なり、しかも盤面の中央向きに放していたので
        ///     フリッパーの真ん中の隙間へそのまま落ちていた → インレーンから離し、相手のゴール側（フリッパー）へ弱めに放す
        ///     確認値：インレーンの壁の下端まで 1.5、ロールオーバーまで 2.3、ブロッカーの範囲まで 0.75
        /// 球は運ばれている間キネマティックなので、途中の物に当たらない。
        /// 見た目（すべて当たり判定なし）：持ち主の色のレール2本＋支柱、入口の枠と床の板、出口の円
        /// </summary>
        static void CreateSideRail(string name, Transform parent, int player, System.Func<float, float, Vector2> P,
            Material ownerMat, Material postMat)
        {
            Vector3 W(float x, float z) { var v = P(x, z); return new Vector3(v.x, 0f, v.y); }
            float s = player == 0 ? 1f : -1f;
            Color pc = player == 0 ? P1Color : P2Color;
            var fillMat = Mat($"RailFill_P{player + 1}", Color.Lerp(new Color(0.07f, 0.11f, 0.24f), pc, 0.35f), 0f, 0.4f);

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var entrance = W(RailEntranceX, RailEntranceZ);
            go.transform.position = entrance;

            var trigger = go.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 0.25f, 0f);
            trigger.size = new Vector3(RailEntranceW, 0.8f, RailEntranceD);

            var rail = go.AddComponent<SideRail>();
            rail.ownerPlayer = player;
            rail.path = new[]
            {
                entrance,
                W(-6.25f, 0.45f),
                W(-6.25f, 4.8f),
                W(-5.5f, 5.35f),
                W(-2.1f, 5.45f),    // インレーン・ロールオーバーの上を通らないよう、ブロッカーの範囲の奥の端に沿って内側へ（インレーンの壁の上端まで 0.79）
                W(-1.2f, 6.3f),     // 出口：相手のフリッパーの真ん中の真上
            };
            rail.entryDirection = new Vector3(0f, 0f, s);        // 相手側へ進む球
            rail.exitDirection = new Vector3(0f, 0f, s);         // 出口では相手のゴール側（＝相手のフリッパー）へ
            rail.exitSpeed = 3f;                                 // 弱めに放す（あとは台の傾きでフリッパーへ転がる）

            // ---- レール：持ち主の色の2本（太め）＋ 壁の上の支柱 ----
            var visual = new GameObject("Rail").transform;
            visual.SetParent(go.transform, false);
            float total = rail.TotalLength;
            const float step = 0.35f;
            Vector3 prev = rail.Evaluate(0f);
            float sincePost = 0f;
            for (float d = step; d <= total + 0.001f; d += step)
            {
                Vector3 cur = rail.Evaluate(Mathf.Min(d, total));
                Vector3 dir = cur - prev;
                Vector3 side = Vector3.Cross(Vector3.up, dir.normalized);
                for (int k = -1; k <= 1; k += 2)
                {
                    var wire = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    wire.name = "Wire";
                    Object.DestroyImmediate(wire.GetComponent<Collider>());
                    wire.transform.SetParent(visual, false);
                    wire.transform.position = (prev + cur) / 2f + side * (0.22f * k) - Vector3.up * 0.18f;
                    wire.transform.rotation = Quaternion.FromToRotation(Vector3.up, dir);
                    wire.transform.localScale = new Vector3(0.09f, dir.magnitude / 2f + 0.02f, 0.09f);
                    wire.GetComponent<Renderer>().sharedMaterial = ownerMat;
                }

                // 高い所では、2本をつなぐ横木と下の支柱（上から見るとはしご状になり、レールだと分かる）
                sincePost += step;
                if (cur.y > 0.8f && sincePost >= 1.05f)
                {
                    sincePost = 0f;
                    var tie = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    tie.name = "Tie";
                    Object.DestroyImmediate(tie.GetComponent<Collider>());
                    tie.transform.SetParent(visual, false);
                    tie.transform.position = cur - Vector3.up * 0.2f;
                    tie.transform.rotation = Quaternion.LookRotation(side, Vector3.up);
                    tie.transform.localScale = new Vector3(0.05f, 0.05f, 0.52f);
                    tie.GetComponent<Renderer>().sharedMaterial = ownerMat;

                    var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    post.name = "Post";
                    Object.DestroyImmediate(post.GetComponent<Collider>());
                    post.transform.SetParent(visual, false);
                    post.transform.position = new Vector3(cur.x, (cur.y - 0.2f) / 2f, cur.z);
                    post.transform.localScale = new Vector3(0.07f, (cur.y - 0.2f) / 2f, 0.07f);
                    post.GetComponent<Renderer>().sharedMaterial = postMat;
                }
                prev = cur;
            }

            // ---- 入口：床の板（持ち主の色を薄く）と、コの字の枠（自分のフリッパー側が開いている）----
            var mouth = new GameObject("Entrance").transform;
            mouth.SetParent(go.transform, false);
            float hw = RailEntranceW / 2f, hd = RailEntranceD / 2f;
            FloorBox("Plate", mouth, entrance + new Vector3(0f, 0.004f, 0f), new Vector3(RailEntranceW, 0.004f, RailEntranceD), fillMat);
            FloorBox("Frame_Side1", mouth, entrance + new Vector3(-hw, 0.01f, 0f), new Vector3(0.08f, 0.01f, RailEntranceD), ownerMat);
            FloorBox("Frame_Side2", mouth, entrance + new Vector3(hw, 0.01f, 0f), new Vector3(0.08f, 0.01f, RailEntranceD), ownerMat);
            FloorBox("Frame_Back", mouth, entrance + new Vector3(0f, 0.01f, s * hd), new Vector3(RailEntranceW + 0.08f, 0.01f, 0.08f), ownerMat);

            // ---- 出口：持ち主の色の円（八角形の枠）と中心の点 ----
            var exitRoot = new GameObject("Exit").transform;
            exitRoot.SetParent(go.transform, false);
            var exitPos = rail.path[rail.path.Length - 1];
            const int sides = 8;
            const float ringR = 0.5f;
            for (int i = 0; i < sides; i++)
            {
                float a0 = Mathf.PI * 2f * i / sides, a1 = Mathf.PI * 2f * (i + 1) / sides;
                var p0 = exitPos + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * ringR;
                var p1 = exitPos + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * ringR;
                var seg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                seg.name = "Ring";
                Object.DestroyImmediate(seg.GetComponent<Collider>());
                seg.transform.SetParent(exitRoot, false);
                seg.transform.position = (p0 + p1) / 2f + Vector3.up * 0.01f;
                seg.transform.rotation = Quaternion.LookRotation(p1 - p0, Vector3.up);
                seg.transform.localScale = new Vector3(0.07f, 0.01f, Vector3.Distance(p0, p1) + 0.04f);
                seg.GetComponent<Renderer>().sharedMaterial = ownerMat;
            }
            FloorBox("Center", exitRoot, exitPos + Vector3.up * 0.004f, new Vector3(0.5f, 0.004f, 0.5f), fillMat);
        }

        /// <summary>床に置く見た目だけの板</summary>
        static void FloorBox(string name, Transform parent, Vector3 center, Vector3 size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = mat;
        }

        static void CreateTurntable(Transform parent, Vector3 position, Material discMat, Material stripeMat)
        {
            const float radius = TurntableRadius;

            var go = new GameObject("Turntable");
            go.transform.SetParent(parent, false);
            go.transform.position = position;

            var trigger = go.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 0.25f, 0f);
            trigger.size = new Vector3(radius * 2f, 0.8f, radius * 2f);

            var tt = go.AddComponent<Turntable>();
            tt.radius = radius;

            var visual = new GameObject("Visual").transform;
            visual.SetParent(go.transform, false);
            tt.visual = visual;

            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = "Disc";
            Object.DestroyImmediate(disc.GetComponent<Collider>());
            disc.transform.SetParent(visual, false);
            disc.transform.localPosition = new Vector3(0f, 0.008f, 0f);
            disc.transform.localScale = new Vector3(radius * 2f, 0.004f, radius * 2f);
            disc.GetComponent<Renderer>().sharedMaterial = discMat;

            for (int i = 0; i < 3; i++)
            {
                var stripe = GameObject.CreatePrimitive(PrimitiveType.Cube);
                stripe.name = $"Stripe_{i}";
                Object.DestroyImmediate(stripe.GetComponent<Collider>());
                stripe.transform.SetParent(visual, false);
                stripe.transform.localPosition = new Vector3(0f, 0.016f, 0f);
                stripe.transform.localRotation = Quaternion.Euler(0f, i * 60f, 0f);
                stripe.transform.localScale = new Vector3(radius * 1.9f, 0.006f, 0.1f);
                stripe.GetComponent<Renderer>().sharedMaterial = stripeMat;
            }
        }

        static Collider[] CreateCurve(string name, Transform parent, System.Func<float, float, Vector2> P,
            float exitZ, Material mat, PhysMat pm)
        {
            var curve = new GameObject(name).transform;
            curve.SetParent(parent, false);
            const int segments = 6;
            float r = CurveRadius + 0.1f; // 壁の中心までの半径（厚み0.2）
            var cols = new Collider[segments];
            for (int i = 0; i < segments; i++)
            {
                float a0 = Mathf.PI * 0.5f * i / segments;
                float a1 = Mathf.PI * 0.5f * (i + 1) / segments;
                var p0 = P(LaneInnerFace + r * Mathf.Cos(a0), exitZ + r * Mathf.Sin(a0));
                var p1 = P(LaneInnerFace + r * Mathf.Cos(a1), exitZ + r * Mathf.Sin(a1));
                var seg = Segment($"{name}_{i}", curve, p0, p1, 0.2f, mat, pm);
                seg.isStatic = false;
                cols[i] = seg.GetComponent<Collider>();
            }
            return cols;
        }

        // ===================== ヘルパー =====================

        static Flipper CreateFlipper(string name, Transform parent, Vector3 pivot, int player, FlipperSide side,
            float rest, float active, Material mat, PhysMat pm)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pivot;
            go.transform.rotation = Quaternion.Euler(0f, rest, 0f);

            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            var f = go.AddComponent<Flipper>();
            f.playerIndex = player;
            f.side = side;
            f.length = FlipperLength;
            f.restAngle = rest;
            f.activeAngle = active;

            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            body.transform.SetParent(go.transform, false);
            body.transform.localPosition = new Vector3(FlipperLength / 2f, 0.25f, 0f);
            body.transform.localScale = new Vector3(FlipperLength, 0.5f, 0.3f);
            body.GetComponent<Renderer>().sharedMaterial = mat;
            body.GetComponent<Collider>().sharedMaterial = pm;

            var hub = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            hub.name = "Hub";
            Object.DestroyImmediate(hub.GetComponent<Collider>());
            hub.transform.SetParent(go.transform, false);
            hub.transform.localPosition = new Vector3(0f, 0.25f, 0f);
            hub.transform.localScale = new Vector3(0.4f, 0.25f, 0.4f);
            hub.GetComponent<Renderer>().sharedMaterial = mat;

            return f;
        }

        static Bumper CreateBumper(string name, Transform parent, Vector3 pos, Material mat, PhysMat pm)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(pos.x, 0.5f, pos.z);
            go.transform.localScale = new Vector3(1f, 0.5f, 1f); // 半径0.5, 高さ1

            // 円柱プリミティブのCapsuleColliderは潰すと球になるので、凸メッシュに差し替え
            Object.DestroyImmediate(go.GetComponent<Collider>());
            var mc = go.AddComponent<MeshCollider>();
            mc.sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;
            mc.convex = true;
            mc.sharedMaterial = pm;

            go.GetComponent<Renderer>().sharedMaterial = mat;
            var bumper = go.AddComponent<Bumper>();
            // 壁際（外壁から 1 以内）のバンパーは、弾く向きを盤面の内側へ寄せる
            if (Mathf.Abs(pos.x) > PlayHalfWidth - 1f)
                bumper.inwardDirection = new Vector3(-Mathf.Sign(pos.x), 0f, 0f);
            return bumper;
        }

        /// <summary>
        /// アウトレーン。外壁と仕切りの間の通り道の入口に「ふた」を置き、Outlane で開け閉めする。
        ///   ふたは外壁側が高い斜めの板（外壁 (6, 入口+0.45) → 仕切りの上端 (5.1, 入口)）。
        ///   閉じているときに当たった球は、仕切りの上端を越えてインレーンの入口へ転がる（平らだと上で止まるので斜め）。
        /// 床には入口からゴールまで持ち主の色の帯と、入口の横線（見た目だけ）。
        /// </summary>
        static void CreateOutlane(string name, Transform parent, int player, System.Func<float, float, Vector2> P, bool mirrorX,
            Material mat, SideMaterials m)
        {
            float mx = mirrorX ? -1f : 1f;
            float top = GuideStartZ + 0.9f;                     // 入口 = 仕切りの上端
            float cx = -(PlayHalfWidth + OutlaneWallX + 0.1f) / 2f; // 外壁の内面と仕切りの面の真ん中
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);

            // ふた
            var gateGo = Segment("Gate", root, P(mx * -PlayHalfWidth, top + 0.45f), P(mx * -OutlaneWallX, top), 0.2f, m.wall, m.pWall);
            gateGo.isStatic = false;
            var ol = root.gameObject.AddComponent<Outlane>();
            ol.ownerPlayer = player;
            ol.gate = gateGo.GetComponent<Collider>();
            ol.gateRenderer = gateGo.GetComponent<Renderer>();
            var en = P(mx * cx, top + 0.2f);
            var le = P(mx * cx, -PlayHalfLength);
            ol.entrance = new Vector3(en.x, 0f, en.y);
            ol.laneEnd = new Vector3(le.x, 0f, le.y);
            ol.sideSign = Mathf.Sign(en.x);
            ol.laneWidth = OutlaneWidth;

            var a = P(mx * cx, top);
            var b = P(mx * cx, -PlayHalfLength);
            var center = new Vector3((a.x + b.x) / 2f, 0.006f, (a.y + b.y) / 2f);
            Decal("Strip", root, center, new Vector3(0.12f, 0.01f, Mathf.Abs(a.y - b.y)), mat);
            var e = P(mx * cx, top);
            Decal("EntranceLine", root, new Vector3(e.x, 0.006f, e.y), new Vector3(OutlaneWidth, 0.01f, 0.08f), mat);
        }

        static void CreateGoal(string name, Transform parent, Vector3 pos, int owner)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var col = go.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = new Vector3(12f, 4f, 1.2f);
            go.AddComponent<Drain>().ownerPlayer = owner;
        }

        static GameObject Box(string name, Transform parent, Vector3 center, Vector3 size, Material mat, PhysMat pm)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            go.GetComponent<Collider>().sharedMaterial = pm;
            go.isStatic = true;
            return go;
        }

        /// <summary>見た目だけの板（当たり判定なし）。</summary>
        static void Decal(string name, Transform parent, Vector3 center, Vector3 size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            go.isStatic = true;
        }

        /// <summary>XZ平面上の2点 a→b を結ぶ壁。継ぎ目に隙間ができないよう厚み分だけ延長する。</summary>
        static GameObject Segment(string name, Transform parent, Vector2 a, Vector2 b, float thickness, Material mat, PhysMat pm)
        {
            var a3 = new Vector3(a.x, WallHeight / 2f, a.y);
            var b3 = new Vector3(b.x, WallHeight / 2f, b.y);

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = (a3 + b3) / 2f;
            go.transform.rotation = Quaternion.LookRotation(b3 - a3, Vector3.up);
            go.transform.localScale = new Vector3(thickness, WallHeight, Vector3.Distance(a3, b3) + thickness);
            go.GetComponent<Renderer>().sharedMaterial = mat;
            go.GetComponent<Collider>().sharedMaterial = pm;
            go.isStatic = true;
            return go;
        }

        /// <summary>延長なしの壁（開け閉めする板用。隣の壁と重ねすぎない）</summary>
        static GameObject ExactSegment(string name, Transform parent, Vector2 a, Vector2 b, float thickness, Material mat, PhysMat pm)
        {
            var go = Segment(name, parent, a, b, thickness, mat, pm);
            var sc = go.transform.localScale;
            go.transform.localScale = new Vector3(sc.x, sc.y, Vector2.Distance(a, b));
            go.isStatic = false;
            return go;
        }

        static void SetupCamera()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = camGo.AddComponent<Camera>();
                camGo.AddComponent<AudioListener>();
                Undo.RegisterCreatedObjectUndo(camGo, "Create Camera");
            }

            Undo.RecordObject(cam.transform, "Setup Camera");
            Undo.RecordObject(cam, "Setup Camera");

            // 真上から縦向き（P1 下・P2 上）。CameraFit が向きとサイズを毎フレーム合わせる
            cam.transform.position = new Vector3(0f, 20f, 0f);
            cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            cam.orthographic = true;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 50f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.02f, 0.02f, 0.05f);

            var fit = cam.GetComponent<CameraFit>();
            if (fit == null) fit = Undo.AddComponent<CameraFit>(cam.gameObject);
            fit.tableWidth = (LaneOuterFace + 0.3f) * 2f;   // 盤面の外の発射レーンまで映す
            fit.tableLength = PlayHalfLength * 2f + 1f;
        }

        static void SetupLight()
        {
#if UNITY_2023_1_OR_NEWER
            var lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
#else
            var lights = Object.FindObjectsOfType<Light>();
#endif
            foreach (var l in lights)
                if (l.type == LightType.Directional) return;

            var go = new GameObject("Directional Light");
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            go.transform.rotation = Quaternion.Euler(60f, -30f, 0f);
            Undo.RegisterCreatedObjectUndo(go, "Create Light");
        }

        static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Pinball")) AssetDatabase.CreateFolder("Assets", "Pinball");
            if (!AssetDatabase.IsValidFolder(AssetDir)) AssetDatabase.CreateFolder("Assets/Pinball", "Generated");
        }

        static Shader DefaultShader()
        {
            var pipeline = GraphicsSettings.currentRenderPipeline;
            if (pipeline != null && pipeline.defaultShader != null) return pipeline.defaultShader;
            return Shader.Find("Standard");
        }

        static Material Mat(string name, Color color, float metallic, float smoothness)
        {
            string path = $"{AssetDir}/M_{name}.mat";
            var shader = DefaultShader();
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            else if (m.shader != shader)
            {
                m.shader = shader;
            }

            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);   // URP
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);   // Built-in
            EditorUtility.SetDirty(m);
            return m;
        }

        static PhysMat PMat(string name, float bounciness, float friction)
        {
            string path = $"{AssetDir}/PM_{name}.asset";
            var pm = AssetDatabase.LoadAssetAtPath<PhysMat>(path);
            if (pm == null)
            {
                pm = new PhysMat(name);
                AssetDatabase.CreateAsset(pm, path);
            }

            pm.bounciness = bounciness;
            pm.dynamicFriction = friction;
            pm.staticFriction = friction;
            pm.bounceCombine = PhysCombine.Average;
            pm.frictionCombine = PhysCombine.Average;
            EditorUtility.SetDirty(pm);
            return pm;
        }
    }
}
