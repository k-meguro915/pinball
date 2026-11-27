using Pinball;
using UnityEngine;

namespace PinballFrontend
{
    /// <summary>
    /// 対戦ルールのあそびかた。
    /// ・PinballHud（uGUI の HUD）を見えなくする（右の説明と重なるため）。部品は止めない（止めると本編が内蔵の HUD を出す）。
    ///   Canvas を止めると PinballHud が警告を出すので、映す先を使っていない画面（Display 2）に変えて見えなくする
    /// ・試合が終わったら、少し待ってやり直す
    /// ・サーブは P1 から（GameManager.ApplyRules の最初のサーブ番。やり直しても P1 から）
    /// </summary>
    public sealed class FEVersusTutorial : IFETutorialRule
    {
        const float RestartDelay = 2f;

        GameManager gm;
        float restartAt = -1f;

        public bool Setup(FEMatchSettings s)
        {
            gm = GameManager.Instance;
            if (gm == null) return false;

            // Adapter が入れた設定に、最初のサーブ番（P1）を足して入れ直す
            gm.ApplyRules(FEVersusAdapter.ToMatchMode(s.playMode), s.targetScore,
                          FEVersusAdapter.ToCpuLevel(s.cpuLevel), 0);

            var hud = Object.FindAnyObjectByType<PinballUI.PinballHud>();
            if (hud != null)
            {
                var canvas = hud.GetComponentInParent<Canvas>();
                if (canvas != null) canvas.rootCanvas.targetDisplay = 1; // 使っていない画面に映す = 見えない
            }

            gm.onMatchOver.AddListener(OnMatchOver);
            return true;
        }

        void OnMatchOver()
        {
            restartAt = Time.time + RestartDelay;
        }

        public void Tick()
        {
            if (gm == null) return;
            if (restartAt > 0f && Time.time >= restartAt)
            {
                restartAt = -1f;
                if (gm.State == MatchState.MatchOver) gm.StartMatch();
            }
        }

        public string StatusText()
        {
            if (gm == null) return "";
            var sb = new System.Text.StringBuilder();
            sb.Append(Colored(gm.PlayerName(0), GameManager.PlayerColor(0))).Append("　").Append(gm.Scores[0])
              .Append("　－　").Append(gm.Scores[1]).Append("　").Append(Colored(gm.PlayerName(1), GameManager.PlayerColor(1)))
              .Append("\n");

            switch (gm.State)
            {
                case MatchState.MatchOver:
                    if (gm.Winner >= 0)
                        sb.Append(Colored(gm.PlayerName(gm.Winner) + " の勝ち！", GameManager.PlayerColor(gm.Winner))).Append("　もう一度はじめます");
                    break;
                case MatchState.Serve:
                    sb.Append(gm.PlayerName(gm.Server)).Append(" のサーブ：")
                      .Append(gm.CurrentRoute == ServeRoute.OwnField ? "自陣ルート" : "相手陣地ルート");
                    var pl = gm.plungers != null && gm.Server < gm.plungers.Length ? gm.plungers[gm.Server] : null;
                    if (pl != null && pl.Charge > 0.01f) sb.Append("　溜め ").Append(Mathf.RoundToInt(pl.Charge * 100f)).Append("%");
                    if (gm.Faults > 0) sb.Append("　フォルト ").Append(gm.Faults);
                    break;
            }
            return sb.ToString();
        }

        public string ButtonLabel(string token, int player)
        {
            string action;
            switch (token)
            {
                case "L": action = "FlipperTop"; break;
                case "R": action = "FlipperBottom"; break;
                case "Serve": action = "Plunger"; break;
                case "Route": action = "RouteToggle"; break;
                case "Move": action = "MoveBlockers"; break;
                case "Catch": action = "BlockerCatch"; break;
                case "Bounce": action = "BlockerBounce"; break;
                default: return null;
            }
            return PinballInput.GetActionLabel(player, action);
        }

        public (string action, string token)[] ControlRows { get; } =
        {
            ("左のフリッパー", "L"),
            ("右のフリッパー", "R"),
            ("サーブ（長押しで溜めて、離すと発射）", "Serve"),
            ("サーブのルート切り替え（打つ前）", "Route"),
            ("ブロッカーを動かす", "Move"),
            ("キャッチ（押している間）／離して発射", "Catch"),
            ("跳ね返す（当たる直前に押す）", "Bounce"),
        };

        static string Colored(string text, Color c) => "<color=#" + ColorUtility.ToHtmlStringRGB(c) + ">" + text + "</color>";

        public void Dispose()
        {
            if (gm != null) gm.onMatchOver.RemoveListener(OnMatchOver);
            gm = null;
        }
    }
}
