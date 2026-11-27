using UnityEngine;

namespace Pinball
{
    public enum PinballSound { Bumper, Slingshot, FlipperUp, Launch, Goal, Fault }

    /// <summary>
    /// 効果音。音ファイルが無くても鳴るよう、起動時に簡単な音を自動生成する。
    /// 好きな音に変えたいときは Inspector の各 Clip 欄に AudioClip を入れる（入れた音が優先）。
    /// </summary>
    public class PinballAudio : MonoBehaviour
    {
        public static PinballAudio Instance { get; private set; }

        [Range(0f, 1f)] public float masterVolume = 0.8f;

        [Header("差し替え用（空なら自動生成の音）")]
        public AudioClip bumperClip;
        public AudioClip slingshotClip;
        public AudioClip flipperClip;
        public AudioClip launchClip;
        public AudioClip goalClip;
        public AudioClip faultClip;

        [Header("音量")]
        [Range(0f, 1f)] public float bumperVolume = 0.8f;
        [Range(0f, 1f)] public float slingshotVolume = 0.8f;
        [Range(0f, 1f)] public float flipperVolume = 0.5f;
        [Range(0f, 1f)] public float launchVolume = 0.6f;
        [Range(0f, 1f)] public float goalVolume = 0.8f;
        [Range(0f, 1f)] public float faultVolume = 0.7f;

        AudioSource source;
        AudioClip[] clips;
        float[] lastPlayed;

        const int Rate = 44100;

        void Awake()
        {
            Instance = this;
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;

            clips = new[]
            {
                bumperClip != null ? bumperClip : MakeBumper(),
                slingshotClip != null ? slingshotClip : MakeSlingshot(),
                flipperClip != null ? flipperClip : MakeFlipper(),
                launchClip != null ? launchClip : MakeLaunch(),
                goalClip != null ? goalClip : MakeGoal(),
                faultClip != null ? faultClip : MakeFault(),
            };
            lastPlayed = new float[clips.Length];
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>どこからでも呼べる。音の部品が無ければ何もしない</summary>
        public static void Play(PinballSound sound, float volumeScale = 1f)
        {
            if (Instance != null) Instance.PlayInternal(sound, volumeScale);
        }

        void PlayInternal(PinballSound sound, float volumeScale)
        {
            int i = (int)sound;
            if (Time.unscaledTime - lastPlayed[i] < 0.03f) return; // 同じ音の連打を間引く
            lastPlayed[i] = Time.unscaledTime;

            source.pitch = 1f + Random.Range(-0.04f, 0.04f);
            source.PlayOneShot(clips[i], Mathf.Clamp01(VolumeOf(sound) * volumeScale) * masterVolume);
        }

        float VolumeOf(PinballSound s)
        {
            switch (s)
            {
                case PinballSound.Bumper: return bumperVolume;
                case PinballSound.Slingshot: return slingshotVolume;
                case PinballSound.FlipperUp: return flipperVolume;
                case PinballSound.Launch: return launchVolume;
                case PinballSound.Goal: return goalVolume;
                default: return faultVolume;
            }
        }

        // ===================== 自動生成の音 =====================

        delegate float Sample(float t);

        static AudioClip Make(string name, float seconds, Sample f)
        {
            int n = Mathf.CeilToInt(seconds * Rate);
            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(f(i / (float)Rate), -1f, 1f);
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static float Sine(float hz, float t) => Mathf.Sin(2f * Mathf.PI * hz * t);
        static float Noise() => Random.value * 2f - 1f;

        // ポン：高めの音が少し下がりながら消える
        static AudioClip MakeBumper() => Make("Bumper", 0.18f, t =>
            Sine(900f - 1500f * t, t) * Mathf.Exp(-t * 22f) * 0.8f + Noise() * Mathf.Exp(-t * 120f) * 0.3f);

        // バチン：短い雑音＋低いドン
        static AudioClip MakeSlingshot() => Make("Slingshot", 0.12f, t =>
            Noise() * Mathf.Exp(-t * 60f) * 0.7f + Sine(160f, t) * Mathf.Exp(-t * 30f) * 0.6f);

        // カチッ
        static AudioClip MakeFlipper() => Make("Flipper", 0.06f, t =>
            Noise() * Mathf.Exp(-t * 150f) * 0.6f + Sine(110f, t) * Mathf.Exp(-t * 60f) * 0.5f);

        // シュッ：だんだん高くなる雑音
        static AudioClip MakeLaunch()
        {
            float prev = 0f;
            return Make("Launch", 0.3f, t =>
            {
                float k = Mathf.Lerp(0.05f, 0.5f, t / 0.3f); // 簡単なローパス（後半ほど明るい音）
                prev += (Noise() - prev) * k;
                return prev * Mathf.Sin(Mathf.PI * t / 0.3f) * 0.7f;
            });
        }

        // ゴール：ド・ミ・ソ
        static AudioClip MakeGoal() => Make("Goal", 0.6f, t =>
        {
            float hz = t < 0.15f ? 523.25f : t < 0.3f ? 659.25f : 783.99f;
            float local = t < 0.15f ? t : t < 0.3f ? t - 0.15f : t - 0.3f;
            float env = Mathf.Exp(-local * (t < 0.3f ? 10f : 5f));
            return (Sine(hz, t) * 0.6f + Sine(hz * 2f, t) * 0.15f) * env;
        });

        // フォルト：下がる2音
        static AudioClip MakeFault() => Make("Fault", 0.4f, t =>
        {
            float hz = t < 0.18f ? 330f : 247f;
            float local = t < 0.18f ? t : t - 0.18f;
            return (Mathf.Sign(Sine(hz, t)) * 0.25f) * Mathf.Exp(-local * 9f);
        });
    }
}
