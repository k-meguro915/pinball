using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PinballFrontend
{
    /// <summary>
    /// 選んでいる行・ボタンの左に出る印（金色の細い帯）と、文字色の切り替え。
    /// キーやパッドで選んだときも、マウスを乗せたときも出す。
    /// </summary>
    public class FESelectAccent : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public Graphic bar;
        public Graphic[] tintTargets;
        public Color normalTint = Color.white;
        public Color selectedTint = Color.white;

        bool selected, hovered;

        void OnEnable() => Apply();

        public void OnSelect(BaseEventData e) { selected = true; Apply(); }
        public void OnDeselect(BaseEventData e) { selected = false; Apply(); }
        public void OnPointerEnter(PointerEventData e) { hovered = true; Apply(); }
        public void OnPointerExit(PointerEventData e) { hovered = false; Apply(); }

        void Apply()
        {
            bool on = selected || hovered;
            if (bar != null) bar.enabled = on;
            if (tintTargets == null) return;
            foreach (var g in tintTargets)
                if (g != null) g.color = on ? selectedTint : normalTint;
        }
    }
}
