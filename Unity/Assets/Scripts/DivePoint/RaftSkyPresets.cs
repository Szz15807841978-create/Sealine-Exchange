using UnityEngine;

namespace Fishing
{
    /// <summary>一天里的四个时段。</summary>
    public enum RaftTimeOfDay
    {
        Day = 0,        // 早上（保留 Day 枚举名，避免旧场景序列化值变化）
        Afternoon = 1,  // 下午
        Dusk = 2,       // 傍晚
        Night = 3,      // 夜晚
    }

    /// <summary>一个「时段 × 天气」下的整套外观数值。</summary>
    public struct SkyLook
    {
        public Color Water;         // 海面主体色（_Diffuse）
        public Color Grazing;       // 掠射/地平线色（_DiffuseGrazing）
        public Color SkyBase;       // 反射天空色（_SkyBase）
        public Color SkyAway;       // 背阳天空色（_SkyAwayFromSun）
        public Color SunColor;
        public Color FogColor;
        public Color OverlayTint;   // 2D 叠加层（雨幕等）的着色
        public Vector3 SunEuler;
        public float SunIntensity;
        public float FogDensity;
        public float AmbientIntensity;
        public float Roughness;
        public float Normals;
        public float Foam;
        public float SkyExposure;   // 天空盒曝光
        public Color SkyTint;       // 天空盒染色
    }

    /// <summary>
    /// 时段 × 天气 = 8 套预设。
    ///
    /// 纯数据 + 纯插值，不碰场景、不碰材质，所以能用 tools/run-skycheck 无头跑断言。
    /// 数值取「白天晴天」为基准（与改造前的场景观感一致），其余时段沿
    /// 「更暖 → 更红 → 更暗更蓝」的曲线推，避免出现某一档突变。
    ///
    /// 天空立方图：四个时段各一张晴天天空（素材库里挑的 Day/积云/dusk/Night），
    /// 雨天只有一张 Rain，四个时段共用它、靠 SkyTint + SkyExposure 区分 ——
    /// 阴天本来主要就差在亮度和色温上，这是物理上也说得通的做法。
    /// </summary>
    public static class RaftSkyPresets
    {
        public const int TimeCount = 4;
        public const int WeatherCount = 2;

        public static readonly string[] TimeNames = { "早上", "下午", "傍晚", "夜晚" };
        public static readonly string[] WeatherNames = { "晴天", "雨天" };

        /// <summary>四个时段用的晴天天空立方图（文件名，不含扩展名）。</summary>
        public static readonly string[] SunnyCubeFiles =
        {
            "Sky_Anime_15_Day_a",   // 白天：干净的亮蓝
            "Sky_Anime_20_Day_a",   // 下午：积云更多、地平线偏暖
            "Sky_Anime_13_dusk_a",  // 傍晚：金色光带 + 大太阳
            "Sky_Anime_09_Night_a", // 夜晚：深藏青 + 青绿地平辉光，够夜但不黑到看不清木筏
        };

        /// <summary>雨天的天空立方图：素材包里只有这一张，四个时段共用。</summary>
        public const string RainyCubeFile = "Sky_Anime_14_Rain_a";

        public static int Index(RaftTimeOfDay t, bool rainy)
        {
            return Mathf.Clamp((int)t, 0, TimeCount - 1) * WeatherCount + (rainy ? 1 : 0);
        }

        public static string Label(RaftTimeOfDay t, bool rainy)
        {
            int i = Mathf.Clamp((int)t, 0, TimeCount - 1);
            return TimeNames[i] + (rainy ? " · 雨" : " · 晴");
        }

        /// <summary>从 8 套预设里取一套。</summary>
        public static SkyLook Get(RaftTimeOfDay t, bool rainy)
        {
            switch (t)
            {
                case RaftTimeOfDay.Afternoon: return rainy ? AfternoonRain : Afternoon;
                case RaftTimeOfDay.Dusk: return rainy ? DuskRain : Dusk;
                case RaftTimeOfDay.Night: return rainy ? NightRain : Night;
                default: return rainy ? DayRain : Day;
            }
        }

