using System;
using System.Collections.Generic;
using Fishing;
using UnityEngine;

namespace RaftRuleCheck
{
    /// <summary>
    /// 木筏浮力解算的无头断言。跑法见 tools/run-raftcheck.ps1。
    /// 覆盖：平面拟合的正确性/符号约定、退化输入、噪声鲁棒性、平滑器的帧率无关性。
    /// </summary>
    public static class RaftRuleCheckMain
    {
        private static int _passed;
        private static int _failed;
        private static readonly List<string> _failures = new List<string>();

        // 场景实测的采样点布局：甲板沿 X 排开（长 12.6），左右沿 Z（半宽约 1.1）
        private const float HalfLen = 6.3f;
        private const float HalfWid = 1.1f;

        public static int Main()
        {
            FlatWater_ZeroTilt();
            PurePitch_Recovered();
            PureRoll_Recovered();
            CombinedTilt_Recovered();
            Heave_IsValueAtOrigin_NotMean();
            ExactPlane_ZeroResidual();
            Degenerate_TooFewPoints();
            Degenerate_Collinear();
            Degenerate_NaN();
            Noise_TiltStaysBounded();
            SignConvention_XHigherIsPositivePitch();
            SignConvention_ZHigherIsPositiveRoll();
            Smooth_HalfLifeHalvesTheGap();
            Smooth_IsFrameRateIndependent();
            Smooth_ZeroHalfLifeSnaps();
            Smooth_NoOvershoot();
            Smooth_NegativeDtKeepsValue();
            Clamp_Symmetric();
            Submerged_Fraction();
            Sky_EightDistinctPresets();
            Sky_TimeDimmingIsMonotonic();
            Sky_RainDimsEveryTime();
            Sky_MixEndpointsAndMiddle();
            Sky_IndexAndLabels();
            Sky_CubeFilesSane();

            Console.WriteLine();
            Console.WriteLine("RAFT RULE CHECK: " + _passed + " passed, " + _failed + " failed");
            foreach (var f in _failures) Console.WriteLine("  FAIL " + f);
            return _failed == 0 ? 0 : 1;
        }

        private static void Ok(bool cond, string name)
        {
            if (cond) { _passed++; }
            else { _failed++; _failures.Add(name); }
        }

        private static void Approx(float a, float b, float eps, string name)
        {
            Ok(Math.Abs(a - b) <= eps, name + " (" + a + " vs " + b + ")");
        }

        /// <summary>标准十字五点：中心 / 前 / 后 / 左 / 右。</summary>
        private static Vector2[] Cross()
        {
            return new[]
            {
                new Vector2(0f, 0f),          // 中心
                new Vector2(HalfLen, 0f),     // 前 (+X)
                new Vector2(-HalfLen, 0f),    // 后 (-X)
                new Vector2(0f, HalfWid),     // 左 (+Z)
                new Vector2(0f, -HalfWid),    // 右 (-Z)
            };
        }

        /// <summary>按平面 y = a + b·x + c·z 造一组合成水面高度。</summary>
        private static float[] PlaneHeights(Vector2[] pts, float a, float b, float c)
        {
            var h = new float[pts.Length];
            for (int i = 0; i < pts.Length; i++) h[i] = a + b * pts[i].x + c * pts[i].y;
            return h;
        }

        // 1 —— 静水面：升沉等于水位，两个倾角都是 0
        private static void FlatWater_ZeroTilt()
        {
            var pts = Cross();
            var h = PlaneHeights(pts, 4.25f, 0f, 0f);
            RaftWavePose p;
            Ok(RaftBuoyancySolver.Solve(h, pts, 5, out p), "静水面_解算成功");
            Approx(p.Heave, 4.25f, 1e-4f, "静水面_升沉=水位");
            Approx(p.PitchDeg, 0f, 1e-3f, "静水面_俯仰=0");
            Approx(p.RollDeg, 0f, 1e-3f, "静水面_横滚=0");
        }

