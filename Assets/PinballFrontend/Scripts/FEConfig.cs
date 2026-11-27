using UnityEngine;

namespace PinballFrontend
{
    /// <summary>
    /// FE の設定（Data/FEConfig.asset）。シーンの場所とゲームの表示名を 1 か所にまとめる。
    /// シーンは番号ではなくパスで読むので、Build Settings の並びが変わっても困らない。
    /// Build Settings の並びは FEBuildMenu がこの順番で作る：タイトル → モード選択 → 対戦 → ドッジボール。
    /// </summary>
    [CreateAssetMenu(fileName = "FEConfig", menuName = "Pinball Frontend/FE Config")]
    public class FEConfig : ScriptableObject
    {
        [Header("表示")]
        [Tooltip("タイトル画面に出すゲームの名前（ビルドしたアプリの名前は Player Settings の Product Name）")]
        public string gameTitle = "THE PINBALL";

        [Header("アプリ（ビルドのときに Player Settings に入れる）")]
        [Tooltip("Product Name：ウィンドウのタイトル、.exe のファイル名。変えると PlayerPrefs（前回の設定）の保存場所も変わる")]
        public string productName = "Pinball_Versus";
        [Tooltip("Company Name：設定やログの置き場所の名前。変えると PlayerPrefs の保存場所も変わる")]
        public string companyName = "ICE";

        [Header("FE のシーン")]
        public string titleScenePath = "Assets/PinballFrontend/Scenes/FE_Title.unity";
        public string modeSelectScenePath = "Assets/PinballFrontend/Scenes/FE_ModeSelect.unity";

        [Header("本編のシーン")]
        public string versusScenePath = "Assets/Scenes/Pinball_VersusMode.unity";
        public string dodgeballScenePath = "Assets/Scenes/PinBall_DodgeMode.unity";

        [Header("あそびかた（チュートリアル）")]
        [Tooltip("左の試し打ちの台が占める横幅の割合（残りが右の説明）")]
        [Range(0.3f, 0.8f)] public float tutorialGameWidth = 0.5f;
        [Tooltip("右の説明の中身（Tools > Pinball Frontend > Create Tutorial Content で作る）")]
        public FETutorialContent versusTutorial;
        public FETutorialContent dodgeballTutorial;

        public FETutorialContent TutorialContent(FERule rule) => rule == FERule.Versus ? versusTutorial : dodgeballTutorial;

        public string MatchScenePath(FERule rule) => rule == FERule.Versus ? versusScenePath : dodgeballScenePath;
    }
}
