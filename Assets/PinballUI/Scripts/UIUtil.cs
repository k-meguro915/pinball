using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PinballUI
{
    /// <summary>UI を組み立てるときの小さな共通処理</summary>
    public static class UIUtil
    {
        /// <summary>折り返しなし（TextMeshPro の版の違いを吸収）</summary>
        public static void NoWrap(TMP_Text t)
        {
#if UNITY_2023_2_OR_NEWER
            t.textWrappingMode = TextWrappingModes.NoWrap;
#else
            t.enableWordWrapping = false;
#endif
        }

        static Sprite white;

        /// <summary>
        /// 真っ白な四角のスプライト。Image の Filled（ゲージ）はスプライトが無いと効かないので、これを使う
        /// </summary>
        public static Sprite WhiteSprite
        {
            get
            {
                if (white == null)
                {
                    var tex = Texture2D.whiteTexture;
                    white = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                    white.name = "PinballUI_White";
                }
                return white;
            }
        }

        public static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static Image NewImage(string name, Transform parent, Sprite sprite, Color color)
        {
            var rt = NewRect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        public static TextMeshProUGUI NewText(string name, Transform parent, string text, float size, Color color,
            TextAlignmentOptions align, TMP_FontAsset font)
        {
            var rt = NewRect(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.raycastTarget = false;
            NoWrap(t);
            return t;
        }
    }
}
