using System;

namespace PinballFrontend
{
    /// <summary>
    /// モード選択で選んだ内容。本編の型は参照しない（本編に入れるのは各 Adapter の仕事）。
    /// どちらのルールでも同じ入れ物を使い、そのルールで使わない項目は無視される。
    /// </summary>
    [Serializable]
    public class FEMatchSettings
    {
        /// <summary>対戦ルールの先取点で選べる値。本編の GameManager.TargetOptions と同じにしておく</summary>
        public static readonly int[] TargetScoreOptions = { 3, 5, 10 };

        public FERule rule = FERule.Versus;
        public FEPlayMode playMode = FEPlayMode.VsCpu;
        public FECpuLevel cpuLevel = FECpuLevel.Normal;

        // ---- 対戦ルール ----
        public int targetScore = 5;

        // ---- ドッジボール ----
        public FEControl outfieldControl = FEControl.Auto;

        public bool IsVsCpu => playMode == FEPlayMode.VsCpu;

        public FEMatchSettings Clone() => (FEMatchSettings)MemberwiseClone();

        // ---- 前回の設定（PlayerPrefs。Windows ではレジストリに入る） ----
        const string PrefsKey = "PinballFrontend.LastSettings";

        /// <summary>前回選んだ設定。無い・読めないときは初期値</summary>
        public static FEMatchSettings LoadLast()
        {
            var s = new FEMatchSettings();
            string json = UnityEngine.PlayerPrefs.GetString(PrefsKey, "");
            if (string.IsNullOrEmpty(json)) return s;
            try { UnityEngine.JsonUtility.FromJsonOverwrite(json, s); }
            catch (Exception e) { UnityEngine.Debug.LogWarning("[PinballFrontend] 前回の設定を読めませんでした：" + e.Message); }
            s.Validate();
            return s;
        }

        public void SaveAsLast()
        {
            UnityEngine.PlayerPrefs.SetString(PrefsKey, UnityEngine.JsonUtility.ToJson(this));
            UnityEngine.PlayerPrefs.Save();
        }

        /// <summary>範囲の外の値（古い保存データなど）を直す</summary>
        public void Validate()
        {
            if (!Enum.IsDefined(typeof(FERule), rule)) rule = FERule.Versus;
            if (!Enum.IsDefined(typeof(FEPlayMode), playMode)) playMode = FEPlayMode.VsCpu;
            if (!Enum.IsDefined(typeof(FECpuLevel), cpuLevel)) cpuLevel = FECpuLevel.Normal;
            if (!Enum.IsDefined(typeof(FEControl), outfieldControl)) outfieldControl = FEControl.Auto;
            if (Array.IndexOf(TargetScoreOptions, targetScore) < 0) targetScore = 5;
        }

        public override string ToString()
        {
            string common = $"{rule} / {playMode}" + (IsVsCpu ? $" / CPU {cpuLevel}" : "");
            return rule == FERule.Versus
                ? $"{common} / first to {targetScore}"
                : $"{common} / outfield {outfieldControl}";
        }
    }
}
