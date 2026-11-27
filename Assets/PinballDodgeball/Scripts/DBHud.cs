using UnityEngine;

namespace PinballDodgeball
{
    /// <summary>
    /// α版の簡単な表示（IMGUI）。タイトル・各プレイヤーのパネル・知らせ・決着画面。
    /// 日本語が四角になるときは font に日本語フォント（例：BIZ UDPゴシックの .ttf）を入れる。
    /// P1 = 左下、P2 / CPU = 右上、知らせ = 中央。
    /// </summary>
    public class DBHud : MonoBehaviour
    {
        public DBGameManager gm;
        public Font font;
        [Tooltip("画面の高さ 1080 のときの文字の大きさ")]
        public float baseFontSize = 26f;

        int sel;
        GUIStyle label, title, panel, center;
        float lastScale = -1f;

        const int ItemMode = 0, ItemCpu = 1, ItemOutfield = 2, ItemShoot = 3, ItemFlipP1 = 4, ItemFlipP2 = 5, ItemStart = 6, ItemCount = 7;

        void Update()
        {
            if (gm == null) gm = DBGameManager.Instance;
            if (gm == null) return;
            var u = DBInput.ReadUI();

            if (gm.CurrentState == DBGameManager.State.Title)
            {
                if (u.up) sel = (sel + ItemCount - 1) % ItemCount;
                if (u.down) sel = (sel + 1) % ItemCount;
                if (u.left) Change(sel, -1);
                if (u.right) Change(sel, 1);
                if (u.submit)
                {
                    if (sel == ItemStart) gm.StartMatch();
                    else Change(sel, 1);
                }
            }
            else if (gm.CurrentState == DBGameManager.State.Over)
            {
                if (u.submit) gm.StartMatch();
                else if (u.cancel) gm.GoTitle();
            }
        }

        void Change(int item, int d)
        {
            switch (item)
            {
                case ItemMode:
                    gm.mode = gm.mode == DBGameManager.Mode.VsCpu ? DBGameManager.Mode.Versus : DBGameManager.Mode.VsCpu;
                    break;
                case ItemCpu:
                    gm.cpuLevel = (DBGameManager.CpuLevel)(((int)gm.cpuLevel + d + 3) % 3);
                    break;
                case ItemOutfield:
                    gm.outfieldControl = gm.outfieldControl == DBGameManager.OutfieldControl.Auto
                        ? DBGameManager.OutfieldControl.Buttons : DBGameManager.OutfieldControl.Auto;
                    break;
                case ItemShoot:
                    gm.shootControl = gm.shootControl == DBGameManager.ShootControl.Auto
                        ? DBGameManager.ShootControl.Buttons : DBGameManager.ShootControl.Auto;
                    break;
                case ItemFlipP1:
                    gm.flipperAuto[0] = !gm.flipperAuto[0];
                    break;
                case ItemFlipP2:
                    if (!gm.IsCpu(1)) gm.flipperAuto[1] = !gm.flipperAuto[1];
                    break;
            }
        }

        void Styles(float k)
        {
            if (Mathf.Approximately(k, lastScale) && label != null) return;
            lastScale = k;
            label = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(baseFontSize * k), richText = true, wordWrap = true };
            title = new GUIStyle(label) { fontSize = Mathf.RoundToInt(baseFontSize * 1.8f * k), alignment = TextAnchor.MiddleCenter };
            center = new GUIStyle(label) { fontSize = Mathf.RoundToInt(baseFontSize * 1.4f * k), alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            panel = new GUIStyle(GUI.skin.box) { padding = new RectOffset(12, 12, 10, 10) };
            if (font != null) { label.font = font; title.font = font; center.font = font; }
        }

