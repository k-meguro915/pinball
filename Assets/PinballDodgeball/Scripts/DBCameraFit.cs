using UnityEngine;

namespace PinballDodgeball
{
    /// <summary>
    /// 真上から見下ろすカメラ。横長の画面では台を上下いっぱいに、縦長の画面では左右いっぱいに収める。
    /// 1 VS 1 の試合中は、画面を左右に分ける（Pinball.SplitScreen。P2 の画面は 2 台目のカメラ）
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Camera))]
    public class DBCameraFit : MonoBehaviour
    {
        [Tooltip("ON：台の寸法（DBConst）から自動で決める")]
        public bool fromTable = true;
        public float halfWidth = 7.5f;
        public float halfLength = 13.9f;
        public float margin = 1.03f;
        public float height = 30f;

        Camera cam;
        Camera second; // 1 VS 1 の画面分けの P2 の画面（Pinball.SplitScreen）

        /// <summary>画面分けの P2 の画面のカメラ（分けていないときは null）</summary>
        public Camera SecondCamera => second != null && second.enabled ? second : null;

        void OnDisable()
        {
            Pinball.SplitScreen.Release(cam, second);
            Pinball.SplitScreen.DestroySecond(ref second);
        }

        void LateUpdate()
        {
            if (cam == null) cam = GetComponent<Camera>();
            if (fromTable)
            {
                halfWidth = DBConst.OuterHalfWidth + 0.1f;
                halfLength = DBConst.PlayHalfLength + DBConst.WallThickness + 0.1f;
            }
            cam.orthographic = true;
            if (DBGameManager.SplitActive)
            {
                Pinball.SplitScreen.Apply(cam, ref second, new Vector3(0f, height, 0f), halfWidth, halfLength, margin);
                return;
            }
            if (second != null) Pinball.SplitScreen.Release(cam, second);
            float aspect = Mathf.Max(0.01f, cam.aspect);
            cam.orthographicSize = Mathf.Max(halfLength * margin, halfWidth * margin / aspect);
            transform.SetPositionAndRotation(new Vector3(0f, height, 0f), Quaternion.Euler(90f, 0f, 0f));
        }
    }
}
