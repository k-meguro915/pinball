using UnityEngine;

namespace Pinball
{
    /// <summary>
    /// サイドレール（台の上を通るワイヤーレール）。自陣の壁際の入口に、相手側へ向かう勢いで入った球を
    /// レールに乗せて運び、相手の陣地（相手のブロッカーとフリッパーの間あたり）に落とす。
    ///
    /// 運んでいる間、球はキネマティックにして path に沿って動かすので、途中の物には当たらず引っかからない。
    /// 入口は床の印とトリガーだけで、壁や角は作らない。
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class SideRail : MonoBehaviour
    {
        [Tooltip("レールの持ち主（0 = P1, 1 = P2）。色分けなどに使う")]
        public int ownerPlayer = 0;

        [Tooltip("通り道（ワールド座標。最初 = 入口、最後 = 出口。Y は無視して床の高さで考える）")]
        public Vector3[] path = new Vector3[0];

        [Tooltip("入口に入るのに必要な、入る向きへの速さ")]
        public float minEntrySpeed = 3f;

        [Tooltip("入口の向き（この向きに進んでいる球だけが乗る）")]
        public Vector3 entryDirection = Vector3.forward;

        [Tooltip("レールの上を進む速さ")]
        public float travelSpeed = 14f;

        [Tooltip("レールの高さ（壁の上を越える）")]
        public float railHeight = 1.15f;

        [Tooltip("出口で放す速さと向き")]
        public float exitSpeed = 7f;
        public Vector3 exitDirection = Vector3.right;

        [Tooltip("ON = 持ち主が最後に打った球だけが乗る（相手の球・相手のレールに自分の球が誤って入るのを防ぐ）")]
        public bool ownerOnly = true;

        [Tooltip("同じ球を続けて乗せない時間（秒）")]
        public float cooldown = 0.5f;

        public bool IsRiding => riding != null;

        /// <summary>この球はこのレールに乗れるか（持ち主が最後に打った球だけ）</summary>
        public bool CanRide(Ball ball) => ball != null && (!ownerOnly || ball.LastHitter == ownerPlayer);
        /// <summary>運んでいる途中の進み具合（0〜1。乗っていなければ 0）</summary>
        public float RideProgress => riding != null && total > 0f ? Mathf.Clamp01(distance / total) : 0f;
        public Vector3 EntrancePoint => path != null && path.Length > 0 ? path[0] : transform.position;
        public Vector3 ExitPoint => path != null && path.Length > 0 ? path[path.Length - 1] : transform.position;
        /// <summary>入口のトリガーの大きさ（ワールド、X と Z）</summary>
        public Vector2 EntranceSize
        {
            get
            {
                var b = GetComponent<BoxCollider>();
                return b != null ? new Vector2(b.size.x, b.size.z) : new Vector2(1f, 0.5f);
            }
        }
        public float TotalLength { get { if (cumulative == null || cumulative.Length != Mathf.Max(path.Length, 1)) BuildLengths(); return total; } }

        Ball riding;
        float distance;
        float[] cumulative;
        float total;
        float readyAt;

        void Awake()
        {
            BuildLengths();
        }

        void BuildLengths()
        {
            int n = path != null ? path.Length : 0;
            cumulative = new float[Mathf.Max(n, 1)];
            total = 0f;
            for (int i = 1; i < n; i++)
            {
                total += Flat(path[i] - path[i - 1]).magnitude;
                cumulative[i] = total;
            }
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        bool InRally => GameManager.Instance != null && GameManager.Instance.State == MatchState.Playing;

        void OnTriggerStay(Collider other)
        {
            if (riding != null || !InRally || Time.time < readyAt || path == null || path.Length < 2) return;

            var ball = other.GetComponentInParent<Ball>();
            if (ball == null || ball.IsFrozen) return;
            if (!CanRide(ball)) return;

            float along = Vector3.Dot(ball.Body.GetVelocity(), entryDirection.normalized);
            if (along < minEntrySpeed) return;

            riding = ball;
            distance = 0f;
            ball.Freeze(true);
            PinballAudio.Play(PinballSound.Launch, 0.7f);
            PowerShot.AddCharge(ball, ChargeSource.Rail, this);
        }

        void FixedUpdate()
        {
            if (riding == null) return;

            // 試合の流れで球が消えた・止まったときは降ろす
            if (!riding.isActiveAndEnabled || !InRally)
            {
                riding = null;
                return;
            }

            distance += travelSpeed * Time.fixedDeltaTime;
            if (distance >= total)
            {
                Vector3 end = Flat(path[path.Length - 1]) + Vector3.up * 0.25f;
                riding.ResetTo(end);
                riding.Launch(exitDirection.normalized * exitSpeed);
                riding = null;
                readyAt = Time.time + cooldown;
                PinballAudio.Play(PinballSound.Slingshot, 0.6f);
                return;
            }

            riding.Body.MovePosition(Evaluate(distance));
        }

        /// <summary>入口からの距離 d の位置（両端の1ユニットで床の高さ ↔ レールの高さへ上り下り）</summary>
        public Vector3 Evaluate(float d)
        {
            if (cumulative == null || cumulative.Length != Mathf.Max(path.Length, 1)) BuildLengths();
            int i = 1;
            while (i < path.Length - 1 && cumulative[i] < d) i++;
            float segLen = Mathf.Max(0.0001f, cumulative[i] - cumulative[i - 1]);
            float t = Mathf.Clamp01((d - cumulative[i - 1]) / segLen);
            Vector3 p = Vector3.Lerp(Flat(path[i - 1]), Flat(path[i]), t);

            float ramp = Mathf.Clamp01(Mathf.Min(d, total - d) / 1f);
            p.y = Mathf.Lerp(0.25f, railHeight, Mathf.SmoothStep(0f, 1f, ramp));
            return p;
        }

        void OnDrawGizmosSelected()
        {
            if (path == null || path.Length < 2) return;
            Gizmos.color = Color.yellow;
            for (int i = 1; i < path.Length; i++) Gizmos.DrawLine(path[i - 1] + Vector3.up, path[i] + Vector3.up);
        }
    }
}
