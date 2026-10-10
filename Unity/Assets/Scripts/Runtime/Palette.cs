using UnityEngine;

namespace Fishing
{
    /// <summary>
    /// index.html &lt;style&gt; 里 :root 变量的 Unity 版本。
    /// 界面全部用代码搭，颜色只在这里定义一次。
    /// </summary>
    public static class Palette
    {
        public static Color Hex(string hex)
        {
            Color c;
            if (ColorUtility.TryParseHtmlString(hex.StartsWith("#") ? hex : "#" + hex, out c)) return c;
            return Color.magenta;
        }

        public static Color Hex(string hex, float a)
        {
            var c = Hex(hex);
            c.a = a;
            return c;
        }

        // ---- :root ----
        // 《鲸落》固定美术配色：深海蓝、灰蓝、青绿、浅青、象牙白、暖赭黄。
        public static readonly Color Bg          = Hex("#173A57");
        public static readonly Color Panel       = Hex("#F2E7CB", 0.96f);
        public static readonly Color PanelBottom = Hex("#F2E7CB", 0.96f);
        public static readonly Color Panel2      = Hex("#E7DDC4", 0.98f);
        public static readonly Color Line        = Hex("#173A57");
        public static readonly Color LineSoft    = Hex("#5E84A3", 0.58f);
        public static readonly Color Text        = Hex("#173A57");
        public static readonly Color Muted       = Hex("#5E84A3");
        public static readonly Color Dim         = Hex("#6E899D");
        public static readonly Color Green       = Hex("#398C84");
        public static readonly Color GreenSoft   = Hex("#2D817B");
        public static readonly Color Red         = Hex("#D9674E");
        public static readonly Color RedSoft     = Hex("#C95342");
        public static readonly Color Amber       = Hex("#DDA64F");
        public static readonly Color AmberSoft   = Hex("#E9BC68");
        public static readonly Color Cyan        = Hex("#5D929A");
        public static readonly Color Violet      = Hex("#7289A3");

        // ---- 面板 / 控件 ----
        public static readonly Color ChipBg      = Hex("#F2E7CB", 0.94f);
        public static readonly Color BoxBg       = Hex("#F7EED9", 0.96f);
        public static readonly Color ButtonBg    = Hex("#5E84A3");
        public static readonly Color ButtonHover = Hex("#7097B4");
        public static readonly Color ChartTop    = Hex("#F6EEDB", 0.97f);
        public static readonly Color ChartBottom = Hex("#F6EEDB", 0.97f);
        public static readonly Color GridLine    = Hex("#5E84A3", 0.28f);
        public static readonly Color AxisText    = Hex("#446984");
        public static readonly Color TimeText    = Hex("#6B8498");
        public static readonly Color RowLine     = Hex("#5E84A3", 0.25f);
        public static readonly Color RowFlash    = Hex("#5D929A", 0.22f);

        public static readonly Color PrimaryTop  = Hex("#E9B65C");
        public static readonly Color PrimaryBot  = Hex("#DDA64F");
        public static readonly Color CyanTop     = Hex("#71A7AD");
        public static readonly Color CyanBot     = Hex("#5D929A");
        public static readonly Color GreenTop    = Hex("#4A9A91");
        public static readonly Color GreenBot    = Hex("#397F79");
        public static readonly Color DangerTop   = Hex("#DB765E");
        public static readonly Color DangerBot   = Hex("#C95342");

        // ---- 曲线 ----
        public static readonly Color UpLine      = Hex("#2F8B82");
        public static readonly Color DownLine    = Hex("#D9674E");
        public static readonly Color AreaTop     = Hex("#5D929A", 0.20f);
        public static readonly Color AreaBottom  = Hex("#5D929A", 0f);
        public static readonly Color BonusLine   = Hex("#2F8B82", 0.48f);
        public static readonly Color MidLine     = Hex("#5E84A3", 0.45f);
        public static readonly Color EntryWin    = Hex("#2F8B82", 0.55f);
        public static readonly Color EntryLose   = Hex("#D9674E", 0.55f);
        public static readonly Color WipeLine    = Hex("#D9674E", 0.46f);
        public static readonly Color WipeLabel   = Hex("#B94E3B", 0.88f);
        public static readonly Color MarkerLine  = Hex("#173A57", 0.46f);
        public static readonly Color DotNormal   = Hex("#F2E7CB");
        public static readonly Color DotRisk     = Hex("#D9674E");
        public static readonly Color HaloNormal  = Hex("#5D929A", 0.28f);
        public static readonly Color HaloRisk    = Hex("#D9674E", 0.30f);
        public static readonly Color PriceTagOk  = Hex("#397F79");
        public static readonly Color PriceTagRisk= Hex("#B94E3B");
        public static readonly Color EmptyHint   = Hex("#5E84A3");

        // ---- 仪表盘 ----
        public static readonly Color GaugeBg        = Hex("#090e19", 0.94f);
        public static readonly Color GaugeBorder    = Hex("#2b3a55");
        public static readonly Color GaugeTrackBg   = Hex("#0b1322");
        public static readonly Color GaugeTrackLine = Hex("#1f2c44");
        public static readonly Color GaugeBest      = Hex("#fbbf24");
        public static readonly Color GaugeGood      = Hex("#22d3ee");
        public static readonly Color GaugeNormal    = Hex("#64748b");
        public static readonly Color GaugeBad       = Hex("#ef4444");
        public static readonly Color GaugeTimerA    = Hex("#f59e0b");
        public static readonly Color GaugeTimerB    = Hex("#fbbf24");

        public static readonly Color ToastBg     = Hex("#173A57", 0.96f);
        public static readonly Color OverlayBg   = Hex("#173A57", 0.46f);
        public static readonly Color ModalBg     = Hex("#F2E7CB");

        public static readonly Color BtnPrimaryText = Hex("#173A57");
        public static readonly Color BtnCyanText    = Hex("#F8F0DD");
        public static readonly Color BtnGreenText   = Hex("#F8F0DD");
        public static readonly Color BtnDangerText  = Hex("#FFF4E6");

        public static readonly Color OkBadgeBg   = Hex("#22c55e", 0.12f);
        public static readonly Color OkBadgeLine = Hex("#22c55e", 0.35f);
        public static readonly Color RiskBadgeBg = Hex("#ef4444", 0.18f);
        public static readonly Color RiskBadgeLine = Hex("#ef4444", 0.55f);
        public static readonly Color RiskText    = Hex("#fca5a5");

        public static readonly Color ChipWarnBg   = Hex("#f59e0b", 0.10f);
        public static readonly Color ChipWarnLine = Hex("#f59e0b", 0.55f);

        /// <summary>盈亏配色：&gt;0 绿，&lt;0 红，0 灰。</summary>
        public static Color Of(float v)
        {
            if (v > 0.0001f) return GreenSoft;
            if (v < -0.0001f) return RedSoft;
            return Text;
        }

        /// <summary>仪表盘区段底色（对应 CSS 里 GAUGE_COLOR + '66' / 'cc'）。</summary>
        public static Color ForGauge(string kind)
        {
            switch (kind)
            {
                case "best":   return Hex("#fbbf24");
                case "good":   return Hex("#22d3ee");
                case "normal": return Hex("#64748b", 0.4f);
                default:       return Hex("#ef4444");
            }
        }

        public static Color GaugeColor(string kind)
        {
            switch (kind)
            {
                case "best":   return GaugeBest;
                case "good":   return GaugeGood;
                case "normal": return GaugeNormal;
                default:       return GaugeBad;
            }
        }
    }
}
