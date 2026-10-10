using Crest;
using UnityEngine;

namespace Fishing
{
    /// <summary>
    /// 可掉落物品的浮力（第二阶段「完整刚体浮力」用）。
    ///
    /// 和木筏的区别：木筏走「受控浮力」（直接摆位姿、限幅、绝不翻覆），
    /// 可掉落物品走真实受力 —— 独立刚体 + 碰撞体 + 多个浮力点，
    /// 每个点按浸没深度给一个向上的力（作用在该点上，自然产生扭矩），
    /// 于是能滚动、滑落、漂浮、被浪推着走。
    ///
    /// 当前侧视玩法里还没有真正的可掉落物品，所以这个组件先备好不装；
    /// 进入搜打撤水域后，给物品预制体挂上它、配一个 Collider 和 Rigidbody 即可。
    ///
    /// 坐标系注意：如果物品和木筏一样待在 layer 30 的隔离世界里，
    /// 必须填 OceanProbe（和木筏共用同一个海面锚点），否则会去查询不存在的海面。
    /// 若物品本来就活在真实海面空间里，OceanProbe 留空即可。
    /// </summary>
    [AddComponentMenu("鲸落/可掉落物品浮力（刚体）")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class BuoyantObject : MonoBehaviour
    {
        [Header("采样")]
        [Tooltip("海面采样锚点。物品在隔离空间（layer 30）里时必须填；在真实海面空间里留空。")]
        public Transform OceanProbe;

        [Tooltip("留空则用 OceanRenderer.Instance。")]
        public OceanRenderer Ocean;

        [Tooltip("物体最小空间尺度（米）：越大越忽略细小波长。")]
        public float MinSpatialLength = 1.2f;

        [Tooltip("勾选=按包围盒自动生成 5 个浮力点（四角 + 中心，贴在底部）；取消=用下面的手填点。")]
        public bool AutoPointsFromBounds = true;

        [Tooltip("手填的浮力点（物体本地坐标）。AutoPointsFromBounds 勾选时忽略。")]
        public Vector3[] LocalPoints = new Vector3[0];

        [Header("浮力")]
        [Tooltip("单位浸没深度产生的浮力系数。越大浮得越猛。")]
        public float BuoyancyCoeff = 3f;

        [Tooltip("单个浮力点的最大加速度（m/s²），防止全浸没时弹飞。")]
        public float MaxBuoyancyAccel = 45f;

        [Tooltip("把物体整体抬高/降低，用来修吃水。")]
        public float RaiseObject = 0f;

        [Tooltip("物体的排水厚度（米）：浸没深度按它归一化。")]
        public float HullHeight = 0.6f;

        [Header("阻尼")]
        public float DragUp = 3f;
        public float DragRight = 2f;
        public float DragForward = 1f;
        public float DragAngular = 0.25f;

        [Tooltip("施加阻尼的高度偏移（物体本地 y）。负值表示略低于重心。")]
        public float ForceHeightOffset = -0.2f;

        [Header("受控（可选的“不翻滚”模式）")]
        [Tooltip("勾选=限制角速度并向水面法线回正，物品会漂浮但不会疯狂翻滚。")]
        public bool ControlledFloat = true;

        [Tooltip("受控模式下的最大角速度（度/秒）。")]
        public float MaxAngularSpeed = 120f;

        [Tooltip("受控模式下向水面法线回正的力矩强度。")]
        public float AlignTorque = 4f;

        [Header("诊断")]
        public bool DrawGizmos = true;

        private Rigidbody _rb;
        private Vector3[] _points;
        private Vector3[] _query;
        private float[] _heights;
        private Vector3[] _normals;
        private Vector3[] _vels;
        private int _hash;
        private bool _hasSurface;
        private int _failStreak;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _hash = GetHashCode();
            BuildPoints();
        }

        /// <summary>按包围盒自动摆 5 个浮力点：底部四角 + 底部中心。</summary>
        public void BuildPoints()
        {
            if (AutoPointsFromBounds)
            {
                var colliders = GetComponentsInChildren<Collider>();
                if (colliders.Length > 0)
                {
                    Bounds b = colliders[0].bounds;
                    for (int i = 1; i < colliders.Length; i++) b.Encapsulate(colliders[i].bounds);
                    float y = b.min.y - transform.position.y;
                    Vector3 c = transform.InverseTransformPoint(b.center);
                    Vector3 e = transform.InverseTransformVector(b.extents);
                    _points = new[]
                    {
                        new Vector3(c.x, y, c.z),
                        new Vector3(c.x + e.x, y, c.z + e.z),
                        new Vector3(c.x - e.x, y, c.z + e.z),
                        new Vector3(c.x + e.x, y, c.z - e.z),
                        new Vector3(c.x - e.x, y, c.z - e.z),
                    };
                }
                else
                {
                    _points = new[]
                    {
                        new Vector3(0f, -0.5f, 0f),
                        new Vector3(0.5f, -0.5f, 0.5f),
                        new Vector3(-0.5f, -0.5f, 0.5f),
                        new Vector3(0.5f, -0.5f, -0.5f),
                        new Vector3(-0.5f, -0.5f, -0.5f),
                    };
                }
            }
            else
            {
                _points = LocalPoints != null && LocalPoints.Length >= 1
                    ? LocalPoints
                    : new[] { Vector3.zero };
            }

            int n = _points.Length;
            _query = new Vector3[n];
            _heights = new float[n];
            _normals = new Vector3[n];
            _vels = new Vector3[n];
        }

        private void FixedUpdate()
        {
            if (!Application.isPlaying || _points == null || _points.Length == 0) return;

            OceanRenderer ocean = Ocean ? Ocean : OceanRenderer.Instance;
            if (!ocean) return;

            ICollProvider provider = ocean.CollisionProvider;
            if (provider != null)
            {
                for (int i = 0; i < _points.Length; i++)
                {
                    Vector3 local = _points[i];
                    _query[i] = OceanProbe
                        ? OceanProbe.TransformPoint(local)
                        : transform.TransformPoint(local);
                }

                int status = provider.Query(_hash, MinSpatialLength, _query, _heights, _normals, _vels);
                if (provider.RetrieveSucceeded(status))
                {
                    _hasSurface = true;
                    _failStreak = 0;
                }
                else
                {
                    _failStreak++;
                }
            }

            // 拿不到海面数据时沿用上一次的结果，避免物品凭空沉底丢失
            if (!_hasSurface) return;
            if (_failStreak > 0 && _failStreak % 120 == 1)
                Debug.LogWarning("[鲸落] 物品浮力已连续 " + _failStreak + " 次拿不到海面数据，暂用上一次结果。", this);

            ApplyBuoyancy(ocean.SeaLevel);
            ApplyDrag(ocean.SeaLevel);
            if (ControlledFloat) ApplyAlignment();
        }

        private void ApplyBuoyancy(float seaLevel)
        {
            float g = Mathf.Abs(Physics.gravity.y);
            if (g <= 0f) g = 9.81f;

            for (int i = 0; i < _points.Length; i++)
            {
                float submerged = RaftBuoyancySolver.SubmergedFraction(_heights[i] + RaiseObject, _query[i].y, HullHeight);
                if (submerged <= 0f) continue;

                float accel = BuoyancyCoeff * submerged * g;
                if (MaxBuoyancyAccel > 0f) accel = Mathf.Min(accel, MaxBuoyancyAccel);
                _rb.AddForceAtPosition(Vector3.up * accel, _query[i], ForceMode.Acceleration);
            }
        }

        private void ApplyDrag(float seaLevel)
        {
            Vector3 vRel = _rb.velocity;
            if (_vels != null && _vels.Length > 0 && _hasSurface)
            {
                Vector3 sum = Vector3.zero;
                for (int i = 0; i < _vels.Length; i++) sum += _vels[i];
                vRel -= sum / _vels.Length;
            }

            Vector3 pos = _rb.worldCenterOfMass + ForceHeightOffset * Vector3.up;
            _rb.AddForceAtPosition(DragUp * Vector3.Dot(Vector3.up, -vRel) * Vector3.up, pos, ForceMode.Acceleration);
            _rb.AddForceAtPosition(DragRight * Vector3.Dot(transform.right, -vRel) * transform.right, pos, ForceMode.Acceleration);
            _rb.AddForceAtPosition(DragForward * Vector3.Dot(transform.forward, -vRel) * transform.forward, pos, ForceMode.Acceleration);
            _rb.AddTorque(-DragAngular * _rb.angularVelocity, ForceMode.Acceleration);
        }

        private void ApplyAlignment()
        {
            Vector3 normal = Vector3.up;
            if (_normals != null && _normals.Length > 0)
            {
                Vector3 sum = Vector3.zero;
                for (int i = 0; i < _normals.Length; i++) sum += _normals[i];
                if (sum.sqrMagnitude > 1e-6f) normal = sum.normalized;
            }

            _rb.AddTorque(Vector3.Cross(transform.up, normal) * AlignTorque, ForceMode.Acceleration);

            float maxRad = MaxAngularSpeed * Mathf.Deg2Rad;
            if (maxRad > 0f && _rb.angularVelocity.magnitude > maxRad)
                _rb.angularVelocity = _rb.angularVelocity.normalized * maxRad;
        }

        private void OnDrawGizmosSelected()
        {
            if (!DrawGizmos) return;
            if (_points == null) BuildPoints();
            if (_points == null) return;

            Gizmos.color = new Color(0.3f, 0.85f, 1f, 0.9f);
            for (int i = 0; i < _points.Length; i++)
                Gizmos.DrawWireSphere(transform.TransformPoint(_points[i]), 0.06f);
        }
    }
}
