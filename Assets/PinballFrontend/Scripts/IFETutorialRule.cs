using System;

namespace PinballFrontend
{
    /// <summary>
    /// あそびかた（チュートリアル）の、ルールごとの違い。本編の型を参照するのは、これを実装するクラスだけにする。
    /// 設定を入れるのは IFEMatchAdapter（ふつうの試合と同じ）。こちらは、あそびかた用の準備と、試合の様子の読み取り。
    /// </summary>
    public interface IFETutorialRule : IDisposable
    {
        /// <summary>
        /// 本編シーンを読み込んだ直後（本編の Awake の後、Start の前）に、Adapter が設定を入れたあとで呼ばれる。
        /// HUD を止める、始めの操作説明を閉じる、試合が終わったらやり直す準備など。できなければ false
        /// </summary>
        bool Setup(FEMatchSettings settings);

        /// <summary>毎フレーム呼ばれる（試合のやり直し、サーブを P1 からにする、など）</summary>
        void Tick();

        /// <summary>右の説明に出す「試合の様子」（TMP のリッチテキスト）</summary>
        string StatusText();

        /// <summary>
        /// 説明の文の印（"L"、"R"、"Serve"、"Route"、"Move"、"Catch"、"Bounce"）を、player のボタン名にする。
        /// このルールに無い印なら null
        /// </summary>
        string ButtonLabel(string token, int player);

        /// <summary>操作の一覧の表の行（左から：やること、印）</summary>
        (string action, string token)[] ControlRows { get; }
    }
}
