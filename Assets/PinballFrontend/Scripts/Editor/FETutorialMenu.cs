using System.IO;
using UnityEditor;
using UnityEngine;

namespace PinballFrontend.EditorTools
{
    /// <summary>
    /// メニュー「Tools > Pinball Frontend > Create Tutorial Content」。
    /// あそびかたの右の説明（Data/Tutorial_Versus.asset、Data/Tutorial_Dodgeball.asset）を、ここに書いた文と画像で作り、FEConfig に入れる。
    /// 作ったあとは、アセットを直接直してよい（このメニューをもう一度実行すると、ここの文で上書きする）。
    /// 画像は UI/Tutorial/ にある（Tools~/draw_tutorial_images.ps1 で描いたもの。Versus_Rules.png だけは、もとの操作説明（ControlsGuide）の画像を写したもの）。
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

            var versus = Write(vPath, new[]
            {
                Page("ルール", VersusRulesImage,
                    "・相手のゴール（相手側の端）に球を入れると 1 点。\n" +
                    "・台は真ん中が山。球は、今いる側のゴールへ転がっていく。\n" +
                    "・先に決めた点数（3／5／10 点）を取った方の勝ち。\n" +
                    "・自分のゴールは、{L}／{R} のフリッパーで守る。"),
                Page("操作", ImageDir + "/Versus_Controls.png",
                    "{ControlsTable}"),
                Page("サーブ", ImageDir + "/Versus_Serve.png",
                    "・サーブ番のときに、{Serve} を長押しで溜めて、離すと発射。\n" +
                    "・打つ前なら {Route} で、自陣ルート／相手陣地ルートを切り替えられる。\n" +
                    "・弱すぎて戻ってくるとフォルト。2 回続くと相手に 1 点。\n" +
                    "・サーブは 2 本ごとに交代。"),
                Page("ブロッカー", ImageDir + "/Versus_Blocker.png",
                    "・{Move} で、自分の陣地のブロッカーを動かす。\n" +
                    "・球が来たら {Catch} を押したままでつかみ、離すと発射（{Move} の左右で狙う）。\n" +
                    "・当たる直前に {Bounce} を押すと、強く跳ね返す。\n" +
                    "・強い球が当たると HP が減り、0 になるとしばらくダウン。"),
            });

            var dodge = Write(dPath, new[]
            {
                Page("ルール", ImageDir + "/Dodgeball_Rules.png",
                    "・相手の選手 3 人を、全員アウトにしたら勝ち。\n" +
                    "・相手の色の球に当たるとアウト。球の色は「いま誰の球か」（白は、だれの球でもない）。\n" +
                    "・アウトになった選手は、相手の陣地の左右で外野になる。外野が投げた球で相手をアウトにすると、戻ってくる。\n" +
                    "・{L}／{R} のフリッパーで、球を打ち返す。"),
                Page("操作", ImageDir + "/Dodgeball_Controls.png",
                    "{ControlsTable}\n\n" +
                    "・球が来る直前に {Catch} を押してつかむと「ジャストキャッチ」。投げる球が速くなる。"),
                Page("サーブ", ImageDir + "/Dodgeball_Serve.png",
                    "・サーブは試合の始めだけ。\n" +
                    "・{Serve} を長押しで溜めて、離すと発射。\n" +
                    "・打つ前なら {Route} で、自陣へ／相手陣地へを切り替えられる。\n" +
                    "・弱すぎて戻ってくるとフォルト。2 回続くと、サーブが相手に移る。"),
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