        void OnGUI()
        {
            if (gm == null) return;
            float k = Screen.height / 1080f;
            Styles(k);

            if (gm.CurrentState == DBGameManager.State.Title)
            {
                DrawTitle(k);
                return;
            }

            DrawPanel(0, new Rect(20f * k, Screen.height - 380f * k, 420f * k, 360f * k), k);
            DrawPanel(1, new Rect(Screen.width - 440f * k, 20f * k, 420f * k, 360f * k), k);
            DrawServeLabels(k);
            DrawMessages(k);
            if (gm.CurrentState == DBGameManager.State.Over) DrawResult(k);
        }

        void DrawTitle(float k)
        {
            float w = 820f * k, h = 760f * k;
            var r = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
            GUI.Box(r, GUIContent.none, panel);
            GUILayout.BeginArea(new Rect(r.x + 30f * k, r.y + 20f * k, r.width - 60f * k, r.height - 40f * k));
            GUILayout.Label("ピンボール ドッジボール（α）", title);
            GUILayout.Space(10f * k);
            GUILayout.Label("相手の選手（ブロッカー 3 人）を全員アウトにしたら勝ち。\nアウトになった選手は相手陣地の左右（選手の範囲のすぐ外）で外野になる。外野は動けないが、球を自動でキャッチする。外野が投げた球で相手をアウトにすると戻ってくる。\n球の色 = いま誰の球か（白 = 誰の球でもない）。相手の色の球に当たるとアウト。\n球が来る直前にキャッチのボタン（パッドは RB）を押してキャッチ（ジャストキャッチ）すると、投げる球がパワーショット（速い球）になる。\nフリッパーの間から球を落とすと、相手のシュートポスト（自陣の角）から斜め後ろへ速い球を撃たれる。\n相手陣地の壁ぎわのドロップターゲットを 3 つ倒すと、自分のシュートポストから撃てる。中立地帯のスピナーを通ると球の向きが少しぶれ、勢いよく通すと加速する。\n右のフリッパーで左へ寄せて打った球を、相手の陣地の入口（自分の色の帯）に入れると、壁ぞいのレーンを走った先の射出装置から、相手の内野の選手へ横・後ろから撃ち出される。\nガイドの上の矢印の帯は加速レーン。転がり落ちる球をフリッパーへ速く運ぶ。", label);
            GUILayout.Space(16f * k);

            Item(ItemMode, "モード", gm.mode == DBGameManager.Mode.VsCpu ? "VS CPU" : "1 VS 1");
            if (gm.mode == DBGameManager.Mode.VsCpu)
                Item(ItemCpu, "CPU の強さ", gm.cpuLevel.ToString().ToUpperInvariant());
            else
                Item(ItemCpu, "CPU の強さ", "―");
            Item(ItemOutfield, "外野の投げ方", gm.outfieldControl == DBGameManager.OutfieldControl.Auto ? "全自動" : "ボタン（RB で投げる・スティックの上下で向き）");
            Item(ItemShoot, "シュートポストの撃ち方", gm.shootControl == DBGameManager.ShootControl.Auto ? "全自動" : "ボタン（RB で撃つ・スティックの左右で向き）");
            Item(ItemFlipP1, "P1 のフリッパー", gm.flipperAuto[0] ? "自動" : "手動");
            Item(ItemFlipP2, (gm.IsCpu(1) ? "CPU" : "P2") + " のフリッパー", gm.IsCpu(1) ? "自動（CPU）" : (gm.flipperAuto[1] ? "自動" : "手動"));
            Item(ItemStart, "▶ スタート", "");

            GUILayout.Space(10f * k);
            GUILayout.Label("<size=" + Mathf.RoundToInt(baseFontSize * 0.75f * k) + ">上下：選ぶ　左右：変える　Enter / Space / A：決定</size>", label);
            GUILayout.EndArea();
        }

        void Item(int index, string name, string value)
        {
            bool on = sel == index;
            string text = (on ? "<color=#ffd84a>▶ " : "<color=#ffffff>　") + name + (value.Length > 0 ? "：" + value : "") + "</color>";
            if (GUILayout.Button(text, label))
            {
                sel = index;
                if (index == ItemStart) gm.StartMatch();
                else Change(index, 1);
            }
        }

