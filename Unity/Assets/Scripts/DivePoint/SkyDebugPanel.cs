using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

namespace Fishing
{
    /// <summary>
    /// 天空调试面板：8 个「时段 × 天气」组合的切换器。
    ///
    /// 为什么藏在调试面板里：正式界面只该有「晴天 / 雨天」两个按钮（玩家不该直接调时段），
    /// 但开发和验收时需要能逐个看这 8 套天空，所以做成一个不进正常流程的调试层。
    ///
    /// 编辑器只绑定已保存的控件，不重建、不改变手调布局和显隐。
    /// 编辑模式下 uGUI 按钮点了不响应（Game 视图不处理输入），所以那个场景下请用
    /// Inspector 上的 8 个按钮（见 Assets/Editor/SkyDebugPanelEditor.cs）。
    /// 运行时（Play / 打包）一律默认隐藏，按 F1 唤出。
    ///
    /// 仅在运行时或显式展开面板且没有现成面板时才创建默认控件，不依赖任何贴图 ——
    /// 配色直接取概念图那套（纸面 / 墨线 / 琥珀 / 青灰），所以即便 UI 皮肤没装也看着是一家的。
    /// </summary>
    [AddComponentMenu("鲸落/天空调试面板")]
    [ExecuteAlways]
    public sealed class SkyDebugPanel : MonoBehaviour
    {
        const string RootName = "天空调试面板";

        [Tooltip("唤出/收起面板的按键（仅运行时有效）。")]
        public KeyCode ToggleKey = KeyCode.F1;

        [Tooltip("留空则自动查找场景里的 RaftWeatherController。")]
        public RaftWeatherController Weather;

        [Tooltip("留空则自动用 Weather 所在的画布。")]
        public Canvas TargetCanvas;

        [Tooltip("运行时是否默认展开。编辑器保留场景中保存的显隐状态。")]
        public bool StartsVisible = false;

        [Tooltip("留空则自动查找场景里的 RealisticEnvironmentController（编辑器里靠它即时套用外观）。")]
        public RealisticEnvironmentController Environment;

        // 概念图取样出的色板
        static readonly Color Navy = new Color32(0x17, 0x3A, 0x57, 0xFF);
        static readonly Color NavySoft = new Color32(0x5E, 0x84, 0xA3, 0xFF);
        static readonly Color Paper = new Color32(0xF2, 0xE7, 0xCB, 0xFF);
        static readonly Color PaperDim = new Color32(0xE4, 0xD8, 0xBA, 0xFF);
        static readonly Color Amber = new Color32(0xDD, 0xA6, 0x4F, 0xFF);
        static readonly Color Teal = new Color32(0x5D, 0x92, 0x9A, 0xFF);

        private GameObject _root;
        private Text _state;
        private readonly List<Image> _cellBg = new List<Image>();
        private readonly List<Text> _cellText = new List<Text>();
        private readonly List<int> _cellIndex = new List<int>();
        private readonly List<Button> _boundButtons = new List<Button>();
        private readonly List<UnityAction> _boundActions = new List<UnityAction>();
        private bool _needsBind = true;
        private bool _runtimeVisibilitySet;

        // Validation/deserialization may run while Unity cannot safely mutate a hierarchy.
        // These callbacks only request work; Start/Update handles binding on the main thread.
        private void Awake() { _needsBind = true; }
        private void OnEnable() { _needsBind = true; }
        private void Start() { EnsureBuilt(Application.isPlaying); }
        private void OnDisable() { UnbindButtons(); _needsBind = true; }

        /// <summary>编辑器里改 Inspector 数值时也要跟上（含 8 个按钮的选中态）。</summary>
        private void OnValidate()
        {
            _needsBind = true;
        }

        private void Resolve()
        {
            if (!Weather) Weather = FindObjectOfType<RaftWeatherController>(true);
            if (!Environment) Environment = FindObjectOfType<RealisticEnvironmentController>(true);
            if (!TargetCanvas) TargetCanvas = Weather ? Weather.GetComponentInParent<Canvas>() : FindObjectOfType<Canvas>(true);
        }

