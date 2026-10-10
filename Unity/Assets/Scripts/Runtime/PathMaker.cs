using System.Globalization;
using UnityEngine;

namespace Fishing
{
    /// <summary>对应 index.html 的小工具函数（clamp / smoothstep / lerp / f1 / f2 / signed / cls）。</summary>
    public static class Num
    {
        public static float Clamp(float v, float a, float b)
        {
            return v < a ? a : (v > b ? b : v);
        }

        public static int Clamp(int v, int a, int b)
        {
            return v < a ? a : (v > b ? b : v);
        }

        public static float Smoothstep(float t)
        {
            t = Clamp(t, 0f, 1f);
            return t * t * (3f - 2f * t);
        }

        public static float Lerp(float a, float b, float t)
        {
            return a + (b - a) * t;
        }

        /// <summary>f1：保留 1 位小数的字符串（JS 的 toFixed(1) 等价物）。</summary>
        public static string F1(float v)
        {
            float r = Mathf.Round(v * 10f) / 10f;
            if (float.IsNaN(r) || float.IsInfinity(r)) r = 0f;
            return r.ToString("0.0", CultureInfo.InvariantCulture);
        }

        /// <summary>f2：保留 2 位小数。</summary>
        public static string F2(float v)
        {
            float r = Mathf.Round(v * 100f) / 100f;
            if (float.IsNaN(r) || float.IsInfinity(r)) r = 0f;
            return r.ToString("0.00", CultureInfo.InvariantCulture);
        }

        /// <summary>signed：正数前面补 +，负数保留 -，保留 1 位小数。</summary>
        public static string Signed(float v)
        {
            return (v > 0f ? "+" : "") + F1(v);
        }

        /// <summary>F1 的补零版本，用于价格标签（90.0s 这种）。</summary>
        public static string Seconds(float v)
        {
            return F1(v) + "s";
        }
    }

    /// <summary>
    /// 曲线：基准路径 + 冲击叠加层。
    /// 基准路径 Arr 在开盘时算好，永不修改；仪表盘的判定结果作为"冲击"叠加在上面，
    /// 已画出的历史不受影响 —— 和 index.html 的分层方式完全一致。
    /// </summary>
    public class MarketPath
    {
        public float[] Arr;
        public string Name;
        public int N;

        /// <summary>shockFactor(t)：把所有生效的冲击乘起来。</summary>
        public static float ShockFactor(GameState s, float t)
        {
            float f = 1f;
            for (int i = 0; i < s.Shocks.Count; i++)
            {
                var sh = s.Shocks[i];
                if (t <= sh.At) continue;
                float u = sh.Dur > 0f ? Mathf.Min(1f, (t - sh.At) / sh.Dur) : 1f;
                f *= 1f + sh.Mag * u;
            }
            return f;
        }

        public float PriceAtIndex(GameState s, int i)
        {
            if (Arr == null) return 100f;
            int idx = Num.Clamp(i, 0, N - 1);
            return Arr[idx] * ShockFactor(s, idx * Cfg.Tick);
        }

        public float PriceAt(GameState s, float t)
        {
            if (Arr == null) return 100f;
            return PriceAtIndex(s, Mathf.RoundToInt(t / Cfg.Tick));
        }
    }

    /// <summary>曲线生成：buildPath / sampleKeys / makeNoise。</summary>
    public static class PathMaker
    {
        /// <summary>对应 sampleKeys(keys, t)：关键帧之间用 smoothstep 平滑插值。</summary>
        public static float SampleKeys(float[][] keys, float t)
        {
            if (t <= keys[0][0]) return keys[0][1];
            for (int i = 1; i < keys.Length; i++)
            {
                if (t <= keys[i][0])
                {
                    float[] a = keys[i - 1], b = keys[i];
                    float u = Num.Smoothstep((t - a[0]) / (b[0] - a[0]));
                    return Num.Lerp(a[1], b[1], u);
                }
            }
            return keys[keys.Length - 1][1];
        }

        /// <summary>对应 makeNoise()：均值回归的随机游走噪声。</summary>
        public static float[] MakeNoise(int n, float sigma, float tau, float dt, float cap)
        {
            var arr = new float[n];
            float lim = cap > 0f ? cap : 0.16f;
            float x = 0f;
            for (int i = 1; i < n; i++)
            {
                x += (-x / tau) * dt + sigma * Mathf.Sqrt(dt) * Rng.Randn();
                x = Num.Clamp(x, -lim, lim);
                arr[i] = x;
            }
            return arr;
        }

        /// <summary>对应 buildPath()：随机套用一种形态，拉伸幅度，叠噪声，开盘价固定 100。</summary>
        public static MarketPath Build()
        {
            var p = Patterns.All[Rng.Index(Patterns.All.Length)];
            int n = Mathf.RoundToInt(Cfg.Duration / Cfg.Tick) + 1;
            float scale = Cfg.PatternScale;

            // 形态幅度整体拉伸：100 + (v-100)×SCALE，让涨跌更狠
            var keys = new float[p.Keys.Length][];
            for (int i = 0; i < p.Keys.Length; i++)
            {
                keys[i] = new[] { p.Keys[i][0], Mathf.Max(12f, 100f + (p.Keys[i][1] - 100f) * scale) };
            }

            var baseArr = new float[n];
            for (int i = 0; i < n; i++) baseArr[i] = SampleKeys(keys, (float)i / (n - 1));

            var noise = MakeNoise(n, Cfg.VolSigma, 2.6f, Cfg.Tick, Cfg.VolClamp);

            var arr = new float[n];
            for (int i = 0; i < n; i++) arr[i] = Mathf.Max(3f, baseArr[i] * (1f + noise[i]));
            arr[0] = 100f;   // 开盘价固定 100

            var path = new MarketPath();
            path.Arr = arr;
            path.Name = p.Name;
            path.N = n;
            return path;
        }
    }
}
