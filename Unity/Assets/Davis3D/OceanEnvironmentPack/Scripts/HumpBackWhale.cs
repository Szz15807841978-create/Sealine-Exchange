using UnityEngine;

public class HumpBackWhale : MonoBehaviour
{

    private BezierSpline Spline;

    private Animator WhaleAnimator;

    private Transform TransformToMove;

    [SerializeField, Header("Movement")]
    private Transform TargetT;

    [SerializeField]
    private float Duration = 10.0f;

    [SerializeField]
    private bool LookForward;

    [SerializeField]
    private SplineWalkerMode MovementMode;

    private float Progress;
    private bool GoingForward = true;

    [SerializeField, Header("Animation")]
    private float Mouth_MinDistance = 10.0f;

    [SerializeField]
    private float Mouth_DistanceToOpen = 20.0f;

    [SerializeField, Range(0.1f, 1.0f)]
    private float Mouth_BlendFactor = 0.5f;

    [SerializeField, Header("Setup")]
    private float Scale = 1.0f;

    [SerializeField]
    private float Scale_LengthRatio = 1.0f;

    [SerializeField]
    private bool DynamicSpeed = true,
        Mouth_OpensNearPlayer = true;

    [SerializeField, Header("Non-Dynamic Speed")]
    private float NonDynamic_AnimPlayRate = 0.9f;

    [SerializeField]
    private float NonDynamic_MovementSpeed = 4.0f,
        NonDynamic_AnimIntensity = 0.6f;

    [SerializeField, Header("Dynamic Timing")]
    private float DynamicAnim_FastLength = 3.0f;

    [SerializeField]
    private float DynamicAnim_TimeRandomness = 1.0f,
        DynamicAnim_SlowLength = 6.0f;

    [SerializeField, Header("Dynamic Movement")]
    private float DynamicSpeed_Acceleration = 0.15f;

    [SerializeField]
    private float DynamicSpeed_Deceleration = 0.05f,
        MovementSpeed_Min = 1.2f,
        MovementSpeed_Max = 8.0f,
        Roll_RotationIntensity = 2.0f;

    [SerializeField, Header("Movement Smoothing")]
    private float Roll_SmoothingSpeed = 0.5f;

    [SerializeField]
    private float Rotation_SmoothingSpeed = 5.0f,
        Movement_SmoothingSpeed = 4.0f;

    private AudioSource Audio_Source;

    [SerializeField, Header("Audio")]
    private float SpatialBlend = 1.0f;

    [SerializeField]
    private AudioClip[] Sounds;

    [SerializeField]
    private Vector2 RangeToPlaySound = new Vector2(3.0f, 10.0f);

    #region UNITY_CALLBACKS

    /// <summary>
    /// Start is called before the first frame update
    /// </summary>
    private void Start()
    {
        // Save whale transform
        TransformToMove = transform.GetChild(1);
        TransformToMove.localRotation = Quaternion.Euler(Vector3.zero);
        TransformToMove.localPosition = Vector3.zero;

        // Audio component
        Audio_Source = TransformToMove.gameObject.AddComponent<AudioSource>();
        Audio_Source.playOnAwake = false;
        Audio_Source.spatialBlend = SpatialBlend;
        PlayRandomClip();

        // Add spline component
        Spline = GetComponentInChildren<BezierSpline>();
        WhaleAnimator = GetComponentInChildren<Animator>();
    }

    /// <summary>
    /// Update is called once per frame
    /// </summary>
    private void Update()
    {
        UpdateMovement();
        UpdateMouthAnimation();
    }

    /// <summary>
    /// 
    /// </summary>
    private void OnValidate()
    {
        transform.GetChild(1).localScale = new Vector3(Scale, Scale, Scale * Scale_LengthRatio);
    }

    /// <summary>
    /// 
    /// </summary>
    private void OnDestroy()
    {
        CancelInvoke();
    }

    #endregion

    #region MOVEMENT