        // 2 —— 纯俯仰：+X 端抬高 3°
        private static void PurePitch_Recovered()
        {
            var pts = Cross();
            float b = (float)Math.Tan(3.0 * Math.PI / 180.0);
            var h = PlaneHeights(pts, 0f, b, 0f);
            RaftWavePose p;
            Ok(RaftBuoyancySolver.Solve(h, pts, 5, out p), "俯仰_解算成功");
            Approx(p.PitchDeg, 3f, 1e-2f, "俯仰_角度还原");
            Approx(p.RollDeg, 0f, 1e-3f, "俯仰_横滚不受影响");
            Ok(p.PitchDeg > 0f, "俯仰_+X 抬高为正");
        }

        // 3 —— 纯横滚：+Z 端抬高 4°
        private static void PureRoll_Recovered()
        {
            var pts = Cross();
            float c = (float)Math.Tan(4.0 * Math.PI / 180.0);
            var h = PlaneHeights(pts, 0f, 0f, c);
            RaftWavePose p;
            Ok(RaftBuoyancySolver.Solve(h, pts, 5, out p), "横滚_解算成功");
            Approx(p.RollDeg, 4f, 1e-2f, "横滚_角度还原");
            Approx(p.PitchDeg, 0f, 1e-3f, "横滚_俯仰不受影响");
            Ok(p.RollDeg > 0f, "横滚_+Z 抬高为正");
        }

        // 4 —— 俯仰 + 横滚同时存在，两个角都要还原
        private static void CombinedTilt_Recovered()
        {
            var pts = Cross();
            float b = (float)Math.Tan(2.5 * Math.PI / 180.0);
            float c = (float)Math.Tan(-3.5 * Math.PI / 180.0);
            var h = PlaneHeights(pts, -1.75f, b, c);
            RaftWavePose p;
            Ok(RaftBuoyancySolver.Solve(h, pts, 5, out p), "复合_解算成功");
            Approx(p.Heave, -1.75f, 1e-3f, "复合_升沉");
            Approx(p.PitchDeg, 2.5f, 1e-2f, "复合_俯仰");
            Approx(p.RollDeg, -3.5f, 1e-2f, "复合_横滚");
        }

        // 5 —— 升沉是「采样坐标系原点处的水面高度」，不是采样点平均值。
        //      采样点偏心时这两个量差别很大，必须确认取的是前者，
        //      否则木筏会随采样点布局莫名其妙地漂。
        private static void Heave_IsValueAtOrigin_NotMean()
        {
            var pts = new[]
            {
                new Vector2(1f, 0f),
                new Vector2(2f, 0f),
                new Vector2(3f, 0f),
                new Vector2(2f, 1f),
                new Vector2(2f, -1f),
            };
            var h = PlaneHeights(pts, 10f, 0.5f, 0.25f);
            RaftWavePose p;
            Ok(RaftBuoyancySolver.Solve(h, pts, 5, out p), "偏心采样_解算成功");
            Approx(p.Heave, 10f, 1e-3f, "偏心采样_升沉取原点值");
            Approx(p.MeanHeight, 11f, 1e-3f, "偏心采样_平均值确实不同");
        }

        // 6 —— 平面数据拟合残差应为 0
        private static void ExactPlane_ZeroResidual()
        {
            var pts = Cross();
            var h = PlaneHeights(pts, 0.9f, 0.07f, -0.04f);
            RaftWavePose p;
            RaftBuoyancySolver.Solve(h, pts, 5, out p);
            Ok(p.ResidualRms < 1e-4f, "平面数据_残差为 0 (" + p.ResidualRms + ")");
        }

        // 7 —— 少于 3 个点无法定平面
        private static void Degenerate_TooFewPoints()
        {
            var pts = new[] { new Vector2(1f, 0f), new Vector2(-1f, 0f) };
            var h = new[] { 1f, 1f };
            RaftWavePose p;
            Ok(!RaftBuoyancySolver.Solve(h, pts, 2, out p), "退化_两点返回 false");
            Ok(!RaftBuoyancySolver.Solve(null, pts, 2, out p), "退化_空高度返回 false");
            Ok(!RaftBuoyancySolver.Solve(h, null, 2, out p), "退化_空偏移返回 false");
        }

