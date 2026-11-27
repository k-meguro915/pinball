using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace PinballFrontend
{
    /// <summary>
    /// あそびかたのときに、本編シーンに実行時に FESession が足す部品（シーンには置かない）。
    /// ・カメラを左（gameWidth の割合）に寄せ、右に説明の画面（FETutorialPanel）を出す
    /// ・ルールごとの準備と毎フレームの処理は IFETutorialRule に任せる
    /// ・Esc／パッドの Select：やめる（モード選択に戻る）
    /// ・Enter／パッドの Start：この設定で、ふつうの試合をはじめる
    /// </summary>
    public class FETutorialBridge : MonoBehaviour
    {
        IFEMatchAdapter adapter;
        IFETutorialRule rule;
        FEMatchSettings settings;
        string matchScenePath;
        float gameWidth = 0.5f;
        FETutorialPanel panel;
        bool leaving;

        public void Init(IFEMatchAdapter matchAdapter, IFETutorialRule tutorialRule, FEMatchSettings matchSettings,
                         string scenePath, float width, FETutorialContent content)
        {
            adapter = matchAdapter;
            rule = tutorialRule;
            settings = matchSettings;
            matchScenePath = scenePath;
            gameWidth = width;

            // 1 VS 1 でも画面を左右に分けない（台は左に寄せて 1 画面で映す。本編の Pinball.SplitScreen）
            Pinball.SplitScreen.Allowed = false;

            if (!rule.Setup(settings))
                Debug.LogError("[PinballFrontend] あそびかたの準備ができませんでした（本編の管理役が見つかりません）");

            ApplyViewport();
            panel = gameObject.AddComponent<FETutorialPanel>();
            panel.Init(settings, gameWidth, content, rule);
        }

        void Update()
        {
            if (leaving || adapter == null) return;
            rule.Tick();
            if (panel != null) panel.SetStatus(rule.StatusText());

            if (QuitPressed() || adapter.Tick())
            {
                leaving = true;
                FESession.ReturnToMenu();
                return;
            }
            if (StartMatchPressed())
            {
                leaving = true;
                FESession.StartMatch(settings, matchScenePath, FESession.ReturnScenePath);
            }
        }

        void LateUpdate() => ApplyViewport();

        /// <summary>メインカメラを左に寄せる（本編のカメラは LateUpdate で、映す枠の縦横比に合わせて台全体を映す）</summary>
        void ApplyViewport()
        {
            var cam = Camera.main;
            if (cam == null) return;
            var r = new Rect(0f, 0f, gameWidth, 1f);
            if (cam.rect != r) cam.rect = r;
        }

        static bool QuitPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) return true;
            foreach (var g in Gamepad.all) if (g.selectButton.wasPressedThisFrame) return true;
            return false;
#else
            return Input.GetKeyDown(KeyCode.Escape);
#endif
        }

        static bool StartMatchPressed()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)) return true;
            foreach (var g in Gamepad.all) if (g.startButton.wasPressedThisFrame) return true;
            return false;
#else
            return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
#endif
        }

        void OnDestroy()
        {
            Pinball.SplitScreen.Allowed = true;
            rule?.Dispose();
            adapter?.Dispose();
            rule = null;
            adapter = null;
        }
    }
}
