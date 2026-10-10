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
    [InitializeOnLoad]
    public static class OriginalOceanSceneBuilder
    {
        public const string Source = "Assets/Davis3D/OceanEnvironmentPack/Scenes/Demonstration.unity";
        public const string Destination = "Assets/Scenes/UnderwaterExploreOriginal.unity";
        const string Request = "../_local/packages/OceanOriginal.request";
        public const string Reports = "../_local/packages/OceanReview";
        static double nextCheck;
        static OriginalOceanSceneBuilder() { EditorApplication.update += Poll; }
        static void Poll()
        {
            if (EditorApplication.timeSinceStartup < nextCheck) return;
            nextCheck = EditorApplication.timeSinceStartup + 2;
            if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            string command = File.ReadAllText(Request).Trim();
            File.Delete(Request);
            try
            {
                if (command == "probe") Probe();
                if (command == "build") Build();
                if (command == "validate") OriginalOceanPlayCheck.Begin();
            }
            catch (Exception e) { Directory.CreateDirectory(Reports); File.WriteAllText(Reports+"/error.txt",e.ToString()); Debug.LogException(e); }
        }

        [MenuItem("鲸落/海底探索/检查资源包完整原场景")]
        public static void Probe()
        {
            Directory.CreateDirectory(Reports);
            var previous = SceneManager.GetActiveScene();
            var previousRoots = Enumerable.Range(0,SceneManager.sceneCount).SelectMany(i=>SceneManager.GetSceneAt(i).GetRootGameObjects()).Where(g=>g.activeSelf).ToArray();
            foreach(var root in previousRoots) root.SetActive(false);
            var scene = EditorSceneManager.OpenScene(Source,OpenSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            var roots = scene.GetRootGameObjects();
            var text = new StringBuilder();
            foreach (var root in roots)
            {
                text.AppendLine("ROOT "+root.name+" pos="+root.transform.position+" scale="+root.transform.localScale+" children="+root.GetComponentsInChildren<Transform>(true).Length);
                foreach(var m in root.GetComponentsInChildren<MeshFilter>(true))
                    if(m.name.IndexOf("Terrain",StringComparison.OrdinalIgnoreCase)>=0) text.AppendLine("MESH "+m.name+" world="+m.GetComponent<Renderer>()?.bounds);
                foreach(var c in root.GetComponentsInChildren<Camera>(true))
                {
                    text.AppendLine("CAMERA "+c.name+" pos="+c.transform.position+" rot="+c.transform.eulerAngles+" fov="+c.fieldOfView);
                    Capture(c,"original");
                    RenderSettings.fogDensity=.028f;
                    c.fieldOfView=55;
                    for(int j=0;j<3;j++)
                    {
                        var target=new Vector3(0,4-j*15,16+j*23);
                        c.transform.position=target+new Vector3(17,3,0);
                        c.transform.LookAt(target);
                        Capture(c,"side-"+j);
                    }
                }
            }
            text.AppendLine("Renderers="+roots.Sum(r=>r.GetComponentsInChildren<Renderer>(true).Length));
            text.AppendLine("Colliders="+roots.Sum(r=>r.GetComponentsInChildren<Collider>(true).Length));
            foreach(var group in roots.SelectMany(r=>r.GetComponentsInChildren<MeshFilter>(true)).Where(m=>!m.sharedMesh).GroupBy(m=>m.name)) text.AppendLine("MissingMesh "+group.Key+" count="+group.Count());
            foreach(string path in AssetDatabase.FindAssets("t:Model",new[]{"Assets/Davis3D/OceanEnvironmentPack/Meshes"}).Select(AssetDatabase.GUIDToAssetPath))
                foreach(var mesh in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>()) text.AppendLine("MODEL "+path+" / "+mesh.name);
            Physics.SyncTransforms();
            for(int z=0;z<=76;z+=4)
            {
                var hits=Physics.RaycastAll(new Vector3(0,15,z),Vector3.down,70).Where(h=>h.collider.gameObject.scene==scene).OrderBy(h=>h.distance).ToArray();
                text.AppendLine("FLOOR z="+z+" "+string.Join(";",hits.Take(3).Select(h=>h.collider.name+" y="+h.point.y.ToString("F2"))));
            }
            var materials=roots.SelectMany(r=>r.GetComponentsInChildren<Renderer>(true)).SelectMany(r=>r.sharedMaterials).Distinct().ToArray();
            foreach(var m in materials) text.AppendLine("MAT "+(m ? m.name+" shader="+m.shader.name+" supported="+m.shader.isSupported : "MISSING"));
            File.WriteAllText(Reports+"/probe.txt",text.ToString());
            EditorSceneManager.CloseScene(scene,true);
            SceneManager.SetActiveScene(previous);
            foreach(var root in previousRoots) root.SetActive(true);
            Debug.Log("Original ocean probe completed: "+Reports);
        }

        [MenuItem("鲸落/海底探索/从完整原场景创建下潜地图")]
        public static void Build()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) return;
            Directory.CreateDirectory(Reports);
            if(File.Exists(Destination))
                throw new InvalidOperationException("Scene already exists; use the existing map instead of overwriting edits: "+Destination);
            var previous=Enumerable.Range(0,SceneManager.sceneCount).Select(SceneManager.GetSceneAt).ToArray();
            var scene=EditorSceneManager.OpenScene(Source,OpenSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            var roots=scene.GetRootGameObjects();
            int originalTransforms=roots.Sum(r=>r.GetComponentsInChildren<Transform>(true).Length);
            int originalRenderers=roots.Sum(r=>r.GetComponentsInChildren<Renderer>(true).Length);
            int missingScripts=0, repairedMeshes=0;
            var meshes=AssetDatabase.LoadAllAssetsAtPath("Assets/Davis3D/OceanEnvironmentPack/Meshes/Plants/SM_Anemone.FBX").OfType<Mesh>().ToDictionary(m=>m.name);
            foreach(var root in roots)
            {
                foreach(var t in root.GetComponentsInChildren<Transform>(true))
                {
                    missingScripts+=GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
                    GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
                }
                foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true))
                {
                    if(filter.sharedMesh==null && filter.name.StartsWith("Anemone_2_LOD"))
                    {
                        string meshName=filter.name.Replace("Anemone_2_","SM_Anemone_");
                        if(meshes.TryGetValue(meshName,out var mesh))
                        {
                            filter.sharedMesh=mesh;
                            PrefabUtility.RecordPrefabInstancePropertyModifications(filter);
                            repairedMeshes++;
                        }
                    }
                    if(filter.name=="Terrain" && !filter.GetComponent<Collider>())
                        filter.gameObject.AddComponent<MeshCollider>().sharedMesh=filter.sharedMesh;
                }
                foreach(var script in root.GetComponentsInChildren<MonoBehaviour>(true))
                    if(script && script.GetType().Name=="FlyCamera") script.enabled=false;
            }
            var camera=roots.SelectMany(r=>r.GetComponentsInChildren<Camera>(true)).First();
            camera.name="Main Camera · 侧视跟随";
            camera.fieldOfView=55;
            camera.nearClipPlane=.1f;
            camera.farClipPlane=160;
            camera.clearFlags=CameraClearFlags.SolidColor;
            camera.backgroundColor=RenderSettings.fogColor;
            // The original first-person fog is too dense at the side camera's longer distance.
            RenderSettings.fogDensity=.028f;
            var player=new GameObject("玩家 · 二维潜水控制",typeof(Rigidbody),typeof(SphereCollider));
            var control=player.AddComponent<WhaleFall.OriginalOceanDiver>();
            control.spawn=new Vector3(0,2,18);
            player.transform.position=control.spawn;
            player.GetComponent<SphereCollider>().radius=.65f;
            var physics=new PhysicMaterial("Swim frictionless"){dynamicFriction=0,staticFriction=0,bounciness=0};
            const string physicsPath="Assets/WhaleFall/UnderwaterExplore/OriginalSwimPhysics.physicMaterial";
            AssetDatabase.CreateAsset(physics,physicsPath);
            player.GetComponent<SphereCollider>().sharedMaterial=physics;
            control.followCamera=camera;
            var pivot=new GameObject("朝向").transform;
            pivot.SetParent(player.transform,false);
            var sourceDiver=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/WhaleFall/Diver/DiverAnimator.prefab");
            if(!sourceDiver) throw new InvalidOperationException("Existing diver prefab missing");
            var diver=(GameObject)PrefabUtility.InstantiatePrefab(sourceDiver,pivot);
            foreach(var rb in diver.GetComponentsInChildren<Rigidbody>(true)) UnityEngine.Object.DestroyImmediate(rb);
            foreach(var col in diver.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(col);
            var anim=diver.GetComponentInChildren<Animator>();
            anim.applyRootMotion=false; anim.Update(0);
            var b=RenderBounds(diver);
            diver.transform.localScale*=2.3f/Mathf.Max(.01f,b.size.y);
            anim.Update(0);
            b=RenderBounds(diver);
            diver.transform.position+=player.transform.position-b.center;
            control.visual=pivot; control.animator=anim;
            var fill=new GameObject("角色近身补光",typeof(Light));
            fill.transform.SetParent(player.transform,false); fill.transform.localPosition=new Vector3(2,1,0);
            var light=fill.GetComponent<Light>(); light.type=LightType.Point; light.color=new Color(.7f,.86f,1); light.range=6; light.intensity=.8f;
            camera.transform.position=control.spawn+control.cameraOffset;
            camera.transform.rotation=Quaternion.LookRotation(-control.cameraOffset,Vector3.up);
            Physics.SyncTransforms();
            var report=new StringBuilder();
            report.AppendLine("Source: "+Source);
            report.AppendLine("Destination: "+Destination);
            report.AppendLine("Original transforms preserved: "+originalTransforms);
            report.AppendLine("Original renderers preserved: "+originalRenderers);
            report.AppendLine("Anemone mesh references reconnected: "+repairedMeshes);
            report.AppendLine("Missing legacy script components removed from copy: "+missingScripts);
            report.AppendLine("Original mesh/material/placement data retained; original scene not saved or modified.");
            for(int z=4;z<=72;z+=4)
            {
                var hits=Physics.RaycastAll(new Vector3(0,15,z),Vector3.down,70).Where(h=>h.collider.gameObject.scene==scene && h.collider.gameObject!=player).OrderBy(h=>h.distance).ToArray();
                report.AppendLine("FLOOR z="+z+" "+string.Join(";",hits.Take(2).Select(h=>h.collider.name+" y="+h.point.y.ToString("F2"))));
            }
            int nullMeshes=roots.SelectMany(r=>r.GetComponentsInChildren<MeshFilter>(true)).Count(m=>!m.sharedMesh);
            report.AppendLine("Missing meshes remaining: "+nullMeshes);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene,Destination);
            foreach(var old in previous) if(!old.isDirty) EditorSceneManager.CloseScene(old,true);
            File.WriteAllText(Reports+"/build.txt",report.ToString());
            Capture(camera,"playable-entry");
            if(SceneView.lastActiveSceneView) SceneView.lastActiveSceneView.LookAt(control.spawn,camera.transform.rotation,18);
            Selection.activeGameObject=player;
            Debug.Log("Original ocean ready: "+Destination);
        }

        static Bounds RenderBounds(GameObject go)
        {
            var rs=go.GetComponentsInChildren<Renderer>(); var b=rs[0].bounds;
            foreach(var r in rs.Skip(1)) b.Encapsulate(r.bounds);
            return b;
        }

        public static void Capture(Camera camera,string name)
        {
            var oldTarget=camera.targetTexture;
            var oldActive=RenderTexture.active;
            var rt=new RenderTexture(1440,900,24);
            var tex=new Texture2D(1440,900,TextureFormat.RGB24,false);
            try
            {
                camera.targetTexture=rt; camera.Render(); RenderTexture.active=rt;
                tex.ReadPixels(new Rect(0,0,1440,900),0,0); tex.Apply();
                File.WriteAllBytes(Reports+"/"+name+".png",tex.EncodeToPNG());
            }
            finally { camera.targetTexture=oldTarget; RenderTexture.active=oldActive; UnityEngine.Object.DestroyImmediate(tex); UnityEngine.Object.DestroyImmediate(rt); }
        }
    }
}
