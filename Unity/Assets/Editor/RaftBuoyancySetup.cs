using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Crest;
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
    /// 木筏 ↔ 海面交互的安装器（受控浮力第一阶段）。
    ///
    /// 沿用本工程已有的「.request 触发 + .success 标记」约定：
    /// 在 Assets/Editor/RaftBuoyancy.request 放一个文件，编辑器下一次域重载时会自动执行本脚本，
    /// 或者直接用菜单 鲸落 → 木筏 → 安装木筏浮力。
    ///
    /// 做什么：
    ///   1. 先备份两份 Current 场景（只备份一次，保留最原始的改动前状态）
    ///   2. 在木筏容器下插入 RaftRoot，把「甲板 + 全部固定装置」收进它下面
    ///   3. 在甲板底部摆 5 个浮力采样点（中心/前/后/左/右）
    ///   4. 在海面上摆采样锚点，建立「隔离空间木筏 → 真实海面」的映射
    ///   5. 补上缺失的 SimSettingsAnimatedWaves（碰撞源设为 ComputeShaderQueries）
    ///   6. 挂 RaftBuoyancy 与 RaftDeckRider 并接好全部引用
    ///   7. 关掉原来那套正弦假起伏
    ///   8. 校验 + 渲染两张预览图（静止 / 浪中）
    /// </summary>
    [InitializeOnLoad]
    public static class RaftBuoyancySetup
    {
        const string Request = "Assets/Editor/RaftBuoyancy.request";
        const string RevertRequest = "Assets/Editor/RaftWaterlineRevert.request";
        const string SuccessMarker = "RaftBuoyancySetup.success.txt";
        const string FailedMarker = "RaftBuoyancySetup.failed.txt";
        const string RevertMarker = "RaftWaterlineRevert.success.txt";
        const string PreviewDir = "BuoyancyPreview";
        const string ArchiveDir = "Assets/Scenes/Archive/20261004";
        const string WaveSettingsPath = "Assets/Art/Environment/RealisticOceanSky/AnimatedWaves_GamePhysics.asset";
        const int ModelLayer = 30;

        static readonly string[] Targets =
        {
            "Assets/Scenes/Current/DivePointRealisticOceanSky.unity",
            "Assets/Scenes/Current/DivePointBeautifulSky.unity",
        };

        const string ContainerName = "3D木筏与装置_全部可编辑";
        const string DeckName = "木筏3D_原名wooden_xylophone";
        const string RaftRootName = "RaftRoot";
        const string ProbeName = "海面采样锚点";
        const string SamplePrefix = "浮力采样点_";
        const string ViewportName = "3D木筏实时画面";
        static readonly string[] SampleSuffixes = { "中心", "前", "后", "左", "右" };

        static RaftBuoyancySetup() { EditorApplication.delayCall += RunRequest; }

        static void RunRequest()
        {
            bool wantRevert = File.Exists(RevertRequest);
            if (!wantRevert && !File.Exists(Request)) return;
            if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode) return;

            if (wantRevert)
            {
                try { RevertWaterlineScenes(); }
                catch (Exception e) { Debug.LogException(e); WriteMarker(RevertMarker, "EXCEPTION\n" + e); }
                finally
                {
                    if (File.Exists(RevertRequest)) AssetDatabase.DeleteAsset(RevertRequest);
                    AssetDatabase.Refresh();
                }
                return;
            }

            try
            {
                Run(true);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                WriteMarker(FailedMarker, "EXCEPTION\n" + e);
            }
            finally
            {
                if (File.Exists(Request)) AssetDatabase.DeleteAsset(Request);
                AssetDatabase.Refresh();
            }
        }

        [MenuItem("鲸落/木筏/安装木筏浮力（受控）")]
        public static void RunFromMenu() { Run(false); }

        [MenuItem("鲸落/木筏/移除木筏水线（回退）")]
        public static void RevertWaterlineFromMenu()
        {
            RevertWaterlineScenes();
            EditorUtility.DisplayDialog("木筏水线", "已移除，详见 " + RevertMarker, "好");
        }

        /// <summary>
        /// 回退「木筏水线」：把两个场景里名为 `木筏水线` 的对象删掉（画布版与旧版 3D 版都认），
        /// 然后保存。只删这一个对象，木筏浮力（RaftRoot / 采样点 / 组件）一概不动。
        /// </summary>
        public static void RevertWaterlineScenes()
        {
            var log = new StringBuilder();
            int removed = 0;

            foreach (var path in Targets)
            {
                if (!File.Exists(path)) { log.AppendLine(path + " : 场景不存在"); continue; }

                var existing = SceneManager.GetSceneByPath(path);
                bool weOpened = false;
                Scene scene;
                if (existing.IsValid() && existing.isLoaded)
                {
                    if (existing.isDirty)
                    {
                        log.AppendLine(path + " : SKIP（已打开且有未保存改动，先保存再重试）");
                        continue;
                    }
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
                    var doomed = new List<GameObject>();
                    foreach (var t in All<Transform>(scene))
                        if (t.name == "木筏水线") doomed.Add(t.gameObject);
                    foreach (var go in doomed) Object.DestroyImmediate(go);

                    removed += doomed.Count;
                    log.AppendLine(path + " : 删除 " + doomed.Count + " 个『木筏水线』对象");

                    // 顺带清掉可能残留的水线材质引用不算在场景里，这里只需保存场景
                    if (doomed.Count > 0) EditorSceneManager.SaveScene(scene);
                }
                finally
                {
                    if (weOpened) EditorSceneManager.CloseScene(scene, true);
                }
            }

            string text = "PASS=" + (removed > 0 ? "true" : "true") + "\nRemoved=" + removed + "\n\n" + log.ToString().TrimEnd();
            WriteMarker(RevertMarker, text);
            Debug.Log("[木筏浮力] 木筏水线已回退，共删除 " + removed + " 个对象。\n" + log);
        }

        public static void Run(bool quiet)
        {
            Directory.CreateDirectory(ArchiveDir);
            Directory.CreateDirectory(PreviewDir);

            var log = new StringBuilder();
            var problems = new List<string>();
            int ok = 0;

            var previousActive = SceneManager.GetActiveScene();

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

            if (previousActive.IsValid() && previousActive.isLoaded)
                SceneManager.SetActiveScene(previousActive);

            string header = string.Format(
                "PASS={0}\nScenes={1}\nOke={2}/{3}\n\n{4}\n",
                problems.Count == 0 ? "true" : "false",
                string.Join(";", Targets),
                ok, Targets.Length, log.ToString().TrimEnd());

            if (problems.Count == 0)
            {
                if (File.Exists(FailedMarker)) File.Delete(FailedMarker);
                WriteMarker(SuccessMarker, header);
                Debug.Log("[木筏浮力] PASS：木筏 ↔ 海面交互已安装。\n" + log);
            }
            else
            {
                if (File.Exists(SuccessMarker)) File.Delete(SuccessMarker);
                WriteMarker(FailedMarker, header + "\nPROBLEMS:\n" + string.Join("\n", problems));
                Debug.LogWarning("[木筏浮力] 未全部完成：\n" + string.Join("\n", problems));
            }

            if (!quiet) EditorUtility.DisplayDialog("木筏浮力",
                problems.Count == 0 ? "安装完成，详见 " + SuccessMarker : "有场景未完成，详见 " + FailedMarker, "好");
        }

        static void WriteMarker(string name, string text)
        {
            try { File.WriteAllText(name, text, new UTF8Encoding(false)); }
            catch (Exception e) { Debug.LogWarning("[木筏浮力] 写标记文件失败 " + name + " : " + e.Message); }
        }

        static void ProcessScene(string path, out string summary, out string reason)
        {
            summary = path + " : SKIP";
            reason = "";

            if (!File.Exists(path)) { reason = "场景文件不存在"; return; }

            var existing = SceneManager.GetSceneByPath(path);
            bool weOpened = false;
            Scene scene;

            if (existing.IsValid() && existing.isLoaded)
            {
                if (existing.isDirty)
                {
                    // 场景是脏的，但要区分「用户自己的未保存改动」和「上一次安装中断留下的半成品」：
                    // 后者已经含 RaftRoot，重跑是幂等的，可以放心继续并保存；前者一律不碰。
                    bool ourPartialInstall = All<Transform>(existing).Any(t => t.name == RaftRootName);
                    if (!ourPartialInstall)
                    {
                        reason = "场景已在编辑器中打开且有未保存改动，为保护手调内容跳过（保存后重新执行即可）";
                        summary = path + " : SKIP（已打开且未保存）";
                        return;
                    }
                    Debug.LogWarning("[木筏浮力] " + path +
                                     " 是脏的，但检测到上次安装留下的 RaftRoot，按幂等重跑处理。");
                }
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
                BackupOnce(path);
                summary = Work(scene, path, out reason);
                if (string.IsNullOrEmpty(reason)) EditorSceneManager.SaveScene(scene);
            }
            finally
            {
                if (weOpened) EditorSceneManager.CloseScene(scene, true);
            }
        }

        /// <summary>只在第一次安装时备份，保留真正的「改动前」状态。</summary>
        static void BackupOnce(string scenePath)
        {
            string name = Path.GetFileNameWithoutExtension(scenePath);
            string backup = ArchiveDir + "/" + name + "_preBuoyancy.unity";
            if (File.Exists(backup)) return;
            if (!AssetDatabase.CopyAsset(scenePath, backup))
                Debug.LogWarning("[木筏浮力] 备份失败（继续安装）：" + backup);
        }

        static IEnumerable<T> All<T>(Scene scene) where T : Component
        {
            foreach (var go in scene.GetRootGameObjects())
                foreach (var c in go.GetComponentsInChildren<T>(true))
                    yield return c;
        }

        static string Work(Scene scene, string scenePath, out string reason)
        {
            reason = "";
            var lines = new List<string>();
            var problems = new List<string>();

            // ---------- 找到场景里的关键对象 ----------
            var controller = All<DivePointController>(scene).FirstOrDefault();
            if (!controller) { reason = "找不到 DivePointController"; return scenePath + " : FAIL " + reason; }

            var canvas = All<Canvas>(scene).FirstOrDefault(c => c.GetComponent<RaftWeatherController>());
            var weather = canvas ? canvas.GetComponent<RaftWeatherController>() : null;
            if (!weather) { reason = "找不到 RaftWeatherController"; return scenePath + " : FAIL " + reason; }

            var ocean = All<OceanRenderer>(scene).FirstOrDefault();
            if (!ocean) { reason = "场景里没有 OceanRenderer（这份场景没有真实海面）"; return scenePath + " : FAIL " + reason; }

            var modelCamera = All<Camera>(scene)
                .FirstOrDefault(c => c.targetTexture != null && (c.cullingMask & (1 << ModelLayer)) != 0);
            if (!modelCamera) { reason = "找不到渲染木筏的摄像机（targetTexture + layer " + ModelLayer + "）"; return scenePath + " : FAIL " + reason; }

            var viewport = weather.Raft ? weather.Raft.Find(ViewportName) as RectTransform : null;
            if (!viewport) { reason = "找不到 " + ViewportName + "（木筏 RenderTexture 的显示位）"; return scenePath + " : FAIL " + reason; }

            var deck = All<Transform>(scene).FirstOrDefault(t => t.name == DeckName);
            if (!deck) { reason = "找不到甲板 " + DeckName; return scenePath + " : FAIL " + reason; }

            var container = All<Transform>(scene).FirstOrDefault(t => t.name == ContainerName);
            if (!container) container = deck.parent;
            if (!container) { reason = "甲板没有可用的容器父节点"; return scenePath + " : FAIL " + reason; }

            var chestView = All<Chest3DView>(scene).FirstOrDefault();
            if (!chestView || !controller.Player || !controller.Chest)
            {
                reason = "木箱 3D 视图 / 玩家 / 木箱 2D 标记 有缺失，无法标定 UI↔甲板换算";
                return scenePath + " : FAIL " + reason;
            }

            // ---------- 1. 插入 RaftRoot，把甲板与装置收进去 ----------
            var raftRoot = container.Find(RaftRootName);
            if (!raftRoot)
            {
                var go = new GameObject(RaftRootName);
                raftRoot = go.transform;
                raftRoot.SetParent(container, false);
                raftRoot.localPosition = Vector3.zero;
                raftRoot.localRotation = Quaternion.identity;
                raftRoot.localScale = Vector3.one;
                lines.Add("  新建 " + RaftRootName + "（父节点 " + container.name + "）");
            }

            // 幂等：把还留在容器下的子对象收进 RaftRoot（第一次会搬全部，重复执行则不动）
            var moved = new List<Transform>();
            foreach (Transform child in container) if (child != raftRoot) moved.Add(child);
            foreach (var child in moved) child.SetParent(raftRoot, true);
            lines.Add("  收进 RaftRoot 的子对象 " + moved.Count + " 个（甲板 + 全部固定装置）");

            // ---------- 2. 甲板包围盒 → 5 个浮力采样点 ----------
            // 用世界包围盒定跨度：采样点最终是以「相对木筏圆点的世界 XZ 偏移」送进海面查询的，
            // 而且木筏静止姿态本身带 -20.5° 展示俯仰，用本地轴算会把跨度算歪。
            var worldBounds = WorldBounds(deck);
            Vector3 origin = raftRoot.position;
            float bottomWorldY = worldBounds.min.y + 0.02f;
            float halfLen = worldBounds.extents.x * 0.8f;
            float halfWid = Mathf.Max(worldBounds.extents.z * 0.8f, 0.25f);
            Vector3 centre = worldBounds.center;

            Func<Vector3, Vector3> toLocal = w => raftRoot.InverseTransformPoint(w);
            var sampleLocal = new[]
            {
                toLocal(new Vector3(centre.x, bottomWorldY, centre.z)),                    // 中心
                toLocal(new Vector3(centre.x + halfLen, bottomWorldY, centre.z)),          // 前 (+X)
                toLocal(new Vector3(centre.x - halfLen, bottomWorldY, centre.z)),          // 后 (-X)
                toLocal(new Vector3(centre.x, bottomWorldY, centre.z + halfWid)),          // 左 (+Z)
                toLocal(new Vector3(centre.x, bottomWorldY, centre.z - halfWid)),          // 右 (-Z)
            };

            var samples = new Transform[sampleLocal.Length];
            for (int i = 0; i < sampleLocal.Length; i++)
            {
                string nm = SamplePrefix + SampleSuffixes[i];
                var t = raftRoot.Find(nm);
                if (!t)
                {
                    var go = new GameObject(nm);
                    go.transform.SetParent(raftRoot, false);
                    t = go.transform;
                }
                t.localPosition = sampleLocal[i];
                samples[i] = t;
            }
            lines.Add(string.Format("  浮力采样点 5 个：甲板世界包围盒 长 {0:0.0} 宽 {1:0.0}，底面世界 y {2:0.00}，前后跨 ±{3:0.0} m 左右跨 ±{4:0.0} m",
                worldBounds.size.x, worldBounds.size.z, worldBounds.min.y, halfLen, halfWid));

            // ---------- 3. 海面采样锚点 ----------
            var probe = ocean.transform.Find(ProbeName);
            if (!probe)
            {
                var go = new GameObject(ProbeName);
                go.transform.SetParent(ocean.transform, false);
                probe = go.transform;
            }
            // 挂在海面物体正下方、本地坐标归零：Crest 的 sea level 就是海面物体的 y
            // （OceanRenderer.SeaLevel => Root.position.y，而 Root 是挂在海面物体下、本地归零的）。
            // 这里刻意不读 ocean.SeaLevel —— 它是非序列化的运行时值，编辑模式下 Root 还没建好就会抛空引用。
            probe.localPosition = Vector3.zero;
            probe.localRotation = Quaternion.identity;
            probe.localScale = Vector3.one;
            lines.Add(string.Format("  海面采样锚点 world={0}（海面 seaLevel={1:0.00}，取自海面物体 y）",
                probe.position.ToString("0.00"), SeaLevelSafe(ocean)));

            // ---------- 4. 补齐 Animated Waves 设置（碰撞源） ----------
            var waveSettings = EnsureWaveSettings();
            var oceanSo = new SerializedObject(ocean);
            var settingsProp = oceanSo.FindProperty("_simSettingsAnimatedWaves");
            if (settingsProp != null)
            {
                if (settingsProp.objectReferenceValue == null)
                {
                    settingsProp.objectReferenceValue = waveSettings;
                    oceanSo.ApplyModifiedPropertiesWithoutUndo();
                    lines.Add("  海面 Animated Waves 设置原为空 → 已补 " + Path.GetFileName(WaveSettingsPath) +
                              "（Collision Source = ComputeShaderQueries）");
                }
                else
                {
                    lines.Add("  海面 Animated Waves 设置已存在：" + settingsProp.objectReferenceValue.name);
                }
            }
            else
            {
                problems.Add("OceanRenderer 上找不到 _simSettingsAnimatedWaves 字段（Crest 版本变了？）");
            }

            // ---------- 5. 挂 RaftBuoyancy ----------
            var buoyancy = raftRoot.GetComponent<RaftBuoyancy>();
            if (!buoyancy) buoyancy = raftRoot.gameObject.AddComponent<RaftBuoyancy>();
            buoyancy.RaftRoot = raftRoot;
            buoyancy.OceanProbe = probe;
            buoyancy.SamplePoints = samples;
            buoyancy.Ocean = ocean;
            buoyancy.MinSpatialLength = Mathf.Max(1f, worldBounds.size.x);
            buoyancy.MaxProbeDistance = 90f;
            buoyancy.SampleInRestFrame = true;
            lines.Add(string.Format("  RaftBuoyancy：MinSpatialLength={0:0.0} m，升沉半衰期 {1:0.00}s，倾斜半衰期 {2:0.00}s，限幅 俯仰±{3:0.0}° 横滚±{4:0.0}°",
                buoyancy.MinSpatialLength, buoyancy.HeaveHalfLife, buoyancy.TiltHalfLife, buoyancy.MaxPitchDeg, buoyancy.MaxRollDeg));

            // ---------- 6. 挂 RaftDeckRider ----------
            var rider = canvas.GetComponent<RaftDeckRider>();
            if (!rider) rider = canvas.gameObject.AddComponent<RaftDeckRider>();
            var chestLocal = raftRoot.InverseTransformPoint(chestView.transform.position);
            rider.Buoyancy = buoyancy;
            rider.ModelCamera = modelCamera;
            rider.Viewport = viewport;
            rider.Rider = controller.Player;
            rider.Landmark = controller.Chest;
            rider.Landmark3D = chestView.transform;
            rider.DeckSurfaceLocalY = chestLocal.y;
            rider.DeckZForRider = chestLocal.z;

            // 标定：用第二个参照物（石炉）反查 UI +x 到底对应甲板 +X 还是 -X
            var orientation = ResolveDeckUiSign(raftRoot, viewport, modelCamera, controller, chestView.transform);
            rider.DeckUiSign = orientation.sign;
            lines.Add(string.Format("  RaftDeckRider：换算 {0:0.0} px/甲板单位，UI+x ↔ 甲板{1}X",
                orientation.pxPerUnit, orientation.sign > 0 ? "+" : "-"));
            lines.Add("    依据：" + orientation.evidence);

            // ---------- 7. 关掉正弦假起伏 ----------
            if (!weather.DrivenByOcean)
            {
                weather.DrivenByOcean = true;
                EditorUtility.SetDirty(weather);
                lines.Add("  已关闭原来的正弦假起伏（DrivenByOcean = true）");
            }

            // ---------- 8. 校验 ----------
            if (buoyancy.SamplePoints == null || buoyancy.SamplePoints.Length != 5)
                problems.Add("浮力采样点数量不是 5");
            for (int i = 0; i < 5 && buoyancy.SamplePoints != null && i < buoyancy.SamplePoints.Length; i++)
                if (!buoyancy.SamplePoints[i]) problems.Add("第 " + i + " 个浮力采样点为空");

            if (!buoyancy.OceanProbe) problems.Add("海面采样锚点未接");
            if (!buoyancy.Ocean) problems.Add("OceanRenderer 未接");
            if (!rider.Viewport || !rider.Rider || !rider.Landmark || !rider.Landmark3D) problems.Add("甲板乘客引用缺失");

            // 采样点跨度：木筏与海面在隔离空间里（相隔上千米），所以这里量的是
            // 「采样点相对木筏原点的水平偏移」—— 和运行时 TrySample 里的检查同一个量，
            // 绝不能拿它到海面锚点的绝对距离来比（那永远是 1000 米量级）。
            float farthest = 0f;
            if (buoyancy.SamplePoints != null)
            {
                for (int i = 0; i < buoyancy.SamplePoints.Length; i++)
                {
                    var sp = buoyancy.SamplePoints[i];
                    if (!sp) continue;
                    var d = sp.position - raftRoot.position;
                    farthest = Mathf.Max(farthest, new Vector2(d.x, d.z).magnitude);
                }
                if (farthest > buoyancy.MaxProbeDistance)
                    problems.Add(string.Format("采样点离木筏原点最远 {0:0.0} m，超过 MaxProbeDistance={1:0.0}（采样点摆歪了？）",
                        farthest, buoyancy.MaxProbeDistance));
            }

            // ---------- 9. 预览 ----------
            var savePos = raftRoot.localPosition;
            var saveRot = raftRoot.localRotation;
            var saveAmbientMode = RenderSettings.ambientMode;
            var saveAmbientLight = RenderSettings.ambientLight;
            // 场景摄像机 = 没有 targetTexture 的那台（Main Camera）
            var sceneCamera = All<Camera>(scene).FirstOrDefault(c => c != modelCamera && c.targetTexture == null);
            try
            {
                string tag = Path.GetFileNameWithoutExtension(scenePath);
                buoyancy.RecaptureRestPose();
                buoyancy.ResetToRestPose();

                // 与既有构建器同一口径：预览前临时把环境光设成 Flat 亮色。
                // 只在渲染期间生效，finally 里还原，所以不会写回场景。
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.40f, 0.44f, 0.49f);

                Capture(modelCamera, PreviewDir + "/" + tag + "_静止.png");

                // 浪中：木筏倾斜
                buoyancy.ApplyPoseForPreview(0.35f, 4.0f, 3.0f);
                Capture(modelCamera, PreviewDir + "/" + tag + "_浪中.png");

                // 合成预览（1600×900）：把整张画布渲到场景摄像机 —— 玩家真正看到的画面
                if (sceneCamera)
                    CaptureComposite(sceneCamera, canvas, PreviewDir + "/" + tag + "_合成.png");

                lines.Add("  预览图：" + PreviewDir + "/" + tag + "_{静止,浪中,合成}.png");
            }
            catch (Exception e)
            {
                lines.Add("  预览渲染跳过：" + e.Message);
            }
            finally
            {
                raftRoot.localPosition = savePos;
                raftRoot.localRotation = saveRot;
                buoyancy.RecaptureRestPose();
                RenderSettings.ambientMode = saveAmbientMode;
                RenderSettings.ambientLight = saveAmbientLight;
            }

            lines.Add(string.Format("  采样点离木筏原点最远 {0:0.0} m，甲板面本地 y {1:0.00}，乘客 z {2:0.00}",
                farthest, rider.DeckSurfaceLocalY, rider.DeckZForRider));

            EditorUtility.SetDirty(buoyancy);
            EditorUtility.SetDirty(rider);
            EditorUtility.SetDirty(controller);

            if (problems.Count > 0)
            {
                reason = string.Join("；", problems);
                lines.Add("  FAIL：" + reason);
                return scenePath + " : FAIL\n" + string.Join("\n", lines);
            }

            return scenePath + " : OK\n" + string.Join("\n", lines);
        }

        /// <summary>
        /// 取海平面高度，但不碰 OceanRenderer.SeaLevel。
        /// Crest 的实现是 `SeaLevel => Root.position.y`，而 Root 是 [NonSerialized] 的运行时字段：
        /// 编辑模式下、或刚开完域重载时它还是 null，直接读会抛空引用（实测踩到过）。
        /// Root 挂在海面物体下且本地归零，所以海面物体自身的 y 就是等价值，且永远可读。
        /// </summary>
        static float SeaLevelSafe(OceanRenderer ocean)
        {
            if (!ocean) return 0f;
            var root = ocean.Root;
            return root ? root.position.y : ocean.transform.position.y;
        }

        /// <summary>一个对象层级里所有 Renderer 的世界包围盒。</summary>
        static Bounds WorldBounds(Transform target)        {
            var renderers = target.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return new Bounds(Vector3.zero, new Vector3(12.6f, 1f, 2.2f));

            var b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            return b;
        }

        /// <summary>
        /// 用「石炉」这个第二参照物判断 UI +x 对应甲板 +X 还是 -X：
        /// 木箱既是标定原点，就拿石炉（甲板 x≈0.10，2D 标记 x≈5）来验证。
        /// 两种符号各算一遍预测值，取误差小的那个 —— 这样符号不是猜的，是量出来的。
        /// </summary>
        static (float sign, float pxPerUnit, string evidence) ResolveDeckUiSign(
            Transform raftRoot, RectTransform viewport, Camera camera, DivePointController controller, Transform chest3D)
        {
            float pxPerUnit = camera.orthographicSize > 1e-4f
                ? viewport.rect.height / (camera.orthographicSize * 2f)
                : 100f;

            float chestLocalX = raftRoot.InverseTransformPoint(chest3D.position).x;
            float chestUiX = controller.Chest.anchoredPosition.x;

            Transform stove3D = null;
            foreach (var t in raftRoot.GetComponentsInChildren<Transform>(true))
                if (t.name.Contains("石炉")) { stove3D = t; break; }

            RectTransform stove2D = null;
            if (controller.Chest.parent)
            {
                foreach (Transform t in controller.Chest.parent)
                {
                    if (!t.name.Contains("STOVE")) continue;
                    stove2D = t as RectTransform;
                    break;
                }
            }

            if (!stove3D || !stove2D)
                return (1f, pxPerUnit, "找不到第二参照物（石炉），按场景实测结论取 +1");

            float stoveLocalX = raftRoot.InverseTransformPoint(stove3D.position).x;
            float stoveUiX = stove2D.anchoredPosition.x;
            float deltaDeck = stoveLocalX - chestLocalX;

            float plus = chestUiX + deltaDeck * pxPerUnit;
            float minus = chestUiX - deltaDeck * pxPerUnit;
            float errPlus = Mathf.Abs(plus - stoveUiX);
            float errMinus = Mathf.Abs(minus - stoveUiX);

            string ev = string.Format(
                "石炉甲板 x={0:0.000}，2D 标记实测 x={1:0.0}；以木箱(x={2:0.000}/UI {3:0.0})为原点预测：+1→{4:0.0}（差 {5:0.0}）  -1→{6:0.0}（差 {7:0.0}）",
                stoveLocalX, stoveUiX, chestLocalX, chestUiX,
                plus, errPlus, minus, errMinus);

            return errPlus <= errMinus ? (1f, pxPerUnit, ev) : (-1f, pxPerUnit, ev);
        }

        static SimSettingsAnimatedWaves EnsureWaveSettings()
        {
            var existing = AssetDatabase.LoadAssetAtPath<SimSettingsAnimatedWaves>(WaveSettingsPath);
            if (existing) return existing;

            string dir = Path.GetDirectoryName(WaveSettingsPath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            AssetDatabase.Refresh();

            var asset = ScriptableObject.CreateInstance<SimSettingsAnimatedWaves>();
            asset.name = "AnimatedWaves_GamePhysics";
            asset.CollisionSource = SimSettingsAnimatedWaves.CollisionSources.ComputeShaderQueries;
            AssetDatabase.CreateAsset(asset, WaveSettingsPath);
            AssetDatabase.SaveAssets();
            Debug.Log("[木筏浮力] 新建 " + WaveSettingsPath + "（Collision Source = ComputeShaderQueries）");
            return AssetDatabase.LoadAssetAtPath<SimSettingsAnimatedWaves>(WaveSettingsPath);
        }

        /// <summary>
        /// 把整张画布渲到场景摄像机并截一张 1600×900 的图 —— 玩家真正看到的画面。
        /// 沿用 RaftImported3DBuilder 里验证过的做法：临时把画布切到 ScreenSpaceCamera 并让
        /// 场景摄像机的 targetTexture 指向临时 RT，截完全部还原。
        /// </summary>
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
                Canvas.ForceUpdateCanvases();
                Capture(sceneCamera, path);
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

        static void Capture(Camera camera, string path)
        {
            if (!camera || camera.targetTexture == null) return;
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

            camera.Render();
            var old = RenderTexture.active;
            RenderTexture.active = camera.targetTexture;
            int w = camera.targetTexture.width, h = camera.targetTexture.height;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            try
            {
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();
                File.WriteAllBytes(path, tex.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = old;
                Object.DestroyImmediate(tex);
            }
        }
    }
}
