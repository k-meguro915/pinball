#if ENABLE_INPUT_SYSTEM
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Pinball.EditorTools
{
    /// <summary>
    /// プロジェクトの Input Actions（Project-wide Actions）のパッドの割り当てを、ステップ31の配置に書き換えるメニュー。
    ///   サーブ（Plunger）・キャッチ（BlockerCatch）：右のボタン（B / buttonEast）→ 下のボタン（A / buttonSouth）
    ///   ルート切り替え（RouteToggle）・跳ね返し（BlockerBounce）：上のボタン（Y / buttonNorth）→ 左のボタン（X / buttonWest）
    /// P1 / P2 マップの、上の4つのアクションの「パッドのボタン」の割り当てだけを書き換える（キーボードやほかのアクションはそのまま）。
    /// 手で設定したい場合はこのメニューを使わず、Input Actions の画面で同じように変更すればよい。
    /// </summary>
    public static class PinballPadBindingsMenu
    {
        static readonly (string action, string from, string to)[] Changes =
        {
            ("Plunger", "buttonEast", "buttonSouth"),
            ("BlockerCatch", "buttonEast", "buttonSouth"),
            ("RouteToggle", "buttonNorth", "buttonWest"),
            ("BlockerBounce", "buttonNorth", "buttonWest"),
        };

        [MenuItem("Tools/Pinball/Set Pad Buttons (Serve-Catch = A, Route-Bounce = X)")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Pinball", "再生を止めてから実行してください。", "OK");
                return;
            }

            var asset = PinballInput.GetProjectWideActions();
            string path = asset != null ? AssetDatabase.GetAssetPath(asset) : null;
            if (asset == null || string.IsNullOrEmpty(path) || !path.EndsWith(".inputactions"))
            {
                EditorUtility.DisplayDialog("Pinball",
                    "Project-wide Actions（Project Settings > Input System Package）が見つかりませんでした。\n" +
                    "Input Actions の画面で、Plunger・BlockerCatch を A（Button South）、RouteToggle・BlockerBounce を X（Button West）に変更してください。",
                    "OK");
                return;
            }

            var log = new StringBuilder();
            int changed = 0;
            foreach (var mapName in new[] { "P1", "P2" })
            {
                var map = asset.FindActionMap(mapName, false);
                if (map == null) continue;
                bool wasEnabled = map.enabled;
                if (wasEnabled) map.Disable();

                foreach (var c in Changes)
                {
                    var action = map.FindAction(c.action, false);
                    if (action == null) continue;
                    for (int i = 0; i < action.bindings.Count; i++)
                    {
                        string p = action.bindings[i].path;
                        if (string.IsNullOrEmpty(p) || !p.EndsWith("/" + c.from)) continue;
                        string np = p.Substring(0, p.Length - c.from.Length) + c.to;
                        action.ChangeBinding(i).WithPath(np);
                        log.AppendLine($"  {mapName}/{c.action}: {p} → {np}");
                        changed++;
                    }
                }

                if (wasEnabled) map.Enable();
            }

            if (changed == 0)
            {
                Debug.Log("[Pinball] 書き換える割り当てはありませんでした（すでに A / X になっているか、B / Y の割り当てがありません）。");
                return;
            }

            File.WriteAllText(path, asset.ToJson());
            AssetDatabase.ImportAsset(path);
            Debug.Log($"[Pinball] パッドの割り当てを {changed} か所書き換えました（{path}）\n{log}");
        }
    }
}
#endif
