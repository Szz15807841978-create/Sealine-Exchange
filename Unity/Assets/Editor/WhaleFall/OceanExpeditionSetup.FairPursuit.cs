using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using WhaleFall;

namespace WhaleFallEditor
{
    public static partial class OceanExpeditionSetup
    {
        [MenuItem("鲸落/海底探索/配置遮挡脱战与重新确认目标（不试玩）")]
        public static void ConfigureFairPursuit()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||Enumerable.Range(0,SceneManager.sceneCount).Any(i=>SceneManager.GetSceneAt(i).isDirty))
                throw new InvalidOperationException("Stop Play mode and save scene changes before updating pursuit.");
            var report=new StringBuilder("Pursuit configuration only. No Play-mode / gameplay validation run.\n");
            foreach(string path in new[]{Underwater3DPrototypeBuilder.Destination,Third})
            {
                string backup=Work+"/"+Path.GetFileNameWithoutExtension(path)+"-before-fair-pursuit.unity";
                if(!File.Exists(backup)) File.Copy(path,backup);
                var scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Single);
                var shark=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<SharkBossAI>()).Single();
                shark.loseSightSeconds=1.5f;shark.searchSeconds=5;shark.trackingExposure=.55f;shark.reacquireSeconds=.65f;
                shark.chargeLostSightSeconds=.4f;shark.memoryChaseSpeed=.55f;shark.searchRadius=4.5f;shark.noisePositionError=3;
                EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
                report.AppendLine("SAVED "+path+"; lose sight=1.5s; charge loss grace=0.4s; reacquire=4/7 samples for 0.65s; search=5s; sound location error=3m (occluded 4.5m)");
            }
            var first=EditorSceneManager.OpenScene(Underwater3DPrototypeBuilder.Destination,OpenSceneMode.Single);
            Selection.activeGameObject=first.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<SharkBossAI>()).Single().gameObject;
            File.WriteAllText(Work+"/fair-pursuit-setup.txt",report.ToString());
        }
    }
}
