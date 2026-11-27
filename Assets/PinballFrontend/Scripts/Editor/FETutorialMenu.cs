using System.IO;
using UnityEditor;
using UnityEngine;

namespace PinballFrontend.EditorTools
{
    /// <summary>
    /// メニュー「Tools > Pinball Frontend > Create Tutorial Content」。
    /// あそびかたの右の説明（Data/Tutorial_Versus.asset、Data/Tutorial_Dodgeball.asset）を、ここに書いた文と画像で作り、FEConfig に入れる。
    /// 作ったあとは、アセットを直接直してよい（このメニューをもう一度実行すると、ここの文で上書きする）。
    /// 画像は UI/Tutorial/ にある（すべて Tools~/draw_tutorial_images.ps1 で描いたもの）。
    /// 文の中の {L} などの印は FETutorialContent の説明を参照。
    /// </summary>
    public static class FETutorialMenu
    {
        const string DataDir = "Assets/PinballFrontend/Data";
        const string ImageDir = "Assets/PinballFrontend/UI/Tutorial";
        const string VersusRulesImage = ImageDir + "/Versus_Rules.png";

        [MenuItem("Tools/Pinball Frontend/Create Tutorial Content", priority = 11)]
        static void Create()
        {
            string vPath = DataDir + "/Tutorial_Versus.asset";
            string dPath = DataDir + "/Tutorial_Dodgeball.asset";
            if ((File.Exists(vPath) || File.Exists(dPath)) &&
                !EditorUtility.DisplayDialog("Pinball Frontend",
                    "あそびかたの説明のアセットはもうあります。このメニューの文で上書きしますか？\n（アセットを直接直していた場合、その分は消えます）",
                    "上書きする", "やめる"))
                return;

            SetupImages();

            // 文は 1 ページ 1〜2 行（2026-11-27 の見直し：「文字だらけで読めない」）。中身は画像で見せ、文はボタンだけを書く
            var versus = Write(vPath, new[]
            {
                Page("ルール", VersusRulesImage,
                    "{L} {R} で守って、相手のゴールへ！"),
                Page("操作", ImageDir + "/Versus_Controls.png",
                    "{ControlsTable}"),
                Page("サーブ", ImageDir + "/Versus_Serve.png",
                    "{Serve} 長押し → 離して発射\n" +
                    "{Route} でルート切替"),
                Page("ブロッカー", ImageDir + "/Versus_Blocker.png",
                    "{Move} 移動　{Catch} キャッチ　{Bounce} 跳ね返す"),
            });

            var dodge = Write(dPath, new[]
            {
                Page("ルール", ImageDir + "/Dodgeball_Rules.png",
                    "{L} {R} で打ち返して、相手に当てろ！"),
                Page("操作", ImageDir + "/Dodgeball_Controls.png",
                    "{ControlsTable}"),
                Page("サーブ", ImageDir + "/Dodgeball_Serve.png",
                    "{Serve} 長押し → 離して発射\n" +
                    "{Route} でルート切替"),
            });

            var config = FEBuildMenu.LoadOrCreateConfig();
            config.versusTutorial = versus;
            config.dodgeballTutorial = dodge;
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            Selection.activeObject = versus;
            Debug.Log("[PinballFrontend] あそびかたの説明を作り、FEConfig に入れました：" + vPath + "、" + dPath);
        }

        static FETutorialContent.Page Page(string title, string imagePath, string body)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(imagePath);
            if (tex == null) Debug.LogWarning("[PinballFrontend] 画像が見つかりません：" + imagePath);
            return new FETutorialContent.Page { title = title, image = tex, body = body };
        }

        static FETutorialContent Write(string path, FETutorialContent.Page[] pages)
        {
            Directory.CreateDirectory(DataDir);
            var asset = AssetDatabase.LoadAssetAtPath<FETutorialContent>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<FETutorialContent>();
                AssetDatabase.CreateAsset(asset, path);
            }
            asset.pages = pages;
            EditorUtility.SetDirty(asset);
            return asset;
        }

        /// <summary>説明の画像は文字が多いので、ぼやけないように取り込み方を決めておく</summary>
        static void SetupImages()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { ImageDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!(AssetImporter.GetAtPath(path) is TextureImporter ti)) continue;
                bool changed = ti.mipmapEnabled || ti.textureCompression != TextureImporterCompression.CompressedHQ ||
                               ti.maxTextureSize < 2048 || ti.npotScale != TextureImporterNPOTScale.None;
                if (!changed) continue;
                ti.mipmapEnabled = false;
                ti.textureCompression = TextureImporterCompression.CompressedHQ;
                ti.maxTextureSize = 2048;
                ti.npotScale = TextureImporterNPOTScale.None;
                ti.SaveAndReimport();
            }
        }
    }
}
