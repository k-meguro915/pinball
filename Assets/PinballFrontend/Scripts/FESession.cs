using UnityEngine;
using UnityEngine.SceneManagement;

namespace PinballFrontend
{
    /// <summary>
    /// FE から本編シーンへ設定を運ぶ。
    /// StartMatch() で本編シーンを読み、読み込んだ直後（sceneLoaded。本編の Awake の後、Start の前）に
    /// ルールに合った Adapter で設定を入れ、そのシーンに FEMatchBridge を足す。
    /// FE から始めたときだけ働くので、本編シーンを Editor で直接再生したときは何もしない。
    /// StartTutorial() は同じしくみで本編シーンを読み、FEMatchBridge の代わりに FETutorialBridge を足す（あそびかた）。
    /// </summary>
    public static class FESession
    {
        static FEMatchSettings pending;
        static string pendingScenePath;
        static bool pendingTutorial;
        static float pendingGameWidth = 0.5f;
        static FETutorialContent pendingContent;

        /// <summary>いま遊んでいる試合の設定（FE から入っていないときは null）</summary>
        public static FEMatchSettings Current { get; private set; }

        /// <summary>本編から戻るときに読むシーン</summary>
        public static string ReturnScenePath { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Init()
        {
            // Enter Play Mode Options でドメインの再読み込みを切っていても、前回の値が残らないようにする
            pending = null;
            pendingScenePath = null;
            pendingTutorial = false;
            Current = null;
            ReturnScenePath = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        /// <summary>設定を持って本編シーンへ。returnScenePath は「タイトルへ」で戻るシーン</summary>
        public static bool StartMatch(FEMatchSettings settings, string matchScenePath, string returnScenePath)
            => Load(settings, matchScenePath, returnScenePath, false, 0.5f);

        /// <summary>
        /// あそびかた：設定を持って本編シーンへ。左（gameWidth の割合）で試し打ち、右に説明。
        /// returnScenePath は「やめる」で戻るシーン
        /// </summary>
        public static bool StartTutorial(FEMatchSettings settings, string matchScenePath, string returnScenePath, float gameWidth,
                                         FETutorialContent content)
        {
            pendingContent = content;
            return Load(settings, matchScenePath, returnScenePath, true, gameWidth);
        }

        static bool Load(FEMatchSettings settings, string matchScenePath, string returnScenePath, bool tutorial, float gameWidth)
        {
            if (settings == null) { Debug.LogError("[PinballFrontend] 設定が空です"); return false; }
            if (!IsInBuild(matchScenePath) || !IsInBuild(returnScenePath)) return false;

            pending = settings.Clone();
            pendingScenePath = matchScenePath;
            pendingTutorial = tutorial;
            pendingGameWidth = Mathf.Clamp(gameWidth, 0.2f, 0.9f);
            ReturnScenePath = returnScenePath;
            Time.timeScale = 1f;
            Debug.Log($"[PinballFrontend] {(tutorial ? "あそびかたへ" : "試合へ")}：{pending}");
            SceneManager.LoadScene(matchScenePath);
            return true;
        }

        /// <summary>本編から FE のシーンへ戻る</summary>
        public static void ReturnToMenu()
        {
            Current = null;
            Time.timeScale = 1f; // 本編が止めたまま抜けても、FE が止まらないように
            if (string.IsNullOrEmpty(ReturnScenePath))
            {
                Debug.LogWarning("[PinballFrontend] 戻り先のシーンが決まっていません");
                return;
            }
            Debug.Log($"[PinballFrontend] FE に戻る：{ReturnScenePath}");
            SceneManager.LoadScene(ReturnScenePath);
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (pending == null || scene.path != pendingScenePath) return;
            var settings = pending;
            bool tutorial = pendingTutorial;
            pending = null;
            pendingScenePath = null;
            pendingTutorial = false;

            IFEMatchAdapter adapter = settings.rule == FERule.Versus
                ? new FEVersusAdapter()
                : (IFEMatchAdapter)new FEDodgeballAdapter();
            if (!adapter.Apply(settings))
            {
                Debug.LogError($"[PinballFrontend] {scene.name} に設定を入れられませんでした（本編の管理役が見つかりません）。本編シーンの値のまま動きます");
                adapter.Dispose();
                return;
            }

            if (tutorial)
            {
                IFETutorialRule rule = settings.rule == FERule.Versus
                    ? new FEVersusTutorial()
                    : (IFETutorialRule)new FEDodgeballTutorial();
                var tgo = new GameObject("FE Tutorial Bridge");
                SceneManager.MoveGameObjectToScene(tgo, scene);
                tgo.AddComponent<FETutorialBridge>().Init(adapter, rule, settings, scene.path, pendingGameWidth, pendingContent);
                pendingContent = null;
            }
            else
            {
                var go = new GameObject("FE Match Bridge");
                SceneManager.MoveGameObjectToScene(go, scene);
                go.AddComponent<FEMatchBridge>().Init(adapter);
            }
            Current = settings;
        }

        /// <summary>FE のシーン同士の移動（タイトル ⇔ モード選択）。Build Settings に無ければ読まずに false</summary>
        public static bool LoadScene(string scenePath)
        {
            if (!IsInBuild(scenePath)) return false;
            Time.timeScale = 1f;
            SceneManager.LoadScene(scenePath);
            return true;
        }

        public static bool IsInBuild(string scenePath)
        {
            if (!string.IsNullOrEmpty(scenePath) && SceneUtility.GetBuildIndexByScenePath(scenePath) >= 0) return true;
            Debug.LogError($"[PinballFrontend] シーン「{scenePath}」が Build Settings に入っていません。" +
                           "Tools > Pinball Frontend > Apply Build Settings を実行してください");
            return false;
        }
    }
}
