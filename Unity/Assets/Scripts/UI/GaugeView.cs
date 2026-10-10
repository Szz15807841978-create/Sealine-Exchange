using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Fishing
{
    /// <summary>仪表盘小玩法：对应 index.html 里 #gauge 那块 UI 与 gaugeTick()。</summary>
    public class GaugeView
    {
        private const float TrackH = 26f;
        private const float PanelW = 600f;
        private const float PanelH = 78f;

        private readonly RectTransform _root;
        private readonly RectTransform _track;
        private readonly RectTransform _needle;
        private readonly RectTransform _segHost;
        private readonly Text _title;
        private readonly Image _timerFill;
        private readonly RectTransform _parent;
        private float _lastWidth = -1f;

        private readonly List<Image> _segs = new List<Image>();

        public GaugeView(RectTransform parent)
        {
            _parent = parent;
            var editableAnchor = parent.Find("仪表盘锚点") as RectTransform;
            _root = UiKit.Outline("仪表盘", editableAnchor ? editableAnchor : parent, Palette.GaugeBg, Palette.GaugeBorder).rectTransform;
            if (editableAnchor) UiKit.Stretch(_root);
            else UiKit.Fixed(_root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 14f), PanelW, PanelH);
            _root.gameObject.SetActive(false);

            // 轨道
            var trackBg = UiKit.Outline("轨道", _root, Palette.GaugeTrackBg, Palette.GaugeTrackLine);
            _track = trackBg.rectTransform;
            UiKit.Fixed(_track, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -11f), PanelW - 26f, TrackH);

            _segHost = UiKit.Rect("区段", _track);
            UiKit.Stretch(_segHost);

            _needle = UiKit.Block("指针", _track, Color.white).rectTransform;
            _needle.anchorMin = _needle.anchorMax = new Vector2(0f, 0.5f);
            _needle.pivot = new Vector2(0.5f, 0.5f);
            _needle.sizeDelta = new Vector2(3f, TrackH + 6f);
            _needle.anchoredPosition = Vector2.zero;

            // 底部：提示文字 + 倒计时条
            _title = Txt.Make(_root, "提示", 12, Palette.Muted, TextAnchor.MiddleLeft);
            UiKit.Fixed(_title.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(13f, 11f), 320f, 20f);

            var barBg = UiKit.Outline("计时条", _root, Palette.GaugeTrackBg, Palette.LineSoft);
            UiSkinTheme.ApplyBar(barBg, false);
            UiKit.Fixed(barBg.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-13f, 18f), 200f, 6f);

            var fillHost = UiKit.Rect("填充", barBg.rectTransform);
            UiKit.Stretch(fillHost);
            _timerFill = UiKit.Gradient("亮条", fillHost, Palette.GaugeTimerB, Palette.GaugeTimerA, 0f);
            UiSkinTheme.ApplyBar(_timerFill, true);
            var timerGradient = _timerFill.GetComponent<Gradient2>();
            if (timerGradient && UiSkinTheme.Active) timerGradient.enabled = false;
            _timerFill.rectTransform.anchorMin = new Vector2(0f, 0f);
            _timerFill.rectTransform.anchorMax = new Vector2(1f, 1f);
            _timerFill.rectTransform.offsetMin = Vector2.zero;
            _timerFill.rectTransform.offsetMax = Vector2.zero;

            // 点一下也能停指针
            var btn = _root.gameObject.AddComponent<Button>();
            btn.targetGraphic = _root.GetComponent<Image>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() => { if (GameController.Instance != null) GameController.Instance.PressGauge(); });
        }

        public bool Visible { get { return _root.gameObject.activeSelf; } }

        public void Show(bool on)
        {
            if (on) FitToParent();
            _root.gameObject.SetActive(on);
        }

        /// <summary>对应网页的 width:min(600px, 86%)，避免窄窗口下仪表盘溢出。</summary>
        private void FitToParent()
        {
            float parentW = _parent != null ? _parent.rect.width : 0f;
            var editableAnchor = _parent != null ? _parent.Find("仪表盘锚点") as RectTransform : null;
            if (editableAnchor != null)
            {
                float anchoredWidth = Mathf.Max(1f, editableAnchor.rect.width);
                _root.sizeDelta = Vector2.zero;
                _track.sizeDelta = new Vector2(Mathf.Max(1f, anchoredWidth - 26f), TrackH);
                _lastWidth = anchoredWidth;
                return;
            }
            float w = parentW > 1f ? Mathf.Min(PanelW, parentW * 0.86f) : PanelW;
            if (Mathf.Abs(w - _lastWidth) < 0.1f) return;

            _lastWidth = w;
            _root.sizeDelta = new Vector2(w, PanelH);
            _track.sizeDelta = new Vector2(Mathf.Max(1f, w - 26f), TrackH);
        }

        /// <summary>把洗好的区间布局画出来（对应 renderGaugeTrack）。</summary>
        public void BuildSegments(List<GaugeSegment> segs)
        {
            FitToParent();
            while (_segs.Count < segs.Count)
            {
                var img = UiKit.Block("段" + _segs.Count, _segHost, Color.white);
                img.raycastTarget = false;
                _segs.Add(img);
            }

            float trackW = _track.rect.width;
            if (trackW <= 1f) trackW = PanelW - 26f;

            for (int i = 0; i < _segs.Count; i++)
            {
                if (i >= segs.Count)
                {
                    _segs[i].gameObject.SetActive(false);
                    continue;
                }
                var seg = segs[i];
                var img = _segs[i];
                img.gameObject.SetActive(true);
                img.color = Palette.ForGauge(seg.Kind);
                var rt = img.rectTransform;
                rt.anchorMin = new Vector2(0f, 0f);
                rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 0.5f);
                rt.offsetMin = new Vector2(seg.From / 100f * trackW, 0f);
                rt.offsetMax = new Vector2(seg.To / 100f * trackW, 0f);
            }
        }

        /// <summary>本次判定后把没中的区段压暗（对应 renderGaugeVerdict）。</summary>
        public void ShowVerdict(List<GaugeSegment> segs, string kind)
        {
            for (int i = 0; i < _segs.Count && i < segs.Count; i++)
            {
                bool hit = segs[i].Kind == kind;
                var c = Palette.ForGauge(segs[i].Kind);
                c.a = hit ? 1f : 0.28f;
                _segs[i].color = c;
                var ol = _segs[i].GetComponent<Outline>();
                if (ol != null) ol.effectColor = hit ? Color.white : new Color(0f, 0f, 0f, 0f);
            }
        }

        public void ResetVerdictAlpha()
        {
            for (int i = 0; i < _segs.Count; i++)
            {
                var ol = _segs[i].GetComponent<Outline>();
                if (ol != null) ol.effectColor = new Color(0f, 0f, 0f, 0f);
            }
        }

        public void SetNeedle(float pos)
        {
            FitToParent();
            float trackW = _track.rect.width;
            if (trackW <= 1f) trackW = PanelW - 26f;
            _needle.anchoredPosition = new Vector2(pos / 100f * trackW, 0f);
        }

        public void SetTitle(string text, Color color)
        {
            _title.text = text;
            _title.color = color;
        }

        public void SetTimerFill(float ratio)
        {
            ratio = Num.Clamp(ratio, 0f, 1f);
            _timerFill.rectTransform.anchorMax = new Vector2(ratio, 1f);
            _timerFill.rectTransform.offsetMax = Vector2.zero;
        }
    }
}