        // 8 —— 采样点共线（都在一条线上）时平面不唯一，必须拒绝而不是给个乱角度
        private static void Degenerate_Collinear()
        {
            var pts = new[]
            {
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(2f, 0f),
                new Vector2(3f, 0f), new Vector2(4f, 0f),
            };
            var h = new[] { 0f, 1f, 2f, 3f, 4f };
            RaftWavePose p;
            Ok(!RaftBuoyancySolver.Solve(h, pts, 5, out p), "退化_共线返回 false");
        }

        // 9 —— NaN 输入必须被挡掉，不能污染木筏位姿
        private static void Degenerate_NaN()
        {
            var pts = Cross();
            var h = PlaneHeights(pts, 0f, 0.1f, 0f);
            h[3] = float.NaN;
            RaftWavePose p;
            Ok(!RaftBuoyancySolver.Solve(h, pts, 5, out p), "退化_NaN 返回 false");
        }

        // 10 —— 加 ±5cm 噪声后，角度仍被限制在合理范围（这正是不用两点相减的理由）。
        //      注意两个轴的噪声上限天然不同：甲板沿 X 长 12.6、沿 Z 只有 2.2，
        //      「横滚」由很短的一条基线定出来，同样噪声下角度误差约是俯仰的 5—6 倍。
        //      这不是 bug，是窄船体的物理事实 —— 代码侧靠 TiltHalfLife 平滑 + MaxRollDeg 限幅兜住。
        private static void Noise_TiltStaysBounded()
        {
            var pts = Cross();
            var rng = new System.Random(20261004);
            float worstPitch = 0f, worstRoll = 0f;
            bool anyFail = false;
            for (int trial = 0; trial < 200; trial++)
            {
                var h = PlaneHeights(pts, 0f, 0f, 0f);
                for (int i = 0; i < h.Length; i++) h[i] += (float)((rng.NextDouble() * 2 - 1) * 0.05);
                RaftWavePose p;
                if (!RaftBuoyancySolver.Solve(h, pts, 5, out p)) { anyFail = true; break; }
                worstPitch = Math.Max(worstPitch, Math.Abs(p.PitchDeg));
                worstRoll = Math.Max(worstRoll, Math.Abs(p.RollDeg));
            }
            Ok(!anyFail, "噪声_解算不失败");
            Ok(worstPitch < 1.0f, "噪声_俯仰最坏 " + worstPitch.ToString("0.00") + "° < 1.0°");
            Ok(worstRoll < 4.0f, "噪声_横滚最坏 " + worstRoll.ToString("0.00") + "° < 4.0°");
        }

        // 11 —— 把符号约定钉死：这两个断言保证 gluing 层 Quaternion.Euler(-Roll,0,Pitch) 的方向不会反。
        //      十字布局的法方程是对角阵（Σx=Σz=Σxz=0），所以「只抬前后两点之一」恰好只产生纯俯仰，
        //      「只抬左右两点之一」恰好只产生纯横滚 —— 顺带把两个轴的解耦也验了。
        private static void SignConvention_XHigherIsPositivePitch()
        {
            var pts = Cross();
            var h = PlaneHeights(pts, 0f, 0f, 0f);
            h[1] = 1f;    // 只把「前」(+X, z=0) 抬高 1 米
            RaftWavePose p;
            Ok(RaftBuoyancySolver.Solve(h, pts, 5, out p), "符号_单点抬高可解");
            Ok(p.PitchDeg > 0f, "符号_+X 端抬高 → 俯仰为正 (" + p.PitchDeg.ToString("0.000") + "°)");
            Approx(p.RollDeg, 0f, 1e-3f, "符号_只抬 +X 端不产生横滚");
        }

        private static void SignConvention_ZHigherIsPositiveRoll()
        {
            var pts = Cross();
            var h = PlaneHeights(pts, 0f, 0f, 0f);
            h[3] = 1f;    // 只把「左」(+Z, x=0) 抬高 1 米
            RaftWavePose p;
            Ok(RaftBuoyancySolver.Solve(h, pts, 5, out p), "符号_单点抬高可解（横滚）");
            Ok(p.RollDeg > 0f, "符号_+Z 端抬高 → 横滚为正 (" + p.RollDeg.ToString("0.000") + "°)");
            Approx(p.PitchDeg, 0f, 1e-3f, "符号_只抬 +Z 端不产生俯仰");
        }