        private void Update()
        {
            if (_needsBind) EnsureBuilt(Application.isPlaying);
            if (!Application.isPlaying) return;
            if (Input.GetKeyDown(ToggleKey)) SetVisible(_root && !_root.activeSelf);
        }

        public void SetVisible(bool on)
        {
            if (!_root || _needsBind) EnsureBuilt(on || Application.isPlaying);
            if (_root) _root.SetActive(on);
            if (on) RefreshState();
        }

        public bool IsVisible { get { var root = _root ? _root.transform : ExistingRoot(); return root && root.gameObject.activeSelf; } }

        /// <summary>设置「时段 + 天气」。天气走 Weather.Rainy，时段写进 Weather.TimeOfDay。</summary>
        public void Apply(int timeIndex, bool rainy)
        {
            Resolve();
            if (!_root || _needsBind) EnsureBuilt(false);
            int t = Mathf.Clamp(timeIndex, 0, RaftSkyPresets.TimeCount - 1);
            if (Weather)
            {
                Weather.TimeOfDay = (RaftTimeOfDay)t;
                Weather.SetRain(rainy);
                // 编辑器里 Weather 的 Update 不跑、RainAmount 不会自己爬过去，所以直接给到目标值
                Weather.RainAmount = rainy ? 1f : 0f;
            }
            // 编辑器里靠它即时套用天空/海面/光照；Play 模式下环境控制器自己每帧会套
            if (Environment) Environment.ApplyForPreview((RaftTimeOfDay)t, rainy ? 1f : 0f);
            RefreshState();
        }

        private void RefreshState()
        {
            if (_state && Weather)
                _state.text = "当前：" + RaftSkyPresets.Label(Weather.TimeOfDay, Weather.Rainy)
                              + "　雨量 " + Mathf.RoundToInt(Weather.RainAmount * 100) + "%";

            for (int i = 0; i < _cellIndex.Count; i++)
            {
                int idx = _cellIndex[i];
                bool selected = Weather && idx == RaftSkyPresets.Index(Weather.TimeOfDay, Weather.Rainy);
                if (i < _cellBg.Count && _cellBg[i]) _cellBg[i].color = selected ? Amber : PaperDim;
                if (i < _cellText.Count && _cellText[i]) _cellText[i].color = selected ? Navy : NavySoft;
            }
        }

        private void LateUpdate() { if (Application.isPlaying && _root && _root.activeSelf) RefreshState(); }

        private Canvas FindCanvas()
        {
            if (TargetCanvas) return TargetCanvas;
            if (Weather) return Weather.GetComponentInParent<Canvas>();
            return GetComponentInParent<Canvas>();
        }

        private Transform ExistingRoot()
        {
            var canvas = FindCanvas();
            return canvas ? canvas.transform.Find(RootName) : null;
        }

