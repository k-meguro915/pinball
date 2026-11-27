using UnityEngine;

namespace PinballDodgeball
{
    /// <summary>
    /// 発射レーン（盤面の外）と外壁の出入り口。出入り口はサーブ中、選んだルートの方だけ開く。
    /// </summary>
    public class DBServeLane : MonoBehaviour
    {
        public int owner;
        public float laneX;
        public Vector3 plungerPosition;
        public float ownExitZ;
        public float oppExitZ;

        [Header("出入り口（台を作ると自動で入る）")]
        public Collider ownDoor;
        public Collider oppDoor;
        public Renderer ownDoorRenderer;
        public Renderer oppDoorRenderer;

        public void SetDoors(bool ownOpen, bool oppOpen)
        {
            if (ownDoor != null) ownDoor.enabled = !ownOpen;
            if (oppDoor != null) oppDoor.enabled = !oppOpen;
            if (ownDoorRenderer != null) ownDoorRenderer.enabled = !ownOpen;
            if (oppDoorRenderer != null) oppDoorRenderer.enabled = !oppOpen;
        }
    }
}
