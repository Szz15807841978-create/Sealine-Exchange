using UnityEngine;
using UnityEngine.UI;

namespace Fishing.Player
{
    [AddComponentMenu("Fishing/Player/Editable Market Chart")]
    [RequireComponent(typeof(CanvasRenderer))]
    [ExecuteAlways]
    public sealed class PlayerMarketChart : MaskableGraphic
    {
        [Header("曲线外观，可独立移动/缩放此对象")]
        public Color CurveColor=new Color(.45f,.88f,.72f),GridColor=new Color(.16f,.27f,.33f),ZeroColor=new Color(.38f,.78f,.93f),CostColor=new Color(.97f,.77f,.47f);
        public Color EventColor=new Color(1,.55f,.57f);
        public float LineWidth=2.5f;
        [Tooltip("场景里的独立坐标文字，可以逐个调整位置、字体。")]
        public Text[] YLabels;
        [System.NonSerialized] public MarketState State;
        public int Duration=90;
        double low=74,high=126;
        public void Refresh(MarketState state,int duration)
        {
            State=state;Duration=duration;Bounds();
            if(YLabels!=null)for(int i=0;i<YLabels.Length;i++)if(YLabels[i])YLabels[i].text=(low+(high-low)*i/System.Math.Max(1,YLabels.Length-1)-100).ToString("+0;-0;0")+"%";
            SetVerticesDirty();
        }
        void Bounds()
        {
            low=80;high=120;if(State!=null){foreach(var p in State.series){low=System.Math.Min(low,p.p);high=System.Math.Max(high,p.p);}if(State.position!=null){low=System.Math.Min(low,State.position.entry);high=System.Math.Max(high,State.position.entry);}}
            double pad=(high-low)*.16;low=System.Math.Max(0,low-pad);high+=pad;
        }
        Vector2 Point(double t,double p) { var r=rectTransform.rect;return new Vector2(r.xMin+(float)(t/Duration)*r.width,r.yMin+(float)((p-low)/(high-low))*r.height); }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();Bounds();for(int i=0;i<5;i++){double p=low+(high-low)*i/4;Line(vh,Point(0,p),Point(Duration,p),GridColor,1);}
            Line(vh,Point(0,100),Point(Duration,100),ZeroColor,1.5f);
            if(State==null)return;
            if(State.position!=null)Line(vh,Point(State.position.at,State.position.entry),Point(Duration,State.position.entry),CostColor,1);
            for(int i=1;i<State.series.Count;i++)Line(vh,Point(State.series[i-1].t,State.series[i-1].p),Point(State.series[i].t,State.series[i].p),CurveColor,LineWidth);
            foreach(var m in State.markers){var p=Point(m.t,m.p);Color c=m.type=="collect"?ZeroColor:m.type=="event"?EventColor:CostColor;Quad(vh,p-new Vector2(3,3),p+new Vector2(3,3),c);}
        }
        static void Line(VertexHelper vh,Vector2 a,Vector2 b,Color c,float width)
        {
            if((b-a).sqrMagnitude<.0001f)return;Vector2 n=new Vector2(-(b-a).y,(b-a).x).normalized*width*.5f;int i=vh.currentVertCount;
            vh.AddVert(a-n,c,Vector2.zero);vh.AddVert(a+n,c,Vector2.zero);vh.AddVert(b+n,c,Vector2.zero);vh.AddVert(b-n,c,Vector2.zero);vh.AddTriangle(i,i+1,i+2);vh.AddTriangle(i,i+2,i+3);
        }
        static void Quad(VertexHelper vh,Vector2 a,Vector2 b,Color c)
        {
            int i=vh.currentVertCount;vh.AddVert(a,c,Vector2.zero);vh.AddVert(new Vector2(a.x,b.y),c,Vector2.zero);vh.AddVert(b,c,Vector2.zero);vh.AddVert(new Vector2(b.x,a.y),c,Vector2.zero);vh.AddTriangle(i,i+1,i+2);vh.AddTriangle(i,i+2,i+3);
        }
    }
}
