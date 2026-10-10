using UnityEngine;
using UnityEngine.UI;

namespace Fishing
{
    /// <summary>底部飘字提示：对应 index.html 的 #toast。</summary>
    public class ToastView
    {
        private readonly RectTransform _root;
        private readonly Text _text;
        private float _until;

        public ToastView(RectTransform parent)
        {
            var bg = UiKit.Outline("Toast", parent, Palette.ToastBg, Palette.Line);
            _root = bg.rectTransform;
            _root.anchorMin = new Vector2(0.5f, 0f);
            _root.anchorMax = new Vector2(0.5f, 0f);
            _root.pivot = new Vector2(0.5f, 0f);
            _root.anchoredPosition = new Vector2(0f, 34f);
            _root.sizeDelta = new Vector2(360f, 38f);

            _text = Txt.Make(_root, "文本", 13, Palette.Text, TextAnchor.MiddleCenter);
            UiKit.Stretch((RectTransform)_text.transform, 14f, 0f, 14f, 0f);

            _root.gameObject.SetActive(false);
        }

        public void Show(string msg)
        {
            _text.text = msg;
            float w = Mathf.Max(160f, _text.preferredWidth + 36f);
            _root.sizeDelta = new Vector2(w, 38f);
            _root.gameObject.SetActive(true);
            _until = Time.unscaledTime + 1.9f;   // 对应 CSS 的 1900ms
        }

        public void Tick()
        {
            if (!_root.gameObject.activeSelf) return;
            if (Time.unscaledTime >= _until) _root.gameObject.SetActive(false);
        }
    }
}
