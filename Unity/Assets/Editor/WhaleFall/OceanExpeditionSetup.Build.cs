using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using WhaleFall;
using Object=UnityEngine.Object;

namespace WhaleFallEditor
{
    public static partial class OceanExpeditionSetup
    {
        [MenuItem("鲸落/海底探索/配置第四图与生物矿玩法（不启动试玩）")]
        public static void Configure() {BuildRequestedScenes();}
        static partial void BuildRequestedScenes()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Please stop Play mode before setup.");
            if(Enumerable.Range(0,SceneManager.sceneCount).Any(i=>SceneManager.GetSceneAt(i).isDirty)) throw new InvalidOperationException("Save your scene changes before setup.");
            Directory.CreateDirectory(Art);Directory.CreateDirectory(Work);
            var report=new StringBuilder("Scene setup only; no Play-mode / gameplay validation was run.\n");
            string backup=Work+"/Underwater_LifeDeluxe_3D-before-gameplay.unity";
            if(!File.Exists(backup)) File.Copy(Third,backup);
            if(!File.Exists(Fourth)) BuildFourth(report);
            else report.AppendLine("Preserved existing fourth scene.");
            ConfigureThird(report);
            var scenes=EditorBuildSettings.scenes.ToList();
            if(!scenes.Any(s=>s.path==Fourth)) scenes.Add(new EditorBuildSettingsScene(Fourth,true));
            EditorBuildSettings.scenes=scenes.ToArray();AssetDatabase.SaveAssets();
            report.AppendLine("SAVED · Ready for user playtesting. F3 expedition / F4 new reef.");
            File.WriteAllText(Work+"/setup-complete.txt",report.ToString());
        }
        static void BuildFourth(StringBuilder report)
        {
            RequireOceanTerrainModules();
            var source=EditorSceneManager.OpenScene(Source,OpenSceneMode.Single);
            // Work on a new scene asset, leaving the downloaded demo intact.
            EditorSceneManager.SaveScene(source,Fourth,true);
            var scene=EditorSceneManager.OpenScene(Fourth,OpenSceneMode.Single);
            var environment=new GameObject("第四图 · Corals2原版海底布景").transform;
            var roots=scene.GetRootGameObjects().Where(r=>r.transform!=environment).ToArray();
            foreach(var root in roots)
            {
                UnpackAll(root);
                if(root.name.StartsWith("Cube")||root.name=="Fog Volume Data") {Object.DestroyImmediate(root);continue;}
                // Retain the art and terrain; third-party camera/fog controller dependencies are not used.
                foreach(var t in root.GetComponentsInChildren<Transform>(true)) GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
                foreach(var script in root.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(script);
                foreach(var camera in root.GetComponentsInChildren<Camera>(true)) Object.DestroyImmediate(camera);
                foreach(var audio in root.GetComponentsInChildren<AudioListener>(true)) Object.DestroyImmediate(audio);
                foreach(var light in root.GetComponentsInChildren<Light>(true)) Object.DestroyImmediate(light);
                foreach(var probe in root.GetComponentsInChildren<ReflectionProbe>(true)) probe.enabled=false;
                foreach(var body in root.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(body);
                root.transform.SetParent(environment,true);
            }
            environment.position=new Vector3(214,-12,181);
            foreach(var terrain in environment.GetComponentsInChildren<Terrain>(true))
            {
                terrain.gameObject.SetActive(true);terrain.enabled=true;
                if(!terrain.GetComponent<TerrainCollider>()) terrain.gameObject.AddComponent<TerrainCollider>().terrainData=terrain.terrainData;
                terrain.materialTemplate=null;
            }
            foreach(var renderer in environment.GetComponentsInChildren<Renderer>(true))
            {renderer.lightmapIndex=-1;renderer.sharedMaterials=renderer.sharedMaterials.Select(AdaptCoralMaterial).ToArray();}
            foreach(var filter in environment.GetComponentsInChildren<MeshFilter>(true))
            {
                if(!filter.sharedMesh||filter.GetComponent<Collider>()) continue;
                string n=filter.name.ToLowerInvariant();
                if(n.Contains("rock")||n.Contains("shell")||n.Contains("coral")) filter.gameObject.AddComponent<MeshCollider>().sharedMesh=filter.sharedMesh;
            }
            SetupFourthWater();Physics.SyncTransforms();
            var terrainCollider=environment.GetComponentsInChildren<TerrainCollider>().FirstOrDefault();
            Vector3 spawn=new Vector3(0,-7,-1);
            if(terrainCollider&&terrainCollider.Raycast(new Ray(new Vector3(0,5,-1),Vector3.down),out var floor,100)) spawn.y=Mathf.Clamp(floor.point.y+6,-24,-5);
            spawn=FindOpenWater(scene,spawn,1);
            var diver=CloneDiver(scene,spawn);
            var fishFocus=new GameObject("第四图鱼群中心").transform;fishFocus.position=new Vector3(0,-15,28);
            string[] fishPrefabs={"Yellowtang","Powderblue_tang","Clownfish","Emperor_angelfish"};
            for(int school=0;school<4;school++)
            {
                var schoolRoot=new GameObject("游动鱼群 · "+fishPrefabs[school]).transform;
                for(int i=0;i<16;i++)
                {
                    var pivot=new GameObject("游鱼_"+(i+1)).transform;pivot.SetParent(schoolRoot);
                    var visual=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(AdditionalOceanScenes.DeluxeRoot+"/Prefabs/Animals/"+fishPrefabs[school]+"_prefab.prefab"),pivot);
                    FitVisual(visual,.45f+school*.09f,false);visual.transform.localRotation=Quaternion.Euler(0,90,0);
                    foreach(var col in visual.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(col);
                    var swim=pivot.gameObject.AddComponent<MineralFaunaSwimmer>();swim.attraction=fishFocus;swim.preferredRadius=8+school*2;swim.maximumRadius=31;swim.yLimits=new Vector2(-23,-3);
                    swim.speed=.8f+(i%5)*.16f;
                    float angle=i*2.39996f+school;
                    var position=fishFocus.position+new Vector3(Mathf.Cos(angle)*(5+school*5),4+(i%3),Mathf.Sin(angle)*(5+school*5));
                    pivot.position=FindOpenWater(scene,position,.35f);
                }
            }
            AddParticleSchool(scene,new Vector3(-5,-9,19));
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            report.AppendLine("Fourth scene: "+Fourth+"; original Corals2 art and terrain, 64 moving fish, diver at "+diver.spawn);
        }
        static Material AdaptCoralMaterial(Material source)
        {
            if(!source) return DefaultMaterial();
            // Pack ships Built-in ASE shaders; keep supported authored materials intact.
            if(source.shader&&source.shader.isSupported&&!source.shader.name.Contains("Error")) return source;
            string path=Art+"/Corals2_"+source.name+".mat";var existing=AssetDatabase.LoadAssetAtPath<Material>(path);if(existing) return existing;
            var mat=new Material(Shader.Find("Standard"));
            mat.mainTexture=TextureProperty(source,"_Albedo")??TextureProperty(source,"_MainTex");
            var normal=TextureProperty(source,"_NormalMap")??TextureProperty(source,"_BumpMap");
            if(normal) {mat.SetTexture("_BumpMap",normal);mat.EnableKeyword("_NORMALMAP");}
            mat.SetFloat("_Glossiness",.2f);mat.enableInstancing=true;
            if(source.name.ToLowerInvariant().Contains("weed")) {mat.SetFloat("_Mode",1);mat.SetFloat("_Cutoff",.45f);mat.EnableKeyword("_ALPHATEST_ON");mat.renderQueue=2450;}
            AssetDatabase.CreateAsset(mat,path);return mat;
        }
        static Texture TextureProperty(Material mat,string key) {return mat.HasProperty(key)?mat.GetTexture(key):null;}
        static Material DefaultMaterial()
        {
            string path=Art+"/FallbackReef.mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!mat) {mat=new Material(Shader.Find("Standard")){color=new Color(.35f,.48f,.42f)};AssetDatabase.CreateAsset(mat,path);}return mat;
        }
        static void SetupFourthWater()
        {
            RenderSettings.skybox=null;RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;RenderSettings.fogColor=new Color(.025f,.25f,.31f);RenderSettings.fogDensity=.017f;
            RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.48f,.72f,.77f);RenderSettings.ambientEquatorColor=new Color(.2f,.4f,.44f);RenderSettings.ambientGroundColor=new Color(.12f,.21f,.24f);
            var sun=new GameObject("水下漫射阳光",typeof(Light));sun.transform.rotation=Quaternion.Euler(48,-30,0);var light=sun.GetComponent<Light>();light.type=LightType.Directional;light.intensity=1.05f;light.shadows=LightShadows.Soft;light.shadowStrength=.45f;RenderSettings.sun=light;
            var water=GameObject.CreatePrimitive(PrimitiveType.Plane);water.name="水面 · 双面波光";Object.DestroyImmediate(water.GetComponent<Collider>());water.transform.position=new Vector3(0,0,25);water.transform.localScale=new Vector3(25,1,25);
            water.GetComponent<Renderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/WhaleFall/AdditionalOceans/OceanSurface.mat");
        }
        static Underwater3DDiver CloneDiver(Scene target,Vector3 spawn)
        {
            var reference=EditorSceneManager.OpenScene(Underwater3DPrototypeBuilder.Destination,OpenSceneMode.Additive);
            GameObject player;
            try {var old=reference.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Underwater3DDiver>()).Single();player=Object.Instantiate(old.gameObject);SceneManager.MoveGameObjectToScene(player,target);}
            finally {EditorSceneManager.CloseScene(reference,true);SceneManager.SetActiveScene(target);}
            // First map is now an expedition; copy its diver, not its scene-bound gameplay session.
            if(player.GetComponent<DiveExpedition>()) Object.DestroyImmediate(player.GetComponent<DiveExpedition>());
            if(player.GetComponent<DiveInventory>()) Object.DestroyImmediate(player.GetComponent<DiveInventory>());
            if(player.GetComponent<HarpoonGun>()) Object.DestroyImmediate(player.GetComponent<HarpoonGun>());
            var diver=player.GetComponent<Underwater3DDiver>();player.name="玩家 · 潜水员 · 3D自由游泳";
            diver.allowReset=true;diver.showPrototypeHud=true;
            var camera=new GameObject("Main Camera · 潜水员跟随",typeof(Camera),typeof(AudioListener)).GetComponent<Camera>();camera.tag="MainCamera";camera.fieldOfView=62;camera.nearClipPlane=.1f;camera.farClipPlane=250;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=RenderSettings.fogColor;
            diver.followCamera=camera;diver.spawn=spawn;player.transform.position=spawn;diver.xLimits=new Vector2(-43,43);diver.yLimits=new Vector2(-42,-.7f);diver.zLimits=new Vector2(-20,75);diver.cameraYaw=0;diver.cameraDistance=6;diver.testDrive=false;diver.SnapCamera();
            var selector=player.GetComponent<OceanSceneSelector>()??player.AddComponent<OceanSceneSelector>();selector.sceneTitle="Corals2 · 珊瑚贝壳海底";return diver;
        }
        static void ConfigureThird(StringBuilder report)
        {
            var scene=EditorSceneManager.OpenScene(Third,OpenSceneMode.Single);
            var diver=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Underwater3DDiver>()).Single();
            var existing=diver.GetComponent<DiveExpedition>();
            if(existing) {report.AppendLine("Third scene already has expedition components; preserved Inspector tuning.");return;}
            var resource=scene.GetRootGameObjects().Single(r=>r.name=="资源点");
            var stages=Enumerable.Range(1,4).Select(i=>resource.transform.Find(i.ToString())?.gameObject).ToArray();
            if(stages.Any(s=>!s)) throw new InvalidOperationException("Expected the user's four children 1,2,3,4 under 资源点.");
            Vector3 anchor=resource.transform.position;
            var floorCollider=scene.GetRootGameObjects().Single(r=>r.name=="连续海床 · 可碰撞").GetComponent<Collider>();
            if(floorCollider.Raycast(new Ray(new Vector3(anchor.x,5,anchor.z),Vector3.down),out var floorHit,60)) anchor.y=floorHit.point.y;
            foreach(var stage in stages)
            {
                UnpackAll(stage);stage.SetActive(true);
                var bounds=AdditionalOceanScenes.BoundsOf(stage);stage.transform.position+=anchor-new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
                foreach(var mesh in stage.GetComponentsInChildren<MeshFilter>(true)) if(mesh.sharedMesh&&!mesh.GetComponent<Collider>()) mesh.gameObject.AddComponent<MeshCollider>().sharedMesh=mesh.sharedMesh;
            }
            var node=resource.AddComponent<BioMineralNode>();node.stages=stages;
            var initial=AdditionalOceanScenes.BoundsOf(stages[0]);
            var volume=resource.AddComponent<BoxCollider>();volume.isTrigger=true;volume.center=resource.transform.InverseTransformPoint(initial.center);volume.size=initial.size;node.interactionVolume=volume;node.RefreshStage();
            var session=diver.gameObject.AddComponent<DiveExpedition>();session.diver=diver;session.inventory=diver.gameObject.AddComponent<DiveInventory>();session.mineral=node;session.seaLevel=1;
            diver.yLimits=new Vector2(diver.yLimits.x,2.5f);diver.allowReset=false;diver.showPrototypeHud=false;diver.testDrive=false;
            // Preserve the old ambient shark as an inactive authoring reference, replace its gameplay role with PA Shark.
            var oldShark=scene.GetRootGameObjects().FirstOrDefault(r=>r.name=="鱼群 · Great_white_shark_prefab");if(oldShark) oldShark.SetActive(false);
            AddCover(scene,node,floorCollider);
            session.chestPrefab=BuildChestPrefab();session.chestSites=CreateChestSites(scene,floorCollider,node,diver);
            session.boss=CreateBoss(scene,session,node,report);
            RetargetFauna(scene,node,report);
            var selector=diver.GetComponent<OceanSceneSelector>();selector.sceneTitle="生物矿禁区 · 氧气搜打撤";
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject=session.boss.gameObject;
            if(SceneView.lastActiveSceneView) SceneView.lastActiveSceneView.LookAt(node.Center,Quaternion.Euler(24,25,0),24);
            report.AppendLine("Third scene saved; mineral anchor="+node.Center+"; chest candidate sites="+session.chestSites.Length+"; oxygen="+session.maximumOxygen);
        }
        static void AddCover(Scene scene,BioMineralNode node,Collider floor)
        {
            var root=new GameObject("生物矿周边 · 视线掩体").transform;
            string[] names={"Landscape_rock3_prefab","Landscape_rock4_prefab","Underwater_rock5_prefab"};
            for(int i=0;i<7;i++)
            {
                float a=i*Mathf.PI*2/7;float radius=i%2==0?9:14;
                Vector3 point=node.Center+new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius);
                if(floor.Raycast(new Ray(new Vector3(point.x,5,point.z),Vector3.down),out var hit,60)) point.y=hit.point.y;
                var rock=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(AdditionalOceanScenes.DeluxeRoot+"/Prefabs/Landscape/"+names[i%3]+".prefab"),root);
                FitVisual(rock,4.5f+i%3,true);rock.transform.position+=point;rock.transform.Rotate(0,i*61,0);
                foreach(var mesh in rock.GetComponentsInChildren<MeshFilter>()) if(mesh.sharedMesh&&!mesh.GetComponent<Collider>()) mesh.gameObject.AddComponent<MeshCollider>().sharedMesh=mesh.sharedMesh;
            }
            Physics.SyncTransforms();
        }
        static Vector3[] CreateChestSites(Scene scene,Collider floor,BioMineralNode node,Underwater3DDiver diver)
        {
            var sites=new List<Vector3>();Physics.SyncTransforms();
            for(int z=-9;z<=59;z+=9) for(int x=-24;x<=24;x+=8)
            {
                if(!floor.Raycast(new Ray(new Vector3(x,3,z),Vector3.down),out var hit,60)||hit.normal.y<.85f) continue;
                Vector3 point=hit.point+Vector3.up*.05f;
                if(Vector3.Distance(point,node.Center)<8||Vector3.Distance(point,diver.spawn)<4) continue;
                if(Physics.OverlapBox(point+Vector3.up*.7f,new Vector3(1.3f,.6f,1),Quaternion.identity,~0,QueryTriggerInteraction.Ignore).Any(c=>c!=floor&&c.gameObject.scene==scene)) continue;
                sites.Add(point);
            }
            if(sites.Count<5) throw new InvalidOperationException("Not enough clear seabed sites for random chests.");return sites.ToArray();
        }
        static GameObject BuildChestPrefab()
        {
            string path=Art+"/ExpeditionLootChest.prefab";var saved=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(saved) return saved;
            var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Chest3D/Chest_LowPoly.prefab"));UnpackAll(go);
            var old=go.GetComponent<Fishing.Chest3DView>();Transform hinge=old?old.LidHinge:null;
            foreach(var script in go.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(script);
            foreach(var collider in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
            var pivot=new GameObject("可开启随机宝箱");go.transform.SetParent(pivot.transform,false);FitVisual(go,1.8f,true);
            var bounds=AdditionalOceanScenes.BoundsOf(go);var box=pivot.AddComponent<BoxCollider>();box.center=bounds.center;box.size=bounds.size;
            var chest=pivot.AddComponent<DiveLootChest>();chest.lid=hinge;
            saved=PrefabUtility.SaveAsPrefabAsset(pivot,path);Object.DestroyImmediate(pivot);return saved;
        }
        static SharkBossAI CreateBoss(Scene scene,DiveExpedition session,BioMineralNode node,StringBuilder report)
        {
            string asset="Assets/PopupAsylum/PA_Shark/_Imported3D/PA_Shark.fbx";
            var root=new GameObject("BOSS · 大白鲨 · PA Shark");var visual=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(asset),root.transform);
            FitVisual(visual,6,false);
            var mat=AssetDatabase.LoadAssetAtPath<Material>(Art+"/PAShark_BuiltIn.mat");
            if(!mat)
            {
                mat=new Material(Shader.Find("Standard"));mat.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/PopupAsylum/PA_Shark/_Textures/PA_SharkDiffuse.png");
                mat.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/PopupAsylum/PA_Shark/_Textures/PA_SharkNorm.png"));mat.EnableKeyword("_NORMALMAP");mat.SetFloat("_Glossiness",.4f);mat.SetFloat("_Metallic",0);AssetDatabase.CreateAsset(mat,Art+"/PAShark_BuiltIn.mat");
            }
            foreach(var renderer in visual.GetComponentsInChildren<Renderer>()) {renderer.sharedMaterial=mat;if(renderer is SkinnedMeshRenderer skin) {skin.quality=SkinQuality.Bone4;skin.updateWhenOffscreen=true;}}
            var clips=AssetDatabase.LoadAllAssetsAtPath(asset).OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__")).ToArray();
            string controllerPath=Art+"/PASharkBoss.controller";
            var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if(!controller)
            {
                controller=AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
                foreach(string name in new[]{"Swim","Bite","Knockback"})
                {
                    string sourceName=name=="Knockback"?"PA_SharkKnockBackClip":"PA_Shark"+name+"Clip";
                    var clip=Object.Instantiate(clips.Single(c=>c.name==sourceName));clip.name=name;
                    var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=name=="Swim";AnimationUtility.SetAnimationClipSettings(clip,settings);
                    AssetDatabase.CreateAsset(clip,Art+"/Shark_"+name+".anim");var state=controller.layers[0].stateMachine.AddState(name);state.motion=clip;
                    if(name=="Swim") controller.layers[0].stateMachine.defaultState=state;
                }
            }
            var animator=visual.GetComponent<Animator>()??visual.AddComponent<Animator>();animator.runtimeAnimatorController=controller;animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            var boss=root.AddComponent<SharkBossAI>();boss.expedition=session;boss.mineral=node;boss.animator=animator;boss.head=visual.GetComponentsInChildren<Transform>().Single(t=>t.name=="PA_SharkNoseTipJnt");boss.depthLimits=new Vector2(-19,-2);
            root.transform.position=FindOpenWater(scene,node.Center+new Vector3(10,6,7),1.9f);root.transform.rotation=Quaternion.Euler(0,150,0);
            var body=root.AddComponent<CapsuleCollider>();body.direction=2;body.radius=boss.bodyRadius;body.height=5;body.isTrigger=true;
            report.AppendLine("PA Shark BOSS: 6 world units, swim / bite / knockback animations; Inspector controls on "+root.name);return boss;
        }
        static void RetargetFauna(Scene scene,BioMineralNode node,StringBuilder report)
        {
            var focus=new GameObject("生态吸引中心 · 生物矿").transform;focus.position=node.Center;
            int index=0;
            foreach(var walker in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<SplineWalker>()).ToArray())
            {
                if(!walker.gameObject.activeInHierarchy) continue;
                var go=walker.gameObject;Object.DestroyImmediate(walker);
                var fish=go.AddComponent<MineralFaunaSwimmer>();fish.attraction=focus;fish.preferredRadius=7+(index%4)*2;fish.maximumRadius=29;fish.yLimits=new Vector2(-16,-2.2f);fish.speed=.9f+(index%5)*.12f;
                float angle=index*2.39996f;float radius=3+(index%13)*1.25f;
                fish.transform.position=FindOpenWater(scene,node.Center+new Vector3(Mathf.Cos(angle)*radius,3+(index%4)*.65f,Mathf.Sin(angle)*radius),.35f);index++;
            }
            var particleRoot=scene.GetRootGameObjects().FirstOrDefault(r=>r.name=="地图1粒子鱼群");
            if(particleRoot)
            {
                int i=0;foreach(var particles in particleRoot.GetComponentsInChildren<ParticleSystem>())
                {particles.transform.position=node.Center+new Vector3(i++==0?-3:5,5,0);var shape=particles.shape;shape.scale=new Vector3(8,3,8);var main=particles.main;main.maxParticles=70;}
            }
            report.AppendLine("Fauna: "+index+" fish retargeted with exponential falloff around mineral; original particle schools moved nearby.");
        }
        static void AddParticleSchool(Scene scene,Vector3 point)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Davis3D/OceanEnvironmentPack/Prefabs/PS_Medaka.prefab");
            var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab);go.name="第四图 · 小鱼粒子群";go.transform.position=point;
            var ps=go.GetComponent<ParticleSystem>();var main=ps.main;main.maxParticles=100;main.prewarm=true;var shape=ps.shape;shape.scale=new Vector3(10,3,10);
        }
        static void FitVisual(GameObject visual,float longest,bool floor)
        {
            visual.transform.localPosition=Vector3.zero;
            var bounds=AdditionalOceanScenes.BoundsOf(visual);visual.transform.localScale*=longest/Mathf.Max(bounds.size.x,bounds.size.y,bounds.size.z,.001f);
            bounds=AdditionalOceanScenes.BoundsOf(visual);visual.transform.position-=new Vector3(bounds.center.x,floor?bounds.min.y:bounds.center.y,bounds.center.z);
        }
        static Vector3 FindOpenWater(Scene scene,Vector3 desired,float radius)
        {
            for(int i=0;i<60;i++)
            {
                Vector3 p=desired+new Vector3((i%5-2)*1.3f,((i/5)%4)*1.3f,(i/20-1)*2);
                if(!Physics.OverlapSphere(p,radius,~0,QueryTriggerInteraction.Ignore).Any(c=>c.gameObject.scene==scene)) return p;
            }
            return desired+Vector3.up*5;
        }
        static void UnpackAll(GameObject root)
        {
            if(PrefabUtility.IsPartOfPrefabInstance(root)) PrefabUtility.UnpackPrefabInstance(PrefabUtility.GetOutermostPrefabInstanceRoot(root),PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
            foreach(var t in root.GetComponentsInChildren<Transform>(true)) if(t&&PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject)) PrefabUtility.UnpackPrefabInstance(t.gameObject,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
        }
    }
}
