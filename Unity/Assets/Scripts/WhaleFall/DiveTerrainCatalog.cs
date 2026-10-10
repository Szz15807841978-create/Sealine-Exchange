using UnityEngine;

namespace WhaleFall
{
    /// <summary>
    /// DiveLoop 的 3D 地形模块表。资产可以放在任意目录；运行时只读取这里的引用。
    /// 由「鲸落/搜打撤/重建 3D 地形模块表」自动生成，也可以在 Inspector 手工调整。
    /// </summary>
    [CreateAssetMenu(menuName = "WhaleFall/Dive Terrain Catalog", fileName = "DiveTerrainCatalog")]
    public sealed class DiveTerrainCatalog : ScriptableObject
    {
        [System.Serializable]
        public class ModuleAdjustment
        {
            public GameObject prefab;
            [Tooltip("Additional rotation after the prefab's saved import-axis correction.")]
            public Vector3 rotationOffset;
            public Vector3 colliderSize = Vector3.one;
            public Vector3 colliderOffset;
        }
        [Header("Per-module orientation and collision")]
        public ModuleAdjustment[] adjustments;
        [Range(2, 8)] public int platformCount = 5;
        [Min(4f)] public float platformWidth = 16f;
        [Header("按用途组织")]
        public GameObject boundaryPlatform;
        public GameObject boundaryCliff;
        public GameObject seabedSlope;
        public GameObject seabedSteps;
        public GameObject seabedPillar;
        [Tooltip("Reserved until a compound collider has been fitted to the opening.")]
        public GameObject seabedArch;
        public GameObject[] decorativeRocks;

        [Header("完整地图模板")]
        [Tooltip("每个模板已经包含按模型网格生成的 MeshCollider。每局只选择一张完整地图。")]
        public GameObject[] mapTemplates;

        [Header("地形来源")]
        [Tooltip("勾上则用 Kenney Nature Kit（CC0）按种子程序化生成地形；" +
                 "不勾则沿用场景里手工摆的 3D 模块。")]
        public bool useKenneyGenerator;

        [Header("大块轮廓（海床、侧壁、宽平台）")]
        public GameObject[] largeModules;

        [Header("中小型岩石（填充地形边缘）")]
        public GameObject[] rockModules;

        [Header("地标（拱门等，少量生成）")]
        public GameObject[] landmarkModules;

    }
}
