using UnityEngine;

namespace PinballDodgeball
{
    /// <summary>ドロップターゲット 1 つ。当たったことを列（DBDropTargetBank）へ伝える（Rigidbody なしの止まった当たり判定なので、自分に届く）</summary>
    public class DBDropTarget : MonoBehaviour
    {
        [HideInInspector] public DBDropTargetBank bank;
        [HideInInspector] public int index;

        void OnCollisionEnter(Collision c)
        {
            if (bank != null) bank.OnTargetHit(index, c);
        }
    }
}
