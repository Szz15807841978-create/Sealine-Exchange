using UnityEngine;

namespace WhaleFall
{
    public sealed class MineralFaunaSwimmer : MonoBehaviour
    {
        public Transform attraction;
        public Vector3 centerOffset=new Vector3(0,4,0);
        [Min(1)] public float preferredRadius=9;
        [Min(1)] public float maximumRadius=28;
        [Min(.1f)] public float speed=1.2f;
        public float turnSpeed=95;
        public Vector2 yLimits=new Vector2(-16,-2);
        public float avoidanceRadius=.25f;
        Vector3 target;
        float nextChoice;
        float fleeUntil;
        public void Flee(Vector3 source,float seconds)
        {
            fleeUntil=Time.time+seconds;
            Vector3 away=(transform.position-source).normalized;
            target=transform.position+away*12+Vector3.up*2;target.y=Mathf.Clamp(target.y,yLimits.x,yLimits.y);
            nextChoice=fleeUntil;
        }
        void Start() {Choose();}
        void Choose()
        {
            Vector3 center=(attraction?attraction.position:Vector3.zero)+centerOffset;
            // Truncated exponential radial choice: busy inner waters with occasional long excursions.
            float r=Mathf.Min(maximumRadius,-Mathf.Log(Mathf.Max(.001f,1-Random.value))*preferredRadius);
            float a=Random.value*Mathf.PI*2;
            target=center+new Vector3(Mathf.Cos(a)*r,Random.Range(-2f,2f),Mathf.Sin(a)*r);
            target.y=Mathf.Clamp(target.y,yLimits.x,yLimits.y);nextChoice=Time.time+Random.Range(6f,12f);
        }
        void Update()
        {
            bool fleeing=Time.time<fleeUntil;
            if(!fleeing&&(Time.time>=nextChoice||Vector3.Distance(transform.position,target)<.7f)) Choose();
            Vector3 desired=(target-transform.position).normalized;
            RaycastHit hit;
            if(DivePhysics.Obstacle(transform.position,desired,2,avoidanceRadius,transform,null,out hit))
            {desired=(Vector3.ProjectOnPlane(desired,hit.normal)+hit.normal*.7f+Vector3.up*.3f).normalized;nextChoice=Mathf.Min(nextChoice,Time.time+.5f);}
            if(desired.sqrMagnitude>.001f) transform.rotation=Quaternion.RotateTowards(transform.rotation,Quaternion.LookRotation(desired),turnSpeed*Time.deltaTime);
            float step=speed*(fleeing?2.5f:1)*Time.deltaTime;
            if(!DivePhysics.Obstacle(transform.position,transform.forward,step+.06f,avoidanceRadius,transform,null,out hit))
            {Vector3 p=transform.position+transform.forward*step;p.y=Mathf.Clamp(p.y,yLimits.x,yLimits.y);transform.position=p;}
        }
    }
}
