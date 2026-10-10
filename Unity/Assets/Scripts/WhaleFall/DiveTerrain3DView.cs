using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WhaleFall
{
    /// <summary>装配场景里的 3D 地形：补齐碰撞体、统一卡通材质、登记碰撞体供玩法层查询。</summary>
    public sealed class DiveTerrain3DView
    {
        private readonly Transform _root;
        private readonly DiveTerrainCatalog _catalog;
        private readonly List<GameObject> _instances = new List<GameObject>();

        /// <summary>这些是场景里非地形的根对象，扫描时要跳过。</summary>
        private static readonly HashSet<string> SkipRoots = new HashSet<string>
        {
            "搜打撤世界", "HUD画布", "DiveGameHost", "Main Camera",
        };

        public DiveTerrain3DView(Transform parent)
        {
            _root = new GameObject("3D地图模板").transform;
            _root.SetParent(parent, false);
            _catalog = Resources.Load<DiveTerrainCatalog>("WhaleFall/DiveTerrainCatalog");
            if (Object.FindObjectOfType<Light>() == null)
            {
                var go = new GameObject("水下地形主光", typeof(Light));
                go.transform.SetParent(parent, false);
                go.transform.rotation = Quaternion.Euler(38f, -28f, 0f);
                var light = go.GetComponent<Light>();
                light.type = LightType.Directional;
                light.color = new Color(0.65f, 0.84f, 1f);
                light.intensity = 0.9f;
            }
        }

        public void Rebuild(World world, int seed)
        {
            foreach (var old in _instances)
            {
                if (!old) continue;
                old.SetActive(false);
                if (Application.isPlaying) Object.Destroy(old);
                else Object.DestroyImmediate(old);
            }
            _instances.Clear();
            world.ClearTerrainColliders();

            // 地形来源二选一：
            //   勾了 useKenneyGenerator → 用 Kenney Nature Kit（CC0）按种子程序化生成；
            //   否则                  → 沿用场景里手工摆的 3D 模块（DiveLoop 那套 Tripo 地形）。
            if (_catalog != null && _catalog.useKenneyGenerator)
            {
                var t = DiveKenneyTerrain.Build(world, seed, _root);
                if (t != null) { _instances.Add(t.gameObject); return; }
                Debug.LogWarning("[鲸落] Kenney 生成器没出结果，回退到场景手摆地形。");
            }

            // 地形是**手工摆的场景根对象**（海崖 / 海底 / 柱崖 / 拱坡 / 阶梯 …），
            // 不是挂在某个固定名字的父节点下。以前只找 "地图测试_场景地形"，
            // 找不到就实例化模板 —— 结果手摆的那批既没补碰撞体、也没换卡通材质，
            // 而且模板还会额外叠一份上去。所以这里按"场景根"收集。
            var roots = new List<Transform>();
            var scene = SceneManager.GetActiveScene();
            foreach (var go in scene.GetRootGameObjects())
            {
                if (!go) continue;
                if (SkipRoots.Contains(go.name)) continue;
                if (go.GetComponent<Camera>() || go.GetComponent<Canvas>()) continue;
                if (go.GetComponentInChildren<MeshFilter>(true) == null) continue;
                roots.Add(go.transform);
            }

            if (roots.Count == 0)
            {
                // 场景里没有手摆地形 → 用 Kenney 程序化生成（新场景走这条）
                var gen = DiveKenneyTerrain.Build(world, seed, _root);
                if (gen != null) { _instances.Add(gen.gameObject); return; }

                if (!_catalog || _catalog.mapTemplates == null || _catalog.mapTemplates.Length == 0)
                {
                    Debug.LogError("[鲸落] 场景里没有手摆地形，模块表里也没有地图模板，Kenney 也不可用。");
                    return;
                }
                var rng = new WfRandom(seed);
                var prefab = _catalog.mapTemplates[rng.Next(_catalog.mapTemplates.Length)];
                var map = Object.Instantiate(prefab, _root, false);
                map.name = "本次地图_" + prefab.name;
                _instances.Add(map);
                roots.Add(map.transform);
            }

            // 1) 补齐缺失的 MeshCollider（拱桥/崖壁这类子物件常常没挂）
            int added = 0;
            foreach (var root in roots)
            {
                foreach (var f in root.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (f.sharedMesh == null || f.sharedMesh.vertexCount < 3) continue;
                    if (f.GetComponent<MeshCollider>() != null) continue;
                    var mc = f.gameObject.AddComponent<MeshCollider>();
                    mc.sharedMesh = f.sharedMesh;
                    mc.convex = false;
                    added++;
                }
            }
            Physics.SyncTransforms();
            if (added > 0) Debug.Log("[鲸落] 地形补齐了 " + added + " 个缺失的 MeshCollider");

            // 2) 统一卡通材质。强制同一个色调：地形由几十块模型拼成，
            //    各自贴图的明暗不一，不统一就会一块深一块浅（柱崖/海崖对比最明显）。
            var terrainTint = new Color(0.37f, 0.52f, 0.64f);   // 调色板 灰蓝 #5E84A3
            foreach (var root in roots) DiveWorldView.ApplyToon(root.gameObject, terrainTint);

            // 3) 登记碰撞体 + 用总包围盒确定地图范围
            bool has = false;
            Bounds bounds = default;
            foreach (var root in roots)
            {
                foreach (var c in root.GetComponentsInChildren<MeshCollider>(true))
                {
                    if (!c || c.sharedMesh == null || c.sharedMesh.vertexCount < 3) continue;
                    c.enabled = true;
                    c.convex = false;
                    if (!has) { bounds = c.bounds; has = true; } else bounds.Encapsulate(c.bounds);
                    world.AddTerrainCollider(c);
                }
            }
            if (!has)
            {
                Debug.LogError("[鲸落] 场景里的地形没有可用的碰撞网格。");
                return;
            }

            world.MapMinX = bounds.min.x;
            world.MapMaxX = bounds.max.x;
            world.MapBottomY = bounds.min.y;
            // 碰撞查询平面取地形自身的 Z 中心；渲染仍用 World.SwimZ（玩家画在地形前面）。
            world.TerrainQueryZ = bounds.center.z;
            world.RaftPos = new Vector2(bounds.center.x, 0f);
            Physics.SyncTransforms();

            Debug.Log("[鲸落] 地形装配完成：根对象 " + roots.Count + " 个，碰撞体 "
                + world.TerrainColliders.Count + " 个，范围 X[" + bounds.min.x.ToString("F1") + ","
                + bounds.max.x.ToString("F1") + "] 底 " + bounds.min.y.ToString("F1")
                + " 查询面 z=" + world.TerrainQueryZ.ToString("F2"));
        }
    }
}
