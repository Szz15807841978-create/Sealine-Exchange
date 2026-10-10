using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace WhaleFallEditor
{
    public static partial class AdditionalOceanScenes
    {
        const string Art = "Assets/WhaleFall/AdditionalOceans";
        static readonly Dictionary<Material, Material> Converted = new Dictionary<Material, Material>();
        static partial void Dispatch(string command)
        {
            if (command == "build") Build();
            else if (command == "audit") Audit();
            else if (command == "polish") Polish();
            else if (command == "verify") AdditionalOceanVerification.Begin();
            else if (command == "low") OpenLow();
            else if (command == "deluxe") OpenDeluxe();
            else if (command == "scale-probe") ProbeScaleAndFish();
            else if (command == "upgrade") UpgradeScaleAndFish();
            else throw new ArgumentException("Unknown ocean scene command: " + command);
        }
        [MenuItem("鲸落/海底探索/打开低多边形沉船礁")]
        public static void OpenLow() { Open(LowScene); }
        [MenuItem("鲸落/海底探索/打开珊瑚生态海底")]
        public static void OpenDeluxe() { Open(DeluxeScene); }
        static void Open(string path)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(path);
            var diver = Object.FindObjectOfType<WhaleFall.Underwater3DDiver>();
            if (diver && SceneView.lastActiveSceneView) SceneView.lastActiveSceneView.LookAt(diver.spawn, diver.followCamera.transform.rotation, 18);
        }
        [MenuItem("鲸落/海底探索/配置另外两张水下地图")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode before configuring scenes.");
            Directory.CreateDirectory(Art); Directory.CreateDirectory(Reports);
            AssetDatabase.Refresh();
            var previous = SceneManager.GetActiveScene();
            var report = new StringBuilder();
            try
            {
                if (!File.Exists(LowScene)) BuildLow(report);
                else report.AppendLine("Existing scene preserved: " + LowScene);
                if (!File.Exists(DeluxeScene)) BuildDeluxe(report);
                else report.AppendLine("Existing scene preserved: " + DeluxeScene);
                var settings = EditorBuildSettings.scenes.ToList();
                foreach (string path in new[] { Underwater3DPrototypeBuilder.Destination, LowScene, DeluxeScene })
                {
                    var entry = settings.FirstOrDefault(s => s.path == path);
                    if (entry == null) settings.Add(new EditorBuildSettingsScene(path, true)); else entry.enabled = true;
                }
                EditorBuildSettings.scenes = settings.ToArray();
                AssetDatabase.SaveAssets();
                report.AppendLine("COMPLETE");
                File.WriteAllText(Reports + "/build.txt", report.ToString());
            }
            finally { if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous); }
        }

        static void BuildLow(StringBuilder report)
        {
            var scene = EditorSceneManager.OpenScene(LowSource, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                var originalRoots = scene.GetRootGameObjects();
                var environment = new GameObject("低多边形海底 · 原版完整布景");
                foreach (var root in originalRoots)
                {
                    Unpack(root);
                    foreach (var t in root.GetComponentsInChildren<Transform>(true)) GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
                    foreach (var camera in root.GetComponentsInChildren<Camera>(true)) Object.DestroyImmediate(camera.gameObject);
                    if (!root) continue;
                    foreach (var body in root.GetComponentsInChildren<Rigidbody>(true)) { body.isKinematic = true; body.useGravity = false; }
                    foreach (var light in root.GetComponentsInChildren<Light>(true)) light.enabled = false;
                    if (root.name == "Water") { Object.DestroyImmediate(root); continue; }
                    root.transform.SetParent(environment.transform, true);
                }
                environment.transform.localScale = Vector3.one * .2f;
                environment.transform.position = new Vector3(-22, 8, 8.2f);
                foreach (var renderer in environment.GetComponentsInChildren<Renderer>(true))
                {
                    renderer.sharedMaterials = renderer.sharedMaterials.Select(ConvertLowMaterial).ToArray();
                    RepairParticle(renderer);
                    renderer.lightmapIndex = -1;
                }
                foreach(var lod in environment.GetComponentsInChildren<LODGroup>(true)) lod.fadeMode=LODFadeMode.None;
                foreach (var filter in environment.GetComponentsInChildren<MeshFilter>(true))
                {
                    string n = filter.name.ToLowerInvariant();
                    if (filter.sharedMesh && !filter.GetComponent<Collider>() && (n.Contains("terrain") || n.Contains("ground") || n.Contains("rock") || n.Contains("cliff") || n.Contains("sunken")))
                        filter.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
                }
                SetupWater(new Color(.07f, .34f, .42f), .018f, 0f, 240);
                Physics.SyncTransforms();
                Vector3 spawn = FindSafe(scene, new Vector3(7, -4, 18));
                var diver = AddDiver(scene, spawn, new Vector2(-82, 80), new Vector2(-12, -1), new Vector2(-48, 64), "低多边形 · 沉船礁", 0);
                AddSchool(scene, "Assets/ThirdParty/UnderwaterExplore/Models/Yellow Tang.fbx", new Vector3(2, -4, 24), 15, .45f, 7, 0, true);
                AddSchool(scene, "Assets/ThirdParty/UnderwaterExplore/Models/Blue Tang.fbx", new Vector3(20, -5, 13), 12, .6f, 6, 0, true);
                AddSchool(scene, "Assets/ThirdParty/UnderwaterExplore/Models/Clownfish.fbx", new Vector3(-4, -6, 10), 9, .35f, 3, 0, true);
                FinishScene(scene, LowScene, diver, report);
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }

        static void BuildDeluxe(StringBuilder report)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            var randomState = UnityEngine.Random.state;
            UnityEngine.Random.InitState(261006);
            try
            {
                SetupWater(new Color(.055f, .27f, .34f), .025f, 1f, 180);
                var geology = new GameObject("礁石峡谷 · Deluxe原版模型").transform;
                var vegetation = new GameObject("珊瑚花园与海草").transform;
                var benthos = new GameObject("海星与贝类").transform;
                CreateSeabed();
                string[] rocks = { "Landscape_rock_large", "Landscape_rock_medium", "Landscape_rock3", "Landscape_rock4", "Underwater_rock5" };
                var reefCenters = new List<Vector3>();
                for (int i = 0; i < 26; i++)
                {
                    float z = -18 + (i / 2) * 6.2f;
                    float x = (i % 2 == 0 ? -1 : 1) * UnityEngine.Random.Range(11f, 20f);
                    var p = new Vector3(x, Floor(x,z) - .6f, z);
                    var rock = Place(DeluxeRoot + "/Prefabs/Landscape/" + rocks[i % rocks.Length] + "_prefab.prefab", geology, p, UnityEngine.Random.Range(7f, 13f), true);
                    rock.name = "礁壁_" + (i + 1);
                    reefCenters.Add(new Vector3(x * .76f, 0, z));
                    AddRockColliders(rock);
                }
                for (int i = 0; i < 35; i++)
                {
                    float x = UnityEngine.Random.Range(-32f, 32f), z = UnityEngine.Random.Range(-22f, 68f);
                    if (Mathf.Abs(x) < 6) x += x < 0 ? -7 : 7;
                    var rock = Place(DeluxeRoot + "/Prefabs/Landscape/Underwater_rock" + (i % 5 + 1) + "_prefab.prefab", geology,
                        new Vector3(x, Floor(x,z) - .2f, z), UnityEngine.Random.Range(1.5f, 4.5f), true);
                    AddRockColliders(rock);
                }
                Physics.SyncTransforms();
                string[] plants = { "Coral_v2", "Coral_v2_1", "Coral_v2_2", "Coral_v3", "Coral_green", "Tube_coral", "Underwater_plant2", "Underwater_plant5" };
                for (int i = 0; i < 300; i++)
                {
                    var center = reefCenters[i % reefCenters.Count];
                    float x = center.x + UnityEngine.Random.Range(-4f,4f), z = center.z + UnityEngine.Random.Range(-3.5f,3.5f);
                    float y = SurfaceY(scene,x,z);
                    Place(DeluxeRoot + "/Prefabs/Vegetation/" + plants[i % plants.Length] + "_prefab.prefab", vegetation,
                        new Vector3(x,y-.08f,z), UnityEngine.Random.Range(.55f,2.1f), true);
                }
                for (int i = 0; i < 60; i++)
                {
                    float x = (i % 2 == 0 ? -1 : 1) * UnityEngine.Random.Range(19f,29f), z = UnityEngine.Random.Range(-20f,66f);
                    Place(DeluxeRoot + "/Prefabs/Vegetation/Underwater_kelp_plant_prefab.prefab", vegetation,
                        new Vector3(x,SurfaceY(scene,x,z)-.15f,z), UnityEngine.Random.Range(3f,6f), true);
                }
                string[] seabedAnimals = { "Starfish_v1", "Starfish_v2", "Murex_shellfish", "Mussel", "Sea_urchin_v1" };
                for (int i = 0; i < 35; i++)
                {
                    float x = UnityEngine.Random.Range(-15f,15f), z = UnityEngine.Random.Range(-14f,60f);
                    Place(DeluxeRoot + "/Prefabs/Animals/" + seabedAnimals[i % 5] + "_prefab.prefab", benthos,
                        new Vector3(x,SurfaceY(scene,x,z),z), UnityEngine.Random.Range(.2f,.6f), true);
                }
                AddSchool(scene, DeluxeRoot + "/Prefabs/Animals/Yellowtang_prefab.prefab", new Vector3(-7,-5,11), 16, .5f, 7, 90, false);
                AddSchool(scene, DeluxeRoot + "/Prefabs/Animals/Powderblue_tang_prefab.prefab", new Vector3(6,-7,24), 18, .55f, 8, 90, false);
                AddSchool(scene, DeluxeRoot + "/Prefabs/Animals/Clownfish_prefab.prefab", new Vector3(-10,-9,36), 12, .35f, 4, 90, false);
                AddSchool(scene, DeluxeRoot + "/Prefabs/Animals/Emperor_angelfish_prefab.prefab", new Vector3(10,-8,44), 8, .8f, 5, 90, false);
                AddSchool(scene, DeluxeRoot + "/Prefabs/Animals/Great_white_shark_prefab.prefab", new Vector3(0,-4,47), 1, 4.5f, 12, 0, false);
                var diver = AddDiver(scene, new Vector3(0,-5,0), new Vector2(-31,31), new Vector2(-22,0), new Vector2(-20,67), "Deluxe · 珊瑚生态海底", 0);
                FinishScene(scene, DeluxeScene, diver, report);
            }
            finally { UnityEngine.Random.state = randomState; EditorSceneManager.CloseScene(scene, true); }
        }

        static Material ConvertLowMaterial(Material source)
        {
            if (!source || !AssetDatabase.GetAssetPath(source).StartsWith("Assets/Ventuar/")) return source;
            if (Converted.TryGetValue(source, out var existing) && existing) return existing;
            string path = Art + "/LowPoly_" + source.name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!mat)
            {
                mat = new Material(source);
                string shaderName = source.name == "PlantCoral" ? "UnderwaterPack/PlantCoral"
                    : source.name == "Fish" ? "UnderwaterPack/Fish"
                    : source.name == "Caustics" ? "UnderwaterPack/Caustics"
                    : source.name == "Glass" ? "UnderwaterPack/Glass"
                    : source.shader && source.shader.isSupported && !source.shader.name.Contains("Error") ? source.shader.name : "Standard";
                mat.shader = Shader.Find(shaderName) ?? Shader.Find("Standard");
                if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", .15f);
                if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0);
                if (source.name == "Caustics")
                {
                    mat.SetFloat("_CausticsStartLevel",0); mat.SetFloat("_CausticsBlending",3);
                    mat.SetFloat("_CausticsIntensity",.4f); mat.SetVector("_CausticsScale",new Vector4(450,520,0,0));
                }
                if (source.name == "Fish") { mat.SetFloat("_WaveHeight1",.035f); mat.SetFloat("_WaveHeight2",.02f); }
                mat.enableInstancing = true;
                AssetDatabase.CreateAsset(mat,path);
            }
            Converted[source] = mat; return mat;
        }
        static void Unpack(GameObject root)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t && PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject))
                    PrefabUtility.UnpackPrefabInstance(t.gameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        }
        static void SetupWater(Color color, float fog, float seaLevel, float size)
        {
            RenderSettings.skybox = null; RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = color; RenderSettings.fogDensity = fog;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.48f,.7f,.75f);
            RenderSettings.ambientEquatorColor = new Color(.19f,.38f,.42f);
            RenderSettings.ambientGroundColor = new Color(.09f,.17f,.20f);
            var sun = new GameObject("水下漫射阳光",typeof(Light));
            sun.transform.rotation = Quaternion.Euler(48,-30,0);
            var light = sun.GetComponent<Light>(); light.type = LightType.Directional; light.color = new Color(.78f,.94f,1); light.intensity = 1.1f; light.shadows = LightShadows.Soft; light.shadowStrength=.5f;
            RenderSettings.sun = light;
            var water = GameObject.CreatePrimitive(PrimitiveType.Plane); water.name = "水面 · 双面波光";
            Object.DestroyImmediate(water.GetComponent<Collider>());
            water.transform.position = new Vector3(0,seaLevel,15); water.transform.localScale = new Vector3(size/10,1,size/10);
            string path = Art + "/OceanSurface.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!mat) { mat = new Material(Shader.Find("WhaleFall/UnderwaterSceneSurface")); AssetDatabase.CreateAsset(mat,path); }
            water.GetComponent<Renderer>().sharedMaterial = mat;
        }
        static WhaleFall.Underwater3DDiver AddDiver(Scene scene, Vector3 spawn, Vector2 x, Vector2 y, Vector2 z, string title, float yaw)
        {
            var referenceScene = SceneManager.GetSceneByPath(Underwater3DPrototypeBuilder.Destination);
            bool opened = !referenceScene.IsValid() || !referenceScene.isLoaded;
            if (opened) referenceScene = EditorSceneManager.OpenScene(Underwater3DPrototypeBuilder.Destination, OpenSceneMode.Additive);
            GameObject player;
            try
            {
                var source = referenceScene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<WhaleFall.Underwater3DDiver>(true)).Single();
                player = Object.Instantiate(source.gameObject); SceneManager.MoveGameObjectToScene(player,scene);
            }
            finally { if(opened) EditorSceneManager.CloseScene(referenceScene,true); SceneManager.SetActiveScene(scene); }
            if(player.GetComponent<WhaleFall.DiveExpedition>()) Object.DestroyImmediate(player.GetComponent<WhaleFall.DiveExpedition>());
            if(player.GetComponent<WhaleFall.DiveInventory>()) Object.DestroyImmediate(player.GetComponent<WhaleFall.DiveInventory>());
            if(player.GetComponent<WhaleFall.HarpoonGun>()) Object.DestroyImmediate(player.GetComponent<WhaleFall.HarpoonGun>());
            player.name = "玩家 · 潜水员 · 3D自由游泳";
            var diver = player.GetComponent<WhaleFall.Underwater3DDiver>();
            diver.allowReset=true;diver.showPrototypeHud=true;
            var cameraGo = new GameObject("Main Camera · 潜水员跟随", typeof(Camera),typeof(AudioListener)); cameraGo.tag = "MainCamera";
            var camera = cameraGo.GetComponent<Camera>(); camera.fieldOfView = 62; camera.nearClipPlane = .1f; camera.farClipPlane = 200;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = RenderSettings.fogColor;
            diver.followCamera = camera; diver.spawn = spawn; player.transform.position = spawn; diver.xLimits=x; diver.yLimits=y; diver.zLimits=z;
            diver.cameraDistance=6; diver.cameraYaw=yaw; diver.cameraPitch=13; diver.testDrive=false; diver.testWorldMove=Vector3.zero;
            diver.SnapCamera();
            var selector = player.GetComponent<WhaleFall.OceanSceneSelector>()??player.AddComponent<WhaleFall.OceanSceneSelector>(); selector.sceneTitle=title;
            selector.verificationWaypoints = new[] { spawn + new Vector3(3,0,2), spawn + new Vector3(-2,-1,5), spawn + new Vector3(0,1,8) };
            return diver;
        }
        static Vector3 FindSafe(Scene scene, Vector3 desired)
        {
            for (int i=0;i<20;i++)
            {
                Vector3 p=desired+new Vector3((i%5-2)*2,0,(i/5)*2);
                if (Physics.OverlapSphere(p,.9f).All(c=>c.gameObject.scene!=scene) && Physics.OverlapSphere(p+Vector3.back*6,.5f).All(c=>c.gameObject.scene!=scene)) return p;
            }
            throw new InvalidOperationException("No clear spawn near "+desired);
        }
        static GameObject Place(string path, Transform parent, Vector3 basePoint, float size, bool onFloor)
        {
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if(!source) throw new FileNotFoundException(path);
            var go=(GameObject)PrefabUtility.InstantiatePrefab(source,parent);
            var bounds=BoundsOf(go);
            float longest=Mathf.Max(bounds.size.x,bounds.size.y,bounds.size.z,.001f);
            go.transform.localScale*=size/longest;
            go.transform.rotation=Quaternion.Euler(0,UnityEngine.Random.Range(0f,360f),0);
            bounds=BoundsOf(go);
            go.transform.position+=basePoint-new Vector3(bounds.center.x,onFloor?bounds.min.y:bounds.center.y,bounds.center.z);
            return go;
        }
        static void AddRockColliders(GameObject root)
        {
            foreach(var filter in root.GetComponentsInChildren<MeshFilter>())
                if(filter.sharedMesh && !filter.GetComponent<Collider>()) filter.gameObject.AddComponent<MeshCollider>().sharedMesh=filter.sharedMesh;
        }
        static void AddSchool(Scene scene,string prefab,Vector3 center,int count,float size,float radius,float yaw,bool low)
        {
            var school=new GameObject("鱼群 · "+Path.GetFileNameWithoutExtension(prefab)).transform;
            for(int i=0;i<count;i++)
            {
                var pivot=new GameObject("游鱼_"+(i+1)).transform; pivot.SetParent(school,false);
                var visual=Place(prefab,pivot,Vector3.zero,size*(.85f+(i%5)*.07f),false);
                visual.transform.rotation=Quaternion.Euler(0,yaw,0);
                if(low)
                {
                    var material=ConvertLowMaterial(AssetDatabase.LoadAssetAtPath<Material>("Assets/Ventuar/UnderwaterPack/PackAssets/Materials/Fish.mat"));
                    foreach(var r in visual.GetComponentsInChildren<Renderer>()) r.sharedMaterial=material;
                }
                foreach(var collider in visual.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(collider);
                var motion=pivot.gameObject.AddComponent<WhaleFall.OceanLifeMotion>();
                motion.center=center+new Vector3(i%3*.45f,(i%4-1.5f)*.35f,(i%5-2)*.3f);
                motion.radius=new Vector2(radius,radius*.5f); motion.speed=.15f+(i%3)*.008f; motion.phase=i*.4f;
                pivot.position=motion.center+new Vector3(Mathf.Sin(motion.phase)*radius,0,Mathf.Cos(motion.phase)*radius*.5f);
            }
        }
        static float Floor(float x,float z) { return -11f - Mathf.Clamp01((z+15)/85)*7f + Mathf.Sin(x*.19f+z*.075f)*.6f + Mathf.Cos(z*.16f)*.45f; }
        static float SurfaceY(Scene scene,float x,float z)
        {
            var hit=Physics.RaycastAll(new Vector3(x,5,z),Vector3.down,40).Where(h=>h.collider.gameObject.scene==scene).OrderBy(h=>h.distance).FirstOrDefault();
            return hit.collider?hit.point.y:Floor(x,z);
        }
        static void CreateSeabed()
        {
            const int n=56; var mesh=new Mesh{name="Deluxe_连续起伏海床"};
            var vertices=new Vector3[(n+1)*(n+1)]; var uv=new Vector2[vertices.Length]; var indices=new int[n*n*6];
            for(int z=0;z<=n;z++) for(int x=0;x<=n;x++)
            { int i=z*(n+1)+x; float px=-65+x*130f/n,pz=-45+z*145f/n; vertices[i]=new Vector3(px,Floor(px,pz),pz); uv[i]=new Vector2(px*.15f,pz*.15f); }
            int at=0;
            for(int z=0;z<n;z++) for(int x=0;x<n;x++)
            { int i=z*(n+1)+x; indices[at++]=i;indices[at++]=i+n+1;indices[at++]=i+1;indices[at++]=i+1;indices[at++]=i+n+1;indices[at++]=i+n+2; }
            mesh.vertices=vertices;mesh.uv=uv;mesh.triangles=indices;mesh.RecalculateNormals();mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh,Art+"/DeluxeSeabed.asset");
            var go=new GameObject("连续海床 · 可碰撞",typeof(MeshFilter),typeof(MeshRenderer),typeof(MeshCollider));
            go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshCollider>().sharedMesh=mesh;
            var mat=new Material(Shader.Find("Standard")){color=new Color(.57f,.62f,.54f)};
            mat.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(DeluxeRoot+"/Textures/Landscape/underwater_rock_detail_tex.tga");
            mat.SetFloat("_Glossiness",.08f);AssetDatabase.CreateAsset(mat,Art+"/DeluxeSeabed.mat");go.GetComponent<Renderer>().sharedMaterial=mat;
        }
        static void FinishScene(Scene scene,string path,WhaleFall.Underwater3DDiver diver,StringBuilder report)
        {
            foreach(var root in scene.GetRootGameObjects())
                foreach(var t in root.GetComponentsInChildren<Transform>(true)) GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
            Physics.SyncTransforms();
            if(!EditorSceneManager.SaveScene(scene,path)) throw new IOException("Could not save "+path);
            var rs=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Renderer>(true)).ToArray();
            report.AppendLine(path+" renderers="+rs.Length+" spawn="+diver.spawn);
            report.AppendLine("Colliders="+scene.GetRootGameObjects().Sum(r=>r.GetComponentsInChildren<Collider>(true).Length));
            report.AppendLine("Invalid materials="+rs.SelectMany(r=>r.sharedMaterials).Count(m=>!m||!m.shader||!m.shader.isSupported||m.shader.name.Contains("Error")));
        }
        static void Audit()
        {
            if(Enumerable.Range(0,SceneManager.sceneCount).Any(i=>SceneManager.GetSceneAt(i).isDirty)) throw new InvalidOperationException("Save scene changes before the audit.");
            var report=new StringBuilder();
            foreach(string path in new[]{LowScene,DeluxeScene})
            {
                var scene=EditorSceneManager.OpenScene(path);
                report.AppendLine(path);
                var roots=scene.GetRootGameObjects();
                foreach(var r in roots.SelectMany(root=>root.GetComponentsInChildren<Renderer>(true)))
                    foreach(var m in r.sharedMaterials)
                        if(!m||!m.shader||!m.shader.isSupported||m.shader.name.Contains("Error")) report.AppendLine("BAD MATERIAL "+r.name+" = "+(m?m.name+" shader="+m.shader?.name:"null"));
                foreach(var f in roots.SelectMany(root=>root.GetComponentsInChildren<MeshFilter>(true)).Where(f=>!f.sharedMesh)) report.AppendLine("NO MESH "+f.name);
                var diver=roots.SelectMany(root=>root.GetComponentsInChildren<WhaleFall.Underwater3DDiver>()).Single();
                Capture(diver.followCamera,Path.GetFileNameWithoutExtension(path)+"-preview");
            }
            File.WriteAllText(Reports+"/audit.txt",report.ToString());
        }
        static void RepairParticle(Renderer renderer)
        {
            if(!(renderer is ParticleSystemRenderer)||renderer.sharedMaterials.All(m=>m)) return;
            string material=renderer.name.Contains("Ray")?"LowPoly_SunShafts":"LowPoly_Bubbles";
            var fallback=AssetDatabase.LoadAssetAtPath<Material>(Art+"/"+material+".mat");
            if(!fallback) throw new InvalidOperationException("Missing particle fallback: "+material);
            renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>m?m:fallback).ToArray();
        }
        static void Polish()
        {
            if(Enumerable.Range(0,SceneManager.sceneCount).Any(i=>SceneManager.GetSceneAt(i).isDirty)) throw new InvalidOperationException("Save modified scenes before presentation adjustments.");
            foreach(string path in new[]{LowScene,DeluxeScene})
            {
                var scene=EditorSceneManager.OpenScene(path);
                foreach(var root in scene.GetRootGameObjects())
                {
                    foreach(var renderer in root.GetComponentsInChildren<Renderer>(true)) RepairParticle(renderer);
                    foreach(var lod in root.GetComponentsInChildren<LODGroup>(true)) lod.fadeMode=LODFadeMode.None;
                    foreach(var light in root.GetComponentsInChildren<Light>()) if(light.type==LightType.Directional) light.shadowStrength=.5f;
                }
                EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            }
            var water=AssetDatabase.LoadAssetAtPath<Material>(Art+"/OceanSurface.mat");water.color=new Color(.08f,.35f,.42f);EditorUtility.SetDirty(water);
            AssetDatabase.SaveAssets();Audit();
        }
        public static void Capture(Camera camera,string name)
        {
            var old=camera.targetTexture;var active=RenderTexture.active;var rt=new RenderTexture(1440,900,24);var tex=new Texture2D(1440,900,TextureFormat.RGB24,false);
            try { camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1440,900),0,0);tex.Apply();File.WriteAllBytes(Reports+"/"+name+".png",tex.EncodeToPNG()); }
            finally { camera.targetTexture=old;RenderTexture.active=active;Object.DestroyImmediate(rt);Object.DestroyImmediate(tex); }
        }
    }
}