    private void UpdateMovement()
    {
        if (GoingForward)
        {
            Progress += Time.deltaTime / Duration;
            if (Progress > 1f)
            {
                if (MovementMode == SplineWalkerMode.Once)
                {
                    Progress = 1f;
                }
                else if (MovementMode == SplineWalkerMode.Loop)
                {
                    Progress -= 1f;
                }
                else
                {
                    Progress = 2f - Progress;
                    GoingForward = false;
                }
            }
        }
        else
        {
            Progress -= Time.deltaTime / Duration;
            if (Progress < 0f)
            {
                Progress = -Progress;
                GoingForward = true;
            }
        }

        Vector3 position = Spline.GetPoint(Progress);
        TransformToMove.localPosition = position;
        if (LookForward)
        {
            TransformToMove.LookAt(position + Spline.GetDirection(Progress));
        }
    }

    #endregion

    #region ANIMATION

    /// <summary>
    /// 
    /// </summary>
    private void UpdateMouthAnimation()
    {
        if (TargetT && Mouth_OpensNearPlayer)
        {
            float dst = Vector3.Distance(TransformToMove.position, TargetT.position);
            dst = 1.0f - Mathf.InverseLerp(Mouth_MinDistance, Mouth_DistanceToOpen, dst);
            WhaleAnimator.SetFloat("Mouth", dst * Mouth_BlendFactor);
        }
    }

    /// <summary>
    /// 
    /// </summary>
    private void AnimateLookAtFrontVariable()
    {

    }

    /// <summary>
    /// 
    /// </summary>
    private void AnimateLookAtFront()
    {

    }

    /// <summary>
    /// 
    /// </summary>
    private void AnimateTurningBlendspace()
    {

    }

    /// <summary>
    /// 
    /// </summary>
    private void AnimateDistanceVariable()
    {

    }

    /// <summary>
    /// 
    /// </summary>
    private void SetMeshToDistanceAlongSpline()
    {

    }

    #endregion

    #region CONSTRUCTION

    /// <summary>
    /// 
    /// </summary>
    private void ToggleLookAtSpheres()
    {

    }

    /// <summary>
    /// 
    /// </summary>
    private void SetMeshesInitialTransform()
    {

    }

    /// <summary>
    /// 
    /// </summary>
    private void SetLookAtFrontsInitialPosition()
    {

    }

    #endregion

    /// <summary>
    /// 
    /// </summary>
    private void SetInitialSpeed()
    {

    }

    /// <summary>
    /// 
    /// </summary>
    private void PlayDynamicAnimations()
    {

    }

    /// <summary>
    /// 
    /// </summary>
    private void AnimateMouthWhenNearPlayer()
    {

    }

    /// <summary>
    /// 
    /// </summary>
    private void InterpolateDynamicSpeedVariable()
    {

    }

    /// <summary>
    /// 
    /// </summary>
    private void AnimateMouthIntermittenly()
    {

    }

    #region MACROS

    /// <summary>
    /// 
    /// </summary>
    private void DynamicSpeedSetFast()
    {

    }

    /// <summary>
    /// 
    /// </summary>
    private void DynamicSpeedSetSlow()
    {

    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="TurningAngle"></param>
    /// <param name="TurningBlendSpaceAngle"></param>
    private void CalculateTurningAngle(ref float TurningAngle, ref float TurningBlendSpaceAngle)
    {

    }

    /// <summary>
    /// 
    /// </summary>
    private void DynamicSpeedTimer()
    {

    }

    /// <summary>
    /// 
    /// </summary>
    private void WhaleCalls()
    {

    }

    #endregion

    #region SOUND

    /// <summary>
    /// 
    /// </summary>
    private void PlayRandomClip()
    {
        Audio_Source.clip = Sounds[Random.Range(0, Sounds.Length)];
        Audio_Source.Play();

        Invoke("PlayRandomClip", Audio_Source.clip.length + Random.Range(RangeToPlaySound.x, RangeToPlaySound.y));
    }

    #endregion

}
