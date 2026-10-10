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
    public static class AdditionalOceanVerification
    {
        const string Flag="AdditionalOceans.Verify";
        static readonly string[] Paths={AdditionalOceanScenes.LowScene,AdditionalOceanScenes.DeluxeScene};
        static StringBuilder report;
        static int sceneIndex, waypoint;
        static double started, deadline;
        static bool ready, switching;
        static int runtimeErrors;
        static SplineWalker[] fish;
        static Vector3[] fishStart;
        static bool previousBackground;
        static int startFrame;
        static float startGameTime;
        static AdditionalOceanVerification() { EditorApplication.update+=Tick; }
        [MenuItem("鲸落/海底探索/验证新增两张地图")]
        public static void Begin()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode first.");
            if(Enumerable.Range(0,SceneManager.sceneCount).Any(i=>SceneManager.GetSceneAt(i).isDirty)) throw new InvalidOperationException("Save modified scenes before running scene verification.");
            EditorSceneManager.OpenScene(Paths[0]);
            SessionState.SetBool(Flag,true);
            EditorApplication.isPlaying=true;
        }
        static void Tick()
        {
            if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying||!Application.isPlaying) return;
            if(report==null)
            {
                report=new StringBuilder("ADDITIONAL OCEAN PLAY CHECK\n");sceneIndex=0;waypoint=0;ready=false;switching=false;
                started=EditorApplication.timeSinceStartup;deadline=started+90;runtimeErrors=0;
                previousBackground=Application.runInBackground;Application.runInBackground=true;
                Application.logMessageReceived+=Log;
            }
            if(EditorApplication.timeSinceStartup>deadline) { Finish("FAIL timeout");return; }
            var scene=SceneManager.GetActiveScene();
            if(scene.path!=Paths[sceneIndex]) return;
            var diver=UnityEngine.Object.FindObjectOfType<WhaleFall.Underwater3DDiver>();
            if(!diver) return;
            if(!ready)
            {
                switching=false;ready=true;waypoint=0;started=EditorApplication.timeSinceStartup;deadline=started+75;
                startFrame=Time.frameCount;startGameTime=Time.time;
                diver.testDrive=true;
                var roots=scene.GetRootGameObjects();
                int missing=roots.Sum(r=>r.GetComponentsInChildren<Transform>(true).Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)));
                int bad=roots.SelectMany(r=>r.GetComponentsInChildren<Renderer>(true)).SelectMany(r=>r.sharedMaterials).Count(m=>!m||!m.shader||!m.shader.isSupported||m.shader.name.Contains("Error"));
                int cameras=roots.SelectMany(r=>r.GetComponentsInChildren<Camera>()).Count(c=>c.enabled);
                report.AppendLine(scene.name+": missing scripts="+missing+", invalid materials="+bad+", enabled cameras="+cameras);
                if(missing!=0||bad!=0||cameras!=1||!diver.animator) { Finish("FAIL scene bindings");return; }
                report.AppendLine("PASS diver, materials, scripts and single camera");
                fish=roots.SelectMany(r=>r.GetComponentsInChildren<SplineWalker>()).ToArray();
                int expected=scene.path==AdditionalOceanScenes.LowScene?36:55;
                if(fish.Length!=expected||fish.Any(f=>!f.spline||f.spline.gameObject.scene!=scene||f.duration<=0||f.mode!=SplineWalkerMode.Loop))
                { Finish("FAIL shared fish spline bindings: "+fish.Length+" expected "+expected);return; }
                if(roots.SelectMany(r=>r.GetComponentsInChildren<WhaleFall.OceanLifeMotion>()).Any())
                { Finish("FAIL old fish behaviour remains");return; }
                fishStart=fish.Select(f=>f.transform.position).ToArray();
                var particleRoot=roots.SingleOrDefault(r=>r.name=="地图1粒子鱼群");
                if(!particleRoot||particleRoot.GetComponentsInChildren<ParticleSystem>().Length!=2)
                { Finish("FAIL original particle schools missing");return; }
                report.AppendLine("PASS "+fish.Length+" spline fish and 2 original particle schools");
                AdditionalOceanScenes.Capture(diver.followCamera,scene.name+"-entry");
                Physics.SyncTransforms();
                var overlap=Physics.OverlapSphere(diver.spawn,.65f).Where(c=>!c.isTrigger&&!c.transform.IsChildOf(diver.transform)).ToArray();
                if(overlap.Length>0) { Finish("FAIL spawn intersects "+string.Join(",",overlap.Select(c=>c.name)));return; }
                report.AppendLine("PASS clear spawn");
            }
            var selector=UnityEngine.Object.FindObjectOfType<WhaleFall.OceanSceneSelector>();
            if(!selector||selector.verificationWaypoints==null) { Finish("FAIL route missing");return; }
            if(waypoint<selector.verificationWaypoints.Length)
            {
                var delta=selector.verificationWaypoints[waypoint]-diver.transform.position;
                diver.testWorldMove=delta.normalized;
                if(delta.magnitude<.7f)
                {
                    report.AppendLine("PASS waypoint "+waypoint+" at "+diver.transform.position);
                    waypoint++;started=EditorApplication.timeSinceStartup;
                }
                else if(EditorApplication.timeSinceStartup-started>18) { Finish("FAIL route blocked at "+diver.transform.position+" toward "+selector.verificationWaypoints[waypoint]+"; elapsed frames="+(Time.frameCount-startFrame)+" game seconds="+(Time.time-startGameTime)+" velocity="+diver.Speed+" kinematic="+diver.GetComponent<Rigidbody>().isKinematic);return; }
            }
            else if(!switching)
            {
                diver.testWorldMove=Vector3.zero;
                float distance=Vector3.Distance(diver.transform.position,diver.followCamera.transform.position);
                if(float.IsNaN(distance)||distance>diver.maxCameraDistance+3) { Finish("FAIL camera");return; }
                report.AppendLine("PASS swimming and camera distance="+distance.ToString("F2"));
                int moved=fish.Where((f,i)=>Vector3.Distance(f.transform.position,fishStart[i])>.1f).Count();
                if(moved!=fish.Length) { Finish("FAIL fish movement "+moved+"/"+fish.Length);return; }
                var particleRoot=scene.GetRootGameObjects().Single(r=>r.name=="地图1粒子鱼群");
                int particles=particleRoot.GetComponentsInChildren<ParticleSystem>().Sum(p=>p.particleCount);
                if(particles==0) { Finish("FAIL no live particle fish");return; }
                report.AppendLine("PASS moving fish="+moved+"/"+fish.Length+"; live particle fish="+particles);
                AdditionalOceanScenes.Capture(diver.followCamera,scene.name+"-swim");
                if(sceneIndex+1==Paths.Length) { Finish(runtimeErrors==0?"COMPLETE; runtime errors=0":"FAIL runtime errors="+runtimeErrors);return; }
                sceneIndex++;ready=false;switching=true;deadline=EditorApplication.timeSinceStartup+90;
                SceneManager.LoadSceneAsync(Path.GetFileNameWithoutExtension(Paths[sceneIndex]));
            }
        }
        static void Log(string message,string stack,LogType type)
        { if(type==LogType.Error||type==LogType.Exception) { runtimeErrors++;report?.AppendLine("RUNTIME ERROR "+message); } }
        static void Finish(string result)
        {
            report.AppendLine(result);
            Directory.CreateDirectory(AdditionalOceanScenes.Reports);
            File.WriteAllText(AdditionalOceanScenes.Reports+"/play-check.txt",report.ToString());
            Application.logMessageReceived-=Log;
            Application.runInBackground=previousBackground;
            SessionState.SetBool(Flag,false);report=null;
            EditorApplication.isPlaying=false;
        }
    }
}
