using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WhaleFallEditor
{
    public static partial class AdditionalOceanScenes
    {
        static void ProbeScaleAndFish()
        {
            var report=new StringBuilder();
            var previous=SceneManager.GetActiveScene();
            foreach(string path in new[]{Underwater3DPrototypeBuilder.Destination,LowScene})
            {
                var scene=SceneManager.GetSceneByPath(path);
                bool opened=!scene.IsValid()||!scene.isLoaded;
                if(opened) scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
                try
                {
                    report.AppendLine(path);
                    var roots=scene.GetRootGameObjects();
                    foreach(var root in roots)
                    {
                        report.AppendLine("ROOT "+root.name+" pos="+root.transform.position+" scale="+root.transform.localScale);
                        foreach(var walker in root.GetComponentsInChildren<SplineWalker>(true))
                            report.AppendLine("WALKER "+walker.name+" parent="+walker.transform.parent?.name+" spline="+walker.spline?.name+" duration="+walker.duration+" mode="+walker.mode+" random="+walker.randomStart+" bounds="+BoundsOf(walker.gameObject));
                        foreach(var spline in root.GetComponentsInChildren<BezierSpline>(true))
                        {
                            var bounds=new Bounds(spline.GetPoint(0),Vector3.zero);
                            for(int i=1;i<=50;i++) bounds.Encapsulate(spline.GetPoint(i/50f));
                            report.AppendLine("SPLINE "+spline.name+" points="+spline.ControlPointCount+" loop="+spline.Loop+" "+bounds);
                        }
                        foreach(var particles in root.GetComponentsInChildren<ParticleSystem>(true).Where(p=>p.name.IndexOf("tang",StringComparison.OrdinalIgnoreCase)>=0||p.name.IndexOf("medaka",StringComparison.OrdinalIgnoreCase)>=0))
                            report.AppendLine("FISH PARTICLES "+particles.name+" max="+particles.main.maxParticles+" speed="+particles.main.startSpeed.constant+" simulation="+particles.main.simulationSpace);
                        foreach(var t in root.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="SunkenShip"||t.name=="SunkenShipFront"||t.name=="FullShip"))
                            report.AppendLine("SHIP "+t.name+" scale="+t.lossyScale+" bounds="+BoundsOf(t.gameObject));
                        foreach(var diver in root.GetComponentsInChildren<WhaleFall.Underwater3DDiver>())
                            report.AppendLine("DIVER "+BoundsOf(diver.visual.gameObject));
                    }
                }
                finally { if(opened) EditorSceneManager.CloseScene(scene,true); }
            }
            if(previous.IsValid()&&previous.isLoaded) SceneManager.SetActiveScene(previous);
            File.WriteAllText(Reports+"/scale-fish-probe.txt",report.ToString());
        }
        [MenuItem("鲸落/海底探索/放大沉船礁并迁移原图鱼群")]
        public static void UpgradeScaleAndFish()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode first.");
            if(Enumerable.Range(0,SceneManager.sceneCount).Any(i=>SceneManager.GetSceneAt(i).isDirty)) throw new InvalidOperationException("Save modified scenes before upgrading.");
            var report=new StringBuilder("OCEAN SCALE AND SHARED FISH BEHAVIOUR\n");
            var source=EditorSceneManager.OpenScene(Underwater3DPrototypeBuilder.Destination);
            var curves=source.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<BezierSpline>()).OrderBy(s=>s.name).ToArray();
            var patterns=curves.Select(s=>Enumerable.Range(0,s.ControlPointCount).Select(i=>s.transform.TransformPoint(s.GetControlPoint(i))).ToArray()).ToArray();
            report.AppendLine("Source: "+Underwater3DPrototypeBuilder.Destination+"; BezierSpline/SplineWalker + PS_Bluetang/PS_Medaka");
            foreach(string path in new[]{LowScene,DeluxeScene})
            {
                var scene=EditorSceneManager.OpenScene(path);
                report.AppendLine(path);
                var roots=scene.GetRootGameObjects();
                var diver=roots.SelectMany(r=>r.GetComponentsInChildren<WhaleFall.Underwater3DDiver>()).Single();
                if(path==LowScene)
                {
                    var environment=roots.Single(r=>r.name=="低多边形海底 · 原版完整布景").transform;
                    float factor=.4f/environment.localScale.x;
                    report.AppendLine("Environment scale: "+environment.localScale.x+" -> 0.4 (relative factor="+factor+")");
                    environment.localScale*=factor;environment.position*=factor;
                    var water=roots.Single(r=>r.name=="水面 · 双面波光").transform;
                    water.position*=factor;water.localScale=new Vector3(water.localScale.x*factor,water.localScale.y,water.localScale.z*factor);
                    // Small creatures remain life-sized while their habitat moves with the enlarged terrain.
                    var originalFish=environment.Find("Fish");
                    if(originalFish)
                    {
                        foreach(var t in originalFish.GetComponentsInChildren<Transform>(true).Where(t=>t.GetComponent<Renderer>()||t.GetComponent<LODGroup>()).ToArray())
                        {
                            if(t.parent&&t.parent.GetComponentsInParent<Renderer>().Length==0&&t.GetComponent<LODGroup>()) t.localScale/=factor;
                        }
                    }
                    diver.spawn*=factor;diver.transform.position=diver.spawn;
                    diver.xLimits*=factor;diver.yLimits*=factor;diver.zLimits*=factor;
                    var selector=diver.GetComponent<WhaleFall.OceanSceneSelector>();
                    for(int i=0;i<selector.verificationWaypoints.Length;i++) selector.verificationWaypoints[i]*=factor;
                    foreach(var motion in roots.SelectMany(r=>r.GetComponentsInChildren<WhaleFall.OceanLifeMotion>()))
                    { motion.center*=factor;motion.radius*=factor;motion.transform.position*=factor; }
                    RenderSettings.fogDensity/=(factor>1?factor:1);
                    diver.followCamera.farClipPlane=350;diver.SnapCamera();
                    var ship=environment.GetComponentsInChildren<Transform>(true).First(t=>t.name=="SunkenShip");
                    var shipBounds=BoundsOf(ship.gameObject);var diverBounds=BoundsOf(diver.visual.gameObject);
                    report.AppendLine("Main wreck world AABB length="+Mathf.Max(shipBounds.size.x,shipBounds.size.z).ToString("F2")+"; diver height="+diverBounds.size.y.ToString("F2")+"; ratio="+(Mathf.Max(shipBounds.size.x,shipBounds.size.z)/diverBounds.size.y).ToString("F2"));
                    report.AppendLine("Play bounds X="+diver.xLimits+" Y="+diver.yLimits+" Z="+diver.zLimits);
                }
                Physics.SyncTransforms();
                var motionGroups=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<WhaleFall.OceanLifeMotion>()).GroupBy(m=>m.transform.parent).ToArray();
                int schoolIndex=0;
                foreach(var group in motionGroups)
                {
                    var fishes=group.ToArray();
                    var center=fishes.Aggregate(Vector3.zero,(v,m)=>v+m.center)/fishes.Length;
                    var radii=fishes[0].radius;
                    bool shark=fishes.Length==1;
                    var route=CreateSafeRoute(scene,group.Key,patterns[(schoolIndex+1)%patterns.Length],center,radii,shark?2.7f:1.05f,diver.yLimits.y,report);
                    float length=RouteLength(route);
                    for(int i=0;i<fishes.Length;i++)
                    {
                        var old=fishes[i];var target=old.gameObject;
                        var walker=target.GetComponent<SplineWalker>()??target.AddComponent<SplineWalker>();
                        walker.spline=route;walker.duration=length/(shark?1.7f:1.05f+(i%3)*.035f);
                        walker.mode=SplineWalkerMode.Loop;walker.lookForward=true;walker.randomStart=false;
                        walker.initialProgress=(i*.027f+schoolIndex*.17f)%1;
                        walker.pathOffset=shark?Vector3.zero:new Vector3((i%3-1)*.32f,(i%4-1.5f)*.19f,(i%5-2)*.17f);
                        target.transform.position=route.GetPoint(walker.initialProgress)+walker.pathOffset;
                        target.transform.rotation=Quaternion.LookRotation(route.GetDirection(walker.initialProgress),Vector3.up);
                        UnityEngine.Object.DestroyImmediate(old);
                    }
                    report.AppendLine("Migrated "+group.Key.name+": "+fishes.Length+" fish; curve="+route.name+" length="+length.ToString("F1"));schoolIndex++;
                }
                if(!scene.GetRootGameObjects().Any(r=>r.name=="地图1粒子鱼群"))
                {
                    var particleRoot=new GameObject("地图1粒子鱼群").transform;
                    var positions=path==LowScene?new[]{new Vector3(6,-9,45),new Vector3(28,-10,34)}:new[]{new Vector3(-3,-6,13),new Vector3(4,-9,34)};
                    for(int i=0;i<2;i++)
                    {
                        string asset="Assets/Davis3D/OceanEnvironmentPack/Prefabs/"+(i==0?"PS_Bluetang":"PS_Medaka")+".prefab";
                        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(asset);
                        var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab,particleRoot);
                        go.name=i==0?"原图蓝吊鱼群":"原图小鱼群";go.transform.position=positions[i];
                        var ps=go.GetComponent<ParticleSystem>();
                        var main=ps.main;main.maxParticles=100;main.prewarm=true;
                        var emission=ps.emission;emission.rateOverTime=5;
                        var shape=ps.shape;shape.scale=new Vector3(5,2,5);
                        report.AppendLine("Copied particle school "+go.name+"; original noise="+ps.noise.enabled+"; velocity="+ps.velocityOverLifetime.enabled);
                    }
                }
                foreach(var root in scene.GetRootGameObjects())
                {
                    foreach(var renderer in root.GetComponentsInChildren<Renderer>(true)) RepairParticle(renderer);
                    foreach(var lod in root.GetComponentsInChildren<LODGroup>(true)) lod.fadeMode=LODFadeMode.None;
                    foreach(var light in root.GetComponentsInChildren<Light>()) if(light.type==LightType.Directional) light.shadowStrength=.5f;
                }
                Physics.SyncTransforms();
                EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
                Capture(diver.followCamera,System.IO.Path.GetFileNameWithoutExtension(path)+"-upgraded");
            }
            var surface=AssetDatabase.LoadAssetAtPath<Material>(Art+"/OceanSurface.mat");surface.color=new Color(.08f,.35f,.42f);EditorUtility.SetDirty(surface);
            AssetDatabase.SaveAssets();report.AppendLine("COMPLETE");File.WriteAllText(Reports+"/scale-fish-upgrade.txt",report.ToString());
            Audit();
            OpenLow();
        }

        static BezierSpline CreateSafeRoute(Scene scene,Transform parent,Vector3[] template,Vector3 center,Vector2 radii,float clearance,float ceiling,StringBuilder report)
        {
            var go=new GameObject("巡游路线 · 复用地图1曲线");go.transform.SetParent(parent,false);
            var spline=go.AddComponent<BezierSpline>();
            var bounds=new Bounds(template[0],Vector3.zero);foreach(var p in template.Skip(1)) bounds.Encapsulate(p);
            var offsets=template.Select(p=>new Vector3((p.x-bounds.center.x)/Mathf.Max(.01f,bounds.extents.x),(p.y-bounds.center.y)/Mathf.Max(.01f,bounds.extents.y),(p.z-bounds.center.z)/Mathf.Max(.01f,bounds.extents.z))).ToArray();
            bool found=false;
            for(int attempt=0;attempt<45;attempt++)
            {
                float shrink=Mathf.Pow(.82f,attempt/9);
                Vector3 candidate=center+new Vector3((attempt%3-1)*3f,((attempt/3)%3)*1.5f,0);
                candidate.y=Mathf.Min(candidate.y,ceiling-clearance-1f);
                var points=offsets.Select(p=>candidate+Vector3.Scale(p,new Vector3(radii.x*shrink,.7f,radii.y*shrink))).ToArray();
                SetCurvePoints(spline,points);
                found=true;
                Vector3 last=spline.GetPoint(0);
                for(int j=0;j<=80&&found;j++)
                {
                    var point=spline.GetPoint(j/80f);
                    if(Physics.OverlapSphere(point,clearance,~0,QueryTriggerInteraction.Ignore).Any(c=>c.gameObject.scene==scene&&!c.GetComponent<WhaleFall.Underwater3DDiver>())) found=false;
                    var delta=point-last;
                    if(delta.magnitude>.001f&&Physics.SphereCastAll(last,clearance,delta.normalized,delta.magnitude,~0,QueryTriggerInteraction.Ignore).Any(h=>h.collider.gameObject.scene==scene&&!h.collider.GetComponent<WhaleFall.Underwater3DDiver>())) found=false;
                    last=point;
                }
                if(found) { report.AppendLine("PASS route clearance "+clearance+" center="+candidate+" radius="+radii*shrink);break; }
            }
            if(!found) throw new InvalidOperationException("Could not fit a collision-free fish route near "+center);
            return spline;
        }
        static void SetCurvePoints(BezierSpline spline,Vector3[] points)
        {
            var serialized=new SerializedObject(spline);var array=serialized.FindProperty("points");array.arraySize=points.Length;
            for(int i=0;i<points.Length;i++) array.GetArrayElementAtIndex(i).vector3Value=spline.transform.InverseTransformPoint(points[i]);
            var modes=serialized.FindProperty("modes");modes.arraySize=(points.Length-1)/3+1;
            for(int i=0;i<modes.arraySize;i++) modes.GetArrayElementAtIndex(i).enumValueIndex=0;
            serialized.FindProperty("loop").boolValue=true;serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        static float RouteLength(BezierSpline spline)
        {
            float result=0;var previous=spline.GetPoint(0);
            for(int i=1;i<=100;i++) { var p=spline.GetPoint(i/100f);result+=Vector3.Distance(p,previous);previous=p; }return result;
        }
    }
}
