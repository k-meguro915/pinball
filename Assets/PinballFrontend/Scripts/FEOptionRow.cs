using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PinballFrontend
{
    /// <summary>
    /// 「項目名　◀ 値 ▶」の 1 行。上下で行を選び、左右で値を変える。
    /// 決定（Enter／A）とクリックは次の値へ、右クリックは前の値へ。
    /// </summary>
    public class FEOptionRow : Selectable, IPointerClickHandler, ISubmitHandler
    {
        TextMeshProUGUI valueText;
        string[] options;
        Func<int> getIndex;
        Action<int> setIndex;
        Action onChanged;

        public static FEOptionRow Create(Transform parent, string name, string label, string[] options,
                                         Func<int> get, Action<int> set, Action changed, TMP_FontAsset font, float height)
        {
            var bg = FEUI.CreateImage(parent, name, Color.white);
            FEUI.SetRounded(bg, Color.white);
            bg.gameObject.AddComponent<LayoutElement>().preferredHeight = height;

            var row = bg.gameObject.AddComponent<FEOptionRow>();
            row.targetGraphic = bg;
            row.colors = FEUI.ItemColors();
            row.options = options;
            row.getIndex = get;
            row.setIndex = set;
            row.onChanged = changed;

            float size = height * 0.42f;
            var l = FEUI.CreateText(bg.transform, "Label", label, size, FEUI.TextColor, TextAlignmentOptions.MidlineLeft, font);
            FEUI.Stretch(l.rectTransform);
            l.rectTransform.offsetMin = new Vector2(40f, 0f);
            l.rectTransform.anchorMax = new Vector2(0.48f, 1f);

            row.valueText = FEUI.CreateText(bg.transform, "Value", "", size, FEUI.TextColor, TextAlignmentOptions.Center, font);
            FEUI.Stretch(row.valueText.rectTransform);
            row.valueText.rectTransform.anchorMin = new Vector2(0.48f, 0f);
            row.valueText.rectTransform.offsetMax = new Vector2(-24f, 0f);
            row.valueText.fontStyle = FontStyles.Bold;

            FEUI.AddSelectAccent(bg.gameObject, height, l, row.valueText);
            row.Refresh();
            return row;
        }

        public void Refresh()
        {
            if (valueText == null || options == null || options.Length == 0) return;
            int i = Mathf.Clamp(getIndex(), 0, options.Length - 1);
            // 矢印は少し暗くして、値を目立たせる
            valueText.text = "<alpha=#66>◀<alpha=#FF>　" + options[i] + "　<alpha=#66>▶";
        }

        void Step(int delta)
        {
            if (!IsInteractable() || options == null || options.Length == 0) return;
            int n = options.Length;
            setIndex(((getIndex() + delta) % n + n) % n);
            Refresh();
            onChanged?.Invoke();
        }

        public override void OnMove(AxisEventData eventData)
        {
            if (eventData.moveDir == MoveDirection.Left) { Step(-1); eventData.Use(); return; }
            if (eventData.moveDir == MoveDirection.Right) { Step(1); eventData.Use(); return; }
            base.OnMove(eventData);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            Select();
            Step(eventData.button == PointerEventData.InputButton.Right ? -1 : 1);
        }

        public void OnSubmit(BaseEventData eventData) => Step(1);
    }
}
