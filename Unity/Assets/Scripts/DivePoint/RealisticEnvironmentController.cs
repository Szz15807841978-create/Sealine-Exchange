using Crest;
using UnityEngine;

namespace Fishing
{
    /// <summary>
    /// Keeps the Crest ocean, sky and scene lighting in step with the weather + time of day.
    ///
    /// 数值现在全部来自 <see cref="RaftSkyPresets"/>（8 套：4 时段 × 晴/雨），
    /// 这里只负责把它们按当前雨量插值后写进各处的材质与渲染设置 ——
    /// 改数值请改 RaftSkyPresets，不要在这里散落字面量。
    ///
    /// 天空是自定义 shader「WhaleFall/Beautiful Sky Blend」做的双立方图混合：
    /// 每个时段换一张晴天天空（_SunnyTex），雨天共用一张（_RainyTex），
    /// 时段差异再由 _Tint / _Exposure 叠加。
    /// </summary>
    public sealed class RealisticEnvironmentController : MonoBehaviour
    {
        public RaftWeatherController Weather;
        public OceanRenderer Ocean;
        public Material OceanMaterial;
        public Material SkyMaterial;
        public Light Sun;

        [Header("天空立方图（按 白天/下午/傍晚/夜晚 顺序，安装器写入）")]
        public Cubemap[] SunnyByTime = new Cubemap[RaftSkyPresets.TimeCount];
        public Cubemap RainySky;

        [Header("风（Crest 全局风）")]
        public float SunnyWindKph = 18f;
        public float RainyWindKph = 46f;

        [Header("天空盒绕 Y 轴旋转（度）")]
        public float SkyRotation = 0f;

        [Header("时段亮度修正（在天空预设曝光之上再乘一次）")]
        [UnityEngine.Range(0.1f, 2f)] public float MorningBrightness = 1.15f;
        [UnityEngine.Range(0.1f, 2f)] public float AfternoonBrightness = 0.95f;
        [UnityEngine.Range(0.1f, 2f)] public float DuskBrightness = 0.68f;
        [UnityEngine.Range(0.1f, 2f)] public float NightBrightness = 0.40f;
        [UnityEngine.Range(0.1f, 1.5f)] public float RainBrightness = 0.82f;
        [Min(0.05f)] public float BrightnessTransitionSeconds = 1.2f;

        private RaftTimeOfDay _appliedTime = (RaftTimeOfDay)(-1);
        private float _currentSkyExposure;
        private bool _exposureReady;

        private void Awake()
        {
            if (!Weather) Weather = FindObjectOfType<RaftWeatherController>(true);
            Apply();
        }

        private void LateUpdate() => Apply();

        private void Apply()
        {
            float rain = Weather ? Weather.RainAmount : 0f;
            RaftTimeOfDay time = Weather ? Weather.TimeOfDay : RaftTimeOfDay.Day;
            ApplyLook(time, rain, false);
        }

        /// <summary>编辑器预览用：按指定时段/雨量套一遍，不依赖 Play、也不改 Weather 的状态。</summary>
        public void ApplyForPreview(RaftTimeOfDay time, float rain)
        {
            _appliedTime = (RaftTimeOfDay)(-1);   // 强制重贴天空立方图
            ApplyLook(time, rain, true);
        }

