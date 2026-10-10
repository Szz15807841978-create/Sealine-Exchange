using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace WhaleFallEditor
{
    /// <summary>通过 Assets/Editor/WhaleFallArtImport.request 自动应用美术导入设置。</summary>
    [InitializeOnLoad]
    internal static class WhaleFallArtImportAuto
    {
        private const string RequestPath = "Assets/Editor/WhaleFallArtImport.request";
        static WhaleFallArtImportAuto() { EditorApplication.delayCall += RunOnce; }
        private static void RunOnce()
        {
            if (!File.Exists(RequestPath)) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            try
            {
                WhaleFallArtImport.Apply();
                AssetDatabase.DeleteAsset(RequestPath);
                AssetDatabase.Refresh();
            }
            catch (System.Exception e) { Debug.LogException(e); }
        }
    }

    /// <summary>
    /// 搜打撤用的贴图导入设置：保留 alpha、关 mipmap、不压缩、钳制寻址，保证线稿缩放后干净。
    /// 关键点是 alphaSource —— 程序化设置 textureType 时它会被重置成 None，
    /// 那样纹理会被导成 RGB24，透明区域全部变白，精灵周围出现白/浅色方块（Play 实测踩到过）。
    /// </summary>
    public static class WhaleFallArtImport
    {
        private const string Root = "Assets/Resources/WhaleFall";

        [MenuItem("鲸落/搜打撤/配置美术导入设置")]
        public static void Apply()
        {
            if (!AssetDatabase.IsValidFolder(Root))
            {
                Debug.LogWarning("[鲸落] 找不到 " + Root);
                return;
            }

            // 先把路径收集齐，再逐个改：边遍历 FindAssets 边 SaveAndReimport 会抛异常
            var paths = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Root }))
                paths.Add(AssetDatabase.GUIDToAssetPath(guid));

            int ok = 0, fail = 0;
            foreach (var path in paths)
            {
                try
                {
                    var imp = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (imp == null) continue;
                    imp.textureType = TextureImporterType.Default;
                    imp.alphaSource = TextureImporterAlphaSource.FromInput;   // ★ 必须显式设置
                    imp.alphaIsTransparency = true;
                    imp.mipmapEnabled = false;
                    imp.wrapMode = TextureWrapMode.Clamp;
                    imp.filterMode = FilterMode.Bilinear;
                    imp.textureCompression = TextureImporterCompression.Uncompressed;
                    imp.maxTextureSize = 2048;
                    imp.SaveAndReimport();
                    ok++;
                }
                catch (System.Exception e)
                {
                    fail++;
                    Debug.LogWarning("[鲸落] 导入设置失败 " + path + " : " + e.Message);
                }
            }
            Debug.Log("[鲸落] 搜打撤美术导入设置完成：成功 " + ok + " 张，失败 " + fail + " 张");
        }
    }
}
