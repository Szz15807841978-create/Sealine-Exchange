using UnityEngine;
using UnityEngine.UI;

namespace Fishing
{
    /// <summary>
    /// 编辑模式可见的曲线预览。它是一个真正的场景 Graphic，不依赖 GameController，
    /// 用来逐步把原先纯运行时生成的界面转换成可在 Hierarchy 中编辑的对象。
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu("Fishing/Scene Curve Preview")]
    public class SceneCurvePreview : MaskableGraphic
    {
        [SerializeField, Range(32, 240)] private int samples = 120;
        [SerializeField, Range(1f, 6f)] private float lineWidth = 2.4f;
        [SerializeField] private Color upColor = new Color(0.29f, 0.87f, 0.50f, 1f);
        [SerializeField] private Color downColor = new Color(0.97f, 0.44f, 0.44f, 1f);
        [SerializeField] private Color gridColor = new Color(0.16f, 0.23f, 0.34f, 0.55f);
        [SerializeField] private Color areaTop = new Color(0.22f, 0.74f, 0.97f, 0.18f);
        [SerializeField] private Color areaBottom = new Color(0.22f, 0.74f, 0.97f, 0.015f);

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = rectTransform.rect;
            if (r.width < 2f || r.height < 2f) return;

            const float padL = 42f, padR = 14f, padT = 14f, padB = 24f;
            float left = r.xMin + padL;
            float right = r.xMax - padR;
            float bottom = r.yMin + padB;
            float top = r.yMax - padT;
            float w = right - left;
            float h = top - bottom;
            if (w <= 1f || h <= 1f) return;

            // 网格
            for (int i = 0; i <= 6; i++)
            {
                float x = left + w * i / 6f;
                AddLine(vh, new Vector2(x, bottom), new Vector2(x, top), 1f, gridColor);
            }
            for (int i = 0; i <= 5; i++)
            {
                float y = bottom + h * i / 5f;
                AddLine(vh, new Vector2(left, y), new Vector2(right, y), 1f, gridColor);
            }

            int count = Mathf.Max(2, samples);
            Vector2 previous = PointAt(0, count, left, bottom, w, h);
            for (int i = 1; i < count; i++)
            {
                Vector2 current = PointAt(i, count, left, bottom, w, h);

                // 曲线下方的半透明面积，以每段梯形构成；上下顶点透明度不同形成渐变。
                AddGradientQuad(vh,
                    new Vector2(previous.x, bottom), previous,
                    current, new Vector2(current.x, bottom),
                    areaBottom, areaTop);

                Color segmentColor = current.y >= previous.y ? upColor : downColor;
                AddLine(vh, previous, current, lineWidth, segmentColor);
                previous = current;
            }
        }

        private static Vector2 PointAt(int i, int count, float left, float bottom, float w, float h)
        {
            float t = i / (float)(count - 1);
            // 固定预览路径：上升、回撤、拉升、跳水、反弹，方便在编辑器中观察红绿分段。
            float wave = Mathf.Sin(t * 19f) * 0.055f + Mathf.Sin(t * 43f) * 0.018f;
            float trend;
            if (t < 0.22f) trend = Mathf.Lerp(0.35f, 0.68f, Smooth(t / 0.22f));
            else if (t < 0.42f) trend = Mathf.Lerp(0.68f, 0.49f, Smooth((t - 0.22f) / 0.20f));
            else if (t < 0.70f) trend = Mathf.Lerp(0.49f, 0.82f, Smooth((t - 0.42f) / 0.28f));
            else if (t < 0.82f) trend = Mathf.Lerp(0.82f, 0.27f, Smooth((t - 0.70f) / 0.12f));
            else trend = Mathf.Lerp(0.27f, 0.55f, Smooth((t - 0.82f) / 0.18f));
            float y = Mathf.Clamp01(trend + wave);
            return new Vector2(left + w * t, bottom + h * y);
        }

        private static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        private static void AddLine(VertexHelper vh, Vector2 a, Vector2 b, float width, Color color)
        {
            Vector2 d = b - a;
            float len = d.magnitude;
            if (len < 0.0001f) return;
            Vector2 n = new Vector2(-d.y, d.x) / len * (width * 0.5f);
            AddQuad(vh, a - n, a + n, b + n, b - n, color, color, color, color);
        }

        private static void AddGradientQuad(VertexHelper vh, Vector2 bl, Vector2 tl, Vector2 tr, Vector2 br,
                                            Color bottomColor, Color topColor)
        {
            AddQuad(vh, bl, tl, tr, br, bottomColor, topColor, topColor, bottomColor);
        }

        private static void AddQuad(VertexHelper vh, Vector2 bl, Vector2 tl, Vector2 tr, Vector2 br,
                                    Color cbl, Color ctl, Color ctr, Color cbr)
        {
            int start = vh.currentVertCount;
            AddVert(vh, bl, cbl);
            AddVert(vh, tl, ctl);
            AddVert(vh, tr, ctr);
            AddVert(vh, br, cbr);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }

        private static void AddVert(VertexHelper vh, Vector2 point, Color color)
        {
            var v = UIVertex.simpleVert;
            v.position = point;
            v.color = color;
            vh.AddVert(v);
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            SetVerticesDirty();
        }
#endif
    }
}
