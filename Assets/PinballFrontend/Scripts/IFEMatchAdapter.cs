using System;

namespace PinballFrontend
{
    /// <summary>
    /// 本編の 1 つのルールとのつなぎ役。本編の型を直接参照するのは、これを実装するクラスだけにする。
    /// </summary>
    public interface IFEMatchAdapter : IDisposable
    {
        /// <summary>
        /// 本編シーンを読み込んだ直後（本編の Awake / OnEnable の後、Start の前）に呼ばれる。
        /// 設定を本編に入れる。本編の管理役が見つからないなどで入れられなければ false
        /// </summary>
        bool Apply(FEMatchSettings settings);

        /// <summary>毎フレーム呼ばれる。FE のシーンに戻るときは true</summary>
        bool Tick();
    }
}
