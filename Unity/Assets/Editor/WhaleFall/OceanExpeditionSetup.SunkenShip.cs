using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using WhaleFall;
using Object=UnityEngine.Object;

namespace WhaleFallEditor
{
    public static partial class OceanExpeditionSetup
    {
        const string ShipArt="Assets/LeartesStudios/UnderwaterShip";
        const string ShipSource=ShipArt+"/Scene/BuiltIn_UnderwaterShip.unity";
        const string ShipScene="Assets/Scenes/Underwater_SunkenShip_3D.unity";
        const string ShipWork="../_local/packages/SunkenShip20261007";
        const string ShipOutput=Art+"/SunkenShip";

        [MenuItem("鲸落/海底探索/配置第五图沉船搜打撤（不启动试玩）")]
        public static void ConfigureSunkenShip()
        {
            RequireOceanTerrainModules();
            if(EditorApplication.isPlayingOrWillChangePlaymode||Enumerable.Range(0,SceneManager.sceneCount).Any(i=>SceneManager.GetSceneAt(i).isDirty))
                throw new InvalidOperationException("Save scene edits and stop Play mode first.");
            if(File.Exists(ShipScene)) throw new InvalidOperationException("Fifth map already exists; refuse to overwrite Inspector or layout edits.");
            Directory.CreateDirectory(ShipWork);Directory.CreateDirectory(ShipOutput);
            var report=new StringBuilder("Fifth-map scene authoring only. No Play-mode / gameplay validation.\n");
            var source=EditorSceneManager.OpenScene(ShipSource,OpenSceneMode.Single);
            // All changes below target a copy. The downloaded demo and maps 1-4 remain intact.
            if(!EditorSceneManager.SaveScene(source,ShipScene,true)) throw new IOException("Could not create fifth-map copy.");
            var scene=EditorSceneManager.OpenScene(ShipScene,OpenSceneMode.Single);
            var environment=new GameObject("第五图 · 原版沉船与海床").transform;
            foreach(var root in scene.GetRootGameObjects().Where(r=>r.transform!=environment).ToArray())
            {
                UnpackAll(root);
                foreach(var t in root.GetComponentsInChildren<Transform>(true)) GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
                foreach(var camera in root.GetComponentsInChildren<Camera>(true)) camera.gameObject.SetActive(false);
                foreach(var listener in root.GetComponentsInChildren<AudioListener>(true)) listener.enabled=false;
                root.transform.SetParent(environment,true);
            }
            AdaptShipEnvironment(environment,report);
            Physics.SyncTransforms();
            var session=CloneShipExpedition(scene);
            FinishSunkenShip(scene,session,environment,report);
        }

        static void ContinueSunkenShip()
        {
            var scene=SceneManager.GetActiveScene();
            if(scene.path!=ShipScene||File.Exists(ShipWork+"/setup-complete.txt")) throw new InvalidOperationException("Only resume an unfinished fifth-map setup.");
            var session=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<DiveExpedition>()).Single();
            var environment=scene.GetRootGameObjects().Single(r=>r.name=="第五图 · 原版沉船与海床").transform;
            FinishSunkenShip(scene,session,environment,new StringBuilder("Resumed fifth-map placement. No Play mode.\n"));
        }

