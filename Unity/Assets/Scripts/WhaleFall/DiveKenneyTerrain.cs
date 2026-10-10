using System.Collections.Generic;
using UnityEngine;

namespace WhaleFall
{
    /// <summary>
    /// 用 Kenney Nature Kit（CC0）的 cliff 模块按网格生成海底地形。
    ///
    /// 模块约定（实测 FBX 顶点包围盒得出）：
    ///   · 原始网格 = 10 × 10 × 10 单位
    ///   · 轴心在**底面中心**（min.y = 0，x/z 居中）
    ///   · 面片件 Z 很薄（1.685 / 4.185），实心件 Z = 10
    /// 所以这里统一缩放 BlockMeters / 10，并把块的底面贴到格子的底边。
    /// 侧视游戏只用面片件 + 少量实心件做厚度。
    /// </summary>
    public static class DiveKenneyTerrain
    {
        private const string ResRoot = "WhaleFall/Kenney/";
        private const float KitGrid = 10f;         // Kenney 模块原始网格边长
        public const float BlockMeters = 2.5f;     // 每块在世界里的边长（= 2 个 1.25 m 格）

        /// <summary>一套加载好的模块。缺哪个就为 null，生成时会退而求其次。</summary>
        public sealed class Kit
        {
            public GameObject face, top, half, large, corner, cornerInner, cornerInnerTop,
                              cornerLarge, steps, diagonal, cave, caveBlock, slope, blockHalf, block;
            public GameObject platform;
            public readonly List<GameObject> rocks = new List<GameObject>();

            public bool Ready { get { return face != null && top != null; } }
        }

        private static GameObject Load(string name)
        {
            var go = Resources.Load<GameObject>(ResRoot + name);
            if (go == null) Debug.LogWarning("[鲸落] 缺 Kenney 模块：" + name);
            return go;
        }

        public static Kit LoadKit()
        {
            var k = new Kit();
            k.face = Load("cliff_stone");
            k.top = Load("cliff_top_stone");
            k.half = Load("cliff_half_stone");
            k.large = Load("cliff_large_stone");
            k.corner = Load("cliff_corner_stone");
            k.cornerInner = Load("cliff_cornerInner_stone");
            k.cornerInnerTop = Load("cliff_cornerInnerTop_stone");
            k.cornerLarge = Load("cliff_cornerLarge_stone");
            k.steps = Load("cliff_steps_stone");
            k.diagonal = Load("cliff_diagonal_stone");
            k.cave = Load("cliff_cave_stone");
            k.caveBlock = Load("cliff_blockCave_stone");
            k.slope = Load("cliff_blockSlope_stone");
            k.blockHalf = Load("cliff_blockHalf_stone");
            k.block = Load("cliff_block_stone");
            k.platform = Load("platform_stone");

            string[] rockNames =
            {
                "rock_largeA", "rock_largeB", "rock_largeC",
                "rock_tallA", "rock_tallB", "rock_tallC", "rock_tallD",
                "rock_smallA", "rock_smallB", "rock_smallC",
                "rock_smallFlatA", "rock_smallFlatB", "rock_smallTopA",
            };
            foreach (var n in rockNames)
            {
                var g = Resources.Load<GameObject>(ResRoot + n);
                if (g != null) k.rocks.Add(g);
            }
            return k;
        }

        private static readonly Dictionary<GameObject, float> _baseOffset = new Dictionary<GameObject, float>();

        /// <summary>实测模板在默认缩放下的包围盒（世界空间）。</summary>
        private static void MeasurePrefab(GameObject prefab, out Vector3 size, out float minY)
        {
            size = Vector3.one; minY = 0f;
            if (prefab == null) return;
            var probe = Object.Instantiate(prefab);
            probe.transform.position = Vector3.zero;
            probe.transform.rotation = Quaternion.identity;
            var rs = probe.GetComponentsInChildren<Renderer>();
            if (rs.Length > 0)
            {
                Bounds b = rs[0].bounds;
                for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
                size = b.size; minY = b.min.y;
            }
            if (Application.isPlaying) Object.Destroy(probe); else Object.DestroyImmediate(probe);
        }

        private static void MeasurePrefab(GameObject prefab, out Vector3 size)
        {
            float ignored; MeasurePrefab(prefab, out size, out ignored);
        }

        /// <summary>模块底面相对其轴心的偏移（缓存）。用它把块的底面精确贴到格子的底边。</summary>
        private static float BaseOffset(GameObject prefab)
        {
            if (prefab == null) return 0f;
            float v;
            if (_baseOffset.TryGetValue(prefab, out v)) return v;
            Vector3 sz; float minY;
            MeasurePrefab(prefab, out sz, out minY);
            _baseOffset[prefab] = minY;
            return minY;
        }