        // 13 —— 一个半衰期正好走掉一半差值
        private static void Smooth_HalfLifeHalvesTheGap()
        {
            float v = RaftBuoyancySolver.SmoothTowards(0f, 10f, 0.4f, 0.4f);
            Approx(v, 5f, 1e-3f, "平滑_一个半衰期走一半");
        }

        // 14 —— 拆成两步半个半衰期，结果必须和一步相同（帧率无关）
        private static void Smooth_IsFrameRateIndependent()
        {
            float one = RaftBuoyancySolver.SmoothTowards(0f, 10f, 0.4f, 0.4f);
            float two = RaftBuoyancySolver.SmoothTowards(0f, 10f, 0.4f, 0.2f);
            two = RaftBuoyancySolver.SmoothTowards(two, 10f, 0.4f, 0.2f);
            Approx(two, one, 1e-3f, "平滑_帧率无关（2×20fps = 1×10fps）");

            float many = 0f;
            for (int i = 0; i < 40; i++) many = RaftBuoyancySolver.SmoothTowards(many, 10f, 0.4f, 0.01f);
            Approx(many, one, 1e-3f, "平滑_帧率无关（40×100fps = 1×10fps）");
        }

        // 15 —— halfLife ≤ 0 表示直接吸附
        private static void Smooth_ZeroHalfLifeSnaps()
        {
            Approx(RaftBuoyancySolver.SmoothTowards(0f, 7f, 0f, 0.016f), 7f, 1e-5f, "平滑_半衰期 0 直接吸附");
            Approx(RaftBuoyancySolver.SmoothTowards(3f, 7f, -1f, 0.016f), 7f, 1e-5f, "平滑_半衰期负数直接吸附");
        }

        // 16 —— 不允许过冲：指数趋近必须单调逼近目标
        private static void Smooth_NoOvershoot()
        {
            float v = 0f;
            bool overshoot = false;
            bool nonMonotonic = false;
            float prev = 0f;
            for (int i = 0; i < 500; i++)
            {
                v = RaftBuoyancySolver.SmoothTowards(v, 10f, 0.3f, 0.016f);
                if (v > 10f + 1e-4f) overshoot = true;
                if (v < prev - 1e-6f) nonMonotonic = true;
                prev = v;
            }
            Ok(!overshoot, "平滑_不会过冲");
            Ok(!nonMonotonic, "平滑_单调逼近");
            Approx(v, 10f, 1e-3f, "平滑_最终收敛到目标");
        }

        // 17 —— dt ≤ 0 时保持原值，不能因为暂停/首帧把姿态弹飞
        private static void Smooth_NegativeDtKeepsValue()
        {
            Approx(RaftBuoyancySolver.SmoothTowards(3f, 9f, 0.3f, 0f), 3f, 1e-6f, "平滑_dt=0 保持原值");
            Approx(RaftBuoyancySolver.SmoothTowards(3f, 9f, 0.3f, -1f), 3f, 1e-6f, "平滑_dt<0 保持原值");
        }

        // 18 —— 限幅：受控浮力靠它保证不翻覆
        private static void Clamp_Symmetric()
        {
            Approx(RaftBuoyancySolver.ClampSymmetric(12f, 6f), 6f, 1e-5f, "限幅_正向");
            Approx(RaftBuoyancySolver.ClampSymmetric(-12f, 6f), -6f, 1e-5f, "限幅_负向");
            Approx(RaftBuoyancySolver.ClampSymmetric(3f, 6f), 3f, 1e-5f, "限幅_区间内不动");
            Approx(RaftBuoyancySolver.ClampSymmetric(3f, 0f), 0f, 1e-5f, "限幅_限值为 0 时归零");
            Approx(RaftBuoyancySolver.ClampSymmetric(3f, -1f), 0f, 1e-5f, "限幅_限值为负时归零");
        }

