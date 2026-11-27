using System;
using UnityEngine;

namespace PinballFrontend
{
    /// <summary>
    /// あそびかたの右の説明（ルールごとに 1 つ：Data/Tutorial_Versus.asset、Data/Tutorial_Dodgeball.asset）。
    /// 文を直すときは、このアセットを直せばよい（コードは触らない）。
    ///
    /// 文の中に次の印を書くと、実行時に、つないでいる機器（パッド／キーボード）のボタン名に置き換わる。
    /// 割り当ては Input Actions から読むので、割り当てを変えても説明がずれない。
    ///   {L} 左のフリッパー　{R} 右のフリッパー　{Serve} サーブ（溜めて発射）　{Route} サーブのルート切り替え
    ///   {Move} 移動（スティック）　{Catch} キャッチ　{Bounce} 跳ね返し（対戦ルールだけ）
    ///   {ControlsTable} 操作の一覧の表（1 VS 1 のときは P1 と P2 を並べる）
    /// </summary>
    [CreateAssetMenu(fileName = "Tutorial", menuName = "Pinball Frontend/Tutorial Content")]
    public class FETutorialContent : ScriptableObject
    {
        [Serializable]
        public class Page
        {
            [Tooltip("タブに出す短い見出し")]
            public string title;
            [Tooltip("説明の画像（無くてもよい）。16:9 を想定")]
            public Texture2D image;
            [TextArea(3, 12)]
            public string body;
        }

        public Page[] pages = new Page[0];
    }
}