        /// <summary>按种子生成地形，并把它登记进 world。返回生成的对象根。</summary>
        public static Transform Build(World world, int seed, Transform parent)
        {
            var kit = LoadKit();
            if (!kit.Ready)
            {
                Debug.LogError("[鲸落] Kenney 模块不全，无法生成地形（至少需要 cliff_stone 与 cliff_top_stone）。");
                return null;
            }

            var root = new GameObject("Kenney地形").transform;
            root.SetParent(parent, false);

            var rng = new WfRandom(seed);
            float block = BlockMeters;

            // Kenney 的 FBX 顶点是 10 单位一格，但 Unity 导入时还会按文件单位再乘一次缩放。
            // 直接写 localScale = block/10 会把导入缩放覆盖掉，结果块变得极小（渲染成一堆小点）。
            // 所以拿一个已知尺寸的模块（cliff_block_stone 原始就是 10×10×10）实测反推。
            float scale = block / KitGrid;
            if (kit.block != null)
            {
                Vector3 probe;
                MeasurePrefab(kit.block, out probe);
                float unitsPerGrid = probe.x / KitGrid;          // 1 个 Kenney 网格单位 = 多少 Unity 单位
                if (unitsPerGrid > 0.00001f) scale = block / (KitGrid * unitsPerGrid);
                Debug.Log("[鲸落] Kenney 导入缩放实测：单块原始 X = " + probe.x.ToString("F4")
                    + " Unity 单位 → 每格 " + unitsPerGrid.ToString("F5")
                    + "，采用 scale = " + scale.ToString("F5"));
            }

            int cols = Mathf.Max(8, Mathf.RoundToInt(DiveConfig.WorldW / block));
            int rows = Mathf.Max(4, Mathf.RoundToInt(DiveConfig.WorldH / block));

            // ---------- 1. 生成高度剖面 ----------
            // 从浅到深随机游走，限制单步落差，形成台地而不是锯齿
            var topRow = new int[cols];
            float depth = rng.Range(2f, 5f);                       // 起始深度（块数）
            for (int c = 0; c < cols; c++)
            {
                if (c > 0 && rng.Value() < 0.32f)
                    depth += rng.Range(-2.4f, 2.4f);
                depth = Mathf.Clamp(depth, 1.5f, rows - 3f);
                topRow[c] = Mathf.RoundToInt(depth);
            }
            // 抹平孤立尖刺
            for (int c = 1; c < cols - 1; c++)
                topRow[c] = Mathf.RoundToInt((topRow[c - 1] + topRow[c] * 2f + topRow[c + 1]) * 0.25f);

            // ---------- 2. 决定哪些格子挖成洞穴 ----------
            var isCave = new bool[cols * rows];
            int caveCount = Mathf.Max(2, cols / 9);
            for (int i = 0; i < caveCount; i++)
            {
                int cc = rng.Range(2, Mathf.Max(3, cols - 3));
                int cr = topRow[cc] + rng.Range(1, 3);
                for (int dc = 0; dc < 2; dc++)
                    for (int dr = 0; dr < 2; dr++)
                    {
                        int x = cc + dc, y = cr + dr;
                        if (x < cols && y < rows) isCave[y * cols + x] = true;
                    }
            }

            // ---------- 3. 铺块 ----------
            int placed = 0, caves = 0;
            var colliders = new List<MeshCollider>();
            for (int c = 0; c < cols; c++)
            {
                int top = topRow[c];
                int prevTop = c > 0 ? topRow[c - 1] : top;

                for (int r = top; r < rows; r++)
                {
                    bool front = !isCave[r * cols + c];
                    GameObject prefab;
                    if (r == top) prefab = kit.top;
                    else if (isCave[r * cols + c]) { prefab = kit.cave; caves++; }
                    else prefab = kit.face;

                    // 相邻列高差 ≥2 时，在竖直边放转角件
                    if (r == top && prevTop < top - 1 && kit.cornerInner != null)
                        prefab = kit.cornerInner;

                    var go = Object.Instantiate(prefab, root);
                    go.name = "k_" + c + "_" + r;
                    go.transform.localScale = Vector3.one * scale;
                    // 轴心在底面中心：把块的**实际底面**贴到该格的底边（用实测偏移修正）
                    float baseY = -(r + 1) * block - BaseOffset(prefab) * scale;
                    go.transform.localPosition = new Vector3(
                        (c + 0.5f) * block,
                        baseY,
                        isCave[r * cols + c] ? 1.2f : 0f);   // 洞穴面片往后缩，留出可游的空腔
                    placed++;

                    if (front)
                    {
                        var mc = go.GetComponent<MeshCollider>();
                        if (mc == null)
                        {
                            var mf = go.GetComponent<MeshFilter>();
                            if (mf != null && mf.sharedMesh != null)
                            {
                                mc = go.AddComponent<MeshCollider>();
                                mc.sharedMesh = mf.sharedMesh;
                                mc.convex = false;
                                colliders.Add(mc);
                            }
                        }
                        else colliders.Add(mc);
                    }
                }

                // 台面装饰：岩石
                if (kit.rocks.Count > 0 && rng.Value() < 0.45f)
                {
                    var rock = kit.rocks[rng.Next(kit.rocks.Count)];
                    var go = Object.Instantiate(rock, root);
                    go.name = "rock_" + c;
                    go.transform.localScale = Vector3.one * scale * rng.Range(0.7f, 1.3f);
                    go.transform.localPosition = new Vector3(
                        (c + 0.5f) * block + rng.Range(-0.4f, 0.4f),
                        -top * block,
                        rng.Range(-0.6f, 0.6f));
                    go.transform.localRotation = Quaternion.Euler(0f, rng.Range(0f, 360f), 0f);
                }
            }

            // ---------- 4. 卡通化 + 登记碰撞体 ----------
            DiveWorldView.ApplyToon(root.gameObject, new Color(0.37f, 0.52f, 0.64f));
            Physics.SyncTransforms();

            foreach (var mc in colliders) world.AddTerrainCollider(mc);

            float minX = 0f, maxX = cols * block;
            float bottomY = -rows * block;
            world.MapMinX = minX;
            world.MapMaxX = maxX;
            world.MapBottomY = bottomY;
            world.TerrainQueryZ = 0f;      // 面片就在 z≈0 平面上
            world.RaftPos = new Vector2((minX + maxX) * 0.5f, 0f);

            Debug.Log("[鲸落] Kenney 地形生成完成：块 " + placed + " 个（洞穴 " + caves
                + "），碰撞体 " + world.TerrainColliders.Count
                + "，范围 X[" + minX.ToString("F1") + "," + maxX.ToString("F1")
                + "] 底 " + bottomY.ToString("F1") + "，块边长 " + block + " m");
            return root;
        }
    }
}