        static void FinishSunkenShip(Scene scene,DiveExpedition session,Transform environment,StringBuilder report)
        {
            var diver=session.diver;var node=session.mineral;var boss=session.boss;
            // The imported ship runs along X, its broken middle is around (0, 12, 3).
            // Keep the authored coordinates, including the original terrain and particle effects.
            session.seaLevel=62;session.seed=0;session.minimumChests=4;session.maximumChests=5;
            diver.xLimits=new Vector2(-46,42);diver.yLimits=new Vector2(-3,64);diver.zLimits=new Vector2(-35,38);
            diver.allowReset=false;diver.showPrototypeHud=false;diver.testDrive=false;
            var bounds=AdditionalOceanScenes.BoundsOf(node.stages[0]);
            Vector3 mineralPoint=ShipMineralSite(scene,bounds);
            node.transform.position+=mineralPoint-new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
            node.progress=0;node.RefreshStage();Physics.SyncTransforms();
            AddShipCover(scene,node,report);
            // Approach from outside the hull with a clear view of the wreck, not inside a cabin.
            diver.spawn=ShipOpenWater(scene,new Vector3(-27,18,-20),1.1f,session);
            diver.transform.position=diver.spawn;diver.transform.rotation=Quaternion.identity;
            diver.cameraYaw=38;diver.cameraPitch=5;diver.firstPerson=false;
            diver.followCamera.clearFlags=CameraClearFlags.SolidColor;diver.followCamera.backgroundColor=RenderSettings.fogColor;
            diver.followCamera.nearClipPlane=.04f;diver.followCamera.farClipPlane=350;diver.SnapCamera();
            boss.depthLimits=new Vector2(0,59);boss.patrolRadius=12;boss.excursionRadius=22;boss.leashRadius=42;
            boss.transform.position=ShipOpenWater(scene,node.Center+new Vector3(11,6,-5),3.2f,session);
            boss.transform.rotation=Quaternion.LookRotation(node.Center+Vector3.up*boss.patrolHeight-boss.transform.position);
            session.chestPrefab=BuildChestPrefab();session.chestSites=ShipChestSites(scene,session);
            AddShipFish(scene,session,environment,report);
            var water=GameObject.CreatePrimitive(PrimitiveType.Plane);water.name="第五图水面 · 上浮撤离 y=62";
            Object.DestroyImmediate(water.GetComponent<Collider>());water.transform.position=new Vector3(0,session.seaLevel,0);water.transform.localScale=new Vector3(24,1,24);
            water.GetComponent<Renderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/WhaleFall/AdditionalOceans/OceanSurface.mat");
            var selector=diver.GetComponent<OceanSceneSelector>()??diver.gameObject.AddComponent<OceanSceneSelector>();
            selector.sceneTitle="沉船遗迹 · 生物矿搜打撤";selector.verificationWaypoints=new Vector3[0];
            var scenes=EditorBuildSettings.scenes.ToList();
            if(!scenes.Any(s=>s.path==ShipScene)) scenes.Add(new EditorBuildSettingsScene(ShipScene,true));
            EditorBuildSettings.scenes=scenes.ToArray();
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            Selection.activeGameObject=diver.gameObject;
            if(SceneView.lastActiveSceneView) SceneView.lastActiveSceneView.LookAt(new Vector3(-3,18,3),Quaternion.Euler(14,38,0),45);
            report.AppendLine("SAVED "+scene.path+"; entry F5. Maps 1-4 and source scene preserved.");
            report.AppendLine("Spawn="+diver.spawn+"; mineral="+node.Center+"; shark="+boss.transform.position+"; seaLevel="+session.seaLevel+"; oxygen="+session.maximumOxygen);
            report.AppendLine("Chest candidates="+session.chestSites.Length+"; each round randomly chooses 4-5.\n"+string.Join("\n",session.chestSites.Select(p=>p.ToString())));
            report.AppendLine("Copied current first-map damage, sight, hearing, fair occlusion, inventory, four stages and harpoon tuning.");
            File.WriteAllText(ShipWork+"/setup-complete.txt",report.ToString());
        }

