using System.Collections.Generic;

namespace Fishing
{
    public class FishDef
    {
        public readonly string Id;
        public readonly string Name;
        public readonly int Value;
        public readonly string Emoji;
        public readonly string Color;

        public FishDef(string id, string name, int value, string emoji, string color)
        {
            Id = id;
            Name = name;
            Value = value;
            Emoji = emoji;
            Color = color;
        }
    }

    /// <summary>鱼类表（价值单位 = 元），对应 index.html 的 FISH 数组。</summary>
    public static class FishDefs
    {
        public static readonly FishDef[] All =
        {
            new FishDef("grouper",   "石斑鱼", 100, "🐟", "#fbbf24"),
            new FishDef("tuna",      "金枪鱼",  50, "🐟", "#f87171"),
            new FishDef("seabass",   "鲈鱼",    25, "🐟", "#c084fc"),
            new FishDef("octopus",   "章鱼",    20, "🐙", "#f472b6"),
            new FishDef("yellowfin", "黄斑鱼",  10, "🐠", "#facc15"),
            new FishDef("sardine",   "沙丁鱼",   5, "🐟", "#38bdf8"),
            new FishDef("worm",      "线虫",     2, "🪱", "#84cc16"),
            new FishDef("minnow",    "小杂鱼",   1, "🐟", "#94a3b8"),
        };

        private static Dictionary<string, FishDef> _byId;

        public static FishDef ById(string id)
        {
            if (_byId == null)
            {
                _byId = new Dictionary<string, FishDef>();
                for (int i = 0; i < All.Length; i++) _byId[All[i].Id] = All[i];
            }
            FishDef f;
            return _byId.TryGetValue(id, out f) ? f : null;
        }

        public static FishDef ByValue(int v)
        {
            for (int i = 0; i < All.Length; i++) if (All[i].Value == v) return All[i];
            return null;
        }

        /// <summary>对应 NET_COLORS：每张网按 id 轮换颜色。</summary>
        public static readonly string[] NetColors =
        {
            "#38bdf8", "#a78bfa", "#f472b6", "#fbbf24",
            "#4ade80", "#fb923c", "#22d3ee", "#e879f9",
        };

        public static string NetColor(int netId)
        {
            int i = (netId - 1) % NetColors.Length;
            if (i < 0) i += NetColors.Length;
            return NetColors[i];
        }

        /// <summary>直接给出颜色值（状态层用，避免依赖 UI 的 Palette）。</summary>
        public static UnityEngine.Color NetColorValue(int netId)
        {
            UnityEngine.Color c;
            if (UnityEngine.ColorUtility.TryParseHtmlString(NetColor(netId), out c)) return c;
            return UnityEngine.Color.magenta;
        }
    }
}
