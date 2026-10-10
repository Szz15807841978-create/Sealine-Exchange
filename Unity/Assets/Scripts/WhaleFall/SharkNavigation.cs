using System.Collections.Generic;
using UnityEngine;

namespace WhaleFall
{
    // Bounded 3D route search: underwater movement is not tied to a ground NavMesh.
    public sealed class SharkNavigation
    {
        sealed class Node { public Vector3Int key; public float cost,score; public Node parent; public bool closed; }
        readonly List<Vector3> route=new List<Vector3>();
        readonly Dictionary<Vector3Int,Node> nodes=new Dictionary<Vector3Int,Node>();
        readonly RaycastHit[] hits=new RaycastHit[48];
        readonly Collider[] overlaps=new Collider[48];
        float nextPlan;Vector3 plannedTarget;
        public int RemainingCorners=>route.Count;
        bool Ignore(Collider c,SharkBossAI shark)
        {return !c||c.transform.IsChildOf(shark.transform)||c.GetComponentInParent<Underwater3DDiver>()||c.GetComponentInParent<MineralFaunaSwimmer>()||c.GetComponentInParent<CatchableFish>();}
        bool Clear(Vector3 from,Vector3 to,SharkBossAI shark)
        {
            Vector3 delta=to-from;float distance=delta.magnitude;if(distance<.01f) return true;
            int count=Physics.SphereCastNonAlloc(from,shark.bodyRadius+.18f,delta/distance,hits,distance,shark.occlusionMask,QueryTriggerInteraction.Ignore);
            if(count==hits.Length) return false;
            for(int i=0;i<count;i++) if(!Ignore(hits[i].collider,shark)) return false;
            return true;
        }
        bool Free(Vector3 point,SharkBossAI shark)
        {
            if(point.y<shark.depthLimits.x+shark.bodyRadius||point.y>shark.depthLimits.y-shark.bodyRadius) return false;
            int count=Physics.OverlapSphereNonAlloc(point,shark.bodyRadius+.2f,overlaps,shark.occlusionMask,QueryTriggerInteraction.Ignore);
            if(count==overlaps.Length) return false;
            for(int i=0;i<count;i++) if(!Ignore(overlaps[i],shark)) return false;
            return true;
        }
        public Vector3 Guide(SharkBossAI shark,Vector3 target)
        {
            Vector3 origin=shark.transform.position;
            if(Clear(origin,target,shark)) {route.Clear();return target;}
            if(Time.time>=nextPlan&&(route.Count==0||Vector3.Distance(plannedTarget,target)>5||Time.time>=nextPlan+2))
            {Plan(shark,origin,target);nextPlan=Time.time+shark.routeReplanSeconds;plannedTarget=target;}
            while(route.Count>0&&Vector3.Distance(origin,route[0])<1.6f) route.RemoveAt(0);
            for(int i=route.Count-1;i>0;i--) if(Clear(origin,route[i],shark)) {route.RemoveRange(0,i);break;}
            return route.Count>0?route[0]:target;
        }
        void Plan(SharkBossAI shark,Vector3 origin,Vector3 target)
        {
            route.Clear();nodes.Clear();float step=shark.routeCellSize;
            var start=new Node{key=Vector3Int.zero,cost=0,score=Vector3.Distance(origin,target)};nodes.Add(start.key,start);
            Node best=start;float closest=start.score;
            for(int iteration=0;iteration<shark.routeSearchBudget;iteration++)
            {
                Node current=null;
                foreach(var n in nodes.Values) if(!n.closed&&(current==null||n.score<current.score)) current=n;
                if(current==null) break;current.closed=true;
                Vector3 p=origin+(Vector3)current.key*step;float remaining=Vector3.Distance(p,target);
                if(remaining<closest) {closest=remaining;best=current;}
                if(current!=start&&Clear(p,target,shark)) {best=current;break;}
                for(int x=-1;x<=1;x++) for(int y=-1;y<=1;y++) for(int z=-1;z<=1;z++)
                {
                    if(x==0&&y==0&&z==0) continue;
                    var offset=new Vector3Int(x,y,z);var key=current.key+offset;if(key.sqrMagnitude>49) continue;
                    Node existing;nodes.TryGetValue(key,out existing);if(existing!=null&&existing.closed) continue;
                    float cost=current.cost+((Vector3)offset).magnitude*step;if(existing!=null&&cost>=existing.cost) continue;
                    Vector3 next=origin+(Vector3)key*step;if(!Free(next,shark)||!Clear(p,next,shark)) continue;
                    if(existing==null) {existing=new Node{key=key};nodes.Add(key,existing);}
                    existing.cost=cost;existing.score=cost+Vector3.Distance(next,target)*1.15f;existing.parent=current;
                }
            }
            while(best!=null&&best!=start) {route.Add(origin+(Vector3)best.key*step);best=best.parent;}route.Reverse();
        }
        public void Invalidate() {nextPlan=0;route.Clear();}
    }
}
