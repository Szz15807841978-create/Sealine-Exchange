using UnityEngine;
using UnityEngine.UI;

namespace Fishing
{
    /// <summary>创建 uGUI 元素的小工具（全部用代码搭，不需要预制体）。</summary>
    public static class UiKit
    {
        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        /// <summary>带纯色背景的矩形块。</summary>
        public static Image Block(string name, Transform parent, Color color)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            return img;
        }

        /// <summary>纵向渐变背景（对应 CSS 的 linear-gradient(180deg,...)）。</summary>
        public static Image Gradient(string name, Transform parent, Color top, Color bottom, float angle = 90f)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = Color.white;
            var g = rt.gameObject.AddComponent<Gradient2>();
            g.Top = top;
            g.Bottom = bottom;
            g.Angle = angle;
            return img;
        }

        /// <summary>带 1px 描边的面板。</summary>
        public static Image Outline(string name, Transform parent, Color fill, Color border, float thickness = 1f)
        {
            var img = Block(name, parent, fill);
            UiSkinTheme.ApplyPanel(img, inset: true);
            var o = img.gameObject.AddComponent<Outline>();
            o.effectColor = UiSkinTheme.Active ? new Color(border.r, border.g, border.b, border.a * 0.22f) : border;
            o.effectDistance = new Vector2(thickness, -thickness);
            return img;
        }

        /// <summary>布局用：横向 / 纵向排布。</summary>
        public static HorizontalLayoutGroup Row(RectTransform rt, float spacing, RectOffset padding = null)
        {
            var h = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.padding = padding ?? new RectOffset(0, 0, 0, 0);
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childAlignment = TextAnchor.MiddleLeft;
            return h;
        }

        public static VerticalLayoutGroup Col(RectTransform rt, float spacing, RectOffset padding = null)
        {
            var v = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.padding = padding ?? new RectOffset(0, 0, 0, 0);
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childAlignment = TextAnchor.UpperLeft;
            return v;
        }

        public static LayoutElement Size(RectTransform rt, float w = -1f, float h = -1f, float flexW = -1f, float flexH = -1f)
        {
            var le = rt.gameObject.GetComponent<LayoutElement>();
            if (le == null) le = rt.gameObject.AddComponent<LayoutElement>();
            if (w >= 0f) le.preferredWidth = w;
            if (h >= 0f) le.preferredHeight = h;
            if (flexW >= 0f) le.flexibleWidth = flexW;
            if (flexH >= 0f) le.flexibleHeight = flexH;
            return le;
        }

        public static LayoutElement MinSize(RectTransform rt, float w = -1f, float h = -1f)
        {
            var le = rt.gameObject.GetComponent<LayoutElement>();
            if (le == null) le = rt.gameObject.AddComponent<LayoutElement>();
            if (w >= 0f) le.minWidth = w;
            if (h >= 0f) le.minHeight = h;
            return le;
        }

        /// <summary>把 RectTransform 铺满父对象。</summary>
        public static RectTransform Stretch(RectTransform rt, float l = 0f, float t = 0f, float r = 0f, float b = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(l, b);
            rt.offsetMax = new Vector2(-r, -t);
            return rt;
        }

        /// <summary>固定尺寸 + 锚点定位（用于绝对摆放的元素）。</summary>
        public static RectTransform Fixed(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, float w, float h)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        public static void Clear(Transform t)
        {
            if (t == null) return;
            for (int i = t.childCount - 1; i >= 0; i--)
            {
                Object.Destroy(t.GetChild(i).gameObject);
            }
        }
    }

    /// <summary>渐变背景（用 Sprite 实现，走 uGUI 的 Image 管线）。</summary>
    [AddComponentMenu("")]
    public class Gradient2 : BaseMeshEffect
    {
        public Color Top = Color.white;
        public Color Bottom = Color.black;
        public float Angle = 90f;

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive()) return;

            var ownRect = GetComponent<RectTransform>();
            if (ownRect == null) return;

            float w = Mathf.Max(1f, ownRect.rect.width);
            float h = Mathf.Max(1f, ownRect.rect.height);
            float rad = Angle * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));

            // 计算四个角在梯度方向上的投影范围，做归一化
            Vector3[] corners =
            {
                new Vector3(-w * 0.5f, -h * 0.5f), new Vector3(w * 0.5f, -h * 0.5f),
                new Vector3(w * 0.5f, h * 0.5f),   new Vector3(-w * 0.5f, h * 0.5f),
            };
            float min = float.MaxValue, max = float.MinValue;
            for (int i = 0; i < 4; i++)
            {
                float d = corners[i].x * dir.x + corners[i].y * dir.y;
                if (d < min) min = d;
                if (d > max) max = d;
            }
            float span = Mathf.Max(0.0001f, max - min);

            var v = new UIVertex();
            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref v, i);
                float d = v.position.x * dir.x + v.position.y * dir.y;
                float t = Mathf.Clamp01((d - min) / span);
                v.color = Color.Lerp(Bottom, Top, t);
                vh.SetUIVertex(v, i);
            }
        }
    }
}
