using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Fishing;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
public static class Raft3DPlayerSetup
{
    private const string RequestPath = "Assets/Editor/Raft3DPlayerSetup.request";
    private const string ScenePath = "Assets/Scenes/Current/DivePointRealisticOceanSky_Unified.unity";
    private const string ModelPath = "Assets/TripoModels/cartoon_character_3d_model/cartoon_character_3d_model.fbx";
    private const string FallbackModelPath = "Assets/TripoModels/stylized_boy_3d_model/stylized_boy_3d_model.fbx";
    private const string ControllerPath = "Assets/Art/Characters/CartoonRaftPlayer/CartoonRaftPlayer.controller";
    private const int RaftLayer = 30;

    static Raft3DPlayerSetup()
    {
        if (File.Exists(RequestPath)) EditorApplication.delayCall += RunWhenReady;
    }

    [MenuItem("鲸落/木筏/安装3D玩家与基础动作")]
    public static void InstallFromMenu() { RunWhenReady(); }

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
            File.WriteAllText("Raft3DPlayerSetup.failed.txt", ex.ToString());
            Debug.LogException(ex);
        }
    }

    private static void Install()
    {
        if (!File.Exists(ScenePath)) throw new FileNotFoundException("找不到统一海洋场景", ScenePath);

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded || scene.path != ScenePath)
        {
            if (scene.IsValid() && scene.isDirty && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                throw new OperationCanceledException("用户取消保存当前场景，3D 玩家安装已停止。");
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        GameObject raftRootObject = Find(scene, "RaftRoot");
        DivePointController gameplay = All<DivePointController>(scene).FirstOrDefault();
        RaftDeckRider deckRider = All<RaftDeckRider>(scene).FirstOrDefault();
        if (!raftRootObject || !gameplay || !deckRider)
            throw new InvalidOperationException("场景缺少 RaftRoot、DivePointController 或 RaftDeckRider。");

        // 统一场景不再使用旧的 RenderTexture 木筏摄像机，因此保存明确的标定值。
        // 104.6 来自场景内同一木箱的 UI 与 3D 坐标配对测量。
        if (deckRider.PixelsPerUnit <= 0f)
        {
            Undo.RecordObject(deckRider, "设置统一场景甲板坐标标定");
            deckRider.PixelsPerUnit = 104.6f;
            EditorUtility.SetDirty(deckRider);
        }

        string archiveDir = "Assets/Scenes/Archive/20261004/3DPlayer";
        Directory.CreateDirectory(archiveDir);
        string backup = AssetDatabase.GenerateUniqueAssetPath(archiveDir + "/DivePointRealisticOceanSky_Unified_pre3DPlayer.unity");
        if (!EditorSceneManager.SaveScene(scene, backup, true))
            throw new IOException("保存 3D 玩家安装前副本失败。");

        GameObject existing = Find(scene, "3D玩家_可编辑");
        if (existing) Undo.DestroyObjectImmediate(existing);

        GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        string usedModelPath = ModelPath;
        if (!modelAsset)
        {
            modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(FallbackModelPath);
            usedModelPath = FallbackModelPath;
        }
        if (!modelAsset) throw new FileNotFoundException("找不到已导入的 Tripo 人物模型。");

        AnimatorController controller = BuildController();

        GameObject playerRoot = new GameObject("3D玩家_可编辑");
        Undo.RegisterCreatedObjectUndo(playerRoot, "创建木筏3D玩家");
        SceneManager.MoveGameObjectToScene(playerRoot, scene);
        playerRoot.transform.SetParent(raftRootObject.transform, false);
        playerRoot.transform.localPosition = new Vector3(0f, deckRider.DeckSurfaceLocalY, deckRider.DeckZForRider);
        playerRoot.layer = RaftLayer;

        GameObject visual = PrefabUtility.InstantiatePrefab(modelAsset, scene) as GameObject;
        if (!visual) visual = UnityEngine.Object.Instantiate(modelAsset);
        visual.name = "人物模型_cartoon_character";
        visual.transform.SetParent(playerRoot.transform, false);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
        SetLayerRecursive(visual, RaftLayer);

        FitHeightAndFeet(playerRoot.transform, visual.transform, 2.05f);

        Animator animator = visual.GetComponentInChildren<Animator>(true);
        if (!animator) animator = visual.AddComponent<Animator>();
        Avatar modelAvatar = AssetDatabase.LoadAllAssetsAtPath(usedModelPath).OfType<Avatar>().FirstOrDefault();
        if (!modelAvatar || !modelAvatar.isValid || !modelAvatar.isHuman)
            throw new InvalidOperationException(
                "人物模型没有生成有效的 Humanoid Avatar。请在模型 Rig 页确认 Animation Type=Humanoid 并且骨骼映射为绿色。");
        animator.avatar = modelAvatar;
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

        var bridge = playerRoot.AddComponent<Raft3DPlayerBridge>();
        bridge.DeckRider = deckRider;
        bridge.Visual = visual.transform;
        bridge.Animator = animator;
        bridge.ChestPanel = gameplay.ChestPanel;

        // 2D 人物继续作为输入/交互坐标标记，但不再绘制。
        int hiddenGraphics = 0;
        if (gameplay.Player)
        {
            foreach (Graphic graphic in gameplay.Player.GetComponentsInChildren<Graphic>(true))
            {
                Undo.RecordObject(graphic, "隐藏旧2D玩家图像");
                graphic.enabled = false;
                EditorUtility.SetDirty(graphic);
                hiddenGraphics++;
            }
        }

        EditorUtility.SetDirty(playerRoot);
        EditorUtility.SetDirty(visual);
        EditorUtility.SetDirty(animator);
        EditorUtility.SetDirty(bridge);
        if (!EditorSceneManager.SaveScene(scene, ScenePath, false))
            throw new IOException("3D 玩家已创建，但场景保存失败。");
        AssetDatabase.SaveAssets();

        string report =
            "PASS=true\n" +
            "Scene=" + ScenePath + "\n" +
            "Backup=" + backup + "\n" +
            "Model=" + usedModelPath + "\n" +
            "Controller=" + ControllerPath + "\n" +
            "AnimatorAvatarValid=" + (animator.avatar && animator.avatar.isValid) + "\n" +
            "AnimatorAvatarHuman=" + (animator.avatar && animator.avatar.isHuman) + "\n" +
            "Hidden2DGraphics=" + hiddenGraphics + "\n";
        File.WriteAllText("Raft3DPlayerSetup.success.txt", report);
        Debug.Log("[鲸落] 木筏 3D 玩家安装完成。\n" + report, playerRoot);
        Selection.activeGameObject = playerRoot;
        EditorGUIUtility.PingObject(playerRoot);
    }

    private static AnimatorController BuildController()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ControllerPath));
        AnimatorController old = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (old) AssetDatabase.DeleteAsset(ControllerPath);

        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("Open", AnimatorControllerParameterType.Trigger);

        AnimationClip idle = LoadClip("Assets/ThirdParty/BasicMotions/Animations/Male/Idles/HumanM@Idle01.fbx");
        AnimationClip walk = LoadClip("Assets/ThirdParty/BasicMotions/Animations/Male/Movement/Walk/HumanM@Walk01_Forward.fbx");
        AnimationClip open = LoadClip("Assets/ThirdParty/BasicMotions/Animations/Male/Misc/Open/HumanM@Opening01 - Begin.fbx");
        if (!idle || !walk || !open) throw new FileNotFoundException("Basic Motions 的待机、行走或开启动作没有正确导入。");

        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        AnimatorState idleState = machine.AddState("Idle", new Vector3(250, 70));
        AnimatorState walkState = machine.AddState("Walk", new Vector3(500, 70));
        AnimatorState openState = machine.AddState("OpenChest", new Vector3(380, 230));
        idleState.motion = idle;
        walkState.motion = walk;
        openState.motion = open;
        machine.defaultState = idleState;

        AnimatorStateTransition toWalk = idleState.AddTransition(walkState);
        toWalk.hasExitTime = false;
        toWalk.duration = 0.12f;
        toWalk.AddCondition(AnimatorConditionMode.Greater, 0.08f, "Speed");
        AnimatorStateTransition toIdle = walkState.AddTransition(idleState);
        toIdle.hasExitTime = false;
        toIdle.duration = 0.16f;
        toIdle.AddCondition(AnimatorConditionMode.Less, 0.08f, "Speed");
        AnimatorStateTransition toOpen = machine.AddAnyStateTransition(openState);
        toOpen.hasExitTime = false;
        toOpen.duration = 0.08f;
        toOpen.AddCondition(AnimatorConditionMode.If, 0f, "Open");
        AnimatorStateTransition openDone = openState.AddTransition(idleState);
        openDone.hasExitTime = true;
        openDone.exitTime = 0.96f;
        openDone.duration = 0.12f;

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        return controller;
    }

    private static AnimationClip LoadClip(string path)
    {
        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
            .FirstOrDefault(clip => !clip.name.StartsWith("__preview", StringComparison.OrdinalIgnoreCase));
    }

    private static void FitHeightAndFeet(Transform playerRoot, Transform visual, float targetHeight)
    {
        Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return;
        Bounds localBounds = LocalBounds(playerRoot, renderers);
        if (localBounds.size.y > 0.001f)
            visual.localScale *= targetHeight / localBounds.size.y;
        localBounds = LocalBounds(playerRoot, renderers);
        visual.localPosition += Vector3.up * -localBounds.min.y;
    }

    private static Bounds LocalBounds(Transform root, Renderer[] renderers)
    {
        bool started = false;
        Bounds result = default(Bounds);
        foreach (Renderer renderer in renderers)
        {
            Bounds b = renderer.bounds;
            Vector3 min = b.min, max = b.max;
            for (int x = 0; x < 2; x++)
            for (int y = 0; y < 2; y++)
            for (int z = 0; z < 2; z++)
            {
                Vector3 world = new Vector3(x == 0 ? min.x : max.x, y == 0 ? min.y : max.y, z == 0 ? min.z : max.z);
                Vector3 local = root.InverseTransformPoint(world);
                if (!started) { result = new Bounds(local, Vector3.zero); started = true; }
                else result.Encapsulate(local);
            }
        }
        return result;
    }

    private static void SetLayerRecursive(GameObject root, int layer)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
    }

    private static GameObject Find(Scene scene, string name)
    {
        return All<GameObject>(scene).FirstOrDefault(go => go.name == name);
    }

    private static IEnumerable<T> All<T>(Scene scene) where T : UnityEngine.Object
    {
        return Resources.FindObjectsOfTypeAll<T>().Where(o =>
        {
            GameObject go = o as GameObject;
            if (!go && o is Component component) go = component.gameObject;
            return go && go.scene == scene && !EditorUtility.IsPersistent(o);
        });
    }
}
