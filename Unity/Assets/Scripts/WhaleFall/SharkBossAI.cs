using UnityEngine;

namespace WhaleFall
{
    public enum SharkState { Patrol, Windup, Charge, Chase, Bite, Recover, Search, Investigate }
    public sealed class SharkBossAI : MonoBehaviour
    {
        [Header("绑定")]
        public DiveExpedition expedition;
        public BioMineralNode mineral;
        public Transform head;
        public Animator animator;
        [Header("视野 · 保留遮挡，近身可感知上下与身后")]
        [Min(1)] public float sightDistance=32;
        [Range(10,240)] public float horizontalFov=140;
        [Range(10,180)] public float verticalFov=155;
        [Range(.01f,1)] public float minimumExposure=.28f;
        [Min(.1f)] public float acquisitionSeconds=.65f;
        [Min(.1f)] public float suspicionDecay=.3f;
        [Min(.03f)] public float sensorInterval=.08f;
        public LayerMask occlusionMask=~0;
        [Min(1)] public float closeAwarenessRadius=8;
        [Min(0)] public float predictionSeconds=.35f;
        [Header("遮挡脱战 · 重新锁定必须持续看清")]
        [Range(.15f,1)] public float trackingExposure=.55f;
        [Min(.1f)] public float reacquireSeconds=.65f;
        [Min(.1f)] public float chargeLostSightSeconds=.4f;
        [Range(.1f,1)] public float memoryChaseSpeed=.55f;
        [Min(.5f)] public float searchRadius=4.5f;
        [Min(0)] public float noisePositionError=3;
        [Header("听觉 · 声音引导调查，不隔墙锁定")]
        [Min(0)] public float hearingMultiplier=1;
        [Range(0,1)] public float occludedHearing=.65f;
        [Min(1)] public float investigateSeconds=9;
        [Header("巡游 · 以生物矿为中心")]
        [Min(1)] public float patrolRadius=12;
        [Min(1)] public float excursionRadius=23;
        [Range(0,1)] public float excursionChance=.18f;
        [Min(0)] public float patrolHeight=4;
        [Min(.1f)] public float patrolSpeed=2.8f;
        [Min(1)] public float patrolTurnSpeed=95;
        public Vector2 depthLimits=new Vector2(-18,-2);
        [Header("冲刺 · 可追踪转弯、概率撞击硬直")]
        [Min(.1f)] public float windupSeconds=.65f;
        [Min(.1f)] public float chargeSpeed=14;
        [Min(.1f)] public float chargeSeconds=3.2f;
        [Min(0)] public float chargeTurnSpeed=100;
        [Min(0)] public float chargeOxygenDamage=22;
        [Min(.1f)] public float collisionStunSeconds=2.2f;
        [Range(0,1)] public float collisionStunChance=.35f;
        [Range(.1f,1)] public float obstacleChargeSpeed=.55f;
        [Header("追逐 / 撕咬")]
        [Min(.1f)] public float chaseSpeed=6.2f;
        [Min(1)] public float chaseTurnSpeed=165;
        [Min(.1f)] public float biteRange=1.9f;
        [Min(.1f)] public float biteWindup=.4f;
        [Min(.1f)] public float biteCooldown=1.8f;
        [Min(0)] public float biteOxygenDamage=16;
        [Min(0)] public float knockback=5;
        [Min(.1f)] public float loseSightSeconds=1.5f;
        [Min(.1f)] public float searchSeconds=5;
        [Min(1)] public float leashRadius=43;
        [Header("三维寻路 / 体积避障")]
        [Min(.1f)] public float bodyRadius=.8f;
        [Min(0)] public float bodyHalfLength=1.6f;
        [Min(.2f)] public float avoidanceLookahead=5;
        [Min(.4f)] public float routeReplanSeconds=1.3f;
        [Range(1.5f,5)] public float routeCellSize=2.8f;
        [Range(24,240)] public int routeSearchBudget=100;
        [Header("运行时（只读观察）")]
        [SerializeField] SharkState state;
        [SerializeField,Range(0,1)] float suspicion;
        [SerializeField,Range(0,1)] float exposure;
        [SerializeField] int routeCorners;
        [SerializeField] bool visualContact;
        [SerializeField] float visibleFor;
        public float Suspicion=>suspicion;
        public bool IsHunting=>state==SharkState.Windup||state==SharkState.Charge||state==SharkState.Chase||state==SharkState.Bite||state==SharkState.Recover;
        public string StatusLabel
        {
            get {switch(state) {case SharkState.Windup:return SeesPlayer?"发现目标 · 正在蓄力":"目标被遮挡 · 失去锁定";case SharkState.Charge:return SeesPlayer?"转向冲刺！利用礁石甩开它":"冲刺失去目标 · 正在减速";case SharkState.Chase:return SeesPlayer?"绕障追逐中":"视线被挡 · 仅追查最后位置";case SharkState.Bite:return "撕咬前摇！";case SharkState.Recover:return "撞击 / 攻击硬直";case SharkState.Search:return visibleFor>0?"疑似发现 · 正在确认":"丢失目标 · 区域搜索";case SharkState.Investigate:return "听到动静 · 调查大致方向";default:return suspicion>.12f?"察觉动静":"围绕生物矿巡游";}}
        }
        readonly SharkNavigation navigation=new SharkNavigation();
        Vector3 waypoint,lastSeen,seenVelocity,chargeTarget,heardPoint,steerDirection,searchCenter;
        float sensorAt,stateUntil,lastSeenAt=-100,nextBite,nextWaypoint,steerAt,stuckTime,dodgeUntil,lastSenseAt;
        bool huntingMemory;
        int lastAnimation=-1;Collider contact;
        Transform Player=>expedition&&expedition.diver?expedition.diver.transform:null;
        Vector3 Eye=>head?head.position:transform.position+transform.forward*bodyHalfLength;
        bool SeesPlayer=>visualContact;
        Vector3 PursuitPoint=>SeesPlayer?lastSeen+seenVelocity*predictionSeconds:lastSeen;
        void Start() {state=SharkState.Patrol;PickPatrolPoint();PlayAnimation(0);}
        void Update()
        {
            if(!expedition||!expedition.ActiveRound||!Player||!mineral) return;
            if(Time.time>=sensorAt) {Sense();sensorAt=Time.time+sensorInterval;}
            float dt=Time.deltaTime;
            if(IsHunting&&!SeesPlayer) suspicion=Mathf.MoveTowards(suspicion,0,dt*suspicionDecay);
            if(state==SharkState.Patrol||state==SharkState.Search||state==SharkState.Investigate)
            {
                if(SeesPlayer)
                {float proximity=Mathf.Lerp(2,.8f,Mathf.Clamp01(Vector3.Distance(Eye,Player.position)/sightDistance));suspicion=Mathf.Clamp01(suspicion+dt*Mathf.Pow(exposure,1.3f)*proximity/Mathf.Max(.1f,acquisitionSeconds));}
                else suspicion=Mathf.MoveTowards(suspicion,0,dt*suspicionDecay);
                if(suspicion>=1&&SeesPlayer)
                {chargeTarget=PursuitPoint;SetState(SharkState.Windup,windupSeconds);expedition.Notify("鲨鱼发现你了！它会拐弯追击，利用礁石遮断视线！");}
            }
            switch(state)
            {
                case SharkState.Patrol:
                    if(Time.time>=nextWaypoint||Vector3.Distance(transform.position,waypoint)<2.5f) PickPatrolPoint();
                    Move(waypoint,patrolSpeed,patrolTurnSpeed,dt);break;
                case SharkState.Windup:
                    if(!SeesPlayer&&Time.time-lastSeenAt>chargeLostSightSeconds) {BeginSearch();break;}
                    if(SeesPlayer) chargeTarget=PursuitPoint;
                    Turn(chargeTarget-transform.position,chaseTurnSpeed*dt);
                    if(Time.time>=stateUntil) {navigation.Invalidate();SetState(SharkState.Charge,chargeSeconds);}break;
                case SharkState.Charge:
                    if(!SeesPlayer&&Time.time-lastSeenAt>chargeLostSightSeconds) {BeginSearch();break;}
                    if(SeesPlayer) chargeTarget=PursuitPoint;
                    Move(chargeTarget,chargeSpeed*(SeesPlayer?1:memoryChaseSpeed),chargeTurnSpeed,dt);if(state!=SharkState.Charge) break;
                    if(Vector3.Distance(Eye,Player.position)<biteRange&&CanBite()) {Hit(chargeOxygenDamage);SetState(SharkState.Recover,.45f);}
                    else if(Time.time>=stateUntil||Vector3.Distance(Eye,chargeTarget)<1.5f) SetState(SharkState.Chase,0);break;
                case SharkState.Chase:
                    if(Vector3.Distance(transform.position,mineral.Center)>leashRadius||Time.time-lastSeenAt>loseSightSeconds) {BeginSearch();break;}
                    Move(PursuitPoint,chaseSpeed*(SeesPlayer?1:memoryChaseSpeed),chaseTurnSpeed,dt);
                    if(SeesPlayer&&Time.time>=nextBite&&Vector3.Distance(Eye,Player.position)<biteRange&&CanBite()) SetState(SharkState.Bite,biteWindup);break;
                case SharkState.Bite:
                    Turn(lastSeen-transform.position,chaseTurnSpeed*.65f*dt);
                    if(Time.time>=stateUntil) {if(Vector3.Distance(Eye,Player.position)<biteRange&&CanBite()) Hit(biteOxygenDamage);nextBite=Time.time+biteCooldown;SetState(SharkState.Recover,.55f);}break;
                case SharkState.Recover:
                    if(Time.time>=stateUntil) {if(Time.time-lastSeenAt<=loseSightSeconds) SetState(SharkState.Chase,0);else BeginSearch();}break;
                case SharkState.Search:
                case SharkState.Investigate:
                    Move(waypoint,patrolSpeed*1.3f,chaseTurnSpeed,dt);
                    if(Vector3.Distance(transform.position,waypoint)<2) waypoint=(state==SharkState.Investigate?heardPoint:searchCenter)+Random.insideUnitSphere*searchRadius;
                    if(Time.time>=stateUntil) {suspicion=0;SetState(SharkState.Patrol,0);PickPatrolPoint();}break;
            }
            routeCorners=navigation.RemainingCorners;
            if(animator) animator.speed=state==SharkState.Charge?1.8f:state==SharkState.Chase?1.3f:1;
        }
        public void HearNoise(Vector3 position,float radius)
        {
            if(!expedition||!expedition.ActiveRound||hearingMultiplier<=0) return;
            bool direct=DivePhysics.Visible(Eye,position,transform,Player,occlusionMask);
            if(Vector3.Distance(transform.position,position)>radius*hearingMultiplier*(direct?1:occludedHearing)) return;
            heardPoint=position+Random.insideUnitSphere*noisePositionError*(direct?1:1.5f);
            // Sound estimates an area, not a hidden player's exact position or velocity.
            if(!IsHunting) {waypoint=heardPoint;suspicion=Mathf.Max(suspicion,.22f);SetState(SharkState.Investigate,investigateSeconds);navigation.Invalidate();}
        }
        void Sense()
        {
            float interval=lastSenseAt>0?Mathf.Min(.2f,Time.time-lastSenseAt):sensorInterval;lastSenseAt=Time.time;
            Vector3 center=Player.position,side=expedition.diver.visual?expedition.diver.visual.right:Vector3.right,up=expedition.diver.visual?expedition.diver.visual.up:Vector3.up;
            var samples=new[]{center,center+up*.8f,center-up*.65f,center+side*.5f,center-side*.5f,center+up*.35f+side*.3f,center+up*.35f-side*.3f};
            int visible=0;bool close=Vector3.Distance(transform.position,center)<=closeAwarenessRadius;
            foreach(var point in samples)
            {
                Vector3 delta=point-Eye;if(delta.sqrMagnitude>sightDistance*sightDistance) continue;
                if(!close)
                {
                    Vector3 local=transform.InverseTransformDirection(delta);
                    float horizontal=IsHunting?Mathf.Max(horizontalFov,210):horizontalFov,vertical=IsHunting?Mathf.Max(verticalFov,170):verticalFov;
                    if(Mathf.Abs(Mathf.Atan2(local.x,local.z)*Mathf.Rad2Deg)>horizontal*.5f||Mathf.Abs(Mathf.Atan2(local.y,new Vector2(local.x,local.z).magnitude)*Mathf.Rad2Deg)>vertical*.5f) continue;
                }
                if(DivePhysics.SightVisible(Eye,point,transform,Player,occlusionMask)) visible++;
            }
            exposure=visible/(float)samples.Length;
            float required=huntingMemory?Mathf.Max(minimumExposure,trackingExposure):minimumExposure;
            if(exposure<required)
            {
                // Stop refreshing memory immediately; a glimpse must not restart the full chase timer.
                if(visualContact) {navigation.Invalidate();chargeTarget=lastSeen;}
                visualContact=false;visibleFor=0;seenVelocity=Vector3.zero;return;
            }
            visibleFor+=interval;
            if(!visualContact&&visibleFor<reacquireSeconds) return;
            visualContact=true;
            float elapsed=Time.time-lastSeenAt;
            seenVelocity=elapsed>0&&elapsed<.5f?Vector3.ClampMagnitude((center-lastSeen)/elapsed,expedition.diver.boostSpeed):Vector3.zero;
            lastSeen=center;lastSeenAt=Time.time;
            if(IsHunting) suspicion=1;
        }
        bool CanBite() {Vector3 delta=Player.position-Eye;return SeesPlayer&&Vector3.Dot(transform.forward,delta.normalized)>-.2f&&DivePhysics.SightVisible(Eye,Player.position,transform,Player,occlusionMask);}
        void Hit(float damage) {expedition.TakeOxygenDamage(damage,(Player.position-transform.position).normalized,knockback);nextBite=Time.time+biteCooldown;}
        void BeginSearch()
        {
            searchCenter=lastSeen;waypoint=searchCenter+Random.insideUnitSphere*searchRadius;suspicion=.1f;
            visualContact=false;visibleFor=0;seenVelocity=Vector3.zero;navigation.Invalidate();SetState(SharkState.Search,searchSeconds);
        }
        void SetState(SharkState next,float seconds)
        {
            state=next;stateUntil=Time.time+seconds;
            if(next==SharkState.Windup) huntingMemory=true;
            if(next==SharkState.Patrol) {huntingMemory=false;visualContact=false;visibleFor=0;}
            PlayAnimation(next==SharkState.Bite?1:next==SharkState.Recover?2:0);
        }
        void PlayAnimation(int index) {if(!animator||lastAnimation==index) return;lastAnimation=index;animator.CrossFade(index==1?"Bite":index==2?"Knockback":"Swim",.12f);}
        void PickPatrolPoint()
        {
            float radius=Random.value<excursionChance?excursionRadius:patrolRadius;Vector2 circle=Random.insideUnitCircle.normalized*radius*Random.Range(.75f,1.15f);
            waypoint=mineral.Center+new Vector3(circle.x,patrolHeight+Random.Range(-1.5f,1.5f),circle.y);waypoint.y=Mathf.Clamp(waypoint.y,depthLimits.x,depthLimits.y);nextWaypoint=Time.time+Random.Range(7f,13f);
        }
        void Turn(Vector3 direction,float degrees)
        {if(direction.sqrMagnitude>.001f) {Vector3 up=Mathf.Abs(Vector3.Dot(direction.normalized,Vector3.up))>.98f?transform.up:Vector3.up;transform.rotation=Quaternion.RotateTowards(transform.rotation,Quaternion.LookRotation(direction,up),degrees);}}
        bool BodyObstacle(Vector3 direction,float distance,out RaycastHit hit)
        {
            hit=default(RaycastHit);float best=float.PositiveInfinity;
            for(int i=-1;i<=1;i++) if(DivePhysics.Obstacle(transform.position+transform.forward*bodyHalfLength*i,direction,distance,bodyRadius,transform,Player,out var candidate,occlusionMask)&&candidate.distance<best) {hit=candidate;best=hit.distance;}
            return hit.collider;
        }
        Vector3 Steer(Vector3 desired,float distance)
        {
            if(!BodyObstacle(desired,distance,out var wall)) return desired;
            Vector3 best=Vector3.zero;float bestScore=float.NegativeInfinity;
            for(int y=-1;y<=1;y++) for(int x=-1;x<=1;x++) for(int z=-1;z<=1;z++)
            {
                if(x==0&&y==0&&z==0) continue;Vector3 direction=(desired+new Vector3(x,y,z)*1.5f).normalized;
                float nextY=transform.position.y+direction.y*distance;if(nextY<depthLimits.x+bodyRadius||nextY>depthLimits.y-bodyRadius) continue;
                float clear=BodyObstacle(direction,distance,out var hit)?hit.distance:distance;
                float score=clear/distance*3+Vector3.Dot(direction,desired)*1.3f+Vector3.Dot(direction,steerDirection)*.6f;
                if(score>bestScore) {bestScore=score;best=direction;}
            }
            return best.sqrMagnitude>.01f?best:(wall.normal+Vector3.up*.3f).normalized;
        }
        void Move(Vector3 target,float speed,float turn,float dt)
        {
            target.y=Mathf.Clamp(target.y,depthLimits.x+bodyRadius,depthLimits.y-bodyRadius);
            Vector3 guide=navigation.Guide(this,target),desired=(guide-transform.position).normalized;float look=Mathf.Max(avoidanceLookahead,speed*.45f);
            if(Time.time>=steerAt) {steerDirection=Steer(desired,look);steerAt=Time.time+.12f;}
            bool avoiding=Vector3.Dot(desired,steerDirection)<.85f;Turn(steerDirection,turn*dt*(avoiding?1.35f:1));
            float facing=Mathf.Clamp01(Vector3.Dot(transform.forward,steerDirection)),step=speed*dt*Mathf.Lerp(.25f,1,facing);
            if(state==SharkState.Charge&&(avoiding||Time.time<dodgeUntil)) step*=obstacleChargeSpeed;
            if(BodyObstacle(transform.forward,step+.12f,out var wall))
            {
                if(state==SharkState.Charge&&contact!=wall.collider)
                {contact=wall.collider;if(Random.value<collisionStunChance) {SetState(SharkState.Recover,collisionStunSeconds);expedition.Notify("鲨鱼撞礁硬直！趁机脱离视线。");return;}dodgeUntil=Time.time+.7f;}
                // A failed stun roll never means passing through solid objects.
                Turn(Steer(desired,look)+wall.normal*.8f,turn*dt*2.5f);steerAt=0;stuckTime+=dt;
                if(stuckTime>.65f) {navigation.Invalidate();nextWaypoint=0;stuckTime=0;}return;
            }
            contact=null;stuckTime=0;Vector3 next=transform.position+transform.forward*step;
            next.y=Mathf.Clamp(next.y,depthLimits.x+bodyRadius,depthLimits.y-bodyRadius);transform.position=next;
        }
        void OnDrawGizmosSelected()
        {
            Gizmos.color=new Color(1,.7f,.1f,.7f);Gizmos.DrawLine(Eye,Eye+Quaternion.AngleAxis(-horizontalFov*.5f,transform.up)*transform.forward*sightDistance);Gizmos.DrawLine(Eye,Eye+Quaternion.AngleAxis(horizontalFov*.5f,transform.up)*transform.forward*sightDistance);
            Gizmos.color=new Color(1,.3f,.1f,.25f);Gizmos.DrawWireSphere(transform.position,closeAwarenessRadius);
            if(mineral) {Gizmos.color=new Color(.1f,.8f,1,.3f);Gizmos.DrawWireSphere(mineral.Center+Vector3.up*patrolHeight,patrolRadius);}
        }
    }
}