        // 19 —— 浸没比例（第二阶段完整刚体浮力用）
        private static void Submerged_Fraction()
        {
            Approx(RaftBuoyancySolver.SubmergedFraction(1f, 0.5f, 1f), 0.5f, 1e-5f, "浸没_半浸");
            Approx(RaftBuoyancySolver.SubmergedFraction(1f, 1f, 1f), 0f, 1e-5f, "浸没_刚好在水面为 0");
            Approx(RaftBuoyancySolver.SubmergedFraction(0f, 1f, 1f), 0f, 1e-5f, "浸没_在水面上为 0");
            Approx(RaftBuoyancySolver.SubmergedFraction(5f, 0f, 1f), 1f, 1e-5f, "浸没_全浸没夹到 1");
            Approx(RaftBuoyancySolver.SubmergedFraction(1f, 0f, 0f), 0f, 1e-5f, "浸没_高度 0 不除零");
        }

        // ================= 天空时段预设（RaftSkyPresets） =================
        private static float Lum(UnityEngine.Color c) { return 0.299f * c.r + 0.587f * c.g + 0.114f * c.b; }

        private static bool Same(UnityEngine.Color a, UnityEngine.Color b, float eps)
        {
            return Math.Abs(a.r - b.r) < eps && Math.Abs(a.g - b.g) < eps && Math.Abs(a.b - b.b) < eps;
        }

        // 29 —— 8 套预设两两不同（否则面板上 8 个按钮里会有重复看不出差别）
        private static void Sky_EightDistinctPresets()
        {
            var all = new System.Collections.Generic.List<SkyLook>();
            for (int t = 0; t < RaftSkyPresets.TimeCount; t++)
                for (int w = 0; w < RaftSkyPresets.WeatherCount; w++)
                    all.Add(RaftSkyPresets.Get((RaftTimeOfDay)t, w == 1));

            int dup = 0;
            for (int i = 0; i < all.Count; i++)
                for (int j = i + 1; j < all.Count; j++)
                {
                    bool same = Same(all[i].Water, all[j].Water, 1e-5f)
                             && Math.Abs(all[i].SunIntensity - all[j].SunIntensity) < 1e-5f
                             && Math.Abs(all[i].FogDensity - all[j].FogDensity) < 1e-5f
                             && Same(all[i].SkyTint, all[j].SkyTint, 1e-5f);
                    if (same) dup++;
                }
            Ok(dup == 0, "天空_8 套预设互不相同（重复 " + dup + " 对）");
        }

        // 30 —— 四个时段沿「白天→下午→傍晚→夜晚」单调变暗，不能有某一档反跳
        private static void Sky_TimeDimmingIsMonotonic()
        {
            var order = new[] { RaftTimeOfDay.Day, RaftTimeOfDay.Afternoon, RaftTimeOfDay.Dusk, RaftTimeOfDay.Night };
            bool sunMono = true, ambMono = true, expMono = true, fogMono = true, waterMono = true;
            for (int i = 1; i < order.Length; i++)
            {
                var a = RaftSkyPresets.Get(order[i - 1], false);
                var b = RaftSkyPresets.Get(order[i], false);
                if (!(a.SunIntensity > b.SunIntensity)) sunMono = false;
                if (!(a.AmbientIntensity > b.AmbientIntensity)) ambMono = false;
                if (!(a.SkyExposure > b.SkyExposure)) expMono = false;
                if (!(a.FogDensity < b.FogDensity)) fogMono = false;
                if (!(Lum(a.Water) > Lum(b.Water))) waterMono = false;
            }
            Ok(sunMono, "天空_日照强度随时段单调变暗");
            Ok(ambMono, "天空_环境光随时段单调变暗");
            Ok(expMono, "天空_天空盒曝光随时段单调变暗");
            Ok(fogMono, "天空_雾密度随时段单调变浓");
            Ok(waterMono, "天空_海水亮度随时段单调变暗");
        }

        // 31 —— 每个时段里，雨天都要比晴天更暗、日照更弱
        private static void Sky_RainDimsEveryTime()
        {
            bool ok = true;
            string bad = "";
            for (int t = 0; t < RaftSkyPresets.TimeCount; t++)
            {
                var clear = RaftSkyPresets.Get((RaftTimeOfDay)t, false);
                var rain = RaftSkyPresets.Get((RaftTimeOfDay)t, true);
                bool darker = Lum(rain.Water) < Lum(clear.Water)
                           && rain.SunIntensity < clear.SunIntensity
                           && rain.AmbientIntensity < clear.AmbientIntensity
                           && rain.FogDensity > clear.FogDensity;
                if (!darker) { ok = false; bad += RaftSkyPresets.TimeNames[t] + " "; }
            }
            Ok(ok, "天空_每个时段的雨天都更暗更雾" + (ok ? "" : "（问题：" + bad + "）"));
        }

