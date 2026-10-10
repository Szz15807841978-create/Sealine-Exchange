using UnityEngine;
using UnityEngine.UI;

namespace Fishing
{
    /// <summary>
    /// 主界面骨架：对应 index.html 的 #app 网格。
    /// 行高固定，行位置用 anchor/pivot 手工摆（不依赖 LayoutGroup 的父级拉伸），
    /// 这样图表区能精确占满剩余空间。
    /// </summary>
    public class MainLayout
    {
        public RectTransform Root;
        public RectTransform Hud;
        public RectTransform Chart;
        public ChartView ChartView;
        public RectTransform StatsHost;
        public RectTransform StripHost;
        public RectTransform ControlsHost;
        public GaugeView Gauge;
        public RectTransform Overlay;
        public RectTransform Modal;
        public ToastView Toast;
        public RectTransform DevPanel;

        public Text SPrice, SHold, SFloat, STotal;

        private const float PadX = 14f;
        private const float Gap = 9f;
        private const float PadTop = 10f;
        private const float PadBottom = 12f;

        private const float HudH = 46f;
        private const float StatsH = 52f;
        private const float StripH = 46f;
        private const float ControlsH = 48f;

        private const float MaxWidth = 1220f;
        private bool _usesSceneLayout;

        public void Build(RectTransform canvasRoot, bool includeStaticBackground = true)
        {
            _canvas = canvasRoot != null ? canvasRoot.GetComponent<Canvas>() : null;
            var sceneLayout = canvasRoot != null ? canvasRoot.Find("Scene UI Layout") as RectTransform : null;

            RectTransform toastGo;
            if (sceneLayout != null)
            {
                // 第二阶段场景化：这些宿主对象的位置和尺寸完全以 Main.unity 为准。
                _usesSceneLayout = true;
                Root = sceneLayout;
                Hud = ScenePart("状态栏");
                Chart = ScenePart("曲线区");
                StatsHost = ScenePart("数据条");
                StripHost = ScenePart("持仓条");
                ControlsHost = ScenePart("操作区");
                Overlay = ScenePart("遮罩层");
                Modal = ScenePart("窗口层");
                toastGo = ScenePart("Toast层");

                ApplyScenePanel(Hud, detailed: true);
                ApplyScenePanel(Chart, grid: true);
                ApplyScenePanel(StatsHost);
                ApplyScenePanel(StripHost);
                ApplyScenePanel(ControlsHost);

                UiKit.Clear(toastGo);
            }
            else
            {
                _usesSceneLayout = false;

                // 兼容未执行场景化的旧场景。
                RawImage bgGo = null;
                if (includeStaticBackground)
                {
                    var bgRt = UiKit.Rect("03-等待收网背景", canvasRoot);
                    UiKit.Stretch(bgRt);
                    bgGo = bgRt.gameObject.AddComponent<RawImage>();
                    bgGo.raycastTarget = false;
                    var backgroundTexture = Resources.Load<Texture2D>("Art/03-等待收网");
                    if (backgroundTexture != null)
                    {
                        bgGo.texture = backgroundTexture;
                        bgGo.color = Color.white;
                        var aspect = bgRt.gameObject.AddComponent<AspectRatioFitter>();
                        aspect.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                        aspect.aspectRatio = (float)backgroundTexture.width / backgroundTexture.height;
                    }
                    else bgGo.color = Palette.Bg;
                }

                Root = UiKit.Rect("App", canvasRoot);
                UiKit.Stretch(Root);
                Root.gameObject.AddComponent<AppLayout>().Layout = this;
                Hud = Panel("状态栏", Root);
                Chart = Panel("曲线区", Root);
                StatsHost = Panel("数据条", Root);
                StripHost = Panel("持仓条", Root);
                ControlsHost = Panel("操作区", Root);

                var ovGo = UiKit.Block("遮罩层", Root, Palette.OverlayBg);
                Overlay = ovGo.rectTransform;
                UiKit.Stretch(Overlay);
                var modalGo = UiKit.Block("窗口层", Root, Palette.OverlayBg);
                Modal = modalGo.rectTransform;
                UiKit.Stretch(Modal);
                toastGo = UiKit.Rect("Toast层", Root);
                UiKit.Stretch(toastGo);

                if (bgGo != null) bgGo.transform.SetAsFirstSibling();
            }

            ChartView = new ChartView(Chart);
            BuildStats();
            Gauge = new GaugeView(Chart);
            BuildDevPanel();
            Toast = new ToastView(toastGo);

            Overlay.gameObject.SetActive(false);
            Modal.gameObject.SetActive(false);
            Overlay.SetAsLastSibling();
            Modal.SetAsLastSibling();
            toastGo.SetAsLastSibling();
        }

        private RectTransform ScenePart(string name)
        {
            var rt = Root.Find(name) as RectTransform;
            if (rt != null) return rt;
            return Panel(name, Root);
        }

