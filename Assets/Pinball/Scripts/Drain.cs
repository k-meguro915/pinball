using UnityEngine;

namespace Pinball
{
    /// <summary>ゴール（アウト穴）。ownerPlayer のゴールにボールが入ると、相手に1点。</summary>
    [RequireComponent(typeof(Collider))]
    public class Drain : MonoBehaviour
    {
        [Tooltip("このゴールを守るプレイヤー（0 = P1, 1 = P2/CPU）")]
        public int ownerPlayer = 0;

        void OnTriggerEnter(Collider other)
        {
            var ball = other.GetComponentInParent<Ball>();
            if (ball != null && GameManager.Instance != null)
                GameManager.Instance.OnGoal(ownerPlayer);
        }
    }
}
