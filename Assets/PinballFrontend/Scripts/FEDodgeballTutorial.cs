using PinballDodgeball;
using UnityEngine;

namespace PinballFrontend
{
    /// <summary>
    /// ドッジボールのあそびかた。本編は直さずに使える。
    /// ・DBHud（IMGUI の HUD）を止める（右の説明と重なるため）。試合の様子は右に出す
    /// ・試合が終わったら、少し待ってやり直す
    /// ・サーブは P1 から：本編の StartMatch() は P1 → P2 → … と交互に始めるので、P2 からになったらもう一度呼ぶ
    /// </summary>
    public sealed class FEDodgeballTutorial : IFETutorialRule
    {
        const float RestartDelay = 2f;

        DBGameManager gm;
        float restartAt = -1f;
        bool firstServeChecked;
        int lastWinner = -1;

        public bool Setup(FEMatchSettings s)
        {
            gm = DBGameManager.Instance;
            if (gm == null) return false;

            var hud = Object.FindAnyObjectByType<DBHud>();
            if (hud != null) hud.enabled = false;

            gm.MatchOver += OnMatchOver;
            return true;
        }

        void OnMatchOver(int winner)
        {
            lastWinner = winner;
            // 本編のイベントの途中（選手がアウトになった処理の中）なので、すぐにはやり直さない
            restartAt = Time.time + RestartDelay;
        }

        public void Tick()
        {
            if (gm == null) return;

            // 最初の試合（本編の Start で始まる）も P1 のサーブになっているか確かめる
            if (!firstServeChecked && gm.CurrentState != DBGameManager.State.Title)
            {
                firstServeChecked = true;
                if (gm.CurrentState == DBGameManager.State.Serve && gm.Server != 0) Restart();
            }

            if (restartAt > 0f && Time.time >= restartAt)
            {
                restartAt = -1f;
                Restart();
            }
        }

        void Restart()
        {
            lastWinner = -1;
            gm.StartMatch();
            if (gm.Server != 0) gm.StartMatch(); // 交互に始まるので、もう一度で P1 になる
        }

        public string StatusText()
        {
            if (gm == null) return "";
            var sb = new System.Text.StringBuilder();
            for (int p = 0; p < 2; p++)
            {
                var row = gm.rows != null && p < gm.rows.Length ? gm.rows[p] : null;
                int alive = 0, count = row != null ? row.Count : 0;
                for (int i = 0; i < count; i++) if (row.IsAlive(i)) alive++;
                sb.Append(Colored(gm.PlayerName(p), gm.PlayerColor(p)))
                  .Append("　選手 ").Append(alive).Append(" / ").Append(count)
                  .Append("　外野 ").Append(gm.OutfieldCount(p)).Append(" 人\n");
            }

            if (gm.CurrentState == DBGameManager.State.Over && lastWinner >= 0)
                sb.Append(Colored(gm.PlayerName(lastWinner) + " の勝ち！", gm.PlayerColor(lastWinner))).Append("　もう一度はじめます");
            else if (gm.CurrentState == DBGameManager.State.Serve)
            {
                sb.Append(gm.PlayerName(gm.Server)).Append(" のサーブ：").Append(gm.RouteOpponent ? "相手陣地へ" : "自陣へ");
                if (gm.Charging) sb.Append("　溜め ").Append(Mathf.RoundToInt(gm.Charge01 * 100f)).Append("%");
            }
            return sb.ToString();
        }

        public string ButtonLabel(string token, int player)
        {
            switch (token)
            {
                case "Serve":
                case "Catch": return DBInput.Label(player, "B");
                case "Route": return DBInput.Label(player, "Y");
                case "Move": return DBInput.Label(player, "Move");
                default: return null;
            }
        }

        public (string action, string token)[] ControlRows { get; } =
        {
            ("選手移動", "Move"),
            ("サーブ", "Serve"),
            ("ルート切替", "Route"),
            ("キャッチ", "Catch"),
        };

        static string Colored(string text, Color c) => "<color=#" + ColorUtility.ToHtmlStringRGB(c) + ">" + text + "</color>";

        public void Dispose()
        {
            if (gm != null) gm.MatchOver -= OnMatchOver;
            gm = null;
        }
    }
}
