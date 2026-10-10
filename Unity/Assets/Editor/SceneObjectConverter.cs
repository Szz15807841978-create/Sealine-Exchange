using System.Linq;
using Fishing;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FishingEditor
{
    /// <summary>把 Main 场景从空场景转换为可在 Hierarchy 中查看的第一阶段场景。</summary>
    [InitializeOnLoad]
    public static class SceneObjectConverter
    {
        private const string ScenePath = "Assets/Scenes/Main.unity";
        private const string RootName = "Fishing Scene Objects (Stage 1)";
        private const string SessionKey = "Fishing.SceneConversion.Stage1.Cleanup.v3";
        private const string StripControlsRequestPath = "Assets/Editor/StripControlsConversion.request";
        private const string EditableRuntimeUiRequestPath = "Assets/Editor/EditableRuntimeUi.request";

        static SceneObjectConverter()
        {
            // 只自动清理异常中断留下的半成品。正式改写场景必须由用户从菜单触发，
            // 避免脚本导入期间意外修改当前正在编辑的场景。
            EditorApplication.delayCall += AutoCleanupOnce;
            EditorApplication.delayCall += RunRequestedStripControlsConversion;
            EditorApplication.delayCall += RunRequestedEditableRuntimeUi;
        }

        private static void RunRequestedEditableRuntimeUi()
        {
            if (!AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(EditableRuntimeUiRequestPath)) return;
            try
            {
                ConvertEditableRuntimeUiFromMenu();
                AssetDatabase.DeleteAsset(EditableRuntimeUiRequestPath);
                AssetDatabase.Refresh();
            }
            catch (System.Exception ex) { Debug.LogException(ex); }
        }

        private static void RunRequestedStripControlsConversion()
        {
            if (!AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(StripControlsRequestPath)) return;
            try
            {
                ConvertStripAndControlsFromMenu();
                AssetDatabase.DeleteAsset(StripControlsRequestPath);
                AssetDatabase.Refresh();
            }
            catch (System.Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        [MenuItem("Fishing/场景化/生成第一阶段场景对象")]
        public static void ConvertFromMenu()
        {
            Convert(true);
        }

        [MenuItem("Fishing/场景化/生成可编辑UI场景对象")]
        public static void ConvertEditableUiFromMenu()
        {
            CleanupFailedConversions();

            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool wasLoaded = scene.IsValid() && scene.isLoaded;
            if (!wasLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

            var root = scene.GetRootGameObjects().FirstOrDefault(x => x.name == RootName);
            if (root == null) root = Build(scene);

            var canvas = root.GetComponentInChildren<Canvas>(true);
            if (canvas == null) throw new MissingComponentException("场景化根对象中没有 Canvas。");

            var oldLayout = canvas.transform.Find("Scene UI Layout");
            if (oldLayout != null) Object.DestroyImmediate(oldLayout.gameObject);

            var layout = BuildEditableUi(canvas.transform);
            var oldPreview = canvas.transform.Find("Edit Mode Curve Preview");
            if (oldPreview != null) oldPreview.gameObject.SetActive(false);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Fishing] 可编辑 UI 已写入 Main.unity；面板位置和尺寸以场景对象为准。", layout);

            if (wasLoaded) Selection.activeGameObject = layout;
            else EditorSceneManager.CloseScene(scene, true);
        }

        [MenuItem("Fishing/场景化/仅实体化持仓条和操作区")]
        public static void ConvertStripAndControlsFromMenu()
        {
            CleanupFailedConversions();

            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool wasLoaded = scene.IsValid() && scene.isLoaded;
            if (!wasLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

            var root = scene.GetRootGameObjects().FirstOrDefault(x => x.name == RootName);
            if (root == null) throw new MissingComponentException("Main 场景缺少场景化根对象。");
            var canvas = root.GetComponentInChildren<Canvas>(true);
            var layout = canvas != null ? canvas.transform.Find("Scene UI Layout") : null;
            if (layout == null) throw new MissingComponentException("Main 场景缺少 Scene UI Layout。");

            var strip = layout.Find("持仓条") as RectTransform;
            var controls = layout.Find("操作区") as RectTransform;
            if (strip == null || controls == null) throw new MissingComponentException("场景中缺少持仓条或操作区。");

            ClearImmediate(strip);
            ClearImmediate(controls);
            BuildEditableStrip(strip);
            BuildEditableControls(controls);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Fishing] 已仅实体化持仓条和操作区；其他 UI 场景对象未重建。", strip);

            if (wasLoaded) Selection.activeGameObject = strip.gameObject;
            else EditorSceneManager.CloseScene(scene, true);
        }

        [MenuItem("Fishing/场景化/实体化状态栏、数据条和动态锚点")]
        public static void ConvertEditableRuntimeUiFromMenu()
        {
            CleanupFailedConversions();
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool wasLoaded = scene.IsValid() && scene.isLoaded;
            if (!wasLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

            var root = scene.GetRootGameObjects().FirstOrDefault(x => x.name == RootName);
            if (root == null) throw new MissingComponentException("Main 场景缺少场景化根对象。");
            var canvas = root.GetComponentInChildren<Canvas>(true);
            var layout = canvas != null ? canvas.transform.Find("Scene UI Layout") : null;
            if (layout == null) throw new MissingComponentException("Main 场景缺少 Scene UI Layout。");

            var hud = layout.Find("状态栏") as RectTransform;
            var stats = layout.Find("数据条") as RectTransform;
            var chart = layout.Find("曲线区") as RectTransform;
            var overlay = layout.Find("遮罩层") as RectTransform;
            var modal = layout.Find("窗口层") as RectTransform;
            if (!hud || !stats || !chart || !overlay || !modal)
                throw new MissingComponentException("Main 场景的可编辑 UI 宿主不完整。");

            ClearImmediate(hud);
            ClearImmediate(stats);
            BuildEditableHud(hud);
            BuildEditableStats(stats);
            EnsureAnchor(chart, "仪表盘锚点", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 14f), 600f, 78f);
            EnsureAnchor(overlay, "动态窗口锚点", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, 760f, 560f);
            EnsureAnchor(modal, "动态窗口锚点", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, 760f, 560f);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Fishing] 状态栏、数据条、仪表盘与动态窗口已改为场景可编辑对象/锚点。", layout);
            if (wasLoaded) Selection.activeGameObject = hud.gameObject;
            else EditorSceneManager.CloseScene(scene, true);
        }

        [MenuItem("Fishing/场景化/恢复可游玩的运行时场景")]
        public static void RestorePlayableScene()
        {
            CleanupFailedConversions();
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Debug.Log("[Fishing] 已重新打开干净的 Main 场景；可直接进入 Play。场景化请在确认可玩后手动执行。");
        }

        private static void AutoCleanupOnce()
        {
            if (SessionState.GetBool(SessionKey, false) || EditorApplication.isPlayingOrWillChangePlaymode) return;
            SessionState.SetBool(SessionKey, true);
            CleanupFailedConversions();
        }

        private static void Convert(bool selectRoot)
        {
            CleanupFailedConversions();

            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool wasLoaded = scene.IsValid() && scene.isLoaded;
            if (!wasLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

            var existing = scene.GetRootGameObjects().FirstOrDefault(x => x.name == RootName);
            if (existing != null && !IsComplete(existing))
            {
                Object.DestroyImmediate(existing);
                existing = null;
            }
            if (existing == null)
            {
                try
                {
                    existing = Build(scene);
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    Debug.Log("[Fishing] Main 场景第一阶段转换完成：基础对象和编辑模式曲线已经写入 Hierarchy。", existing);
                }
                catch
                {
                    if (existing != null) Object.DestroyImmediate(existing);
                    throw;
                }
            }

            if (selectRoot) Selection.activeGameObject = existing;
            if (!wasLoaded) EditorSceneManager.CloseScene(scene, true);
        }

        /// <summary>清理上一次异常中断时遗留在临时/恢复场景里的半成品，避免持续刷异常。</summary>
        private static void CleanupFailedConversions()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene loaded = SceneManager.GetSceneAt(i);
                if (!loaded.IsValid() || !loaded.isLoaded) continue;
                foreach (var root in loaded.GetRootGameObjects())
                {
                    if (root.name != RootName) continue;
                    bool complete = IsComplete(root);
                    if (!complete) Object.DestroyImmediate(root);
                }
            }
        }

        private static bool IsComplete(GameObject root)
        {
            if (root == null || root.GetComponentInChildren<SceneGameHost>(true) == null) return false;
            var curve = root.GetComponentInChildren<SceneCurvePreview>(true);
            return curve != null && curve.GetComponent<CanvasRenderer>() != null;
        }

        private static GameObject Build(Scene scene)
        {
            var root = new GameObject(RootName);
            SceneManager.MoveGameObjectToScene(root, scene);
            try
            {
                BuildCamera(root.transform);
                BuildEventSystem(root.transform);
                Canvas canvas = BuildCanvas(root.transform);
                GameObject preview = BuildPreview(canvas.transform);

                var hostGo = new GameObject("Scene Game Host");
                hostGo.transform.SetParent(root.transform, false);
                var host = hostGo.AddComponent<SceneGameHost>();
                var so = new SerializedObject(host);
                so.FindProperty("targetCanvas").objectReferenceValue = canvas;
                so.FindProperty("editModePreview").objectReferenceValue = preview;
                so.ApplyModifiedPropertiesWithoutUndo();

                return root;
            }
            catch
            {
                Object.DestroyImmediate(root);
                throw;
            }
        }

        private static void BuildCamera(Transform parent)
        {
            var go = new GameObject("Main Camera");
            go.transform.SetParent(parent, false);
            go.tag = "MainCamera";
            go.transform.localPosition = new Vector3(0f, 0f, -10f);
            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Palette.Bg;
            cam.orthographic = true;
            cam.orthographicSize = 5f;
        }

        private static void BuildEventSystem(Transform parent)
        {
            var go = new GameObject("EventSystem");
            go.transform.SetParent(parent, false);
            go.AddComponent<EventSystem>();
            go.AddComponent<StandaloneInputModule>();
        }

        private static Canvas BuildCanvas(Transform parent)
        {
            var go = new GameObject("UICanvas", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1220f, 700f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        private static GameObject BuildPreview(Transform canvas)
        {
            var root = new GameObject("Edit Mode Curve Preview", typeof(RectTransform));
            root.transform.SetParent(canvas, false);
            var rootRt = (RectTransform)root.transform;
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            var panel = new GameObject("Chart Preview Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(root.transform, false);
            var panelRt = (RectTransform)panel.transform;
            panelRt.anchorMin = new Vector2(0.5f, 0.5f);
            panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.pivot = new Vector2(0.5f, 0.5f);
            panelRt.sizeDelta = new Vector2(1100f, 500f);
            panel.GetComponent<Image>().color = Palette.Panel;

            var curve = new GameObject("Up Green Down Red Curve", typeof(RectTransform), typeof(CanvasRenderer));
            curve.transform.SetParent(panel.transform, false);
            var curveRt = (RectTransform)curve.transform;
            curveRt.anchorMin = Vector2.zero;
            curveRt.anchorMax = Vector2.one;
            curveRt.offsetMin = new Vector2(12f, 12f);
            curveRt.offsetMax = new Vector2(-12f, -12f);
            curve.AddComponent<SceneCurvePreview>();

            var titleGo = new GameObject("Preview Label", typeof(RectTransform));
            titleGo.transform.SetParent(panel.transform, false);
            var titleRt = (RectTransform)titleGo.transform;
            titleRt.anchorMin = new Vector2(0f, 1f);
            titleRt.anchorMax = new Vector2(0f, 1f);
            titleRt.pivot = new Vector2(0f, 1f);
            titleRt.anchoredPosition = new Vector2(18f, -12f);
            titleRt.sizeDelta = new Vector2(500f, 28f);
            var title = titleGo.AddComponent<Text>();
            title.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            title.fontSize = 16;
            title.fontStyle = FontStyle.Bold;
            title.color = Palette.Text;
            title.text = "编辑模式曲线预览 · 上涨绿色 / 下跌红色";

            return root;
        }

        private static GameObject BuildEditableUi(Transform canvas)
        {
            var root = new GameObject("Scene UI Layout", typeof(RectTransform));
            root.transform.SetParent(canvas, false);
            Stretch((RectTransform)root.transform);

            var background = new GameObject("03-等待收网背景", typeof(RectTransform), typeof(RawImage));
            background.transform.SetParent(root.transform, false);
            var bgRt = (RectTransform)background.transform;
            Stretch(bgRt);
            var raw = background.GetComponent<RawImage>();
            raw.raycastTarget = false;
            raw.texture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/Art/03-等待收网.png");
            raw.color = raw.texture != null ? Color.white : Palette.Bg;
            if (raw.texture != null)
            {
                var aspect = background.AddComponent<AspectRatioFitter>();
                aspect.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                aspect.aspectRatio = (float)raw.texture.width / raw.texture.height;
            }

            var hud = MakePanel(root.transform, "状态栏");
            Place(hud, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(14f, -10f), 378f, 46f);
            BuildEditableHud(hud);

            var chart = MakePanel(root.transform, "曲线区");
            Place(chart, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-14f, -18f), 671f, 322f);
            BuildEditableChart(chart);

            var stats = MakePanel(root.transform, "数据条");
            Place(stats, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-14f, -349f), 671f, 52f);
            BuildEditableStats(stats);

            var strip = MakePanel(root.transform, "持仓条");
            Place(strip, Vector2.zero, Vector2.zero, new Vector2(14f, 69f), 586f, 46f);
            BuildEditableStrip(strip);

            var controls = MakePanel(root.transform, "操作区");
            Place(controls, Vector2.zero, Vector2.zero, new Vector2(14f, 12f), 720f, 48f);
            BuildEditableControls(controls);

            var overlay = MakeLayer(root.transform, "遮罩层", Palette.OverlayBg);
            BuildPreopenPanel(overlay);
            overlay.gameObject.SetActive(false);
            var modal = MakeLayer(root.transform, "窗口层", Palette.OverlayBg);
            modal.gameObject.SetActive(false);
            MakeLayer(root.transform, "Toast层", Color.clear);

            EnsureAnchor(chart, "仪表盘锚点", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 14f), 600f, 78f);
            EnsureAnchor(overlay, "动态窗口锚点", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, 760f, 560f);
            EnsureAnchor(modal, "动态窗口锚点", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, 760f, 560f);

            background.transform.SetAsFirstSibling();
            return root;
        }

        private static void BuildEditableHud(RectTransform root)
        {
            MakeFixedText(root, "标题", "鲸落", 22, FontStyle.Bold,
                          new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(12f, 0f), 76f, 28f, TextAnchor.MiddleLeft);
            MakeFixedText(root, "天次", "第 1 天 · 第 1 场", 13, FontStyle.Bold,
                          new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(94f, 0f), 155f, 22f, TextAnchor.MiddleLeft);
            var timer = MakePanel(root, "倒计时底");
            Place(timer, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-12f, 0f), 104f, 28f);
            var key = MakeText(timer, "键", "收市", 12, TextAnchor.MiddleLeft, 12f, 0f, 54f, 0f);
            key.fontStyle = FontStyle.Normal; key.color = Palette.Muted;
            MakeText(timer, "值", "120.0s", 16, TextAnchor.MiddleRight, 40f, 0f, 10f, 0f);
        }

        private static void BuildEditableStats(RectTransform root)
        {
            string[] names = { "当前价", "持仓", "浮动", "本场收益" };
            string[] values = { "100.0", "0 张 / 0.0", "+0.0", "+0.0" };
            float cellW = 154f;
            for (int i = 0; i < names.Length; i++)
            {
                var cell = MakePanel(root, names[i]);
                Place(cell, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(12f + i * (cellW + 8f), 0f), cellW, 36f);
                var key = MakeText(cell, "键", names[i], 11, TextAnchor.UpperLeft, 10f, 2f, 8f, 20f);
                key.fontStyle = FontStyle.Normal; key.color = Palette.Muted;
                MakeText(cell, "值", values[i], 16, TextAnchor.LowerLeft, 10f, 15f, 8f, 1f);
            }
        }

        private static RectTransform EnsureAnchor(RectTransform parent, string name, Vector2 anchor, Vector2 pivot,
                                                  Vector2 position, float width, float height)
        {
            var existing = parent.Find(name) as RectTransform;
            if (existing) return existing;
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            Place(rt, anchor, pivot, position, width, height);
            return rt;
        }

        private static void BuildEditableStrip(RectTransform root)
        {
            var row = UiKit.Rect("行", root);
            UiKit.Stretch(row, 12f, 7f, 12f, 7f);
            UiKit.Row(row, 10f);

            var viewport = UiKit.Rect("胶囊视口", row);
            var viewportSize = UiKit.Size(viewport, 120f, 32f, 1f);
            viewportSize.minWidth = 120f;
            viewport.gameObject.AddComponent<RectMask2D>();

            var strip = UiKit.Rect("胶囊区", viewport);
            strip.anchorMin = strip.anchorMax = new Vector2(0f, 0.5f);
            strip.pivot = new Vector2(0f, 0.5f);
            strip.anchoredPosition = Vector2.zero;
            strip.sizeDelta = new Vector2(0f, 32f);
            var stripLayout = UiKit.Row(strip, 7f);
            stripLayout.childAlignment = TextAnchor.MiddleLeft;
            strip.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = strip;
            scroll.horizontal = true;
            scroll.vertical = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.inertia = true;
            scroll.scrollSensitivity = 24f;

            var chip = UiKit.Outline("持仓胶囊模板", strip, Palette.ChipBg, Palette.LineSoft);
            UiKit.Size(chip.rectTransform, 116f, 30f);
            UiKit.MinSize(chip.rectTransform, 116f, 30f);
            UiKit.Row(chip.rectTransform, 5f, new RectOffset(9, 9, 0, 0));
            var dot = UiKit.Block("点", chip.rectTransform, Palette.Cyan);
            UiKit.Size(dot.rectTransform, 8f, 8f);
            UiKit.MinSize(dot.rectTransform, 8f, 8f);
            var number = Txt.Make(chip.rectTransform, "编号", 12, Palette.Muted, TextAnchor.MiddleLeft);
            number.text = "网#1";
            UiKit.Size(number.rectTransform, 42f, 18f);
            var pl = Txt.Make(chip.rectTransform, "盈亏", 12, Palette.GreenSoft, TextAnchor.MiddleRight, FontStyle.Bold);
            pl.text = "+0.0";
            UiKit.Size(pl.rectTransform, 44f, 18f);
            var chipButton = chip.gameObject.AddComponent<Button>();
            chipButton.targetGraphic = chip;
            chip.gameObject.SetActive(false);

            var manage = MakeSceneButton(row, "持仓管理", "持仓管理", Palette.CyanBot, Palette.BtnCyanText,
                                         new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, 96f, 32f, 12);
            SetLayoutSize(manage.GetComponent<RectTransform>(), 96f, 32f);
        }

        private static void BuildEditableControls(RectTransform root)
        {
            var row = UiKit.Rect("行", root);
            UiKit.Stretch(row, 12f, 8f, 12f, 8f);
            UiKit.Row(row, 10f);

            var pause = MakeSceneButton(row, "暂停", "暂停", Palette.ButtonBg, Palette.Text,
                                        new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, 78f, 32f, 13);
            SetLayoutSize(pause.GetComponent<RectTransform>(), 78f, 32f);
            var collect = MakeSceneButton(row, "全部收网", "全部收网", Palette.CyanBot, Palette.BtnCyanText,
                                          new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, 100f, 32f, 13);
            SetLayoutSize(collect.GetComponent<RectTransform>(), 100f, 32f);

            var cd = UiKit.Rect("CD", row);
            UiKit.Size(cd, 190f, 32f, 1f);
            var cdRow = UiKit.Row(cd, 9f);
            cdRow.childAlignment = TextAnchor.MiddleLeft;

            var cdBg = UiKit.Outline("CD条", cd, Palette.ChipBg, Palette.LineSoft);
            UiKit.Size(cdBg.rectTransform, 110f, 9f, 1f);
            var fillHost = UiKit.Rect("填充区", cdBg.rectTransform);
            UiKit.Stretch(fillHost, 1f, 1f, 1f, 1f);
            var fill = UiKit.Gradient("填充", fillHost, Palette.GaugeTimerB, Palette.GaugeTimerA, 0f);
            UiKit.Stretch(fill.rectTransform);

            var cdText = Txt.Make(cd, "CD文字", 12, Palette.Muted, TextAnchor.MiddleLeft);
            cdText.text = "可收网";
            UiKit.Size(cdText.rectTransform, 74f, 20f);

            var endDay = MakeSceneButton(row, "结束今天", "结束今天", Color.clear, Palette.Text,
                                         new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, 86f, 32f, 13);
            SetLayoutSize(endDay.GetComponent<RectTransform>(), 86f, 32f);
            var hint = Txt.Make(row, "提示", 12, Palette.Muted, TextAnchor.MiddleLeft);
            hint.text = "空格 暂停";
            UiKit.Size(hint.rectTransform, 260f, 20f, 1f);
        }

        private static void ClearImmediate(Transform root)
        {
            for (int i = root.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(root.GetChild(i).gameObject);
        }

        private static void SetLayoutSize(RectTransform rt, float width, float height)
        {
            var layout = rt.GetComponent<LayoutElement>();
            if (layout == null) layout = rt.gameObject.AddComponent<LayoutElement>();
            layout.minWidth = width;
            layout.preferredWidth = width;
            layout.minHeight = height;
            layout.preferredHeight = height;
        }

        private static RectTransform MakePanel(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Outline));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = Palette.Panel;
            var outline = go.GetComponent<Outline>();
            outline.effectColor = Palette.Line;
            outline.effectDistance = new Vector2(2f, -2f);
            return (RectTransform)go.transform;
        }

        private static RectTransform MakeLayer(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            Stretch(rt);
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = color.a > 0f;
            return rt;
        }

        private static Text MakeText(RectTransform parent, string name, string value, int size,
                                     TextAnchor anchor, float left, float top, float right, float bottom)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            Stretch(rt, left, top, right, bottom);
            var text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.fontStyle = FontStyle.Bold;
            text.color = Palette.Text;
            text.alignment = anchor;
            text.text = value;
            return text;
        }

        private static void BuildPreopenPanel(RectTransform overlay)
        {
            var panel = MakePanel(overlay, "准备开始面板");
            Place(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, 620f, 250f);

            MakeFixedText(panel, "标题", "第 1 天 · 早晨", 24, FontStyle.Bold,
                          new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -18f), 360f, 34f, TextAnchor.MiddleLeft);
            MakeFixedText(panel, "下网说明", "下网数量 · 可选择 0～1 张", 15, FontStyle.Bold,
                          new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -67f), 400f, 26f, TextAnchor.MiddleLeft);

            var stepper = new GameObject("网数控件", typeof(RectTransform));
            stepper.transform.SetParent(panel, false);
            var stepRt = (RectTransform)stepper.transform;
            Place(stepRt, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -105f), 132f, 42f);
            MakeSceneButton(stepRt, "减少", "−", Palette.ButtonBg, Palette.BtnCyanText,
                            new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, 42f, 42f, 20);
            MakeFixedText(stepRt, "数值", "1", 20, FontStyle.Bold,
                          new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, 48f, 42f, TextAnchor.MiddleCenter);
            MakeSceneButton(stepRt, "增加", "+", Palette.ButtonBg, Palette.BtnCyanText,
                            new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, 42f, 42f, 20);

            MakeFixedText(panel, "下网摘要", "下水 1 张 · 留手里 0 张 · 买入价 100.0", 14, FontStyle.Normal,
                          new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(174f, -105f), 420f, 42f, TextAnchor.MiddleLeft);

            MakeSceneButton(panel, "跳过这一场", "跳过这一场", Color.clear, Palette.Text,
                            new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-174f, 22f), 122f, 38f, 14);
            MakeSceneButton(panel, "开始捕捞", "开始捕捞", Palette.PrimaryBot, Palette.BtnPrimaryText,
                            new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-24f, 22f), 138f, 38f, 14);
        }

        private static void BuildEditableChart(RectTransform chart)
        {
            MakeFixedText(chart, "行情标题", "股市走向", 21, FontStyle.Bold,
                          new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -10f), 132f, 30f, TextAnchor.MiddleLeft);

            var labels = new GameObject("标签层", typeof(RectTransform));
            labels.transform.SetParent(chart, false);
            var labelsRt = (RectTransform)labels.transform;
            Stretch(labelsRt);
            MakeFixedText(labelsRt, "开盘提示", "开盘前 · 曲线将从 100.0 开始向右延伸", 13, FontStyle.Normal,
                          new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, 420f, 20f, TextAnchor.MiddleCenter).color = Palette.EmptyHint;

            MakeChartLabelGroup(labelsRt, "Y轴标签", 24, 52f, 16f, 11, Palette.AxisText, TextAnchor.MiddleRight);
            MakeChartLabelGroup(labelsRt, "X轴标签", 16, 40f, 16f, 11, Palette.TimeText, TextAnchor.UpperCenter);
            MakeChartLabelGroup(labelsRt, "丢失线标签", 16, 170f, 16f, 11, Palette.WipeLabel, TextAnchor.LowerLeft);

            var tag = MakePanel(chart, "持仓标签");
            Place(tag, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(154f, -13f), 108f, 24f);
            var tagText = MakeText(tag, "文本", "持仓 0 张", 12, TextAnchor.MiddleLeft, 9f, 0f, 6f, 0f);
            tagText.fontStyle = FontStyle.Normal;
            tagText.color = Palette.Muted;

            var legend = new GameObject("图例", typeof(RectTransform));
            legend.transform.SetParent(chart, false);
            var legendRt = (RectTransform)legend.transform;
            Place(legendRt, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-12f, -10f), 250f, 20f);
            var loss = MakeFixedText(legendRt, "丢失线", "—  买入价 ×0.7 丢失线", 11, FontStyle.Normal,
                                     new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, 184f, 18f, TextAnchor.MiddleLeft);
            loss.color = Palette.Red;
            var bonus = MakeFixedText(legendRt, "丰收线", "—  200", 11, FontStyle.Normal,
                                      new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, 58f, 18f, TextAnchor.MiddleRight);
            bonus.color = Palette.Green;

            var curve = new GameObject("曲线", typeof(RectTransform), typeof(CanvasRenderer), typeof(ChartGraphic));
            curve.transform.SetParent(chart, false);
            var curveRt = (RectTransform)curve.transform;
            Stretch(curveRt);
            curveRt.pivot = Vector2.zero;
            curve.GetComponent<ChartGraphic>().raycastTarget = false;

            var preview = new GameObject("编辑模式预览曲线", typeof(RectTransform), typeof(CanvasRenderer), typeof(SceneCurvePreview));
            preview.transform.SetParent(chart, false);
            var previewRt = (RectTransform)preview.transform;
            Stretch(previewRt, 18f, 50f, 18f, 18f);

            MakeChartImage(chart, "光晕", Palette.HaloNormal, 18f, 18f, false);
            MakeChartImage(chart, "点", Palette.DotNormal, 8f, 8f, false);

            var priceTag = MakePanel(chart, "价格标签");
            Place(priceTag, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, 54f, 20f);
            priceTag.GetComponent<Image>().color = Palette.PriceTagOk;
            var priceText = MakeText(priceTag, "文本", "100.0", 12, TextAnchor.MiddleCenter, 4f, 0f, 4f, 0f);
            priceText.color = Color.white;
            priceTag.gameObject.SetActive(false);

            var paused = MakeChartImage(chart, "暂停遮罩", Palette.Hex("#173A57", 0.46f), 1f, 1f, false);
            Stretch(paused.rectTransform);
            var pausedText = MakeText(paused.rectTransform, "文本", "暂 停 中", 20, TextAnchor.MiddleCenter, 0f, 0f, 0f, 0f);
            pausedText.color = Color.white;
        }

        private static void MakeChartLabelGroup(RectTransform labels, string name, int count,
                                                float w, float h, int size, Color color, TextAnchor alignment)
        {
            var group = new GameObject(name, typeof(RectTransform));
            group.transform.SetParent(labels, false);
            var groupRt = (RectTransform)group.transform;
            Stretch(groupRt);
            for (int i = 0; i < count; i++)
            {
                var text = MakeFixedText(groupRt, name + i, i.ToString(), size, FontStyle.Normal,
                                         new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, w, h, alignment);
                text.color = color;
                text.gameObject.SetActive(false);
            }
        }

        private static Image MakeChartImage(RectTransform parent, string name, Color color, float w, float h, bool active)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            Place(rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, w, h);
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            go.SetActive(active);
            return image;
        }

        private static Button MakeSceneButton(RectTransform parent, string name, string label, Color color, Color textColor,
                                              Vector2 anchor, Vector2 pivot, Vector2 position,
                                              float w, float h, int fontSize)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Outline), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            Place(rt, anchor, pivot, position, w, h);
            var image = go.GetComponent<Image>();
            image.color = color;
            var outline = go.GetComponent<Outline>();
            outline.effectColor = Palette.Line;
            outline.effectDistance = new Vector2(2f, -2f);
            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            var text = MakeText(rt, "文本", label, fontSize, TextAnchor.MiddleCenter, 6f, 0f, 6f, 0f);
            text.color = textColor;
            return button;
        }

        private static Text MakeFixedText(RectTransform parent, string name, string value, int size, FontStyle style,
                                          Vector2 anchor, Vector2 pivot, Vector2 position,
                                          float w, float h, TextAnchor alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            Place(rt, anchor, pivot, position, w, h);
            var text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.fontStyle = style;
            text.color = Palette.Text;
            text.alignment = alignment;
            text.text = value;
            return text;
        }

        private static void Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 position, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = position;
            rt.sizeDelta = new Vector2(w, h);
        }

        private static void Stretch(RectTransform rt, float left = 0f, float top = 0f,
                                    float right = 0f, float bottom = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }
    }
}
