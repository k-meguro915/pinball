namespace PinballFrontend
{
    /// <summary>遊ぶルール（どちらの本編シーンに入るか）</summary>
    public enum FERule { Versus, Dodgeball }

    /// <summary>VS CPU か、2 人で遊ぶか</summary>
    public enum FEPlayMode { VsCpu, Versus }

    /// <summary>CPU の強さ。本編の並び（EASY, NORMAL, HARD）と同じ順番にしてある</summary>
    public enum FECpuLevel { Easy, Normal, Hard }

    /// <summary>ドッジボールの外野・シュートポストの操作：全自動／ボタン</summary>
    public enum FEControl { Auto, Buttons }
}
