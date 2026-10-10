using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace WhaleFallEditor
{
    [InitializeOnLoad]
    public static class DiveTerrainCatalogBuilder
    {
        private const string TerrainFolder = "Assets/Art/地形文件";
        private const string CatalogFolder = "Assets/Resources/WhaleFall";
        private const string CatalogPath = CatalogFolder + "/DiveTerrainCatalog.asset";
        private const string RequestPath = "Assets/Editor/DiveTerrainCatalog.request";
        private const string MarkerPath = "DiveTerrainCatalog.success.txt";

        static DiveTerrainCatalogBuilder() { EditorApplication.delayCall += RunRequest; }

        private static void RunRequest()
        {
            if (!File.Exists(RequestPath) || EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode) return;
            try { Build(); }
            finally
            {
                if (File.Exists(RequestPath)) AssetDatabase.DeleteAsset(RequestPath);
                AssetDatabase.Refresh();
            }
        }

        [MenuItem("鲸落/搜打撤/重建 3D 地形模块表")]
        public static void Build()
        {
            if (!AssetDatabase.IsValidFolder(CatalogFolder))
            {
                if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
                if (!AssetDatabase.IsValidFolder("Assets/Resources/WhaleFall")) AssetDatabase.CreateFolder("Assets/Resources", "WhaleFall");
            }

            var prefabs = AssetDatabase.FindAssets("t:Prefab", new[] { TerrainFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<GameObject>)
                .Where(x => x != null)
                .OrderBy(x => x.name)
                .ToArray();

            var catalog = AssetDatabase.LoadAssetAtPath<WhaleFall.DiveTerrainCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<WhaleFall.DiveTerrainCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            catalog.largeModules = prefabs.Where(x =>
                IsNamed(x, "cliff", "mountain", "landscape", "平台", "崖壁", "缓坡", "阶梯")).ToArray();
            catalog.landmarkModules = prefabs.Where(x => IsNamed(x, "arch", "拱桥", "拱门")).ToArray();
            catalog.largeModules = catalog.largeModules.Except(catalog.landmarkModules).ToArray();
            catalog.rockModules = prefabs.Except(catalog.largeModules).Except(catalog.landmarkModules).ToArray();
            catalog.boundaryPlatform = prefabs.FirstOrDefault(p => IsNamed(p, "边界-平台"));
            catalog.boundaryCliff = prefabs.FirstOrDefault(p => IsNamed(p, "边界-崖壁"));
            catalog.seabedSlope = prefabs.FirstOrDefault(p => IsNamed(p, "海底-缓坡"));
            catalog.seabedSteps = prefabs.FirstOrDefault(p => IsNamed(p, "海底-阶梯"));
            catalog.seabedPillar = prefabs.FirstOrDefault(p => IsNamed(p, "海底-柱子"));
            catalog.seabedArch = prefabs.FirstOrDefault(p => IsNamed(p, "海底-拱桥"));
            catalog.decorativeRocks = prefabs.Where(p => p != catalog.boundaryPlatform &&
                p != catalog.boundaryCliff && p != catalog.seabedSlope && p != catalog.seabedSteps &&
                p != catalog.seabedPillar && p != catalog.seabedArch).ToArray();
            var existing = catalog.adjustments ?? new WhaleFall.DiveTerrainCatalog.ModuleAdjustment[0];
            catalog.adjustments = prefabs.Select(p => existing.FirstOrDefault(a => a != null && a.prefab == p)
                ?? new WhaleFall.DiveTerrainCatalog.ModuleAdjustment { prefab = p }).ToArray();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();

            const int checkedSeeds = 8;
            ValidateGeneration(checkedSeeds);

            File.WriteAllText(MarkerPath,
                "PASS=true\nCatalog=" + CatalogPath +
                "\nLarge=" + catalog.largeModules.Length +
                "\nRock=" + catalog.rockModules.Length +
                "\nLandmark=" + catalog.landmarkModules.Length +
                "\nValidatedSeeds=" + checkedSeeds + "\n");
            Debug.Log("[鲸落] 3D 地形模块表已更新，共 " + prefabs.Length + " 个模块。", catalog);
        }

        private static bool IsNamed(GameObject prefab, params string[] names)
        {
            string text = prefab.name + " " + AssetDatabase.GetAssetPath(prefab);
            return names.Any(text.Contains);
        }

        private static void ValidateGeneration(int count)
        {
            var root = new GameObject("Temporary terrain validation");
            try
            {
                var view = new WhaleFall.DiveTerrain3DView(root.transform);
                for (int seed = 0; seed < count; seed++)
                {
                    var world = WhaleFall.DiveWorldGen.Generate(seed);
                    view.Rebuild(world, seed);
                    WhaleFall.DiveWorldGen.Populate(world, seed);
                    if (world.TerrainColliders.Count < 10) throw new System.Exception("Missing terrain colliders");
                    foreach (var collider in world.TerrainColliders)
                    {
                        var b = collider.bounds;
                        if (!world.HitsTerrain(b.center.x, b.center.y, 0.4f))
                            throw new System.Exception("3D collider does not intersect the swimming plane");
                    }
                    for (float y = -1f; y >= -110f; y -= 0.25f)
                        if (world.HitsTerrain(world.RaftPos.x, y, WhaleFall.DiveConfig.PlayerR))
                            throw new System.Exception("Central route blocked, seed " + seed);
                    if (world.Mines.Count != 2) throw new System.Exception("Missing accessible mines");
                    foreach (var resource in world.Resources)
                        if (world.IsSolid(resource.pos.x, resource.pos.y))
                            throw new System.Exception("Resource spawned in terrain");
                }
            }
            finally { Object.DestroyImmediate(root); Physics.SyncTransforms(); }
        }
    }
}
