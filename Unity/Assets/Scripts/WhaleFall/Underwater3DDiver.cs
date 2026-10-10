using System.Linq;
using UnityEngine;

namespace WhaleFall
{
    /// <summary>
    /// Camera-relative, free 3D underwater movement for the first playable ocean prototype.
    /// The rigidbody owns collision while the diver visual turns toward the swim direction.
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
    public sealed class Underwater3DDiver : MonoBehaviour
    {
        [Header("References")]
        public Camera followCamera;
        public Transform visual;
        public Animator animator;

        [Header("Swimming")]
        public Vector3 spawn = new Vector3(0f, 2f, 18f);
        public float swimSpeed = 4.5f;
        public float boostSpeed = 7.5f;
        public float acceleration = 14f;
        public float turnSpeed = 8f;
        public Vector2 xLimits = new Vector2(-20f, 20f);
        public Vector2 yLimits = new Vector2(-33f, 7.5f);
        public Vector2 zLimits = new Vector2(-8f, 78f);

        [Header("Third-person camera")]
        public float cameraDistance = 7.5f;
        public float minCameraDistance = 3.5f;
        public float maxCameraDistance = 12f;
        public float cameraHeight = 0.7f;
        public float cameraSensitivity = 2.2f;
        public float cameraSmoothTime = 0.12f;
        public float cameraCollisionRadius = 0.25f;
        public float cameraYaw;
        public float cameraPitch = 12f;

        [Header("First-person camera · V 切换 / Esc 释放鼠标")]
        public bool firstPerson;
        public float firstPersonEyeHeight=.35f;
        public float firstPersonFov=76;
        public bool IsFirstPerson=>firstPerson;
        public bool IsAiming=>!inputSuppressed&&(firstPerson||Input.GetMouseButton(1));
        Renderer[] bodyRenderers;
        bool[] originalRenderingOff;
        bool freeFirstPersonMouse;
        float thirdPersonFov;

        [Header("Automated verification")]
        public bool testDrive;
        public Vector3 testWorldMove;
        public bool testBoost;

        [Header("Expedition integration")]
        public bool allowReset = true;
        public bool showPrototypeHud = true;
        [System.NonSerialized] public bool inputSuppressed;
        Vector3 impactVelocity;
        float impactUntil;

        Rigidbody body;
        Vector3 swimDirection;
        Vector3 cameraVelocity;
        bool boosting;
        bool orbiting;
        GUIStyle titleStyle;
        GUIStyle textStyle;
        Font uiFont;

        public Vector3 SwimDirection { get { return swimDirection; } }
        public float Speed { get { return body ? body.velocity.magnitude : 0f; } }

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.useGravity = false;
            body.constraints = RigidbodyConstraints.FreezeRotation;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.drag = 2.2f;
            body.angularDrag = 8f;

            if (animator)
            {
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }

            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            thirdPersonFov=followCamera?followCamera.fieldOfView:62;
            bodyRenderers=visual?visual.GetComponentsInChildren<Renderer>(true):new Renderer[0];
            originalRenderingOff=bodyRenderers.Select(r=>r.forceRenderingOff).ToArray();
            ApplyViewMode();
        }

        void Start()
        {
            SnapCamera();
        }

        void Update()
        {
            if (inputSuppressed)
            {
                swimDirection=Vector3.zero;boosting=false;
                if(orbiting) {orbiting=false;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}
            }
            else if (!testDrive)
            {
                if(Input.GetKeyDown(KeyCode.V)) {firstPerson=!firstPerson;freeFirstPersonMouse=false;ApplyViewMode();SnapCamera();}
                UpdateCameraInput();
                ReadPlayerInput();
                if (allowReset && Input.GetKeyDown(KeyCode.R)) ResetPosition();
                if (Input.GetKeyDown(KeyCode.C)) RecenterCamera();
            }
            else
            {
                swimDirection = Vector3.ClampMagnitude(testWorldMove, 1f);
                boosting = testBoost;
            }

            UpdateVisual();
        }

        void ReadPlayerInput()
        {
            float horizontal = Input.GetAxisRaw("Horizontal");
            float forwardInput = Input.GetAxisRaw("Vertical");
            float vertical = 0f;
            if (Input.GetKey(KeyCode.Space)) vertical += 1f;
            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) vertical -= 1f;