        // ---------------------------------------------------------------- 搭界面
        /// <summary>
        /// 只绑定现有控件。没有面板时，只有运行时/显式展开允许创建默认面板。
        /// 编辑器自动刷新不写场景，不销毁对象，也不会补回用户主动删除的控件。
        /// </summary>
        private void EnsureBuilt(bool allowCreate)
        {
            if (Application.isPlaying || allowCreate) Resolve();
            var canvas = FindCanvas();
            if (!canvas) return;

            var existing = canvas.transform.Find(RootName);
            if (existing) _root = existing.gameObject;
            else
            {
                if (!allowCreate) { UnbindButtons(); _root = null; _needsBind = false; return; }
                _root = new GameObject(RootName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                var rootRt = (RectTransform)_root.transform;
                rootRt.SetParent(canvas.transform, false);
                rootRt.anchorMin = rootRt.anchorMax = new Vector2(1f, 0.5f);
                rootRt.pivot = new Vector2(1f, 0.5f);
                rootRt.anchoredPosition = new Vector2(-16f, 0f);
                rootRt.sizeDelta = new Vector2(300f, 300f);
                var bg = _root.GetComponent<Image>();
                bg.color = Paper;
                bg.raycastTarget = true;
                Build();
            }

            UnbindButtons();
            _cellBg.Clear(); _cellText.Clear(); _cellIndex.Clear();
            var state = _root.transform.Find("状态");
            _state = state ? state.GetComponent<Text>() : null;
            for (int w = 0; w < RaftSkyPresets.WeatherCount; w++)
            for (int t = 0; t < RaftSkyPresets.TimeCount; t++)
            {
                var cell = _root.transform.Find("格_" + t + "_" + w);
                if (!cell) continue;
                var button = cell.GetComponent<Button>();
                if (!button) continue;
                int time = t; bool rain = w == 1;
                UnityAction action = () => Apply(time, rain);
                button.onClick.AddListener(action);
                _boundButtons.Add(button); _boundActions.Add(action);
                _cellBg.Add(cell.GetComponent<Image>());
                _cellText.Add(cell.GetComponentInChildren<Text>(true));
                _cellIndex.Add(RaftSkyPresets.Index((RaftTimeOfDay)t, rain));
            }
            _needsBind = false;
            if (Application.isPlaying && !_runtimeVisibilitySet)
            {
                _runtimeVisibilitySet = true;
                if (_root.activeSelf != StartsVisible) _root.SetActive(StartsVisible);
            }
            if (Application.isPlaying) RefreshState();
        }

        private void UnbindButtons()
        {
            // Remove only listeners owned by this component, never Inspector/user listeners.
            for (int i = 0; i < _boundButtons.Count; i++)
                if (_boundButtons[i]) _boundButtons[i].onClick.RemoveListener(_boundActions[i]);
            _boundButtons.Clear(); _boundActions.Clear();
        }

        private void Build()
        {
            if (!_root) return;
            var rt = (RectTransform)_root.transform;

            // 标题
            var title = MakeText(rt, "标题", "调试 · 天空", 20, Navy, TextAnchor.MiddleCenter);
            Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(280f, 32f));

            // 状态
            _state = MakeText(rt, "状态", "", 15, NavySoft, TextAnchor.MiddleCenter);
            Place(_state.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -52f), new Vector2(280f, 24f));

            // 4 列 × 2 行：列 = 时段，行 = 天气
            const float cw = 68f, chh = 44f, gap = 4f;
            float x0 = -(cw * 2f + gap) + cw * 0.5f;
            for (int t = 0; t < RaftSkyPresets.TimeCount; t++)
            {
                var head = MakeText(rt, "时段" + t, RaftSkyPresets.TimeNames[t], 14, Navy, TextAnchor.MiddleCenter);
                Place(head.rectTransform, new Vector2(0.5f, 1f),
                    new Vector2(x0 + t * (cw + gap), -86f), new Vector2(cw, 22f));
            }
            for (int w = 0; w < RaftSkyPresets.WeatherCount; w++)
            {
                bool rainy = w == 1;
                for (int t = 0; t < RaftSkyPresets.TimeCount; t++)
                {
                    int time = t;
                    bool isRain = rainy;
                    MakeButton(rt, "格_" + time + "_" + w,
                        rainy ? "雨" : "晴",
                        new Vector2(x0 + t * (cw + gap), -116f - w * (chh + gap)),
                        new Vector2(cw, chh),
                        time, isRain);
                }
            }

            // 底部提示 + 关闭
            var hint = MakeText(rt, "提示", "F1 收起 / 唤出　（正式界面只暴露晴天·雨天）", 12, NavySoft, TextAnchor.MiddleCenter);
            Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(280f, 20f));
        }

        private Text MakeText(RectTransform parent, string name, string value, int size, Color color, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.text = value;
            t.font = FontUtil.Get();
            t.fontSize = size;
            t.color = color;
            t.alignment = anchor;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        private void MakeButton(RectTransform parent, string name, string label, Vector2 pos, Vector2 size,
            int timeIndex, bool rainy)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            Place(rt, new Vector2(0.5f, 1f), pos, size);

            var img = go.GetComponent<Image>();
            img.color = PaperDim;

            var txt = MakeText(rt, "文字", label, 18, NavySoft, TextAnchor.MiddleCenter);
            var trt = txt.rectTransform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
        }

        private static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }
    }
}
