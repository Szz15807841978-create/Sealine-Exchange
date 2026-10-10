using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace WhaleFallEditor
{
    /// <summary>通过 Assets/Editor/DiveLoop2.request 自动生成新场景。</summary>
    [InitializeOnLoad]
    internal static class DiveLoop2Auto
    {
        private const string RequestPath = "Assets/Editor/DiveLoop2.request";
        static DiveLoop2Auto() { EditorApplication.delayCall += RunOnce; }
        private static void RunOnce()
        {
            if (!File.Exists(RequestPath)) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            try
            {
                DiveLoop2SceneBuilder.Build();
                AssetDatabase.DeleteAsset(RequestPath);
                AssetDatabase.Refresh();
            }
            catch (System.Exception e) { Debug.LogException(e); }
        }
    }

    /// <summary>
    /// 生成「搜打撤 · Kenney 版」新场景。
    /// 与 DiveLoop 的区别：**场景里不放任何地形对象** —— 运行时由
    /// DiveKenneyTerrain 按种子程序化生成，所以每局地形都不同。
    /// 相机 / HUD / EventSystem / DiveGameHost 与老场景一致，玩法层完全复用。
    /// </summary>
    public static class DiveLoop2SceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/DiveLoop2.unity";

        [MenuItem("鲸落/搜打撤/生成新场景（Kenney 程序化地形）")]
        public static void Build()
        {
            // 新建场景会替换当前场景，先确认没有未保存的改动（自动触发时同样生效）
            var cur = EditorSceneManager.GetActiveScene();
            if (cur.IsValid() && cur.isDirty)
            {
                Debug.LogWarning("[鲸落] 当前场景有未保存改动，先保存再执行「鲸落/搜打撤/生成新场景」。已跳过本次自动生成。");
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ---------- 相机 ----------
            var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            camGo.tag = "MainCamera";
            var cam = camGo.GetComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 14f;                  // 视高 28 m，对齐 HTML 版
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Hex("#0B2233");
            cam.nearClipPlane = -50f;
            cam.farClipPlane = 200f;
            camGo.transform.position = new Vector3(50f, -20f, -10f);

            // ---------- 灯光（地形/角色都是 3D，需要一盏方向光）----------
            var lightGo = new GameObject("水下主光", typeof(Light));
            lightGo.transform.rotation = Quaternion.Euler(38f, -28f, 0f);
            var light = lightGo.GetComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.65f, 0.84f, 1f);
            light.intensity = 0.9f;

            // ---------- HUD ----------
            var canvasGo = new GameObject("HUD画布", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);
            scaler.matchWidthOrHeight = 0.5f;

            if (Object.FindObjectOfType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            // ---------- 玩法宿主 ----------
            var hostGo = new GameObject("DiveGameHost");
            var host = hostGo.AddComponent<WhaleFall.DiveGameHost>();
            var so = new SerializedObject(host);
            var pc = so.FindProperty("targetCanvas");
            if (pc != null) pc.objectReferenceValue = canvas;
            var pw = so.FindProperty("worldCamera");
            if (pw != null) pw.objectReferenceValue = cam;
            so.ApplyModifiedPropertiesWithoutUndo();

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();

            Debug.Log("[鲸落] 新场景已生成：" + ScenePath
                + "（场景内无地形对象 —— 运行时由 Kenney 程序化生成）");
        }

        private static Color Hex(string v)
        {
            Color c;
            return ColorUtility.TryParseHtmlString(v, out c) ? c : Color.magenta;
        }
    }
}
