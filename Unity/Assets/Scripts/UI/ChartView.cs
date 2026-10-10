using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Fishing
{
    /// <summary>
    /// 实时曲线：对应 index.html 的 drawChart()。
    ///
    /// 坐标约定（唯一一套，避免上下翻转的坑）：
    ///   * 网格顶点走 ChartGraphic，使用 root 的**局部坐标**，原点在左下角。
    ///   * 文字 / 图标走 RectTransform.anchoredPosition，锚点与 pivot 都是中心，
    ///     所以 local(x,y) → anchored(x - W/2, y - H/2)。
    /// </summary>
    public class ChartView
    {
        private const float PadL = 52f, PadR = 16f, PadT = 52f, PadB = 22f;

        private RectTransform _root;
        private ChartGraphic _graphic;
        private RectTransform _labels;
        private Text _hint;
        private Text _tagNets;

        private Image _pausedFlag;
        private Text _pausedText;

        private readonly List<Text> _yLabels = new List<Text>();
        private readonly List<Text> _xLabels = new List<Text>();
        private readonly List<Text> _wipeLabels = new List<Text>();

        private Image _halo;
        private Image _dot;
        private Image _priceTag;
        private Outline _priceTagOutline;
        private Text _priceText;
        private bool _sceneBound;
        private readonly Dictionary<RectTransform, Vector2> _sceneOffsets = new Dictionary<RectTransform, Vector2>();
        private Vector2 _sceneHaloSize;
        private string _hintTemplate;
        private string _tagTemplate;
        private string _pausedTemplate;
        private string _frozenTemplate;

        private float _w = 800f, _h = 320f;

        public ChartView(RectTransform parent)
        {
            _root = parent;
            if (TryBindSceneObjects()) return;

            var background = UiKit.Gradient("背景", _root, Palette.ChartTop, Palette.ChartBottom);
            UiSkinTheme.ApplyGridPanel(background);
            var backgroundGradient = background.GetComponent<Gradient2>();
            if (backgroundGradient && UiSkinTheme.Active) backgroundGradient.enabled = false;
            UiKit.Stretch(background.rectTransform);
            background.raycastTarget = false;

            var g = UiKit.Rect("曲线", _root);
            // BuildMesh 使用左下角为 (0,0) 的坐标；Graphic 的 RectTransform 也必须使用左下 pivot。
            // 默认中心 pivot 会把整张网格向右上平移半个面板，导致曲线大部分落在裁切区外。
            g.pivot = Vector2.zero;
            UiKit.Stretch(g);
            g.gameObject.AddComponent<CanvasRenderer>();
            _graphic = g.gameObject.AddComponent<ChartGraphic>();
            _graphic.View = this;
            _graphic.raycastTarget = false;

            _labels = UiKit.Rect("标签", _root);
            UiKit.Stretch(_labels);

            var title = Txt.Make(_labels, "行情标题", 21, Palette.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiKit.Fixed(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -10f), 132f, 30f);
            title.text = "股市走向";

            _hint = Txt.Make(_labels, "开盘提示", 13, Palette.EmptyHint, TextAnchor.MiddleCenter);
            UiKit.Fixed(_hint.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, 420f, 20f);

            // 左上角"持仓 N 张"
            var tagBox = UiKit.Outline("持仓标签", _root, Palette.BoxBg, Palette.LineSoft);
            UiKit.Fixed(tagBox.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(154f, -13f), 108f, 24f);
            _tagNets = Txt.Make(tagBox.rectTransform, "文本", 12, Palette.Muted, TextAnchor.MiddleLeft);
            UiKit.Stretch((RectTransform)_tagNets.transform, 9f, 0f, 6f, 0f);

            MakeLegend();

            _halo = UiKit.Block("光晕", _root, Palette.HaloNormal);
            _halo.raycastTarget = false;
            _halo.gameObject.SetActive(false);

            _dot = UiKit.Block("点", _root, Palette.DotNormal);
            _dot.raycastTarget = false;
            _dot.gameObject.SetActive(false);

            _priceTag = UiKit.Outline("价格标签", _root, Palette.PriceTagOk, Palette.PriceTagOk);
            _priceTag.raycastTarget = false;
            _priceTagOutline = _priceTag.GetComponent<Outline>();
            _priceText = Txt.Make(_priceTag.rectTransform, "文本", 12, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Stretch((RectTransform)_priceText.transform);
            _priceTag.gameObject.SetActive(false);

            _pausedFlag = UiKit.Block("暂停遮罩", _root, Palette.Hex("#173A57", 0.46f));
            _pausedFlag.raycastTarget = false;
            UiKit.Stretch(_pausedFlag.rectTransform);
            _pausedText = Txt.Make(_pausedFlag.rectTransform, "文本", 20, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Stretch((RectTransform)_pausedText.transform);
            _pausedFlag.gameObject.SetActive(false);

            _hintTemplate = _hint.text;
            _tagTemplate = "持仓 {count} 张";
            _pausedTemplate = "暂 停 中";
            _frozenTemplate = "市 场 冻 结";
        }

        private bool TryBindSceneObjects()
        {
            var curve = _root.Find("曲线");
            var labels = _root.Find("标签层") as RectTransform;
            if (curve == null || labels == null) return false;

            var preview = _root.Find("编辑模式预览曲线");
            if (preview != null) preview.gameObject.SetActive(false);

            _graphic = curve.GetComponent<ChartGraphic>();
            _labels = labels;
            _hint = Required<Text>(_labels, "开盘提示");
            _tagNets = Required<Text>(_root, "持仓标签/文本");
            _halo = Required<Image>(_root, "光晕");
            _dot = Required<Image>(_root, "点");
            _priceTag = Required<Image>(_root, "价格标签");
            _priceTagOutline = _priceTag.GetComponent<Outline>();
            _priceText = Required<Text>(_priceTag.transform, "文本");
            _pausedFlag = Required<Image>(_root, "暂停遮罩");
            _pausedText = Required<Text>(_pausedFlag.transform, "文本");

            _sceneBound = true;
            _hintTemplate = _hint.text;
            _tagTemplate = _tagNets.text == "持仓 0 张" ? "持仓 {count} 张" : _tagNets.text;
            _pausedTemplate = _pausedText.text == "暂 停 中" ? "暂 停 中" : _pausedText.text;
            _frozenTemplate = _pausedText.text == "暂 停 中" ? "市 场 冻 结" : _pausedText.text;

            CaptureOffset(_hint.rectTransform);
            CaptureOffset(_halo.rectTransform);
            CaptureOffset(_dot.rectTransform);
            CaptureOffset(_priceTag.rectTransform);
            _sceneHaloSize = _halo.rectTransform.sizeDelta;

            if (_graphic == null) throw new MissingComponentException("场景曲线对象缺少 ChartGraphic。");
            _graphic.View = this;
            _graphic.raycastTarget = false;

            BindLabelGroup("Y轴标签", _yLabels);
            BindLabelGroup("X轴标签", _xLabels);
            BindLabelGroup("丢失线标签", _wipeLabels);

            for (int i = 0; i < _yLabels.Count; i++) CaptureOffset(_yLabels[i].rectTransform);
            for (int i = 0; i < _xLabels.Count; i++) CaptureOffset(_xLabels[i].rectTransform);
            for (int i = 0; i < _wipeLabels.Count; i++) CaptureOffset(_wipeLabels[i].rectTransform);

            _halo.gameObject.SetActive(false);
            _dot.gameObject.SetActive(false);
            _priceTag.gameObject.SetActive(false);
            _pausedFlag.gameObject.SetActive(false);
            return true;
        }

        private void CaptureOffset(RectTransform rt)
        {
            if (rt != null) _sceneOffsets[rt] = rt.anchoredPosition;
        }

        private void PlaceDynamic(RectTransform rt, Vector2 calculatedPosition)
        {
            Vector2 offset;
            rt.anchoredPosition = calculatedPosition + (_sceneOffsets.TryGetValue(rt, out offset) ? offset : Vector2.zero);
        }

        private void BindLabelGroup(string path, List<Text> target)
        {
            var group = _labels.Find(path);
            if (group == null) return;
            for (int i = 0; i < group.childCount; i++)
            {
                var text = group.GetChild(i).GetComponent<Text>();
                if (text != null) target.Add(text);
            }
        }

        private static T Required<T>(Transform root, string path) where T : Component
        {
            var child = root.Find(path);
            if (child == null || child.GetComponent<T>() == null)
                throw new MissingComponentException("曲线区缺少场景组件：" + path + " / " + typeof(T).Name);
            return child.GetComponent<T>();
        }

        private void MakeLegend()
        {
            var box = UiKit.Rect("图例", _root);
            UiKit.Fixed(box, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-12f, -10f), 250f, 20f);

            var dash = UiKit.Block("红虚线", box, Palette.Red);
            UiKit.Fixed(dash.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-236f, 0f), 14f, 2f);

            var loss = Txt.Make(box, "丢失线", 11, Palette.Muted, TextAnchor.MiddleLeft);
            UiKit.Fixed(loss.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-216f, 0f), 140f, 18f);
            loss.text = "买入价 ×0.7 丢失线";

            var dash2 = UiKit.Block("绿线", box, Palette.Green);
            UiKit.Fixed(dash2.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-70f, 0f), 14f, 2f);

            var bonus = Txt.Make(box, "200", 11, Palette.Muted, TextAnchor.MiddleLeft);
            UiKit.Fixed(bonus.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-50f, 0f), 40f, 18f);
            bonus.text = "200";
        }

        public void Resize(float w, float h)
        {
            _w = Mathf.Max(320f, w);
            _h = Mathf.Max(150f, h);
        }

        private static float NiceStep(float range, int target)
        {
            float raw = range / target;
            float[] steps = { 1f, 2f, 5f, 10f, 20f, 25f, 50f, 100f, 200f, 500f };
            for (int i = 0; i < steps.Length; i++) if (steps[i] >= raw) return steps[i];
            return 1000f;
        }

        /// <summary>局部坐标（原点左下）→ 中心锚点的 anchoredPosition。</summary>
        private Vector2 Local(float x, float y)
        {
            return new Vector2(x - _w * 0.5f, y - _h * 0.5f);
        }

        /// <summary>
        /// 计算视图纵向范围（只根据已揭示的数据，避免剧透），并对变化做平滑。
        /// 注意：必须在 BuildMesh 之前调用 —— 网格是在本帧稍后的 Canvas 重建里生成的。
        /// </summary>
        private void CalculateView(GameState s)
        {
            int n = s.Path != null ? Num.Clamp(s.Revealed, 1, s.Path.N) : 1;

            float lo = 48f, hi = 108f;
            if (s.Path != null)
            {
                for (int i = 0; i < n; i++)
                {
                    float v = s.Path.Arr[i] * MarketPath.ShockFactor(s, i * Cfg.Tick);
                    if (v < lo) lo = v;
                    if (v > hi) hi = v;
                }
            }
            float pad = (hi - lo) * 0.12f + 2f;
            lo -= pad;
            hi += pad;

            if (!s.View.Init)
            {
                s.View.Lo = lo;
                s.View.Hi = hi;
                s.View.Init = true;
            }
            else
            {
                s.View.Lo += (lo - s.View.Lo) * 0.14f;
                s.View.Hi += (hi - s.View.Hi) * 0.14f;
            }
        }

        // =====================================================================
        //  每帧刷新（文字与图标层）
        // =====================================================================

        public void Draw(GameState s, float now, bool hasGauge)
        {
            float pw = _w - PadL - PadR, ph = _h - PadT - PadB;
            if (pw <= 10f || ph <= 10f) return;

            CalculateView(s);
            _graphic.SetVerticesDirty();

            float vlo = s.View.Lo, vhi = s.View.Hi;
            float X(float t) { return PadL + pw * Num.Clamp(t / Cfg.Duration, 0f, 1f); }
            float Y(float p) { return _h - PadT - ph * (1f - Num.Clamp((p - vlo) / (vhi - vlo), -0.5f, 1.5f)); }

            // ---- 价格刻度 ----
            float step = NiceStep(vhi - vlo, 5);
            float start = Mathf.Ceil(vlo / step) * step;
            int usedY = 0;
            for (float p = start; p <= vhi && usedY < 24; p += step)
            {
                float y = Y(p);
                if (y < PadT - 6f || y > _h - PadB + 6f) continue;
                Text t = YLabel(usedY++);
                t.gameObject.SetActive(true);
                t.text = Mathf.RoundToInt(p).ToString();
                PlaceDynamic(t.rectTransform, Local(PadL - 8f - 26f, y));
            }
            for (int i = usedY; i < _yLabels.Count; i++) _yLabels[i].gameObject.SetActive(false);

            // ---- 时间刻度 ----
            int usedX = 0;
            for (float t = 0f; t <= Cfg.Duration && usedX < 16; t += 15f)
            {
                Text lab = XLabel(usedX++);
                lab.gameObject.SetActive(true);
                lab.text = Mathf.RoundToInt(t) + "s";
                PlaceDynamic(lab.rectTransform, Local(X(t), PadB - 4f));
            }
            for (int i = usedX; i < _xLabels.Count; i++) _xLabels[i].gameObject.SetActive(false);

            // ---- 开盘前提示 ----
            bool hasCurve = s.Path != null;
            _hint.gameObject.SetActive(!hasCurve);
            if (!hasCurve)
            {
                _hint.text = _hintTemplate;
                PlaceDynamic(_hint.rectTransform, Local(PadL + pw * 0.5f, PadT + ph * 0.5f));
            }

            _tagNets.text = (_tagTemplate ?? string.Empty).Replace("{count}", s.Nets.Count.ToString());

            // ---- 每张网的丢失线文字 ----
            int usedW = 0;
            for (int k = 0; k < s.Nets.Count; k++)
            {
                var net = s.Nets[k];
                float wipe = net.Entry * Cfg.WipeRatio;
                float wy = Y(wipe);
                if (wy < PadT || wy > _h - PadB) continue;
                Text lab = WipeLabel(usedW++);
                lab.gameObject.SetActive(true);
                lab.text = "网#" + net.Id + " 丢失线 " + Num.F1(wipe);
                PlaceDynamic(lab.rectTransform, Local(X(net.TEnter) + 3f, wy - 9f));
            }
            for (int i = usedW; i < _wipeLabels.Count; i++) _wipeLabels[i].gameObject.SetActive(false);

            // ---- 当前点位 ----
            bool showDot = s.Phase != "preopen" && hasCurve;
            _halo.gameObject.SetActive(showDot);
            _dot.gameObject.SetActive(showDot);
            _priceTag.gameObject.SetActive(showDot);
            if (showDot)
            {
                float x = X(s.Elapsed), y = Y(s.Price);
                bool risk = false;
                for (int i = 0; i < s.Nets.Count; i++)
                {
                    var net = s.Nets[i];
                    if (net.Entry > 0f && s.Price / net.Entry <= Cfg.WipeRatio) { risk = true; break; }
                }

                float pulse = 1f + Mathf.Sin(now / 260f) * 0.25f;
                float d = 18f * pulse;
                _halo.rectTransform.sizeDelta = _sceneBound ? _sceneHaloSize * pulse : new Vector2(d, d);
                PlaceDynamic(_halo.rectTransform, Local(x, y));
                if (!_sceneBound) _halo.color = risk ? Palette.HaloRisk : Palette.HaloNormal;

                if (!_sceneBound) _dot.rectTransform.sizeDelta = new Vector2(7.2f, 7.2f);
                PlaceDynamic(_dot.rectTransform, Local(x, y));
                if (!_sceneBound) _dot.color = risk ? Palette.DotRisk : Palette.DotNormal;

                _priceText.text = Num.F1(s.Price);
                float tw = Mathf.Max(44f, _priceText.preferredWidth + 14f);
                if (!_sceneBound) _priceTag.rectTransform.sizeDelta = new Vector2(tw, 18f);
                float actualWidth = _sceneBound ? _priceTag.rectTransform.rect.width : tw;
                float actualHeight = _sceneBound ? _priceTag.rectTransform.rect.height : 18f;
                float bx = Mathf.Min(x + 10f, PadL + pw - actualWidth);
                float by = Num.Clamp(y, PadT + 2f, _h - PadB - actualHeight);
                PlaceDynamic(_priceTag.rectTransform, Local(bx + actualWidth * 0.5f, by + actualHeight * 0.5f));
                if (!_sceneBound)
                {
                    var tagColor = risk ? Palette.PriceTagRisk : Palette.PriceTagOk;
                    _priceTag.color = tagColor;
                    if (_priceTagOutline != null) _priceTagOutline.effectColor = tagColor;
                }
            }

            // ---- 暂停 / 市场冻结 ----
            bool frozen = s.Phase == "paused" || hasGauge;
            _pausedFlag.gameObject.SetActive(frozen);
            if (frozen) _pausedText.text = s.Phase == "paused" ? _pausedTemplate : _frozenTemplate;
        }

        private Text YLabel(int i)
        {
            while (_yLabels.Count <= i)
            {
                var t = Txt.Make(_labels, "Y" + _yLabels.Count, 11, Palette.AxisText, TextAnchor.MiddleRight);
                UiKit.Fixed(t.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, 52f, 16f);
                _yLabels.Add(t);
            }
            return _yLabels[i];
        }

        private Text XLabel(int i)
        {
            while (_xLabels.Count <= i)
            {
                var t = Txt.Make(_labels, "X" + _xLabels.Count, 11, Palette.TimeText, TextAnchor.UpperCenter);
                UiKit.Fixed(t.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, 40f, 16f);
                _xLabels.Add(t);
            }
            return _xLabels[i];
        }

        private Text WipeLabel(int i)
        {
            while (_wipeLabels.Count <= i)
            {
                var t = Txt.Make(_labels, "W" + _wipeLabels.Count, 11, Palette.WipeLabel, TextAnchor.LowerLeft);
                UiKit.Fixed(t.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, 170f, 16f);
                t.gameObject.SetActive(false);
                _wipeLabels.Add(t);
            }
            return _wipeLabels[i];
        }

        // =====================================================================
        //  网格 / 阈值线 / 面积 / 折线 —— 全部在局部坐标（原点左下）里画
        // =====================================================================

        public void BuildMesh(VertexHelper vh, ChartGraphic g)
        {
            var gc = GameController.Instance;
            if (gc == null) return;
            var s = gc.S;

            // 直接以 Graphic 当前真实矩形为准，不再假定 pivot 或外部缓存尺寸。
            // 这样无论场景对象使用中心 pivot 还是左下 pivot，网格都严格落在自身范围内。
            Rect meshRect = g.rectTransform.rect;
            float W = meshRect.width, H = meshRect.height;
            float originX = meshRect.xMin, originY = meshRect.yMin;
            float pw = W - PadL - PadR, ph = H - PadT - PadB;
            if (pw <= 10f || ph <= 10f) return;

            float vlo = s.View.Lo, vhi = s.View.Hi;
            float X(float t) { return originX + PadL + pw * Num.Clamp(t / Cfg.Duration, 0f, 1f); }
            float Y(float p) { return originY + H - PadT - ph * (1f - Num.Clamp((p - vlo) / (vhi - vlo), -0.5f, 1.5f)); }

            float top = originY + H - PadT;
            float bottom = originY + PadB;
            float plotLeft = originX + PadL;

            // ---- 横向网格 ----
            float step = NiceStep(vhi - vlo, 5);
            float start = Mathf.Ceil(vlo / step) * step;
            for (float p = start; p <= vhi; p += step)
            {
                float y = Y(p);
                if (y < bottom - 1f || y > top + 1f) continue;
                g.Line(vh, new Vector2(plotLeft, y), new Vector2(plotLeft + pw, y), 1f, Palette.GridLine);
            }

            // ---- 纵向时间网格 ----
            for (float t = 0f; t <= Cfg.Duration; t += 15f)
            {
                float x = X(t);
                g.Line(vh, new Vector2(x, bottom), new Vector2(x, top), 1f, Palette.GridLine);
            }

            // ---- 阈值线 ----
            Threshold(vh, g, Y, plotLeft, pw, bottom, top, Cfg.BonusLine, vlo, vhi, Palette.BonusLine);
            Threshold(vh, g, Y, plotLeft, pw, bottom, top, Cfg.MidLine, vlo, vhi, Palette.MidLine);

            // ---- 买入参考线 / 丢失线 ----
            for (int k = 0; k < s.Nets.Count; k++)
            {
                var net = s.Nets[k];
                float ey = Y(net.Entry);
                if (ey >= bottom - 20f && ey <= top + 20f)
                {
                    bool win = s.Price >= net.Entry;
                    g.DashedLine(vh, new Vector2(X(net.TEnter), ey), new Vector2(X(s.Elapsed), ey), 1.2f,
                                 win ? Palette.EntryWin : Palette.EntryLose, 3f, 4f);
                }

                float wy = Y(net.Entry * Cfg.WipeRatio);
                if (wy >= bottom && wy <= top)
                {
                    g.DashedLine(vh, new Vector2(X(net.TEnter), wy), new Vector2(X(s.Elapsed), wy), 1f,
                                 Palette.WipeLine, 2f, 5f);
                }
            }

            if (s.Path == null) return;

            int n = Num.Clamp(s.Revealed, 1, s.Path.N);
            if (n < 2) return;

            // 叠加冲击后的实际价格（基准路径本身一个点都不动）
            var arr = new float[n];
            for (int i = 0; i < n; i++) arr[i] = s.Path.Arr[i] * MarketPath.ShockFactor(s, i * Cfg.Tick);

            // ---- 面积填充：顶点色在"曲线点→基线"之间插值，得到上亮下透的效果 ----
            var fillTop = Palette.AreaTop;
            var fillBot = Palette.AreaBottom;
            for (int i = 1; i < n; i++)
            {
                int fillStart = vh.currentVertCount;
                float x0 = X((i - 1) * Cfg.Tick), x1 = X(i * Cfg.Tick);
                g.AddVertRaw(vh, new Vector2(x0, bottom), fillBot);
                g.AddVertRaw(vh, new Vector2(x0, Mathf.Clamp(Y(arr[i - 1]), bottom, top)), fillTop);
                g.AddVertRaw(vh, new Vector2(x1, Mathf.Clamp(Y(arr[i]), bottom, top)), fillTop);
                g.AddVertRaw(vh, new Vector2(x1, bottom), fillBot);
                vh.AddTriangle(fillStart, fillStart + 1, fillStart + 2);
                vh.AddTriangle(fillStart, fillStart + 2, fillStart + 3);
            }

            // ---- 折线：涨绿跌红分段 ----
            int idx = 1;
            while (idx < n)
            {
                bool up = arr[idx] >= arr[idx - 1];
                Color seg = up ? Palette.UpLine : Palette.DownLine;
                Vector2 prev = new Vector2(X((idx - 1) * Cfg.Tick), Y(arr[idx - 1]));
                int j = idx;
                while (j < n)
                {
                    bool upJ = arr[j] >= arr[j - 1];
                    if (upJ != up) break;
                    Vector2 cur = new Vector2(X(j * Cfg.Tick), Y(arr[j]));
                    g.Line(vh, prev, cur, 2f, seg);
                    prev = cur;
                    j++;
                }
                idx = j;
            }

            // ---- 入场点标记 ----
            for (int k = 0; k < s.Nets.Count; k++)
            {
                var net = s.Nets[k];
                float x = X(net.TEnter);
                float y = Y(net.Entry);
                var c = net.Color;
                g.Tri(vh, new Vector2(x, y - 3f), new Vector2(x - 5f, y - 11f), new Vector2(x + 5f, y - 11f), c);
                g.Ring(vh, new Vector2(x, y), 3.2f, 1.6f, c);
            }

            // ---- 当前时刻竖线 ----
            if (s.Phase != "preopen")
            {
                float x = X(s.Elapsed);
                g.DashedLine(vh, new Vector2(x, bottom), new Vector2(x, top), 1f, Palette.MarkerLine, 2f, 4f);
            }
        }

        private static void Threshold(VertexHelper vh, ChartGraphic g, System.Func<float, float> Y,
                                      float padL, float pw, float bottom, float top,
                                      float price, float vlo, float vhi, Color c)
        {
            if (price < vlo || price > vhi) return;
            float y = Y(price);
            g.DashedLine(vh, new Vector2(padL, y), new Vector2(padL + pw, y), 1f, c, 6f, 5f);
        }
    }
}
