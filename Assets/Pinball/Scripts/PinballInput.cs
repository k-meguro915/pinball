using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Pinball
{
    /// <summary>
    /// フリッパーの位置。画面は横向きで、Top = 画面上側（ワールド -X）、Bottom = 画面下側（ワールド +X）。
    /// </summary>
    public enum FlipperSide { Top = 0, Bottom = 1 }

    /// <summary>
    /// 入力の集約。プレイヤー0 = P1（画面左）、プレイヤー1 = P2 / CPU（画面右）。
    ///
    /// ボタンの割り当てはプロジェクト側の Input Actions で設定する（スクリプトにボタンは書かない）。
    ///   アクションマップ "P1" / "P2"（名前は GameManager で変更可）に、次の名前の Button アクションを作る:
    ///     FlipperTop    … 画面上側のフリッパー
    ///     FlipperBottom … 画面下側のフリッパー
    ///     Plunger       … サーブ（長押しで溜め、離して発射）
    ///     RouteToggle   … サーブのルート切り替え
    ///   さらに Value（Vector2）型のアクションを1つ:
    ///     MoveBlockers  … 自陣のブロッカーの列を動かす（画面の上下左右。スティック・十字キーなど）
    ///   任意の Button アクション（無ければ RouteToggle / Plunger の割り当てをそのまま使う）:
    ///     BlockerBounce … ブロッカーで球を跳ね返す（押した瞬間）
    ///     BlockerCatch  … 押している間ブロッカーに球がくっつき、離すと発射
    ///   アクションマップ "UI" の Submit … 決定（スタート・再戦）
    ///                          Cancel … 決着画面からタイトルへ戻る（試合中は読まない）
    ///                          BackToTitle（任意）… 試合中にタイトルへ戻る
    /// ゲームパッドは接続順に 1台目 = P1、2台目 = P2 に自動で割り当てる（キーボードは両方のマップで使える）。
    ///
    /// Input Actions が見つからないときだけ、従来のキーボード固定操作になる。
    /// CPU担当のプレイヤーは CpuPlayer が押したかどうかで動く。
    /// </summary>
    public static class PinballInput
    {
        public static readonly string[] ActionNames = { "FlipperTop", "FlipperBottom", "Plunger", "RouteToggle" };
        const int ActTop = 0, ActBottom = 1, ActPlunger = 2, ActRoute = 3;

        // ===================== CPU =====================

        static readonly bool[] cpuControlled = new bool[2];
        static readonly bool[,] cpuPress = new bool[2, 2];
        static readonly bool[] cpuPlunge = new bool[2];
        static readonly Vector2[] cpuMove = new Vector2[2];
        static readonly bool[] cpuBounceRequest = new bool[2];
        static readonly bool[] cpuCatch = new bool[2];

        public static bool IsCpu(int player) => cpuControlled[player];

        public static void SetCpuControlled(int player, bool value)
        {
            cpuControlled[player] = value;
            cpuPress[player, 0] = false;
            cpuPress[player, 1] = false;
            cpuPlunge[player] = false;
            cpuMove[player] = Vector2.zero;
            cpuBounceRequest[player] = false;
            cpuCatch[player] = false;
        }

        /// <summary>CPU：跳ね返しを1回要求する（次に読まれたときに消える）</summary>
        public static void RequestCpuBounce(int player) => cpuBounceRequest[player] = true;
        public static void SetCpuCatch(int player, bool held) => cpuCatch[player] = held;

        /// <summary>ブロッカーで跳ね返す（押した瞬間）</summary>
        public static bool BlockerBouncePressed(int player)
        {
            if (player < 0 || player > 1) return false;
            if (cpuControlled[player])
            {
                bool r = cpuBounceRequest[player];
                cpuBounceRequest[player] = false;
                return r;
            }
            bool pressed = BouncePressedRaw(player);
            if (player == 0 && cpuControlled[1]) pressed |= BouncePressedRaw(1);
            return pressed;
        }

        /// <summary>ブロッカーで球をつかむ（押している間）</summary>
        public static bool BlockerCatchHeld(int player)
        {
            if (player < 0 || player > 1) return false;
            if (cpuControlled[player]) return cpuCatch[player];
            bool held = CatchHeldRaw(player);
            if (player == 0 && cpuControlled[1]) held |= CatchHeldRaw(1);
            return held;
        }

        public static void SetCpuMove(int player, Vector2 move)
        {
            cpuMove[player] = Vector2.ClampMagnitude(move, 1f);
        }

        /// <summary>障害物の列の移動入力（画面基準：x = 右、y = 上。長さ 1 以下）</summary>
        public static Vector2 Move(int player)
        {
            if (player < 0 || player > 1) return Vector2.zero;
            if (cpuControlled[player]) return cpuMove[player];

            Vector2 v = ReadMove(player);
            if (player == 0 && cpuControlled[1])
            {
                var v2 = ReadMove(1); // CPU戦では P2 側の操作でも動かせる
                if (v2.sqrMagnitude > v.sqrMagnitude) v = v2;
            }
            // 1 VS 1 の画面分けでは、P2 の画面は 180 度回っている。P2 の画面の上下左右で動くよう逆にする
            if (player == 1 && SplitScreen.VersusActive) v = -v;
            return Vector2.ClampMagnitude(v, 1f);
        }

        public static void SetCpuPress(int player, FlipperSide side, bool pressed)
        {
            cpuPress[player, (int)side] = pressed;
        }

        public static void SetCpuPlunger(int player, bool pressed)
        {
            cpuPlunge[player] = pressed;
        }

        // ===================== ゲームから使う =====================

        public static bool Flipper(int player, FlipperSide side)
        {
            if (player < 0 || player > 1) return false;
            if (cpuControlled[player]) return cpuPress[player, (int)side];
            // 1 VS 1 の画面分けでは、P2 の画面は 180 度回っていて左右が入れ替わる。
            // P2 の画面の左のフリッパー = FlipperTop（LB）になるよう、P2 だけ入れ替える
            if (player == 1 && SplitScreen.VersusActive) side = side == FlipperSide.Top ? FlipperSide.Bottom : FlipperSide.Top;
            return HeldForHuman(player, side == FlipperSide.Top ? ActTop : ActBottom);
        }

        /// <summary>サーブ（長押しで溜め、離して発射）</summary>
        public static bool Plunger(int player)
        {
            if (player < 0 || player > 1) return false;
            if (cpuControlled[player]) return cpuPlunge[player];
            return HeldForHuman(player, ActPlunger);
        }

        /// <summary>サーブのルート切り替え（押した瞬間）。CPU担当は常に false</summary>
        public static bool RouteTogglePressed(int player)
        {
            if (player < 0 || player > 1 || cpuControlled[player]) return false;
            bool pressed = Pressed(player, ActRoute);
            if (player == 0 && cpuControlled[1]) pressed |= Pressed(1, ActRoute);
            return pressed;
        }

        /// <summary>
        /// 画面表示用：そのプレイヤーのアクションに割り当てられたボタン名（例 "LB"、"B"、"W"）。
        /// パッドが割り当てられていればパッドの、無ければキーボードの割り当てを返す。
        /// actionName は FlipperTop / FlipperBottom / Plunger / RouteToggle / MoveBlockers / BlockerBounce / BlockerCatch
        /// </summary>
        public static string GetActionLabel(int player, string actionName)
        {
            if (player < 0 || player > 1) return "";
#if ENABLE_INPUT_SYSTEM
            if (maps[player] != null) return ActionLabelFromMap(player, actionName);
#endif
            return FallbackKeyLabel(player, actionName);
        }

        static string FallbackKeyLabel(int player, string actionName)
        {
            bool p1 = player == 0;
            switch (actionName)
            {
                case "FlipperTop": return p1 ? "W" : "↑";
                case "FlipperBottom": return p1 ? "S" : "↓";
                case "Plunger":
                case "BlockerCatch": return p1 ? "D" : "←";
                case "RouteToggle":
                case "BlockerBounce": return p1 ? "A" : "→";
                case "MoveBlockers": return p1 ? "TFGH" : "IJKL";
                default: return "";
            }
        }

        // CPU戦では P1 は P2 側の操作（矢印キーや2台目のパッド）でも動かせる
        static bool HeldForHuman(int player, int act)
        {
            bool held = Held(player, act);
            if (player == 0 && cpuControlled[1]) held |= Held(1, act);
            return held;
        }

#if ENABLE_INPUT_SYSTEM
        // ===================== Input System（Input Actions） =====================

        static readonly InputActionMap[] maps = new InputActionMap[2];
        static readonly InputAction[,] actions = new InputAction[2, 4];
        static InputAction submitAction, cancelAction, backToTitleAction;
        static readonly InputAction[] moveActions = new InputAction[2];
        static readonly InputAction[] bounceActions = new InputAction[2];
        static readonly InputAction[] catchActions = new InputAction[2];
        static bool moveTypeWarned;
        static bool hooked;

        /// <summary>プロジェクト設定（Project Settings > Input System Package）の Project-wide Actions を取得</summary>
        public static InputActionAsset GetProjectWideActions()
        {
            try
            {
                // InputSystem.actions は Input System 1.8 以降のみ。古い版でもコンパイルできるようリフレクションで読む
                var prop = typeof(InputSystem).GetProperty("actions",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                return prop?.GetValue(null) as InputActionAsset;
            }
            catch
            {
                return null;
            }
        }

        public static void Configure(InputActionAsset asset, string p1Map, string p2Map, string uiMap)
        {
            Shutdown();

            if (asset == null)
            {
                Debug.Log("[Pinball] Input Actions が見つからないため、キーボード固定の操作を使います。");
                return;
            }

            string[] mapNames = { p1Map, p2Map };
            for (int p = 0; p < 2; p++)
            {
                var map = asset.FindActionMap(mapNames[p], false);
                if (map == null)
                {
                    Debug.LogWarning($"[Pinball] アクションマップ \"{mapNames[p]}\" がありません。P{p + 1} はキーボード固定の操作になります。");
                    continue;
                }

                maps[p] = map;
                for (int a = 0; a < ActionNames.Length; a++)
                {
                    actions[p, a] = map.FindAction(ActionNames[a], false);
                    if (actions[p, a] == null)
                        Debug.LogWarning($"[Pinball] \"{mapNames[p]}\" にアクション \"{ActionNames[a]}\" がありません。");
                }
                bounceActions[p] = map.FindAction("BlockerBounce", false);
                catchActions[p] = map.FindAction("BlockerCatch", false);
                if (bounceActions[p] == null || catchActions[p] == null)
                    Debug.Log($"[Pinball] \"{mapNames[p]}\" に BlockerBounce / BlockerCatch が無いので、RouteToggle / Plunger の割り当てを使います。");
                moveActions[p] = map.FindAction("MoveBlockers", false);
                if (moveActions[p] == null)
                    Debug.LogWarning($"[Pinball] \"{mapNames[p]}\" にアクション \"MoveBlockers\"（Value / Vector2）がありません。障害物は動かせません。");
                map.Enable();
            }

            var ui = asset.FindActionMap(uiMap, false);
            if (ui != null)
            {
                submitAction = ui.FindAction("Submit", false);
                cancelAction = ui.FindAction("Cancel", false);
                backToTitleAction = ui.FindAction("BackToTitle", false);
                ui.Enable();
            }

            AssignGamepads();
            InputSystem.onDeviceChange += OnDeviceChange;
            hooked = true;
        }

        public static void Shutdown()
        {
            if (hooked)
            {
                InputSystem.onDeviceChange -= OnDeviceChange;
                hooked = false;
            }
            for (int p = 0; p < 2; p++)
            {
                if (maps[p] != null) maps[p].devices = null; // 割り当てを解除（全デバイスに戻す）
                maps[p] = null;
                for (int a = 0; a < ActionNames.Length; a++) actions[p, a] = null;
                moveActions[p] = null;
                bounceActions[p] = null;
                catchActions[p] = null;
            }
            submitAction = null;
            cancelAction = null;
            backToTitleAction = null;
        }

        static void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            if (device is Gamepad) AssignGamepads();
        }

        /// <summary>パッド1台目 → P1、2台目 → P2。パッド以外（キーボード等）は両方のマップで使える</summary>
        static void AssignGamepads()
        {
            var pads = Gamepad.all;
            for (int p = 0; p < 2; p++)
            {
                if (maps[p] == null) continue;
                var list = new List<InputDevice>();
                foreach (var d in InputSystem.devices)
                    if (!(d is Gamepad)) list.Add(d);
                if (p < pads.Count) list.Add(pads[p]);
                maps[p].devices = list.ToArray();
            }
        }

        /// <summary>このプレイヤーにゲームパッドが割り当てられているか（画面の操作説明用）</summary>
        public static bool HasGamepad(int player)
        {
            return maps[player] != null && player < Gamepad.all.Count;
        }

        public static bool UsesActions(int player) => maps[player] != null;

        static string ActionLabelFromMap(int player, string actionName)
        {
            var action = maps[player].FindAction(actionName, false);
            if (action == null && actionName == "BlockerBounce") action = actions[player, ActRoute];
            if (action == null && actionName == "BlockerCatch") action = actions[player, ActPlunger];
            if (action == null) return "";

            bool wantPad = HasGamepad(player);
            string any = null;
            var bindings = action.bindings;
            for (int i = 0; i < bindings.Count; i++)
            {
                var b = bindings[i];
                if (b.isPartOfComposite) continue;

                // 合成（2D Vector など）は最初の部品の機器で判定
                string path = b.isComposite && i + 1 < bindings.Count ? bindings[i + 1].effectivePath : b.effectivePath;
                string layout = InputControlPath.TryGetDeviceLayout(path);
                bool isPad = IsLayout(layout, "Gamepad");
                bool isKey = IsLayout(layout, "Keyboard");

                string s;
                try { s = action.GetBindingDisplayString(i); }
                catch { continue; }
                if (string.IsNullOrEmpty(s)) continue;

                if (any == null) any = s;
                if ((wantPad && isPad) || (!wantPad && isKey)) return s;
            }
            return any ?? "";
        }

        static bool IsLayout(string layout, string baseLayout)
        {
            if (string.IsNullOrEmpty(layout) || layout == "*") return false;
            try { return InputSystem.IsFirstLayoutBasedOnSecond(layout, baseLayout); }
            catch { return false; }
        }

        static bool BouncePressedRaw(int player)
        {
            if (maps[player] != null && bounceActions[player] != null) return bounceActions[player].WasPressedThisFrame();
            return Pressed(player, ActRoute);
        }

        static bool CatchHeldRaw(int player)
        {
            if (maps[player] != null && catchActions[player] != null) return catchActions[player].IsPressed();
            return Held(player, ActPlunger);
        }

        static Vector2 ReadMove(int player)
        {
            if (maps[player] != null)
            {
                var a = moveActions[player];
                if (a == null) return Vector2.zero;
                try
                {
                    return a.ReadValue<Vector2>();
                }
                catch (System.InvalidOperationException)
                {
                    if (!moveTypeWarned)
                    {
                        Debug.LogWarning("[Pinball] MoveBlockers は Action Type = Value、Control Type = Vector2 にしてください。");
                        moveTypeWarned = true;
                    }
                    return Vector2.zero;
                }
            }

            var kb = Keyboard.current;
            if (kb == null) return Vector2.zero;
            // キーボード固定（Input Actions が無いときの予備）：P1 = T/F/G/H、P2 = I/J/K/L
            if (player == 0)
                return new Vector2((kb.hKey.isPressed ? 1 : 0) - (kb.fKey.isPressed ? 1 : 0),
                                   (kb.tKey.isPressed ? 1 : 0) - (kb.gKey.isPressed ? 1 : 0));
            return new Vector2((kb.lKey.isPressed ? 1 : 0) - (kb.jKey.isPressed ? 1 : 0),
                               (kb.iKey.isPressed ? 1 : 0) - (kb.kKey.isPressed ? 1 : 0));
        }

        static bool Held(int player, int act)
        {
            if (maps[player] != null)
            {
                var a = actions[player, act];
                return a != null && a.IsPressed();
            }
            return KeyHeld(player, act);
        }

        static bool Pressed(int player, int act)
        {
            if (maps[player] != null)
            {
                var a = actions[player, act];
                return a != null && a.WasPressedThisFrame();
            }
            return KeyDown(player, act);
        }

        public static bool ConfirmPressed =>
            Down(Key.Enter) || Down(Key.NumpadEnter) || Down(Key.Space) ||
            (submitAction != null && submitAction.WasPressedThisFrame());

        /// <summary>決着画面からタイトルへ（Esc / UI の Cancel）</summary>
        public static bool BackPressed =>
            Down(Key.Escape) || (cancelAction != null && cancelAction.WasPressedThisFrame());

        /// <summary>
        /// 試合中にタイトルへ（Esc / UI の BackToTitle）。
        /// UI の Cancel はパッドの右ボタン等を含むことが多く、プレイ用のボタンと重なるので試合中は読まない。
        /// </summary>
        public static bool QuitMatchPressed =>
            Down(Key.Escape) || (backToTitleAction != null && backToTitleAction.WasPressedThisFrame());

        // ---- キーボード固定（Input Actions が無いときの予備） ----
        static bool KeyHeld(int player, int act)
        {
            var kb = Keyboard.current;
            if (kb == null) return false;
            if (player == 0)
            {
                switch (act)
                {
                    case ActTop: return kb.wKey.isPressed;
                    case ActBottom: return kb.sKey.isPressed;
                    case ActPlunger: return kb.dKey.isPressed || kb.spaceKey.isPressed;
                    default: return kb.aKey.isPressed;
                }
            }
            switch (act)
            {
                case ActTop: return kb.upArrowKey.isPressed;
                case ActBottom: return kb.downArrowKey.isPressed;
                case ActPlunger: return kb.leftArrowKey.isPressed;
                default: return kb.rightArrowKey.isPressed;
            }
        }

        static bool KeyDown(int player, int act)
        {
            var kb = Keyboard.current;
            if (kb == null) return false;
            if (act != ActRoute) return false; // 押した瞬間が必要なのはルート切り替えだけ
            return player == 0 ? kb.aKey.wasPressedThisFrame : kb.rightArrowKey.wasPressedThisFrame;
        }

        static bool Down(Key k)
        {
            var kb = Keyboard.current;
            return kb != null && kb[k].wasPressedThisFrame;
        }
#else
        // ===================== 旧 Input Manager（キーボード固定） =====================

        public static bool HasGamepad(int player) => false;
        public static bool UsesActions(int player) => false;

        static bool BouncePressedRaw(int player) => Pressed(player, ActRoute);
        static bool CatchHeldRaw(int player) => Held(player, ActPlunger);

        static Vector2 ReadMove(int player)
        {
            if (player == 0)
                return new Vector2((Input.GetKey(KeyCode.H) ? 1 : 0) - (Input.GetKey(KeyCode.F) ? 1 : 0),
                                   (Input.GetKey(KeyCode.T) ? 1 : 0) - (Input.GetKey(KeyCode.G) ? 1 : 0));
            return new Vector2((Input.GetKey(KeyCode.L) ? 1 : 0) - (Input.GetKey(KeyCode.J) ? 1 : 0),
                               (Input.GetKey(KeyCode.I) ? 1 : 0) - (Input.GetKey(KeyCode.K) ? 1 : 0));
        }

        static bool Held(int player, int act)
        {
            if (player == 0)
            {
                switch (act)
                {
                    case ActTop: return Input.GetKey(KeyCode.W);
                    case ActBottom: return Input.GetKey(KeyCode.S);
                    case ActPlunger: return Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.Space);
                    default: return Input.GetKey(KeyCode.A);
                }
            }
            switch (act)
            {
                case ActTop: return Input.GetKey(KeyCode.UpArrow);
                case ActBottom: return Input.GetKey(KeyCode.DownArrow);
                case ActPlunger: return Input.GetKey(KeyCode.LeftArrow);
                default: return Input.GetKey(KeyCode.RightArrow);
            }
        }

        static bool Pressed(int player, int act)
        {
            if (act != ActRoute) return false;
            return Input.GetKeyDown(player == 0 ? KeyCode.A : KeyCode.RightArrow);
        }

        public static bool ConfirmPressed =>
            Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space);
        public static bool BackPressed => Input.GetKeyDown(KeyCode.Escape);
        public static bool QuitMatchPressed => Input.GetKeyDown(KeyCode.Escape);
#endif
    }
}
