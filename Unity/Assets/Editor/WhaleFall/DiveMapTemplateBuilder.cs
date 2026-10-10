using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WhaleFallEditor
{
    [InitializeOnLoad]
    public static class DiveMapTemplateBuilder
    {
        private const string SourcePath = "Assets/Art/地形文件/地图测试.fbx";
        private const string OutputFolder = "Assets/Art/地形文件/Generated";
        private const string PrefabPath = OutputFolder + "/地图测试_网格碰撞.prefab";
        private const string ScenePath = "Assets/Scenes/DiveLoop.unity";
        private const string CatalogPath = "Assets/Resources/WhaleFall/DiveTerrainCatalog.asset";
        private const string RequestPath = "Assets/Editor/DiveMapTemplate.request";
        private const string SceneObjectName = "地图测试_场景地形";
        private const string MarkerPath = "DiveMapTemplate.success.txt";

        static DiveMapTemplateBuilder() { EditorApplication.delayCall += RunRequest; }

        private static void RunRequest()
        {
            if (!File.Exists(RequestPath) || EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode) return;
            try { Build(); }
            catch (Exception e) { Debug.LogException(e); }
            finally
            {
                if (File.Exists(RequestPath)) AssetDatabase.DeleteAsset(RequestPath);
                AssetDatabase.Refresh();
            }
        }

        [MenuItem("鲸落/搜打撤/从地图测试生成网格碰撞地图")]
        public static void Build()
        {
            AssetDatabase.ImportAsset(SourcePath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(SourcePath) as ModelImporter;
            if (importer == null) throw new InvalidOperationException("地图测试.fbx 尚未成功导入。");
            if (!importer.isReadable)
            {
                importer.isReadable = true;
                importer.SaveAndReimport();
            }

            if (!AssetDatabase.IsValidFolder(OutputFolder)) AssetDatabase.CreateFolder("Assets/Art/地形文件", "Generated");
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePath);
            if (!source) throw new MissingReferenceException(SourcePath);

            var root = PrefabUtility.InstantiatePrefab(source) as GameObject;
            if (!root) throw new InvalidOperationException("无法实例化地图测试模型。");
            try
            {
                PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                root.name = "地图测试_网格碰撞";
                foreach (var old in root.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(old);

                var filters = root.GetComponentsInChildren<MeshFilter>(true)
                    .Where(x => x.sharedMesh != null && x.sharedMesh.vertexCount >= 3).ToArray();
                if (filters.Length == 0) throw new InvalidOperationException("地图测试中没有可碰撞的 MeshFilter。");

                long triangles = 0;
                foreach (var filter in filters)
                {
                    var collider = filter.gameObject.AddComponent<MeshCollider>();
                    collider.sharedMesh = filter.sharedMesh;
                    collider.convex = false;
                    collider.cookingOptions = MeshColliderCookingOptions.CookForFasterSimulation |
                                              MeshColliderCookingOptions.EnableMeshCleaning |
                                              MeshColliderCookingOptions.WeldColocatedVertices;
                    triangles += filter.sharedMesh.triangles.LongLength / 3;
                    EditorUtility.SetDirty(filter.gameObject);
                }

                Bounds original = RendererBounds(root);
                Quaternion rotation = ChooseMapRotation(root);
                root.transform.rotation = rotation;
                Bounds oriented = RendererBounds(root);
                float scale = 100f / Mathf.Max(0.001f, oriented.size.x);
                root.transform.localScale *= scale;
                Bounds scaled = RendererBounds(root);
                root.transform.position += new Vector3(50f - scaled.center.x, -scaled.max.y,
                                                        WhaleFall.World.SwimZ - scaled.center.z);
                Bounds final = RendererBounds(root);

                var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                if (!prefab) throw new InvalidOperationException("网格碰撞 Prefab 保存失败。");
                var catalog = AssetDatabase.LoadAssetAtPath<WhaleFall.DiveTerrainCatalog>(CatalogPath);
                if (!catalog) throw new MissingReferenceException(CatalogPath);
                catalog.mapTemplates = new[] { prefab };
                EditorUtility.SetDirty(catalog);
                AssetDatabase.SaveAssets();

                PutInScene(prefab);
                File.WriteAllText(MarkerPath,
                    "PASS=true\nSource=" + SourcePath + "\nPrefab=" + PrefabPath +
                    "\nMeshes=" + filters.Length + "\nMeshColliders=" + filters.Length +
                    "\nTriangles=" + triangles + "\nOriginalBounds=" + original.size.ToString("F3") +
                    "\nRotation=" + rotation.eulerAngles.ToString("F1") +
                    "\nFinalBounds=" + final.size.ToString("F3") + "\n", new UTF8Encoding(false));
                Debug.Log("[鲸落] 地图测试已生成：" + filters.Length + " 个贴合 MeshCollider。", prefab);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static Quaternion ChooseMapRotation(GameObject root)
        {
            Quaternion best = Quaternion.identity;
            float bestScore = float.MaxValue;
            Vector3[] angles = { Vector3.zero, new Vector3(90,0,0), new Vector3(270,0,0),
                new Vector3(0,90,0), new Vector3(0,270,0), new Vector3(0,0,90), new Vector3(0,0,270),
                new Vector3(90,90,0), new Vector3(90,270,0), new Vector3(270,90,0), new Vector3(270,270,0) };
            foreach (var angle in angles)
            {
                root.transform.rotation = Quaternion.Euler(angle);
                Vector3 s = RendererBounds(root).size;
                if (s.x < 0.001f || s.y < 0.001f) continue;
                float thickness = s.z / Mathf.Max(0.001f, Mathf.Min(s.x, s.y));
                float aspect = s.y / s.x;
                float score = thickness * 8f + Mathf.Abs(Mathf.Log(Mathf.Max(0.001f, aspect / 1.5f)));
                if (score < bestScore) { bestScore = score; best = root.transform.rotation; }
            }
            return best;
        }

        private static Bounds RendererBounds(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) throw new InvalidOperationException("地图测试没有 Renderer。");
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            return b;
        }

        private static void PutInScene(GameObject prefab)
        {
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool loaded = scene.IsValid() && scene.isLoaded;
            if (!loaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == SceneObjectName) UnityEngine.Object.DestroyImmediate(root);
            var instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
            instance.name = SceneObjectName;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            if (!loaded) EditorSceneManager.CloseScene(scene, true);
        }
    }
}
