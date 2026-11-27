using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PinballFrontend.EditorTools
{
    /// <summary>
    /// メニュー「Tools > Pinball Frontend > Remove Controls Guide From Scenes」。
    /// F1 で開く操作説明（ControlsGuide）を、本編の 2 つのシーンから外す（2026-11-24 の決定。説明は「あそびかた」に一本化）。
    /// ControlsGuideView の付いた GameObject を消して、シーンを保存する。ControlsGuide のフォルダ（スクリプトや画像）は消さない。
    /// 本編の GameManager は操作説明をリフレクションで探すので、無くなっても動く。
    /// FE からは ControlsGuide の型を直接参照しない（あとでフォルダを消してもコンパイルが通るように、型は名前で探す）。
    /// </summary>
    public static class FEControlsGuideRemover
    {
        const string ViewTypeName = "ControlsGuide.ControlsGuideView";

        [MenuItem("Tools/Pinball Frontend/Remove Controls Guide From Scenes", priority = 30)]
        static void Remove()
        {
            var config = FEBuildMenu.LoadOrCreateConfig();
            var viewType = FindType(ViewTypeName);
            if (viewType == null)
            {
                EditorUtility.DisplayDialog("Pinball Frontend", "ControlsGuideView のスクリプトが見つかりません（もう消してある？）。", "OK");
                return;
            }
            if (!EditorUtility.DisplayDialog("Pinball Frontend",
                    "F1 の操作説明（ControlsGuide）を、本編の 2 つのシーンから外して保存します。\n\n" +
                    config.versusScenePath + "\n" + config.dodgeballScenePath + "\n\n" +
                    "ControlsGuide のフォルダ（スクリプトや画像）は消しません。", "外す", "やめる"))
                return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            string before = EditorSceneManager.GetActiveScene().path;
            var removed = new List<string>();
            foreach (var path in new[] { config.versusScenePath, config.dodgeballScenePath })
            {
                if (string.IsNullOrEmpty(path) || AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null) continue;
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                int n = 0;
                foreach (var c in UnityEngine.Object.FindObjectsByType(viewType, FindObjectsInactive.Include))
                {
                    var comp = c as Component;
                    if (comp == null) continue;
                    var go = comp.gameObject;
                    // 操作説明だけの GameObject なら丸ごと、ほかの部品もあれば操作説明の部品だけ消す
                    if (go.GetComponents<Component>().Length <= 2 && go.transform.childCount == 0) UnityEngine.Object.DestroyImmediate(go);
                    else UnityEngine.Object.DestroyImmediate(comp);
                    n++;
                }
                if (n > 0)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    removed.Add(path + "（" + n + " 個）");
                }
            }

            if (!string.IsNullOrEmpty(before) && AssetDatabase.LoadAssetAtPath<SceneAsset>(before) != null)
                EditorSceneManager.OpenScene(before, OpenSceneMode.Single);

            Debug.Log(removed.Count > 0
                ? "[PinballFrontend] 操作説明（ControlsGuide）を外しました：" + string.Join("、", removed)
                : "[PinballFrontend] 操作説明（ControlsGuide）は、どのシーンにもありませんでした");
        }

        static Type FindType(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(fullName, false);
                if (t != null) return t;
            }
            return null;
        }
    }
}