            Vector3 cameraForward = followCamera ? followCamera.transform.forward : transform.forward;
            Vector3 cameraRight = followCamera ? followCamera.transform.right : transform.right;
            swimDirection = Vector3.ClampMagnitude(
                cameraForward * forwardInput + cameraRight * horizontal + Vector3.up * vertical,
                1f);
            boosting = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        }

        void UpdateCameraInput()
        {
            if(firstPerson&&Input.GetKeyDown(KeyCode.Escape)) freeFirstPersonMouse=true;
            if(firstPerson&&Input.GetMouseButtonDown(1)) freeFirstPersonMouse=false;
            bool wantsOrbit = firstPerson?!freeFirstPersonMouse:Input.GetMouseButton(1);
            if (wantsOrbit != orbiting)
            {
                orbiting = wantsOrbit;
                Cursor.lockState = orbiting ? CursorLockMode.Locked : CursorLockMode.None;
                Cursor.visible = !orbiting;
            }

            if (orbiting)
            {
                cameraYaw += Input.GetAxis("Mouse X") * cameraSensitivity;
                cameraPitch -= Input.GetAxis("Mouse Y") * cameraSensitivity;
                cameraPitch = Mathf.Clamp(cameraPitch, firstPerson?-88f:-75f, firstPerson?88f:80f);
            }

            float scroll = Input.mouseScrollDelta.y;
            if (!firstPerson&&Mathf.Abs(scroll) > 0.01f)
                cameraDistance = Mathf.Clamp(cameraDistance - scroll * 0.8f, minCameraDistance, maxCameraDistance);
        }

        void FixedUpdate()
        {
            float targetSpeed = boosting ? boostSpeed : swimSpeed;
            Vector3 desiredVelocity = swimDirection * targetSpeed;
            if(Time.time<impactUntil) desiredVelocity+=impactVelocity;
            ApplyBounds(ref desiredVelocity);
            body.velocity = Vector3.MoveTowards(body.velocity, desiredVelocity, acceleration * Time.fixedDeltaTime);

            Vector3 clamped = ClampPosition(body.position);
            if ((clamped - body.position).sqrMagnitude > 0.0001f)
            {
                body.position = clamped;
                Vector3 velocity = body.velocity;
                if (clamped.x <= xLimits.x || clamped.x >= xLimits.y) velocity.x = 0f;
                if (clamped.y <= yLimits.x || clamped.y >= yLimits.y) velocity.y = 0f;
                if (clamped.z <= zLimits.x || clamped.z >= zLimits.y) velocity.z = 0f;
                body.velocity = velocity;
            }
        }

        void UpdateVisual()
        {
            if (visual && (swimDirection.sqrMagnitude > 0.01f||IsAiming))
            {
                Vector3 dir = IsAiming?Quaternion.Euler(cameraPitch,cameraYaw,0)*Vector3.forward:swimDirection.normalized;
                Vector3 up = Mathf.Abs(Vector3.Dot(dir, Vector3.up)) > 0.92f ? Vector3.forward : Vector3.up;
                Quaternion targetRotation = Quaternion.LookRotation(dir, up);
                visual.rotation = Quaternion.Slerp(visual.rotation, targetRotation, turnSpeed * Time.deltaTime);
            }

            if (animator)
            {
                animator.SetFloat("Speed", swimDirection.magnitude * (boosting ? 1.35f : 1f));
                animator.SetFloat("DirY", swimDirection.y);
                animator.SetBool("Dead", false);
            }
        }

        void LateUpdate()
        {
            if (!followCamera) return;

            if(firstPerson)
            {
                followCamera.transform.SetPositionAndRotation(transform.position+Vector3.up*firstPersonEyeHeight,Quaternion.Euler(cameraPitch,cameraYaw,0));
                return;
            }

            Vector3 target = transform.position + Vector3.up * cameraHeight;
            Quaternion orbit = Quaternion.Euler(cameraPitch, cameraYaw, 0f);
            Vector3 desired = target - orbit * Vector3.forward * cameraDistance;
            Vector3 direction = desired - target;
            float distance = direction.magnitude;
            if (distance > 0.01f)
            {
                RaycastHit[] hits = Physics.SphereCastAll(
                    target,
                    cameraCollisionRadius,
                    direction / distance,
                    distance,
                    ~0,
                    QueryTriggerInteraction.Ignore);
                RaycastHit? nearest = hits
                    .Where(h => h.collider && !h.collider.transform.IsChildOf(transform))
                    .OrderBy(h => h.distance)
                    .Select(h => (RaycastHit?)h)
                    .FirstOrDefault();
                if (nearest.HasValue)
                    desired = target + direction.normalized * Mathf.Max(0.6f, nearest.Value.distance - cameraCollisionRadius);
            }

            followCamera.transform.position = Vector3.SmoothDamp(
                followCamera.transform.position,
                desired,
                ref cameraVelocity,
                cameraSmoothTime);
            followCamera.transform.rotation = Quaternion.LookRotation(target - followCamera.transform.position, Vector3.up);
        }

        void ApplyBounds(ref Vector3 velocity)
        {
            Vector3 p = body.position;
            if ((p.x <= xLimits.x && velocity.x < 0f) || (p.x >= xLimits.y && velocity.x > 0f)) velocity.x = 0f;
            if ((p.y <= yLimits.x && velocity.y < 0f) || (p.y >= yLimits.y && velocity.y > 0f)) velocity.y = 0f;
            if ((p.z <= zLimits.x && velocity.z < 0f) || (p.z >= zLimits.y && velocity.z > 0f)) velocity.z = 0f;
        }

        Vector3 ClampPosition(Vector3 p)
        {
            p.x = Mathf.Clamp(p.x, xLimits.x, xLimits.y);
            p.y = Mathf.Clamp(p.y, yLimits.x, yLimits.y);
            p.z = Mathf.Clamp(p.z, zLimits.x, zLimits.y);
            return p;
        }

        public void RecenterCamera()
        {
            if (swimDirection.sqrMagnitude > 0.01f)
            {
                Vector3 flat = Vector3.ProjectOnPlane(swimDirection, Vector3.up);
                if (flat.sqrMagnitude > 0.01f)
                    cameraYaw = Quaternion.LookRotation(flat.normalized, Vector3.up).eulerAngles.y;
            }
            cameraPitch = 12f;
        }

        public void ResetPosition()
        {
            body.position = spawn;
            body.velocity = Vector3.zero;
            swimDirection = Vector3.zero;
            cameraVelocity = Vector3.zero;
            cameraYaw = 0f;
            cameraPitch = 12f;
            SnapCamera();
        }

        public void ApplyImpact(Vector3 velocity,float seconds)
        {impactVelocity=velocity;impactUntil=Time.time+seconds;}

        public void SnapCamera()
        {
            if (!followCamera) return;
            if(firstPerson) {followCamera.transform.SetPositionAndRotation(transform.position+Vector3.up*firstPersonEyeHeight,Quaternion.Euler(cameraPitch,cameraYaw,0));return;}
            Vector3 target = transform.position + Vector3.up * cameraHeight;
            Quaternion orbit = Quaternion.Euler(cameraPitch, cameraYaw, 0f);
            followCamera.transform.position = target - orbit * Vector3.forward * cameraDistance;
            followCamera.transform.rotation = Quaternion.LookRotation(target - followCamera.transform.position, Vector3.up);
        }

        void OnGUI()
        {
            if(!showPrototypeHud) return;
            if (titleStyle == null)
            {
                uiFont = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "Arial" }, 18);
                titleStyle = new GUIStyle(GUI.skin.label) { font = uiFont, fontSize = 24, fontStyle = FontStyle.Bold };
                titleStyle.normal.textColor = new Color(0.83f, 0.96f, 1f);
                textStyle = new GUIStyle(titleStyle) { fontSize = 16, fontStyle = FontStyle.Normal };
            }

            float scale = Mathf.Max(0.65f, Screen.height / 900f);
            Matrix4x4 old = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(Vector3.one * scale);
            GUI.color = new Color(0.01f, 0.055f, 0.09f, 0.76f);
            GUI.DrawTexture(new Rect(24f, 24f, 365f, 112f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(40f, 32f, 335f, 34f), "鲸落 · 3D 海底探索", titleStyle);
            GUI.Label(new Rect(40f, 69f, 335f, 25f), "深度  " + Mathf.Max(0f, yLimits.y - transform.position.y).ToString("0.0") + " m", textStyle);
            GUI.Label(new Rect(40f, 95f, 335f, 25f), "速度  " + Speed.ToString("0.0") + " m/s", textStyle);
            GUI.Label(new Rect(28f, 850f, 1300f, 30f), "WASD 游动 · 空格上浮 · Ctrl 下潜 · Shift 加速 · 右键转镜头 · V 第一/三人称 · Esc 释放鼠标 · 滚轮缩放", textStyle);
            GUI.matrix = old;
        }

        void OnDisable()
        {
            RestoreBody();
            if (orbiting)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                orbiting = false;
            }
        }

        void ApplyViewMode()
        {
            for(int i=0;i<bodyRenderers.Length;i++) if(bodyRenderers[i]) bodyRenderers[i].forceRenderingOff=firstPerson||originalRenderingOff[i];
            if(followCamera) followCamera.fieldOfView=firstPerson?firstPersonFov:thirdPersonFov;
            cameraVelocity=Vector3.zero;
        }
        void RestoreBody()
        {
            if(bodyRenderers==null) return;
            for(int i=0;i<bodyRenderers.Length;i++) if(bodyRenderers[i]) bodyRenderers[i].forceRenderingOff=originalRenderingOff[i];
            if(followCamera) followCamera.fieldOfView=thirdPersonFov;
        }

        void OnDestroy()
        {
            if (uiFont) Destroy(uiFont);
        }
    }
}
