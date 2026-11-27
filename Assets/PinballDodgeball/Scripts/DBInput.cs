using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using System.Collections.Generic;
using UnityEngine.InputSystem;
#endif

namespace PinballDodgeball
{
    /// <summary>
    /// 1 プレイヤーぶんの入力。b = 発射・キャッチ・投げる・撃つ、y = サーブのルート切り替え。
    /// 押した瞬間・離した瞬間（bDown / bUp / yDown）は GameManager がフレームごとに計算する。
    /// move は画面の向き（上 = +Z、右 = +X）。
    /// </summary>
    public struct DBInputState
    {
        public bool b, y;
        public Vector2 move;
        public bool bDown, bUp, yDown;
    }

    public struct DBUIInput
    {
        public bool up, down, left, right, submit, cancel;
    }

    /// <summary>
    /// 試合中の入力。対戦ルール（Pinball）と同じ操作にそろえてある（2026-11-24）。
    /// Project-wide Actions（Assets/InputSystem_Actions.inputactions）の "P1" / "P2" マップを、対戦ルールと同じアクション名で読む。
    ///   b     ← BlockerCatch、無ければ Plunger（RB）
    ///   y     ← BlockerBounce、無ければ RouteToggle（LB）
    ///   move  ← MoveBlockers（左スティック）
    /// パッドは接続順に 1 台目 → P1、2 台目 → P2（対戦ルールの PinballInput と同じ）。キーボードは両方のマップで使える。
    /// 割り当てを変えるときは、アセットを直せば両方のルールに効く。
    /// Input Actions が見つからないときだけ、対戦ルールの予備と同じキーボード固定の操作になる。
    /// </summary>
    public static class DBInput
    {
        public static DBInputState ReadHeld(int player)
        {
            var s = new DBInputState();
#if ENABLE_INPUT_SYSTEM
            if (Configure() && player >= 0 && player < 2 && maps[player] != null)
            {
                s.b = IsPressed(catchOrPlunger[player]);
                s.y = IsPressed(bounceOrRoute[player]);
                s.move = ReadVector(move[player]);
            }
            else
            {
                ReadFallbackKeys(player, ref s);
            }
#else
            ReadFallbackKeys(player, ref s);
#endif
            s.move = Vector2.ClampMagnitude(s.move, 1f);
            return s;
        }

        static int uiFrame = -1;
        static DBUIInput uiCache;
#if ENABLE_INPUT_SYSTEM
        static Vector2 prevNav;
#endif

        /// <summary>タイトル・決着画面の操作（同じフレームなら何度呼んでも同じ結果）</summary>
        public static DBUIInput ReadUI()
        {
            if (uiFrame == Time.frameCount) return uiCache;
            uiFrame = Time.frameCount;
            var u = new DBUIInput();
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null)
            {
                u.submit |= kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame;
                u.cancel |= kb.escapeKey.wasPressedThisFrame;
                u.up |= kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame;
                u.down |= kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame;
                u.left |= kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame;
                u.right |= kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame;
            }
            Vector2 nav = Vector2.zero;
            foreach (var g in Gamepad.all)
            {
                u.submit |= g.buttonSouth.wasPressedThisFrame || g.startButton.wasPressedThisFrame;
                u.cancel |= g.selectButton.wasPressedThisFrame;
                nav += g.leftStick.ReadValue() + g.dpad.ReadValue();
            }
            u.up |= nav.y > 0.5f && prevNav.y <= 0.5f;
            u.down |= nav.y < -0.5f && prevNav.y >= -0.5f;
            u.right |= nav.x > 0.5f && prevNav.x <= 0.5f;
            u.left |= nav.x < -0.5f && prevNav.x >= -0.5f;
            prevNav = nav;
#else
            u.submit = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space);
            u.cancel = Input.GetKeyDown(KeyCode.Escape);
            u.up = Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W);
            u.down = Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S);
            u.left = Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A);
            u.right = Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D);
#endif
            uiCache = u;
            return u;
        }

        /// <summary>このプレイヤーにパッドがつながっているか（表示のボタン名の切り替え用）</summary>
        public static bool HasPad(int player)
        {
#if ENABLE_INPUT_SYSTEM
            return player >= 0 && player < Gamepad.all.Count;
#else
            return false;
#endif
        }

        /// <summary>
        /// 画面表示用のボタン名。id は "B"（発射・キャッチ・投げる）/ "Y"（ルート切り替え）/ "Move"。
        /// パッドがあればパッドの、無ければキーボードの割り当てを、Input Actions から読む
        /// </summary>
        public static string Label(int player, string id)
        {
#if ENABLE_INPUT_SYSTEM
            if (Configure() && player >= 0 && player < 2 && maps[player] != null)
            {
                InputAction a = null;
                switch (id)
                {
                    case "B": a = catchOrPlunger[player]; break;
                    case "Y": a = bounceOrRoute[player]; break;
                    case "Move": a = move[player]; break;
                }
                string s = BindingLabel(a, HasPad(player));
                if (!string.IsNullOrEmpty(s)) return s;
            }
#endif
            return FallbackLabel(player, id);
        }

