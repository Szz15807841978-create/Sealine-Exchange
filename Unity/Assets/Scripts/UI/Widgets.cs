using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Fishing
{
    /// <summary>可复用的小控件：按钮 / 数值框 / 标题 / 鱼篓胶囊。全部用代码搭。</summary>
    public static class Widgets
    {
        public enum Hue { Normal, Primary, Cyan, Green, Danger, Ghost }

        public static readonly Dictionary<string, string> ResultText = new Dictionary<string, string>
        {
            { "lost", "网丢失" }, { "win", "盈利归还" }, { "flat", "持平归还" }, { "bonus", "丰收" },
        };

        public static readonly Dictionary<string, string> ResultColor = new Dictionary<string, string>
        {
            { "lost", "#f87171" }, { "win", "#4ade80" }, { "bonus", "#4ade80" }, { "flat", "#94a3b8" },
        };

        // =====================================================================
        //  按钮
        // =====================================================================

        public static Button MakeButton(RectTransform parent, string label, Hue hue, int fontSize,
                                        float w, float h, Action onClick, TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            Color top, bottom, txtColor;
            switch (hue)
            {
                case Hue.Primary:
                    top = Palette.PrimaryTop; bottom = Palette.PrimaryBot; txtColor = Palette.BtnPrimaryText; break;
                case Hue.Cyan:
                    top = Palette.CyanTop; bottom = Palette.CyanBot; txtColor = Palette.BtnCyanText; break;
                case Hue.Green:
                    top = Palette.GreenTop; bottom = Palette.GreenBot; txtColor = Palette.BtnGreenText; break;
                case Hue.Danger:
                    top = Palette.DangerTop; bottom = Palette.DangerBot; txtColor = Palette.BtnDangerText; break;
                case Hue.Ghost:
                    top = new Color(0f, 0f, 0f, 0f); bottom = new Color(0f, 0f, 0f, 0f); txtColor = Palette.Text; break;
                default:
                    top = Palette.ButtonBg; bottom = Palette.ButtonBg; txtColor = Palette.Text; break;
            }

            Image img;
            if (hue == Hue.Ghost)
            {
                img = UiKit.Block(label, parent, new Color(1f, 1f, 1f, 0f));
            }
            else
            {
                img = UiKit.Gradient(label, parent, top, bottom);
            }

            if (UiSkinTheme.Active)
            {
                var gradient = img.GetComponent<Gradient2>();
                if (gradient) gradient.enabled = false;
                UiSkinTheme.ApplyButton(img, hue);
            }

            if (w >= 0f || h >= 0f)
            {
                if (w >= 0f && h >= 0f) UiKit.Fixed(img.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, w, h);
                UiKit.Size(img.rectTransform, w, h);
                UiKit.MinSize(img.rectTransform, w, h);
            }

            if (hue != Hue.Ghost)
            {
                var ol = img.gameObject.AddComponent<Outline>();
                ol.effectColor = UiSkinTheme.Active
                    ? new Color(Palette.Line.r, Palette.Line.g, Palette.Line.b, 0.18f)
                    : Darken(bottom);
                ol.effectDistance = new Vector2(1f, -1f);
            }

            var text = Txt.Make(img.rectTransform, "文本", fontSize, txtColor, anchor);
            text.text = label;
            UiKit.Stretch((RectTransform)text.transform, 8f, 0f, 8f, 0f);

            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.transition = Selectable.Transition.ColorTint;
            var cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
            cb.pressedColor = new Color(0.88f, 0.88f, 0.88f, 1f);
            cb.selectedColor = Color.white;
            cb.disabledColor = new Color(1f, 1f, 1f, 0.42f);
            cb.fadeDuration = 0.08f;
            btn.colors = cb;

            if (onClick != null) btn.onClick.AddListener(() => onClick());
            return btn;
        }

        private static Color Darken(Color c)
        {
            return new Color(c.r * 0.6f, c.g * 0.6f, c.b * 0.6f, c.a);
        }

        /// <summary>把按钮的文字取出来（改标题用）。</summary>
        public static Text LabelOf(Button b)
        {
            return b.GetComponentInChildren<Text>();
        }

        // =====================================================================
        //  数值方框（对应 .box）
        // =====================================================================

        public class Box
        {
            public RectTransform Root;
            public Text Key;
            public Text Value;
            public Image Line;
        }

        public static Box MakeBox(RectTransform parent, string key, float w, float h)
        {
            var box = new Box();
            var img = UiKit.Outline(key, parent, Palette.BoxBg, Palette.LineSoft);
            box.Line = img;
            box.Root = img.rectTransform;
            UiKit.Size(box.Root, w, h);

            box.Key = Txt.Make(box.Root, "键", 11, Palette.Muted, TextAnchor.UpperLeft);
            box.Key.text = key;
            UiKit.Stretch((RectTransform)box.Key.transform, 11f, 6f, 8f, h - 24f);

            box.Value = Txt.Make(box.Root, "值", 17, Palette.Text, TextAnchor.LowerLeft, FontStyle.Bold);
            UiKit.Stretch((RectTransform)box.Value.transform, 11f, 22f, 8f, 6f);
            return box;
        }

        public static void SetBox(Box b, string value, Color color)
        {
            b.Value.text = value;
            b.Value.color = color;
            var ol = b.Line.GetComponent<Outline>();
            if (ol != null) ol.effectColor = Palette.LineSoft;
        }

        // =====================================================================
        //  标题 / 分隔线
        // =====================================================================

        public static Text MakeCaption(RectTransform parent, string text, float h = 28f)
        {
            var t = Txt.Make(parent, "小标题", 13, Palette.Muted, TextAnchor.LowerLeft, FontStyle.Bold);
            UiKit.Size(t.rectTransform, -1f, h);
            t.text = text;
            return t;
        }

        public static Image MakeDivider(RectTransform parent, float h = 1f, Color? c = null)
        {
            var img = UiKit.Block("分隔线", parent, c ?? Palette.LineSoft);
            UiKit.Size(img.rectTransform, -1f, h);
            img.raycastTarget = false;
            return img;
        }

        // =====================================================================
        //  鱼篓胶囊（对应 .fish）
        // =====================================================================

        public class FishChip
        {
            public RectTransform Root;
            public Image Line;
            public Text Body;
        }

        public static FishChip MakeFishChip(RectTransform parent)
        {
            var chip = new FishChip();
            var img = UiKit.Outline("鱼", parent, Palette.BoxBg, Palette.LineSoft);
            chip.Line = img;
            chip.Root = img.rectTransform;
            UiKit.Size(chip.Root, 132f, 30f);

            chip.Body = Txt.Make(chip.Root, "文本", 12, Palette.Text, TextAnchor.MiddleLeft);
            UiKit.Stretch((RectTransform)chip.Body.transform, 10f, 0f, 8f, 0f);
            return chip;
        }

        public static void SetFishChip(FishChip chip, FishDef f, int count, bool showValue)
        {
            string s = f.Emoji + " " + f.Name + " <b>" + count + "</b>";
            if (showValue) s += " <color=#64748b>" + f.Value + "</color>";
            chip.Body.text = s;
            var ol = chip.Line.GetComponent<Outline>();
            if (ol != null) ol.effectColor = Palette.Hex(f.Color, 0.33f);
        }
    }
}
