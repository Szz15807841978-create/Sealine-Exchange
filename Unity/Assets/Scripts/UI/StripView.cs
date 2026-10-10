using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Fishing
{
    /// <summary>持仓条：对应 index.html 的 .stripbar。每张网一个小胶囊，实时盈亏一直可见。</summary>
    public class StripView
    {
        private class Chip
        {
            public int NetId;
            public RectTransform Root;
            public Image Dot;
            public Text Number;
            public Text Pl;
            public Image Line;
            public Color BaseOutline;
        }

        private RectTransform _root;
        private RectTransform _strip;
        private Button _posButton;
        private GameObject _chipTemplate;
        private readonly List<Chip> _chips = new List<Chip>();

        public StripView(RectTransform parent)
        {
            _root = parent;
            if (TryBindSceneObjects()) return;

            var row = UiKit.Rect("行", _root);
            UiKit.Stretch(row, 12f, 7f, 12f, 7f);
            UiKit.Row(row, 10f);

            // 网页版会换行；主界面在 Unity 中是固定 46 高，因此这里用横向滚动
            // 保证持仓再多也不会把“持仓管理”按钮和后续行挤出屏幕。
            var viewport = UiKit.Rect("胶囊视口", row);
            var stripLe = UiKit.Size(viewport, -1f, 32f, 1f);
            stripLe.minWidth = 120f;
            viewport.gameObject.AddComponent<RectMask2D>();

            _strip = UiKit.Rect("胶囊区", viewport);
            _strip.anchorMin = new Vector2(0f, 0.5f);
            _strip.anchorMax = new Vector2(0f, 0.5f);
            _strip.pivot = new Vector2(0f, 0.5f);
            _strip.anchoredPosition = Vector2.zero;
            _strip.sizeDelta = new Vector2(0f, 32f);
            var h = UiKit.Row(_strip, 7f);
            h.childAlignment = TextAnchor.MiddleLeft;
            _strip.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = _strip;
            scroll.horizontal = true;
            scroll.vertical = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.inertia = true;
            scroll.scrollSensitivity = 24f;

            _posButton = Widgets.MakeButton(row, "持仓管理", Widgets.Hue.Cyan, 12, 96f, 32f,
                () => { if (GameController.Instance != null) GameController.Instance.OpenPosWindow(); });
        }

        private bool TryBindSceneObjects()
        {
            var strip = _root.Find("行/胶囊视口/胶囊区") as RectTransform;
            var template = _root.Find("行/胶囊视口/胶囊区/持仓胶囊模板");
            var button = _root.Find("行/持仓管理");
            if (strip == null || template == null || button == null) return false;

            _strip = strip;
            _chipTemplate = template.gameObject;
            _chipTemplate.SetActive(false);
            _posButton = button.GetComponent<Button>();
            if (_posButton == null) throw new MissingComponentException("持仓管理场景对象缺少 Button。");
            _posButton.onClick.RemoveAllListeners();
            _posButton.onClick.AddListener(() =>
            {
                if (GameController.Instance != null) GameController.Instance.OpenPosWindow();
            });
            return true;
        }

        private Chip CreateChip()
        {
            if (_chipTemplate != null)
            {
                var clone = Object.Instantiate(_chipTemplate, _strip, false);
                clone.name = "网";
                var c = new Chip
                {
                    Root = clone.transform as RectTransform,
                    Line = clone.GetComponent<Image>(),
                    Dot = Required<Image>(clone.transform, "点"),
                    Number = EnsureText(clone.transform, "编号", "网#1", TextAnchor.MiddleLeft),
                    Pl = EnsureText(clone.transform, "盈亏", "+0.0", TextAnchor.MiddleRight)
                };
                var outline = clone.GetComponent<Outline>();
                c.BaseOutline = outline != null ? outline.effectColor : Palette.LineSoft;
                var btn = clone.GetComponent<Button>();
                if (btn == null) btn = clone.AddComponent<Button>();
                btn.targetGraphic = c.Line;
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() =>
                {
                    if (GameController.Instance != null) GameController.Instance.OpenPosWindow();
                });
                clone.SetActive(true);
                return c;
            }

            var runtime = new Chip();
            var img = UiKit.Outline("网", _strip, Palette.ChipBg, Palette.LineSoft);
            runtime.Root = img.rectTransform;
            runtime.Line = img;
            runtime.BaseOutline = Palette.LineSoft;
            UiKit.Size(runtime.Root, 116f, 30f);
            UiKit.Row(runtime.Root, 5f, new RectOffset(9, 9, 0, 0));
            runtime.Dot = UiKit.Block("点", runtime.Root, Palette.Cyan);
            UiKit.Size(runtime.Dot.rectTransform, 8f, 8f);
            runtime.Number = Txt.Make(runtime.Root, "编号", 12, Palette.Muted, TextAnchor.MiddleLeft);
            UiKit.Size(runtime.Number.rectTransform, 42f, 18f);
            runtime.Pl = Txt.Make(runtime.Root, "盈亏", 12, Palette.Text, TextAnchor.MiddleRight, FontStyle.Bold);
            UiKit.Size(runtime.Pl.rectTransform, 44f, 18f);
            var runtimeButton = img.gameObject.AddComponent<Button>();
            runtimeButton.targetGraphic = img;
            runtimeButton.onClick.AddListener(() =>
            {
                if (GameController.Instance != null) GameController.Instance.OpenPosWindow();
            });
            return runtime;
        }

        private static T Required<T>(Transform root, string path) where T : Component
        {
            var child = root.Find(path);
            if (child == null || child.GetComponent<T>() == null)
                throw new MissingComponentException("持仓条缺少场景组件：" + path + " / " + typeof(T).Name);
            return child.GetComponent<T>();
        }

        private static Text EnsureText(Transform root, string path, string fallback, TextAnchor alignment)
        {
            var child = root.Find(path);
            if (child == null) child = FindDeep(root, path.Substring(path.LastIndexOf('/') + 1));
            if (child == null)
            {
                var go = new GameObject(path.Substring(path.LastIndexOf('/') + 1), typeof(RectTransform));
                go.transform.SetParent(root, false);
                child = go.transform;
                Debug.LogWarning("持仓条场景文字对象缺失，已在运行时补回：" + path, root);
            }
            var text = child.GetComponent<Text>();
            if (text != null) return text;
            text = child.gameObject.AddComponent<Text>();
            text.font = FontUtil.Get();
            text.fontSize = 12;
            text.color = Palette.Text;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.text = fallback;
            Debug.LogWarning("持仓条场景文字组件缺失，已在运行时补回：" + path, child);
            return text;
        }

        private static Transform FindDeep(Transform root, string name)
        {
            for (int i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (child.name == name) return child;
                var found = FindDeep(child, name);
                if (found != null) return found;
            }
            return null;
        }

        public void Refresh(GameState s)
        {
            // 需要几个胶囊就补几个
            while (_chips.Count < s.Nets.Count)
            {
                var c = CreateChip();
                _chips.Add(c);
            }

            for (int i = 0; i < _chips.Count; i++)
            {
                var c = _chips[i];
                if (i >= s.Nets.Count)
                {
                    c.Root.gameObject.SetActive(false);
                    continue;
                }

                var net = s.Nets[i];
                c.Root.gameObject.SetActive(true);
                c.Root.SetSiblingIndex(_chipTemplate != null ? i + 1 : i);
                c.NetId = net.Id;
                c.Dot.color = net.Color;
                c.Number.text = "网#" + net.Id;

                float pl = s.Price - net.Entry;
                c.Pl.text = Num.Signed(pl);
                c.Pl.color = pl >= 0f ? Palette.GreenSoft : Palette.RedSoft;

                var st = GameController.StatusOf(s.Price, net.Entry);
                bool risk = st.Kind == "risk";
                var ol = c.Line.GetComponent<Outline>();
                if (ol != null) ol.effectColor = risk ? Palette.Hex("#ef4444", 0.6f) : c.BaseOutline;
            }
        }
    }

    /// <summary>底部操作区：对应 index.html 的 .controls。</summary>
    public class ControlsView
    {
        private RectTransform _root;
        private Button _pause;
        private Button _all;
        private Image _cdFill;
        private RectTransform _cdFillRect;
        private Text _cdText;
        private Button _endDay;
        private Text _hint;
        private string _pauseLabel;
        private string _resumeLabel;

        public ControlsView(RectTransform parent)
        {
            _root = parent;
            if (TryBindSceneObjects()) return;

            var row = UiKit.Rect("行", _root);
            UiKit.Stretch(row, 12f, 8f, 12f, 8f);
            UiKit.Row(row, 10f);

            _pause = Widgets.MakeButton(row, "暂停", Widgets.Hue.Normal, 13, 78f, 32f,
                () => { if (GameController.Instance != null) GameController.Instance.TogglePause(); });

            _all = Widgets.MakeButton(row, "全部收网", Widgets.Hue.Cyan, 13, 100f, 32f, () =>
            {
                var gc = GameController.Instance;
                if (gc == null) return;
                if (gc.S.Phase == "running") { gc.Toast("先暂停"); return; }
                gc.CollectAll();
            });

            var cdWrap = UiKit.Rect("CD", row);
            UiKit.Size(cdWrap, 190f, 32f, 1f);
            var cdRow = UiKit.Row(cdWrap, 9f);
            cdRow.childAlignment = TextAnchor.MiddleLeft;

            var cdBg = UiKit.Outline("CD条", cdWrap, Palette.ChipBg, Palette.LineSoft);
            UiSkinTheme.ApplyBar(cdBg, false);
            UiKit.Size(cdBg.rectTransform, 110f, 9f, 1f);
            var fillHost = UiKit.Rect("填充区", cdBg.rectTransform);
            UiKit.Stretch(fillHost, 1f, 1f, 1f, 1f);
            _cdFill = UiKit.Gradient("填充", fillHost, Palette.GaugeTimerB, Palette.GaugeTimerA, 0f);
            UiSkinTheme.ApplyBar(_cdFill, true);
            var cdGradient = _cdFill.GetComponent<Gradient2>();
            if (cdGradient && UiSkinTheme.Active) cdGradient.enabled = false;
            _cdFillRect = _cdFill.rectTransform;
            _cdFillRect.anchorMin = new Vector2(0f, 0f);
            _cdFillRect.anchorMax = new Vector2(1f, 1f);
            _cdFillRect.offsetMin = Vector2.zero;
            _cdFillRect.offsetMax = Vector2.zero;

            _cdText = Txt.Make(cdWrap, "CD文字", 12, Palette.Muted, TextAnchor.MiddleLeft);
            UiKit.Size(_cdText.rectTransform, 74f, 20f);

            _endDay = Widgets.MakeButton(row, "结束今天", Widgets.Hue.Ghost, 13, 86f, 32f,
                () => { if (GameController.Instance != null) GameController.Instance.EndDay(); });

            _hint = Txt.Make(row, "提示", 12, Palette.Muted, TextAnchor.MiddleLeft);
            UiKit.Size(_hint.rectTransform, 260f, 20f, 1f);

            _pauseLabel = "暂停";
            _resumeLabel = "继续";
        }

        private bool TryBindSceneObjects()
        {
            var row = _root.Find("行");
            if (row == null || row.Find("暂停") == null || row.Find("CD/CD条/填充区/填充") == null) return false;

            _pause = Required<Button>(row, "暂停");
            _all = Required<Button>(row, "全部收网");
            _cdFill = Required<Image>(row, "CD/CD条/填充区/填充");
            UiSkinTheme.ApplyBar(Required<Image>(row, "CD/CD条"), false);
            UiSkinTheme.ApplyBar(_cdFill, true);
            _cdFillRect = _cdFill.rectTransform;
            _cdText = EnsureText(row, "CD/CD文字", "可收网", TextAnchor.MiddleLeft);
            _endDay = Required<Button>(row, "结束今天");
            _hint = EnsureText(row, "提示", "空格 暂停", TextAnchor.MiddleLeft);

            var pauseText = Widgets.LabelOf(_pause);
            _pauseLabel = pauseText != null && !string.IsNullOrEmpty(pauseText.text) ? pauseText.text : "暂停";
            _resumeLabel = _pauseLabel == "暂停" ? "继续" : _pauseLabel;

            _pause.onClick.RemoveAllListeners();
            _all.onClick.RemoveAllListeners();
            _endDay.onClick.RemoveAllListeners();
            _pause.onClick.AddListener(() =>
            {
                if (GameController.Instance != null) GameController.Instance.TogglePause();
            });
            _all.onClick.AddListener(() =>
            {
                var gc = GameController.Instance;
                if (gc == null) return;
                if (gc.S.Phase == "running") { gc.Toast("先暂停"); return; }
                gc.CollectAll();
            });
            _endDay.onClick.AddListener(() =>
            {
                if (GameController.Instance != null) GameController.Instance.EndDay();
            });
            return true;
        }

        private static T Required<T>(Transform root, string path) where T : Component
        {
            var child = root.Find(path);
            if (child == null || child.GetComponent<T>() == null)
                throw new MissingComponentException("操作区缺少场景组件：" + path + " / " + typeof(T).Name);
            return child.GetComponent<T>();
        }

        private static Text EnsureText(Transform root, string path, string fallback, TextAnchor alignment)
        {
            var child = root.Find(path);
            if (child == null) child = FindDeep(root, path.Substring(path.LastIndexOf('/') + 1));
            if (child == null)
            {
                string parentPath = path.Contains("/") ? path.Substring(0, path.LastIndexOf('/')) : string.Empty;
                var parent = string.IsNullOrEmpty(parentPath) ? root : root.Find(parentPath);
                if (parent == null) parent = root;
                var go = new GameObject(path.Substring(path.LastIndexOf('/') + 1), typeof(RectTransform));
                go.transform.SetParent(parent, false);
                child = go.transform;
                Debug.LogWarning("操作区场景文字对象缺失，已在运行时补回：" + path, root);
            }
            var text = child.GetComponent<Text>();
            if (text != null) return text;
            text = child.gameObject.AddComponent<Text>();
            text.font = FontUtil.Get();
            text.fontSize = 12;
            text.color = Palette.Muted;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.text = fallback;
            Debug.LogWarning("操作区场景文字组件缺失，已在运行时补回：" + path, child);
            return text;
        }

        private static Transform FindDeep(Transform root, string name)
        {
            for (int i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (child.name == name) return child;
                var found = FindDeep(child, name);
                if (found != null) return found;
            }
            return null;
        }

        public void Refresh(GameState s, bool hasGauge)
        {
            bool running = s.Phase == "running";
            bool paused = s.Phase == "paused";

            var pauseText = Widgets.LabelOf(_pause);
            pauseText.text = paused ? _resumeLabel : _pauseLabel;
            _pause.interactable = running || paused;

            bool canCollectAll = paused && s.Cd <= 0f && s.Nets.Count > 0;
            _all.interactable = canCollectAll;

            _endDay.interactable = running || paused;

            // CD 条
            float ratio;
            Color fillColor;
            if (s.Cd > 0f)
            {
                ratio = 100f * s.Cd / (s.CdTotal > 0.0001f ? s.CdTotal : 1f);
                fillColor = Palette.Amber;
                _cdText.text = "CD " + Num.F1(s.Cd) + "s";
            }
            else
            {
                ratio = 100f;
                fillColor = Palette.GreenSoft;
                _cdText.text = paused ? "可收网" : (s.Nets.Count > 0 ? "暂停后可收网" : "");
            }
            _cdFillRect.anchorMax = new Vector2(Num.Clamp(ratio, 0f, 100f) / 100f, 1f);
            _cdFillRect.offsetMax = Vector2.zero;
            _cdFill.color = fillColor;

            bool gauging = running && hasGauge;
            if (paused) _hint.text = "空格 继续 · 点击持仓管理收网";
            else if (gauging) _hint.text = "按 Q 停下指针";
            else if (running) _hint.text = "空格 暂停";
            else _hint.text = "";
        }
    }
}
