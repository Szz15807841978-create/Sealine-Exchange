using System;
using UnityEngine;

namespace WhaleFall
{
    [DefaultExecutionOrder(100)]
    public sealed class HarpoonGun : MonoBehaviour
    {
        public Underwater3DDiver diver;
        public DiveExpedition session;
        [Header("原创气动鱼叉枪模型 / 鱼叉")]
        public GameObject gunPrefab;
        public GameObject projectilePrefab;
        [Header("鱼叉参数 · 当前原型自动装填，不消耗物品")]
        public float projectileSpeed=32;
        public float range=38;
        public float reloadSeconds=1.6f;
        public float hitRadius=.09f;
        [Header("第一人称持枪位置 / 后坐")]
        public Vector3 viewOffset=new Vector3(.22f,-.2f,.48f);
        public float recoilDistance=.14f;
        [Header("第三人称双臂瞄准绑定")]
        public Transform rightUpperArm,rightForearm,rightHand;
        public Transform leftUpperArm,leftForearm,leftHand;
        Transform viewGun,worldGun,viewMuzzle,worldMuzzle,viewBolt,worldBolt;
        GameObject viewSpear,worldSpear;
        float nextShot,lastShot=-100,hitUntil;
        bool captured,fireRequested;
        GUIStyle textStyle;Font font;
        bool CanAct=>diver&&!diver.inputSuppressed&&session&&session.AcceptsAction&&!session.IsMining;
        void Start()
        {
            if(!diver) diver=GetComponent<Underwater3DDiver>();if(!session) session=GetComponent<DiveExpedition>();
            if(!gunPrefab||!projectilePrefab||!diver.followCamera) {enabled=false;return;}
            viewGun=Instantiate(gunPrefab,diver.followCamera.transform).transform;viewGun.name="第一人称 · 气动鱼叉枪";
            worldGun=Instantiate(gunPrefab,transform).transform;worldGun.name="第三人称 · 气动鱼叉枪";
            var arms=worldGun.Find("ViewArms");if(arms) arms.gameObject.SetActive(false);
            viewMuzzle=viewGun.Find("Muzzle");worldMuzzle=worldGun.Find("Muzzle");
            viewBolt=viewGun.Find("Bolt");worldBolt=worldGun.Find("Bolt");
            viewSpear=viewGun.Find("LoadedSpear").gameObject;worldSpear=worldGun.Find("LoadedSpear").gameObject;
        }
        void Update()
        {
            fireRequested=CanAct&&Input.GetMouseButtonDown(0)&&Time.time>=nextShot&&
                (diver.IsFirstPerson?Cursor.lockState==CursorLockMode.Locked:Input.GetMouseButton(1));
        }
        void LateUpdate()
        {
            if(!viewGun||!worldGun) return;
            bool usable=CanAct;viewGun.gameObject.SetActive(usable&&diver.IsFirstPerson);worldGun.gameObject.SetActive(usable&&!diver.IsFirstPerson);
            float elapsed=Time.time-lastShot,recoil=elapsed<.3f?Mathf.Sin(Mathf.Clamp01(elapsed/.3f)*Mathf.PI):0;
            float reload=elapsed<reloadSeconds?Mathf.Sin(Mathf.Clamp01(elapsed/reloadSeconds)*Mathf.PI):0;
            viewGun.localPosition=viewOffset+Vector3.back*(recoil*recoilDistance)+Vector3.down*reload*.07f;
            viewGun.localRotation=Quaternion.Euler(-recoil*9+reload*5,0,-reload*8);
            Quaternion aim=diver.followCamera.transform.rotation;
            worldGun.SetPositionAndRotation(transform.position+Vector3.up*.1f+aim*new Vector3(.32f,-.1f,.5f-recoil*recoilDistance),aim*Quaternion.Euler(-recoil*7,0,0));
            viewSpear.SetActive(Time.time>=nextShot-.25f);worldSpear.SetActive(Time.time>=nextShot-.25f);
            if(viewBolt) viewBolt.localPosition=new Vector3(0,.075f,-.08f-reload*.18f);
            if(worldBolt) worldBolt.localPosition=new Vector3(0,.075f,-.08f-reload*.18f);
            if(usable&&!diver.IsFirstPerson&&diver.IsAiming)
            {
                AimArm(rightUpperArm,rightForearm,rightHand,worldGun.TransformPoint(new Vector3(0,-.13f,-.2f)),aim*Vector3.right);
                AimArm(leftUpperArm,leftForearm,leftHand,worldGun.TransformPoint(new Vector3(0,-.08f,.26f)),-(aim*Vector3.right));
            }
            if(fireRequested) {fireRequested=false;Fire(diver.IsFirstPerson?viewMuzzle:worldMuzzle);}
        }
        static void AimArm(Transform shoulder,Transform elbow,Transform hand,Vector3 target,Vector3 bend)
        {
            if(!shoulder||!elbow||!hand) return;
            Vector3 a=shoulder.position;float upper=Vector3.Distance(a,elbow.position),lower=Vector3.Distance(elbow.position,hand.position);
            if(upper<.001f||lower<.001f) return;
            Vector3 to=target-a;float distance=Mathf.Clamp(to.magnitude,Mathf.Abs(upper-lower)+.001f,upper+lower-.001f);
            Vector3 direction=to.normalized,plane=Vector3.ProjectOnPlane(bend+Vector3.down*.6f,direction).normalized;
            float along=(upper*upper-lower*lower+distance*distance)/(2*distance);
            Vector3 elbowTarget=a+direction*along+plane*Mathf.Sqrt(Mathf.Max(0,upper*upper-along*along));
            shoulder.rotation=Quaternion.FromToRotation(elbow.position-a,elbowTarget-a)*shoulder.rotation;
            elbow.rotation=Quaternion.FromToRotation(hand.position-elbow.position,target-elbow.position)*elbow.rotation;
        }
        void Fire(Transform muzzle)
        {
            if(!muzzle||!CanAct) return;lastShot=Time.time;nextShot=Time.time+Mathf.Max(.2f,reloadSeconds);
            session.EmitNoise(transform.position,session.harpoonNoiseRadius);
            Vector3 cameraOrigin=diver.followCamera.transform.position,direction=diver.followCamera.transform.forward;
            Vector3 aimPoint=cameraOrigin+direction*range;
            var hits=Physics.RaycastAll(cameraOrigin,direction,range,~0,QueryTriggerInteraction.Collide);
            Array.Sort(hits,(a,b)=>a.distance.CompareTo(b.distance));
            foreach(var h in hits)
            {
                if(h.collider.transform.IsChildOf(transform)) continue;
                if(h.collider.isTrigger&&!h.collider.GetComponentInParent<CatchableFish>()&&!h.collider.GetComponentInParent<SharkBossAI>()) continue;
                aimPoint=h.point;break;
            }
            // No shooting through nearby cover even if the third-person camera can see around it.
            if(!DivePhysics.Visible(transform.position,muzzle.position,transform,null)) {session.Notify("枪口被礁石挡住了，换个位置再发射。");return;}
            foreach(var col in Physics.OverlapSphere(muzzle.position,hitRadius,~0,QueryTriggerInteraction.Ignore))
                if(!col.transform.IsChildOf(transform)) {session.Notify("枪口贴住障碍物，无法发射。");return;}
            var spear=Instantiate(projectilePrefab,muzzle.position,Quaternion.identity);
            spear.GetComponent<HarpoonProjectile>().Launch(this,(aimPoint-muzzle.position).normalized);
        }
        public void ShowHit(bool success) {hitUntil=Time.time+.35f;captured=success;}
        void OnGUI()
        {
            if(!CanAct) return;
            if(textStyle==null) {font=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei","Arial"},16);textStyle=new GUIStyle(GUI.skin.label){font=font,fontSize=16,alignment=TextAnchor.MiddleCenter};textStyle.normal.textColor=Color.white;}
            float cx=Screen.width*.5f,cy=Screen.height*.5f;
            GUI.color=Time.time<hitUntil?(captured?Color.green:Color.yellow):new Color(.8f,1,1,.9f);
            GUI.DrawTexture(new Rect(cx-9,cy-1,18,2),Texture2D.whiteTexture);GUI.DrawTexture(new Rect(cx-1,cy-9,2,18),Texture2D.whiteTexture);GUI.color=Color.white;
            string hint=Time.time<nextShot?"装填 "+(nextShot-Time.time).ToString("0.0")+"s":diver.IsFirstPerson?"左键 发射鱼叉 · V 第三人称 · Esc 释放鼠标":"按住右键瞄准 + 左键发射 · V 第一人称";
            GUI.Label(new Rect(cx-250,cy+24,500,26),hint,textStyle);
        }
        void OnDestroy() {if(viewGun) Destroy(viewGun.gameObject);if(worldGun) Destroy(worldGun.gameObject);if(font) Destroy(font);}
    }
}
