using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace FishingEditor
{
    /// <summary>把扩展 Kenney CC0 素材批量配置为可直接拖入 uGUI 的 Sprite。</summary>
    [InitializeOnLoad]
    public static class KenneyUiLibrarySetup
    {
        const string Request = "Assets/Editor/KenneyUiLibrary.request";
        const string Marker = "KenneyUiLibrarySetup.success.txt";
        static readonly string[] Roots =
        {
            "Assets/ThirdParty/Kenney/UI Pack Adventure/PNG",
            "Assets/ThirdParty/Kenney/Game Icons/PNG",
        };

        static KenneyUiLibrarySetup() { EditorApplication.delayCall += RunRequest; }

        static void RunRequest()
        {
            if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode) return;
            try { Run(); }
            catch (Exception e) { Debug.LogException(e); }
            finally
            {
                if (File.Exists(Request)) AssetDatabase.DeleteAsset(Request);
                AssetDatabase.Refresh();
            }
        }

        [MenuItem("鲸落/界面/配置 Kenney 扩展资源库")]
        public static void Run()
        {
            AssetDatabase.Refresh();
            int configured = 0;
            foreach (string root in Roots)
            {
                foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { root }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!path.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) continue;
                    var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (!importer) continue;

                    string name = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
                    float border = name.StartsWith("panel_") ? 14f
                                 : name.StartsWith("button_") ? 10f
                                 : name.StartsWith("progress_") || name.StartsWith("scrollbar_") ? 6f
                                 : 0f;

                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    var settings = new TextureImporterSettings();
                    importer.ReadTextureSettings(settings);
                    settings.spriteMode = (int)SpriteImportMode.Single;
                    settings.spriteMeshType = SpriteMeshType.FullRect;
                    settings.spriteBorder = new Vector4(border, border, border, border);
                    settings.spritePixelsPerUnit = 100f;
                    settings.spriteGenerateFallbackPhysicsShape = false;
                    importer.SetTextureSettings(settings);
                    importer.alphaIsTransparency = true;
                    importer.mipmapEnabled = false;
                    importer.wrapMode = TextureWrapMode.Clamp;
                    importer.filterMode = FilterMode.Bilinear;
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.maxTextureSize = 512;
                    importer.SaveAndReimport();

                    var asset = AssetDatabase.LoadMainAssetAtPath(path);
                    if (asset) AssetDatabase.SetLabels(asset, new[] { "Kenney", "CC0", "UI" });
                    configured++;
                }
            }

            File.WriteAllText(Marker,
                "PASS=true\nConfigured=" + configured + "\nRoots=" + string.Join(";", Roots) +
                "\nLicense=Creative Commons CC0\n", new UTF8Encoding(false));
            Debug.Log("[Kenney UI] 已配置 " + configured + " 张扩展 Sprite。", AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(Roots[0]));

            // 扩展 Sprite 已可用，立即重新接入三个场景中的主题组件。
            RaftUiSkinSetup.Run(true);
        }
    }
}
