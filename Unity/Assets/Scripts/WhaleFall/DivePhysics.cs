using UnityEngine;

namespace WhaleFall
{
    public static class DivePhysics
    {
        // Slightly thick sight probes make tiny rock seams less permissive than a zero-width ray.
        // Reject an embedded eye origin as well, so a nose intersecting cover cannot see from inside it.
        public static bool SightVisible(Vector3 from,Vector3 to,Transform observer,Transform target,int mask=~0)
        {
            const float radius=.08f;
            foreach(var col in Physics.OverlapSphere(from,radius,mask,QueryTriggerInteraction.Ignore))
                if(!IgnoreSightCollider(col,observer,target)) return false;
            Vector3 delta=to-from;if(delta.sqrMagnitude<.001f) return true;
            foreach(var hit in Physics.SphereCastAll(from,radius,delta.normalized,delta.magnitude,mask,QueryTriggerInteraction.Ignore))
                if(!IgnoreSightCollider(hit.collider,observer,target)) return false;
            return true;
        }
        static bool IgnoreSightCollider(Collider col,Transform observer,Transform target)
        {return !col||observer&&col.transform.IsChildOf(observer)||target&&col.transform.IsChildOf(target)||col.GetComponentInParent<MineralFaunaSwimmer>()||col.GetComponentInParent<CatchableFish>();}

        // Trigger interaction volumes, fish, and the querying actor do not constitute cover.
        public static bool Visible(Vector3 from,Vector3 to,Transform observer,Transform target,int mask=~0)
        {
            Vector3 delta=to-from;if(delta.sqrMagnitude<.001f) return true;
            foreach(var hit in Physics.RaycastAll(from,delta.normalized,delta.magnitude,mask,QueryTriggerInteraction.Ignore))
            {
                var t=hit.collider.transform;
                if(observer&&t.IsChildOf(observer)) continue;
                if(target&&t.IsChildOf(target)) continue;
                if(hit.collider.GetComponentInParent<MineralFaunaSwimmer>()) continue;
                return false;
            }
            return true;
        }
        public static bool Obstacle(Vector3 from,Vector3 direction,float distance,float radius,Transform self,Transform ignore,out RaycastHit nearest,int mask=~0)
        {
            nearest=default(RaycastHit);float best=float.PositiveInfinity;
            foreach(var hit in Physics.SphereCastAll(from,radius,direction,distance,mask,QueryTriggerInteraction.Ignore))
            {
                var t=hit.collider.transform;
                if(self&&t.IsChildOf(self)||ignore&&t.IsChildOf(ignore)) continue;
                if(hit.collider.GetComponentInParent<MineralFaunaSwimmer>()||hit.collider.GetComponentInParent<CatchableFish>()) continue;
                if(hit.distance<best) { best=hit.distance;nearest=hit; }
            }
            return nearest.collider;
        }
    }
}
