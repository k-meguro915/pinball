using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

namespace PinballFrontend
{
    /// <summary>
    /// FE の画面を組み立てる道具（uGUI＋TextMeshPro）。画面は実行時にコードで作る（本編の HUD や操作説明と同じやり方）。
    /// 色・角丸・背景はここにまとめておき、見た目を直すときはここを直す。
    ///
    /// 見た目の方針：
    /// ・背景は暗い紺のグラデーション。左下に P1 の青、右上に P2 の赤の、ぼんやりした光（本編の P1 / P2 の色と同じ）
    /// ・選んでいるものは金色（本編の DBHud と同じ #ffd84a）。左に細い帯、文字も金色
    /// ・行やボタンは角丸の半透明の板
    /// </summary>
    public static class FEUI
    {
        // ---- 色 ----
        public static readonly Color Background = new Color(0.05f, 0.06f, 0.10f);
        static readonly Color BackgroundTop = new Color(0.09f, 0.11f, 0.19f);
        static readonly Color BackgroundBottom = new Color(0.03f, 0.03f, 0.06f);
        public static readonly Color P1Color = new Color(0.35f, 0.7f, 1f);   // 本編の GameManager と同じ
        public static readonly Color P2Color = new Color(1f, 0.45f, 0.4f);
        public static readonly Color PanelColor = new Color(1f, 1f, 1f, 0.045f);
        public static readonly Color TextColor = new Color(0.93f, 0.94f, 0.97f);
        public static readonly Color SubTextColor = new Color(0.62f, 0.66f, 0.76f);
        public static readonly Color Accent = new Color(1f, 0.85f, 0.29f);
        public static readonly Color ItemNormal = new Color(1f, 1f, 1f, 0.06f);
        public static readonly Color ItemSelected = new Color(1f, 0.85f, 0.29f, 0.16f);
        public static readonly Color ItemPressed = new Color(1f, 0.85f, 0.29f, 0.34f);

        public const float ReferenceWidth = 1920f, ReferenceHeight = 1080f;
        public const float Radius = 18f;

        // ---------------------------------------------------------------
        // 土台

        /// <summary>EventSystem が無ければ作る（入力は Input System の UI モジュール。既定のアクションを使う）</summary>
        public static EventSystem EnsureEventSystem()
        {
            var es = Object.FindAnyObjectByType<EventSystem>();
            if (es != null) return es;
            var go = new GameObject("EventSystem");
            es = go.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            go.AddComponent<InputSystemUIInputModule>();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
            return es;
        }

        /// <summary>
        /// 画面いっぱいの Canvas（1920×1080 基準、縦横の中間で拡大縮小）と背景。フェードイン用に CanvasGroup も付ける。
        /// withBackground = false なら背景を付けない（本編の画面の上に重ねるとき）
        /// </summary>
        public static RectTransform CreateCanvas(string name, bool withBackground = true, int sortingOrder = 0)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            go.AddComponent<CanvasGroup>();

            var root = (RectTransform)go.transform;
            if (withBackground) CreateBackground(root);
            return root;
        }

        /// <summary>背景（グラデーションと、P1 / P2 の色の光）を root いっぱいに敷く</summary>
        public static void CreateBackground(RectTransform root)
        {
            var bg = new GameObject("Background", typeof(RectTransform)).AddComponent<RawImage>();
            bg.transform.SetParent(root, false);
            bg.texture = GradientTexture();
            bg.raycastTarget = false;
            Stretch(bg.rectTransform);

            // P1（左下・青）と P2（右上・赤）のぼんやりした光
            Glow(root, "Glow P1", P1Color, 0.16f, new Vector2(0.08f, 0.12f), 1500f);
            Glow(root, "Glow P2", P2Color, 0.13f, new Vector2(0.92f, 0.9f), 1400f);
        }

        static void Glow(RectTransform root, string name, Color color, float alpha, Vector2 anchor, float size)
        {
            var g = new GameObject(name, typeof(RectTransform)).AddComponent<RawImage>();
            g.transform.SetParent(root, false);
            g.texture = SoftCircleTexture();
            g.color = new Color(color.r, color.g, color.b, alpha);
            g.raycastTarget = false;
            var rt = g.rectTransform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.sizeDelta = new Vector2(size, size);
        }