#if ENABLE_INPUT_SYSTEM
        // ===================== Input Actions =====================

        static readonly InputActionMap[] maps = new InputActionMap[2];
        static readonly InputAction[] catchOrPlunger = new InputAction[2];
        static readonly InputAction[] bounceOrRoute = new InputAction[2];
        static readonly InputAction[] move = new InputAction[2];
        static bool configured, hooked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            if (hooked) InputSystem.onDeviceChange -= OnDeviceChange;
            hooked = false;
            configured = false;
            for (int p = 0; p < 2; p++)
            {
                maps[p] = null;
                catchOrPlunger[p] = bounceOrRoute[p] = move[p] = null;
            }
        }

        /// <summary>初めて呼ばれたときにマップを探す。使えるマップが 1 つでもあれば true</summary>
        static bool Configure()
        {
            if (!configured)
            {
                configured = true;
                var asset = InputSystem.actions;
                string[] names = { "P1", "P2" };
                for (int p = 0; p < 2; p++)
                {
                    var map = asset != null ? asset.FindActionMap(names[p], false) : null;
                    if (map == null) continue;
                    maps[p] = map;
                    catchOrPlunger[p] = map.FindAction("BlockerCatch", false) ?? map.FindAction("Plunger", false);
                    bounceOrRoute[p] = map.FindAction("BlockerBounce", false) ?? map.FindAction("RouteToggle", false);
                    move[p] = map.FindAction("MoveBlockers", false);
                }
                if (maps[0] == null && maps[1] == null)
                    Debug.Log("[PinballDodgeball] Input Actions（P1 / P2）が見つからないため、キーボード固定の操作を使います。");
                else if (!hooked)
                {
                    InputSystem.onDeviceChange += OnDeviceChange;
                    hooked = true;
                }
            }
            if (maps[0] == null && maps[1] == null) return false;

            // 対戦ルールの PinballInput は、シーンを抜けるときにパッドの割り当てを外す（全デバイスに戻す）。
            // 同じマップを使うので、外れていたら割り当て直し、止まっていたら動かす
            for (int p = 0; p < 2; p++)
            {
                var map = maps[p];
                if (map == null) continue;
                if (!map.devices.HasValue) AssignGamepads();
                if (!map.enabled) map.Enable();
            }
            return true;
        }

        static void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            if (device is Gamepad) AssignGamepads();
        }

        /// <summary>パッド 1 台目 → P1、2 台目 → P2。パッド以外（キーボードなど）は両方のマップで使える</summary>
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

        static bool IsPressed(InputAction a) => a != null && a.IsPressed();

        static Vector2 ReadVector(InputAction a)
        {
            if (a == null) return Vector2.zero;
            try { return a.ReadValue<Vector2>(); }
            catch (System.InvalidOperationException) { return Vector2.zero; }
        }

        static string BindingLabel(InputAction a, bool wantPad)
        {
            if (a == null) return "";
            string any = null;
            var bindings = a.bindings;
            for (int i = 0; i < bindings.Count; i++)
            {
                var bd = bindings[i];
                if (bd.isPartOfComposite) continue;
                string path = bd.isComposite && i + 1 < bindings.Count ? bindings[i + 1].effectivePath : bd.effectivePath;
                string layout = InputControlPath.TryGetDeviceLayout(path);
                bool isPad = IsLayout(layout, "Gamepad");
                bool isKey = IsLayout(layout, "Keyboard");

                string s;
                try { s = a.GetBindingDisplayString(i); }
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

        // ---- キーボード固定（Input Actions が無いときの予備。対戦ルールの予備と同じ配置） ----
        static void ReadFallbackKeys(int player, ref DBInputState s)
        {
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (player == 0)
                {
                    s.b = kb.dKey.isPressed || kb.spaceKey.isPressed;
                    s.y = kb.aKey.isPressed;
                    s.move = new Vector2((kb.hKey.isPressed ? 1f : 0f) - (kb.fKey.isPressed ? 1f : 0f),
                                         (kb.tKey.isPressed ? 1f : 0f) - (kb.gKey.isPressed ? 1f : 0f));
                }
                else
                {
                    s.b = kb.leftArrowKey.isPressed;
                    s.y = kb.rightArrowKey.isPressed;
                    s.move = new Vector2((kb.lKey.isPressed ? 1f : 0f) - (kb.jKey.isPressed ? 1f : 0f),
                                         (kb.iKey.isPressed ? 1f : 0f) - (kb.kKey.isPressed ? 1f : 0f));
                }
            }
            var pads = Gamepad.all;
            if (player >= 0 && player < pads.Count)
            {
                var g = pads[player];
                s.b |= g.rightShoulder.isPressed;
                s.y |= g.leftShoulder.isPressed;
                Vector2 stick = g.leftStick.ReadValue();
                if (stick.magnitude > 0.2f) s.move += stick;
            }
        }
#else
        static void ReadFallbackKeys(int player, ref DBInputState s)
        {
            if (player == 0)
            {
                s.b = Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.Space);
                s.y = Input.GetKey(KeyCode.A);
                s.move = new Vector2((Input.GetKey(KeyCode.H) ? 1f : 0f) - (Input.GetKey(KeyCode.F) ? 1f : 0f),
                                     (Input.GetKey(KeyCode.T) ? 1f : 0f) - (Input.GetKey(KeyCode.G) ? 1f : 0f));
            }
            else
            {
                s.b = Input.GetKey(KeyCode.LeftArrow);
                s.y = Input.GetKey(KeyCode.RightArrow);
                s.move = new Vector2((Input.GetKey(KeyCode.L) ? 1f : 0f) - (Input.GetKey(KeyCode.J) ? 1f : 0f),
                                     (Input.GetKey(KeyCode.I) ? 1f : 0f) - (Input.GetKey(KeyCode.K) ? 1f : 0f));
            }
        }
#endif

        static string FallbackLabel(int player, string id)
        {
            if (HasPad(player))
            {
                switch (id)
                {
                    case "Y": return "LB";
                    case "B": return "RB";
                    case "Move": return "L スティック";
                }
            }
            bool p1 = player == 0;
            switch (id)
            {
                case "B": return p1 ? "D / Space" : "←";
                case "Y": return p1 ? "A" : "→";
                case "Move": return p1 ? "TFGH" : "IJKL";
                default: return id;
            }
        }
    }
}