        /// <summary>按雨量在「当前时段的晴天」和「当前时段的雨天」之间插值。</summary>
        public static SkyLook Mix(RaftTimeOfDay t, float rain)
        {
            rain = Mathf.Clamp01(rain);
            SkyLook a = Get(t, false);
            SkyLook b = Get(t, true);
            if (rain <= 0f) return a;
            if (rain >= 1f) return b;

            SkyLook o;
            o.Water = Color.Lerp(a.Water, b.Water, rain);
            o.Grazing = Color.Lerp(a.Grazing, b.Grazing, rain);
            o.SkyBase = Color.Lerp(a.SkyBase, b.SkyBase, rain);
            o.SkyAway = Color.Lerp(a.SkyAway, b.SkyAway, rain);
            o.SunColor = Color.Lerp(a.SunColor, b.SunColor, rain);
            o.FogColor = Color.Lerp(a.FogColor, b.FogColor, rain);
            o.OverlayTint = Color.Lerp(a.OverlayTint, b.OverlayTint, rain);
            o.SunEuler = Vector3.Lerp(a.SunEuler, b.SunEuler, rain);
            o.SunIntensity = Mathf.Lerp(a.SunIntensity, b.SunIntensity, rain);
            o.FogDensity = Mathf.Lerp(a.FogDensity, b.FogDensity, rain);
            o.AmbientIntensity = Mathf.Lerp(a.AmbientIntensity, b.AmbientIntensity, rain);
            o.Roughness = Mathf.Lerp(a.Roughness, b.Roughness, rain);
            o.Normals = Mathf.Lerp(a.Normals, b.Normals, rain);
            o.Foam = Mathf.Lerp(a.Foam, b.Foam, rain);
            o.SkyTint = Color.Lerp(a.SkyTint, b.SkyTint, rain);
            // 曝光按几何插值更接近人眼对亮度的感受，避免中间态过亮
            o.SkyExposure = Mathf.Lerp(a.SkyExposure, b.SkyExposure, rain);
            return o;
        }

        /// <summary>
        /// 「白天晴天」= 改造前场景的原值，作为基准不动，保证默认观感不回归。
        /// </summary>
        static SkyLook Make(Color water, Color grazing, Color skyBase, Color skyAway,
            Color sun, float sunI, Vector3 sunEuler, Color fog, float fogD, float ambient,
            float rough, float normals, float foam, Color skyTint, float skyExp)
        {
            SkyLook o = default(SkyLook);
            o.Water = water; o.Grazing = grazing; o.SkyBase = skyBase; o.SkyAway = skyAway;
            o.SunColor = sun; o.SunIntensity = sunI; o.SunEuler = sunEuler;
            o.FogColor = fog; o.FogDensity = fogD; o.AmbientIntensity = ambient;
            o.Roughness = rough; o.Normals = normals; o.Foam = foam;
            o.SkyTint = skyTint; o.SkyExposure = skyExp;
            o.OverlayTint = Color.white;
            return o;
        }

        // ---------------- 晴天 ----------------
        static readonly SkyLook Day = Make(
            new Color(0.035f, 0.20f, 0.30f), new Color(0.34f, 0.62f, 0.78f),
            new Color(0.46f, 0.72f, 0.90f), new Color(0.13f, 0.28f, 0.48f),
            new Color(1.00f, 0.94f, 0.82f), 1.05f, new Vector3(48f, -38f, 0f),
            new Color(0.55f, 0.70f, 0.78f), 0.0012f, 1.00f,
            0.08f, 0.07f, 0.25f, Color.white, 1.00f);

