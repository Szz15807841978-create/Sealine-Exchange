using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace WhaleFallEditor
{
    [InitializeOnLoad]
    public static partial class OceanExpeditionSetup
    {
        const string Fourth="Assets/Scenes/Underwater_Corals2_3D.unity";
        const string Third=AdditionalOceanScenes.DeluxeScene;
        const string Source="Assets/_Corals2/Scene.unity";
        const string Art="Assets/WhaleFall/Expedition";
        const string Work="../_local/packages/Expansion20261007";
        const string Request=Work+"/Editor.request";
        static double nextPoll;
        static OceanExpeditionSetup() {EditorApplication.update+=Poll;}
        static void Poll()
        {
            if(EditorApplication.timeSinceStartup<nextPoll) return;nextPoll=EditorApplication.timeSinceStartup+2;
            if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode) return;
            string command=File.ReadAllText(Request).Trim();File.Move(Request,Request+".done-"+DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            try
            {
                if(command=="inventory") Inventory();
                else if(command=="build") BuildRequestedScenes();
                else if(command=="first") ConfigureFirst();
                else if(command=="first-layout") FirstLayout();
                else if(command=="first-continue") ContinueFirst();
                else if(command=="first-chests") RefineFirstChests();
                else if(command=="hunting") ConfigureHunting();
                else if(command=="fair-pursuit") ConfigureFairPursuit();
                else if(command=="ship-layout") SunkenShipLayout();
                else if(command=="ship") ConfigureSunkenShip();
                else if(command=="ship-placement") ShipPlacementLayout();
                else if(command=="ship-continue") ContinueSunkenShip();
                else if(command=="terrain-repair") RepairOceanTerrain();
                else throw new ArgumentException(command);
            }
            catch(Exception e) {File.WriteAllText(Work+"/setup-error.txt",e.ToString());Debug.LogException(e);}
        }
        static void Inventory()
        {
            var report=new StringBuilder();var previous=SceneManager.GetActiveScene();
            foreach(var node in previous.GetRootGameObjects().Where(r=>r.name=="资源点"))
            {
                report.AppendLine("MINERAL ROOT "+node.transform.position);
                foreach(Transform child in node.transform) report.AppendLine("STAGE "+child.name+" pos="+child.position+" local="+child.localPosition+" scale="+child.localScale+" bounds="+AdditionalOceanScenes.BoundsOf(child.gameObject));
            }
            var source=EditorSceneManager.OpenScene(Source,OpenSceneMode.Additive);
            try
            {
                foreach(var root in source.GetRootGameObjects())
                {
                    report.AppendLine("ROOT "+root.name+" pos="+root.transform.position+" renderers="+root.GetComponentsInChildren<Renderer>(true).Length+" bounds="+AdditionalOceanScenes.BoundsOf(root));
                    foreach(var cam in root.GetComponentsInChildren<Camera>(true)) report.AppendLine("CAM "+cam.name+" enabled="+cam.enabled+" active="+cam.gameObject.activeInHierarchy+" pos="+cam.transform.position+" rot="+cam.transform.eulerAngles);
                    foreach(var terrain in root.GetComponentsInChildren<Terrain>(true)) report.AppendLine("TERRAIN "+terrain.transform.position+" size="+terrain.terrainData?.size);
                }
                foreach(var material in source.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Renderer>(true)).SelectMany(r=>r.sharedMaterials).Where(m=>m).Distinct())
                    report.AppendLine("MATERIAL "+AssetDatabase.GetAssetPath(material)+" shader="+material.shader?.name);
            }
            finally {EditorSceneManager.CloseScene(source,true);SceneManager.SetActiveScene(previous);}
            string sharkPath="Assets/PopupAsylum/PA_Shark/_Imported3D/PA_Shark.fbx";
            var shark=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(sharkPath));
            report.AppendLine("SHARK "+AdditionalOceanScenes.BoundsOf(shark));
            foreach(var t in shark.GetComponentsInChildren<Transform>(true).Where(t=>t.name.Contains("Nose")||t.name.Contains("Tail1")||t.name.Contains("Head"))) report.AppendLine("BONE "+t.name+" "+t.position);
            foreach(var clip in AssetDatabase.LoadAllAssetsAtPath(sharkPath).OfType<AnimationClip>()) report.AppendLine("CLIP "+clip.name+" legacy="+clip.legacy+" length="+clip.length);
            Object.DestroyImmediate(shark);File.WriteAllText(Work+"/asset-layout.txt",report.ToString());
        }
        static partial void BuildRequestedScenes();
    }
}