        /// <summary>中央に置く縦並びの入れ物（幅は固定、高さは中身に合わせる）</summary>
        public static RectTransform CreateColumn(Transform parent, string name, float width, float spacing, Vector2 anchor, Vector2 pivot)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.sizeDelta = new Vector2(width, 0f);
            var v = go.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.childAlignment = TextAnchor.UpperCenter;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            var fit = go.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return rt;
        }

        /// <summary>角丸の半透明の板を、縦並びの入れ物にする（中に行を並べる）</summary>
        public static RectTransform CreatePanelColumn(Transform parent, string name, float width, float spacing, float padding,
                                                      Vector2 anchor, Vector2 pivot)
        {
            var col = CreateColumn(parent, name, width, spacing, anchor, pivot);
            var img = col.gameObject.AddComponent<Image>();
            SetRounded(img, PanelColor);
            img.raycastTarget = false;
            var v = col.GetComponent<VerticalLayoutGroup>();
            int pad = Mathf.RoundToInt(padding);
            v.padding = new RectOffset(pad, pad, pad, pad);
            // 親が並べるときは、大きさは親が決める（ContentSizeFitter と重ねると警告が出る）
            if (parent.GetComponent<LayoutGroup>() != null) Object.Destroy(col.GetComponent<ContentSizeFitter>());
            return col;
        }

        // ---------------------------------------------------------------
        // 部品

        public static Image CreateImage(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = color;
            return img;
        }

        public static TextMeshProUGUI CreateText(Transform parent, string name, string text, float size, Color color,
                                                 TextAlignmentOptions align, TMP_FontAsset font, float preferredHeight = -1f)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.Normal;
            if (preferredHeight > 0f) go.AddComponent<LayoutElement>().preferredHeight = preferredHeight;
            return t;
        }

        /// <summary>角丸のボタン。選ぶと左に金色の帯が出て、文字が金色になる</summary>
        public static Button CreateButton(Transform parent, string name, string label, TMP_FontAsset font, float height,
                                          UnityEngine.Events.UnityAction onClick)
        {
            var bg = CreateImage(parent, name, Color.white);
            SetRounded(bg, Color.white);
            bg.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
            var b = bg.gameObject.AddComponent<Button>();
            b.targetGraphic = bg;
            b.colors = ItemColors();
            b.onClick.AddListener(onClick);

            var t = CreateText(bg.transform, "Label", label, height * 0.44f, TextColor, TextAlignmentOptions.Center, font);
            Stretch(t.rectTransform);
            t.characterSpacing = 4f;

            AddSelectAccent(bg.gameObject, height, t);
            return b;
        }

        /// <summary>選んだときの印（左の金色の帯と文字色）を付ける</summary>
        public static FESelectAccent AddSelectAccent(GameObject target, float height, params Graphic[] tint)
        {
            var bar = CreateImage(target.transform, "Accent", Accent);
            SetRounded(bar, Accent);
            bar.raycastTarget = false;
            var rt = bar.rectTransform;
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(10f, 0f);
            rt.sizeDelta = new Vector2(8f, height * 0.56f);

            var a = target.AddComponent<FESelectAccent>();
            a.bar = bar;
            a.tintTargets = tint;
            a.normalTint = TextColor;
            a.selectedTint = Accent;
            return a;
        }

        /// <summary>選べる部品の板の色（ふだん → 選んでいる → 押した）</summary>
        public static ColorBlock ItemColors()
        {
            var c = ColorBlock.defaultColorBlock;
            c.normalColor = ItemNormal;
            c.highlightedColor = ItemSelected;
            c.selectedColor = ItemSelected;
            c.pressedColor = ItemPressed;
            c.disabledColor = new Color(1f, 1f, 1f, 0.02f);
            c.colorMultiplier = 1f;
            c.fadeDuration = 0.08f;
            return c;
        }

        /// <summary>見出しの下などに引く、金色から透明になる短い線</summary>
        public static Image CreateUnderline(Transform parent, string name, float width, float height)
        {
            var img = CreateImage(parent, name, Accent);
            var le = img.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = height;
            le.preferredWidth = width;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>TMP の文字に、下にぼかした影（アンダーレイ）を付ける。マテリアルはこの文字だけの複製になる</summary>
        public static void AddGlow(TextMeshProUGUI t, Color color, float softness = 0.6f, float dilate = 0.3f)
        {
            var m = t.fontMaterial;
            m.EnableKeyword(ShaderUtilities.Keyword_Underlay);
            m.SetColor(ShaderUtilities.ID_UnderlayColor, color);
            m.SetFloat(ShaderUtilities.ID_UnderlaySoftness, softness);
            m.SetFloat(ShaderUtilities.ID_UnderlayDilate, dilate);
            m.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0f);
            m.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, 0f);
            t.UpdateMeshPadding();
        }

        public static void SetRounded(Image img, Color color)
        {
            img.sprite = RoundedSprite();
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = RoundedTexSize / (Radius * 2f) * 0.5f;
            img.color = color;
        }

        public static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        /// <summary>画面を開いたときに、ふわっと出す（Time.timeScale に関係なく）</summary>
        public static IEnumerator FadeIn(CanvasGroup group, float seconds)
        {
            if (group == null) yield break;
            float t = 0f;
            group.alpha = 0f;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                group.alpha = Mathf.SmoothStep(0f, 1f, t / seconds);
                yield return null;
            }
            group.alpha = 1f;
        }

        // ---------------------------------------------------------------
        // 実行時に作る画像（アセットを増やさないため）

        const int RoundedTexSize = 64;
        static Sprite roundedSprite;
        static Texture2D gradientTex, softCircleTex;

        /// <summary>角丸の四角（9 スライス用）。角の半径はテクスチャの 1/4</summary>
        static Sprite RoundedSprite()
        {
            if (roundedSprite != null) return roundedSprite;
            int n = RoundedTexSize;
            float r = n / 4f;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float cx = Mathf.Clamp(x + 0.5f, r, n - r);
                    float cy = Mathf.Clamp(y + 0.5f, r, n - r);
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                    byte a = (byte)Mathf.RoundToInt(Mathf.Clamp01(r - d + 0.5f) * 255f);
                    px[y * n + x] = new Color32(255, 255, 255, a);
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            roundedSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0,
                                          SpriteMeshType.FullRect, new Vector4(r, r, r, r));
            roundedSprite.name = "FE Rounded";
            return roundedSprite;
        }

        static Texture2D GradientTexture()
        {
            if (gradientTex != null) return gradientTex;
            const int h = 256;
            gradientTex = new Texture2D(1, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < h; y++)
                gradientTex.SetPixel(0, y, Color.Lerp(BackgroundBottom, BackgroundTop, Mathf.SmoothStep(0f, 1f, y / (h - 1f))));
            gradientTex.Apply(false, true);
            return gradientTex;
        }

        static Texture2D SoftCircleTexture()
        {
            if (softCircleTex != null) return softCircleTex;
            const int n = 128;
            softCircleTex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f)) / (n / 2f);
                    float a = Mathf.Clamp01(1f - d);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * a * 255f));
                }
            softCircleTex.SetPixels32(px);
            softCircleTex.Apply(false, true);
            return softCircleTex;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            roundedSprite = null;
            gradientTex = null;
            softCircleTex = null;
        }

        // ---------------------------------------------------------------
        // 入力

        /// <summary>
        /// マウスで空いたところを押すと、何も選ばれていない状態になり、キーやパッドで動かせなくなる。
        /// そのときにキーやパッドが触られたら、fallback を選び直す
        /// </summary>
        public static void KeepSelection(EventSystem es, GameObject fallback)
        {
            if (es == null || fallback == null) return;
            var cur = es.currentSelectedGameObject;
            if (cur != null && cur.activeInHierarchy) return;
            if (NavigationTouched()) es.SetSelectedGameObject(fallback);
        }

        /// <summary>UI の「もどる」（Esc／パッドの B）がこのフレームに押されたか</summary>
        public static bool CancelPressed(EventSystem es)
        {
#if ENABLE_INPUT_SYSTEM
            if (es != null && es.currentInputModule is InputSystemUIInputModule m &&
                m.cancel != null && m.cancel.action != null)
                return m.cancel.action.WasPressedThisFrame();
            return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Escape);
#endif
        }

        /// <summary>パッドがつながっているか（操作の案内の出し分け用）</summary>
        public static bool HasGamepad()
        {
#if ENABLE_INPUT_SYSTEM
            return Gamepad.all.Count > 0;
#else
            return false;
#endif
        }

        static bool NavigationTouched()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) return true;
            foreach (var g in Gamepad.all)
            {
                if (g.dpad.ReadValue().sqrMagnitude > 0.25f || g.leftStick.ReadValue().sqrMagnitude > 0.25f) return true;
                if (g.buttonSouth.wasPressedThisFrame || g.startButton.wasPressedThisFrame) return true;
            }
            return false;
#else
            return Input.anyKeyDown;
#endif
        }

        /// <summary>アプリを終える（Editor では再生を止める）</summary>
        public static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
