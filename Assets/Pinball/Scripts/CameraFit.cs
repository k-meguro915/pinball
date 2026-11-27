using UnityEngine;

namespace Pinball
{
    /// <summary>
    /// 真上から台を縦向きで映すカメラ（向きは縦で固定）。
    ///   画面上 = +Z（P2 / CPU）、画面下 = -Z（P1）、画面右 = +X
    /// 画面の縦横比に合わせて、台全体が収まるよう正投影サイズを調整する。
    /// 縦長の画面（9:16 など）では、上下にスコア・ゲージ用の帯ができるよう余白を多めに取る。
    /// 1 VS 1 の試合中は、画面を左右に分ける（SplitScreen。P2 の画面は 2 台目のカメラ）。
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Camera))]
    public class CameraFit : MonoBehaviour
    {
        [Tooltip("台の奥行（Z方向 = 画面の縦）")]
        public float tableLength = 21f;

        [Tooltip("台の幅（X方向 = 画面の横。盤面の外の発射レーンを含む）")]
        public float tableWidth = 12.8f;

        [Tooltip("横長の画面での余白の倍率（スコア等は台の左右に出るので、台は上下いっぱいに映す）")]
        public float landscapeMargin = 1.03f;

        [Tooltip("縦長の画面での上下の余白の倍率（スコア・ゲージは台の上下の帯に出る）")]
        public float portraitVerticalMargin = 1.25f;

        [Tooltip("縦長の画面での左右の余白の倍率")]
        public float portraitHorizontalMargin = 1.04f;

        Camera cam;

        // 1 VS 1 の画面分け（SplitScreen）。分けている間はカメラを横へ動かすので、もとの位置を覚えておく
        Camera second;
        Vector3 basePos;
        bool moved;

        void OnDisable()
        {
            SplitScreen.Release(cam, second);
            SplitScreen.DestroySecond(ref second);
            if (moved) { transform.position = basePos; moved = false; }
        }

        void LateUpdate()
        {
            if (cam == null) cam = GetComponent<Camera>();

            if (SplitScreen.VersusActive)
            {
                if (!moved) { basePos = transform.position; moved = true; }
                SplitScreen.Apply(cam, ref second, basePos, tableWidth / 2f, tableLength / 2f, landscapeMargin);
                return;
            }
            if (second != null || moved)
            {
                SplitScreen.Release(cam, second);
                if (moved) { transform.position = basePos; moved = false; }
            }

            transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            cam.orthographic = true;

            float aspect = Mathf.Max(cam.aspect, 0.1f);
            bool portrait = aspect < 1f;
            float needV = tableLength / 2f * (portrait ? portraitVerticalMargin : landscapeMargin);
            float needH = tableWidth / 2f / aspect * (portrait ? portraitHorizontalMargin : landscapeMargin);
            cam.orthographicSize = Mathf.Max(needV, needH);
        }
    }
}
