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
    /// 给「下水点」场景换上纸墨风格的 UI 皮肤。
    ///
    /// 形状来自 Kenney「UI Pack - Adventure」(CC0)，色板取自《鲸落-Unity美术风格概念图-v2》：
    ///   纸面 #F2E7CB   墨线 #173A57   琥珀 #DDA64F   青灰 #5D929A   藏蓝 #5E84A3
    ///
    /// 做法是**只换皮、不动布局和逻辑**：按名字找到画布上那批元素，替换 Image 的 sprite / 圆角 9-slice /
    /// 文字颜色，再补上墨线图标。DivePointController / RaftWeatherController / Chest3DView 的引用一律不碰。
    ///
    /// 触发方式沿用本工程约定：放一个 Assets/Editor/RaftUiSkin.request，编辑器下次域重载时自动执行；
    /// 或用菜单 鲸落 → 界面 → 应用纸墨 UI 皮肤。
    /// </summary>
    [InitializeOnLoad]
    public static class RaftUiSkinSetup
    {
        const string Request = "Assets/Editor/RaftUiSkin.request";
        const string Marker = "RaftUiSkinSetup.success.txt";
        const string FailedMarker = "RaftUiSkinSetup.failed.txt";
        const string SkinDir = "Assets/Art/UI/Skin";
        const string KenneyAdventureDir = "Assets/ThirdParty/Kenney/UI Pack Adventure/PNG";
        const string KenneyIconDir = "Assets/ThirdParty/Kenney/Game Icons/PNG";
        const string PreviewDir = "BuoyancyPreview";

        /// <summary>
        /// 目标场景。`_Unified` 是合并摄像机之后的新场景，取代了原来的 DivePointRealisticOceanSky，
        /// 所以只皮肤这两个在用的场景；旧场景如需一并换皮，把它加回这个数组即可。
        /// </summary>
        static readonly string[] Targets =
        {
            "Assets/Scenes/Main.unity",
            "Assets/Scenes/Current/DivePointRealisticOceanSky_Unified.unity",
            "Assets/Scenes/Current/DivePointBeautifulSky.unity",
        };

        // 色板（与概念图取样一致）
        static readonly Color Navy = new Color32(0x17, 0x3A, 0x57, 0xFF);
        static readonly Color NavySoft = new Color32(0x5E, 0x84, 0xA3, 0xFF);
        static readonly Color Cream = new Color32(0xF2, 0xE7, 0xCB, 0xFF);
        static readonly Color White = Color.white;

        /// <summary>
        /// (文件名, 9-slice 边框 左/下/右/上)。
        /// 边框必须小于目标控件短边的一半，否则九宫格四角会互相重叠、出现挤压。
        /// 场景里最小的按钮是「关闭」（约 90×34），所以按钮边框取 12。
        /// </summary>
        static readonly Dictionary<string, Vector4> Borders = new Dictionary<string, Vector4>
        {
            { "panel_paper.png",        new Vector4(16, 16, 16, 16) },
            { "panel_paper_flat.png",   new Vector4(14, 14, 14, 14) },
            { "panel_paper_detail.png", new Vector4(14, 14, 14, 14) },
            { "panel_inset.png",        new Vector4(12, 12, 12, 12) },
            { "panel_grid.png",         new Vector4(14, 14, 14, 14) },
            { "button_amber.png",       new Vector4(12, 12, 12, 12) },
            { "button_teal.png",        new Vector4(12, 12, 12, 12) },
            { "button_navy.png",        new Vector4(12, 12, 12, 12) },
            { "button_paper.png",       new Vector4(12, 12, 12, 12) },
            { "bar_track.png",          new Vector4(6, 6, 6, 6) },
            { "bar_fill.png",           new Vector4(6, 6, 6, 6) },
        };

        static RaftUiSkinSetup() { EditorApplication.delayCall += RunRequest; }

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

        [MenuItem("鲸落/界面/应用纸墨 UI 皮肤")]
        public static void RunFromMenu() { Run(false); }

        static void WriteMarker(string name, string text)
        {
            try { File.WriteAllText(name, text, new UTF8Encoding(false)); }
            catch (Exception e) { Debug.LogWarning("[UI皮肤] 写标记失败 " + name + " : " + e.Message); }
        }

        public static void Run(bool quiet)
        {
            var log = new StringBuilder();
            var problems = new List<string>();
            int ok = 0;

            ApplyImportSettings(problems, log);

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
                Debug.Log("[UI皮肤] PASS：纸墨皮肤已应用。\n" + log);
            }
            else
            {
                if (File.Exists(Marker)) File.Delete(Marker);
                WriteMarker(FailedMarker, header + "\nPROBLEMS:\n" + string.Join("\n", problems));
                Debug.LogWarning("[UI皮肤] 未全部完成：\n" + string.Join("\n", problems));
            }

            if (!quiet) EditorUtility.DisplayDialog("UI 皮肤",
                problems.Count == 0 ? "应用完成，详见 " + Marker : "有场景未完成，详见 " + FailedMarker, "好");
        }

        // ---------------------------------------------------------------- 导入设置
        static void ApplyImportSettings(List<string> problems, StringBuilder log)
        {
            int done = 0;
            foreach (var kv in Borders)
                if (ConfigureSkinTexture(SkinDir + "/" + kv.Key, kv.Value, problems)) done++;

            foreach (var n in new[] { "icon_sun.png", "icon_rain.png", "icon_chest.png", "icon_bag.png", "icon_anchor.png", "icon_whale.png" })
                if (ConfigureSkinTexture(SkinDir + "/" + n, Vector4.zero, problems)) done++;

            log.AppendLine("导入设置：处理 " + done + " 张贴图（Sprite + 九宫格边框 + FullRect）");
        }

        /// <summary>
        /// 把一张皮肤贴图配好：Sprite 单张、九宫格边框、FullRect 网格。
        /// spriteMeshType / spriteBorder 必须走 TextureImporterSettings —— 直接给 TextureImporter
        /// 赋 spriteMeshType 是编译不过的（Tight 网格会把九宫格切坏，所以这个必须设对）。
        /// </summary>
        static bool ConfigureSkinTexture(string path, Vector4 border, List<string> problems)
        {
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null) { problems.Add("找不到贴图 " + path); return false; }

            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;

            var s = new TextureImporterSettings();
            imp.ReadTextureSettings(s);
            s.spriteMode = (int)SpriteImportMode.Single;
            s.spriteBorder = border;
            s.spriteMeshType = SpriteMeshType.FullRect;
            s.spritePixelsPerUnit = 100f;
            s.spriteExtrude = 0;
            s.spriteGenerateFallbackPhysicsShape = false;
            imp.SetTextureSettings(s);

            imp.alphaIsTransparency = true;
            imp.alphaSource = TextureImporterAlphaSource.FromInput;
            imp.mipmapEnabled = false;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.filterMode = FilterMode.Bilinear;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.maxTextureSize = 512;
            imp.SaveAndReimport();
            return true;
        }

        static Sprite S(string name)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>(SkinDir + "/" + name);
        }

        // ---------------------------------------------------------------- 场景
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
                // 同样放宽：换皮只改 UI 元素的 sprite/颜色，不碰玩法与布局，
                // 成功后的 SaveScene 会连同你的未保存改动一起保留。
                if (existing.isDirty)
                    Debug.LogWarning("[UI皮肤] " + path + " 有未保存改动；本次改动会连同它们一起被保存。");
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
                summary = Skin(scene, path, out reason);
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

        static string Skin(Scene scene, string scenePath, out string reason)
        {
            reason = "";
            var lines = new List<string>();
            var problems = new List<string>();

            var canvas = All<Canvas>(scene).FirstOrDefault(c => c.GetComponent<RaftWeatherController>())
                         ?? All<Canvas>(scene).FirstOrDefault(c => c.name == "UICanvas")
                         ?? All<Canvas>(scene).FirstOrDefault();
            if (!canvas) { reason = "找不到画布"; return scenePath + " : FAIL 无画布"; }

            var theme = canvas.GetComponent<UiSkinTheme>();
            if (!theme) theme = canvas.gameObject.AddComponent<UiSkinTheme>();
            theme.Panel = S("panel_paper.png");
            theme.PanelFlat = S("panel_paper_flat.png");
            theme.PanelDetail = S("panel_paper_detail.png");
            theme.PanelInset = S("panel_inset.png");
            theme.PanelGrid = S("panel_grid.png");
            theme.ButtonAmber = S("button_amber.png");
            theme.ButtonTeal = S("button_teal.png");
            theme.ButtonNavy = S("button_navy.png");
            theme.ButtonPaper = S("button_paper.png");
            theme.BarTrack = S("bar_track.png");
            theme.BarFill = S("bar_fill.png");
            theme.Banner = AssetDatabase.LoadAssetAtPath<Sprite>(KenneyAdventureDir + "/banner_hanging.png");
            theme.ButtonClose = AssetDatabase.LoadAssetAtPath<Sprite>(KenneyAdventureDir + "/button_grey_close.png");
            theme.ButtonDanger = AssetDatabase.LoadAssetAtPath<Sprite>(KenneyAdventureDir + "/button_red.png");
            theme.CheckboxOff = AssetDatabase.LoadAssetAtPath<Sprite>(KenneyAdventureDir + "/checkbox_beige_empty.png");
            theme.CheckboxOn = AssetDatabase.LoadAssetAtPath<Sprite>(KenneyAdventureDir + "/checkbox_beige_checked.png");
            theme.ProgressBlue = AssetDatabase.LoadAssetAtPath<Sprite>(KenneyAdventureDir + "/progress_blue.png");
            theme.ProgressGreen = AssetDatabase.LoadAssetAtPath<Sprite>(KenneyAdventureDir + "/progress_green.png");
            theme.ProgressRed = AssetDatabase.LoadAssetAtPath<Sprite>(KenneyAdventureDir + "/progress_red.png");
            theme.Scrollbar = AssetDatabase.LoadAssetAtPath<Sprite>(KenneyAdventureDir + "/scrollbar_grey.png");
            theme.MinimapRing = AssetDatabase.LoadAssetAtPath<Sprite>(KenneyAdventureDir + "/minimap_ring_grey_detail.png");
            theme.IconPause = AssetDatabase.LoadAssetAtPath<Sprite>(KenneyIconDir + "/pause.png");
            theme.IconGear = AssetDatabase.LoadAssetAtPath<Sprite>(KenneyIconDir + "/gear.png");
            theme.IconBasket = AssetDatabase.LoadAssetAtPath<Sprite>(KenneyIconDir + "/basket.png");
            theme.IconWarning = AssetDatabase.LoadAssetAtPath<Sprite>(KenneyIconDir + "/warning.png");
            theme.IconInformation = AssetDatabase.LoadAssetAtPath<Sprite>(KenneyIconDir + "/information.png");
            theme.IconCheck = AssetDatabase.LoadAssetAtPath<Sprite>(KenneyIconDir + "/checkmark.png");
            theme.IconClose = AssetDatabase.LoadAssetAtPath<Sprite>(KenneyIconDir + "/cross.png");
            EditorUtility.SetDirty(theme);
            lines.Add("  运行时主题：已接入 Main / 弹窗 / 代码生成控件");

            // Main 的界面在 Play 时由代码生成；挂好主题后由 UiKit / Widgets 统一换皮。
            if (!canvas.GetComponent<RaftWeatherController>())
                return scenePath + " : OK\n" + string.Join("\n", lines);

            var root = canvas.transform;
            int panels = 0, buttons = 0, slots = 0, texts = 0, icons = 0;

            // ---- 1. 窗口底板 ----
            foreach (var nm in new[] { "木箱界面", "背包界面" })
            {
                var img = FindImage(root, nm);
                if (img == null) { problems.Add("找不到面板 " + nm); continue; }
                SetSliced(img, "panel_paper_flat", White);
                panels++;
            }

            // ---- 2. 按钮 ----
            // 关闭按钮（两个窗口各一个）→ 藏蓝
            foreach (var img in FindImages(root, "关闭")) { SetSliced(img, "button_navy", White); TintText(img, Cream, true); buttons++; }
            // 每行的「拿取」→ 琥珀主操作
            foreach (var img in FindImages(root, "拿取")) { SetSliced(img, "button_amber", White); TintText(img, Navy, true); buttons++; }
            // 全部拿取 → 青灰
            foreach (var img in FindImages(root, "全部拿取")) { SetSliced(img, "button_teal", White); TintText(img, Cream, true); buttons++; }
            // 晴天 / 雨天 → 纸底按钮 + 图标
            var sunny = FindImage(root, "晴天");
            if (sunny) { SetSliced(sunny, "button_paper", White); TintText(sunny, Navy, true); AddIcon(sunny, "icon_sun", 22f, 8f); icons++; buttons++; }
            var rainy = FindImage(root, "雨天");
            if (rainy) { SetSliced(rainy, "button_paper", White); TintText(rainy, Navy, true); AddIcon(rainy, "icon_rain", 22f, 8f); icons++; buttons++; }

            // ---- 3. 背包格子 ----
            foreach (var img in FindImages(root, "格子_"))
            {
                SetSliced(img, "panel_inset", White);
                slots++;
            }

            // ---- 4. 标题底板 + 图标 ----
            var title = FindText(root, "标题");
            if (title)
            {
                PlateBehind(title, "panel_paper_detail", 20f, 9f);
                title.color = Navy; title.fontStyle = FontStyle.Bold;
                AddIcon(title, "icon_anchor", 30f, 34f);
                icons++; texts++;
            }
            var chestTitle = FindText(root, "木箱标题");
            if (chestTitle) { chestTitle.color = Navy; chestTitle.fontStyle = FontStyle.Bold; AddIcon(chestTitle, "icon_chest", 26f, 30f); icons++; texts++; }
            var bagTitle = FindText(root, "背包标题");
            if (bagTitle) { bagTitle.color = Navy; bagTitle.fontStyle = FontStyle.Bold; AddIcon(bagTitle, "icon_bag", 26f, 30f); icons++; texts++; }

            // ---- 5. 状态提示 → 纸片 ----
            var status = FindText(root, "状态提示");
            if (status) { PlateBehind(status, "panel_paper_flat", 26f, 8f); status.color = Navy; texts++; }

            // ---- 6. 其余文字统一到墨色 ----
            foreach (var t in All<Text>(scene))
            {
                if (t == title || t == chestTitle || t == bagTitle || t == status) continue;
                if (t.name == "操作说明" || t.name == "天气状态") { t.color = NavySoft; texts++; continue; }
                if (t.color.a < 0.02f) continue;
                t.color = Navy;
                texts++;
            }

            lines.Add(string.Format("  换皮：面板 {0}，按钮 {1}，格子 {2}，图标 {3}，文字 {4}",
                panels, buttons, slots, icons, texts));

            // ---- 7. 校验 ----
            if (panels < 2) problems.Add("两个窗口底板没换全");
            // Unified 场景目前只保留两个可见窗口按钮；其余操作按钮由玩法层按需生成。
            // 这里校验现有静态按钮均已换皮，不再用旧版场景的固定 6 个数量误报失败。
            if (buttons < 2) problems.Add("现有静态按钮没换全（期望 ≥2，实际 " + buttons + "）");
            if (slots < 12) problems.Add("背包格子没换全（期望 ≥12，实际 " + slots + "）");
            foreach (var nm in new[] { "panel_paper_flat", "button_amber", "button_teal", "button_navy", "button_paper", "panel_inset" })
                if (S(nm + ".png") == null) problems.Add("素材缺失 " + nm);

            // ---- 8. 合成预览 ----
            try
            {
                var modelCamera = All<Camera>(scene).FirstOrDefault(c => c.targetTexture != null && (c.cullingMask & (1 << 30)) != 0);
                var sceneCamera = All<Camera>(scene).FirstOrDefault(c => c != modelCamera && c.targetTexture == null);
                if (sceneCamera)
                {
                    string tag = Path.GetFileNameWithoutExtension(scenePath);
                    CaptureComposite(sceneCamera, canvas, PreviewDir + "/" + tag + "_皮肤.png");
                    lines.Add("  预览图：" + PreviewDir + "/" + tag + "_皮肤.png");
                }
            }
            catch (Exception e) { lines.Add("  预览跳过：" + e.Message); }

            if (problems.Count > 0)
            {
                reason = string.Join("；", problems);
                lines.Add("  FAIL：" + reason);
                return scenePath + " : FAIL\n" + string.Join("\n", lines);
            }
            return scenePath + " : OK\n" + string.Join("\n", lines);
        }

        // ---------------------------------------------------------------- 小工具
        static Image FindImage(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) { var i = t.GetComponent<Image>(); if (i) return i; }
            // 名字带前缀的（格子_01 等）
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name.StartsWith(name)) { var i = t.GetComponent<Image>(); if (i) return i; }
            return null;
        }

        static IEnumerable<Image> FindImages(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name != name && !t.name.StartsWith(name)) continue;
                var i = t.GetComponent<Image>();
                if (i) yield return i;
            }
        }

        static Text FindText(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) { var x = t.GetComponent<Text>(); if (x) return x; }
            return null;
        }

        static void SetSliced(Image img, string spriteName, Color color)
        {
            var sp = S(spriteName + ".png");
            if (!sp) { Debug.LogWarning("[UI皮肤] 缺素材 " + spriteName); return; }
            img.sprite = sp;
            img.type = Image.Type.Sliced;
            img.fillCenter = true;
            img.color = color;
            img.pixelsPerUnitMultiplier = 1f;
            EditorUtility.SetDirty(img);
        }

        static void TintText(Component owner, Color color, bool bold)
        {
            foreach (var t in owner.GetComponentsInChildren<Text>(true))
            {
                t.color = color;
                if (bold) t.fontStyle = FontStyle.Bold;
                EditorUtility.SetDirty(t);
            }
        }

        /// <summary>在控件左侧加一个墨线图标。</summary>
        static void AddIcon(Component owner, string iconName, float size, float leftPad)
        {
            var sp = S(iconName + ".png");
            if (!sp) return;
            var parent = owner.transform;
            const string childName = "图标";
            var t = parent.Find(childName);
            GameObject go;
            if (t) go = t.gameObject;
            else
            {
                go = new GameObject(childName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(parent, false);
                go.transform.SetAsLastSibling();
            }
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(leftPad, 0f);
            rt.sizeDelta = new Vector2(size, size);
            var img = go.GetComponent<Image>();
            img.sprite = sp;
            img.type = Image.Type.Simple;
            img.preserveAspect = true;
            img.raycastTarget = false;
            img.color = Color.white;
            EditorUtility.SetDirty(img);
        }

        /// <summary>
        /// 在文字后面垫一块纸片：作为**兄弟**插在它前面，避免子物体盖住文字。
        /// 底板名字必须带上宿主名字（标题 / 状态提示 的父节点都是画布），
        /// 否则第二次调用会 Find 到第一块底板、把它搬走，标题的底板就会跑到状态提示那儿去。
        /// </summary>
        static void PlateBehind(Component text, string spriteName, float padX, float padY)
        {
            var sp = S(spriteName + ".png");
            if (!sp) return;
            var target = (RectTransform)text.transform;
            if (!target.parent) return;

            string plateName = "底板_" + text.gameObject.name;
            var existing = target.parent.Find(plateName);
            GameObject go;
            if (existing) go = existing.gameObject;
            else
            {
                go = new GameObject(plateName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(target.parent, false);
            }
            var rt = (RectTransform)go.transform;
            rt.anchorMin = target.anchorMin;
            rt.anchorMax = target.anchorMax;
            rt.pivot = target.pivot;
            rt.anchoredPosition = target.anchoredPosition;
            rt.sizeDelta = target.sizeDelta + new Vector2(padX, padY);
            rt.SetSiblingIndex(Mathf.Max(0, target.GetSiblingIndex()));

            var img = go.GetComponent<Image>();
            img.sprite = sp;
            img.type = Image.Type.Sliced;
            img.color = Color.white;
            img.raycastTarget = false;

            // 让底板在运行时按真实字宽重新贴合：编辑器里字体还没赋，量不准
            var fitter = go.GetComponent<UiPlateFitter>();
            if (!fitter) fitter = go.AddComponent<UiPlateFitter>();
            fitter.Target = text as Text;
            fitter.PadX = padX;
            fitter.PadY = padY;
            EditorUtility.SetDirty(fitter);

            EditorUtility.SetDirty(img);
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

                // 先把渲到 RenderTexture 的摄像机各渲一遍，否则画布上是它们的旧内容
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