        static void ShipPlacementLayout()
        {
            var scene=SceneManager.GetActiveScene();if(scene.path!=ShipScene) throw new InvalidOperationException("Open the pending fifth map.");
            var node=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<BioMineralNode>()).Single();
            var bounds=AdditionalOceanScenes.BoundsOf(node.stages[0]);
            var report=new StringBuilder("Mineral bounds="+bounds+"; scale="+node.transform.lossyScale+"\n");
            foreach(var terrain in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Terrain>())) report.AppendLine("Terrain="+terrain.transform.position+" size="+terrain.terrainData.size+" collider="+terrain.GetComponent<Collider>().bounds);
            Physics.SyncTransforms();
            for(int z=-26;z<=0;z+=2) for(int x=-16;x<=16;x+=2)
            {
                if(!ShipFloor(scene,x,z,true,out var hit)) {report.AppendLine("no floor "+x+","+z);continue;}
                bool fit=ShipPatch(scene,x,z,new Vector2(bounds.extents.x*.85f,bounds.extents.z*.85f),true,1.2f,out var p,out var floor);
                if(!fit) {report.AppendLine("uneven "+x+","+z+" y="+hit.point.y+" floor="+hit.collider.name);continue;}
                var blockers=Physics.OverlapBox(p+Vector3.up*(bounds.extents.y+.15f),bounds.extents,Quaternion.identity,~0,QueryTriggerInteraction.Ignore)
                    .Where(c=>c.gameObject.scene==scene&&c!=floor&&!c.GetComponentInParent<BioMineralNode>()&&!c.GetComponentInParent<SharkBossAI>()).ToArray();
                report.AppendLine("fit "+p+" blockers="+string.Join(";",blockers.Select(c=>c.name+" "+c.bounds)));
            }
            File.WriteAllText(ShipWork+"/placement-layout.txt",report.ToString());
        }

        static DiveExpedition CloneShipExpedition(Scene target)
        {
            var reference=EditorSceneManager.OpenScene(Underwater3DPrototypeBuilder.Destination,OpenSceneMode.Additive);
            try
            {
                var old=reference.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<DiveExpedition>()).Single();
                SceneManager.SetActiveScene(target);
                var player=Object.Instantiate(old.diver.gameObject);player.name="玩家 · 潜水员 · 沉船搜打撤";
                SceneManager.MoveGameObjectToScene(player,target);
                var session=player.GetComponent<DiveExpedition>();session.diver=player.GetComponent<Underwater3DDiver>();session.inventory=player.GetComponent<DiveInventory>();
                session.mineral=Object.Instantiate(old.mineral.gameObject).GetComponent<BioMineralNode>();
                SceneManager.MoveGameObjectToScene(session.mineral.gameObject,target);session.mineral.name="资源点 · 沉船外侧生物矿（四形态）";
                session.boss=Object.Instantiate(old.boss.gameObject).GetComponent<SharkBossAI>();
                SceneManager.MoveGameObjectToScene(session.boss.gameObject,target);session.boss.name="BOSS · 大白鲨 · 沉船守卫";
                session.boss.expedition=session;session.boss.mineral=session.mineral;
                var camera=Object.Instantiate(old.diver.followCamera.gameObject);camera.name="Main Camera · 沉船潜水员跟随";
                SceneManager.MoveGameObjectToScene(camera,target);camera.SetActive(true);camera.tag="MainCamera";
                session.diver.followCamera=camera.GetComponent<Camera>();session.diver.followCamera.enabled=true;
                var gun=player.GetComponent<HarpoonGun>();gun.diver=session.diver;gun.session=session;BindHarpoonArms(gun);
                return session;
            }
            finally {EditorSceneManager.CloseScene(reference,true);SceneManager.SetActiveScene(target);}
        }

        static void AdaptShipEnvironment(Transform environment,StringBuilder report)
        {
            var sails=AssetDatabase.LoadAssetAtPath<Material>(ShipOutput+"/Sails_BuiltIn.mat");
            if(!sails)
            {
                var shader=Shader.Find("WhaleFall/SunkenShipSails");if(!shader) throw new InvalidOperationException("Sail compatibility shader has not imported.");
                sails=new Material(shader);
                sails.SetTexture("_MainTex",AssetDatabase.LoadAssetAtPath<Texture2D>(ShipArt+"/Art/Textures/T_Cloth_B.png"));
                sails.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(ShipArt+"/Art/Textures/T_Cloth_N.png"));
                sails.SetTexture("_OpacityMask",AssetDatabase.LoadAssetAtPath<Texture2D>(ShipArt+"/Art/Textures/T_Sails_O.PNG"));
                sails.enableInstancing=true;sails.doubleSidedGI=true;AssetDatabase.CreateAsset(sails,ShipOutput+"/Sails_BuiltIn.mat");
            }
            int adapted=0,water=0,far=0,added=0;
            foreach(var renderer in environment.GetComponentsInChildren<Renderer>(true))
            {
                var materials=renderer.sharedMaterials;
                if(materials.Any(m=>m&&m.name=="M_Water"))
                {
                    // Replace the package's presentation water with an explicit reachable extraction surface.
                    renderer.enabled=false;foreach(var col in renderer.GetComponents<Collider>()) col.enabled=false;water++;continue;
                }
                for(int i=0;i<materials.Length;i++) if(materials[i]&&materials[i].name=="M_Sails") {materials[i]=sails;adapted++;}
                renderer.sharedMaterials=materials;
            }
            var meshes=environment.Find("Meshes");
            if(meshes) foreach(Transform child in meshes)
            {
                var renderers=child.GetComponentsInChildren<Renderer>(true);if(renderers.Length==0) continue;
                // The demo contains spare art hundreds of metres outside the actual wreck. Preserve, but disable in this copy.
                if(renderers.All(r=>r.bounds.SqrDistance(new Vector3(0,15,3))>160*160)) {child.gameObject.SetActive(false);far++;}
            }
            foreach(var filter in environment.GetComponentsInChildren<MeshFilter>())
            {
                if(!filter.sharedMesh||filter.GetComponentInParent<Collider>()||filter.GetComponentInParent<ParticleSystem>()) continue;
                string n=filter.name;
                if(n.Contains("_LOD")&&!n.EndsWith("_LOD0")) continue;
                if(!(n.Contains("Hull")||n.Contains("Floor")||n.Contains("Wall")||n.Contains("Rock")||n.Contains("Ground"))) continue;
                filter.gameObject.AddComponent<MeshCollider>().sharedMesh=filter.sharedMesh;added++;
            }
            foreach(var terrain in environment.GetComponentsInChildren<Terrain>())
                if(!terrain.GetComponent<TerrainCollider>()) terrain.gameObject.AddComponent<TerrainCollider>().terrainData=terrain.terrainData;
            // Retain the original light colours and god rays; slightly extend gameplay visibility.
            RenderSettings.fog=true;RenderSettings.fogMode=FogMode.Linear;RenderSettings.fogStartDistance=0;RenderSettings.fogEndDistance=100;
            report.AppendLine("Native solid colliders="+environment.GetComponentsInChildren<Collider>().Length+"; supplemented="+added+"; sail materials adapted="+adapted+"; replaced water renderers="+water+"; distant spare-art roots disabled="+far);
        }

        static bool ShipFloor(Scene scene,float x,float z,bool seabedOnly,out RaycastHit hit)
        {
            foreach(var h in Physics.RaycastAll(new Vector3(x,60,z),Vector3.down,70,~0,QueryTriggerInteraction.Ignore).OrderBy(h=>h.distance))
            {
                var c=h.collider;if(c.gameObject.scene!=scene) continue;
                if(c.GetComponentInParent<BioMineralNode>()||c.GetComponentInParent<SharkBossAI>()||c.GetComponentInParent<Underwater3DDiver>()) continue;
                string names=c.name+" "+(c.transform.parent?c.transform.parent.name:"");
                bool ground=c is TerrainCollider||names.Contains("Ground")||names.Contains("Rock");
                bool deck=names.Contains("Floor");
                if((ground||!seabedOnly&&deck)&&h.normal.y>.7f&&h.point.y<31) {hit=h;return true;}
            }
            hit=default(RaycastHit);return false;
        }

        static bool ShipPatch(Scene scene,float x,float z,Vector2 halfSize,bool seabedOnly,float slope,out Vector3 point,out Collider floor)
        {
            point=Vector3.zero;floor=null;if(!ShipFloor(scene,x,z,seabedOnly,out var middle)) return false;
            float lo=middle.point.y,hi=lo;
            foreach(var offset in new[]{new Vector2(-1,-1),new Vector2(-1,1),new Vector2(1,-1),new Vector2(1,1)})
            {
                if(!ShipFloor(scene,x+offset.x*halfSize.x,z+offset.y*halfSize.y,seabedOnly,out var edge)) return false;
                lo=Mathf.Min(lo,edge.point.y);hi=Mathf.Max(hi,edge.point.y);
            }
            if(hi-lo>slope) return false;point=new Vector3(x,hi+.06f,z);floor=middle.collider;return true;
        }

        static Vector3 ShipMineralSite(Scene scene,Bounds bounds)
        {
            float best=float.PositiveInfinity;Vector3 site=default(Vector3);Collider[] foliage=new Collider[0];
            for(float z=-18;z<=-6;z+=1) for(float x=-7;x<=9;x+=1)
            {
                if(!ShipPatch(scene,x,z,new Vector2(bounds.extents.x*.85f,bounds.extents.z*.85f),true,1.2f,out var p,out var floor)) continue;
                var blockers=Physics.OverlapBox(p+Vector3.up*(bounds.extents.y+.15f),bounds.extents+new Vector3(.7f,0,.7f),Quaternion.identity,~0,QueryTriggerInteraction.Ignore)
                    .Where(c=>c.gameObject.scene==scene&&c!=floor&&!c.GetComponentInParent<BioMineralNode>()&&!c.GetComponentInParent<SharkBossAI>()).ToArray();
                // The demo densely scatters coral everywhere. Make a small resource clearing in the COPY,
                // retaining hull/rock obstacles and leaving disabled coral objects available for later editing.
                if(blockers.Any(c=>!ShipFoliageRoot(c))) continue;
                float score=(new Vector2(x,z)-new Vector2(1,-10)).sqrMagnitude+blockers.Length;
                if(score<best) {best=score;site=p;foliage=blockers;}
            }
            if(float.IsPositiveInfinity(best)) throw new InvalidOperationException("No clear mineral patch beside the wreck; placement requires adjustment.");
            foreach(var root in foliage.Select(ShipFoliageRoot).Distinct()) root.gameObject.SetActive(false);
            return site;
        }

        static Transform ShipFoliageRoot(Collider collider)
        {
            for(var t=collider.transform;t;t=t.parent) if(t.name.Contains("FromFoliage")) return t;
            return null;
        }

        static Vector3 ShipOpenWater(Scene scene,Vector3 desired,float radius,DiveExpedition session)
        {
            for(int i=0;i<225;i++)
            {
                Vector3 p=desired;if(i>0) {float a=i*2.39996f,r=1.5f+Mathf.Sqrt(i)*.5f;p+=new Vector3(Mathf.Cos(a)*r,(i/45)*1.5f,Mathf.Sin(a)*r);}
                if(ShipFloor(scene,p.x,p.z,false,out var floor)) p.y=Mathf.Max(p.y,floor.point.y+radius+1);
                if(p.y>session.seaLevel-radius-2) continue;
                if(!Physics.OverlapSphere(p,radius,~0,QueryTriggerInteraction.Ignore).Any(c=>c.gameObject.scene==scene&&!c.GetComponentInParent<SharkBossAI>()&&!c.GetComponentInParent<Underwater3DDiver>())) return p;
            }
            throw new InvalidOperationException("No open water near "+desired);
        }

        static void AddShipCover(Scene scene,BioMineralNode node,StringBuilder report)
        {
            var root=new GameObject("矿区新增掩体 · 沉船原生礁石").transform;int count=0;
            foreach(var offset in new[]{new Vector2(-8,-3),new Vector2(7,-4),new Vector2(1,-10),new Vector2(-8,6)})
            {
                float x=node.Center.x+offset.x,z=node.Center.z+offset.y;
                if(!ShipPatch(scene,x,z,new Vector2(2,2),true,1.5f,out var p,out var floor)) continue;
                if(Physics.OverlapBox(p+Vector3.up*2,new Vector3(2.8f,1.8f,2.8f),Quaternion.identity,~0,QueryTriggerInteraction.Ignore).Any(c=>c.gameObject.scene==scene&&c!=floor)) continue;
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(ShipArt+"/Art/Prefabs/SM_Rock0"+(count%3+1)+".prefab");
                var rock=(GameObject)PrefabUtility.InstantiatePrefab(prefab,root);rock.name="矿区掩体 "+(++count);
                FitVisual(rock,5.5f,true);rock.transform.position+=p-Vector3.up*.15f;Physics.SyncTransforms();
            }
            report.AppendLine("Added matching native rock cover="+count);
        }

        static Vector3[] ShipChestSites(Scene scene,DiveExpedition session)
        {
            var candidates=new List<Vector3>();Physics.SyncTransforms();
            for(float z=-25;z<=25;z+=1.5f) for(float x=-35;x<=30;x+=1.5f)
            {
                if(!ShipPatch(scene,x,z,new Vector2(1.1f,1.1f),false,.55f,out var p,out var floor)) continue;
                if(Vector3.Distance(p,session.mineral.Center)<7||Vector3.Distance(p,session.diver.spawn)<4) continue;
                if(Physics.OverlapBox(p+Vector3.up*.52f,new Vector3(1.08f,.43f,1.08f),Quaternion.identity,~0,QueryTriggerInteraction.Ignore).Any(c=>c.gameObject.scene==scene&&c!=floor)) continue;
                // Require swimming space above the chest, not a shelf sealed beneath a low ceiling.
                if(Physics.OverlapSphere(p+Vector3.up*1.7f,.75f,~0,QueryTriggerInteraction.Ignore).Any(c=>c.gameObject.scene==scene)) continue;
                candidates.Add(p);
            }
            var spread=new List<Vector3>();
            // Start on a deck if available, then cover the full authored play area.
            while(candidates.Count>0&&spread.Count<24)
            {
                var next=spread.Count==0?candidates.OrderBy(p=>Mathf.Abs(p.y-18)+new Vector2(p.x,p.z).magnitude*.1f).First()
                    :candidates.OrderByDescending(p=>spread.Min(s=>(s-p).sqrMagnitude)).First();
                spread.Add(next);candidates.RemoveAll(p=>Vector3.Distance(p,next)<5);
            }
            if(spread.Count<5) throw new InvalidOperationException("Insufficient separated loot sites: "+spread.Count);
            return spread.ToArray();
        }

        static void AddShipFish(Scene scene,DiveExpedition session,Transform environment,StringBuilder report)
        {
            var root=new GameObject("第五图鱼群 · 向生物矿聚集 / 可捕鱼").transform;
            var focus=new GameObject("生物矿生态中心").transform;focus.SetParent(root);focus.position=session.mineral.Center;
            string[] prefabs={"Mahi_mahi","Barracuda","Atlantic_cod"};string[] species={"鲯鳅","梭鱼","鳕鱼"};
            for(int i=0;i<27;i++)
            {
                var fish=new GameObject(species[i%3]+" · 可捕获 "+(i+1));fish.transform.SetParent(root);
                var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(AdditionalOceanScenes.DeluxeRoot+"/Prefabs/Animals/"+prefabs[i%3]+"_prefab.prefab"),fish.transform);
                FitVisual(model,1.4f+i%4*.22f,false);model.transform.localRotation=Quaternion.Euler(0,90,0);
                foreach(var collider in model.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(collider);
                var swim=fish.AddComponent<MineralFaunaSwimmer>();swim.attraction=focus;swim.preferredRadius=8;swim.maximumRadius=23;
                swim.speed=1.2f+i%4*.18f;swim.yLimits=new Vector2(1,session.seaLevel-4);swim.avoidanceRadius=.5f;
                float angle=i*2.39996f;Vector3 desired=focus.position+new Vector3(Mathf.Cos(angle)*(5+i%5*2),4+i%4,Mathf.Sin(angle)*(5+i%5*2));
                fish.transform.position=ShipOpenWater(scene,desired,.9f,session);
                fish.AddComponent<CatchableFish>().species=species[i%3];AddCatchable(fish);
            }
            // Keep native small-fish particles as ambience, concentrating half the schools near the resource.
            int schools=0;
            foreach(var particles in environment.GetComponentsInChildren<ParticleSystem>().Where(p=>p.name.StartsWith("PS_Fish")))
            {
                if(schools<2) particles.transform.position=ShipOpenWater(scene,focus.position+new Vector3((schools*2-1)*5,5,3),.5f,session);
                schools++;
            }
            var turtle=environment.GetComponentsInChildren<Animator>().FirstOrDefault(a=>a.name=="SM_Turtle");
            if(turtle)
            {
                var pivot=new GameObject("沉船海龟 · 矿区附近巡游").transform;pivot.SetParent(root);pivot.position=turtle.transform.position;
                turtle.transform.SetParent(pivot,true);turtle.applyRootMotion=false;
                var swim=pivot.gameObject.AddComponent<MineralFaunaSwimmer>();swim.attraction=focus;swim.preferredRadius=13;swim.maximumRadius=25;
                swim.speed=.85f;swim.avoidanceRadius=.8f;swim.yLimits=new Vector2(3,session.seaLevel-5);
            }
            report.AppendLine("Catchable animated fish=27 (10-80kg each); native particle schools="+schools+"; turtle preserved="+(turtle!=null));
        }

        static void SunkenShipLayout()
        {
            Directory.CreateDirectory(ShipWork);
            var previous=SceneManager.GetActiveScene();
            var scene=EditorSceneManager.OpenScene(ShipSource,OpenSceneMode.Additive);
            var report=new StringBuilder("Source layout for fifth-map authoring. No Play mode.\n");
            try
            {
                foreach(var root in scene.GetRootGameObjects())
                {
                    report.AppendLine("ROOT "+root.name+" pos="+root.transform.position+" scale="+root.transform.localScale+" bounds="+AdditionalOceanScenes.BoundsOf(root));
                    foreach(var t in root.GetComponentsInChildren<Transform>(true).Where(t=>t.parent==root.transform||t.name.StartsWith("SM_Hull")||t.name.StartsWith("SM_Ground")||t.name.StartsWith("SM_Floor_Main")||t.name.StartsWith("SM_Rock")))
                        report.AppendLine("  OBJECT "+t.name+" pos="+t.position+" bounds="+AdditionalOceanScenes.BoundsOf(t.gameObject));
                    foreach(var cam in root.GetComponentsInChildren<Camera>(true))
                        report.AppendLine("  CAMERA "+cam.name+" pos="+cam.transform.position+" rot="+cam.transform.eulerAngles+" active="+cam.gameObject.activeInHierarchy);
                }
                var roots=scene.GetRootGameObjects();
                report.AppendLine("COLLIDERS "+roots.Sum(r=>r.GetComponentsInChildren<Collider>(true).Length));
                foreach(var mono in roots.SelectMany(r=>r.GetComponentsInChildren<MonoBehaviour>(true)))
                    report.AppendLine("MONO "+(mono?mono.GetType().FullName+" @"+mono.name:"missing"));
                foreach(var m in roots.SelectMany(r=>r.GetComponentsInChildren<Renderer>(true)).SelectMany(r=>r.sharedMaterials).Where(m=>m).Distinct())
                    report.AppendLine("MATERIAL "+m.name+" shader="+m.shader.name+" supported="+m.shader.isSupported);
                SceneManager.SetActiveScene(scene);
                report.AppendLine("RENDER fog="+RenderSettings.fog+" density="+RenderSettings.fogDensity+" color="+RenderSettings.fogColor+" ambient="+RenderSettings.ambientLight);
                File.WriteAllText(ShipWork+"/source-layout.txt",report.ToString());
            }
            finally {EditorSceneManager.CloseScene(scene,true);SceneManager.SetActiveScene(previous);}
        }
    }
}