        private static void ApplyScenePanel(RectTransform root, bool detailed = false, bool grid = false)
        {
            if (!root) return;
            var image = root.GetComponent<Image>();
            if (!image) image = root.gameObject.AddComponent<Image>();
            if (grid) UiSkinTheme.ApplyGridPanel(image);
            else UiSkinTheme.ApplyPanel(image, detailed: detailed);
            image.raycastTarget = false;
        }

        /// <summary>
        /// 找到承载本布局的 Canvas。
        /// 关键：行尺寸必须用 Canvas 的 rect（**画布单位**）算，不能用 Screen.width/height（设备像素）。
        /// 配了 CanvasScaler 之后两者差一个 scaleFactor，用像素值算会让内容超出画布高度、
        /// 下面几行（操作区）直接跑到屏幕外 —— 这就是"界面全乱"的主因。
        /// </summary>
        public Canvas FindCanvas()
        {
            if (_canvas != null) return _canvas;
            if (Root != null)
            {
                var c = Root.GetComponentInParent<Canvas>();
                if (c != null) { _canvas = c; return c; }
            }
            _canvas = Object.FindObjectOfType<Canvas>();
            return _canvas;
        }

        private Canvas _canvas;

        /// <summary>按当前 Canvas 尺寸重排；尺寸还拿不到时用 CanvasScaler 从屏幕像素反推。</summary>
        public void PlaceRowsFromCanvas()
        {
            if (_usesSceneLayout)
            {
                if (ChartView != null && Chart != null)
                    ChartView.Resize(Mathf.Max(1f, Chart.rect.width), Mathf.Max(1f, Chart.rect.height));
                return;
            }

            var canvas = FindCanvas();
            if (canvas == null)
            {
                PlaceRows(Screen.width, Screen.height);
                return;
            }

            var rt = canvas.GetComponent<RectTransform>();
            float w = rt != null ? rt.rect.width : 0f;
            float h = rt != null ? rt.rect.height : 0f;

            // 首帧 rect 可能还是 0：用 scaler 的 scaleFactor 把屏幕像素折算成画布单位
            if (w < 1f || h < 1f)
            {
                var scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>();
                float sf = (scaler != null && scaler.scaleFactor > 0.001f) ? scaler.scaleFactor : 1f;
                w = Screen.width / sf;
                h = Screen.height / sf;
            }
            PlaceRows(w, h);
        }

        /// <summary>按画布尺寸摆放新版主界面：背景为主体，行情区位于右上，操作位于左下。</summary>
        public void PlaceRows(float canvasW, float canvasH)
        {
            float safeW = Mathf.Min(MaxWidth, canvasW);
            float margin = Mathf.Max(10f, PadX);

            float hudW = Mathf.Clamp(safeW * 0.31f, 300f, 390f);
            PlaceTopLeft(Hud, hudW, HudH, margin, PadTop);

            float chartW = Mathf.Clamp(safeW * 0.55f, 430f, 690f);
            float chartH = Mathf.Clamp(canvasH * 0.46f, 240f, 360f);
            PlaceTopRight(Chart, chartW, chartH, margin, PadTop + 8f);
            PlaceTopRight(StatsHost, chartW, StatsH, margin, PadTop + 8f + chartH + Gap);

            float controlsW = Mathf.Clamp(safeW * 0.59f, 520f, 720f);
            PlaceBottomLeft(ControlsHost, controlsW, ControlsH, margin, PadBottom);

            float stripW = Mathf.Clamp(safeW * 0.48f, 420f, 590f);
            PlaceBottomLeft(StripHost, stripW, StripH, margin, PadBottom + ControlsH + Gap);

            ChartView.Resize(chartW - 4f, chartH - 4f);
        }

        private static void PlaceTopLeft(RectTransform rt, float w, float h, float x, float y)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(x, -y);
        }

