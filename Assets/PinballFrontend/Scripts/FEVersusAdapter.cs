using Pinball;

namespace PinballFrontend
{
    /// <summary>
    /// 対戦ルール（GameManager）とのつなぎ役。
    /// ・GameManager は Start() → ToTitle() → onTitle の順に呼ぶ。1 回目の onTitle で StartMatch() を呼ぶので、
    ///   本編のタイトル画面は描かれない
    /// ・2 回目からの onTitle（試合中の Esc、決着の後の Esc）で FE に戻る。パッドの Select でも戻る（ドッジボールとそろえる）
    /// ・設定（モード／先取点／CPU の強さ）は GameManager.ApplyRules で入れる（2026-11-24 に本編に足した）
    /// </summary>
    public sealed class FEVersusAdapter : IFEMatchAdapter
    {
        GameManager gm;
        int titleCount;
        bool returnRequested;

        public bool Apply(FEMatchSettings s)
        {
            gm = GameManager.Instance; // Awake で入るので、読み込んだ直後なら取れる
            if (gm == null) return false;

            ApplyRules(s);
            gm.onTitle.AddListener(OnTitle); // Start() より前に登録できる
            return true;
        }

        void ApplyRules(FEMatchSettings s)
        {
            gm.ApplyRules(ToMatchMode(s.playMode), s.targetScore, ToCpuLevel(s.cpuLevel));
        }

        void OnTitle()
        {
            titleCount++;
            if (titleCount == 1) gm.StartMatch(); // 起動直後のタイトル → すぐ試合へ
            else returnRequested = true;          // 試合の後のタイトル → FE へ（シーンの切り替えは Tick で）
        }

        public bool Tick()
        {
            if (gm == null) return true; // 管理役が消えた → FE に戻す
            if (returnRequested) return true;

            // 本編は試合中にタイトルへ戻るのが Esc だけなので、ドッジボールとそろえてパッドの Select でも戻す
            return gm.State != MatchState.Title && SelectPressed();
        }

        static bool SelectPressed()
        {
#if ENABLE_INPUT_SYSTEM
            foreach (var g in UnityEngine.InputSystem.Gamepad.all)
                if (g.selectButton.wasPressedThisFrame) return true;
#endif
            return false;
        }

        public void Dispose()
        {
            if (gm != null) gm.onTitle.RemoveListener(OnTitle);
            gm = null;
        }

        internal static MatchMode ToMatchMode(FEPlayMode mode) =>
            mode == FEPlayMode.VsCpu ? MatchMode.VsCpu : MatchMode.Versus;

        internal static CpuPlayer.Level ToCpuLevel(FECpuLevel level)
        {
            switch (level)
            {
                case FECpuLevel.Easy: return CpuPlayer.Level.Easy;
                case FECpuLevel.Hard: return CpuPlayer.Level.Hard;
                default: return CpuPlayer.Level.Normal;
            }
        }
    }
}