        static readonly SkyLook Afternoon = Make(
            new Color(0.045f, 0.185f, 0.255f), new Color(0.44f, 0.60f, 0.66f),
            new Color(0.52f, 0.68f, 0.80f), new Color(0.18f, 0.28f, 0.40f),
            new Color(1.00f, 0.86f, 0.62f), 0.95f, new Vector3(26f, -52f, 0f),
            new Color(0.66f, 0.60f, 0.50f), 0.0022f, 0.92f,
            0.10f, 0.08f, 0.22f, new Color(1.00f, 0.97f, 0.92f), 0.97f);

        static readonly SkyLook Dusk = Make(
            new Color(0.10f, 0.13f, 0.20f), new Color(0.62f, 0.36f, 0.30f),
            new Color(0.38f, 0.34f, 0.46f), new Color(0.12f, 0.14f, 0.30f),
            new Color(1.00f, 0.62f, 0.34f), 0.70f, new Vector3(8f, -72f, 0f),
            new Color(0.44f, 0.30f, 0.30f), 0.0042f, 0.70f,
            0.16f, 0.10f, 0.30f, new Color(1.00f, 0.86f, 0.74f), 0.88f);

        static readonly SkyLook Night = Make(
            new Color(0.012f, 0.035f, 0.075f), new Color(0.070f, 0.11f, 0.19f),
            new Color(0.09f, 0.13f, 0.24f), new Color(0.03f, 0.04f, 0.10f),
            new Color(0.62f, 0.74f, 0.98f), 0.22f, new Vector3(40f, 150f, 0f),
            new Color(0.06f, 0.09f, 0.16f), 0.0075f, 0.42f,
            0.22f, 0.12f, 0.36f, new Color(0.62f, 0.70f, 0.88f), 0.72f);

        // ---------------- 雨天（沿用改造前场景的雨天配色，再按时段推） ----------------
        static readonly SkyLook DayRain = Make(
            new Color(0.025f, 0.075f, 0.105f), new Color(0.22f, 0.28f, 0.32f),
            new Color(0.20f, 0.28f, 0.34f), new Color(0.08f, 0.11f, 0.14f),
            new Color(0.64f, 0.71f, 0.78f), 0.30f, new Vector3(34f, -44f, 0f),
            new Color(0.25f, 0.31f, 0.34f), 0.0090f, 0.62f,
            0.34f, 0.16f, 0.60f, new Color(0.84f, 0.88f, 0.92f), 0.80f);

        static readonly SkyLook AfternoonRain = Make(
            new Color(0.030f, 0.080f, 0.100f), new Color(0.26f, 0.27f, 0.28f),
            new Color(0.24f, 0.26f, 0.30f), new Color(0.10f, 0.11f, 0.13f),
            new Color(0.70f, 0.68f, 0.64f), 0.28f, new Vector3(22f, -56f, 0f),
            new Color(0.32f, 0.31f, 0.30f), 0.0100f, 0.58f,
            0.36f, 0.17f, 0.58f, new Color(0.86f, 0.85f, 0.84f), 0.78f);

        static readonly SkyLook DuskRain = Make(
            new Color(0.040f, 0.050f, 0.075f), new Color(0.24f, 0.19f, 0.21f),
            new Color(0.20f, 0.18f, 0.24f), new Color(0.08f, 0.08f, 0.14f),
            new Color(0.58f, 0.50f, 0.52f), 0.18f, new Vector3(7f, -70f, 0f),
            new Color(0.24f, 0.19f, 0.21f), 0.0110f, 0.44f,
            0.44f, 0.20f, 0.62f, new Color(0.78f, 0.72f, 0.74f), 0.66f);

        static readonly SkyLook NightRain = Make(
            new Color(0.008f, 0.016f, 0.032f), new Color(0.050f, 0.070f, 0.100f),
            new Color(0.07f, 0.09f, 0.13f), new Color(0.02f, 0.03f, 0.06f),
            new Color(0.42f, 0.48f, 0.62f), 0.10f, new Vector3(38f, 152f, 0f),
            new Color(0.04f, 0.05f, 0.09f), 0.0120f, 0.30f,
            0.50f, 0.24f, 0.68f, new Color(0.52f, 0.58f, 0.72f), 0.55f);
    }
}
