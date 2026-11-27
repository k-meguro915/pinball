using UnityEngine;

namespace PinballFrontend
{
    /// <summary>
    /// 本編シーンに、実行時に FESession が足す部品（シーンには置かない）。
    /// 毎フレーム Adapter に聞いて、FE に戻るときが来たら戻す。
    /// </summary>
    public class FEMatchBridge : MonoBehaviour
    {
        IFEMatchAdapter adapter;
        bool returning;

        public void Init(IFEMatchAdapter matchAdapter)
        {
            adapter = matchAdapter;
        }

        void Update()
        {
            if (adapter == null || returning) return;
            if (!adapter.Tick()) return;
            returning = true;
            FESession.ReturnToMenu();
        }

        void OnDestroy()
        {
            adapter?.Dispose();
            adapter = null;
        }
    }
}
