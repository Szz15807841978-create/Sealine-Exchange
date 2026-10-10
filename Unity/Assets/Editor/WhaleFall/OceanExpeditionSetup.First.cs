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
        static void FirstLayout()
        {
            var scene=SceneManager.GetActiveScene();
            if(scene.path!=Underwater3DPrototypeBuilder.Destination) throw new InvalidOperationException("Expected first map.");
            var floor=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<MeshCollider>()).Single(c=>c.name=="Terrain");
            var node=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<BioMineralNode>()).Single();
            var boss=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<SharkBossAI>()).Single();
            var bounds=AdditionalOceanScenes.BoundsOf(node.stages[0]);
            var report=new StringBuilder("Terrain placement data, mineral="+bounds+"\n");
            for(int z=22;z<=46;z+=2) for(int x=-14;x<=14;x+=2)
            {
                float low=float.PositiveInfinity,high=float.NegativeInfinity;bool valid=true;
                for(int a=-1;a<=1;a++) for(int b=-1;b<=1;b++)
                {
                    if(!FirstFloor(floor,x+a*bounds.extents.x*.9f,z+b*bounds.extents.z*.9f,out var hit)) {valid=false;continue;}
                    low=Mathf.Min(low,hit.point.y);high=Mathf.Max(high,hit.point.y);
                }
                if(!valid) continue;
                var obstacles=Physics.OverlapBox(new Vector3(x,high-.15f+bounds.extents.y,z),bounds.extents,Quaternion.identity,~0,QueryTriggerInteraction.Ignore)
                    .Where(c=>c.gameObject.scene==scene&&c!=floor&&!c.transform.IsChildOf(node.transform)&&!c.transform.IsChildOf(boss.transform)
                    &&c.bounds.size.y>1.5f&&Mathf.Max(c.bounds.size.x,c.bounds.size.z)>2).ToArray();
                report.AppendLine("x="+x+" z="+z+" low="+low+" high="+high+" delta="+(high-low)+" blockers="+string.Join(";",obstacles.Select(c=>c.name+" "+c.bounds)));
            }
            File.WriteAllText(Work+"/first-terrain-layout.txt",report.ToString());
        }

        [MenuItem("鲸落/海底探索/将生物矿玩法配置到第一图（不启动试玩）")]
        public static void ConfigureFirst()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||Enumerable.Range(0,SceneManager.sceneCount).Any(i=>SceneManager.GetSceneAt(i).isDirty))
                throw new InvalidOperationException("Stop Play mode and save scene changes before setup.");
            const string first=Underwater3DPrototypeBuilder.Destination;
            Directory.CreateDirectory(Work);
            string backup=Work+"/UnderwaterExplore3D-before-gameplay.unity";
            if(!File.Exists(backup)) File.Copy(first,backup);
            var scene=EditorSceneManager.OpenScene(first,OpenSceneMode.Single);
            var diver=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Underwater3DDiver>()).Single();
            if(diver.GetComponent<DiveExpedition>())
            {
                File.WriteAllText(Work+"/first-setup.txt","First scene already configured; preserved all Inspector tuning. No Play-mode test run.");
                Selection.activeGameObject=diver.gameObject;return;
            }
            var floor=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<MeshCollider>()).Single(c=>c.name=="Terrain");
            var report=new StringBuilder("First-map scene construction only. No Play-mode / gameplay validation run.\n");
            DiveExpedition session=null;
            BioMineralNode node=null;
            SharkBossAI boss=null;
            var reference=EditorSceneManager.OpenScene(Third,OpenSceneMode.Additive);
            try
            {
                var source=reference.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<DiveExpedition>()).Single();
                SceneManager.SetActiveScene(scene);
                var resource=Object.Instantiate(source.mineral.gameObject);
                SceneManager.MoveGameObjectToScene(resource,scene);resource.name="资源点 · 第一图生物矿（四形态）";
                node=resource.GetComponent<BioMineralNode>();node.progress=0;node.RefreshStage();
                // Clone the user's current settings and model setup, never rewrite the third map.
                session=diver.gameObject.AddComponent<DiveExpedition>();EditorUtility.CopySerialized(source,session);
                session.diver=diver;session.inventory=diver.gameObject.AddComponent<DiveInventory>();session.mineral=node;
                var predator=Object.Instantiate(source.boss.gameObject);
                SceneManager.MoveGameObjectToScene(predator,scene);predator.name="BOSS · 大白鲨 · PA Shark";
                boss=predator.GetComponent<SharkBossAI>();boss.expedition=session;boss.mineral=node;session.boss=boss;
            }
            finally {EditorSceneManager.CloseScene(reference,true);SceneManager.SetActiveScene(scene);}
            FinishFirst(scene,diver,session,node,boss,report);
        }

        // Resume only the new, unsaved setup objects if terrain fitting interrupted initial construction.
        static void ContinueFirst()
        {
            var scene=SceneManager.GetActiveScene();
            if(scene.path!=Underwater3DPrototypeBuilder.Destination||File.Exists(Work+"/first-setup.txt"))
                throw new InvalidOperationException("This command only resumes the pending first-map placement.");
            var session=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<DiveExpedition>()).Single();
            if(!session.mineral||session.mineral.name!="资源点 · 第一图生物矿（四形态）"||!session.boss)
                throw new InvalidOperationException("Pending first-map objects not found.");
            FinishFirst(scene,session.diver,session,session.mineral,session.boss,new StringBuilder("First-map scene construction only. No Play-mode / gameplay validation run.\n"));
        }

        static void FinishFirst(Scene scene,Underwater3DDiver diver,DiveExpedition session,BioMineralNode node,SharkBossAI boss,StringBuilder report)
        {
            var floor=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<MeshCollider>()).Single(c=>c.name=="Terrain");
            // The native reef corridor is narrower than map 3: retain shape ratios at a six-metre footprint.
            var sourceBounds=AdditionalOceanScenes.BoundsOf(node.stages[0]);
            node.transform.localScale*=6/Mathf.Max(sourceBounds.size.x,sourceBounds.size.z,.01f);
            Physics.SyncTransforms();
            var mineralBounds=AdditionalOceanScenes.BoundsOf(node.stages[0]);
            Vector3 anchor=FirstMineralSite(scene,floor,mineralBounds,node,boss,report);
            node.transform.position+=anchor-new Vector3(mineralBounds.center.x,mineralBounds.min.y,mineralBounds.center.z);
            // The original first map reaches y=7.5; give it a matching visible surface and enough headroom.
            session.seaLevel=8;diver.yLimits=new Vector2(diver.yLimits.x,9.5f);
            diver.allowReset=false;diver.showPrototypeHud=false;diver.testDrive=false;
            var water=scene.GetRootGameObjects().FirstOrDefault(r=>r.name=="第一图水面 · 上浮撤离高度 8m")??GameObject.CreatePrimitive(PrimitiveType.Plane);water.name="第一图水面 · 上浮撤离高度 8m";
            if(water.GetComponent<Collider>()) Object.DestroyImmediate(water.GetComponent<Collider>());water.transform.position=new Vector3(0,session.seaLevel,35);
            water.transform.localScale=new Vector3(20,1,20);
            water.GetComponent<Renderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/WhaleFall/AdditionalOceans/OceanSurface.mat");
            Physics.SyncTransforms();
            if(!scene.GetRootGameObjects().Any(r=>r.name=="第一图矿区 · 可遮挡视线的礁石")) AddFirstCover(scene,floor,node,report);
            session.chestPrefab=BuildChestPrefab();session.chestSites=FirstChestSites(scene,floor,node,diver);
            boss.depthLimits=new Vector2(-31,6);boss.patrolRadius=10;boss.excursionRadius=17;boss.leashRadius=34;
            boss.transform.position=FirstOpenWater(scene,floor,node.Center+new Vector3(10,5,4),1.9f,diver);
            boss.transform.rotation=Quaternion.LookRotation(node.Center+Vector3.up*boss.patrolHeight-boss.transform.position);
            RetargetFirstFauna(scene,floor,node,diver,report);
            var selector=diver.GetComponent<OceanSceneSelector>()??diver.gameObject.AddComponent<OceanSceneSelector>();
            selector.sceneTitle="原始海底 · 生物矿搜打撤";
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            Selection.activeGameObject=boss.gameObject;
            if(SceneView.lastActiveSceneView) SceneView.lastActiveSceneView.LookAt(node.Center,Quaternion.Euler(25,25,0),25);
            report.AppendLine("SAVED "+scene.path+"; mineral="+node.Center+"; shark="+boss.transform.position+"; mineral footprint="+mineralBounds.size);
            report.AppendLine("Chest candidates="+session.chestSites.Length+"; oxygen="+session.maximumOxygen+"; seaLevel="+session.seaLevel+"; spawn preserved="+diver.spawn);
            report.AppendLine("Third map and other maps preserved. Original first-map backup: "+Work+"/UnderwaterExplore3D-before-gameplay.unity");
            File.WriteAllText(Work+"/first-setup.txt",report.ToString());
        }

        static bool FirstFloor(Collider floor,float x,float z,out RaycastHit hit)
        {return floor.Raycast(new Ray(new Vector3(x,30,z),Vector3.down),out hit,100);}

        static Vector3 FirstMineralSite(Scene scene,Collider floor,Bounds bounds,BioMineralNode node,SharkBossAI boss,StringBuilder report)
        {
            float best=float.PositiveInfinity;Vector3 chosen=Vector3.zero;float chosenSlope=0;
            for(int z=26;z<=40;z++) for(int x=-10;x<=10;x++)
            {
                float low=float.PositiveInfinity,high=float.NegativeInfinity;bool valid=true;
                for(int a=-1;a<=1;a++) for(int b=-1;b<=1;b++)
                {
                    if(!FirstFloor(floor,x+a*bounds.extents.x*.9f,z+b*bounds.extents.z*.9f,out var hit)) {valid=false;continue;}
                    low=Mathf.Min(low,hit.point.y);high=Mathf.Max(high,hit.point.y);
                }
                if(!valid||high-low>2.4f) continue;
                Vector3 point=new Vector3(x,high-.35f,z);
                var obstacles=Physics.OverlapBox(point+Vector3.up*(bounds.extents.y+.2f),bounds.extents,Quaternion.identity,~0,QueryTriggerInteraction.Ignore);
                if(obstacles.Any(c=>c.gameObject.scene==scene&&c!=floor&&!c.transform.IsChildOf(node.transform)&&!c.transform.IsChildOf(boss.transform)
                    &&c.bounds.size.y>1.5f&&Mathf.Max(c.bounds.size.x,c.bounds.size.z)>2)) continue;
                float cost=(high-low)*3+Mathf.Abs(x)*.1f+Mathf.Abs(z-31)*.13f;
                if(cost<best) {best=cost;chosen=point;chosenSlope=high-low;}
            }
            if(float.IsPositiveInfinity(best)) throw new InvalidOperationException("No clear middle seabed patch for the existing mineral footprint; original first map remains backed up.");
            report.AppendLine("Mineral seabed anchor="+chosen+"; sampled footprint height variation="+chosenSlope);return chosen;
        }

        static void AddFirstCover(Scene scene,Collider floor,BioMineralNode node,StringBuilder report)
        {
            var root=new GameObject("第一图矿区 · 可遮挡视线的礁石").transform;
            string[] names={"SM_Rock_A","SM_Rock_B","SM_Rock_C"};int count=0;
            for(int i=0;i<8;i++)
            {
                float angle=i*Mathf.PI/4;float radius=i%2==0?9:12;
                Vector3 point=node.Center+new Vector3(Mathf.Cos(angle)*radius,0,Mathf.Sin(angle)*radius);
                if(!FirstFloor(floor,point.x,point.z,out var hit)) continue;point.y=hit.point.y-.25f;
                if(Physics.OverlapSphere(point+Vector3.up*1.8f,1.9f,~0,QueryTriggerInteraction.Ignore)
                    .Any(c=>c.gameObject.scene==scene&&c!=floor&&!c.transform.IsChildOf(node.transform)&&c.bounds.size.y>1.5f)) continue;
                string path="Assets/Davis3D/OceanEnvironmentPack/Prefabs/"+names[i%3]+".prefab";
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if(!prefab) throw new FileNotFoundException(path);
                var rock=(GameObject)PrefabUtility.InstantiatePrefab(prefab,root);rock.name="矿区掩体 "+(++count);
                FitVisual(rock,4.5f+i%3*.6f,true);rock.transform.Rotate(0,i*53,0);rock.transform.position+=point;
                foreach(var mesh in rock.GetComponentsInChildren<MeshFilter>()) if(mesh.sharedMesh&&!mesh.GetComponent<Collider>()) mesh.gameObject.AddComponent<MeshCollider>().sharedMesh=mesh.sharedMesh;
                Physics.SyncTransforms();
            }
            report.AppendLine("Added native first-map rock cover="+count+"; all original reef objects preserved.");
        }

        static Vector3[] FirstChestSites(Scene scene,Collider floor,BioMineralNode node,Underwater3DDiver diver)
        {
            var sites=new List<Vector3>();Physics.SyncTransforms();
            for(int z=6;z<=72;z++) for(int x=-16;x<=16;x++)
            {
                if(!FirstLootSurface(scene,floor,x,z,out var hit)||hit.normal.y<.75f||hit.point.y<diver.yLimits.x-.5f||hit.point.y>6) continue;
                Vector3 point=hit.point+Vector3.up*.05f;float low=point.y,high=point.y;bool valid=true;
                foreach(var corner in new[]{new Vector2(-1,-1),new Vector2(-1,1),new Vector2(1,-1),new Vector2(1,1)})
                {
                    if(!FirstLootSurface(scene,floor,x+corner.x,z+corner.y,out var edge)) {valid=false;break;}
                    low=Mathf.Min(low,edge.point.y);high=Mathf.Max(high,edge.point.y);
                }
                if(!valid||high-low>.8f) continue;point.y=high+.02f;
                if(Vector3.Distance(point,node.Center)<8||Vector3.Distance(point,diver.spawn)<4) continue;
                // Square footprint covers every randomized chest yaw, not just the editor orientation.
                if(Physics.OverlapBox(point+Vector3.up*.48f,new Vector3(1.06f,.42f,1.06f),Quaternion.identity,~0,QueryTriggerInteraction.Ignore)
                    .Any(c=>c.gameObject.scene==scene&&c!=floor)) continue;
                sites.Add(point);
            }
            // Retain a spread-out subset, so fallback random picks still cannot overlap on one small shelf.
            var spread=new List<Vector3>();
            while(sites.Count>0&&spread.Count<24)
            {
                var next=spread.Count==0?sites[0]:sites.OrderByDescending(p=>spread.Min(s=>(s-p).sqrMagnitude)).First();
                spread.Add(next);sites.RemoveAll(p=>Vector3.Distance(p,next)<3.5f);
            }
            if(spread.Count<5) throw new InvalidOperationException("Need at least five separated first-map chest sites; found "+spread.Count);
            return spread.ToArray();
        }

        static void RefineFirstChests()
        {
            var scene=SceneManager.GetActiveScene();
            if(scene.path!=Underwater3DPrototypeBuilder.Destination||scene.isDirty||EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Save and stop the first map before editing chest sites.");
            var session=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<DiveExpedition>()).Single();
            var floor=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<MeshCollider>()).Single(c=>c.name=="Terrain");
            session.chestSites=FirstChestSites(scene,floor,session.mineral,session.diver);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            File.AppendAllText(Work+"/first-setup.txt","Refined separated chest candidates="+session.chestSites.Length+"\n"+string.Join("\n",session.chestSites.Select(p=>p.ToString()))+"\n");
        }

        static bool FirstLootSurface(Scene scene,Collider floor,float x,float z,out RaycastHit hit)
        {
            // On this reef much of the traversable seabed is rock, above the underlying Terrain mesh.
            foreach(var candidate in Physics.RaycastAll(new Vector3(x,20,z),Vector3.down,85,~0,QueryTriggerInteraction.Ignore).OrderBy(h=>h.distance))
            {
                if(candidate.collider.gameObject.scene!=scene) continue;
                if(candidate.collider==floor||candidate.collider.name.StartsWith("SM_Rock_")) {hit=candidate;return true;}
            }
            hit=default(RaycastHit);return false;
        }

        static Vector3 FirstOpenWater(Scene scene,Collider floor,Vector3 desired,float radius,Underwater3DDiver diver)
        {
            for(int i=0;i<80;i++)
            {
                Vector3 point=desired+new Vector3((i%5-2)*1.7f,(i/25)*1.4f,((i/5)%5-2)*1.7f);
                point.x=Mathf.Clamp(point.x,diver.xLimits.x+2,diver.xLimits.y-2);
                point.z=Mathf.Clamp(point.z,diver.zLimits.x+2,diver.zLimits.y-2);
                if(FirstFloor(floor,point.x,point.z,out var hit)) point.y=Mathf.Max(point.y,hit.point.y+radius+1);
                if(point.y>6-radius) continue;
                if(!Physics.OverlapSphere(point,radius,~0,QueryTriggerInteraction.Ignore).Any(c=>c.gameObject.scene==scene)) return point;
            }
            throw new InvalidOperationException("No clear water near "+desired);
        }

        static void RetargetFirstFauna(Scene scene,Collider floor,BioMineralNode node,Underwater3DDiver diver,StringBuilder report)
        {
            var focus=new GameObject("第一图生态吸引中心 · 生物矿").transform;focus.position=node.Center;int index=0;
            foreach(var walker in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<SplineWalker>()).ToArray())
            {
                var fish=walker.gameObject;Object.DestroyImmediate(walker);
                var swim=fish.AddComponent<MineralFaunaSwimmer>();swim.attraction=focus;swim.preferredRadius=7+index%3*2;
                swim.maximumRadius=19;swim.yLimits=new Vector2(-31,4);swim.speed=1.1f+index%4*.18f;swim.avoidanceRadius=.7f;
                float angle=index*2.39996f,radius=5+index%4*3;
                fish.transform.position=FirstOpenWater(scene,floor,node.Center+new Vector3(Mathf.Cos(angle)*radius,5+index%3,Mathf.Sin(angle)*radius),1.3f,diver);
                index++;
            }
            var originalSchools=scene.GetRootGameObjects().FirstOrDefault(r=>r.name=="Fishes");int schools=0;
            if(originalSchools) foreach(var particles in originalSchools.GetComponentsInChildren<ParticleSystem>().Take(3))
            {
                particles.transform.position=FirstOpenWater(scene,floor,node.Center+new Vector3((schools-1)*5,5,schools%2*4),.5f,diver);
                var shape=particles.shape;shape.scale=new Vector3(7,2,7);var main=particles.main;main.maxParticles=110;schools++;
            }
            report.AppendLine("Original moving fauna retargeted="+index+"; particle schools concentrated near mineral="+schools+"; other particle schools and jellyfish preserved.");
        }
    }
}
