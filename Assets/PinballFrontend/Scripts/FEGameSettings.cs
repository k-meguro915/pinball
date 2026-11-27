using System;

namespace PinballFrontend
{
    /// <summary>1 VS 1 で画面を左右に分けたときの映し方（本編の Pinball.SplitScreen.ViewStyle と同じ並び）</summary>
    public enum FESplitView
    {
        /// <summary>台全体を映す</summary>
        WholeBoard,
        /// <summary>自陣側だけを大きく映す</summary>
        OwnHalf,
    }

    /// <summary>
    /// タイトルの「せってい」で決める、ゲーム全体の設定（試合ごとに選ぶ FEMatchSettings とは別）。
    /// PlayerPrefs に残す。本編へ入る前に Apply() で本編に入れる（FESession）。
    /// </summary>
    [Serializable]
    public class FEGameSettings
    {
        public FESplitView splitView = FESplitView.WholeBoard;

        const string PrefsKey = "PinballFrontend.GameSettings";

        static FEGameSettings current;

        /// <summary>いまの設定（はじめて使うときに PlayerPrefs から読む）</summary>
        public static FEGameSettings Current => current ??= Load();

        static FEGameSettings Load()
        {
            var s = new FEGameSettings();
            string json = UnityEngine.PlayerPrefs.GetString(PrefsKey, "");
            if (!string.IsNullOrEmpty(json))
            {
                try { UnityEngine.JsonUtility.FromJsonOverwrite(json, s); }
                catch (Exception e) { UnityEngine.Debug.LogWarning("[PinballFrontend] せっていを読めませんでした：" + e.Message); }
            }
            if (!Enum.IsDefined(typeof(FESplitView), s.splitView)) s.splitView = FESplitView.WholeBoard;
            return s;
        }

        public void Save()
        {
            UnityEngine.PlayerPrefs.SetString(PrefsKey, UnityEngine.JsonUtility.ToJson(this));
            UnityEngine.PlayerPrefs.Save();
        }

        /// <summary>本編に入れる</summary>
        public void Apply()
        {
            Pinball.SplitScreen.View = splitView == FESplitView.OwnHalf
                ? Pinball.SplitScreen.ViewStyle.OwnHalf
                : Pinball.SplitScreen.ViewStyle.WholeBoard;
        }

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatic() => current = null; // ドメインの再読み込みを切っていても読み直す
    }
}
