using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Crest;
using Fishing;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
public static class UnifiedOceanRaftBuilder
{
    private const string RequestPath = "Assets/Editor/UnifiedOceanRaft.request";
    private const string ReportPath = "UnifiedOceanRaft.success.txt";
    private const int WaterLayer = 4;
    private const int RaftLayer = 30;

    static UnifiedOceanRaftBuilder()
    {
        if (File.Exists(RequestPath))
            EditorApplication.delayCall += RunWhenReady;
    }

    [MenuItem("鲸落/木筏/生成海面与木筏统一渲染场景")]
    public static void BuildFromMenu()
    {
        RunWhenReady();
    }

    private static void RunWhenReady()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.delayCall += RunWhenReady;
            return;
        }

        try
        {
            Build();
            if (File.Exists(RequestPath)) File.Delete(RequestPath);
            AssetDatabase.Refresh();
        }
        catch (Exception ex)
        {
            File.WriteAllText("UnifiedOceanRaft.failed.txt", ex.ToString());
            Debug.LogException(ex);
        }
    }

    private static void Build()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
            throw new InvalidOperationException("当前没有已加载的场景。请先打开下水点场景。");

        GameObject raftWorld = Find(scene, "3D木筏与装置_全部可编辑");
        GameObject raftRootObject = Find(scene, "RaftRoot");
        GameObject oceanCameraObject = Find(scene, "海洋与天空摄像机");
        GameObject raftCameraObject = Find(scene, "3D木筏展示摄像机");
        GameObject raftViewportObject = Find(scene, "3D木筏实时画面");
        GameObject oceanViewportObject = Find(scene, "真实海洋与天空_渲染画面");

        if (!raftWorld || !raftRootObject || !oceanCameraObject || !raftCameraObject || !raftViewportObject || !oceanViewportObject)
            throw new InvalidOperationException("当前场景不是完整的下水点场景，缺少木筏、海洋摄像机或 RenderTexture 显示对象。没有修改场景。");

        Camera oceanCamera = oceanCameraObject.GetComponent<Camera>();
        Camera raftCamera = raftCameraObject.GetComponent<Camera>();
        RawImage raftViewport = raftViewportObject.GetComponent<RawImage>();
        RawImage oceanViewport = oceanViewportObject.GetComponent<RawImage>();
        OceanRenderer ocean = All<OceanRenderer>(scene).FirstOrDefault();
        RaftBuoyancy buoyancy = raftRootObject.GetComponent<RaftBuoyancy>();
        if (!oceanCamera || !raftCamera || !raftViewport || !oceanViewport || !ocean)
            throw new InvalidOperationException("统一渲染所需组件不完整。没有修改场景。");

        string sourcePath = scene.path;
        if (string.IsNullOrWhiteSpace(sourcePath) || !sourcePath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("请先把当前场景保存到 Assets 内，再生成统一渲染版本。");

        string sourceName = Path.GetFileNameWithoutExtension(sourcePath);
        string baseName = sourceName.EndsWith("_Unified", StringComparison.OrdinalIgnoreCase)
            ? sourceName.Substring(0, sourceName.Length - "_Unified".Length)
            : sourceName;
        string targetPath = "Assets/Scenes/Current/" + baseName + "_Unified.unity";
        string archiveDir = "Assets/Scenes/Archive/20261004/UnifiedRender";
        Directory.CreateDirectory(archiveDir);

        // 先把用户此刻的未保存调整落盘，再把完全相同的内存场景另存为新版本。
        if (!EditorSceneManager.SaveScene(scene, sourcePath, false))
            throw new IOException("保存当前场景失败，已停止，未进行后续修改。");

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(targetPath))
        {
            string oldTargetArchive = AssetDatabase.GenerateUniqueAssetPath(
                archiveDir + "/" + baseName + "_Unified_previous.unity");
            string moveError = AssetDatabase.MoveAsset(targetPath, oldTargetArchive);
            if (!string.IsNullOrEmpty(moveError))
                throw new IOException("归档上一次统一场景失败：" + moveError);
        }

        if (!EditorSceneManager.SaveScene(scene, targetPath, false))
            throw new IOException("另存统一渲染场景失败。");

        // 新版本已经成为当前场景；旧源文件移入 Archive，便于在 Project 中区分。
        if (!sourcePath.Equals(targetPath, StringComparison.OrdinalIgnoreCase))
        {
            string archiveSource = AssetDatabase.GenerateUniqueAssetPath(
                archiveDir + "/" + sourceName + "_preUnified.unity");
            string moveError = AssetDatabase.MoveAsset(sourcePath, archiveSource);
            if (!string.IsNullOrEmpty(moveError))
                Debug.LogWarning("[鲸落] 旧场景未能自动移入 Archive，但文件仍保留在原处：" + moveError);
        }

        Undo.SetCurrentGroupName("统一海面与木筏渲染");
        int undoGroup = Undo.GetCurrentGroup();
        Undo.RecordObject(raftWorld.transform, "把木筏移入海洋世界");
        Undo.RecordObject(oceanCamera, "海洋相机同时渲染木筏");
        Undo.RecordObject(raftCameraObject, "停用旧木筏相机");
        Undo.RecordObject(raftViewportObject, "停用旧木筏叠图");

        Bounds hullBoundsBefore = FindHullBounds(raftRootObject);
        float seaLevel = ocean.Root ? ocean.Root.position.y : ocean.transform.position.y;

        Vector3 worldPosition = raftWorld.transform.position;
        worldPosition.x = oceanCamera.transform.position.x;
        raftWorld.transform.position = worldPosition;

        Bounds movedHullBounds = FindHullBounds(raftRootObject);
        const float submergedFraction = 0.38f;
        float desiredWaterline = movedHullBounds.min.y + movedHullBounds.size.y * submergedFraction;
        worldPosition = raftWorld.transform.position;
        worldPosition.y += seaLevel - desiredWaterline;
        raftWorld.transform.position = worldPosition;
        Bounds finalHullBounds = FindHullBounds(raftRootObject);

        // 同一台相机、同一个深度缓冲：海面能按真实前后关系遮挡船体。
        oceanCamera.cullingMask |= (1 << WaterLayer) | (1 << RaftLayer);
        oceanCamera.clearFlags = CameraClearFlags.Skybox;
        oceanCamera.allowHDR = true;
        oceanCamera.allowMSAA = true;
        oceanCamera.nearClipPlane = Mathf.Min(oceanCamera.nearClipPlane, 0.1f);
        oceanCamera.farClipPlane = Mathf.Max(oceanCamera.farClipPlane, 5000f);

        raftCameraObject.SetActive(false);
        raftViewportObject.SetActive(false);
        oceanViewportObject.SetActive(true);
        // 旧木筏 RT 在 UI 上一直采用水平翻转。统一到海洋 RT 后也必须保留这层映射，
        // 否则 3D 木箱会和 2D 触发标记分居左右两侧。
        oceanViewport.uvRect = new Rect(1f, 0f, -1f, 1f);
        EditorUtility.SetDirty(oceanViewport);

        // 旧方案有一张 UI 水线和一条 Layer 30 水线网格。统一深度后它们会重复遮挡，
        // 也是“僵硬水线”的直接来源，因此仅在新场景中停用。
        foreach (GameObject waterline in All<GameObject>(scene).Where(go => go.name == "木筏水线"))
        {
            Undo.RecordObject(waterline, "停用旧木筏水线");
            waterline.SetActive(false);
            EditorUtility.SetDirty(waterline);
        }

        // 保留受控浮力的稳定性，只减轻“迟钝感”。真正的吃水遮挡已由共享深度解决。
        if (buoyancy)
        {
            Undo.RecordObject(buoyancy, "微调统一场景浮力响应");
            buoyancy.MinSpatialLength = Mathf.Clamp(finalHullBounds.size.x * 0.55f, 4.5f, 8f);
            buoyancy.HeaveHalfLife = 0.24f;
            buoyancy.TiltHalfLife = 0.42f;
            buoyancy.MaxPitchDeg = Mathf.Max(buoyancy.MaxPitchDeg, 6f);
            buoyancy.MaxRollDeg = Mathf.Max(buoyancy.MaxRollDeg, 7f);
            buoyancy.RecaptureRestPose();
            EditorUtility.SetDirty(buoyancy);
        }

        EditorUtility.SetDirty(raftWorld.transform);
        EditorUtility.SetDirty(oceanCamera);
        EditorUtility.SetDirty(raftCameraObject);
        EditorUtility.SetDirty(raftViewportObject);
        EditorUtility.SetDirty(oceanViewportObject);
        Undo.CollapseUndoOperations(undoGroup);

        if (!EditorSceneManager.SaveScene(scene, targetPath, false))
            throw new IOException("统一渲染修改已完成，但最终保存失败。");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string report =
            "PASS=true\n" +
            "Scene=" + targetPath + "\n" +
            "OceanCamera=" + oceanCamera.name + "\n" +
            "OceanCameraMask=" + oceanCamera.cullingMask + "\n" +
            "OldRaftCameraActive=" + raftCameraObject.activeSelf + "\n" +
            "OldRaftViewportActive=" + raftViewportObject.activeSelf + "\n" +
            "SeaLevel=" + seaLevel.ToString("0.000") + "\n" +
            "HullBoundsBefore=" + BoundsText(hullBoundsBefore) + "\n" +
            "HullBoundsFinal=" + BoundsText(finalHullBounds) + "\n" +
            "TargetSubmergedFraction=" + submergedFraction.ToString("0.00") + "\n" +
            "RaftWorldPosition=" + raftWorld.transform.position.ToString("F3") + "\n";
        File.WriteAllText(ReportPath, report);
        Debug.Log("[鲸落] 已生成海面与木筏统一渲染场景：" + targetPath + "\n" + report);
        Selection.activeGameObject = raftWorld;
        EditorGUIUtility.PingObject(raftWorld);
    }

    private static Bounds FindHullBounds(GameObject raftRoot)
    {
        GameObject hull = All<GameObject>(raftRoot.scene)
            .FirstOrDefault(go => go.transform.IsChildOf(raftRoot.transform) &&
                                  go.name.IndexOf("wooden_xylophone", StringComparison.OrdinalIgnoreCase) >= 0);
        Transform searchRoot = hull ? hull.transform : raftRoot.transform;
        Renderer[] renderers = searchRoot.GetComponentsInChildren<Renderer>(true)
            .Where(r => r.gameObject.layer == RaftLayer && !(r is ParticleSystemRenderer))
            .ToArray();
        if (renderers.Length == 0)
            throw new InvalidOperationException("找不到木筏主体的 Layer 30 Renderer，无法自动计算吃水线。");

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        if (bounds.size.y < 0.01f)
            throw new InvalidOperationException("木筏主体包围盒高度异常，无法自动计算吃水线。");
        return bounds;
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

    private static string BoundsText(Bounds b)
    {
        return "center=" + b.center.ToString("F3") + ", size=" + b.size.ToString("F3") +
               ", min=" + b.min.ToString("F3") + ", max=" + b.max.ToString("F3");
    }
}
