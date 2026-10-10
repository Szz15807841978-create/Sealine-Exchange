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
    public static partial class OceanExpeditionSetup
    {
        // Native Terrain can appear in Edit mode even while its built-in module is excluded.
        // Unity strips both Terrain and TerrainCollider when entering Play/build in that case.
        static void RequireOceanTerrainModules()
        {
            var packages=UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages();
            foreach(string name in new[]{"com.unity.modules.terrain","com.unity.modules.terrainphysics"})
                if(!packages.Any(p=>p.name==name))
                    throw new InvalidOperationException("Enable the built-in package "+name+" in Packages/manifest.json before loading Terrain-based ocean maps.");
        }

        [MenuItem("鲸落/海底探索/恢复第四、五图原生海床（不启动试玩）")]
        public static void RepairOceanTerrain()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||Enumerable.Range(0,SceneManager.sceneCount).Any(i=>SceneManager.GetSceneAt(i).isDirty))
                throw new InvalidOperationException("Save scene edits and stop Play mode before repairing ocean terrain.");
            RequireOceanTerrainModules();
            const string folder="../_local/packages/TerrainFix20261007";
            Directory.CreateDirectory(folder);
            var previous=SceneManager.GetActiveScene();
            var report=new StringBuilder("Terrain and Terrain Physics built-in modules registered. Editor configuration only; no Play-mode test.\n");
            foreach(string path in new[]{Fourth,ShipScene})
            {
                var scene=SceneManager.GetSceneByPath(path);bool opened=!scene.IsValid()||!scene.isLoaded;
                if(opened) scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
                try
                {
                    var terrains=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Terrain>(true)).ToArray();
                    if(terrains.Length==0) throw new InvalidOperationException("Native Terrain missing in "+path+"; do not replace with a flat plane.");
                    bool changed=false;
                    foreach(var terrain in terrains)
                    {
                        if(!terrain.terrainData) throw new InvalidOperationException("Missing terrain height data: "+path);
                        // Preserve the actual authored heightmap, splat layers, material and world transform.
                        bool needsRepair=!terrain.enabled||!terrain.drawHeightmap||!terrain.gameObject.activeSelf;
                        var collider=terrain.GetComponent<TerrainCollider>();
                        needsRepair|=!collider||!collider.enabled||collider.terrainData!=terrain.terrainData;
                        if(needsRepair)
                        {
                            string backup=folder+"/"+Path.GetFileNameWithoutExtension(path)+"-before-terrain-repair.unity";
                            if(!File.Exists(backup)) File.Copy(path,backup);
                            terrain.gameObject.SetActive(true);terrain.enabled=true;terrain.drawHeightmap=true;
                            if(!collider) collider=terrain.gameObject.AddComponent<TerrainCollider>();
                            collider.enabled=true;collider.terrainData=terrain.terrainData;changed=true;
                        }
                        terrain.Flush();
                        report.AppendLine(path+"\n  native heightmap="+AssetDatabase.GetAssetPath(terrain.terrainData)+"; resolution="+terrain.terrainData.heightmapResolution+"; layers="+terrain.terrainData.terrainLayers.Length
                            +"\n  position="+terrain.transform.position+"; active="+terrain.gameObject.activeInHierarchy+"; drawHeightmap="+terrain.drawHeightmap+"; TerrainCollider="+collider.enabled
                            +"\n  material="+(terrain.materialTemplate?terrain.materialTemplate.name:"default Built-in Terrain")+"; scene data repaired="+needsRepair);
                    }
                    if(changed) {EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);}
                }
                finally {if(opened) EditorSceneManager.CloseScene(scene,true);}
            }
            SceneManager.SetActiveScene(previous);SceneView.RepaintAll();
            File.WriteAllText(folder+"/terrain-repair-complete.txt",report.ToString());
        }
    }
}