        private static void PlaceTopRight(RectTransform rt, float w, float h, float x, float y)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(-x, -y);
        }

        private static void PlaceBottomLeft(RectTransform rt, float w, float h, float x, float y)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(x, y);
        }

        private RectTransform Panel(string name, Transform parent)
        {
            var img = UiKit.Gradient(name, parent, Palette.Panel, Palette.PanelBottom);
            UiSkinTheme.ApplyPanel(img, detailed: name == "状态栏");
            var gradient = img.GetComponent<Gradient2>();
            if (gradient && UiSkinTheme.Active) gradient.enabled = false;
            var o = img.gameObject.AddComponent<Outline>();
            o.effectColor = UiSkinTheme.Active ? Palette.Hex("#173A57", 0.18f) : Palette.Line;
            o.effectDistance = new Vector2(2f, -2f);
            return img.rectTransform;
        }

        private void BuildStats()
        {
            if (TryBindStats()) return;
            UiKit.Clear(StatsHost);
            var row = UiKit.Rect("行", StatsHost);
            UiKit.Stretch(row, 12f, 8f, 12f, 8f);
            UiKit.Row(row, 8f);

            SPrice = StatCell(row, "当前价");
            SHold = StatCell(row, "持仓");
            SFloat = StatCell(row, "浮动");
            STotal = StatCell(row, "本场收益");
        }

        private bool TryBindStats()
        {
            SPrice = FindStatValue("当前价");
            SHold = FindStatValue("持仓");
            SFloat = FindStatValue("浮动");
            STotal = FindStatValue("本场收益");
            return SPrice && SHold && SFloat && STotal;
        }

        private Text FindStatValue(string name)
        {
            var cell = StatsHost.Find(name);
            if (cell == null) return null;
            var value = cell.Find("值");
            return value != null ? value.GetComponent<Text>() : null;
        }

        private Text StatCell(RectTransform parent, string key)
        {
            var img = UiKit.Outline(key, parent, Palette.BoxBg, Palette.LineSoft);
            var le = img.gameObject.AddComponent<LayoutElement>();
            le.minWidth = 110f;
            le.minHeight = 36f;
            le.preferredHeight = 36f;
            le.flexibleWidth = 1f;

            var k = Txt.Make(img.rectTransform, "键", 11, Palette.Muted, TextAnchor.UpperLeft);
            UiKit.Stretch((RectTransform)k.transform, 10f, 0f, 8f, 20f);
            k.text = key;

            var v = Txt.Make(img.rectTransform, "值", 17, Palette.Text, TextAnchor.LowerLeft, FontStyle.Bold);
            UiKit.Stretch((RectTransform)v.transform, 10f, 15f, 8f, 0f);
            return v;
        }

        private void BuildDevPanel()
        {
            var btnGo = UiKit.Outline("调试按钮", Root, Palette.ButtonBg, Palette.Line);
            UiKit.Fixed(btnGo.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-14f, 14f), 36f, 36f);
            var t = Txt.Make(btnGo.rectTransform, "文本", 12, Palette.Muted, TextAnchor.MiddleCenter);
            UiKit.Stretch((RectTransform)t.transform);
            t.text = "调试";
            var btn = btnGo.gameObject.AddComponent<Button>();
            btn.targetGraphic = btnGo;
            btn.transition = Selectable.Transition.ColorTint;

            var panel = UiKit.Outline("调试面板", Root, Palette.Panel2, Palette.Line);
            DevPanel = panel.rectTransform;

            var col = UiKit.Col(DevPanel, 6f, new RectOffset(11, 11, 11, 11));
            col.childForceExpandHeight = false;

            var head = Txt.Make(DevPanel, "标题", 12, Palette.Muted, TextAnchor.MiddleLeft);
            UiKit.Size(head.rectTransform, -1f, 18f);
            head.text = "调试面板";

            DevButton("+2 完整网", "nets", Widgets.Hue.Normal);
            DevButton("+20 线虫", "worms", Widgets.Hue.Normal);
            DevButton("+200 价值渔获", "fish", Widgets.Hue.Normal);
            DevButton("弄破 1 张网", "damage", Widgets.Hue.Normal);
            DevButton("立刻弹出仪表盘", "gauge", Widgets.Hue.Normal);
            DevButton("立即收市", "end", Widgets.Hue.Normal);
            DevButton("重开 Demo", "reset", Widgets.Hue.Danger);

            // 面板高度按内容算：标题 + 7 个按钮 + 间距 + 内边距
            float h = 11f + 18f + 6f + 7f * (28f + 6f) + 11f;
            UiKit.Fixed(DevPanel, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-14f, 58f), 210f, h);

            DevPanel.gameObject.SetActive(false);
            btn.onClick.AddListener(() => DevPanel.gameObject.SetActive(!DevPanel.gameObject.activeSelf));
        }

        private void DevButton(string label, string action, Widgets.Hue hue)
        {
            Widgets.MakeButton(DevPanel, label, hue, 12, -1f, 28f, () =>
            {
                if (GameController.Instance != null) GameController.Instance.DevAction(action);
            });
        }
    }

    /// <summary>每帧按画布尺寸重排主界面各行。</summary>
    public class AppLayout : MonoBehaviour
    {
        public MainLayout Layout;

        private float _lastW = -1f, _lastH = -1f;

        private void LateUpdate()
        {
            if (Layout == null || Layout.Root == null) return;
            var rt = Layout.Root;
            float w = rt.rect.width, h = rt.rect.height;
            if (Mathf.Approximately(w, _lastW) && Mathf.Approximately(h, _lastH)) return;
            _lastW = w;
            _lastH = h;
            Layout.PlaceRowsFromCanvas();
        }
    }
}
