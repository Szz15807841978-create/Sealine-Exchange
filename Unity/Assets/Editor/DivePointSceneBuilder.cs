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
    [InitializeOnLoad]
    internal static class DivePointAutoBuild
    {
        private const string RequestPath = "Assets/Editor/DivePointSceneBuild.request";

        static DivePointAutoBuild()
        {
            EditorApplication.delayCall += RunOnce;
        }

        private static void RunOnce()
        {
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(RequestPath) == null) return;
            try
            {
                DivePointSceneBuilder.BuildFromMenu();
                AssetDatabase.DeleteAsset(RequestPath);
                AssetDatabase.Refresh();
            }
            catch (System.Exception ex)
            {
                Debug.LogException(ex);
            }
        }
    }

    public static class DivePointSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/DivePointDemo.unity";
        private const string DiverPath = "Assets/Resources/DivePoint/Diver_Player.png";
        private const string ChestPath = "Assets/Resources/DivePoint/ITM_DEV_CHEST_01.png";

        [MenuItem("Fishing/下水点/生成木筏交互 Demo")]
        public static void BuildFromMenu()
        {
            ConfigureTexture(DiverPath);
            ConfigureTexture(ChestPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Scene previous = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            BuildScene();
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings();
            EditorSceneManager.CloseScene(scene, true);
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            Debug.Log("[鲸落] 下水点 Demo 已生成：" + ScenePath);
        }

        private static void BuildScene()
        {
            var cameraGo = new GameObject("Main Camera", typeof(Camera));
            cameraGo.tag = "MainCamera";
            cameraGo.transform.position = new Vector3(0f, 0f, -10f);
            cameraGo.GetComponent<Camera>().clearFlags = CameraClearFlags.SolidColor;
            cameraGo.GetComponent<Camera>().backgroundColor = Hex("#78CFF0");

            var eventGo = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            var canvasGo = new GameObject("下水点画布", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            var canvasRt = (RectTransform)canvasGo.transform;

            var sky = Panel(canvasRt, "天空", Hex("#69C8EE"));
            Stretch(sky);
            sky.GetComponent<Image>().raycastTarget = false;
            var glow = Panel(sky, "左上阳光", new Color(1f, 0.96f, 0.75f, 0.2f));
            Place(glow, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(-80f, 60f), 720f, 300f);
            glow.localRotation = Quaternion.Euler(0f, 0f, -18f);

            var ocean = Panel(canvasRt, "海面", Hex("#126EAA"));
            Place(ocean, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, 1800f, 310f);
            ocean.GetComponent<Image>().raycastTarget = false;
            MakeWave(ocean, "远浪", 214f, 1420f, Hex("#A9E9F1"));
            MakeWave(ocean, "近浪", 92f, 1720f, Hex("#E8FAF4"));

            var title = Label(canvasRt, "标题", "鲸落 · 下水点", 27, FontStyle.Bold, TextAnchor.MiddleLeft, Hex("#0B2D4F"));
            Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -22f), 360f, 42f);
            var help = Label(canvasRt, "操作说明", "A / D 或方向键移动    E 打开木箱    B 查看背包", 16, FontStyle.Bold, TextAnchor.MiddleRight, Hex("#153D5D"));
            Place(help.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-28f, -25f), 620f, 38f);

            var raft = Rect(canvasRt, "木筏实体");
            Place(raft, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 138f), 1260f, 270f);
            BuildRaft(raft);

            Texture2D chestTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(ChestPath);
            Texture2D diverTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(DiverPath);

            var chest = Raw(raft, "木箱实体_ITM_DEV_CHEST_01", chestTexture);
            Place(chest.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-230f, 82f), 150f, 150f);
            chest.raycastTarget = true;
            var chestButton = chest.gameObject.AddComponent<Button>();
            chestButton.targetGraphic = chest;
            chestButton.transition = Selectable.Transition.ColorTint;

            var player = Raw(raft, "主控_潜水员", diverTexture);
            Place(player.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(330f, 62f), 190f, 208f);
            player.raycastTarget = false;

            var prompt = Label(raft, "木箱交互提示", "E  打开木箱", 17, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
            Place(prompt.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-230f, 238f), 180f, 36f);
            var promptBg = prompt.gameObject.AddComponent<Outline>();
            promptBg.effectColor = Hex("#0B2D4F");
            promptBg.effectDistance = new Vector2(2f, -2f);
            prompt.gameObject.SetActive(false);

            var status = Label(canvasRt, "状态提示", "走近木箱，按 E 查看物资", 16, FontStyle.Bold, TextAnchor.MiddleCenter, Hex("#FFF6D7"));
            Place(status.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 20f), 660f, 38f);
            var statusOutline = status.gameObject.AddComponent<Outline>();
            statusOutline.effectColor = Hex("#0B2D4F");
            statusOutline.effectDistance = new Vector2(2f, -2f);

            GameObject chestPanel;
            Transform chestRows;
            Transform chestBagSlots;
            Button chestClose;
            Button takeAll;
            BuildChestPanel(canvasRt, out chestPanel, out chestRows, out chestBagSlots, out chestClose, out takeAll);

            GameObject backpackPanel;
            Transform backpackSlots;
            Button backpackClose;
            BuildBackpackPanel(canvasRt, out backpackPanel, out backpackSlots, out backpackClose);

            var controllerGo = new GameObject("下水点控制器", typeof(DivePointController));
            var controller = controllerGo.GetComponent<DivePointController>();
            controller.Player = player.rectTransform;
            controller.Chest = chest.rectTransform;
            controller.ChestImage = chest;
            controller.ChestButton = chestButton;
            controller.Prompt = prompt;
            controller.Status = status;
            controller.ChestPanel = chestPanel;
            controller.ChestRows = chestRows;
            controller.ChestBagSlots = chestBagSlots;
            controller.ChestCloseButton = chestClose;
            controller.TakeAllButton = takeAll;
            controller.BackpackPanel = backpackPanel;
            controller.BackpackSlots = backpackSlots;
            controller.BackpackCloseButton = backpackClose;
        }

        private static void BuildRaft(RectTransform raft)
        {
            var shadow = Panel(raft, "木筏水面阴影", new Color(0.02f, 0.19f, 0.33f, 0.38f));
            Place(shadow, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 18f), 1190f, 78f);

            for (int i = 0; i < 11; i++)
            {
                var log = Panel(raft, "浮木_" + (i + 1).ToString("00"), i % 2 == 0 ? Hex("#B96E35") : Hex("#A95F2D"));
                Place(log, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-550f + i * 110f, 42f), 104f, 72f);
                var line = log.gameObject.AddComponent<Outline>();
                line.effectColor = Hex("#173A57");
                line.effectDistance = new Vector2(3f, -3f);
            }

            var deck = Panel(raft, "生活平台", Hex("#C98343"));
            Place(deck, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 84f), 1180f, 94f);
            var deckLine = deck.gameObject.AddComponent<Outline>();
            deckLine.effectColor = Hex("#173A57");
            deckLine.effectDistance = new Vector2(4f, -4f);
            for (int i = 1; i < 10; i++)
            {
                var seam = Panel(deck, "木板缝_" + i, new Color(0.25f, 0.13f, 0.07f, 0.38f));
                Place(seam, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(i * 118f, 0f), 3f, 86f);
            }

            var flagPole = Panel(raft, "下水点标杆", Hex("#8A4D25"));
            Place(flagPole, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(500f, 164f), 16f, 220f);
            var flag = Panel(raft, "下水旗", Hex("#F4E5BC"));
            Place(flag, new Vector2(0.5f, 0f), new Vector2(0f, 0.5f), new Vector2(510f, 235f), 116f, 54f);
        }

        private static void BuildChestPanel(RectTransform canvas, out GameObject panelGo, out Transform rows,
                                            out Transform bagSlots, out Button close, out Button takeAll)
        {
            var panel = Panel(canvas, "木箱界面", new Color(0.04f, 0.15f, 0.24f, 0.97f));
            panelGo = panel.gameObject;
            Place(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, 940f, 540f);
            Border(panel, Hex("#DCA454"), 3f);
            var title = Label(panel, "标题", "木箱与随身背包", 25, FontStyle.Bold, TextAnchor.MiddleLeft, Hex("#FFF2CE"));
            Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -20f), 500f, 42f);
            close = MakeButton(panel, "关闭", "关闭  Esc", 14, Hex("#25445A"), Color.white,
                               new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -22f), 110f, 36f);

            var leftTitle = Label(panel, "木箱标题", "木箱内容", 18, FontStyle.Bold, TextAnchor.MiddleLeft, Hex("#E5B96B"));
            Place(leftTitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -82f), 300f, 30f);
            var chestRows = Rect(panel, "木箱物品列表");
            Place(chestRows, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -122f), 390f, 290f);
            var vertical = chestRows.gameObject.AddComponent<VerticalLayoutGroup>();
            vertical.spacing = 10f;
            vertical.childControlHeight = true;
            vertical.childControlWidth = true;
            vertical.childForceExpandHeight = false;
            rows = chestRows;
            for (int i = 0; i < 3; i++) BuildChestRow(chestRows, i);
            takeAll = MakeButton(panel, "全部拿取", "全部拿取", 15, Hex("#B77932"), Color.white,
                                 new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(30f, 28f), 150f, 42f);

            var rightTitle = Label(panel, "背包标题", "随身背包 · 12 格", 18, FontStyle.Bold, TextAnchor.MiddleLeft, Hex("#E5B96B"));
            Place(rightTitle.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-430f, -82f), 390f, 30f);
            var slots = Rect(panel, "背包格子");
            Place(slots, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-430f, -122f), 400f, 330f);
            BuildSlotGrid(slots, 12);
            bagSlots = slots;
            panelGo.SetActive(false);
        }

        private static void BuildBackpackPanel(RectTransform canvas, out GameObject panelGo, out Transform slots, out Button close)
        {
            var panel = Panel(canvas, "背包界面", new Color(0.04f, 0.15f, 0.24f, 0.97f));
            panelGo = panel.gameObject;
            Place(panel, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-30f, 0f), 500f, 500f);
            Border(panel, Hex("#DCA454"), 3f);
            var title = Label(panel, "标题", "随身背包 · 12 格", 23, FontStyle.Bold, TextAnchor.MiddleLeft, Hex("#FFF2CE"));
            Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -20f), 320f, 40f);
            close = MakeButton(panel, "关闭", "关闭", 14, Hex("#25445A"), Color.white,
                               new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-20f, -22f), 90f, 34f);
            var grid = Rect(panel, "背包格子");
            Place(grid, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -20f), 440f, 350f);
            BuildSlotGrid(grid, 12);
            slots = grid;
            panelGo.SetActive(false);
        }

        private static void BuildChestRow(RectTransform parent, int index)
        {
            var row = Panel(parent, "物品行_" + (index + 1), Hex("#16354B"));
            var layout = row.gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = 66f;
            layout.minHeight = 66f;
            var horizontal = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            horizontal.padding = new RectOffset(14, 10, 8, 8);
            horizontal.spacing = 8f;
            horizontal.childAlignment = TextAnchor.MiddleLeft;
            horizontal.childControlWidth = true;
            horizontal.childControlHeight = true;
            horizontal.childForceExpandWidth = false;
            var name = Label(row, "名称", "物品", 16, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white);
            Size(name.rectTransform, 190f, 46f, 1f);
            var count = Label(row, "数量", "×0", 15, FontStyle.Bold, TextAnchor.MiddleCenter, Hex("#E9C478"));
            Size(count.rectTransform, 62f, 46f);
            var button = MakeButton(row, "拿取", "拿取", 14, Hex("#B77932"), Color.white,
                                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, 82f, 42f);
            Size(button.GetComponent<RectTransform>(), 82f, 42f);
        }

        private static void BuildSlotGrid(RectTransform grid, int count)
        {
            var layout = grid.gameObject.AddComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(92f, 92f);
            layout.spacing = new Vector2(10f, 10f);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 4;
            layout.childAlignment = TextAnchor.UpperLeft;
            for (int i = 0; i < count; i++)
            {
                var slot = Panel(grid, "格子_" + (i + 1).ToString("00"), Hex("#16354B"));
                Border(slot, Hex("#42667C"), 1f);
                var text = Label(slot, "文字", "空", 13, FontStyle.Normal, TextAnchor.MiddleCenter, Hex("#9FB5C2"));
                Stretch(text.rectTransform, 5f, 5f, 5f, 5f);
            }
        }

        private static void MakeWave(RectTransform ocean, string name, float y, float width, Color color)
        {
            var wave = Panel(ocean, name, color);
            Place(wave, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, y), width, 8f);
            wave.localRotation = Quaternion.Euler(0f, 0f, -2f);
        }

        private static void ConfigureTexture(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
        }

        private static void AddToBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.All(x => x.path != ScenePath)) scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static RectTransform Rect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static RectTransform Panel(Transform parent, string name, Color color)
        {
            var rt = Rect(parent, name);
            var image = rt.gameObject.AddComponent<Image>();
            image.color = color;
            return rt;
        }

        private static RawImage Raw(Transform parent, string name, Texture texture)
        {
            var rt = Rect(parent, name);
            var image = rt.gameObject.AddComponent<RawImage>();
            image.texture = texture;
            image.color = Color.white;
            return image;
        }

        private static Text Label(Transform parent, string name, string value, int size, FontStyle style, TextAnchor alignment, Color color)
        {
            var rt = Rect(parent, name);
            var text = rt.gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = color;
            text.text = value;
            text.raycastTarget = false;
            return text;
        }

        private static Button MakeButton(Transform parent, string name, string label, int fontSize, Color color, Color textColor,
                                         Vector2 anchor, Vector2 pivot, Vector2 position, float width, float height)
        {
            var rt = Panel(parent, name, color);
            Place(rt, anchor, pivot, position, width, height);
            Border(rt, Hex("#173A57"), 1.5f);
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = rt.GetComponent<Image>();
            var text = Label(rt, "文字", label, fontSize, FontStyle.Bold, TextAnchor.MiddleCenter, textColor);
            Stretch(text.rectTransform, 6f, 3f, 6f, 3f);
            return button;
        }

        private static void Border(RectTransform rt, Color color, float width)
        {
            var outline = rt.gameObject.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(width, -width);
        }

        private static void Size(RectTransform rt, float width, float height, float flexibleWidth = -1f)
        {
            var layout = rt.gameObject.GetComponent<LayoutElement>() ?? rt.gameObject.AddComponent<LayoutElement>();
            layout.preferredWidth = width;
            layout.preferredHeight = height;
            if (flexibleWidth >= 0f) layout.flexibleWidth = flexibleWidth;
        }

        private static void Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 position, float width, float height)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = position;
            rt.sizeDelta = new Vector2(width, height);
        }

        private static void Stretch(RectTransform rt, float left = 0f, float top = 0f, float right = 0f, float bottom = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }

        private static Color Hex(string value)
        {
            Color color;
            return ColorUtility.TryParseHtmlString(value, out color) ? color : Color.white;
        }
    }
}
