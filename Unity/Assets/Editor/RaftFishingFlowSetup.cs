using System;
using System.IO;
using System.Linq;
using Fishing;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FishingEditor
{
    [InitializeOnLoad]
    public static class RaftFishingFlowSetup
    {
        private const string ScenePath = "Assets/Scenes/Current/DivePointRealisticOceanSky_Unified.unity";
        private const string RequestPath = "Assets/Editor/RaftFishingFlowSetup.request";
        private const string ReportPath = "RaftFishingFlowSetup.success.txt";
        private const string ControllerPath = "Assets/Art/Characters/CartoonRaftPlayer/CartoonRaftPlayer.controller";

        static RaftFishingFlowSetup()
        {
            if (File.Exists(RequestPath)) EditorApplication.delayCall += RunWhenReady;
        }

        [MenuItem("鲸落/木筏/安装雨天早晨捕捞流程")]
        public static void InstallFromMenu() { RunWhenReady(); }

        public static void InstallFromCommandLine() { Install(); }

        private static void RunWhenReady()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.delayCall += RunWhenReady;
                return;
            }
            try
            {
                Install();
                if (File.Exists(RequestPath)) File.Delete(RequestPath);
                AssetDatabase.Refresh();
            }
            catch (Exception ex)
            {
                File.WriteAllText("RaftFishingFlowSetup.failed.txt", ex.ToString());
                Debug.LogException(ex);
            }
        }

        private static void Install()
        {
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            var dive = All<DivePointController>(scene).FirstOrDefault();
            var weather = All<RaftWeatherController>(scene).FirstOrDefault();
            var bridge = All<Raft3DPlayerBridge>(scene).FirstOrDefault();
            var oceanCameraObject = All<GameObject>(scene).FirstOrDefault(x => x.name == "海洋与天空摄像机");
            if (!dive || !weather || !bridge || !oceanCameraObject)
                throw new InvalidOperationException("统一木筏场景缺少 DivePointController、天气、3D 玩家或海洋摄像机。没有修改场景。");

            var host = All<RaftFishingFlow>(scene).FirstOrDefault();
            if (!host)
            {
                var go = new GameObject("雨天早晨捕捞流程");
                SceneManager.MoveGameObjectToScene(go, scene);
                host = go.AddComponent<RaftFishingFlow>();
            }
            host.DivePoint = dive;
            host.Weather = weather;
            host.PlayerBridge = bridge;
            host.OceanCamera = oceanCameraObject.GetComponent<Camera>();
            host.InteractionCanvas = dive.Player.GetComponentInParent<UnityEngine.Canvas>();
            InstallZone(host, bridge, scene);
            host.FollowPlayerRightLimit = true;
            host.RightLimitInset = 35f;
            host.RightEdgeTriggerX = Mathf.Min(dive.RightLimit - 25f, 500f);
            host.CameraMoveRight = 8f;
            host.CameraMoveSeconds = 1.35f;
            EditorUtility.SetDirty(host);

            EnsureFishingAnimation(bridge);
            EditorUtility.SetDirty(bridge);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath, false))
                throw new IOException("捕捞流程已安装，但场景保存失败。");
            AssetDatabase.SaveAssets();

            bool rulePass = RaftFishingFlow.CanStartFishing(true, RaftTimeOfDay.Day)
                && !RaftFishingFlow.CanStartFishing(false, RaftTimeOfDay.Day)
                && !RaftFishingFlow.CanStartFishing(true, RaftTimeOfDay.Afternoon);
            if (!rulePass) throw new InvalidOperationException("雨天早晨触发规则自检失败。");

            if (!host.FishingZone || !host.FishingZone.isTrigger || !host.InteractionCanvas)
                throw new InvalidOperationException("触发区或弹窗 Canvas 验证失败。");
            File.WriteAllText(ReportPath,
                "PASS=true\nScene=" + ScenePath + "\nTrigger=BoxCollider feet overlap\nZone=" + host.FishingZone.transform.localPosition +
                "\nZoneSize=" + host.FishingZone.size + "\nCanvas=" + host.InteractionCanvas.name +
                "\nWeatherRule=Rainy&&Morning\nMainUI=TransparentOverlay\nCameraMoveRight=" + host.CameraMoveRight +
                "\nGameplay=Main scene additive\nCameraMoveVisualDirection=right\nFishingAnimation=LootLoop\n");
            Debug.Log("[鲸落] 雨天早晨捕捞流程安装完成。", host);
            Selection.activeGameObject = host.gameObject;
        }

        private static void InstallZone(RaftFishingFlow flow, Raft3DPlayerBridge bridge, Scene scene)
        {
            var raft = bridge.DeckRider.Buoyancy.RaftRoot;
            if (!raft) throw new InvalidOperationException("找不到木筏根节点。");
            if (!flow.FishingZone)
            {
                var hull = raft.GetComponentsInChildren<Transform>(true)
                    .FirstOrDefault(t => t.name.IndexOf("wooden_xylophone", StringComparison.OrdinalIgnoreCase) >= 0);
                if (!hull) throw new InvalidOperationException("找不到木筏甲板模型，停止创建触发器。");
                bool found = false;
                Bounds bounds = default;
                foreach (var filter in hull.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (!filter.sharedMesh) continue;
                    var b = filter.sharedMesh.bounds;
                    for (int x = 0; x < 2; x++)
                    for (int y = 0; y < 2; y++)
                    for (int z = 0; z < 2; z++)
                    {
                        var p = raft.InverseTransformPoint(filter.transform.TransformPoint(new Vector3(
                            x == 0 ? b.min.x : b.max.x, y == 0 ? b.min.y : b.max.y, z == 0 ? b.min.z : b.max.z)));
                        if (!found) { bounds = new Bounds(p, Vector3.zero); found = true; }
                        else bounds.Encapsulate(p);
                    }
                }
                if (!found) throw new InvalidOperationException("甲板模型没有网格。");
                var go = new GameObject("捕捞触发区_木筏右端_隐形");
                SceneManager.MoveGameObjectToScene(go, scene);
                go.transform.SetParent(raft, false);
                // DeckUiSign maps movement toward screen-right onto the physical deck axis.
                float side = Mathf.Sign(bridge.DeckRider.DeckUiSign);
                float edge = side >= 0 ? bounds.max.x : bounds.min.x;
                go.transform.localPosition = new Vector3(edge - side * 0.65f,
                    bridge.DeckRider.DeckSurfaceLocalY + 0.5f, bridge.DeckRider.DeckZForRider);
                flow.FishingZone = go.AddComponent<BoxCollider>();
                flow.FishingZone.isTrigger = true;
                flow.FishingZone.size = new Vector3(2.4f, 3f, Mathf.Max(3f, bounds.size.z));
            }
            EditorUtility.SetDirty(flow.FishingZone);
            if (!flow.InteractionCanvas) throw new InvalidOperationException("捕捞弹窗缺少 Canvas。");
        }

        private static void EnsureFishingAnimation(Raft3DPlayerBridge bridge)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (!controller) throw new FileNotFoundException("找不到木筏玩家动画控制器", ControllerPath);
            if (!controller.parameters.Any(p => p.name == "Fishing"))
                controller.AddParameter("Fishing", AnimatorControllerParameterType.Bool);

            var machine = controller.layers[0].stateMachine;
            var idle = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == "Idle");
            if (!idle) throw new InvalidOperationException("玩家动画控制器缺少 Idle 状态。");
            var fishing = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == "Fishing");
            if (!fishing)
            {
                fishing = machine.AddState("Fishing", new Vector3(620f, 230f));
                fishing.motion = AssetDatabase.LoadAllAssetsAtPath(
                    "Assets/ThirdParty/BasicMotions/Animations/Male/Misc/Loot/HumanM@Loot01 - Loop.fbx")
                    .OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview", StringComparison.OrdinalIgnoreCase));
                if (!fishing.motion) throw new FileNotFoundException("找不到可用的循环捕捞动作。");

                var enter = machine.AddAnyStateTransition(fishing);
                enter.hasExitTime = false;
                enter.duration = 0.16f;
                enter.canTransitionToSelf = false;
                enter.AddCondition(AnimatorConditionMode.If, 0f, "Fishing");
                var leave = fishing.AddTransition(idle);
                leave.hasExitTime = false;
                leave.duration = 0.18f;
                leave.AddCondition(AnimatorConditionMode.IfNot, 0f, "Fishing");
            }
            EditorUtility.SetDirty(controller);
            bridge.FishingParameter = "Fishing";
        }

        private static T[] All<T>(Scene scene) where T : UnityEngine.Object
        {
            return Resources.FindObjectsOfTypeAll<T>().Where(o =>
            {
                GameObject go = o as GameObject;
                if (!go && o is Component component) go = component.gameObject;
                return go && go.scene == scene && !EditorUtility.IsPersistent(o);
            }).ToArray();
        }
    }
}
