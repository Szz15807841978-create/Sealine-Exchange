using UnityEngine;
using UnityEngine.UI;

namespace Fishing
{
    /// <summary>
    /// 曲线绘制用的自定义 Graphic：一次性把网格 / 阈值线 / 面积填充 / 涨绿跌红折线
    /// 塞进同一个网格里，按 Canvas 的顶点顺序叠放（后加的点画在上面）。
    /// </summary>
    [AddComponentMenu("")]
    [RequireComponent(typeof(CanvasRenderer))]
    public class ChartGraphic : MaskableGraphic
    {
        public ChartView View;

        private UIVertex _v;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (View != null) View.BuildMesh(vh, this);
        }

        /// <summary>加一个四边形。顶点顺序：左下、左上、右上、右下。</summary>
        public void Quad(VertexHelper vh, Vector2 bl, Vector2 tl, Vector2 tr, Vector2 br, Color c)
        {
            int i = vh.currentVertCount;
            Add(vh, bl, c);
            Add(vh, tl, c);
            Add(vh, tr, c);
            Add(vh, br, c);
            vh.AddTriangle(i, i + 1, i + 2);
            vh.AddTriangle(i, i + 2, i + 3);
        }

        /// <summary>加一条有宽度的线段（用两个三角形拼成四边形）。</summary>
        public void Line(VertexHelper vh, Vector2 a, Vector2 b, float width, Color c)
        {
            Vector2 d = b - a;
            float len = d.magnitude;
            if (len < 0.0001f) return;
            Vector2 n = new Vector2(-d.y, d.x) / len * (width * 0.5f);
            Quad(vh, a - n, a + n, b + n, b - n, c);
        }

        /// <summary>加一个实心圆。</summary>
        public void Disc(VertexHelper vh, Vector2 center, float radius, Color c, int segs = 24)
        {
            int start = vh.currentVertCount;
            Add(vh, center, c);
            for (int i = 0; i <= segs; i++)
            {
                float ang = (float)i / segs * Mathf.PI * 2f;
                Add(vh, center + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * radius, c);
            }
            for (int i = 0; i < segs; i++) vh.AddTriangle(start, start + 1 + i, start + 2 + i);
        }

        /// <summary>加一个空心圆环。</summary>
        public void Ring(VertexHelper vh, Vector2 center, float radius, float width, Color c, int segs = 28)
        {
            int start = vh.currentVertCount;
            for (int i = 0; i <= segs; i++)
            {
                float ang = (float)i / segs * Mathf.PI * 2f;
                Vector2 dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                Add(vh, center + dir * (radius - width * 0.5f), c);
                Add(vh, center + dir * (radius + width * 0.5f), c);
            }
            for (int i = 0; i < segs; i++)
            {
                int a = start + i * 2;
                vh.AddTriangle(a, a + 1, a + 3);
                vh.AddTriangle(a, a + 3, a + 2);
            }
        }

        /// <summary>加一条虚线（对应 ctx.setLineDash）。</summary>
        public void DashedLine(VertexHelper vh, Vector2 a, Vector2 b, float width, Color c, float dash, float gap)
        {
            Vector2 d = b - a;
            float len = d.magnitude;
            if (len < 0.0001f) return;
            Vector2 dir = d / len;
            float t = 0f;
            while (t < len)
            {
                float e = Mathf.Min(len, t + dash);
                Line(vh, a + dir * t, a + dir * e, width, c);
                t = e + gap;
            }
        }

        /// <summary>加一个实心三角形（入场点标记用）。</summary>
        public void Tri(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Color col)
        {
            int i = vh.currentVertCount;
            Add(vh, a, col);
            Add(vh, b, col);
            Add(vh, c, col);
            vh.AddTriangle(i, i + 1, i + 2);
        }

        /// <summary>给 ChartView 直接加顶点用（面积填充的三角扇）。</summary>
        public void AddVertRaw(VertexHelper vh, Vector2 p, Color c)
        {
            Add(vh, p, c);
        }

        private void Add(VertexHelper vh, Vector2 p, Color c)
        {
            _v.position = p;
            _v.color = c;
            _v.uv0 = Vector2.zero;
            _v.uv1 = Vector2.zero;
            _v.normal = Vector3.back;
            _v.tangent = new Vector4(1f, 0f, 0f, -1f);
            vh.AddVert(_v);
        }
    }
}
