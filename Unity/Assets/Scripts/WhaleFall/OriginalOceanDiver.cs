using UnityEngine;

namespace WhaleFall
{
    /// <summary>Swims through the original ocean slope on a fixed YZ plane.</summary>
    [RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
    public sealed class OriginalOceanDiver : MonoBehaviour
    {
        public Camera followCamera;
        public Transform visual;
        public Animator animator;
        public Vector3 spawn = new Vector3(0, 3, 14);
        public Vector3 cameraOffset = new Vector3(16, 3, 0);
        public float speed = 4;
        public float fastSpeed = 7;
        public float surfaceY = 8;
        public Vector2 horizontalLimits = new Vector2(0,74);
        public Vector2 verticalLimits = new Vector2(-34,8);
        public bool testDrive;
        public Vector2 testInput;
        Rigidbody body;
        Vector2 move;
        Vector3 cameraVelocity;
        float facing;
        bool fast;
        GUIStyle label, depth;
        Font uiFont;

        void Awake()
        {
            body=GetComponent<Rigidbody>();
            body.useGravity=false;
            body.constraints=RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezeRotation;
            body.interpolation=RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
            if(animator) { animator.applyRootMotion=false; animator.cullingMode=AnimatorCullingMode.AlwaysAnimate; }
            Cursor.visible=true; Cursor.lockState=CursorLockMode.None;
        }

        void Update()
        {
            move=Vector2.ClampMagnitude(testDrive ? testInput : new Vector2(Input.GetAxisRaw("Horizontal"),Input.GetAxisRaw("Vertical")),1);
            fast=!testDrive && (Input.GetKey(KeyCode.LeftShift)||Input.GetKey(KeyCode.RightShift));
            if(!testDrive && Input.GetKeyDown(KeyCode.R)) ResetPosition();
            if(move.x>.05f) facing=0;
            else if(move.x<-.05f) facing=180;
            if(visual) visual.localRotation=Quaternion.Slerp(visual.localRotation,Quaternion.Euler(0,facing,0),Time.deltaTime*10);
            if(animator)
            {
                animator.SetFloat("Speed",move.magnitude);
                animator.SetFloat("DirY",move.y);
                animator.SetBool("Dead",false);
            }
        }

        void FixedUpdate()
        {
            var desired=new Vector3(0,move.y,move.x)*(fast?fastSpeed:speed);
            if(body.position.z<=horizontalLimits.x && desired.z<0 || body.position.z>=horizontalLimits.y && desired.z>0) desired.z=0;
            if(body.position.y<=verticalLimits.x && desired.y<0 || body.position.y>=verticalLimits.y && desired.y>0) desired.y=0;
            body.velocity=Vector3.MoveTowards(body.velocity,desired,20*Time.fixedDeltaTime);
        }

        void LateUpdate()
        {
            if(!followCamera) return;
            var target=transform.position+new Vector3(0,0,move.x*1.2f);
            followCamera.transform.position=Vector3.SmoothDamp(followCamera.transform.position,target+cameraOffset,ref cameraVelocity,.3f);
            followCamera.transform.rotation=Quaternion.LookRotation(-cameraOffset,Vector3.up);
        }

        public void ResetPosition()
        {
            body.position=spawn; body.velocity=Vector3.zero;
            cameraVelocity=Vector3.zero;
            if(followCamera) followCamera.transform.position=spawn+cameraOffset;
        }

        void OnGUI()
        {
            if(label==null)
            {
                uiFont=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei","Arial"},18);
                label=new GUIStyle(GUI.skin.label){font=uiFont,fontSize=17};
                label.normal.textColor=new Color(.83f,.96f,1);
                depth=new GUIStyle(label){fontSize=25,fontStyle=FontStyle.Bold};
            }
            var old=GUI.matrix;
            float scale=Screen.height/900f;
            GUI.matrix=Matrix4x4.Scale(Vector3.one*scale);
            GUI.color=new Color(.01f,.055f,.09f,.75f);
            GUI.DrawTexture(new Rect(24,24,280,82),Texture2D.whiteTexture);
            GUI.color=Color.white;
            GUI.Label(new Rect(40,31,260,38),"鲸落 · 原场景下潜",depth);
            GUI.Label(new Rect(41,70,250,27),"深度  "+Mathf.Max(0,surfaceY-transform.position.y).ToString("0.0")+" m",label);
            GUI.Label(new Rect(28,854,1200,30),"WASD / 方向键 游动    Shift 加速    R 返回起点    ·    自由探索",label);
            GUI.matrix=old;
        }

        void OnDestroy() { if(uiFont) Destroy(uiFont); }
    }
}
