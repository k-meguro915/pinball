using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PinballDodgeball.EditorTools
{
    /// <summary>
    /// ドッジボールルールの台を今のシーンに作る。
    /// メニュー：Tools > Pinball Dodgeball > Build Dodgeball Table（今の台のツール Tools > Pinball とは別）
    /// 作り直しても「Dodgeball Game」（GameManager・HUD）の設定はそのまま。
    /// </summary>
    public static class DBTableBuilder
    {
        const string RootName = "Dodgeball Table";
        const string GameName = "Dodgeball Game";
        const string BaseDir = "Assets/PinballDodgeball";
        const string MatDir = BaseDir + "/Generated";

        static Dictionary<string, Material> mats;

        [MenuItem("Tools/Pinball Dodgeball/Build Dodgeball Table", priority = 0)]
        public static void Build()
        {
            var old = GameObject.Find(RootName);
            if (old != null) Undo.DestroyObjectImmediate(old);

            LoadMaterials();
            var root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Build Dodgeball Table");
            var t = root.transform;

            BuildFloorAndWalls(t, out var doors);
            // 2026-11-27：中央の線のバンパー（BuildBumpers）とドロップターゲット（BuildDropTargets）は一度外した。戻すときは呼び直す
            var banks = new DBDropTargetBank[0];
            BuildBackLanes(t);
            BuildBackBumpers(t);
            BuildSpinners(t);
            BuildDecor(t);
            var coasters = BuildCoasters(t);
            var rows = BuildBlockerRows(t);
            var lanes = BuildLanes(t, doors);
            BuildOutfieldMarks(t);
            var ball = BuildBall(t);
            var outfieldRoot = new GameObject("Outfield").transform;
            outfieldRoot.SetParent(t, false);

            var gm = SetupGame(ball, rows, lanes, banks, coasters, outfieldRoot);
            SetupCamera();
            SetupLight();

            EditorSceneManager.MarkSceneDirty(root.scene);
            Selection.activeGameObject = gm.gameObject;
            Debug.Log("[Pinball Dodgeball] 台を作りました。再生して確かめてください。");
        }

        [MenuItem("Tools/Pinball Dodgeball/Select Game Manager", priority = 20)]
        public static void SelectGame()
        {
            var gm = Object.FindFirstObjectByType<DBGameManager>();
            if (gm != null) Selection.activeGameObject = gm.gameObject;
            else Debug.LogWarning("[Pinball Dodgeball] Dodgeball Game がありません。先に Build Dodgeball Table を実行してください。");
        }

        // ------------------------------------------------------------------
        // マテリアル（Assets/PinballDodgeball/Generated に保存）

        static void LoadMaterials()
        {
            if (!AssetDatabase.IsValidFolder(BaseDir)) AssetDatabase.CreateFolder("Assets", "PinballDodgeball");
            if (!AssetDatabase.IsValidFolder(MatDir)) AssetDatabase.CreateFolder(BaseDir, "Generated");

            var gm = Object.FindFirstObjectByType<DBGameManager>();
            Color p1 = gm != null ? gm.PlayerColor(0) : new Color(0.25f, 0.6f, 1f);
            Color p2 = gm != null ? gm.PlayerColor(1) : new Color(1f, 0.4f, 0.3f);
            Color floor = new Color(0.08f, 0.1f, 0.16f);

            mats = new Dictionary<string, Material>
            {
                { "Floor", Mat("DB_Floor", floor) },
                { "Wall", Mat("DB_Wall", new Color(0.55f, 0.58f, 0.66f)) },
                { "GoalWall", Mat("DB_GoalWall", new Color(0.4f, 0.42f, 0.5f)) },
                { "Post", Mat("DB_Post", new Color(0.85f, 0.85f, 0.9f)) },
                { "Door", Mat("DB_Door", new Color(0.25f, 0.85f, 0.45f)) },
                { "Bumper", Mat("DB_Bumper", new Color(1f, 0.8f, 0.2f)) },
                { "Spinner", Mat("DB_Spinner", new Color(0.4f, 0.95f, 0.95f)) },
                { "SpinnerDark", Mat("DB_Spinner_Dark", new Color(0.12f, 0.35f, 0.4f)) },
                { "Coaster", Mat("DB_Coaster", new Color(0.55f, 0.6f, 0.7f)) },
                { "BoostPad", Mat("DB_BoostPad", new Color(0.3f, 1f, 0.6f)) },
                { "BoostPadDim", Mat("DB_BoostPad_Dim", Color.Lerp(floor, new Color(0.3f, 1f, 0.6f), 0.18f)) },
                { "Corner", Mat("DB_BackLane_Corner", new Color(0.17f, 0.19f, 0.26f)) },
                { "Gate", Mat("DB_BackLane_Gate", new Color(1f, 0.85f, 0.29f)) },
                { "Line", Mat("DB_Line", new Color(0.85f, 0.88f, 0.95f)) },
                { "Stand", Mat("DB_Stand", new Color(0.18f, 0.2f, 0.28f)) },
                { "Crowd1", Mat("DB_Crowd_1", new Color(0.9f, 0.75f, 0.6f)) },
                { "Crowd2", Mat("DB_Crowd_2", new Color(0.45f, 0.5f, 0.6f)) },
                { "Crowd3", Mat("DB_Crowd_3", new Color(0.95f, 0.95f, 0.98f)) },
                { "LightPole", Mat("DB_Light_Pole", new Color(0.4f, 0.42f, 0.5f)) },
                { "LightLamp", Mat("DB_Light_Lamp", new Color(1f, 0.97f, 0.85f)) },
                { "Ball", Mat("DB_Ball", new Color(0.92f, 0.92f, 0.96f)) },
                { "P1", Mat("DB_P1", p1) },
                { "P2", Mat("DB_P2", p2) },
                { "P1Dim", Mat("DB_P1_Area", Color.Lerp(floor, p1, 0.22f)) },
                { "P2Dim", Mat("DB_P2_Area", Color.Lerp(floor, p2, 0.22f)) },
                { "P1Mark", Mat("DB_P1_Mark", Color.Lerp(floor, p1, 0.45f)) },
                { "P2Mark", Mat("DB_P2_Mark", Color.Lerp(floor, p2, 0.45f)) },
            };
        }

        static Material Mat(string name, Color c)
        {
            string path = MatDir + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(DBUtil.DefaultShader()) { name = name };
                AssetDatabase.CreateAsset(m, path);
            }
            m.color = c;
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material PM(int p) => mats[p == 0 ? "P1" : "P2"];

        // ------------------------------------------------------------------
        // 部品

        static GameObject Box(string name, Transform parent, Vector3 center, Vector3 size, Material mat, bool collider = true, float yaw = 0f)
        {
            return DBUtil.Primitive(PrimitiveType.Cube, name, parent, center, Quaternion.Euler(0f, yaw, 0f), size, mat, collider);
        }

        static GameObject FloorPlate(string name, Transform parent, Vector3 center, Vector3 size, Material mat, float yaw = 0f)
        {
            return Box(name, parent, new Vector3(center.x, 0.004f, center.z), new Vector3(size.x, 0.008f, size.z), mat, false, yaw);
        }

        /// <summary>2 点の間の壁（XZ）</summary>
        static GameObject WallBetween(string name, Transform parent, Vector3 a, Vector3 b, float thickness, Material mat)
        {
            Vector3 d = b - a;
            float len = new Vector2(d.x, d.z).magnitude;
            float yaw = DBUtil.YawOf(d);
            Vector3 mid = (a + b) * 0.5f;
            return Box(name, parent, new Vector3(mid.x, DBConst.WallHeight * 0.5f, mid.z),
                new Vector3(thickness, DBConst.WallHeight, len), mat, true, yaw);
        }

        // ------------------------------------------------------------------

        struct DoorInfo
        {
            public Collider col;
            public Renderer rend;
        }

        static void BuildFloorAndWalls(Transform t, out Dictionary<string, DoorInfo> doors)
        {
            float outer = DBConst.OuterHalfWidth;            // 7.4
            float L = DBConst.PlayHalfLength;                // 12
            float th = DBConst.WallThickness;
            float h = DBConst.WallHeight;

            var g = new GameObject("Walls").transform;
            g.SetParent(t, false);

            Box("Floor", t, new Vector3(0f, -0.05f, 0f), new Vector3(outer * 2f, 0.1f, (L + th) * 2f), mats["Floor"]);

            // ゴールをふさいだ壁（レーンの端も兼ねる）
            for (int s = -1; s <= 1; s += 2)
                Box(s < 0 ? "Goal Wall P1" : "Goal Wall P2", g, new Vector3(0f, h * 0.5f, s * (L + th * 0.5f)),
                    new Vector3(outer * 2f, h, th), mats["GoalWall"]);

            // レーンの外側の壁
            float laneOuterWallX = DBConst.LaneOuterX + th * 0.5f;
            for (int s = -1; s <= 1; s += 2)
                Box("Lane Outer Wall " + (s < 0 ? "L" : "R"), g, new Vector3(s * laneOuterWallX, h * 0.5f, 0f),
                    new Vector3(th, h, L * 2f), mats["Wall"]);

            // 盤面の外壁（出入り口つき）
            // +X の壁：P1 のレーン（自陣側 -3.6、相手陣地側 +2.64）
            // -X の壁：P2 のレーン（自陣側 +3.6、相手陣地側 -2.64）
            doors = new Dictionary<string, DoorInfo>();
            float wallX = DBConst.PlayHalfWidth + th * 0.5f;
            for (int p = 0; p < 2; p++)
            {
                float xs = DBConst.Sign(p);
                float sg = DBConst.Sign(p);
                float ownZ = -sg * DBConst.ServeOwnExitZ;
                float oppZ = sg * DBConst.ServeOppExitZ;
                var gaps = new List<float> { ownZ, oppZ };
                gaps.Sort();
                float hw = DBConst.DoorWidth * 0.5f;
                float z0 = -L;
                int seg = 0;
                foreach (float gz in gaps)
                {
                    SideWall(g, xs * wallX, z0, gz - hw, ref seg);
                    z0 = gz + hw;
                }
                SideWall(g, xs * wallX, z0, L, ref seg);

                string pn = p == 0 ? "P1" : "P2";
                doors[pn + "Own"] = MakeDoor(g, pn + " Door Own", new Vector3(xs * wallX, h * 0.5f, ownZ));
                doors[pn + "Opp"] = MakeDoor(g, pn + " Door Opp", new Vector3(xs * wallX, h * 0.5f, oppZ));
                // 出入り口の床の目印
                FloorPlate(pn + " Door Mark Own", g, new Vector3(xs * (DBConst.PlayHalfWidth + 0.4f), 0f, ownZ), new Vector3(1.2f, 0f, DBConst.DoorWidth), mats["Door"]);
                FloorPlate(pn + " Door Mark Opp", g, new Vector3(xs * (DBConst.PlayHalfWidth + 0.4f), 0f, oppZ), new Vector3(1.2f, 0f, DBConst.DoorWidth), mats["Door"]);
            }
        }

        static void SideWall(Transform g, float x, float z0, float z1, ref int seg)
        {
            if (z1 - z0 < 0.01f) return;
            Box("Side Wall " + (x < 0 ? "L" : "R") + " " + (seg++), g,
                new Vector3(x, DBConst.WallHeight * 0.5f, (z0 + z1) * 0.5f),
                new Vector3(DBConst.WallThickness, DBConst.WallHeight, z1 - z0), mats["Wall"]);
        }

        static DoorInfo MakeDoor(Transform g, string name, Vector3 center)
        {
            var go = Box(name, g, center, new Vector3(DBConst.WallThickness, DBConst.WallHeight, DBConst.DoorWidth), mats["Door"]);
            return new DoorInfo { col = go.GetComponent<Collider>(), rend = go.GetComponent<Renderer>() };
        }

        /// <summary>
        /// 背面の加速レーン（2026-11-27。図面：Docs/図面_ドッジボールの背面レーン.svg）。各陣地に 1 本（点対称）。
        /// 奥の真ん中の入口（内側の壁のすき間）から左右に分かれ、半円にそって横の壁ぞいに中央の線の手前まで運ぶ。
        /// ・加速：DBBoostPad の帯を BackLaneSegmentDeg ごとにつなぐ（入った球は誰の球でもなくなる）。真ん中の帯は、入ったときの球の向きで左右を決める
        /// ・壁：内側の壁（入口のすき間あり）と、外側の半円の壁。外側の角はふさぐ（見た目の板）
        /// ・出口：外側の短い板で内側へ BackLaneExitAngle° 向ける。一方通行のふた（DBOneWayGate）で、外からは入れない
        /// </summary>
        static void BuildBackLanes(Transform t)
        {
            var g = new GameObject("Back Lanes").transform;
            g.SetParent(t, false);
            float rIn = DBConst.BackLaneInnerR, rOut = DBConst.BackLaneOuterR, rMid = DBConst.BackLaneMidR;
            float wt = DBConst.BackLaneWallThickness, seg = DBConst.BackLaneSegmentDeg;
            float width = rOut - rIn;
            float gapDeg = Mathf.Asin(DBConst.BackLaneGapHalf / rIn) * Mathf.Rad2Deg;
            float W = DBConst.PlayHalfWidth;

            for (int territory = 0; territory < 2; territory++)
            {
                float zs = -DBConst.Sign(territory);
                string tn = territory == 0 ? "P1" : "P2";
                var root = new GameObject(tn + " 陣地 Back Lane").transform;
                root.SetParent(g, false);

                // ---- 床：レーンの帯（半円 + まっすぐ）と、外側の角をふさぐ板（見た目） ----
                var band = new GameObject("Lane Floor");
                band.transform.SetParent(root, false);
                band.AddComponent<MeshFilter>().sharedMesh = BandMesh(territory, rIn, rOut, "DB_BackLane_Floor_" + tn);
                band.AddComponent<MeshRenderer>().sharedMaterial = mats["BoostPadDim"];
                for (int s = -1; s <= 1; s += 2)
                {
                    FloorPlate("Lane Floor Straight", root, new Vector3(s * rMid, 0f, zs * (DBConst.BackLaneCenterZ + DBConst.BackLaneExitZ) * 0.5f),
                        new Vector3(width, 0f, DBConst.BackLaneCenterZ - DBConst.BackLaneExitZ), mats["BoostPadDim"]);
                    var corner = new GameObject("Corner Fill " + (s < 0 ? "L" : "R"));
                    corner.transform.SetParent(root, false);
                    corner.AddComponent<MeshFilter>().sharedMesh = CornerMesh(territory, s, rOut, "DB_BackLane_Corner_" + tn + (s < 0 ? "L" : "R"));
                    corner.AddComponent<MeshRenderer>().sharedMaterial = mats["Corner"];
                }

                // ---- 壁 ----
                // 外側の半円（横の壁・奥の壁の内側に接する。角の死角をふさぐ）
                for (float a = -90f; a < 90f - 1e-3f; a += seg)
                    ArcWall("Outer Wall", root, territory, a, a + seg, rOut + wt * 0.5f, wt);
                // 内側の半円（真ん中に入口のすき間）とまっすぐな部分
                for (int s = -1; s <= 1; s += 2)
                {
                    float a0 = gapDeg;
                    while (a0 < 90f - 1e-3f)
                    {
                        float a1 = Mathf.Min(90f, Mathf.Ceil((a0 + 1e-3f) / seg) * seg);
                        ArcWall("Inner Wall", root, territory, s * a0, s * a1, rIn - wt * 0.5f, wt);
                        a0 = a1;
                    }
                    var w0 = new Vector3(s * (rIn - wt * 0.5f), 0f, zs * DBConst.BackLaneCenterZ);
                    var w1 = new Vector3(s * (rIn - wt * 0.5f), 0f, zs * DBConst.BackLaneExitZ);
                    WallBetween("Inner Wall Straight", root, w0, w1, wt, mats["Wall"]);

                    // 出口の板（外側。内側へ BackLaneExitAngle° 向ける）
                    float ea = DBConst.BackLaneExitAngle * Mathf.Deg2Rad;
                    var d0 = new Vector3(s * (W - 0.02f), 0f, zs * DBConst.BackLaneExitZ);
                    var d1 = d0 + new Vector3(-s * Mathf.Sin(ea), 0f, -zs * Mathf.Cos(ea)) * 1.0f;
                    WallBetween("Exit Deflector", root, d0, d1, wt, mats["Wall"]);

                    // 一方通行のふた（出口の線の上。軸は外側の壁の側）
                    var gateRoot = new GameObject("Exit Gate " + (s < 0 ? "L" : "R"));
                    gateRoot.transform.SetParent(root, false);
                    float gx = s * (rIn + rOut) * 0.5f;
                    gateRoot.transform.position = new Vector3(gx, DBConst.WallHeight * 0.5f, zs * DBConst.BackLaneExitZ);
                    var gcol = gateRoot.AddComponent<BoxCollider>();
                    gcol.size = new Vector3(width, DBConst.WallHeight, 0.06f);
                    var hinge = new GameObject("Hinge").transform;
                    hinge.SetParent(gateRoot.transform, false);
                    hinge.localPosition = new Vector3(s * width * 0.5f, 0f, 0f);
                    Box("Flap", hinge, new Vector3(-s * width * 0.5f, 0f, 0f), new Vector3(width, DBConst.WallHeight * 0.8f, 0.05f), mats["Gate"], false);
                    var gate = gateRoot.AddComponent<DBOneWayGate>();
                    gate.allowed = new Vector3(0f, 0f, -zs);
                    gate.gateCollider = gcol;
                    gate.hinge = hinge;
                    // 開く向き：板の先（内側の壁の側）が、出ていく向きへ回る
                    gate.openAngle = 75f * (s * zs < 0f ? 1f : -1f);
                }

                // ---- 加速の帯（入口の真ん中、半円、まっすぐ） ----
                float h = 1f;
                {
                    // 真ん中（奥の真ん中 ±seg）：入ったときの球の向きで左右を決める
                    Vector3 c = DBConst.BackLanePoint(territory, 0f, rMid);
                    var pad = Pad("Pad Center", root, c, Vector3.right, new Vector3(width, h, 2f * rMid * Mathf.Sin(seg * Mathf.Deg2Rad) + 0.12f));
                    pad.chooseSideByVelocity = true;
                    // 入口の目印（三角。見た目だけ）
                    var tri = DBUtil.Primitive(PrimitiveType.Cube, "Entrance Mark", root, Vector3.zero, Quaternion.Euler(0f, 45f, 0f),
                        new Vector3(0.45f, 0.01f, 0.45f), mats["Gate"], false);
                    tri.transform.position = new Vector3(c.x, 0.01f, c.z);
                }
                for (int s = -1; s <= 1; s += 2)
                {
                    for (float a = seg; a < 90f - 1e-3f; a += seg)
                    {
                        float am = a + seg * 0.5f;
                        Vector3 c = DBConst.BackLanePoint(territory, s * am, rMid);
                        Vector3 dir = DBConst.BackLaneTangent(territory, s * am);
                        float len = 2f * rMid * Mathf.Sin(seg * 0.5f * Mathf.Deg2Rad) + 0.12f;
                        Pad("Pad Arc", root, c, dir, new Vector3(width, h, len));
                    }
                    float sl = DBConst.BackLaneCenterZ - DBConst.BackLaneExitZ;
                    Pad("Pad Straight", root, new Vector3(s * rMid, DBConst.BallY, zs * (DBConst.BackLaneCenterZ + DBConst.BackLaneExitZ) * 0.5f),
                        new Vector3(0f, 0f, -zs), new Vector3(width, h, sl + 0.12f));
                }
            }
        }

        /// <summary>加速の帯（トリガー）を 1 つ置く。dir = 押す向き。size = (幅, 高さ, 長さ)（dir の向きが長さ）</summary>
        static DBBoostPad Pad(string name, Transform parent, Vector3 center, Vector3 dir, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(new Vector3(center.x, DBConst.BallY, center.z), Quaternion.Euler(0f, DBUtil.YawOf(dir), 0f));
            var trig = go.AddComponent<BoxCollider>();
            trig.isTrigger = true;
            trig.size = size;
            var arrows = new List<Renderer>();
            for (int e = -1; e <= 1; e += 2)
            {
                var a = Box("Arrow", go.transform, new Vector3(e * 0.13f, 0.008f - DBConst.BallY, 0f), new Vector3(0.06f, 0.008f, 0.34f), mats["BoostPad"], false, -e * 35f);
                arrows.Add(a.GetComponent<Renderer>());
            }
            var bp = go.AddComponent<DBBoostPad>();
            bp.direction = dir;
            bp.clearOwner = true;
            bp.acceleration = 25f;
            bp.maxAlongSpeed = 16f;
            bp.arrows = arrows.ToArray();
            return bp;
        }

        /// <summary>半径 r の円の上、角度 a0 → a1 の弦に沿った壁（両端を少し延ばして、すき間をなくす）</summary>
        static void ArcWall(string name, Transform parent, int territory, float a0, float a1, float r, float thickness)
        {
            Vector3 p0 = DBConst.BackLanePoint(territory, a0, r), p1 = DBConst.BackLanePoint(territory, a1, r);
            Vector3 d = DBUtil.Flat(p1 - p0).normalized * (thickness * 0.5f);
            WallBetween(name, parent, DBUtil.Flat(p0) - d, DBUtil.Flat(p1) + d, thickness, mats["Wall"]);
        }

        /// <summary>レーンの床の半円の帯（rIn〜rOut）のメッシュ（Generated に保存）</summary>
        static Mesh BandMesh(int territory, float rIn, float rOut, string name)
        {
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var idx = new List<int>();
            const int n = 48;
            for (int i = 0; i < n; i++)
            {
                float a0 = -90f + 180f * i / n, a1 = -90f + 180f * (i + 1) / n;
                Vector3 i0 = Lift(DBConst.BackLanePoint(territory, a0, rIn)), i1 = Lift(DBConst.BackLanePoint(territory, a1, rIn));
                Vector3 o0 = Lift(DBConst.BackLanePoint(territory, a0, rOut)), o1 = Lift(DBConst.BackLanePoint(territory, a1, rOut));
                Face(verts, normals, idx, i0, o0, o1, Vector3.up);
                Face(verts, normals, idx, i0, o1, i1, Vector3.up);
            }
            return SaveMesh(name, verts, normals, idx);
        }

        /// <summary>外側の角（横の壁・奥の壁・外側の半円で囲まれた所）をふさぐ板のメッシュ。side = 画面の左 -1 / 右 +1</summary>
        static Mesh CornerMesh(int territory, int side, float rOut, string name)
        {
            float zs = -DBConst.Sign(territory);
            Vector3 corner = new Vector3(side * DBConst.PlayHalfWidth, 0.006f, zs * DBConst.PlayHalfLength);
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var idx = new List<int>();
            const int n = 24;
            for (int i = 0; i < n; i++)
            {
                float a0 = side * 90f * i / n, a1 = side * 90f * (i + 1) / n;
                Face(verts, normals, idx, corner, Lift(DBConst.BackLanePoint(territory, a0, rOut)), Lift(DBConst.BackLanePoint(territory, a1, rOut)), Vector3.up);
            }
            return SaveMesh(name, verts, normals, idx);
        }

        static Vector3 Lift(Vector3 p) => new Vector3(p.x, 0.006f, p.z);

        static Mesh SaveMesh(string name, List<Vector3> verts, List<Vector3> normals, List<int> idx)
        {
            string path = MatDir + "/" + name + ".asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool isNew = mesh == null;
            if (isNew) mesh = new Mesh();
            mesh.Clear();
            mesh.name = name;
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetTriangles(idx, 0);
            mesh.RecalculateBounds();
            if (isNew) AssetDatabase.CreateAsset(mesh, path);
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        /// <summary>背面のバンパー：入口の左右に 2 つずつ（各陣地 4 つ）。はじく向きは内側（入口の方）か陣地の方</summary>
        static void BuildBackBumpers(Transform t)
        {
            var g = new GameObject("Back Bumpers").transform;
            g.SetParent(t, false);
            float d = DBConst.BumperRadius * 2f;
            for (int territory = 0; territory < 2; territory++)
            {
                float zs = -DBConst.Sign(territory);
                for (int k = 0; k < DBConst.BackBumperAngles.Length; k++)
                    for (int s = -1; s <= 1; s += 2)
                    {
                        var go = DBUtil.Primitive(PrimitiveType.Cylinder, (territory == 0 ? "P1" : "P2") + " 陣地 Back Bumper", g,
                            DBConst.BackBumperPosition(territory, s, k), Quaternion.identity, new Vector3(d, 0.3f, d), mats["Bumper"], true);
                        var bm = go.AddComponent<DBBumper>();
                        bm.flashRenderer = go.GetComponent<Renderer>();
                        // 向かい合ったバンパーどうしでラリーにならないよう、はじく向きに必ず中央の線の方を入れる
                        bm.useInwardRule = false;
                        bm.biasDirection = new Vector3(0f, 0f, -zs);
                        bm.minBias = 0.45f;
                    }
            }
        }

        /// <summary>
        /// 三角柱のメッシュ（XZ の三角形を高さ h だけ伸ばす）。面ごとに頂点を分け、法線を明示する
        /// （開発メモ ステップ10b の教訓：表裏を同じ頂点で重ねない・法線を明示）。Generated に保存する
        /// </summary>
        static Mesh PrismMesh(Vector3[] tri, float h, string name)
        {
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var idx = new List<int>();
            Vector3 center = (tri[0] + tri[1] + tri[2]) / 3f;

            Vector3 up = Vector3.up * h;
            Face(verts, normals, idx, tri[0] + up, tri[1] + up, tri[2] + up, Vector3.up);
            Face(verts, normals, idx, tri[0], tri[1], tri[2], Vector3.down);
            for (int i = 0; i < 3; i++)
            {
                Vector3 p0 = tri[i], p1 = tri[(i + 1) % 3];
                Vector3 e = p1 - p0;
                Vector3 nrm = new Vector3(-e.z, 0f, e.x).normalized;
                if (Vector3.Dot(nrm, (p0 + p1) * 0.5f - center) < 0f) nrm = -nrm;
                Face(verts, normals, idx, p0, p1, p1 + up, nrm);
                Face(verts, normals, idx, p0, p1 + up, p0 + up, nrm);
            }

            string path = MatDir + "/" + name + ".asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool isNew = mesh == null;
            if (isNew) mesh = new Mesh();
            mesh.Clear();
            mesh.name = name;
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetTriangles(idx, 0);
            mesh.RecalculateBounds();
            if (isNew) AssetDatabase.CreateAsset(mesh, path);
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        /// <summary>三角形を 1 つ足す。法線の向きが表になるように頂点の順を直す</summary>
        static void Face(List<Vector3> verts, List<Vector3> normals, List<int> idx, Vector3 a, Vector3 b, Vector3 c, Vector3 normal)
        {
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), normal) < 0f) { var tmp = b; b = c; c = tmp; }
            int i0 = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c);
            normals.Add(normal); normals.Add(normal); normals.Add(normal);
            idx.Add(i0); idx.Add(i0 + 1); idx.Add(i0 + 2);
        }

        static void BuildBumpers(Transform t)
        {
            var g = new GameObject("Gimmicks").transform;
            g.SetParent(t, false);
            // 中央の線の左右に 1 つずつ（点対称でも同じ位置）
            for (int s = -1; s <= 1; s += 2)
            {
                float d = DBConst.BumperRadius * 2f;
                var go = DBUtil.Primitive(PrimitiveType.Cylinder, "Bumper " + (s < 0 ? "L" : "R"), g,
                    new Vector3(s * DBConst.BumperX, DBConst.BallY, DBConst.BumperZ), Quaternion.identity,
                    new Vector3(d, 0.3f, d), mats["Bumper"], true);
                var bm = go.AddComponent<DBBumper>();
                bm.flashRenderer = go.GetComponent<Renderer>();
            }
        }

        /// <summary>ドロップターゲット：各陣地の左右の壁ぎわに 3 つずつ（4 列）。攻める側の色</summary>
        static DBDropTargetBank[] BuildDropTargets(Transform t)
        {
            var g = new GameObject("Drop Targets").transform;
            g.SetParent(t, false);
            var list = new DBDropTargetBank[4];
            int n = 0;
            float th = DBConst.DropTargetThickness, w = DBConst.DropTargetWidth, h = DBConst.DropTargetHeight;
            for (int territory = 0; territory < 2; territory++)
            {
                int attacker = DBConst.Opponent(territory);
                float zs = -DBConst.Sign(territory);
                for (int s = -1; s <= 1; s += 2)
                {
                    var root = new GameObject((territory == 0 ? "P1" : "P2") + " 陣地 Drop Targets " + (s < 0 ? "L" : "R"));
                    root.transform.SetParent(g, false);
                    float x = s * (DBConst.PlayHalfWidth - th * 0.5f);
                    float zMid = zs * (DBConst.DropTargetFirstZ + (DBConst.DropTargetCount - 1) * 0.5f * (w + DBConst.DropTargetGap));
                    root.transform.position = new Vector3(x, 0f, zMid);
                    var targets = new Transform[DBConst.DropTargetCount];
                    for (int i = 0; i < DBConst.DropTargetCount; i++)
                    {
                        float z = zs * (DBConst.DropTargetFirstZ + i * (w + DBConst.DropTargetGap));
                        var tg = Box("Target " + (i + 1), root.transform, new Vector3(0f, h * 0.5f, z - zMid), new Vector3(th, h, w), PM(attacker));
                        targets[i] = tg.transform;
                    }
                    // 床の目印（倒すと沈むので、場所がわかるように）
                    FloorPlate("Mark", root.transform, new Vector3(x, 0f, zMid),
                        new Vector3(th + 0.1f, 0f, DBConst.DropTargetCount * (w + DBConst.DropTargetGap) + 0.05f), mats[attacker == 0 ? "P1Mark" : "P2Mark"]);
                    var bank = root.AddComponent<DBDropTargetBank>();
                    bank.territory = territory;
                    bank.side = s;
                    bank.targets = targets;
                    list[n++] = bank;
                }
            }
            return list;
        }

        /// <summary>
        /// スピナー：中立地帯に 1 つ（ステップ19。真ん中は避け、コースターのない帯の壁寄り）。
        /// 真上から見て回っているのがわかるように、門の上に十字の羽根（Y 軸まわりに回る）を載せる。
        /// 通り抜けの判定はトリガー、羽根・門の横木は見た目、両端の柱には当たり判定あり
        /// </summary>
        static void BuildSpinners(Transform t)
        {
            var g = new GameObject("Spinners").transform;
            g.SetParent(t, false);
            float w = DBConst.SpinnerWidth;
            {
                Vector3 c = DBConst.SpinnerCenter();
                var root = new GameObject("Spinner");
                root.transform.SetParent(g, false);
                root.transform.position = c;
                var trig = root.AddComponent<BoxCollider>();
                trig.isTrigger = true;
                trig.size = new Vector3(w, 1f, 0.3f);

                // 門：両端の柱（当たり判定あり）と横木（見た目）
                float pr = DBConst.SpinnerPostRadius;
                for (int e = -1; e <= 1; e += 2)
                    DBUtil.Primitive(PrimitiveType.Cylinder, "Post", root.transform, new Vector3(e * (w * 0.5f + pr), 0.1f, 0f), Quaternion.identity,
                        new Vector3(pr * 2f, 0.35f, pr * 2f), mats["Wall"], true);
                Box("Bar", root.transform, new Vector3(0f, 0.42f, 0f), new Vector3(w + pr * 4f, 0.05f, 0.1f), mats["Wall"], false);

                // 十字の羽根（真上から見て回る）
                var rotor = new GameObject("Rotor");
                rotor.transform.SetParent(root.transform, false);
                rotor.transform.localPosition = new Vector3(0f, 0.5f, 0f);
                DBUtil.Primitive(PrimitiveType.Cylinder, "Disc", rotor.transform, Vector3.zero, Quaternion.identity,
                    new Vector3(w * 0.95f, 0.01f, w * 0.95f), mats["SpinnerDark"], false);
                for (int k = 0; k < 2; k++)
                    Box("Blade " + (k + 1), rotor.transform, new Vector3(0f, 0.02f, 0f), new Vector3(w * 0.9f, 0.02f, 0.12f), mats["Spinner"], false, k * 90f);
                DBUtil.Primitive(PrimitiveType.Cylinder, "Hub", rotor.transform, new Vector3(0f, 0.03f, 0f), Quaternion.identity,
                    new Vector3(0.16f, 0.02f, 0.16f), mats["Wall"], false);

                // 床：通り道の線と、両向きの矢じり
                FloorPlate("Mark", g, DBUtil.Flat(c), new Vector3(w, 0f, 0.1f), mats["Spinner"]);
                for (int dz = -1; dz <= 1; dz += 2)
                    for (int e = -1; e <= 1; e += 2)
                        FloorPlate("Chevron", g, DBUtil.Flat(c) + new Vector3(e * 0.12f, 0f, dz * 0.45f), new Vector3(0.05f, 0f, 0.3f),
                            mats["Spinner"], e * dz * 35f);

                var sp = root.AddComponent<DBSpinner>();
                sp.plate = rotor.transform;
            }
        }

        // ------------------------------------------------------------------
        // ドッジボール台の専用ギミック（2026-11-27）

        /// <summary>見た目だけ：コートの線、横の観客席、四隅の照明（当たり判定なし）</summary>
        static void BuildDecor(Transform t)
        {
            var g = new GameObject("Decor").transform;
            g.SetParent(t, false);
            float W = DBConst.PlayHalfWidth, L = DBConst.PlayHalfLength;
            const float lw = 0.06f;

            // コートの線：外周、中央の線、内野の前の線（点線）
            FloorPlate("Line Center", g, new Vector3(0f, 0f, 0f), new Vector3(W * 2f, 0f, lw * 1.5f), mats["Line"]);
            for (int s = -1; s <= 1; s += 2)
            {
                FloorPlate("Line End", g, new Vector3(0f, 0f, s * (L - 0.15f)), new Vector3(W * 2f - 0.3f, 0f, lw), mats["Line"]);
                FloorPlate("Line Side", g, new Vector3(s * (W - 0.15f), 0f, 0f), new Vector3(lw, 0f, L * 2f - 0.3f), mats["Line"]);
                float z = s * DBConst.BlockerAreaNearZ;
                for (float x = -W + 0.6f; x < W - 0.4f; x += 0.8f)
                    FloorPlate("Line Attack", g, new Vector3(x + 0.2f, 0f, z), new Vector3(0.4f, 0f, lw), mats["Line"]);
            }
            // センターサークル
            const int seg = 32;
            for (int k = 0; k < seg; k++)
            {
                float a0 = k * Mathf.PI * 2f / seg, a1 = (k + 1) * Mathf.PI * 2f / seg;
                var p0 = new Vector3(Mathf.Sin(a0), 0f, Mathf.Cos(a0)) * 1.6f;
                var p1 = new Vector3(Mathf.Sin(a1), 0f, Mathf.Cos(a1)) * 1.6f;
                FloorPlate("Line Circle", g, (p0 + p1) * 0.5f, new Vector3(lw, 0f, (p1 - p0).magnitude + 0.01f), mats["Line"], DBUtil.YawOf(p1 - p0));
            }

            // 観客席（発射レーンの外。3 段）と観客
            float standX0 = DBConst.OuterHalfWidth + 0.6f;
            var rng = new System.Random(1127);
            string[] crowd = { "Crowd1", "Crowd2", "Crowd3", "P1", "P2" };
            for (int s = -1; s <= 1; s += 2)
            {
                for (int step = 0; step < 3; step++)
                {
                    float x = s * (standX0 + 0.5f + step * 1.0f);
                    float h = 0.25f + step * 0.3f;
                    Box("Stand", g, new Vector3(x, h * 0.5f, 0f), new Vector3(1.0f, h, L * 2f - 1f), mats["Stand"], false);
                    for (float z = -L + 1f; z <= L - 1f; z += 0.7f)
                    {
                        if (rng.NextDouble() < 0.25) continue;
                        float jz = (float)(rng.NextDouble() - 0.5) * 0.2f;
                        var m = mats[crowd[rng.Next(crowd.Length)]];
                        DBUtil.Primitive(PrimitiveType.Sphere, "Fan", g, new Vector3(x, h + 0.15f, z + jz), Quaternion.identity,
                            new Vector3(0.3f, 0.3f, 0.3f), m, false);
                    }
                }
            }
            // 四隅の照明（柱と、光る頭）
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    var c = new Vector3(sx * (standX0 + 3.6f), 0f, sz * (L - 0.5f));
                    DBUtil.Primitive(PrimitiveType.Cylinder, "Light Pole", g, c + Vector3.up * 1.0f, Quaternion.identity,
                        new Vector3(0.15f, 1.0f, 0.15f), mats["LightPole"], false);
                    Box("Light Head", g, c + Vector3.up * 2.05f, new Vector3(0.9f, 0.12f, 0.5f), mats["LightLamp"], false, sx * sz * 30f);
                }
        }

        /// <summary>
        /// コースターのレーン：各陣地に 1 本。延ばした入口（斜めの通路。トリガー）と床の目印、レールの支柱（見た目）。
        /// レールそのものは実行時に DBCoaster が LineRenderer で描く。
        /// 入口の向き（path[0] → path[1]）に合わせて root を回す。子の位置は InverseTransformPoint で入れる
        /// </summary>
        static DBCoaster[] BuildCoasters(Transform t)
        {
            var g = new GameObject("Coasters").transform;
            g.SetParent(t, false);
            var list = new DBCoaster[2];
            for (int p = 0; p < 2; p++)
            {
                Vector3[] path = DBConst.CoasterPath(p);
                Vector3 e0 = DBUtil.Flat(path[0]), e1 = DBUtil.Flat(path[1]);
                Vector3 rampDir = (e1 - e0).normalized;
                float rampLen = (e1 - e0).magnitude;
                Vector3 mid = (e0 + e1) * 0.5f;

                var root = new GameObject((p == 0 ? "P1" : "P2") + " Coaster");
                root.transform.SetParent(g, false);
                root.transform.SetPositionAndRotation(new Vector3(mid.x, DBConst.BallY, mid.z), Quaternion.Euler(0f, DBUtil.YawOf(rampDir), 0f));
                var rt = root.transform;

                // 延ばした入口（トリガー。斜めの通路ぜんぶ）
                var trig = root.AddComponent<BoxCollider>();
                trig.isTrigger = true;
                trig.size = new Vector3(DBConst.CoasterEntranceWidth, 1f, rampLen + 0.3f);

                // 床：入口の通路（持ち主の色・うすく）、両ふちの線、相手側へ向かう矢じり
                float hw = DBConst.CoasterEntranceWidth * 0.5f;
                Box("Entrance", rt, new Vector3(0f, 0.004f - DBConst.BallY, 0f), new Vector3(DBConst.CoasterEntranceWidth, 0.008f, rampLen), mats[p == 0 ? "P1Dim" : "P2Dim"], false);
                for (int e = -1; e <= 1; e += 2)
                    Box("Entrance Edge", rt, new Vector3(e * hw, 0.006f - DBConst.BallY, 0f), new Vector3(0.06f, 0.008f, rampLen), PM(p), false);
                for (int k = 0; k < 2; k++)
                    for (int e = -1; e <= 1; e += 2)
                        Box("Arrow", rt, new Vector3(e * 0.15f, 0.008f - DBConst.BallY, -rampLen * 0.25f + k * rampLen * 0.4f), new Vector3(0.06f, 0.008f, 0.4f), PM(p), false, -e * 35f);

                // 支柱（高いところだけ、見た目）。ワールドの位置を root から見た位置に直す
                for (int i = 2; i < path.Length - 1; i++)
                {
                    if (path[i].y < 0.5f) continue;
                    var sup = DBUtil.Primitive(PrimitiveType.Cylinder, "Support", rt, Vector3.zero, Quaternion.identity,
                        new Vector3(0.06f, path[i].y * 0.5f, 0.06f), mats["Coaster"], false);
                    sup.transform.position = new Vector3(path[i].x, path[i].y * 0.5f, path[i].z);
                    sup.transform.rotation = Quaternion.identity;
                }

                // 終点の射出装置（見た目：台と、持ち主の色のランプ。ためている間は点滅）
                Vector3 end = path[path.Length - 1];
                var launcher = DBUtil.Primitive(PrimitiveType.Cylinder, "Launcher", rt, Vector3.zero, Quaternion.identity,
                    new Vector3(0.75f, 0.03f, 0.75f), mats["Coaster"], false);
                launcher.transform.position = new Vector3(end.x, 0.02f, end.z);
                launcher.transform.rotation = Quaternion.identity;
                var lamp = DBUtil.Primitive(PrimitiveType.Cylinder, "Launcher Lamp", rt, Vector3.zero, Quaternion.identity,
                    new Vector3(0.9f, 0.01f, 0.9f), PM(p), false);
                lamp.transform.position = new Vector3(end.x, 0.005f, end.z);
                lamp.transform.rotation = Quaternion.identity;

                // 射出装置の籠（BaseFix06。見た目だけ）。口を相手の内野の真ん中へ向ける
                Vector3 infield = new Vector3(0f, 0f, DBConst.Sign(p) * (DBConst.BlockerAreaNearZ + DBConst.BlockerAreaGoalZ) * 0.5f);
                BuildCoasterCage(rt, new Vector3(end.x, 0f, end.z), infield - new Vector3(end.x, 0f, end.z), mats["Coaster"]);

                var co = root.AddComponent<DBCoaster>();
                co.owner = p;
                co.path = path;
                co.launcherLamp = lamp.GetComponent<Renderer>();
                list[p] = co;
            }
            return list;
        }

        /// <summary>
        /// 射出装置にかぶせる籠（格子の半球。見た目だけ、当たり判定なし）。mouthDir の向き（XZ）に口を開ける。
        /// 縦の格子（30° ごと）と横の輪（仰角 0・30・55・75°）。口の範囲（±CoasterCageMouthHalfAngle、仰角 CoasterCageMouthTop まで）は開けておく
        /// </summary>
        static void BuildCoasterCage(Transform parent, Vector3 center, Vector3 mouthDir, Material mat)
        {
            var cage = new GameObject("Launcher Cage").transform;
            cage.SetParent(parent, false);
            cage.position = center;
            mouthDir.y = 0f;
            cage.rotation = mouthDir.sqrMagnitude > 1e-6f ? Quaternion.Euler(0f, DBUtil.YawOf(mouthDir), 0f) : Quaternion.identity;

            float r = DBConst.CoasterCageRadius;
            float mouth = DBConst.CoasterCageMouthHalfAngle;
            float mouthTop = DBConst.CoasterCageMouthTop;
            float bar = DBConst.CoasterCageBar;

            // 縦の格子（籠の前 = ローカルの +Z）
            for (int az = 0; az < 360; az += 30)
            {
                float a = az > 180 ? az - 360 : az;
                bool inMouth = Mathf.Abs(a) < mouth;
                float e0 = inMouth ? mouthTop : 0f;
                const int steps = 6;
                for (int k = 0; k < steps; k++)
                {
                    float ea = Mathf.Lerp(e0, 90f, k / (float)steps);
                    float eb = Mathf.Lerp(e0, 90f, (k + 1) / (float)steps);
                    CageBar("Cage Rib", cage, DomePoint(r, a, ea), DomePoint(r, a, eb), bar, mat);
                }
            }
            // 横の輪
            float[] rings = { 0f, 30f, mouthTop, 75f };
            for (int i = 0; i < rings.Length; i++)
            {
                float e = rings[i];
                bool open = e < mouthTop; // 口の高さより下の輪は、口のところを開ける
                const int segs = 24;
                for (int k = 0; k < segs; k++)
                {
                    float a0 = -180f + 360f * k / segs, a1 = -180f + 360f * (k + 1) / segs;
                    float mid = (a0 + a1) * 0.5f;
                    if (open && Mathf.Abs(mid) < mouth) continue;
                    CageBar("Cage Ring", cage, DomePoint(r, a0, e), DomePoint(r, a1, e), bar, mat);
                }
            }
        }

        /// <summary>半球の上の点（ローカル。az = 前（+Z）からの向き、el = 仰角）</summary>
        static Vector3 DomePoint(float r, float azDeg, float elDeg)
        {
            float az = azDeg * Mathf.Deg2Rad, el = elDeg * Mathf.Deg2Rad;
            return new Vector3(r * Mathf.Cos(el) * Mathf.Sin(az), r * Mathf.Sin(el), r * Mathf.Cos(el) * Mathf.Cos(az));
        }

        /// <summary>2 点の間の細い棒（ローカル。見た目だけ）</summary>
        static void CageBar(string name, Transform parent, Vector3 a, Vector3 b, float thickness, Material mat)
        {
            Vector3 d = b - a;
            float len = d.magnitude;
            if (len < 1e-4f) return;
            Vector3 dir = d / len;
            Vector3 up = Mathf.Abs(dir.y) > 0.99f ? Vector3.right : Vector3.up;
            DBUtil.Primitive(PrimitiveType.Cube, name, parent, (a + b) * 0.5f, Quaternion.LookRotation(dir, up),
                new Vector3(thickness, thickness, len + thickness), mat, false);
        }

        static DBBlockerRow[] BuildBlockerRows(Transform t)
        {
            var list = new DBBlockerRow[2];
            for (int p = 0; p < 2; p++)
            {
                float zs = -DBConst.Sign(p);
                string pn = p == 0 ? "P1" : "P2";

                // ブロッカーの範囲（床の目印）
                float nz = DBConst.BlockerAreaNearZ, gz = DBConst.BlockerAreaGoalZ;
                FloorPlate(pn + " Blocker Area", t, new Vector3(0f, 0f, zs * (nz + gz) * 0.5f),
                    new Vector3(DBConst.BlockerAreaHalfX * 2f, 0f, gz - nz), mats[pn + "Dim"]);

                var go = new GameObject(pn + " Blockers");
                go.transform.SetParent(t, false);
                var start = new Vector3(0f, DBConst.BallY, zs * DBConst.BlockerStartZ);
                go.transform.position = start;
                var rb = go.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.useGravity = false;

                int n = DBConst.BlockerCount;
                var blockers = new Transform[n];
                for (int i = 0; i < n; i++)
                {
                    float x = (i - (n - 1) * 0.5f) * DBConst.BlockerSpacing;
                    // Y 向きのカプセル（高さ 2・半径 0.5）を Z 軸で 90° 倒して X 向きに。0.5 倍で 長さ 1.0・半径 0.25
                    var b = DBUtil.Primitive(PrimitiveType.Capsule, "Blocker " + (i + 1), go.transform,
                        new Vector3(x, 0f, 0f), Quaternion.Euler(0f, 0f, 90f),
                        Vector3.one * (DBConst.BlockerLength * 0.5f), PM(p), true);
                    blockers[i] = b.transform;
                }

                var row = go.AddComponent<DBBlockerRow>();
                row.owner = p;
                row.startPosition = start;
                row.blockers = blockers;
                row.blockerLength = DBConst.BlockerLength;
                row.blockerRadius = DBConst.BlockerRadius;
                list[p] = row;
            }
            return list;
        }

        static DBServeLane[] BuildLanes(Transform t, Dictionary<string, DoorInfo> doors)
        {
            var list = new DBServeLane[2];
            for (int p = 0; p < 2; p++)
            {
                string pn = p == 0 ? "P1" : "P2";
                float sg = DBConst.Sign(p);
                var go = new GameObject(pn + " Serve Lane");
                go.transform.SetParent(t, false);
                var lane = go.AddComponent<DBServeLane>();
                lane.owner = p;
                lane.laneX = DBConst.LaneCenterX(p);
                lane.plungerPosition = new Vector3(lane.laneX, DBConst.BallY, -sg * DBConst.PlungerZ);
                lane.ownExitZ = -sg * DBConst.ServeOwnExitZ;
                lane.oppExitZ = sg * DBConst.ServeOppExitZ;
                lane.ownDoor = doors[pn + "Own"].col;
                lane.ownDoorRenderer = doors[pn + "Own"].rend;
                lane.oppDoor = doors[pn + "Opp"].col;
                lane.oppDoorRenderer = doors[pn + "Opp"].rend;

                // レーンの床とプランジャー（見た目のみ）
                float laneW = DBConst.LaneOuterX - DBConst.LaneInnerX;
                FloorPlate("Lane Floor", go.transform, new Vector3(lane.laneX, 0f, 0f), new Vector3(laneW, 0f, DBConst.PlayHalfLength * 2f), mats[pn + "Dim"]);
                Box("Plunger", go.transform, new Vector3(lane.laneX, 0.15f, -sg * (DBConst.PlayHalfLength - 0.03f)),
                    new Vector3(laneW * 0.8f, 0.3f, 0.05f), PM(p), false);
                list[p] = lane;
            }
            return list;
        }

        /// <summary>外野ブロッカーが出る場所の床の目印（見た目のみ）</summary>
        static void BuildOutfieldMarks(Transform t)
        {
            var gm = Object.FindFirstObjectByType<DBGameManager>();
            float[] slotZ = gm != null && gm.outfieldZ != null && gm.outfieldZ.Length >= 2 ? gm.outfieldZ : DBConst.OutfieldZ;
            float ox = gm != null ? gm.outfieldX : DBConst.OutfieldX;

            var g = new GameObject("Outfield Marks").transform;
            g.SetParent(t, false);
            for (int p = 0; p < 2; p++)
                for (int w = 0; w < 2; w++)
                    for (int k = 0; k < 2; k++)
                    {
                        float wallSign = w == 0 ? -1f : 1f;
                        var c = new Vector3(wallSign * ox, 0f, DBConst.Sign(p) * slotZ[k]);
                        FloorPlate((p == 0 ? "P1" : "P2") + " Outfield Mark", g, c,
                            new Vector3(DBConst.BlockerRadius * 2f + 0.1f, 0f, DBConst.BlockerLength + 0.1f),
                            mats[p == 0 ? "P1Mark" : "P2Mark"]);
                    }
        }

        static DBBall BuildBall(Transform t)
        {
            float d = DBConst.BallRadius * 2f;
            var go = DBUtil.Primitive(PrimitiveType.Sphere, "Ball", t, new Vector3(0f, DBConst.BallY, 0f), Quaternion.identity,
                new Vector3(d, d, d), mats["Ball"], true);
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 1f;
            rb.useGravity = false;
            return go.AddComponent<DBBall>();
        }

        static DBGameManager SetupGame(DBBall ball, DBBlockerRow[] rows, DBServeLane[] lanes,
            DBDropTargetBank[] banks, DBCoaster[] coasters, Transform outfieldRoot)
        {
            var gm = Object.FindFirstObjectByType<DBGameManager>();
            if (gm == null)
            {
                var go = new GameObject(GameName);
                Undo.RegisterCreatedObjectUndo(go, "Create Dodgeball Game");
                gm = go.AddComponent<DBGameManager>();
            }
            var hud = gm.GetComponent<DBHud>();
            if (hud == null) hud = gm.gameObject.AddComponent<DBHud>();
            hud.gm = gm;

            Undo.RecordObject(gm, "Setup Dodgeball Game");
            gm.ball = ball;
            gm.rows = rows;
            gm.lanes = lanes;
            gm.dropTargetBanks = banks;
            gm.coasters = coasters;
            gm.outfieldRoot = outfieldRoot;
            gm.playerMaterials = new[] { mats["P1"], mats["P2"] };
            EditorUtility.SetDirty(gm);
            EditorUtility.SetDirty(hud);
            return gm;
        }

        static void SetupCamera()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera");
                Undo.RegisterCreatedObjectUndo(go, "Create Camera");
                go.tag = "MainCamera";
                cam = go.AddComponent<Camera>();
            }
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.03f, 0.04f, 0.07f);
            cam.orthographic = true;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;
            if (cam.GetComponent<DBCameraFit>() == null) cam.gameObject.AddComponent<DBCameraFit>();
        }

        static void SetupLight()
        {
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) return;
            var go = new GameObject("Directional Light");
            Undo.RegisterCreatedObjectUndo(go, "Create Light");
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            go.transform.rotation = Quaternion.Euler(60f, -30f, 0f);
        }
    }
}
