using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Fishing
{
    /// <summary>
    /// 运行时自举：不需要任何手工搭建的场景对象。
    /// 打开工程直接按 Play 即可 —— Bootstrap 会自己建相机、EventSystem、Canvas 和游戏控制器。
    /// </summary>
    public static class Bootstrap
    {
        public static GameController Controller;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Launch()
        {
            // Player Main owns an entirely serialized UI; never rebuild the legacy interface over it.
            if (Object.FindObjectOfType<Fishing.Player.PlayerMarketController>() != null) return;
            // Independent raft scenes own their UI and must not launch the fishing overlay.
            if (Object.FindObjectOfType<DivePointController>() != null) return;
            // 搜打撤场景有自己的宿主与控制器，同样不要叠加捕捞界面。
            if (Object.FindObjectOfType<WhaleFall.DiveGameHost>() != null) return;
            if (Object.FindObjectOfType<WhaleFall.UnderwaterExploreController>() != null) return;
            if (Object.FindObjectOfType<WhaleFall.OriginalOceanDiver>() != null) return;
            if (Object.FindObjectOfType<WhaleFall.Underwater3DDiver>() != null) return;
            // 二次进入 Play（关闭 Domain Reload）时静态字段会残留，
            // 所以这里既查静态引用，也查场景里是否已经有控制器。
            var existing = Object.FindObjectOfType<GameController>();
            if (existing != null)
            {
                Controller = existing;
                return;
            }

            // 第一阶段场景化后由 SceneGameHost 使用场景里的 Canvas 初始化控制器。
            // 即使 RuntimeInitialize 的执行时机早于宿主 Awake，也不要再搭一套重复界面。
            if (Object.FindObjectOfType<SceneGameHost>() != null) return;
            Controller = null;

            MakeCamera();

            var canvas = MakeCanvas();
            MakeEventSystem();

            var go = new GameObject("GameController");
            var controller = go.AddComponent<GameController>();

            // Awake 在 AddComponent 时已经跑过，这里显式初始化视图树
            controller.BuildUI(canvas);
            Controller = controller;
        }

        private static void MakeCamera()
        {
            if (Camera.main != null) return;

            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Palette.Bg;
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            cam.transform.position = new Vector3(0f, 0f, -10f);

            // 游戏里不用音频。AudioListener 需要 UnityEngine.AudioModule，
            // 少了这个模块会直接编译不过 —— 已有的监听器就不再加，免得重复。
            if (Object.FindObjectOfType<AudioListener>() == null)
            {
                go.AddComponent<AudioListener>();
            }
        }

        private static Canvas MakeCanvas()
        {
            var go = new GameObject("UICanvas", typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = false;

            // 按 1220×700 的参考分辨率整体缩放，保证在 1080p / 2K 上字号和间距观感一致
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1220f, 700f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        private static void MakeEventSystem()
        {
            if (Object.FindObjectOfType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<StandaloneInputModule>();
        }
    }

    /// <summary>小工具：一次性收集子对象，给 LayoutBuilder 复用。</summary>
    public static class GoUtil
    {
        public static List<T> ChildrenOf<T>(Transform parent) where T : Component
        {
            var list = new List<T>();
            for (int i = 0; i < parent.childCount; i++)
            {
                var c = parent.GetChild(i).GetComponent<T>();
                if (c != null) list.Add(c);
            }
            return list;
        }
    }
}
