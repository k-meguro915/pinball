using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering.Universal;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Pinball
{
    /// <summary>
    /// 1 VS 1 の画面分け（2026-11-27）。左半分 = P1 の画面、右半分 = P2 の画面（180 度回して、P2 のゴールが手前）。
    /// 対戦ルール（CameraFit）とドッジボール（DBCameraFit）の両方から使う。
    /// ・Allowed：分けてよいか。FE のあそびかた（試し打ち）は false にする（台を左に寄せて映すため）
    /// ・P1OnlyLayer：P2 の画面に映さないレイヤー。台の上の文字は、P2 の画面では逆さまになるので、このレイヤーに置く
    /// </summary>
    public static class SplitScreen
    {
        public static bool Allowed = true;

        /// <summary>映し方。FE のタイトルの「せってい」で決める（FEGameSettings）。Editor では、分けている間 F2 でも切り替えられる（保存はしない）</summary>
        public enum ViewStyle
        {
            /// <summary>台全体を映す（外側に HudSpace を空け、台を真ん中の線の側へ寄せる）</summary>
            WholeBoard,
            /// <summary>自陣側だけを映す（台の幅いっぱいに大きく映し、自分のゴールの端から、相手側へ OwnHalfReach まで）</summary>
            OwnHalf,
        }

        public static ViewStyle View = ViewStyle.WholeBoard;

        /// <summary>自陣だけを映すとき、真ん中の線から相手側へ、少なくともどこまで映すか（台の半分の長さに対する割合）</summary>
        public const float OwnHalfReach = 0.2f;

        /// <summary>P2 の画面に映さないレイヤー（名前の付いていない 31 番を使う）</summary>
        public const int P1OnlyLayer = 31;

        /// <summary>半分の画面のうち、外側に空けておく割合（HUD のパネルを置く場所）</summary>
        public const float HudSpace = 0.36f;

        /// <summary>対戦ルールで、今画面を分けているか（1 VS 1 の試合中）</summary>
        public static bool VersusActive
        {
            get
            {
                if (!Allowed || !Application.isPlaying) return false;
                var gm = GameManager.Instance;
                return gm != null && gm.mode == MatchMode.Versus && gm.State != MatchState.Title;
            }
        }

        /// <summary>
        /// 分けて映す。main を左半分（P1）、second を右半分（P2。無ければ作る）にする。
        /// 台（halfWidth × halfLength）が、外側に HudSpace を空けた残りに収まる大きさにし、台を真ん中の線の側へ寄せる。
        /// basePos は分けないときのカメラの位置（台の真上）。
        /// </summary>
        public static void Apply(Camera main, ref Camera second, Vector3 basePos, float halfWidth, float halfLength, float margin)
        {
            PollToggle();
            main.rect = new Rect(0f, 0f, 0.5f, 1f);
            if (second == null) second = CreateSecond(main);
            second.enabled = true;
            second.rect = new Rect(0.5f, 0f, 0.5f, 1f);
            second.orthographic = true;

            float aspect = Mathf.Max(0.01f, main.aspect);
            Vector3 p1Pos, p2Pos;
            float size;
            if (View == ViewStyle.OwnHalf)
            {
                // 台の幅いっぱい。縦は、自分のゴールの端から真ん中の線の先（OwnHalfReach）まで入る大きさ
                size = Mathf.Max(halfWidth * margin / aspect, halfLength * (1f + OwnHalfReach) * margin / 2f);
                // 映す範囲の下の端を、自分のゴールの端（halfLength * margin）に合わせる。台全体が入るときは真ん中
                float offset = Mathf.Max(0f, halfLength * margin - size);
                p1Pos = basePos + new Vector3(0f, 0f, -offset); // P1 の陣地 = -Z
                p2Pos = basePos + new Vector3(0f, 0f, offset);  // P2 の陣地 = +Z（180 度回しているので画面の下）
            }
            else
            {
                size = Mathf.Max(halfLength * margin, halfWidth * margin / (aspect * (1f - HudSpace)));
                // P1：外側 = 画面の左。カメラを -X へ動かすと、台は画面の右（真ん中の線の側）へ寄る。
                // P2：180 度回しているので画面の右 = -X。同じく -X へ動かすと、台は画面の左（真ん中の線の側）へ寄る。
                var shift = new Vector3(-HudSpace * size * aspect, 0f, 0f);
                p1Pos = basePos + shift;
                p2Pos = basePos + shift;
            }
            main.orthographicSize = size;
            second.orthographicSize = size;
            main.transform.SetPositionAndRotation(p1Pos, Quaternion.Euler(90f, 0f, 0f));
            second.transform.SetPositionAndRotation(p2Pos, Quaternion.Euler(90f, 180f, 0f));
        }

#if UNITY_EDITOR
        static int lastPollFrame = -1;
#endif

        /// <summary>F2 で映し方を切り替える（Editor での試し用。1 フレームに 1 回だけ読む）</summary>
        static void PollToggle()
        {
#if UNITY_EDITOR
            if (lastPollFrame == Time.frameCount) return;
            lastPollFrame = Time.frameCount;
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null || !kb.f2Key.wasPressedThisFrame) return;
            View = View == ViewStyle.WholeBoard ? ViewStyle.OwnHalf : ViewStyle.WholeBoard;
            Debug.Log($"[Pinball] 画面分けの映し方：{(View == ViewStyle.OwnHalf ? "自陣だけ" : "台全体")}（F2 で切り替え）");
#endif
#endif
        }

        /// <summary>分けるのをやめる（2 台目を止め、1 台目を画面全体に戻す）</summary>
        public static void Release(Camera main, Camera second)
        {
            if (second != null) second.enabled = false;
            if (main != null) main.rect = new Rect(0f, 0f, 1f, 1f);
        }

        public static void DestroySecond(ref Camera second)
        {
            if (second != null) Object.Destroy(second.gameObject);
            second = null;
        }

        static Camera CreateSecond(Camera main)
        {
            var go = new GameObject(main.name + " (P2)");
            var cam = go.AddComponent<Camera>();
            cam.CopyFrom(main);
            cam.cullingMask = main.cullingMask & ~(1 << P1OnlyLayer);

            // URP の設定（ポストプロセスなど）も写す
            var src = main.GetComponent<UniversalAdditionalCameraData>();
            if (src != null)
            {
                var dst = go.AddComponent<UniversalAdditionalCameraData>();
                dst.renderPostProcessing = src.renderPostProcessing;
                dst.antialiasing = src.antialiasing;
                dst.antialiasingQuality = src.antialiasingQuality;
                dst.renderShadows = src.renderShadows;
                dst.volumeLayerMask = src.volumeLayerMask;
                dst.volumeTrigger = src.volumeTrigger;
                dst.stopNaN = src.stopNaN;
                dst.dithering = src.dithering;
            }
            return cam;
        }
    }

    /// <summary>
    /// 台に貼った Canvas（World Space）の文字だけを、同じ位置・向きの別の Canvas（P1OnlyLayer）へ移す。
    /// P2 の画面（SplitScreen の 2 台目のカメラ）には、文字が映らなくなる（逆さまの文字を出さないため）。
    /// 光・矢印・ゲージなどの絵は、もとの Canvas に残るので、両方の画面に映る。
    /// ・もとの Canvas の直下の文字は、そのまま移す（同じ大きさ・向きの Canvas なので、anchoredPosition の意味は変わらない）
    /// ・もっと深いところの文字は、もとの場所に目印を残し、毎フレーム目印の位置に合わせる
    /// 文字はあとから作られることもあるので、ときどき探し直す。毎フレーム Sync() を呼ぶ（位置を決めたあと）。
    /// </summary>
    public sealed class P1OnlyText
    {
        readonly RectTransform source;
        readonly Canvas sourceCanvas;
        readonly RectTransform target;
        readonly Canvas targetCanvas;
        readonly List<(TMP_Text text, RectTransform anchor)> followers = new List<(TMP_Text, RectTransform)>();
        float nextScan;

        public P1OnlyText(RectTransform sourceRoot)
        {
            source = sourceRoot;
            sourceCanvas = sourceRoot.GetComponent<Canvas>();
            var go = new GameObject(sourceRoot.name + " (Text)", typeof(RectTransform));
            go.layer = SplitScreen.P1OnlyLayer;
            target = (RectTransform)go.transform;
            targetCanvas = go.AddComponent<Canvas>();
            targetCanvas.renderMode = RenderMode.WorldSpace;
            MatchRoot();
        }

        public GameObject GameObject => target != null ? target.gameObject : null;

        void MatchRoot()
        {
            target.SetPositionAndRotation(source.position, source.rotation);
            target.localScale = source.localScale;
            target.sizeDelta = source.sizeDelta;
            target.pivot = source.pivot;
            if (sourceCanvas != null)
            {
                targetCanvas.sortingLayerID = sourceCanvas.sortingLayerID;
                targetCanvas.sortingOrder = sourceCanvas.sortingOrder + 1; // 文字は絵の上に
            }
        }

        public void Sync()
        {
            if (source == null || target == null) return;
            MatchRoot();
            if (Time.unscaledTime >= nextScan)
            {
                nextScan = Time.unscaledTime + 0.5f;
                Scan();
            }
            for (int i = followers.Count - 1; i >= 0; i--)
            {
                var (text, anchor) = followers[i];
                if (text == null || anchor == null) { followers.RemoveAt(i); continue; }
                text.rectTransform.SetPositionAndRotation(anchor.position, anchor.rotation);
                text.enabled = anchor.gameObject.activeInHierarchy;
            }
        }

        void Scan()
        {
            foreach (var t in source.GetComponentsInChildren<TMP_Text>(true))
            {
                var rt = t.rectTransform;
                if (rt.parent == source)
                {
                    rt.SetParent(target, false);
                }
                else
                {
                    // 目印を残す（同じ親・同じ大きさと基準点）
                    var anchor = new GameObject(t.name + " (Anchor)", typeof(RectTransform)).GetComponent<RectTransform>();
                    anchor.SetParent(rt.parent, false);
                    anchor.SetSiblingIndex(rt.GetSiblingIndex());
                    anchor.anchorMin = rt.anchorMin;
                    anchor.anchorMax = rt.anchorMax;
                    anchor.pivot = rt.pivot;
                    anchor.sizeDelta = rt.sizeDelta;
                    anchor.anchoredPosition = rt.anchoredPosition;
                    anchor.localRotation = rt.localRotation;
                    anchor.localScale = rt.localScale;

                    Vector2 size = rt.rect.size;
                    rt.SetParent(target, false);
                    rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                    rt.sizeDelta = size;
                    followers.Add((t, anchor));
                }
                SetLayer(rt.gameObject, SplitScreen.P1OnlyLayer);
            }
        }

        static void SetLayer(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform c in go.transform) SetLayer(c.gameObject, layer);
        }

        public void Destroy()
        {
            if (target != null) Object.Destroy(target.gameObject);
        }
    }
}