        private void ApplyLook(RaftTimeOfDay time, float rain, bool instant)
        {
            SkyLook look = RaftSkyPresets.Mix(time, rain);
            float targetExposure = look.SkyExposure * TimeBrightness(time) * Mathf.Lerp(1f, RainBrightness, rain);
            if (!_exposureReady || instant || !Application.isPlaying)
            {
                _currentSkyExposure = targetExposure;
                _exposureReady = true;
            }
            else
            {
                float blend = 1f - Mathf.Exp(-Time.unscaledDeltaTime * 4f / Mathf.Max(0.05f, BrightnessTransitionSeconds));
                _currentSkyExposure = Mathf.Lerp(_currentSkyExposure, targetExposure, blend);
            }

            // ---- Crest 风 ----
            if (Ocean)
            {
                Ocean._globalWindSpeed = Mathf.Lerp(SunnyWindKph, RainyWindKph, rain);
                Ocean._globalWindTurbulence = Mathf.Lerp(.12f, .48f, rain);
            }

            // ---- 天空盒 ----
            if (SkyMaterial)
            {
                if (time != _appliedTime)
                {
                    ApplySkyCubes(time);
                    _appliedTime = time;
                }

                SetFloat("_Blend", rain);
                SetColor("_Tint", look.SkyTint);
                SetFloat("_Exposure", _currentSkyExposure);
                SetFloat("_Rotation", SkyRotation);

                // 另一套天空盒（CloudSkybox / 程序化天空）的兼容属性：当前 shader 没有就自动跳过
                SetColor("_SkyTint", look.SkyBase);
                SetColor("_GroundColor", look.FogColor * 0.6f);
                SetFloat("_AtmosphereThickness", Mathf.Lerp(.92f, 1.35f, rain));
                // CloudSkybox：bias 越低云越少。雨天加厚但不换成照片级 HDRI。
                SetFloat("_NoiseBias", Mathf.Lerp(-.34f, -.08f, rain));
                SetFloat("_Scatter", Mathf.Lerp(.0065f, .0042f, rain));
                SetFloat("_Extinct", Mathf.Lerp(.008f, .014f, rain));
                SetFloat("_HGCoeff", Mathf.Lerp(.42f, .58f, rain));
            }

            // ---- 海面材质 ----
            if (OceanMaterial)
            {
                SetColor("_Diffuse", look.Water);
                SetColor("_DiffuseGrazing", look.Grazing);
                SetColor("_DiffuseShadow", look.Water * 0.55f);
                SetColor("_SkyBase", look.SkyBase);
                SetColor("_SkyAwayFromSun", look.SkyAway);
                SetFloat("_Roughness", look.Roughness);
                SetFloat("_NormalsStrength", look.Normals);
                SetFloat("_WaveFoamCoverage", look.Foam);
            }

            // ---- 太阳 / 月亮 ----
            if (Sun)
            {
                Sun.intensity = look.SunIntensity;
                Sun.color = look.SunColor;
                Sun.transform.rotation = Quaternion.Euler(look.SunEuler);
            }

            // ---- 雾与环境光 ----
            // 晴朗的白天保持不开雾（和改造前一致），其余时段开雾做空气感
            RenderSettings.fog = rain > 0.02f || time != RaftTimeOfDay.Day;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = look.FogColor;
            RenderSettings.fogDensity = look.FogDensity;
            RenderSettings.ambientIntensity = look.AmbientIntensity;
        }

        private float TimeBrightness(RaftTimeOfDay time)
        {
            switch (time)
            {
                case RaftTimeOfDay.Afternoon: return AfternoonBrightness;
                case RaftTimeOfDay.Dusk: return DuskBrightness;
                case RaftTimeOfDay.Night: return NightBrightness;
                default: return MorningBrightness;
            }
        }

        private void ApplySkyCubes(RaftTimeOfDay time)
        {
            int i = Mathf.Clamp((int)time, 0, RaftSkyPresets.TimeCount - 1);
            if (SunnyByTime != null && i < SunnyByTime.Length && SunnyByTime[i])
                SetTexture("_SunnyTex", SunnyByTime[i]);
            if (RainySky)
                SetTexture("_RainyTex", RainySky);
        }

        private void SetColor(string property, Color value)
        {
            if (OceanMaterial && OceanMaterial.HasProperty(property)) OceanMaterial.SetColor(property, value);
            else if (SkyMaterial && SkyMaterial.HasProperty(property)) SkyMaterial.SetColor(property, value);
        }

        private void SetFloat(string property, float value)
        {
            if (SkyMaterial && SkyMaterial.HasProperty(property)) SkyMaterial.SetFloat(property, value);
        }

        private void SetTexture(string property, Texture value)
        {
            if (SkyMaterial && SkyMaterial.HasProperty(property)) SkyMaterial.SetTexture(property, value);
        }
    }
}
