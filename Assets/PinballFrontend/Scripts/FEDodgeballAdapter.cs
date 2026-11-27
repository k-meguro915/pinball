using PinballDodgeball;

namespace PinballFrontend
{
    /// <summary>
    /// ドッジボールルール（DBGameManager）とのつなぎ役。本編は直さずに使える。
    /// ・DBGameManager は StartMatch() のときに各項目を読み、Start() で skipTitle を見るので、
    ///   読み込んだ直後に値と skipTitle = true を入れれば、その設定ですぐ試合が始まる
    /// ・決着の後に本編のタイトルへ戻ったら（GoTitle）、FE に戻る
    /// ・本編には試合中にタイトルへ戻る操作が無いので、試合中の Esc／パッドの Select で FE に戻す
    /// </summary>
    public sealed class FEDodgeballAdapter : IFEMatchAdapter
    {
        DBGameManager gm;
        bool sawMatch;

        public bool Apply(FEMatchSettings s)
        {
            gm = DBGameManager.Instance; // Awake で入るので、読み込んだ直後なら取れる
            if (gm == null) return false;

            gm.mode = s.IsVsCpu ? DBGameManager.Mode.VsCpu : DBGameManager.Mode.Versus;
            gm.cpuLevel = ToCpuLevel(s.cpuLevel);
            gm.outfieldControl = s.outfieldControl == FEControl.Auto
                ? DBGameManager.OutfieldControl.Auto : DBGameManager.OutfieldControl.Buttons;
            gm.skipTitle = true;                 // 本編のタイトルを出さずに始める
            return true;
        }

        public bool Tick()
        {
            if (gm == null) return true; // 管理役が消えた → FE に戻す

            var state = gm.CurrentState;
            if (state != DBGameManager.State.Title) sawMatch = true;

            // 決着の後に本編のタイトルへ戻った（DBHud の Esc／Select）
            if (sawMatch && state == DBGameManager.State.Title) return true;

            // 試合中の Esc／Select（本編はこの間の cancel を何にも使っていない）
            if (gm.IsPlaying && DBInput.ReadUI().cancel) return true;

            return false;
        }

        public void Dispose()
        {
            gm = null;
        }

        static DBGameManager.CpuLevel ToCpuLevel(FECpuLevel level)
        {
            switch (level)
            {
                case FECpuLevel.Easy: return DBGameManager.CpuLevel.Easy;
                case FECpuLevel.Hard: return DBGameManager.CpuLevel.Hard;
                default: return DBGameManager.CpuLevel.Normal;
            }
        }
    }
}
