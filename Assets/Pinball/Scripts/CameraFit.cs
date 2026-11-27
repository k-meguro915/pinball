using UnityEngine;

namespace Pinball
{
    /// <summary>
    /// 真上から台を縦向きで映すカメラ（向きは縦で固定）。
    ///   画面上 = +Z（P2 / CPU）、画面下 = -Z（P1）、画面右 = +X
    /// 画面の縦横比に合わせて、台全体が収まるよう正投影サイズを調整する。
    /// 縦長の画面（9:16 など）では、上下にスコア・ゲージ用の帯ができるよう余白を多めに取る。
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

        void LateUpdate()
        {
            if (cam == null) cam = GetComponent<Camera>();

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
