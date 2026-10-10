using System;
using System.IO;
using System.Linq;
using CloudSkybox;
using Crest;
using Fishing;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FishingEditor
{
    [InitializeOnLoad]
    public static class RealisticOceanSkyBuilder
    {
        private const string Request = "Assets/Editor/RealisticOceanSky.request";
        private const string SkyUpgradeRequest = "Assets/Editor/RealisticSkyUpgrade.request";
        private const string BeautifulSkyRequest = "Assets/Editor/BeautifulSkyInstall.request";
        private const string SourceScene = "Assets/Scenes/DivePointImported3D.unity";
        private const string TargetScene = "Assets/Scenes/Current/DivePointRealisticOceanSky.unity";
        private const string ArchiveScene = "Assets/Scenes/Archive/20261004/DivePointImported3D_user_tuned.unity";
        private const string Output = "Assets/Art/Environment/RealisticOceanSky";
        private const int WaterLayer = 4;

        static RealisticOceanSkyBuilder() => EditorApplication.delayCall += RunRequest;

        private static void RunRequest()
        {
            if ((!File.Exists(Request) && !File.Exists(SkyUpgradeRequest) && !File.Exists(BeautifulSkyRequest)) || EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode) return;
            try
            {
                if (File.Exists(Request))
                {
                    Build();
                    AssetDatabase.DeleteAsset(Request);
                }
                if (File.Exists(SkyUpgradeRequest))
                {
                    UpgradeToCloudSkybox();
                    AssetDatabase.DeleteAsset(SkyUpgradeRequest);
                }
                if (File.Exists(BeautifulSkyRequest))
                {
                    InstallBeautifulSky();
                    AssetDatabase.DeleteAsset(BeautifulSkyRequest);
                }
            }
            catch (Exception exception)
            {
                File.WriteAllText("RealisticOceanSkyBuild.failed.txt", exception.ToString());
                Debug.LogException(exception);
            }
        }

        [MenuItem("Fishing/下水点/创建漫画风 Beautiful Sky 场景")]
        public static void InstallBeautifulSky()
        {
            const string sourceScene = "Assets/Scenes/Current/DivePointRealisticOceanSky.unity";
            const string targetScene = "Assets/Scenes/Current/DivePointBeautifulSky.unity";
            const string archiveScene = "Assets/Scenes/Archive/20261004/DivePointRealisticOceanSky_CrestCloudSkybox.unity";
            const string integrationFolder = "Assets/Art/Environment/BeautifulSkyIntegration";
            if (!File.Exists(sourceScene)) throw new FileNotFoundException(sourceScene);
            EnsureFolder(integrationFolder);
            if (!File.Exists(archiveScene) && !AssetDatabase.CopyAsset(sourceScene, archiveScene))
                throw new InvalidOperationException("无法归档上一版写实天空场景。");
            if (File.Exists(targetScene)) throw new InvalidOperationException("Beautiful Sky 场景已存在，为保护调整不自动覆盖。");
            if (!AssetDatabase.CopyAsset(sourceScene, targetScene)) throw new InvalidOperationException("无法复制 Beautiful Sky 场景。");

            var shader = Shader.Find("WhaleFall/Beautiful Sky Blend");
            if (!shader) throw new InvalidOperationException("Beautiful Sky 混合着色器尚未导入。");
            var sunny = AssetDatabase.LoadAssetAtPath<Cubemap>("Assets/ThirdParty/BeautifulSky/CubeMap/Textures/Sky_Anime_15_Day_a.png");
            var rainy = AssetDatabase.LoadAssetAtPath<Cubemap>("Assets/ThirdParty/BeautifulSky/CubeMap/Textures/Sky_Anime_14_Rain_a.png");
            if (!sunny || !rainy) throw new InvalidOperationException("Beautiful Sky 晴天或雨天 Cubemap 尚未导入。");
            var material = new Material(shader) { name = "BeautifulSky_SunnyRainy" };
            material.SetTexture("_SunnyTex", sunny);
            material.SetTexture("_RainyTex", rainy);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_Exposure", 1f);
            material.SetColor("_Tint", Color.white);
            material.SetFloat("_Rotation", 0f);
            AssetDatabase.CreateAsset(material, integrationFolder + "/BeautifulSky_SunnyRainy.mat");

            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.OpenScene(targetScene, OpenSceneMode.Additive);
            var eventSystems = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<EventSystem>(true)).ToArray();
            foreach (var eventSystem in eventSystems) eventSystem.enabled = false;
            try
            {
                var controller = scene.GetRootGameObjects()
                    .SelectMany(x => x.GetComponentsInChildren<RealisticEnvironmentController>(true)).Single();
                controller.SkyMaterial = material;
                RenderSettings.skybox = material;
                foreach (var eventSystem in eventSystems) eventSystem.enabled = true;
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene, targetScene);
                AssetDatabase.SaveAssets();
                File.WriteAllText("BeautifulSkyInstall.success.txt",
                    "PASS\nScene=" + targetScene + "\nSunny=Sky_Anime_15_Day_a\nRainy=Sky_Anime_14_Rain_a\n");
                Debug.Log("[BeautifulSky] PASS: comic skybox collection installed and sunny/rainy blend linked.");
            }
            finally
            {
                foreach (var eventSystem in eventSystems) if (eventSystem) eventSystem.enabled = true;
                EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }

        [MenuItem("Fishing/下水点/接入较写实的体积云天空盒")]
        public static void UpgradeToCloudSkybox()
        {
            if (!File.Exists(TargetScene)) throw new FileNotFoundException(TargetScene);
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.OpenScene(TargetScene, OpenSceneMode.Additive);
            try
            {
                var controller = scene.GetRootGameObjects()
                    .SelectMany(x => x.GetComponentsInChildren<RealisticEnvironmentController>(true)).Single();
                var shader = Shader.Find("CloudSkybox");
                if (!shader) throw new InvalidOperationException("CloudSkybox shader 尚未完成导入。");
                var perlin = AssetDatabase.LoadAssetAtPath<NoiseVolume>("Assets/ThirdParty/CloudSkybox/NoiseVolumes/Perlin.asset");
                var worley = AssetDatabase.LoadAssetAtPath<NoiseVolume>("Assets/ThirdParty/CloudSkybox/NoiseVolumes/Worley.asset");
                if (!perlin || !worley || !perlin.texture || !worley.texture)
                    throw new InvalidOperationException("CloudSkybox 噪声体资源缺失。");

                var sky = controller.SkyMaterial;
                sky.shader = shader;
                sky.SetTexture("_NoiseTex1", perlin.texture);
                sky.SetTexture("_NoiseTex2", worley.texture);
                sky.SetFloat("_SampleCount0", 20f);
                sky.SetFloat("_SampleCount1", 48f);
                sky.SetInt("_SampleCountL", 8);
                sky.SetFloat("_NoiseFreq1", 3.1f);
                sky.SetFloat("_NoiseFreq2", 35.1f);
                sky.SetFloat("_NoiseAmp1", 5f);
                sky.SetFloat("_NoiseAmp2", 1f);
                sky.SetFloat("_NoiseBias", -.34f);
                sky.SetVector("_Scroll1", new Vector4(.006f, .018f, .010f, 0f));
                sky.SetVector("_Scroll2", new Vector4(.010f, .026f, .018f, 0f));
                sky.SetFloat("_Altitude0", 1200f);
                sky.SetFloat("_Altitude1", 3100f);
                sky.SetFloat("_FarDist", 30000f);
                sky.SetFloat("_Scatter", .0065f);
                sky.SetFloat("_HGCoeff", .42f);
                sky.SetFloat("_Extinct", .008f);
                sky.SetFloat("_SunSize", .035f);
                sky.SetFloat("_AtmosphereThickness", .92f);
                sky.SetColor("_SkyTint", new Color(.37f, .56f, .76f));
                sky.SetColor("_GroundColor", new Color(.31f, .36f, .38f));
                sky.SetFloat("_Exposure", 1.05f);
                EditorUtility.SetDirty(sky);
                RenderSettings.skybox = sky;
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene, TargetScene);
                AssetDatabase.SaveAssets();
                File.WriteAllText("RealisticSkyUpgrade.success.txt", "PASS\nShader=CloudSkybox (MIT)\nSunny/rainy cloud density linked\n");
                Debug.Log("[RealisticOceanSky] PASS: CloudSkybox imported and linked to sunny/rainy weather.");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }

        [MenuItem("Fishing/下水点/创建写实海洋与天空场景（保留手调原版）")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请先退出 Play。");
            if (!File.Exists(SourceScene)) throw new FileNotFoundException(SourceScene);
            if (!AssetDatabase.IsValidFolder("Assets/Scenes/Current")) CreateFolder("Assets/Scenes", "Current");
            if (!AssetDatabase.IsValidFolder("Assets/Scenes/Archive")) CreateFolder("Assets/Scenes", "Archive");
            if (!AssetDatabase.IsValidFolder("Assets/Scenes/Archive/20261004")) CreateFolder("Assets/Scenes/Archive", "20261004");
            EnsureFolder(Output);

            if (!File.Exists(ArchiveScene) && !AssetDatabase.CopyAsset(SourceScene, ArchiveScene))
                throw new InvalidOperationException("无法建立手调场景归档副本。");

            if (File.Exists(TargetScene))
                throw new InvalidOperationException("写实环境场景已经存在；为保护调整，不自动覆盖：" + TargetScene);
            if (!AssetDatabase.CopyAsset(SourceScene, TargetScene))
                throw new InvalidOperationException("无法复制写实环境场景。");

            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.OpenScene(TargetScene, OpenSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                Setup(scene);
                EditorSceneManager.SaveScene(scene, TargetScene);
                AssetDatabase.SaveAssets();
                File.WriteAllText("RealisticOceanSkyBuild.success.txt",
                    "PASS\nScene=" + TargetScene + "\nArchive=" + ArchiveScene + "\nCrest=4.23.0 BIRP\n");
                Debug.Log("[RealisticOceanSky] PASS: new scene, archive copy, Crest ocean and editable weather bindings created.");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }

        private static void Setup(Scene scene)
        {
            var roots = scene.GetRootGameObjects();
            var canvas = roots.SelectMany(x => x.GetComponentsInChildren<Canvas>(true)).Single();
            var weather = canvas.GetComponent<RaftWeatherController>();
            if (!weather) throw new InvalidOperationException("没有找到晴雨控制器。");

            foreach (string layerName in new[] { "天空渐变", "独立云层", "海面贴图" })
            {
                var layer = FindDeep(canvas.transform, layerName);
                if (layer) layer.gameObject.SetActive(false);
            }

            var renderPath = Output + "/RealisticOceanSky.renderTexture";
            var render = AssetDatabase.LoadAssetAtPath<RenderTexture>(renderPath);
            if (!render)
            {
                render = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32)
                {
                    name = "RealisticOceanSky",
                    antiAliasing = 2,
                    useMipMap = false
                };
                AssetDatabase.CreateAsset(render, renderPath);
            }

            var skyPath = Output + "/GameRealisticSky.mat";
            var sky = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
            if (!sky)
            {
                var shader = Shader.Find("Skybox/Procedural");
                if (!shader) throw new InvalidOperationException("找不到 Unity Procedural Skybox shader。");
                sky = new Material(shader) { name = "GameRealisticSky" };
                sky.SetFloat("_SunSize", .035f);
                sky.SetFloat("_SunSizeConvergence", 4f);
                sky.SetFloat("_AtmosphereThickness", .92f);
                sky.SetColor("_SkyTint", new Color(.37f, .56f, .76f));
                sky.SetColor("_GroundColor", new Color(.31f, .36f, .38f));
                sky.SetFloat("_Exposure", 1.05f);
                AssetDatabase.CreateAsset(sky, skyPath);
            }

            var oceanMaterialPath = Output + "/CrestOcean_GameRealistic.mat";
            var oceanMaterial = AssetDatabase.LoadAssetAtPath<Material>(oceanMaterialPath);
            if (!oceanMaterial)
            {
                var source = AssetDatabase.LoadAssetAtPath<Material>("Assets/Crest/Crest/Materials/Ocean.mat");
                if (!source) throw new FileNotFoundException("Assets/Crest/Crest/Materials/Ocean.mat");
                oceanMaterial = new Material(source) { name = "CrestOcean_GameRealistic" };
                oceanMaterial.SetColor("_Diffuse", new Color(.035f, .20f, .30f));
                oceanMaterial.SetColor("_DiffuseGrazing", new Color(.34f, .62f, .78f));
                oceanMaterial.SetFloat("_Roughness", .08f);
                AssetDatabase.CreateAsset(oceanMaterial, oceanMaterialPath);
            }

            RenderSettings.skybox = sky;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 1f;

            var environment = new GameObject("写实环境3D_可编辑");
            SceneManager.MoveGameObjectToScene(environment, scene);

            var cameraObject = new GameObject("海洋与天空摄像机");
            cameraObject.transform.SetParent(environment.transform, false);
            cameraObject.transform.position = new Vector3(2000f, 2.8f, 9.5f);
            cameraObject.transform.LookAt(new Vector3(2000f, .45f, -4f));
            var camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 47f;
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 5000f;
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.cullingMask = 1 << WaterLayer;
            camera.targetTexture = render;
            camera.depth = -20f;
            camera.allowHDR = true;
            camera.allowMSAA = true;

            var sunObject = new GameObject("环境主光_晴雨可调");
            sunObject.transform.SetParent(environment.transform, false);
            sunObject.transform.rotation = Quaternion.Euler(28f, -38f, 0f);
            var sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.05f;
            sun.color = new Color(1f, .94f, .82f);
            sun.cullingMask = 1 << WaterLayer;

            var oceanObject = new GameObject("Crest海洋_参数可编辑");
            oceanObject.transform.SetParent(environment.transform, false);
            oceanObject.transform.position = new Vector3(2000f, 0f, 0f);
            var ocean = oceanObject.AddComponent<OceanRenderer>();
            ocean.OceanMaterial = oceanMaterial;
            ocean.ViewCamera = camera;
            ocean.Viewpoint = camera.transform;
            ocean._primaryLight = sun;
            ocean._globalWindSpeed = 18f;
            ocean._globalWindDirectionAngle = -25f;
            ocean._globalWindTurbulence = .12f;
            var oceanSerialized = new SerializedObject(ocean);
            oceanSerialized.FindProperty("_layer").intValue = WaterLayer;
            oceanSerialized.FindProperty("_lodDataResolution").intValue = 256;
            oceanSerialized.FindProperty("_lodCount").intValue = 6;
            oceanSerialized.FindProperty("_createSeaFloorDepthData").boolValue = false;
            oceanSerialized.FindProperty("_createDynamicWaveSim").boolValue = false;
            oceanSerialized.FindProperty("_createFlowSim").boolValue = false;
            oceanSerialized.FindProperty("_createShadowData").boolValue = false;
            oceanSerialized.ApplyModifiedPropertiesWithoutUndo();

            var wavesObject = new GameObject("海浪_晴雨强度可调");
            wavesObject.transform.SetParent(oceanObject.transform, false);
            var waves = wavesObject.AddComponent<ShapeGerstnerBatched>();
            var spectrum = ScriptableObject.CreateInstance<OceanWaveSpectrum>();
            spectrum.name = "RaftGameWaveSpectrum";
            spectrum._multiplier = .42f;
            spectrum._chop = 1.12f;
            spectrum._waveDirectionVariance = 42f;
            AssetDatabase.CreateAsset(spectrum, Output + "/RaftGameWaveSpectrum.asset");
            waves._spectrum = spectrum;
            waves._componentsPerOctave = 6;
            waves._windDirectionAngle = -25f;

            var controller = environment.AddComponent<RealisticEnvironmentController>();
            controller.Weather = weather;
            controller.Ocean = ocean;
            controller.OceanMaterial = oceanMaterial;
            controller.SkyMaterial = sky;
            controller.Sun = sun;

            var oldEnvironment = FindDeep(canvas.transform, "环境与天气");
            var displayParent = oldEnvironment ? oldEnvironment : canvas.transform;
            var displayObject = new GameObject("真实海洋与天空_渲染画面", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            displayObject.transform.SetParent(displayParent, false);
            var rect = (RectTransform)displayObject.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var raw = displayObject.GetComponent<RawImage>();
            raw.texture = render;
            raw.raycastTarget = false;
            displayObject.transform.SetAsFirstSibling();

            foreach (var otherCamera in roots.SelectMany(x => x.GetComponentsInChildren<Camera>(true)))
                if (otherCamera != camera) otherCamera.cullingMask &= ~(1 << WaterLayer);
        }

        private static Transform FindDeep(Transform root, string name)
        {
            return root.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == name);
        }

        private static void CreateFolder(string parent, string child)
        {
            if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(parent, child)))
                throw new IOException("无法建立文件夹：" + parent + "/" + child);
        }

        private static void EnsureFolder(string path)
        {
            var parts = path.Split('/');
            var current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
