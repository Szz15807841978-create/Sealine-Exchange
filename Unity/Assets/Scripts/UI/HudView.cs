using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Fishing
{
    /// <summary>顶部状态栏：对应 index.html 的 &lt;header class="panel hud"&gt;。</summary>
    public class HudView
    {
        private class Chip
        {
            public RectTransform Root;
            public Image Bg;
            public Text Value;
            public float Width;
        }

        private readonly RectTransform _root;
        private readonly Text _title;
        private readonly Text _dayLabel;
        private readonly Text _timer;
        private readonly Image _timerBg;

        private readonly Chip _nets = new Chip();
        private readonly Chip _broken = new Chip();
        private readonly Chip _worms = new Chip();
        private readonly Chip _fish = new Chip();

        private const float Pad = 12f;
        private const float RightPad = 12f;
        private const float TimerWidth = 96f;

        public HudView(RectTransform parent)
        {
            _root = parent;
            if (TryBindSceneObjects(out _title, out _dayLabel, out _timerBg, out _timer)) return;

            UiKit.Clear(_root);
            var bg = UiKit.Gradient("底", _root, Palette.Panel, Palette.PanelBottom);
            UiSkinTheme.ApplyPanel(bg, detailed: true);
            var gradient = bg.GetComponent<Gradient2>();
            if (gradient && UiSkinTheme.Active) gradient.enabled = false;
            bg.raycastTarget = false;

            _title = Txt.Make(_root, "标题", 22, Palette.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
            _title.text = "鲸落";
            UiKit.Fixed(_title.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(Pad, 0f), 76f, 28f);

            _dayLabel = Txt.Make(_root, "天次", 13, Palette.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiKit.Fixed(_dayLabel.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(Pad + 82f, 0f), 155f, 22f);

            // 右上角倒计时
            _timerBg = UiKit.Outline("倒计时底", _root, Palette.ChipBg, Palette.LineSoft);
            UiKit.Fixed(_timerBg.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                        new Vector2(-RightPad, 0f), 104f, 28f);
            var timerKey = Txt.Make(_timerBg.rectTransform, "键", 12, Palette.Muted, TextAnchor.MiddleLeft);
            UiKit.Stretch((RectTransform)timerKey.transform, 12f, 0f, 54f, 0f);
            timerKey.text = "收市";
            _timer = Txt.Make(_timerBg.rectTransform, "值", 16, Palette.Text, TextAnchor.MiddleRight, FontStyle.Bold);
            UiKit.Stretch((RectTransform)_timer.transform, 40f, 0f, 10f, 0f);
        }

        private bool TryBindSceneObjects(out Text title, out Text day, out Image timerBg, out Text timer)
        {
            title = null; day = null; timerBg = null; timer = null;
            var titleTr = _root.Find("标题");
            var dayTr = _root.Find("天次");
            var timerTr = _root.Find("倒计时底");
            if (titleTr == null || dayTr == null || timerTr == null) return false;
            title = titleTr.GetComponent<Text>();
            day = dayTr.GetComponent<Text>();
            timerBg = timerTr.GetComponent<Image>();
            var valueTr = timerTr.Find("值");
            timer = valueTr != null ? valueTr.GetComponent<Text>() : null;
            if (!title || !day || !timerBg || !timer) return false;
            UiSkinTheme.ApplyPanel(timerBg, inset: true);
            return true;
        }

        private void BuildChip(Chip c, string key, ref float x, Color valueColor)
        {
            c.Width = 78f + key.Length * 0f;
            float w = 104f;
            c.Root = UiKit.Outline(key, _root, Palette.ChipBg, Palette.LineSoft).rectTransform;
            c.Bg = c.Root.GetComponent<Image>();
            UiKit.Fixed(c.Root, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(x, 0f), w, 28f);

            var k = Txt.Make(c.Root, "键", 12, Palette.Muted, TextAnchor.MiddleLeft);
            UiKit.Stretch((RectTransform)k.transform, 12f, 0f, w - 52f, 0f);
            k.text = key;

            c.Value = Txt.Make(c.Root, "值", 14, valueColor, TextAnchor.MiddleRight, FontStyle.Bold);
            UiKit.Stretch((RectTransform)c.Value.transform, w - 52f, 0f, 10f, 0f);

            x += w + 8f;
        }

        private void BuildFishChip(ref float x)
        {
            const float w = 104f;
            _fish.Root = UiKit.Outline("渔获", _root, Palette.ChipBg, Palette.LineSoft).rectTransform;
            _fish.Bg = _fish.Root.GetComponent<Image>();
            UiKit.Fixed(_fish.Root, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(x, 0f), w, 28f);

            var k = Txt.Make(_fish.Root, "键", 12, Palette.Muted, TextAnchor.MiddleLeft);
            UiKit.Stretch((RectTransform)k.transform, 12f, 0f, w - 52f, 0f);
            k.text = "渔获";

            _fish.Value = Txt.Make(_fish.Root, "值", 14, Palette.Text, TextAnchor.MiddleRight, FontStyle.Bold);
            UiKit.Stretch((RectTransform)_fish.Value.transform, w - 52f, 0f, 10f, 0f);

            var btn = _fish.Root.gameObject.AddComponent<Button>();
            btn.targetGraphic = _fish.Bg;
            btn.transition = Selectable.Transition.ColorTint;
            var cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = Color.Lerp(Palette.ChipBg, Palette.Hex("#3b82f6"), 0.35f);
            cb.pressedColor = cb.highlightedColor;
            cb.selectedColor = Color.white;
            cb.fadeDuration = 0.08f;
            btn.colors = cb;
            btn.onClick.AddListener(() => { if (GameController.Instance != null) GameController.Instance.OpenFishWindow(); });

            x += w + 8f;
        }

        public void Refresh(GameState s)
        {
            _dayLabel.text = "第 " + s.Day + " 天 · 第 " + s.Session + " 场";
            string txt;
            Color c = Palette.Text;
            if (s.Phase == "preopen")
            {
                txt = Num.F1(Cfg.Duration) + "s";
            }
            else if (s.Phase == "settlement" || s.Phase == "evening")
            {
                txt = "已收市";
            }
            else
            {
                float left = Mathf.Max(0f, Cfg.Duration - s.Elapsed);
                txt = Num.F1(left) + "s";
                if (left <= 10f) c = Palette.RedSoft;
                else if (left <= 30f) c = Palette.Amber;
            }
            _timer.text = txt;
            _timer.color = c;
        }
    }
}
