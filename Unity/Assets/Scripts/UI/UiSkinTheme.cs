using UnityEngine;
using UnityEngine.UI;

namespace Fishing
{
    /// <summary>
    /// Kenney UI Pack - Adventure（CC0）的运行时皮肤引用。
    /// 场景中的组件只保存 Sprite 引用；所有代码生成的 uGUI 都从 Active 取得同一套外观。
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class UiSkinTheme : MonoBehaviour
    {
        public Sprite Panel;
        public Sprite PanelFlat;
        public Sprite PanelDetail;
        public Sprite PanelInset;
        public Sprite PanelGrid;
        public Sprite ButtonAmber;
        public Sprite ButtonTeal;
        public Sprite ButtonNavy;
        public Sprite ButtonPaper;
        public Sprite BarTrack;
        public Sprite BarFill;

        [Header("Kenney 扩展资源库（可直接拖给其他 UI）")]
        public Sprite Banner;
        public Sprite ButtonClose;
        public Sprite ButtonDanger;
        public Sprite CheckboxOff;
        public Sprite CheckboxOn;
        public Sprite ProgressBlue;
        public Sprite ProgressGreen;
        public Sprite ProgressRed;
        public Sprite Scrollbar;
        public Sprite MinimapRing;
        public Sprite IconPause;
        public Sprite IconGear;
        public Sprite IconBasket;
        public Sprite IconWarning;
        public Sprite IconInformation;
        public Sprite IconCheck;
        public Sprite IconClose;

        public static UiSkinTheme Active { get; private set; }

        private void OnEnable() => Active = this;

        private void Start() => ApplyExistingHierarchy();

        private void OnDisable()
        {
            if (Active == this) Active = null;
        }

        /// <summary>给 Main.unity 中已经预先摆好的控件换皮；运行时新建控件由 UiKit / Widgets 处理。</summary>
        private void ApplyExistingHierarchy()
        {
            foreach (var button in GetComponentsInChildren<Button>(true))
            {
                if (button.GetComponentInParent<PaperInkUiScope>(true)) continue;
                var image = button.targetGraphic as Image ?? button.GetComponent<Image>();
                if (!image) continue;
                string n = button.name;
                Widgets.Hue hue = n.Contains("开始") || n.Contains("确认") || n.Contains("下网")
                    ? Widgets.Hue.Primary
                    : n.Contains("全部") || n.Contains("持仓") || n.Contains("收选中")
                        ? Widgets.Hue.Cyan
                        : n.Contains("结束") || n.Contains("关闭")
                            ? Widgets.Hue.Ghost
                            : Widgets.Hue.Normal;
                ApplyButton(image, hue);
            }

            foreach (var image in GetComponentsInChildren<Image>(true))
            {
                if (image.GetComponentInParent<PaperInkUiScope>(true)) continue;
                string n = image.name;
                if (n == "CD条" || n == "计时条") ApplyBar(image, false);
                else if (n == "填充" || n == "亮条") ApplyBar(image, true);
                else if (n == "状态栏") ApplyPanel(image, detailed: true);
                else if (n == "曲线区") ApplyGridPanel(image);
                else if (n == "数据条" || n == "持仓条" || n == "操作区" || n.Contains("胶囊模板"))
                    ApplyPanel(image, inset: n.Contains("胶囊"));
            }
        }

        public static void ApplyPanel(Image image, bool inset = false, bool detailed = false)
        {
            if (!image || !Active) return;
            Sprite sprite = detailed ? Active.PanelDetail : inset ? Active.PanelInset : Active.PanelFlat;
            ApplySliced(image, sprite, Color.white);
        }

        public static void ApplyGridPanel(Image image)
        {
            if (!image || !Active) return;
            ApplySliced(image, Active.PanelGrid ? Active.PanelGrid : Active.PanelFlat, Color.white);
        }

        public static void ApplyButton(Image image, Widgets.Hue hue)
        {
            if (!image || !Active) return;
            Sprite sprite;
            Color tint = Color.white;
            switch (hue)
            {
                case Widgets.Hue.Primary: sprite = Active.ButtonAmber; break;
                case Widgets.Hue.Cyan:
                case Widgets.Hue.Green: sprite = Active.ButtonTeal; break;
                case Widgets.Hue.Ghost: sprite = Active.ButtonPaper; break;
                case Widgets.Hue.Danger:
                    sprite = Active.ButtonDanger ? Active.ButtonDanger : Active.ButtonAmber;
                    tint = Active.ButtonDanger ? Color.white : new Color(1f, 0.76f, 0.70f, 1f);
                    break;
                default: sprite = Active.ButtonNavy; break;
            }
            ApplySliced(image, sprite, tint);
        }

        public static void ApplyBar(Image image, bool fill)
        {
            if (!image || !Active) return;
            ApplySliced(image, fill ? Active.BarFill : Active.BarTrack, Color.white);
        }

        private static void ApplySliced(Image image, Sprite sprite, Color tint)
        {
            if (!sprite) return;
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.fillCenter = true;
            image.pixelsPerUnitMultiplier = 1f;
            image.color = tint;
        }
    }
}
