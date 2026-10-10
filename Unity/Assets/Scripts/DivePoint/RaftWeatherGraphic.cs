using UnityEngine;
using UnityEngine.UI;

namespace Fishing
{
    // Independent editable Canvas layer. No temporary textures or external shader dependency.
    [RequireComponent(typeof(CanvasRenderer))]
    public class RaftWeatherGraphic : MaskableGraphic
    {
        public enum Layer { Sky, Clouds, Rain }
        public Layer Kind;
        [Range(0, 1)] public float RainAmount;
        public float MotionTime;
        [Tooltip("时段色调：白天白、傍晚暖、夜晚压暗偏蓝。由 RaftWeatherController 按 RaftSkyPresets 写入。")]
        public Color Tint = Color.white;

        /// <summary>只染 RGB，保留绘制时算好的 alpha。</summary>
        private Color Tint_(Color c) { return new Color(c.r * Tint.r, c.g * Tint.g, c.b * Tint.b, c.a); }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = rectTransform.rect;
            if (Kind == Layer.Sky)
            {
                var top = Tint_(Color.Lerp(new Color(.2f,.69f,.93f),new Color(.16f,.24f,.33f),RainAmount));
                var bottom = Tint_(Color.Lerp(new Color(.75f,.92f,.94f),new Color(.49f,.60f,.64f),RainAmount));
                int i=vh.currentVertCount;
                vh.AddVert(new Vector3(r.xMin,r.yMin),bottom,Vector2.zero);
                vh.AddVert(new Vector3(r.xMax,r.yMin),bottom,Vector2.zero);
                vh.AddVert(new Vector3(r.xMax,r.yMax),top,Vector2.zero);
                vh.AddVert(new Vector3(r.xMin,r.yMax),top,Vector2.zero);
                vh.AddTriangle(i,i+1,i+2); vh.AddTriangle(i,i+2,i+3);
                return;
            }
            if (Kind == Layer.Clouds)
            {
                Color c=Tint_(Color.Lerp(new Color(1f,.98f,.87f,.94f),new Color(.28f,.36f,.44f,.95f),RainAmount));
                for(int k=0;k<8;k++)
                {
                    float x=r.xMin+Mathf.Repeat(k*.153f+MotionTime*.002f,1.25f)*r.width-r.width*.1f;
                    float y=r.yMin+r.height*(.12f+.12f*(k%3));
                    for(int n=0;n<5;n++) Ellipse(vh,new Vector2(x+n*37,y+Mathf.Sin(n*1.3f+k)*20),new Vector2(66,26+(n%3)*10),c);
                }
                return;
            }
            if (RainAmount<=0) return;
            for(int k=0;k<160;k++)
            {
                float x=r.xMin+Mathf.Repeat(k*.618034f-MotionTime*.12f,1)*r.width;
                float y=r.yMin+Mathf.Repeat(k*.414214f-MotionTime*(.65f+(k%5)*.05f),1)*r.height;
                Vector2 p=new Vector2(x,y), q=p+new Vector2(10,26);
                Vector2 side=new Vector2(1.1f,-.4f);
                Quad(vh,p-side,p+side,q+side,q-side,Tint_(new Color(.81f,.92f,1f,RainAmount*.4f)));
            }
        }
        private static void Ellipse(VertexHelper vh,Vector2 p,Vector2 radius,Color color)
        {
            int s=vh.currentVertCount; vh.AddVert(p,color,Vector2.zero);
            for(int j=0;j<=24;j++) {float a=j*Mathf.PI/12; vh.AddVert(p+new Vector2(Mathf.Cos(a)*radius.x,Mathf.Sin(a)*radius.y),color,Vector2.zero);}
            for(int j=0;j<24;j++) vh.AddTriangle(s,s+j+1,s+j+2);
        }
        private static void Quad(VertexHelper vh,Vector2 a,Vector2 b,Vector2 c,Vector2 d,Color color)
        {
            int s=vh.currentVertCount;
            vh.AddVert(a,color,Vector2.zero); vh.AddVert(b,color,Vector2.zero); vh.AddVert(c,color,Vector2.zero); vh.AddVert(d,color,Vector2.zero);
            vh.AddTriangle(s,s+1,s+2); vh.AddTriangle(s,s+2,s+3);
        }
    }
}
