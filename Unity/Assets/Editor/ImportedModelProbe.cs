using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FishingEditor
{
    [InitializeOnLoad]
    internal static class ImportedModelProbe
    {
        const string Request="Assets/Editor/ImportedModelProbe.request";
        static ImportedModelProbe(){EditorApplication.delayCall+=Run;}
        static void Run()
        {
            if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isPlayingOrWillChangePlaymode)return;
            foreach(var path in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Art"}).Select(AssetDatabase.GUIDToAssetPath).Where(x=>!x.Contains("Chest3D/")))
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab);
                try
                {
                    var renderers=instance.GetComponentsInChildren<Renderer>(true);
                    var b=renderers[0].bounds;foreach(var r in renderers.Skip(1))b.Encapsulate(r.bounds);
                    Debug.Log("[ModelProbe] "+path+" renderers="+renderers.Length+" bounds="+b.size+" children="+
                              string.Join(",",renderers.Select(r=>r.name+"@"+r.bounds.center.ToString("F3")+"/"+r.bounds.size.ToString("F3"))));
                }
                finally {Object.DestroyImmediate(instance);}
            }
            AssetDatabase.DeleteAsset(Request);
            Debug.Log("[ModelProbe] DONE");
        }
    }
}
