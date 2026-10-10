using Crest;
using UnityEngine;

namespace Fishing
{
    /// <summary>
    /// 木筏 ↔ 海面交互 · 第一阶段「受控浮力」。
    ///
    /// 做法：在木筏底部布置前/后/左/右/中心 5 个采样点，每帧从 Crest 批量查询这些位置的
    /// 海面高度，用最小二乘拟合出一个平面，取出「整体升降 + 前后俯仰 + 左右倾斜」，
    /// 再以半衰期平滑 + 角度限幅后叠加到木筏的静止姿态上。
    ///
    /// 为什么是「叠加到静止姿态」而不是接管木筏的位置：
    /// 场景里木筏的位置/朝向是手工调好的取景结果（root 上有 -20.5° 的展示俯仰）。
    /// 浮力只产生增量，木筏永远围绕手工零点动，改参数随时能回到原样。
    ///
    /// 为什么采样用「静止姿态的采样点偏移」（SampleInRestFrame）：
    /// 若用船体当前姿态的采样点，会出现「倾斜 → 采样点跟着倾斜 → 测出更大倾斜」的反馈回路，
    /// 在窄船体上很容易自激。第一阶段先切断这个回路，保证「自然起伏 + 轻微倾斜、绝不翻覆」；
    /// 进入真正的搜打撤水域、换成完整刚体浮力时再关掉这个开关。
    ///
    /// 采样点与海面锚点的关系：木筏在 layer 30 的隔离世界里（x≈1000），海面在另一个世界
    /// （x≈2000），两者物理上不在一起。OceanProbe 就是这个映射锚点 —— 它摆在真实海面上，
    /// 采样点的 XZ 偏移换算到锚点坐标系里去查询，于是「木筏怎么动」由「锚点处的海浪」决定。
    /// </summary>
    [AddComponentMenu("鲸落/木筏浮力（受控）")]
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-50)]
    public sealed class RaftBuoyancy : MonoBehaviour
    {
        [Header("引用")]
        [Tooltip("承载浮力位姿的木筏根。甲板与所有固定装置都应挂在它下面，从而自动跟随。留空则用自身。")]
        public Transform RaftRoot;

        [Tooltip("海面采样锚点：摆在真实海面上（Crest 海面的位置）。木筏与海面在隔离空间里，靠它建立映射。")]
        public Transform OceanProbe;

        [Tooltip("浮力采样点：前 / 后 / 左 / 右 / 中心，共 5 个，摆在木筏底部。")]
        public Transform[] SamplePoints = new Transform[0];

        [Tooltip("留空则用 OceanRenderer.Instance。")]
        public OceanRenderer Ocean;

        [Header("采样")]
        [Tooltip("对象最小空间尺度（米），一般填船长。值越大越忽略细小波长，运动越稳。")]
        public float MinSpatialLength = 12.6f;

        [Tooltip("勾选=按静止姿态的采样点偏移查询（无反馈，推荐）；取消=按船体当前姿态查询（更物理，需配合阻尼）。")]
        public bool SampleInRestFrame = true;

        [Tooltip("采样点相对木筏原点的水平偏移超过它就不采信（防止采样点摆歪了导致木筏乱动）。0=不检查。")]
        public float MaxProbeDistance = 90f;

        [Header("响应（受控浮力）")]
        [Tooltip("升沉跟随半衰期（秒）。越大越迟钝、越稳。")]
        public float HeaveHalfLife = 0.35f;

        [Tooltip("倾斜跟随半衰期（秒）。窄船体的横滚估计噪声大，这里一般给得比升沉更大。")]
        public float TiltHalfLife = 0.55f;

        [Tooltip("俯仰限幅（度）。受控浮力靠它保证不翻覆。")]
        public float MaxPitchDeg = 5f;

        [Tooltip("横滚限幅（度）。")]
        public float MaxRollDeg = 6f;

        [Tooltip("升沉偏置（米）：正值把木筏整体抬高。用来微调吃水。")]
        public float HeaveBias = 0f;

        [Tooltip("俯仰方向符号。装好后若方向相反，把这里改成 -1。")]
        public float PitchSign = 1f;

        [Tooltip("横滚方向符号。装好后若方向相反，把这里改成 -1。")]
        public float RollSign = 1f;

        [Header("诊断")]
        public bool DrawGizmos = true;
        public bool LogDiagnostics = false;

        // ---- 只读输出 ----
        /// <summary>当前是否采样成功（失败时木筏平滑回到静止姿态）。</summary>
        public bool Sampling { get; private set; }
        /// <summary>当前升沉（米）。</summary>
        public float Heave { get; private set; }
        /// <summary>当前俯仰（度）。</summary>
        public float PitchDeg { get; private set; }
        /// <summary>当前横滚（度）。</summary>
        public float RollDeg { get; private set; }
        /// <summary>最近一次拟合残差均方根（米）。明显偏大说明 5 点不在一个平面上，浪太碎。</summary>
        public float ResidualRms { get; private set; }
        /// <summary>参与采样的点数。</summary>
        public int SampleCount { get { return _count; } }

        private Vector3 _restLocalPos;
        private Quaternion _restLocalRot;
        private Vector3 _restWorldPos;
        private Transform _parent;

        private Vector2[] _offsets;      // 采样点相对木筏原点的水平偏移（世界 XZ 轴）
        private Vector3[] _query;
        private float[] _heights;
        private Vector3[] _normals;
        private int _count;
        private int _hash;

        private float _targetHeave, _targetPitch, _targetRoll;
        private bool _warnedNoOcean, _warnedNoProvider, _warnedNoQuery, _warnedBadLayout, _loggedFirstSample;
        private int _failFrames;
        private float _nextDiagnostic;

        private void Awake()
        {
            CaptureRestPose();
            _hash = GetHashCode();
            BuildSampleLayout();
        }

        private bool _restCaptured;

        private void CaptureRestPose()
        {
            if (!RaftRoot) RaftRoot = transform;
            _parent = RaftRoot.parent;
            _restLocalPos = RaftRoot.localPosition;
            _restLocalRot = RaftRoot.localRotation;
            _restWorldPos = RaftRoot.position;
            _restCaptured = true;
        }

        /// <summary>
        /// 采集采样点布局。偏移用「世界 XZ 轴上的水平差」表示，而不是木筏本地轴：
        /// 木筏静止姿态本身带 -20.5° 展示俯仰，用本地轴会把 X 基线缩短约 6%，
        /// 让测出来的俯仰虚高；用世界轴则与 OceanProbe（无旋转）的坐标系一致。
        /// </summary>
        public void BuildSampleLayout()
        {
            _count = SamplePoints != null ? SamplePoints.Length : 0;
            int valid = 0;
            if (_count > 0)
            {
                _offsets = new Vector2[_count];
                for (int i = 0; i < _count; i++)
                {
                    var sp = SamplePoints[i];
                    if (!sp) continue;
                    Vector3 d = sp.position - _restWorldPos;
                    _offsets[valid] = new Vector2(d.x, d.z);
                    valid++;
                }
            }
            _count = valid;

            if (_count < 3)
            {
                _offsets = new Vector2[0];
                _query = new Vector3[0];
                _heights = new float[0];
                _normals = new Vector3[0];
                if (!_warnedBadLayout)
                {
                    _warnedBadLayout = true;
                    Debug.LogWarning("[鲸落] RaftBuoyancy 至少需要 3 个采样点才能定出平面，当前 " + _count + " 个。" +
                                     "请在 " + name + " 上补齐前/后/左/右/中心 5 个采样点。", this);
                }
                return;
            }

            _query = new Vector3[_count];
            _heights = new float[_count];
            _normals = new Vector3[_count];
        }

        private void LateUpdate()
        {
            // 只在运行时生效：编辑模式里 Crest 的 LOD 数据不一定就绪，
            // 而且这也避免重蹈 OceanChunkRenderer 在编辑模式下刷异常的覆辙。
            if (!Application.isPlaying) return;

            float dt = Time.deltaTime;
            if (dt <= 0f) dt = Time.unscaledDeltaTime;
            if (dt <= 0f) return;

            _targetHeave = 0f;
            _targetPitch = 0f;
            _targetRoll = 0f;
            Sampling = false;
            ResidualRms = 0f;

            if (_count >= 3 && TrySample(out RaftWavePose pose))
            {
                float sea = _seaLevel;
                _targetHeave = pose.Heave - sea - HeaveBias;
                _targetPitch = RaftBuoyancySolver.ClampSymmetric(pose.PitchDeg * PitchSign, MaxPitchDeg);
                _targetRoll = RaftBuoyancySolver.ClampSymmetric(pose.RollDeg * RollSign, MaxRollDeg);
                ResidualRms = pose.ResidualRms;
                Sampling = true;
                _failFrames = 0;

                if (!_loggedFirstSample)
                {
                    _loggedFirstSample = true;
                    Debug.Log(string.Format(
                        "[鲸落] 木筏浮力已接上海面：{0} 个采样点，水面高度 {1:0.000} m，" +
                        "升沉 {2:0.000} m，俯仰 {3:0.00}°，横滚 {4:0.00}°，拟合残差 {5:0.000} m",
                        _count, pose.MeanHeight, _targetHeave, _targetPitch, _targetRoll, pose.ResidualRms), this);
                }
            }
            else
            {
                _failFrames++;
            }

            Heave = RaftBuoyancySolver.SmoothTowards(Heave, _targetHeave, HeaveHalfLife, dt);
            PitchDeg = RaftBuoyancySolver.SmoothTowards(PitchDeg, _targetPitch, TiltHalfLife, dt);
            RollDeg = RaftBuoyancySolver.SmoothTowards(RollDeg, _targetRoll, TiltHalfLife, dt);

            ApplyPose();

            if (LogDiagnostics && Time.time >= _nextDiagnostic)
            {
                _nextDiagnostic = Time.time + 1f;
                Debug.Log(string.Format(
                    "[鲸落] 木筏浮力 {0}：升沉 {1:0.000} m（目标 {2:0.000}）俯仰 {3:0.00}° 横滚 {4:0.00}° 残差 {5:0.000} m 采样 {6} 点",
                    Sampling ? "正常" : "等待数据", Heave, _targetHeave, PitchDeg, RollDeg, ResidualRms, _count), this);
            }
        }

        private float _seaLevel;

        private bool TrySample(out RaftWavePose pose)
        {
            pose = default(RaftWavePose);

            OceanRenderer ocean = Ocean ? Ocean : OceanRenderer.Instance;
            if (!ocean)
            {
                WarnOnce(ref _warnedNoOcean, "场景里找不到 OceanRenderer，木筏浮力暂停（保持静止姿态）。");
                return false;
            }
            // 不直接用 ocean.SeaLevel：Crest 的实现是 `Root.position.y`，而 Root 是非序列化的
            // 运行时字段，海面还没初始化完（或初始化失败）时是 null，直接读会每帧抛空引用。
            // Root 挂在海面物体下且本地归零，所以海面物体自身的 y 就是等价值，且永远可读。
            Transform seaRoot = ocean.Root;
            _seaLevel = seaRoot ? seaRoot.position.y : ocean.transform.position.y;

            ICollProvider provider = ocean.CollisionProvider;
            if (provider == null)
            {
                WarnOnce(ref _warnedNoProvider, "Crest 海面还没建好碰撞/查询通道（CollisionProvider 为空），" +
                                                "木筏浮力等待中。若一直如此，请检查海面的 Animated Waves 设置里 Collision Source 不是 None。");
                return false;
            }

            Transform probe = OceanProbe ? OceanProbe : ocean.transform;
            Vector3 origin = probe.position;
            float maxDist = MaxProbeDistance;
            float maxDistSq = maxDist > 0f ? maxDist * maxDist : 0f;

            if (SampleInRestFrame)
            {
                // 静止姿态采样：偏移本来就是「静止姿态下的世界 XZ 偏移」，直接用，无反馈
                for (int i = 0; i < _count; i++)
                    _query[i] = origin + new Vector3(_offsets[i].x, 0f, _offsets[i].y);
            }
            else
            {
                // 船体当前姿态采样：把同一个物理点随木筏当前旋转一起搬过去。
                // 偏移存的是静止姿态下的世界 XZ，所以先转回木筏静止时的局部方向，
                // 再用当前旋转转出去 —— 不能直接当成本地向量用（静止姿态本身带展示俯仰）。
                Quaternion restWorldRot = _parent ? _parent.rotation * _restLocalRot : _restLocalRot;
                Quaternion toLocal = Quaternion.Inverse(restWorldRot);
                Quaternion nowRot = RaftRoot.rotation;
                for (int i = 0; i < _count; i++)
                {
                    Vector3 localDir = toLocal * new Vector3(_offsets[i].x, 0f, _offsets[i].y);
                    _query[i] = origin + nowRot * localDir;
                }
            }

            if (maxDistSq > 0f)
            {
                for (int i = 0; i < _count; i++)
                {
                    Vector3 d = _query[i] - origin;
                    if (d.x * d.x + d.z * d.z > maxDistSq)
                    {
                        WarnOnce(ref _warnedNoQuery, "采样点离海面锚点太远（超过 MaxProbeDistance=" + maxDist +
                                                     " m），木筏浮力暂停。请把海面采样锚点摆到真实海面上。");
                        return false;
                    }
                }
            }

            int status = provider.Query(_hash, MinSpatialLength, _query, _heights, _normals, null);
            if (!provider.RetrieveSucceeded(status))
            {
                WarnOnce(ref _warnedNoQuery, "Crest 海面高度查询还没返回结果（通常是海面刚建立、或 Collision Source 为 None）。" +
                                             "木筏浮力会继续重试。");
                return false;
            }

            return RaftBuoyancySolver.Solve(_heights, _offsets, _count, out pose);
        }

        /// <summary>
        /// 只在连续失败足够多帧后才告警一次。
        /// Crest 的查询是异步的：本帧投递、下一帧才能取回，所以头一两帧取不到结果是正常的，
        /// 不该在这时候吓唬人。真的接不上海面（碰撞源为 None 之类）才会超过这个阈值。
        /// </summary>
        private void WarnOnce(ref bool flag, string message)
        {
            if (flag) return;
            if (_failFrames < 20) return;
            flag = true;
            Debug.LogWarning("[鲸落] " + message, this);
        }

        /// <summary>
        /// 把姿态叠到静止姿态上。
        /// 升沉沿**世界**向上的方向平移（父节点可能带展示俯仰，所以要把世界方向换算到父空间）；
        /// 俯仰/横滚绕木筏自身轴旋转（restRot * 增量）。
        /// 应用约定：Quaternion.Euler(-横滚, 0, 俯仰) —— 已在 RaftRuleCheck 里把符号钉死。
        /// </summary>
        private void ApplyPose()
        {
            Vector3 worldDelta = Vector3.up * Heave;
            Vector3 localDelta = _parent ? _parent.InverseTransformVector(worldDelta) : worldDelta;
            RaftRoot.localPosition = _restLocalPos + localDelta;
            RaftRoot.localRotation = _restLocalRot * Quaternion.Euler(-RollDeg, 0f, PitchDeg);
        }

        /// <summary>甲板上某点在「当前姿态」下的世界坐标。local 是相对木筏根的本地坐标。</summary>
        public Vector3 DeckPointNow(Vector3 local)
        {
            return RaftRoot.TransformPoint(local);
        }

        /// <summary>甲板上同一点在「静止姿态」下的世界坐标（用于求视觉增量）。</summary>
        public Vector3 DeckPointRest(Vector3 local)
        {
            Vector3 p = _restLocalRot * local + _restLocalPos;
            return _parent ? _parent.TransformPoint(p) : p;
        }

        /// <summary>木筏静止时的世界原点。</summary>
        public Vector3 RestWorldPosition { get { return _restWorldPos; } }

        /// <summary>把外部改动过的静止姿态重新记为基准（换场景参数、或编辑器预览收尾时调用）。</summary>
        public void RecaptureRestPose()
        {
            CaptureRestPose();
            Heave = PitchDeg = RollDeg = 0f;
            BuildSampleLayout();
        }

        /// <summary>
        /// 编辑器预览用：跳过海面采样，直接摆一个位姿。
        /// 只在 RestCaptured 之后才允许动 —— 否则会把木筏搬到世界原点。
        /// </summary>
        public void ApplyPoseForPreview(float heave, float pitchDeg, float rollDeg)
        {
            if (!_restCaptured) CaptureRestPose();
            Heave = heave;
            PitchDeg = pitchDeg;
            RollDeg = rollDeg;
            ApplyPose();
        }

        /// <summary>恢复到静止姿态。</summary>
        public void ResetToRestPose() { ApplyPoseForPreview(0f, 0f, 0f); }

        private void OnDrawGizmosSelected()
        {
            if (!DrawGizmos || SamplePoints == null) return;
            Transform probe = OceanProbe;
            Vector3 origin = probe ? probe.position : Application.isPlaying && OceanRenderer.Instance
                ? OceanRenderer.Instance.transform.position
                : transform.position;

            Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.9f);
            for (int i = 0; i < SamplePoints.Length; i++)
            {
                var sp = SamplePoints[i];
                if (!sp) continue;
                Vector3 offset = sp.position - (RaftRoot ? RaftRoot.position : transform.position);
                offset.y = 0f;
                Vector3 q = origin + offset;
                Gizmos.DrawWireSphere(q, 0.35f);
                Gizmos.DrawLine(sp.position, q);
            }
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.9f);
            Gizmos.DrawWireSphere(origin, 0.6f);
        }
    }
}
