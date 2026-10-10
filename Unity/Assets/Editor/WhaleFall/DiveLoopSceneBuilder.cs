using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace WhaleFallEditor
{
    /// <summary>通过 Assets/Editor/DiveLoop.request 标记文件自动生成场景（沿用工程既有惯例）。</summary>
    [InitializeOnLoad]
    internal static class DiveLoopAutoBuild
    {
        private const string RequestPath = "Assets/Editor/DiveLoop.request";
        static DiveLoopAutoBuild() { EditorApplication.delayCall += RunOnce; }
        private static void RunOnce()
        {
            if (!System.IO.File.Exists(RequestPath)) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            try
            {
                if (!System.IO.File.Exists(DiveLoopSceneBuilder.ScenePath))
                {
                    DiveLoopSceneBuilder.Build();
                    AssetDatabase.DeleteAsset(RequestPath);
                    AssetDatabase.Refresh();
                }
            }
            catch (System.Exception ex) { Debug.LogException(ex); }
        }
    }

    /// <summary>一键生成「搜打撤」场景：相机 + HUD Canvas + EventSystem + DiveGameHost。</summary>
    public static class DiveLoopSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/DiveLoop.unity";

        [MenuItem("鲸落/搜打撤/生成搜打撤场景")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new System.InvalidOperationException("请先退出 Play。");
            if (System.IO.File.Exists(ScenePath))
                throw new System.InvalidOperationException("搜打撤场景已存在，为保护手动调整不自动覆盖。");

            Scene previous = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                BuildScene();
                EditorSceneManager.SaveScene(scene, ScenePath);
                AddToBuildSettings();
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
            Debug.Log("[鲸落] 搜打撤场景已生成：" + ScenePath);
        }

        private static void BuildScene()
        {
            // 世界相机（正交，渲染灰盒立方体；HUD 用 Overlay Canvas 盖在上层）
            var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            camGo.tag = "MainCamera";
            var cam = camGo.GetComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 14f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Hex("#0B2233");
            camGo.transform.position = new Vector3(80f, -5f, -10f);
            camGo.transform.rotation = Quaternion.identity;

            // HUD Canvas
            var canvasGo = new GameObject("HUD画布", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            // 事件系统
            if (Object.FindObjectOfType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            }

            // 宿主
            var hostGo = new GameObject("DiveGameHost", typeof(WhaleFall.DiveGameHost));
            var host = hostGo.GetComponent<WhaleFall.DiveGameHost>();
            var ser = new SerializedObject(host);
            ser.FindProperty("targetCanvas").objectReferenceValue = canvas;
            ser.FindProperty("worldCamera").objectReferenceValue = cam;
            ser.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AddToBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.All(x => x.path != ScenePath)) scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static Color Hex(string value)
        {
            Color c;
            return ColorUtility.TryParseHtmlString(value, out c) ? c : Color.white;
        }
    }
}
