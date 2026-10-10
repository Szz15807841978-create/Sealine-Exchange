using UnityEngine;

namespace WhaleFall
{
    /// <summary>Independent 2.5D swim sandbox. World geometry and physics remain fully 3D.</summary>
    [RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
    public sealed class UnderwaterExploreController : MonoBehaviour
    {
        public Transform visual;
        public Animator animator;
        public Camera followCamera;
        public Vector3 spawn = new Vector3(-15, -7, 0);
        public float swimSpeed = 6f;
        public float boostSpeed = 10f;
        public Vector2 mapMin = new Vector2(-48, -83);
        public Vector2 mapMax = new Vector2(48, -2);
        Rigidbody body;
        Vector2 input;
        bool boosting;
        float facing = 90f;
        Vector3 cameraVelocity;
        GUIStyle titleStyle, bodyStyle, depthStyle;
        public bool ValidationDrive { get; set; }
        public Vector2 ValidationInput { get; set; }

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.useGravity = false;
            body.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            if (animator) animator.applyRootMotion = false;
        }

        void Update()
        {
            input = ValidationDrive ? ValidationInput : new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            input = Vector2.ClampMagnitude(input, 1);
            boosting = !ValidationDrive && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));
            if (!ValidationDrive && Input.GetKeyDown(KeyCode.R)) ResetToEntry();
            if (input.x > .05f) facing = 90;
            else if (input.x < -.05f) facing = -90;
            if (visual) visual.localRotation = Quaternion.Slerp(visual.localRotation, Quaternion.Euler(0, facing, 0), Time.deltaTime * 8);
            if (animator)
            {
                animator.SetFloat("Speed", input.magnitude);
                animator.SetFloat("DirY", input.y);
                animator.SetBool("Dead", false);
            }
        }

        void FixedUpdate()
        {
            Vector3 desired = new Vector3(input.x, input.y, 0) * (boosting ? boostSpeed : swimSpeed);
            body.velocity = Vector3.MoveTowards(body.velocity, desired, 28f * Time.fixedDeltaTime);
        }

        void LateUpdate()
        {
            if (!followCamera) return;
            float halfWidth = followCamera.orthographicSize * followCamera.aspect;
            float cx = Mathf.Clamp(transform.position.x + input.x * 2, -54 + halfWidth, 54 - halfWidth);
            float cy = Mathf.Clamp(transform.position.y + input.y, -86 + followCamera.orthographicSize, 4 - followCamera.orthographicSize);
            followCamera.transform.position = Vector3.SmoothDamp(followCamera.transform.position, new Vector3(cx, cy, -45), ref cameraVelocity, .35f);
            followCamera.backgroundColor = Color.Lerp(new Color(.045f,.29f,.36f), new Color(.018f,.095f,.18f), Mathf.Clamp01(-transform.position.y / 85));
        }

        public void ResetToEntry()
        {
            body.position = spawn;
            body.velocity = Vector3.zero;
            cameraVelocity = Vector3.zero;
        }

        void OnGUI()
        {
            if (titleStyle == null)
            {
                var font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "Arial" }, 20);
                titleStyle = new GUIStyle(GUI.skin.label) { font = font, fontSize = 24, fontStyle = FontStyle.Bold };
                bodyStyle = new GUIStyle(GUI.skin.label) { font = font, fontSize = 15 };
                depthStyle = new GUIStyle(titleStyle) { fontSize = 32, alignment = TextAnchor.MiddleRight };
                titleStyle.normal.textColor = new Color(.7f,.95f,1);
                bodyStyle.normal.textColor = new Color(.8f,.9f,.94f);
                depthStyle.normal.textColor = Color.white;
            }
            var oldMatrix = GUI.matrix;
            float scale = Screen.height / 900f;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            float width = Screen.width / scale;
            GUI.color = new Color(.015f,.06f,.1f,.8f);
            GUI.DrawTexture(new Rect(24,24,370,105), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(width-210,24,186,85), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(42,35,340,38), "鲸落  /  蓝洞探索", titleStyle);
            string zone = transform.position.y > -28 ? "01   浅海珊瑚礁" : transform.position.y > -55 ? "02   海草峡谷" : "03   深水沉船湾";
            GUI.Label(new Rect(43,81,335,28), zone, bodyStyle);
            GUI.Label(new Rect(width-195,34,150,44), Mathf.Max(0,-transform.position.y).ToString("00.0") + " m", depthStyle);
            GUI.Label(new Rect(width-187,77,160,26), "当前下潜深度", bodyStyle);
            GUI.Label(new Rect(32,852,850,28), "WASD / 方向键  游动     Shift  加速     R  返回入口     ·     自由探索，无氧气限制", bodyStyle);
            GUI.matrix = oldMatrix;
        }
    }
}