        // 32 —— 插值端点必须精确落在预设上，中点要落在两者之间
        private static void Sky_MixEndpointsAndMiddle()
        {
            bool endOk = true, midOk = true;
            for (int t = 0; t < RaftSkyPresets.TimeCount; t++)
            {
                var time = (RaftTimeOfDay)t;
                var a = RaftSkyPresets.Get(time, false);
                var b = RaftSkyPresets.Get(time, true);

                var m0 = RaftSkyPresets.Mix(time, 0f);
                var m1 = RaftSkyPresets.Mix(time, 1f);
                if (!Same(m0.Water, a.Water, 1e-6f) || !Same(m1.Water, b.Water, 1e-6f)) endOk = false;

                var half = RaftSkyPresets.Mix(time, 0.5f);
                float lo = Math.Min(Lum(a.Water), Lum(b.Water));
                float hi = Math.Max(Lum(a.Water), Lum(b.Water));
                if (Lum(half.Water) < lo - 1e-4f || Lum(half.Water) > hi + 1e-4f) midOk = false;
            }
            Ok(endOk, "天空_雨量 0/1 精确落在预设端点");
            Ok(midOk, "天空_雨量 0.5 落在两端之间");
            // 雨量超范围要被夹住，不能外推出诡异的颜色
            var over = RaftSkyPresets.Mix(RaftTimeOfDay.Day, 99f);
            var one = RaftSkyPresets.Get(RaftTimeOfDay.Day, true);
            Ok(Same(over.Water, one.Water, 1e-6f), "天空_雨量越界被夹到 1");
        }

        // 33 —— 8 个组合的索引与标签
        private static void Sky_IndexAndLabels()
        {
            var seen = new System.Collections.Generic.HashSet<int>();
            var labels = new System.Collections.Generic.HashSet<string>();
            bool rangeOk = true;
            for (int t = 0; t < RaftSkyPresets.TimeCount; t++)
                for (int w = 0; w < RaftSkyPresets.WeatherCount; w++)
                {
                    int i = RaftSkyPresets.Index((RaftTimeOfDay)t, w == 1);
                    seen.Add(i);
                    labels.Add(RaftSkyPresets.Label((RaftTimeOfDay)t, w == 1));
                    if (i < 0 || i >= RaftSkyPresets.TimeCount * RaftSkyPresets.WeatherCount) rangeOk = false;
                }
            Ok(seen.Count == 8, "天空_索引覆盖 0..7 且互不相同（实际 " + seen.Count + "）");
            Ok(labels.Count == 8, "天空_8 个标签互不相同（实际 " + labels.Count + "）");
            Ok(rangeOk, "天空_索引都在合法范围内");
            // 越界时段要夹住而不是抛异常
            int safe = RaftSkyPresets.Index((RaftTimeOfDay)99, false);
            Ok(safe >= 0 && safe < 8, "天空_越界时段索引被夹住");
        }

        // 34 —— 立方图文件名齐全且不重复
        private static void Sky_CubeFilesSane()
        {
            Ok(RaftSkyPresets.SunnyCubeFiles.Length == RaftSkyPresets.TimeCount, "天空_晴天立方图数量 = 时段数");
            var set = new System.Collections.Generic.HashSet<string>();
            bool allFilled = true;
            foreach (var f in RaftSkyPresets.SunnyCubeFiles)
            {
                if (string.IsNullOrEmpty(f)) allFilled = false;
                set.Add(f);
            }
            Ok(allFilled, "天空_晴天立方图文件名都不为空");
            Ok(set.Count == RaftSkyPresets.TimeCount, "天空_四个时段用了四张不同的晴天天空");
            Ok(!string.IsNullOrEmpty(RaftSkyPresets.RainyCubeFile), "天空_雨天立方图文件名不为空");
            Ok(RaftSkyPresets.TimeNames.Length == RaftSkyPresets.TimeCount, "天空_时段名数量 = 时段数");
        }
    }
}
