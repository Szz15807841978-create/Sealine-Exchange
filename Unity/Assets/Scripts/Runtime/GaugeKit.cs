using System.Collections.Generic;
using UnityEngine;

namespace Fishing
{
    /// <summary>仪表盘小玩法：区间布局 / 指针 / 判定 / 冲击叠加（对应 index.html 的 gauge* 函数）。</summary>
    public static class GaugeKit
    {
        public static readonly Dictionary<string, string> Label = new Dictionary<string, string>
        {
            { "best", "最佳" }, { "good", "次佳" }, { "normal", "普通" }, { "bad", "差" },
        };

        public static readonly Dictionary<string, string> Text = new Dictionary<string, string>
        {
            { "best",   "最佳！曲线顺风 +12%" },
            { "good",   "次佳 · 曲线小幅顺风 +5%" },
            { "normal", "普通 · 曲线不变" },
            { "bad",    "差！曲线立刻暴跌 −15%" },
        };

        /// <summary>
        /// 区间布局：宽度固定（保证概率），位置每次洗牌 ——
        /// 最佳区间可能落在两端，最坏区间也可能落在正中间，不能靠记位置混过去。
        /// </summary>
        public static List<GaugeSegment> MakeLayout()
        {
            var items = new List<KeyValuePair<string, float>>();
            for (int i = 0; i < Cfg.GaugeHalf.Length; i++)
            {
                items.Add(Cfg.GaugeHalf[i]);
                items.Add(Cfg.GaugeHalf[i]);
            }
            items.Add(new KeyValuePair<string, float>("best", Cfg.GaugeBest));

            if (Cfg.GaugeShuffle)
            {
                for (int i = items.Count - 1; i > 0; i--)
                {
                    int j = Rng.Index(i + 1);
                    var t = items[i];
                    items[i] = items[j];
                    items[j] = t;
                }
            }

            var segs = new List<GaugeSegment>();
            float x = 0f;
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                segs.Add(new GaugeSegment
                {
                    Kind = it.Key,
                    From = x,
                    To = Mathf.Round((x + it.Value) * 100f) / 100f,
                });
                x += it.Value;
            }
            return segs;
        }

        public static List<GaugeSegment> Segments(GameState s)
        {
            if (s.GaugeLayout == null) s.GaugeLayout = MakeLayout();
            return s.GaugeLayout;
        }

        public static string KindAt(GameState s, float pos)
        {
            var segs = Segments(s);
            for (int i = 0; i < segs.Count; i++)
            {
                if (pos >= segs[i].From && pos <= segs[i].To) return segs[i].Kind;
            }
            return "bad";
        }

        /// <summary>指针位置：0 → 100 → 0 的三角波。</summary>
        public static float Needle(float u)
        {
            float k = (u / Cfg.GaugeSweep) % 2f;
            return k <= 1f ? k * 100f : (2f - k) * 100f;
        }

        /// <summary>触发时刻：两次，各自在配置的时间窗内随机，然后按时间排序。</summary>
        public static List<float> Plan()
        {
            var plan = new List<float>();
            int n = Cfg.GaugePerSession;
            for (int i = 0; i < n; i++)
            {
                int pi = Mathf.Min(i, Cfg.GaugePlan.Length - 1);
                float[] w = Cfg.GaugePlan[pi];
                float hi = Mathf.Max(w[0] + 1f, Mathf.Min(w[1], Cfg.Duration - 8f));
                plan.Add(Rng.Range(w[0], hi));
            }
            plan.Sort();
            return plan;
        }

        /// <summary>对应 addShock()：把判定结果作为冲击叠加层挂到曲线上。</summary>
        public static Shock AddShock(GameState s, float at, string kind)
        {
            var cfg = Cfg.ShockOf(kind);
            if (cfg.Mag == 0f) return null;

            var sh = new Shock
            {
                At = at,
                Kind = kind,
                Mag = cfg.Mag,
                Dur = cfg.Dur,
                Label = Label.ContainsKey(kind) ? Label[kind] : kind,
            };
            s.Shocks.Add(sh);
            s.LastShock = sh;
            return sh;
        }

        /// <summary>判定文案（对应 judgeGauge 里那句 toast）。</summary>
        public static string ShockMessage(Shock sh)
        {
            if (sh == null) return "曲线不变";
            if (sh.Mag > 0f) return "曲线顺风 +" + Num.F1(sh.Mag * 100f) + "%";
            return "曲线暴跌 " + Num.F1(sh.Mag * 100f) + "%";
        }
    }
}
