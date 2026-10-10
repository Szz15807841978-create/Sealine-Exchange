using UnityEngine;

namespace Fishing
{
    /// <summary>
    /// 字体：优先用中文字体（Microsoft YaHei 等），保证界面上的中文不会变成方块。
    /// 没有任何可用字体时回退到 Unity 内建字体。
    /// </summary>
    public static class FontUtil
    {
        private static Font _cached;

        private static readonly string[] Preferred =
        {
            "Microsoft YaHei UI",
            "Microsoft YaHei",
            "微软雅黑",
            "SimHei",
            "黑体",
            "Noto Sans CJK SC",
            "Source Han Sans SC",
            "PingFang SC",
            "Segoe UI",
            "Segoe UI Emoji",
        };

        public static Font Get()
        {
            if (_cached != null) return _cached;

            var os = Font.GetOSInstalledFontNames();
            if (os != null)
            {
                for (int i = 0; i < Preferred.Length; i++)
                {
                    if (!Contains(os, Preferred[i])) continue;
                    var f = Font.CreateDynamicFontFromOSFont(Preferred[i], 16);
                    if (f == null) continue;
                    _cached = f;
                    return _cached;
                }
            }

            // 一个系统字体都没拿到时退回 Unity 内建字体（中文会显示成方块，但界面不会崩）
            var builtin = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (builtin == null) builtin = Resources.GetBuiltinResource<Font>("Arial.ttf");
            _cached = builtin;
            return _cached;
        }

        private static bool Contains(string[] arr, string name)
        {
            for (int i = 0; i < arr.Length; i++)
            {
                if (arr[i] == name) return true;
            }
            return false;
        }
    }

    /// <summary>文本样式的小工具，避免到处重复写 Font/alignment。</summary>
    public static class Txt
    {
        public static UnityEngine.UI.Text Make(Transform parent, string name, int size, Color color,
                                               TextAnchor anchor = TextAnchor.MiddleLeft, FontStyle style = FontStyle.Normal)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<UnityEngine.UI.Text>();
            t.font = FontUtil.Get();
            t.fontSize = size;
            t.color = color;
            t.alignment = anchor;
            t.fontStyle = style;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.supportRichText = true;
            return t;
        }
    }
}
