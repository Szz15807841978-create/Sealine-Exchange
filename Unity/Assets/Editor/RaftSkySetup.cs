using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Fishing;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace FishingEditor
{
    /// <summary>
    /// 给「下水点」场景装上 8 套天空（4 时段 × 晴/雨）+ 调试面板。
    ///
    /// 做什么：
    ///   1. 把 BeautifulSky 素材库里挑好的 4 张晴天天空 + 1 张雨天天空（都是 Cube 导入）接到
    ///      RealisticEnvironmentController 上
    ///   2. 给画布挂 SkyDebugPanel（默认隐藏，F1 唤出）
    ///   3. 渲两张合成预览（白天 / 夜晚）作为验收证据
    ///
    /// 触发：Assets/Editor/RaftSky.request，或菜单 鲸落 → 天空 → 安装天空时段（8 套）。
    /// </summary>
    [InitializeOnLoad]
    public static class RaftSkySetup
    {
        const string Request = "Assets/Editor/RaftSky.request";
        const string Marker = "RaftSkySetup.success.txt";
        const string FailedMarker = "RaftSkySetup.failed.txt";
        const string PreviewDir = "BuoyancyPreview";
        const string CubeDir = "Assets/ThirdParty/BeautifulSky/CubeMap/Textures";

        static readonly string[] Targets =
        {
            "Assets/Scenes/Current/DivePointRealisticOceanSky_Unified.unity",
            "Assets/Scenes/Current/DivePointBeautifulSky.unity",
        };

        static RaftSkySetup() { EditorApplication.delayCall += RunRequest; }

        static void RunRequest()
        {
            if (!File.Exists(Request)) return;
            if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode) return;
            try { Run(true); }
            catch (Exception e) { Debug.LogException(e); WriteMarker(FailedMarker, "EXCEPTION\n" + e); }
            finally
            {
                if (File.Exists(Request)) AssetDatabase.DeleteAsset(Request);
                AssetDatabase.Refresh();
            }
        }

        [MenuItem("鲸落/天空/安装天空时段（8 套）")]
        public static void RunFromMenu() { Run(false); }

        static void WriteMarker(string name, string text)
        {
            try { File.WriteAllText(name, text, new UTF8Encoding(false)); }
            catch (Exception e) { Debug.LogWarning("[天空时段] 写标记失败 " + name + " : " + e.Message); }
        }

        public static void Run(bool quiet)
        {
            var log = new StringBuilder();
            var problems = new List<string>();
            int ok = 0;
            var previous = SceneManager.GetActiveScene();

            foreach (var path in Targets)
            {
                try
                {
                    string summary, reason;
                    ProcessScene(path, out summary, out reason);
                    log.AppendLine(summary);
                    if (string.IsNullOrEmpty(reason)) ok++;
                    else problems.Add(path + " -> " + reason);
                }
                catch (Exception e)
                {
                    problems.Add(path + " -> " + e.Message);
                    log.AppendLine(path + " : EXCEPTION " + e.Message);
                    Debug.LogException(e);
                }
            }
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);

            string header = string.Format("PASS={0}\nScenes={1}\nOke={2}/{3}\n\n{4}\n",
                problems.Count == 0 ? "true" : "false", string.Join(";", Targets), ok, Targets.Length,
                log.ToString().TrimEnd());

            if (problems.Count == 0)
            {
                if (File.Exists(FailedMarker)) File.Delete(FailedMarker);
                WriteMarker(Marker, header);
                Debug.Log("[天空时段] PASS：8 套天空已装好。\n" + log);
            }
            else
            {
                if (File.Exists(Marker)) File.Delete(Marker);
                WriteMarker(FailedMarker, header + "\nPROBLEMS:\n" + string.Join("\n", problems));
                Debug.LogWarning("[天空时段] 未全部完成：\n" + string.Join("\n", problems));
            }

            if (!quiet) EditorUtility.DisplayDialog("天空时段",
                problems.Count == 0 ? "安装完成，详见 " + Marker : "有场景未完成，详见 " + FailedMarker, "好");
        }

        static void ProcessScene(string path, out string summary, out string reason)
        {
            summary = path + " : SKIP";
            reason = "";
            if (!File.Exists(path)) { reason = "场景不存在"; return; }

            var existing = SceneManager.GetSceneByPath(path);
            bool weOpened = false;
            Scene scene;
            if (existing.IsValid() && existing.isLoaded)
            {
                // 场景是脏的也照做：本安装器是按名字增量接线，不覆盖手调内容，
                // 而且成功后的 SaveScene 会把你的未保存改动一并留住（不是丢弃）。
                if (existing.isDirty)
                    Debug.LogWarning("[天空时段] " + path + " 有未保存改动；本次改动会连同它们一起被保存。");
                scene = existing;
            }
            else
            {
                scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                weOpened = true;
            }

            try
            {
                SceneManager.SetActiveScene(scene);
                summary = Install(scene, path, out reason);
                if (string.IsNullOrEmpty(reason)) EditorSceneManager.SaveScene(scene);
            }
            finally
            {
                if (weOpened) EditorSceneManager.CloseScene(scene, true);
            }
        }

        static IEnumerable<T> All<T>(Scene scene) where T : Component
        {
            foreach (var go in scene.GetRootGameObjects())
                foreach (var c in go.GetComponentsInChildren<T>(true))
                    yield return c;
        }

        static string Install(Scene scene, string scenePath, out string reason)
        {
            reason = "";
            var lines = new List<string>();
            var problems = new List<string>();

            var env = All<RealisticEnvironmentController>(scene).FirstOrDefault();
            if (!env) { reason = "找不到 RealisticEnvironmentController"; return scenePath + " : FAIL 无环境控制器"; }

            var weather = All<RaftWeatherController>(scene).FirstOrDefault();
            if (!weather) { reason = "找不到 RaftWeatherController"; return scenePath + " : FAIL 无天气控制器"; }

            var canvas = All<Canvas>(scene).FirstOrDefault(c => c.GetComponent<RaftWeatherController>());
            if (!canvas) { reason = "找不到下水点画布"; return scenePath + " : FAIL 无画布"; }

            if (!env.Weather) env.Weather = weather;

            // ---- 1. 接天空立方图 ----
            env.SunnyByTime = new Cubemap[RaftSkyPresets.TimeCount];
            for (int i = 0; i < RaftSkyPresets.TimeCount; i++)
            {
                string file = RaftSkyPresets.SunnyCubeFiles[i];
                var cube = AssetDatabase.LoadAssetAtPath<Cubemap>(CubeDir + "/" + file + ".png");
                if (!cube) { problems.Add("找不到天空立方图 " + file); continue; }
                env.SunnyByTime[i] = cube;
                lines.Add(string.Format("    {0,-5} 晴天天空 = {1}", RaftSkyPresets.TimeNames[i], file));
            }
            var rainy = AssetDatabase.LoadAssetAtPath<Cubemap>(CubeDir + "/" + RaftSkyPresets.RainyCubeFile + ".png");
            if (!rainy) problems.Add("找不到雨天天空立方图 " + RaftSkyPresets.RainyCubeFile);
            else { env.RainySky = rainy; lines.Add("    雨天天空 = " + RaftSkyPresets.RainyCubeFile + "（4 个时段共用，靠 Tint/Exposure 区分）"); }

            EditorUtility.SetDirty(env);

            // ---- 2. 调试面板 ----
            var panel = canvas.GetComponent<SkyDebugPanel>();
            if (!panel) panel = canvas.gameObject.AddComponent<SkyDebugPanel>();
            panel.Weather = weather;
            panel.TargetCanvas = canvas;
            panel.StartsVisible = false;
            EditorUtility.SetDirty(panel);
            lines.Add("    调试面板 SkyDebugPanel 挂在 " + canvas.name + " 上（默认隐藏，F1 唤出）");

            // ---- 3. 校验 ----
            if (env.SunnyByTime.Count(c => c) != RaftSkyPresets.TimeCount)
                problems.Add("晴天天空没接全（" + env.SunnyByTime.Count(c => c) + "/" + RaftSkyPresets.TimeCount + "）");
            if (!env.RainySky) problems.Add("雨天天空未接");
            if (!env.SkyMaterial) problems.Add("SkyMaterial 为空");
            if (!env.OceanMaterial) problems.Add("OceanMaterial 为空");
            if (!env.Sun) problems.Add("Sun 为空");

            // ---- 4. 合成预览：白天 vs 夜晚 ----
            var sceneCamera = All<Camera>(scene).FirstOrDefault(c => c.targetTexture == null);
            if (sceneCamera && env)
            {
                var saveMode = RenderSettings.fogMode;
                var saveFog = RenderSettings.fog;
                var saveFogColor = RenderSettings.fogColor;
                var saveFogDensity = RenderSettings.fogDensity;
                var saveAmbient = RenderSettings.ambientIntensity;
                var saveTime = weather.TimeOfDay;
                float saveRain = weather.RainAmount;
                float saveTarget = weather.Rainy ? 1f : 0f;
                try
                {
                    string tag = Path.GetFileNameWithoutExtension(scenePath);
                    weather.TimeOfDay = RaftTimeOfDay.Day; weather.RainAmount = 0f;
                    env.ApplyForPreview(RaftTimeOfDay.Day, 0f);
                    CaptureComposite(sceneCamera, canvas, PreviewDir + "/" + tag + "_天空_白天.png");

                    weather.TimeOfDay = RaftTimeOfDay.Night; weather.RainAmount = 0f;
                    env.ApplyForPreview(RaftTimeOfDay.Night, 0f);
                    CaptureComposite(sceneCamera, canvas, PreviewDir + "/" + tag + "_天空_夜晚.png");

                    weather.TimeOfDay = RaftTimeOfDay.Dusk; weather.RainAmount = 1f;
                    env.ApplyForPreview(RaftTimeOfDay.Dusk, 1f);
                    CaptureComposite(sceneCamera, canvas, PreviewDir + "/" + tag + "_天空_傍晚雨.png");

                    lines.Add("    预览图：" + PreviewDir + "/" + tag + "_天空_{白天,夜晚,傍晚雨}.png");
                }
                catch (Exception e) { lines.Add("    预览跳过：" + e.Message); }
                finally
                {
                    weather.TimeOfDay = saveTime;
                    weather.RainAmount = saveRain;
                    weather.Rainy = saveTarget > 0.5f;
                    RenderSettings.fog = saveFog;
                    RenderSettings.fogMode = saveMode;
                    RenderSettings.fogColor = saveFogColor;
                    RenderSettings.fogDensity = saveFogDensity;
                    RenderSettings.ambientIntensity = saveAmbient;
                    env.ApplyForPreview(saveTime, saveRain);
                }
            }

            if (problems.Count > 0)
            {
                reason = string.Join("；", problems);
                lines.Add("  FAIL：" + reason);
                return scenePath + " : FAIL\n" + string.Join("\n", lines);
            }
            return scenePath + " : OK\n" + string.Join("\n", lines);
        }

        static void CaptureComposite(Camera sceneCamera, Canvas canvas, string path)
        {
            if (!sceneCamera || !canvas) return;
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

            var rt = new RenderTexture(1600, 900, 24);
            rt.Create();
            var oldMode = canvas.renderMode;
            var oldCam = canvas.worldCamera;
            var oldPlane = canvas.planeDistance;
            var oldTarget = sceneCamera.targetTexture;
            var oldActive = RenderTexture.active;
            try
            {
                sceneCamera.targetTexture = rt;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = sceneCamera;
                canvas.planeDistance = 10f;

                // 画布上显示的海面/木筏是**别的摄像机渲进 RenderTexture** 的旧内容。
                // 只渲场景摄像机的话，RT 不会刷新，截出来的就是上一帧的图
                //（曾经因此截出三张一模一样的"白天/夜晚/傍晚雨"）。所以先把它们各渲一遍。
                foreach (var cam in Camera.allCameras)
                    if (cam != sceneCamera && cam.targetTexture != null) cam.Render();

                Canvas.ForceUpdateCanvases();
                sceneCamera.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(1600, 900, TextureFormat.RGBA32, false);
                try
                {
                    tex.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
                    tex.Apply();
                    File.WriteAllBytes(path, tex.EncodeToPNG());
                }
                finally { Object.DestroyImmediate(tex); }
            }
            finally
            {
                RenderTexture.active = oldActive;
                sceneCamera.targetTexture = oldTarget;
                canvas.renderMode = oldMode;
                canvas.worldCamera = oldCam;
                canvas.planeDistance = oldPlane;
                rt.Release();
                Object.DestroyImmediate(rt);
                Canvas.ForceUpdateCanvases();
            }
        }
    }
}
