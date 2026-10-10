using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Fishing
{
    /// <summary>
    /// 卡片式窗口的骨架：卡片居中、内容自上而下堆叠。
    /// 自动处理"内容过高"的情况（超出就撑高并把卡片顶部对齐）。
    /// </summary>
    public class WindowFrame
    {
        public RectTransform Root;
        public RectTransform Content;
        public bool Active;
        private RectTransform _card;
        private RectTransform _editableAnchor;

        private const float CardMinW = 560f;
        private const float CardMaxW = 780f;
        private const float PadX = 22f;
        private const float PadTop = 20f;
        private const float PadBottom = 18f;
        private const float MaxHeightRatio = 0.92f;

        public void Build(RectTransform overlay)
        {
            Active = false;
            Root = overlay;
            // “准备开始面板”已经场景化，结算/傍晚窗口重建时必须保留它，
            // 否则下一场会失去场景引用。其余运行时窗口仍按原逻辑清理。
            for (int i = overlay.childCount - 1; i >= 0; i--)
            {
                var child = overlay.GetChild(i);
                if (child.name == "准备开始面板" || child.name == "动态窗口锚点") continue;
                UnityEngine.Object.Destroy(child.gameObject);
            }

            _editableAnchor = overlay.Find("动态窗口锚点") as RectTransform;
            if (_editableAnchor)
            {
                for (int i = _editableAnchor.childCount - 1; i >= 0; i--)
                    UnityEngine.Object.Destroy(_editableAnchor.GetChild(i).gameObject);
            }

            var cardImg = UiKit.Outline("卡片", _editableAnchor ? _editableAnchor : overlay, Palette.ModalBg, Palette.Hex("#26324a"));
            var card = cardImg.rectTransform;
            _card = card;
            if (_editableAnchor) UiKit.Stretch(card);
            else
            {
                card.anchorMin = card.anchorMax = new Vector2(0.5f, 0.5f);
                card.pivot = new Vector2(0.5f, 0.5f);
            }

            Content = UiKit.Rect("内容", card);
            UiKit.Stretch(Content, PadX, PadTop, PadX, PadBottom);
            var col = UiKit.Col(Content, 8f);
            col.childForceExpandWidth = true;
            col.childForceExpandHeight = false;
            col.childControlHeight = true;
            col.childControlWidth = true;
        }

        public void Finish()
        {
            if (!Active || Root == null || Content == null || _card == null) return;

            // BuildUI 可能发生在 Canvas 首次布局之前。先强制计算子项首选尺寸；
            // 若 overlay 仍为 0×0，则从 CanvasScaler 的参考分辨率取得可靠回退值。
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(Content);

            float overlayW = Root.rect.width;
            float overlayH = Root.rect.height;
            if (overlayW < 100f || overlayH < 100f)
            {
                var canvas = Root.GetComponentInParent<Canvas>();
                var canvasRt = canvas != null ? canvas.GetComponent<RectTransform>() : null;
                if (canvasRt != null && canvasRt.rect.width >= 100f && canvasRt.rect.height >= 100f)
                {
                    overlayW = canvasRt.rect.width;
                    overlayH = canvasRt.rect.height;
                }
                else
                {
                    var scaler = canvas != null ? canvas.GetComponent<CanvasScaler>() : null;
                    Vector2 fallback = scaler != null ? scaler.referenceResolution : new Vector2(1220f, 700f);
                    overlayW = Mathf.Max(320f, fallback.x);
                    overlayH = Mathf.Max(360f, fallback.y);
                }
            }

            float w = _editableAnchor ? Mathf.Max(240f, _editableAnchor.rect.width) : Mathf.Clamp(overlayW - 40f, CardMinW, CardMaxW);
            if (w > overlayW - 20f) w = Mathf.Max(240f, overlayW - 20f);

            float h = LayoutUtility.GetPreferredHeight(Content) + PadTop + PadBottom;
            float maxH = _editableAnchor ? Mathf.Max(180f, _editableAnchor.rect.height) : overlayH * MaxHeightRatio;
            h = Mathf.Clamp(h, 180f, maxH);

            _card.sizeDelta = _editableAnchor ? Vector2.zero : new Vector2(w, h);
            _card.anchoredPosition = Vector2.zero;
        }

        // =====================================================================
        //  内容块
        // =====================================================================

        public Text Title(string text, int size = 19)
        {
            var t = Txt.Make(Content, "标题", size, Palette.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiKit.Size(t.rectTransform, -1f, 30f);
            t.text = text;
            return t;
        }

        public Text Sub(string text, float h = 20f)
        {
            var t = Txt.Make(Content, "副标题", 12, Palette.Muted, TextAnchor.MiddleLeft);
            UiKit.Size(t.rectTransform, -1f, h);
            t.text = text;
            return t;
        }

        public Text Caption(string text, float h = 26f)
        {
            var t = Txt.Make(Content, "小标题", 12, Palette.Muted, TextAnchor.LowerLeft, FontStyle.Bold);
            UiKit.Size(t.rectTransform, -1f, h);
            t.text = text;
            return t;
        }

        public RectTransform Spacer(float h)
        {
            var rt = UiKit.Rect("间隔", Content);
            UiKit.Size(rt, -1f, h);
            return rt;
        }

        /// <summary>一行元素（横向）。返回行容器。</summary>
        public RectTransform Row(float height, float spacing = 10f, TextAnchor align = TextAnchor.MiddleLeft)
        {
            var rt = UiKit.Rect("行", Content);
            UiKit.Size(rt, -1f, height);
            var h = UiKit.Row(rt, spacing);
            h.childAlignment = align;
            return rt;
        }

        public Text RowText(RectTransform row, string text, int size, Color color, float width,
                            TextAnchor anchor = TextAnchor.MiddleLeft, FontStyle style = FontStyle.Normal,
                            float flex = -1f)
        {
            var t = Txt.Make(row, "文本", size, color, anchor, style);
            UiKit.Size(t.rectTransform, width, -1f, flex);
            t.text = text;
            return t;
        }

        public Button RowButton(RectTransform row, string label, Widgets.Hue hue, float width, float height,
                                Action onClick, int fontSize = 12)
        {
            return Widgets.MakeButton(row, label, hue, fontSize, width, height, onClick);
        }

        // =====================================================================
        //  数值方框区（对应 .boxes）
        // =====================================================================

        public List<Widgets.Box> Boxes(string[] keys, float height = 58f)
        {
            var row = Row(height, 8f);
            var list = new List<Widgets.Box>();
            for (int i = 0; i < keys.Length; i++)
            {
                var b = Widgets.MakeBox(row, keys[i], 120f, height);
                var le = b.Root.GetComponent<LayoutElement>();
                le.flexibleWidth = 1f;
                le.minWidth = 100f;
                list.Add(b);
            }
            return list;
        }
    }
}
