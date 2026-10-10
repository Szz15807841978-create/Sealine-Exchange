using UnityEngine;

namespace Fishing
{
    /// <summary>
    /// 让 UI 上的「站在木筏上的人/物」跟着倾斜的甲板走。
    ///
    /// 背景：侧视玩法里玩家是画布上的 RectTransform，木筏是另一台摄像机渲进 RenderTexture
    /// 再当图片贴到画布上的。两者本来毫无关系，木筏一倾斜，人就会浮在半空。
    ///
    /// 做法：只接管「竖直跟随 + 随板倾斜」两件事，横向位置完全不动 ——
    /// 玩家的 x 仍然是 DivePointController 的玩法坐标（上下限、和木箱的距离判定全部不变），
    /// 本组件只把 y 改成「甲板在这一点的高度」并让人跟着甲板倾角转。
    /// 因为 DivePointController 改位置时只写 x、保留当前 y，两者不会互相打架。
    ///
    /// UI x 与甲板 x 的换算靠一个参照物标定：木箱的隐形 2D 标记 + 木箱的 3D 实体。
    /// 这两个对象指的是同一个木箱，所以用它们定出「每甲板单位多少 UI 像素」和原点，
    /// 不依赖手工量取的常数。场景实测：木箱 2D 在 x=-170、3D 在甲板 x=-1.703，
    /// 推出的换算比例 104.6 px/单位与摄像机正交视口算出来的值吻合到 4 px 以内。
    /// </summary>
    [AddComponentMenu("鲸落/木筏甲板乘客（UI 跟随）")]
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-40)]   // 必须晚于 RaftBuoyancy(-50)
    public sealed class RaftDeckRider : MonoBehaviour
    {
        [Header("引用")]
        [Tooltip("场景里的 RaftBuoyancy。留空会自动查找。")]
        public RaftBuoyancy Buoyancy;

        [Tooltip("渲染木筏的摄像机（3D木筏展示摄像机）。用来算「多少 UI 像素 = 一个甲板单位」。")]
        public Camera ModelCamera;

        [Tooltip("显示该摄像机画面的 RawImage（3D木筏实时画面）。")]
        public RectTransform Viewport;

        [Tooltip("要贴在甲板上的 UI 元素（玩家）。")]
        public RectTransform Rider;

        [Tooltip("标定用参照物 · 2D：木箱的隐形标记。")]
        public RectTransform Landmark;

        [Tooltip("标定用参照物 · 3D：木箱实体。")]
        public Transform Landmark3D;

        [Header("标定")]
        [Tooltip("每甲板单位对应多少 UI 像素。0 = 按摄像机的正交视口自动计算；统一单相机场景请填写固定标定值。")]
        public float PixelsPerUnit = 0f;

        [Tooltip("旧的木筏展示摄像机被停用或丢失时使用的标定值。本场景由木箱位置实测得到。")]
        public float FallbackPixelsPerUnit = 104.6f;

        [Tooltip("UI +x 方向对应甲板 +X（1）还是 -X（-1）。本场景实测为 +1。")]
        public float DeckUiSign = 1f;

        [Tooltip("甲板面在木筏本地坐标里的高度（装置底面所在的 y）。")]
        public float DeckSurfaceLocalY = 1.15f;

        [Tooltip("乘客所在的甲板横向位置（z）。取装置所在的 z 即可。")]
        public float DeckZForRider = 0.67f;

        [Header("表现")]
        public bool Active = true;

        [Tooltip("木筏倾斜时让乘客跟着转。")]
        public bool LeanWithDeck = true;

        [Tooltip("随板倾斜的方向符号。若人往反方向倒，改成 -1。")]
        public float LeanSign = 1f;

        [Tooltip("随板倾斜的比例，1 = 与甲板完全同角度。")]
        public float LeanScale = 1f;

        [Tooltip("竖直跟随的限幅（UI 像素），防止异常浪把人生拉出画面。")]
        public float MaxOffsetPixels = 60f;

        [Header("诊断")]
        public bool LogDiagnostics = false;

        // ---- 只读输出（诊断用）----
        /// <summary>本帧算出的竖直偏移（UI 像素）。</summary>
        public float OffsetPixels { get; private set; }
        /// <summary>本帧采用的换算比例（UI 像素 / 甲板单位）。</summary>
        public float ResolvedPixelsPerUnit { get; private set; }
        /// <summary>乘客当前对应的甲板 x（木筏本地坐标）。</summary>
        public float RiderDeckX { get; private set; }

        private bool _init;
        private float _restY;
        private Quaternion _restRot;
        private float _landmarkUiX;
        private float _landmarkLocalX;
        private float _nextDiagnostic;

        private void LateUpdate()
        {
            if (!Active) return;
            if (!Application.isPlaying) return;

            if (!Buoyancy) Buoyancy = FindObjectOfType<RaftBuoyancy>(true);
            // 统一渲染后旧的木筏展示摄像机可以不存在。甲板跟随只真正依赖
            // Buoyancy、Rider 和一份像素/世界单位标定，不应因旧相机为空而停摆。
            if (!Buoyancy || !Buoyancy.RaftRoot || !Rider) return;

            if (!_init)
            {
                _init = true;
                _restY = Rider.anchoredPosition.y;
                _restRot = Rider.localRotation;

                _landmarkUiX = Landmark ? Landmark.anchoredPosition.x : 0f;
                _landmarkLocalX = 0f;
                if (Landmark3D)
                    _landmarkLocalX = Buoyancy.RaftRoot.InverseTransformPoint(Landmark3D.position).x;
            }

            ResolvedPixelsPerUnit = PixelsPerUnit > 0f ? PixelsPerUnit : AutoPixelsPerUnit();
            if (ResolvedPixelsPerUnit <= 1e-4f)
                ResolvedPixelsPerUnit = FallbackPixelsPerUnit;
            if (ResolvedPixelsPerUnit <= 1e-4f) return;

            // UI x → 甲板 x（以参照物为原点做线性换算）
            float uiX = Rider.anchoredPosition.x;
            RiderDeckX = _landmarkLocalX + (uiX - _landmarkUiX) / ResolvedPixelsPerUnit * DeckUiSign;

            // 同一点在当前姿态 vs 静止姿态下的世界高度差 → UI 像素
            var deckLocal = new Vector3(RiderDeckX, DeckSurfaceLocalY, DeckZForRider);
            float deltaY = Buoyancy.DeckPointNow(deckLocal).y - Buoyancy.DeckPointRest(deckLocal).y;
            float offset = deltaY * ResolvedPixelsPerUnit;
            float limit = Mathf.Abs(MaxOffsetPixels);
            offset = Mathf.Clamp(offset, -limit, limit);
            OffsetPixels = offset;

            // 只改 y：x 始终沿用玩法层刚写入的值
            Rider.anchoredPosition = new Vector2(uiX, _restY + offset);

            if (LeanWithDeck)
            {
                float lean = Buoyancy.PitchDeg * LeanScale * LeanSign * DeckUiSign;
                Rider.localRotation = _restRot * Quaternion.Euler(0f, 0f, lean);
            }

            if (LogDiagnostics && Time.time >= _nextDiagnostic)
            {
                _nextDiagnostic = Time.time + 1f;
                Debug.Log(string.Format(
                    "[鲸落] 甲板乘客：甲板 x {0:0.00}  竖直偏移 {1:0.0} px  换算 {2:0.0} px/单位",
                    RiderDeckX, OffsetPixels, ResolvedPixelsPerUnit), this);
            }
        }

        /// <summary>
        /// 正交摄像机 + RawImage：视口高度（2×orthographicSize 个世界单位）铺满 Viewport 的高度，
        /// 于是「每单位多少像素 = Viewport 高度 / 视口世界高度」。正交投影各向同性，横向同理。
        /// </summary>
        private float AutoPixelsPerUnit()
        {
            if (!ModelCamera || !Viewport) return 0f;
            if (!ModelCamera.orthographic) return 0f;
            float worldHeight = ModelCamera.orthographicSize * 2f;
            if (worldHeight <= 1e-4f) return 0f;
            return Viewport.rect.height / worldHeight;
        }
    }
}
