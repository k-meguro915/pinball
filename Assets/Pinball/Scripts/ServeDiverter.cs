using UnityEngine;

namespace Pinball
{
    /// <summary>サーブのルート</summary>
    public enum ServeRoute
    {
        /// <summary>レーン途中のカーブで曲がり、自分の陣地の横から入る（従来のピンボールと同じ）</summary>
        OwnField = 0,
        /// <summary>レーンを最後まで上り、相手の陣地の横から入る</summary>
        OpponentField = 1,
    }

    /// <summary>
    /// 盤面の外にある発射レーンの切り替え装置。
    /// - 自陣ルート: 途中のカーブを有効にし、自陣側の壁の切れ目を開ける
    /// - 相手陣地ルート: 途中のカーブを外し、レーン終点側（相手陣地）の切れ目を開ける
    /// ラリー中は Close() で切れ目をすべて閉じる。盤面の中には何も出っ張らない。
    /// </summary>
    public class ServeDiverter : MonoBehaviour
    {
        [Tooltip("自陣側の壁の切れ目をふさぐ板")]
        public Collider ownExitGate;

        [Tooltip("相手陣地側の壁の切れ目をふさぐ板")]
        public Collider opponentExitGate;

        [Tooltip("レーン途中のカーブ（自陣ルートのときだけ有効）")]
        public Collider[] ownRouteCurve;

        public ServeRoute Route { get; private set; } = ServeRoute.OwnField;

        /// <summary>ラリー用：切れ目をすべて閉じる</summary>
        public void Close()
        {
            Route = ServeRoute.OwnField;
            SetSolid(ownExitGate, true);
            SetSolid(opponentExitGate, true);
            SetSolid(ownRouteCurve, true);
        }

        /// <summary>サーブ用：選んだルートの切れ目だけ開ける</summary>
        public void OpenFor(ServeRoute route)
        {
            Route = route;
            SetSolid(ownRouteCurve, route == ServeRoute.OwnField);
            SetSolid(ownExitGate, route != ServeRoute.OwnField);
            SetSolid(opponentExitGate, route != ServeRoute.OpponentField);
        }

        static void SetSolid(Collider[] colliders, bool solid)
        {
            if (colliders == null) return;
            foreach (var c in colliders) SetSolid(c, solid);
        }

        static void SetSolid(Collider c, bool solid)
        {
            if (c == null) return;
            c.enabled = solid;
            var r = c.GetComponent<Renderer>();
            if (r != null) r.enabled = solid;
        }
    }
}