        void DrawPanel(int p, Rect r, float k)
        {
            GUI.Box(r, GUIContent.none, panel);
            var row = gm.rows[p];
            string col = "#" + ColorUtility.ToHtmlStringRGB(gm.PlayerColor(p));
            var sb = new System.Text.StringBuilder();
            sb.Append("<b><color=").Append(col).Append(">").Append(gm.PlayerName(p)).Append("</color></b>\n");

            if (row != null)
            {
                sb.Append("選手　");
                for (int i = 0; i < row.Count; i++)
                    sb.Append(row.IsAlive(i) ? "<color=" + col + ">●</color>" : row.IsPendingRevive(i) ? "◐" : "○");
                sb.Append("　外野 ").Append(gm.OutfieldCount(p)).Append(" 人\n");
                if (row.IsJustCatch) sb.Append("<color=#ffd84a>ジャストキャッチ！ 離すとパワーショット</color>\n");
                else if (row.IsCatching) sb.Append("キャッチ中\n");
                // 相手陣地のドロップターゲット（自分が倒す的）の進み具合
                var tsb = new System.Text.StringBuilder();
                foreach (var bank in gm.dropTargetBanks)
                {
                    if (bank == null || bank.Attacker != p) continue;
                    tsb.Append(bank.side < 0 ? " 左" : " 右");
                    for (int i = 0; i < bank.Count; i++) tsb.Append(bank.IsDown(i) ? "<color=" + col + ">●</color>" : "○");
                }
                if (tsb.Length > 0) sb.Append("ターゲット").Append(tsb).Append("\n");
                var myPost = gm.LoadedShootPost(p);
                if (myPost != null)
                    sb.Append("<color=#ffd84a>").Append(myPost.autoFire ? "シュートポスト セット" : "シュートポスト セット（" + Key(p, "B") + " で撃つ）").Append("</color>\n");
                if (gm.LoadedShootPost(DBConst.Opponent(p)) != null)
                    sb.Append("<color=#ff6060>シュートポストから来る！</color>\n");
                if (gm.OutfieldHolding(p))
                    sb.Append(gm.outfieldControl == DBGameManager.OutfieldControl.Auto || gm.IsCpu(p)
                        ? "外野がキャッチ中\n" : "外野がキャッチ中（" + Key(p, "B") + " で投げる）\n");
            }

            if (gm.CurrentState == DBGameManager.State.Serve && gm.Server == p)
            {
                sb.Append("サーブ：").Append(gm.RouteOpponent ? "相手陣地へ" : "自陣へ");
                if (!gm.ServeLaunched) sb.Append("（").Append(Key(p, "Y")).Append(" で切り替え）");
                sb.Append("\nファウル ").Append(gm.Faults).Append("\n");
            }

            sb.Append("<size=").Append(Mathf.RoundToInt(baseFontSize * 0.7f * k)).Append(">");
            if (gm.IsCpu(p) || gm.flipperAuto[p]) sb.Append("フリッパー：自動\n");
            else sb.Append(Key(p, "L")).Append(" / ").Append(Key(p, "R")).Append("：フリッパー\n");
            sb.Append(Key(p, "Move")).Append("：選手を動かす\n");
            sb.Append(Key(p, "B")).Append("：発射（長押しで強く）・キャッチ（押している間。球が来る直前に押すとジャストキャッチ）\n");
            sb.Append(Key(p, "Y")).Append("：サーブのルート切り替え");
            if (gm.outfieldControl == DBGameManager.OutfieldControl.Buttons && !gm.IsCpu(p))
                sb.Append("\n外野：").Append(Key(p, "B")).Append(" で投げる、").Append(Key(p, "Move")).Append(" の上下で向き");
            sb.Append("</size>");

            var inner = new Rect(r.x + 14f * k, r.y + 10f * k, r.width - 28f * k, r.height - 20f * k);
            GUI.Label(inner, sb.ToString(), label);

            // 発射のゲージ
            if (gm.CurrentState == DBGameManager.State.Serve && gm.Server == p && gm.Charging)
            {
                var bar = new Rect(inner.x, inner.yMax - 18f * k, inner.width, 12f * k);
                var old = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, 0.25f);
                GUI.DrawTexture(bar, Texture2D.whiteTexture);
                GUI.color = gm.PlayerColor(p);
                GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * gm.Charge01, bar.height), Texture2D.whiteTexture);
                GUI.color = old;
            }
        }

        /// <summary>操作の表示。割り当ては DBInput（対戦ルールと同じ Input Actions）から読む</summary>
        string Key(int p, string id)
        {
            if (gm.IsCpu(p)) return "―";
            return DBInput.Label(p, id);
        }

        /// <summary>サーブ中：2 つの出入り口の横に「自陣へ」「相手陣地へ」。選んでいる方を明るく</summary>
        void DrawServeLabels(float k)
        {
            if (gm.CurrentState != DBGameManager.State.Serve || gm.ServeLaunched) return;
            var cam = Camera.main;
            var lane = gm.lanes[gm.Server];
            if (cam == null || lane == null) return;
            float x = Mathf.Sign(lane.laneX) * (DBConst.PlayHalfWidth - 0.9f);
            var old = GUI.color;
            Color c = gm.PlayerColor(gm.Server);
            ServeLabel(cam, new Vector3(x, 0f, lane.ownExitZ), "自陣へ", !gm.RouteOpponent, c, k);
            ServeLabel(cam, new Vector3(x, 0f, lane.oppExitZ), "相手陣地へ", gm.RouteOpponent, c, k);
            GUI.color = old;
        }

        void ServeLabel(Camera cam, Vector3 world, string text, bool on, Color c, float k)
        {
            Vector3 sp = cam.WorldToScreenPoint(world);
            GUI.color = on ? c : new Color(1f, 1f, 1f, 0.35f);
            var r = new Rect(sp.x - 120f * k, Screen.height - sp.y - 22f * k, 240f * k, 44f * k);
            GUI.Label(r, on ? "<b>" + text + "</b>" : text, center);
        }

        void DrawMessages(float k)
        {
            var cam = Camera.main;
            float t = Time.time;
            int centerIndex = 0;
            foreach (var m in gm.messages)
            {
                float age = t - m.start;
                float alpha = Mathf.Clamp01((m.until - t) / 0.3f);
                var c = m.color;
                c.a = alpha;
                var old = GUI.color;
                GUI.color = c;
                if (m.hasWorld && cam != null)
                {
                    Vector3 sp = cam.WorldToScreenPoint(m.world);
                    float rise = age * 30f * k;
                    var r = new Rect(sp.x - 200f * k, Screen.height - sp.y - 40f * k - rise, 400f * k, 50f * k);
                    GUI.Label(r, m.text, center);
                }
                else
                {
                    var r = new Rect(0f, Screen.height * 0.42f + centerIndex * 60f * k, Screen.width, 60f * k);
                    GUI.Label(r, m.text, center);
                    centerIndex++;
                }
                GUI.color = old;
            }
        }

        void DrawResult(float k)
        {
            float w = 640f * k, h = 260f * k;
            var r = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
            GUI.Box(r, GUIContent.none, panel);
            string col = "#" + ColorUtility.ToHtmlStringRGB(gm.PlayerColor(Mathf.Max(0, gm.Winner)));
            GUI.Label(new Rect(r.x, r.y + 20f * k, r.width, 90f * k),
                "<color=" + col + ">" + gm.PlayerName(Mathf.Max(0, gm.Winner)) + " の勝ち！</color>", title);
            GUI.Label(new Rect(r.x, r.y + 110f * k, r.width, 50f * k), "相手の選手が全員アウト", center);
            GUI.Label(new Rect(r.x + 20f * k, r.y + 180f * k, r.width - 40f * k, 60f * k),
                "Enter / A：もう一度　　Esc / Select：タイトルへ", center);
        }
    }
}
