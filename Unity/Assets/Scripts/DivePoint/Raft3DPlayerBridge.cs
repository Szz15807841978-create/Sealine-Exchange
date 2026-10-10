using UnityEngine;

namespace Fishing
{
    /// <summary>
    /// 把现有 2D 玩法坐标映射到木筏上的 3D 人物。
    /// DivePointController 继续负责输入、木箱距离、背包与 UI；这个组件只负责 3D 显示和动画。
    /// </summary>
    [AddComponentMenu("鲸落/木筏3D玩家桥接")]
    [DefaultExecutionOrder(200)]
    public sealed class Raft3DPlayerBridge : MonoBehaviour
    {
        public RaftDeckRider DeckRider;
        public Transform Visual;
        public Animator Animator;
        public GameObject ChestPanel;

        [Header("朝向")]
        public float FacingRightYaw = 90f;
        public float FacingLeftYaw = -90f;
        public float TurnSpeed = 720f;

        [Header("动画")]
        public string SpeedParameter = "Speed";
        public string OpenParameter = "Open";
        public string FishingParameter = "Fishing";
        public float MovingThreshold = 0.02f;
        public float AnimationSpeedScale = 0.45f;

        [Header("诊断/兜底")]
        [Tooltip("甲板跟随器尚未更新时使用的 UI 像素/甲板单位标定。")]
        public float FallbackPixelsPerUnit = 104.6f;

        public float ResolvedDeckX { get; private set; }
        public bool UsingFallbackCalibration { get; private set; }

        private float _lastDeckX;
        private bool _ready;
        private bool _chestWasOpen;
        private int _speedHash;
        private int _openHash;
        private int _fishingHash;
        private bool _hasFishingParameter;
        private Quaternion _targetFacing;

        private void Awake()
        {
            _speedHash = Animator.StringToHash(SpeedParameter);
            _openHash = Animator.StringToHash(OpenParameter);
            _fishingHash = Animator.StringToHash(FishingParameter);
            if (Animator)
            {
                foreach (var parameter in Animator.parameters)
                    if (parameter.type == AnimatorControllerParameterType.Bool && parameter.nameHash == _fishingHash)
                        _hasFishingParameter = true;
            }
            _chestWasOpen = ChestPanel && ChestPanel.activeSelf;
        }

        private void LateUpdate()
        {
            if (!Application.isPlaying || !DeckRider) return;

            float deckX;
            if (!TryResolveDeckX(out deckX)) return;
            ResolvedDeckX = deckX;
            transform.localPosition = new Vector3(deckX, DeckRider.DeckSurfaceLocalY, DeckRider.DeckZForRider);

            if (!_ready)
            {
                _ready = true;
                _lastDeckX = deckX;
                _targetFacing = Visual ? Visual.localRotation : Quaternion.identity;
            }

            float dt = Mathf.Max(Time.deltaTime, 0.0001f);
            float velocity = (deckX - _lastDeckX) / dt;
            _lastDeckX = deckX;

            if (Mathf.Abs(velocity) > MovingThreshold)
                _targetFacing = Quaternion.Euler(0f, velocity > 0f ? FacingRightYaw : FacingLeftYaw, 0f);

            if (Visual)
                Visual.localRotation = Quaternion.RotateTowards(
                    Visual.localRotation, _targetFacing, TurnSpeed * dt);

            if (Animator)
            {
                Animator.SetFloat(_speedHash, Mathf.Abs(velocity) * AnimationSpeedScale);
                bool chestOpen = ChestPanel && ChestPanel.activeSelf;
                if (chestOpen && !_chestWasOpen) Animator.SetTrigger(_openHash);
                if (_hasFishingParameter) Animator.SetBool(_fishingHash, FishingPose);
                _chestWasOpen = chestOpen;
            }
        }

        public bool FishingPose { get; private set; }

        public void SetFishingPose(bool active)
        {
            FishingPose = active;
            if (!Animator) return;
            if (_hasFishingParameter)
            {
                Animator.speed = 1f;
                Animator.SetBool(_fishingHash, active);
            }
            else if (active)
            {
                // 旧动画控制器尚未由安装器升级时也要有清晰姿势：
                // 停在俯身伸手动作中段，下一次编辑器刷新后会自动换成 Loot 循环。
                Animator.Play("OpenChest", 0, 0.55f);
                Animator.Update(0f);
                Animator.speed = 0f;
            }
            else
            {
                Animator.speed = 1f;
                Animator.Play("Idle", 0, 0f);
            }
        }

        private bool TryResolveDeckX(out float deckX)
        {
            deckX = 0f;
            UsingFallbackCalibration = false;

            if (!DeckRider.Rider) return false;
            float pixelsPerUnit = DeckRider.ResolvedPixelsPerUnit;
            if (pixelsPerUnit <= 0.0001f)
            {
                pixelsPerUnit = DeckRider.PixelsPerUnit > 0f
                    ? DeckRider.PixelsPerUnit
                    : FallbackPixelsPerUnit;
                UsingFallbackCalibration = true;
            }
            if (pixelsPerUnit <= 0.0001f) return false;

            float landmarkUiX = DeckRider.Landmark
                ? DeckRider.Landmark.anchoredPosition.x
                : 0f;
            float landmarkDeckX = 0f;
            if (DeckRider.Landmark3D && DeckRider.Buoyancy && DeckRider.Buoyancy.RaftRoot)
            {
                landmarkDeckX = DeckRider.Buoyancy.RaftRoot
                    .InverseTransformPoint(DeckRider.Landmark3D.position).x;
            }

            deckX = landmarkDeckX
                + (DeckRider.Rider.anchoredPosition.x - landmarkUiX)
                / pixelsPerUnit * DeckRider.DeckUiSign;
            return true;
        }
    }
}
