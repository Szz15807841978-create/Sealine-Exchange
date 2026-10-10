using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Fishing;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;

namespace FishingEditor
{
    // Narrow, explicitly requested cleanup. Keeps the inactive objects and every valid component.
    [InitializeOnLoad]
    public static class PlayerUiRepairSetup
    {
        const string RaftPath="Assets/Scenes/Current/DivePointRealisticOceanSky_Unified.unity",MainPath="Assets/Scenes/Main.unity";
        const string Request="tools/PlayerUiRepair.request",Report="tools/PlayerUiRepair-report.txt";
        static bool busy,waiting;
        static PlayerUiRepairSetup() { EditorApplication.update+=Watch; }
        static void Watch()
        {
            if(busy||!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
            var open=SceneManager.GetSceneByPath(RaftPath);
            if(open.IsValid()&&open.isLoaded&&open.isDirty){if(!waiting)File.WriteAllText(Report,"WAITING: save the raft scene; no unsaved edit was overwritten.\n");waiting=true;return;}
            var main=SceneManager.GetSceneByPath(MainPath);
            if(main.IsValid()&&main.isLoaded&&main.isDirty){if(!waiting)File.WriteAllText(Report,"WAITING: save Main; no unsaved edit was overwritten.\n");waiting=true;return;}
            busy=true;
            try{Apply();File.Move(Request,Request+".done-"+DateTime.Now.ToString("yyyyMMdd-HHmmss"));}
            catch(Exception e){File.WriteAllText(Report,"FAILED (no automatic retry)\n"+e);Debug.LogException(e);EditorApplication.update-=Watch;}
            finally{busy=false;}
        }
        [MenuItem("Fishing/玩家版/修复天空面板与已停用水线的失效引用（保留布局）")]
        public static void Apply()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play mode first.");
            var scene=SceneManager.GetSceneByPath(RaftPath);bool opened=!scene.IsValid()||!scene.isLoaded;
            if(!opened&&scene.isDirty)throw new InvalidOperationException("Save the raft scene first; no unsaved edit was discarded.");
            var previous=SceneManager.GetActiveScene();if(opened)scene=EditorSceneManager.OpenScene(RaftPath,OpenSceneMode.Additive);
            try{
                var all=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).ToArray();
                var before=PlayerPaperSkinSetup.Snapshot(scene,new Transform[0]);
                var skies=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<SkyDebugPanel>(true)).ToArray();
                int validateCalls=0;bool dirtyBefore=scene.isDirty;
                foreach(var sky in skies)for(int i=0;i<20;i++){
                    typeof(SkyDebugPanel).GetMethod("OnValidate",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(sky,null);
                    typeof(SkyDebugPanel).GetMethod("Update",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(sky,null);
                    validateCalls++;
                }
                PlayerPaperSkinSetup.AssertSame(before,PlayerPaperSkinSetup.Snapshot(scene,new Transform[0]),"Sky validation/rebind");
                if(scene.isDirty!=dirtyBefore)throw new Exception("Sky validation dirtied the scene.");
                string[] paths={"下水点画布/木筏水线","3D木筏与装置_全部可编辑/RaftRoot/木筏水线"};
                ulong[] ids={379665324,1212353402};int removed=0;
                for(int i=0;i<paths.Length;i++){
                    var target=all.SingleOrDefault(t=>PathOf(t)==paths[i]);if(!target)throw new Exception("Target moved/renamed; cleanup stopped: "+paths[i]);
                    var go=target.gameObject;
                    if(go.activeSelf||GlobalObjectId.GetGlobalObjectIdSlow(go).targetObjectId!=ids[i])throw new Exception("Target identity/state changed; cleanup stopped: "+paths[i]);
                    int missing=GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
                    if(missing>1)throw new Exception("Unexpected additional missing components: "+paths[i]);
                    if(missing==1){Undo.RegisterCompleteObjectUndo(go,"清理已停用水线的失效脚本引用");removed+=GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);}
                }
                PlayerPaperSkinSetup.AssertSame(before,PlayerPaperSkinSetup.Snapshot(scene,new Transform[0]),"Waterline cleanup");
                int remaining=all.Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));
                if(removed>0){EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new IOException("Could not save cleanup.");}
                RepairMainInput();
                File.AppendAllText(Report,"PASS\nRemovedMissingComponentsThisPass="+removed+"\nInactiveWaterlineObjectsKept=True\nAllValidRaftComponentsTransformsAndSkySettingsUnchanged=True\nSkyValidateAndRebindCalls="+validateCalls+"\nSkyValidationDidNotDirtyScene=True\nRemainingMissingComponents="+remaining+"\nMainInputStartsDisabledAndIsActivatedOnlyWhenStandalone=True\nBackupCreated=False\n");
                Debug.Log("[UI Repair] 天空面板重复校验无重建、无脏场景；已清理 "+removed+" 个失效水线脚本引用，物件与设置保留。");
            }finally{if(opened&&scene.IsValid()&&scene.isLoaded)EditorSceneManager.CloseScene(scene,true);if(previous.IsValid()&&previous.isLoaded)SceneManager.SetActiveScene(previous);}
        }
        static void RepairMainInput()
        {
            var scene=SceneManager.GetSceneByPath(MainPath);bool opened=!scene.IsValid()||!scene.isLoaded;
            if(!opened&&scene.isDirty)throw new InvalidOperationException("Save Main first.");
            if(opened)scene=EditorSceneManager.OpenScene(MainPath,OpenSceneMode.Additive);
            try{
                var input=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<EventSystem>(true)).Single();
                var before=PlayerPaperSkinSetup.Snapshot(scene,new Transform[0]);bool changed=input.enabled;
                if(changed){before[input.GetInstanceID()]=before[input.GetInstanceID()].Replace("\"m_Enabled\":true","\"m_Enabled\":false");input.enabled=false;}
                PlayerPaperSkinSetup.AssertSame(before,PlayerPaperSkinSetup.Snapshot(scene,new Transform[0]),"Main input guard (only EventSystem.enabled allowed)");
                if(changed){EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new IOException("Could not save Main input guard.");}
            }finally{if(opened&&scene.IsValid()&&scene.isLoaded)EditorSceneManager.CloseScene(scene,true);}
        }
        static string PathOf(Transform t) { return t.parent?PathOf(t.parent)+"/"+t.name:t.name; }
    }
}
