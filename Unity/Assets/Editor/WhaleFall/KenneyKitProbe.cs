using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace WhaleFallEditor
{
    /// <summary>通过 Assets/Editor/KenneyProbe.request 触发，把 Kenney 模块的尺寸打到日志里。</summary>
    [InitializeOnLoad]
    internal static class KenneyKitProbeAuto
    {
        private const string RequestPath = "Assets/Editor/KenneyProbe.request";
        static KenneyKitProbeAuto() { EditorApplication.delayCall += RunOnce; }
        private static void RunOnce()
        {
            if (!File.Exists(RequestPath)) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            try
            {
                KenneyKitProbe.Run();
                AssetDatabase.DeleteAsset(RequestPath);
                AssetDatabase.Refresh();
            }
            catch (System.Exception e) { Debug.LogException(e); }
        }
    }

    /// <summary>量一批 Kenney 模块的包围盒，用于确定网格尺寸与轴心。</summary>
    public static class KenneyKitProbe
    {
        private const string Root = "Assets/ThirdParty/Kenney3D/NatureKit/Models";

        [MenuItem("鲸落/搜打撤/探测 Kenney 模块尺寸")]
        public static void Run()
        {
            string[] names =
            {
                "cliff_stone", "cliff_rock", "cliff_top_stone", "cliff_half_stone",
                "cliff_corner_stone", "cliff_cornerInner_stone", "cliff_steps_stone",
                "cliff_diagonal_stone", "cliff_cave_stone", "cliff_block_stone",
                "cliff_blockSlope_stone", "cliff_large_stone", "cliff_cornerLarge_stone",
                "cliff_blockHalf_stone", "cliff_blockQuarter_stone",
                "rock_largeA", "rock_tallA", "rock_smallA", "platform_stone",
                "stone_largeA", "stone_tallA", "bridge_stone",
            };

            var sb = new StringBuilder();
            sb.Append("[鲸落] Kenney 模块尺寸探测：\n");
            foreach (var n in names)
            {
                string path = Root + "/" + n + ".fbx";
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null) { sb.Append("  " + n + " : 找不到\n"); continue; }

                var mfs = go.GetComponentsInChildren<MeshFilter>();
                if (mfs.Length == 0) { sb.Append("  " + n + " : 无 MeshFilter\n"); continue; }
                Bounds b = mfs[0].sharedMesh.bounds;
                for (int i = 1; i < mfs.Length; i++) b.Encapsulate(mfs[i].sharedMesh.bounds);

                // 模型根的缩放（FBX 可能带 import scale）
                Vector3 s = go.transform.localScale;
                sb.Append(string.Format("  {0,-26} size=({1:F3},{2:F3},{3:F3})  center=({4:F3},{5:F3},{6:F3})  rootScale=({7:F3},{8:F3},{9:F3})  mesh={10}\n",
                    n, b.size.x, b.size.y, b.size.z, b.center.x, b.center.y, b.center.z, s.x, s.y, s.z, mfs.Length));
            }

            // 顺便统计一下 cliff_ 家族有哪些后缀，方便选件
            var all = AssetDatabase.FindAssets("t:GameObject", new[] { Root });
            int cliff = 0;
            foreach (var g in all)
            {
                var p = AssetDatabase.GUIDToAssetPath(g);
                var f = Path.GetFileNameWithoutExtension(p);
                if (f.StartsWith("cliff_") && f.EndsWith("_stone")) cliff++;
            }
            sb.Append("[鲸落] cliff_*_stone 共 " + cliff + " 个；FBX 总数 " + all.Length + "\n");

            Debug.Log(sb.ToString());
        }
    }
}
