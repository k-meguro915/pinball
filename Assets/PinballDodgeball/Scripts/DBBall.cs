using UnityEngine;

namespace PinballDodgeball
{
    /// <summary>
    /// 球。台は水平な XZ 平面で、傾きは加速度で表す（中央が山。今いる側のゴールへ加速）。
    /// LastHitter：最後に打ったプレイヤー（-1 = 誰の球でもない）。
    /// LastOutfield：最後に投げたのが外野ブロッカーならその外野（間に選手が触れると消える）。
    /// 球の色：持ち主の色（誰の球でもないときは neutralColor）。外野が投げた球は少し明るくする。
    /// パワーショット（ジャストキャッチから投げた速い球）：軌跡が出る。ほかのプレイヤーの球になる・止まる・遅くなると終わる。
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class DBBall : MonoBehaviour
    {
        [Tooltip("台の傾き。球は今いる側のゴールへ加速する")]
        public float slopeAcceleration = 9f;
        public float maxSpeed = 30f;

        [Header("色（いま誰の球か）")]
        public bool colorByOwner = true;
        [Tooltip("誰の球でもないときの色")]
        public Color neutralColor = new Color(0.92f, 0.92f, 0.96f);
        [Tooltip("外野が投げた球は、持ち主の色を白へこの割合だけ寄せる")]
        [Range(0f, 1f)] public float outfieldBrighten = 0.35f;
        [Tooltip("色が切り替わる速さ（大きいほどすぐ変わる）")]
        public float colorChangeSpeed = 25f;

        [Header("パワーショット（ジャストキャッチから投げた球）")]
        [Tooltip("この速さより遅くなったらパワーショットを終える")]
        public float powerEndSpeed = 14f;
        public float trailTime = 0.3f;
        public float trailWidth = 0.45f;

        /// <summary>
        /// 最後に打ったプレイヤー（-1 = 誰の球でもない）。別のプレイヤーに変わるとパワーショットは終わる。
        /// 値を入れるたびに FromThrow・FromLauncher・FromServe は消える（選手が投げるときは、入れたあとに FromThrow を立て直す）
        /// </summary>
        public int LastHitter
        {
            get => lastHitter;
            set
            {
                if (value != lastHitter && IsPowerShot) EndPowerShot();
                lastHitter = value;
                FromThrow = false;
                FromLauncher = false;
                FromServe = false;
            }
        }
        /// <summary>持ち主の内野の選手が投げた球か（この球は味方の選手をすり抜け、持ち主のコースターに乗る）。バンパー・壁・スピナーでは消えない</summary>
        public bool FromThrow { get; set; }
        /// <summary>コースターの射出装置が撃った球か（持ち主の外野をすり抜ける）。LastHitter に値が入ると消える</summary>
        public bool FromLauncher { get; set; }
        /// <summary>サーブの球か（外野をすり抜ける）。LastHitter に値が入ると消える（選手に触れるまで）</summary>
        public bool FromServe { get; set; }
        int lastHitter = -1;
        public bool IsPowerShot { get; private set; }
        public DBOutfieldBlocker LastOutfield { get; set; }
        public Rigidbody Body { get; private set; }
        public Collider Col { get; private set; }
        public bool IsHeld { get; private set; }

        public Vector3 Position => Body != null ? Body.position : transform.position;
        public Vector3 Velocity => (Body == null || IsHeld) ? Vector3.zero : Body.GetVelocity();

        Material mat;
        Color currentColor;
        TrailRenderer trail;

        void Awake()
        {
            Body = GetComponent<Rigidbody>();
            Col = GetComponent<Collider>();
            Body.useGravity = false;
            Body.constraints = RigidbodyConstraints.FreezePositionY;
            Body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            Body.interpolation = RigidbodyInterpolation.Interpolate;
            Body.maxDepenetrationVelocity = 10f;
        }

        void Start()
        {
            var rend = GetComponent<Renderer>();
            if (rend != null)
            {
                mat = rend.material; // この球だけの複製（台のマテリアルは変えない）
                currentColor = neutralColor;
                mat.color = currentColor;
            }

            trail = GetComponent<TrailRenderer>();
            if (trail == null) trail = gameObject.AddComponent<TrailRenderer>();
            trail.time = trailTime;
            trail.startWidth = trailWidth;
            trail.endWidth = 0f;
            trail.minVertexDistance = 0.05f;
            var sh = Shader.Find("Sprites/Default");
            if (sh != null) trail.material = new Material(sh);
            trail.emitting = false;
        }

        /// <summary>パワーショットを始める（投げた直後に呼ぶ。LastHitter を決めてから）</summary>
        public void StartPowerShot()
        {
            IsPowerShot = true;
            if (trail != null)
            {
                trail.Clear();
                Color c = currentColor;
                var gm = DBGameManager.Instance;
                if (gm != null && lastHitter >= 0) c = gm.PlayerColor(lastHitter);
                trail.startColor = c;
                trail.endColor = new Color(c.r, c.g, c.b, 0f);
                trail.emitting = true;
            }
        }

        public void EndPowerShot()
        {
            IsPowerShot = false;
            if (trail != null) trail.emitting = false;
        }

        void Update()
        {
            if (!colorByOwner || mat == null) return;
            Color target = neutralColor;
            var gm = DBGameManager.Instance;
            if (gm != null && LastHitter >= 0)
            {
                target = gm.PlayerColor(LastHitter);
                if (LastOutfield != null) target = Color.Lerp(target, Color.white, outfieldBrighten);
            }
            currentColor = Color.Lerp(currentColor, target, 1f - Mathf.Exp(-colorChangeSpeed * Time.deltaTime));
            mat.color = currentColor;
        }

        void FixedUpdate()
        {
            if (IsHeld) return;
            Vector3 v = Body.GetVelocity();
            float z = Body.position.z;
            float dir = Mathf.Abs(z) < 0.02f ? (v.z >= 0f ? 1f : -1f) : Mathf.Sign(z);
            v.z += dir * slopeAcceleration * Time.fixedDeltaTime;
            v.y = 0f;
            float s = v.magnitude;
            if (s > maxSpeed) v *= maxSpeed / s;
            Body.SetVelocity(v);
            if (IsPowerShot && s < powerEndSpeed) EndPowerShot();
        }

        /// <summary>止めて指定の位置に置く（サーブ前・キャッチ中・決着後）</summary>
        public void Hold(Vector3 pos)
        {
            if (IsPowerShot) EndPowerShot();
            if (!IsHeld)
            {
                Body.SetVelocity(Vector3.zero);
                Body.angularVelocity = Vector3.zero;
                Body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                Body.isKinematic = true;
                IsHeld = true;
            }
            Teleport(pos);
        }

        public void MoveHeld(Vector3 pos)
        {
            pos.y = DBConst.BallY;
            Body.MovePosition(pos);
        }

        /// <summary>持ったまま、高さもそのままで動かす（コースターのレーンの上を運ぶとき）</summary>
        public void MoveHeldRaw(Vector3 pos)
        {
            Body.MovePosition(pos);
        }

        public void Release(Vector3 velocity)
        {
            if (IsHeld)
            {
                Body.isKinematic = false;
                Body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                IsHeld = false;
            }
            velocity.y = 0f;
            Body.SetVelocity(velocity);
        }

        public void Teleport(Vector3 pos)
        {
            pos.y = DBConst.BallY;
            Body.position = pos;
            transform.position = pos;
        }

        public void ClearOwner()
        {
            if (IsPowerShot) EndPowerShot();
            LastHitter = -1;
            LastOutfield = null;
        }
    }
}
