using System;
using UnityEngine;

namespace WhaleFall
{
    public sealed class HarpoonProjectile : MonoBehaviour
    {
        HarpoonGun owner;
        float speed,remaining,radius,ttl;
        bool flying;
        public void Launch(HarpoonGun gun,Vector3 direction)
        {
            owner=gun;speed=gun.projectileSpeed;remaining=gun.range;radius=gun.hitRadius;ttl=Time.time+gun.range/Mathf.Max(1,speed)+1;
            transform.rotation=Quaternion.LookRotation(direction);flying=true;
        }
        void Update()
        {
            if(!flying) return;
            if(!owner||!owner.session||!owner.session.ActiveRound||Time.time>=ttl) {Destroy(gameObject);return;}
            float distance=Mathf.Min(remaining,speed*Time.deltaTime);
            var hits=Physics.SphereCastAll(transform.position,radius,transform.forward,distance,~0,QueryTriggerInteraction.Collide);
            Array.Sort(hits,(a,b)=>a.distance.CompareTo(b.distance));
            foreach(var hit in hits)
            {
                var col=hit.collider;if(!col||col.transform.IsChildOf(owner.transform)||col.transform.IsChildOf(transform)) continue;
                var fish=col.GetComponentInParent<CatchableFish>();var shark=col.GetComponentInParent<SharkBossAI>();
                if(col.isTrigger&&!fish&&!shark) continue;
                transform.position=hit.point;flying=false;
                if(fish) {fish.Hit(owner);Destroy(gameObject);}
                else if(shark) {owner.session.Notify("这条大白鲨不是可捕获鱼类！");owner.session.EmitNoise(hit.point,owner.session.harpoonNoiseRadius);Destroy(gameObject);}
                else {owner.ShowHit(false);Destroy(gameObject,2);}
                return;
            }
            transform.position+=transform.forward*distance;remaining-=distance;if(remaining<=0) Destroy(gameObject);
        }
    }
}
