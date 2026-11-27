using UnityEngine;

namespace Pinball
{
    /// <summary>当たったときに一瞬光らせる（色を明るくして戻す）。Play() で光る。</summary>
    public class HitFlash : MonoBehaviour
    {
        public Color flashColor = Color.white;
        public float flashTime = 0.1f;

        [Tooltip("空ならこのオブジェクトの Renderer")]
        public Renderer target;

        MaterialPropertyBlock mpb;
        Color baseColor = Color.white;
        float timer;
        bool flashing;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        void Awake()
        {
            if (target == null) target = GetComponent<Renderer>();
            mpb = new MaterialPropertyBlock();
            if (target != null && target.sharedMaterial != null)
            {
                var m = target.sharedMaterial;
                if (m.HasProperty(BaseColorId)) baseColor = m.GetColor(BaseColorId);
                else if (m.HasProperty(ColorId)) baseColor = m.GetColor(ColorId);
            }
        }

        /// <summary>光っていないときの色を変える（ブロッカーの HP 表示などに使う）</summary>
        public void SetBaseColor(Color c)
        {
            baseColor = c;
            if (!flashing) Apply(baseColor);
        }

        public void Play()
        {
            timer = flashTime;
            flashing = true;
            Apply(flashColor);
        }

        void Update()
        {
            if (!flashing) return;
            timer -= Time.deltaTime;
            if (timer <= 0f)
            {
                flashing = false;
                Apply(baseColor);
            }
        }

        void Apply(Color c)
        {
            if (target == null) return;
            target.GetPropertyBlock(mpb);
            mpb.SetColor(BaseColorId, c);
            mpb.SetColor(ColorId, c);
            target.SetPropertyBlock(mpb);
        }
    }
}
