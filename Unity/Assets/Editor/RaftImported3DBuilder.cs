using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Fishing;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace FishingEditor
{
    [InitializeOnLoad]
    public static class RaftImported3DBuilder
    {
        const string Request="Assets/Editor/RaftImported3D.request";
        const string SourceScene="Assets/Scenes/DivePointWeather.unity";
        const string ScenePath="Assets/Scenes/DivePointImported3D.unity";
        const string Output="Assets/Art/Integrated3D";
        const int ModelLayer=30;
        static RaftImported3DBuilder(){EditorApplication.delayCall+=RunRequest;}
        static void RunRequest()
        {
            if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isPlayingOrWillChangePlaymode)return;
            try {if(File.Exists(ScenePath))RepairAndRender();else Build();AssetDatabase.DeleteAsset(Request);}
            catch(Exception e){Debug.LogException(e);}
        }

        static void RepairAndRender()
        {
            var loaded=SceneManager.GetSceneByPath(ScenePath);
            if(loaded.IsValid()&&loaded.isLoaded)throw new InvalidOperationException("请先保存并关闭导入模型3D场景，以保护手动调整。");
            var previous=SceneManager.GetActiveScene();var scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Additive);
            try
            {
                var roots=scene.GetRootGameObjects();
                var c=roots.SelectMany(x=>x.GetComponentsInChildren<DivePointController>(true)).Single();
                var canvas=roots.SelectMany(x=>x.GetComponentsInChildren<Canvas>(true)).Single();
                var weather=canvas.GetComponent<RaftWeatherController>();
                var view=roots.SelectMany(x=>x.GetComponentsInChildren<Chest3DView>(true)).Single();
                var camera=roots.SelectMany(x=>x.GetComponentsInChildren<Camera>(true)).Single(x=>x.targetTexture);
                var raw=weather.Raft.Find("3D木筏实时画面").GetComponent<RawImage>();raw.uvRect=new Rect(1,0,-1,1);
                RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.40f,.44f,.49f);
                if(!roots.Any(x=>x.name=="3D木筏补光"))
                {
                    var fill=new GameObject("3D木筏补光").AddComponent<Light>();fill.type=LightType.Directional;fill.intensity=.55f;
                    fill.color=new Color(.65f,.78f,1f);fill.transform.rotation=Quaternion.Euler(25,145,0);
                }
                EditorSceneManager.SaveScene(scene);ValidateAndRender(scene,c,weather,view,camera,canvas,raw);
                Debug.Log("[RaftImported3D] PASS: horizontal alignment repaired, fill light added, interaction rechecked.");
            }
            finally {EditorSceneManager.CloseScene(scene,true);if(previous.IsValid()&&previous.isLoaded)SceneManager.SetActiveScene(previous);}
        }

        [MenuItem("Fishing/下水点/创建导入模型3D木筏场景（保留原版）")]
        public static void Build()
        {
            if(File.Exists(ScenePath))throw new InvalidOperationException("导入模型3D场景已存在；为保护手调内容，不自动覆盖。");
            if(!File.Exists(SourceScene))throw new FileNotFoundException(SourceScene);
            Directory.CreateDirectory(Output);AssetDatabase.Refresh();
            var previous=SceneManager.GetActiveScene();
            if(!AssetDatabase.CopyAsset(SourceScene,ScenePath))throw new Exception("无法复制晴雨场景");
            var scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                var roots=scene.GetRootGameObjects();
                var controller=roots.SelectMany(x=>x.GetComponentsInChildren<DivePointController>(true)).Single();
                var canvas=roots.SelectMany(x=>x.GetComponentsInChildren<Canvas>(true)).Single();
                var weather=canvas.GetComponent<RaftWeatherController>();
                var raftUi=weather.Raft;
                foreach(Transform child in raftUi)
                    if(child.name.StartsWith("木筏_")||child.name.StartsWith("装置_")||child.name=="下水点标杆"||child.name=="下水旗")child.gameObject.SetActive(false);

                var world=new GameObject("3D木筏与装置_全部可编辑");
                var raft=PlaceWidth("Assets/Art/wooden_xylophone_3d_model.prefab",world.transform,"木筏3D_原名wooden_xylophone",Vector3.zero,12.6f);
                raft.transform.position+=Vector3.up*.02f;
                var deckBounds=BoundsOf(raft);float deckY=deckBounds.max.y+.03f;
                var workbench=PlaceHeight("Assets/Art/wooden_workbench_3d_model.prefab",world.transform,"工作台3D",new Vector3(-5.15f,deckY,0),1.48f);
                var rack=PlaceHeight("Assets/Art/platypus_3d_model.prefab",world.transform,"装饰模型3D",new Vector3(-3.65f,deckY,.05f),.72f);
                var chest=PlaceHeight("Assets/Art/treasure_chest_3d_model.prefab",world.transform,"交互木箱3D",new Vector3(-1.70f,deckY,-.03f),1.12f);
                var fireplace=PlaceHeight("Assets/Art/stone_fireplace_3d_model.prefab",world.transform,"石炉3D",new Vector3(.10f,deckY,0),1.25f);
                var pot=PlaceHeight("Assets/Art/cooking_pot_3d_model.prefab",world.transform,"蒸馏锅3D",new Vector3(1.95f,deckY,0),1.52f);
                var barrel=PlaceHeight("Assets/Art/wooden_barrel_3d_model.prefab",world.transform,"木桶3D",new Vector3(3.72f,deckY,0),1.25f);
                var toaster=PlaceHeight("Assets/Art/toaster_3d_model.prefab",world.transform,"加工器3D",new Vector3(5.20f,deckY,0),.88f);
                foreach(var go in new[]{workbench,rack,fireplace,pot,barrel,toaster})SetLayer(go,ModelLayer);

                var lidNames=new[]{"tripo_part_1","tripo_part_7","tripo_part_11","tripo_part_13","tripo_part_14","tripo_part_15"};
                var imported=chest.transform.Cast<Transform>().FirstOrDefault();
                if(imported&&PrefabUtility.IsPartOfPrefabInstance(imported))PrefabUtility.UnpackPrefabInstance(imported.gameObject,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
                var lidParts=chest.GetComponentsInChildren<Renderer>(true).Where(x=>lidNames.Contains(x.name)).ToArray();
                if(lidParts.Length!=lidNames.Length)throw new Exception("箱盖零件识别失败："+lidParts.Length+"/"+lidNames.Length);
                var lidBounds=lidParts[0].bounds;foreach(var r in lidParts.Skip(1))lidBounds.Encapsulate(r.bounds);
                var hinge=new GameObject("箱盖转轴_后侧合页").transform;hinge.SetParent(chest.transform,true);
                hinge.position=new Vector3(lidBounds.center.x,lidBounds.min.y,lidBounds.min.z);
                foreach(var r in lidParts)r.transform.SetParent(hinge,true);
                var chestView=chest.AddComponent<Chest3DView>();chestView.LidHinge=hinge;chestView.AxisSign=-1;chestView.OpenAngle=102;
                var box=chest.AddComponent<BoxCollider>();var cb=BoundsOf(chest);box.center=chest.transform.InverseTransformPoint(cb.center);box.size=cb.size;
                SetLayer(chest,ModelLayer);SetLayer(raft,ModelLayer);

                // Reusable assembly keeps all imported parts editable; the scene adds its own UI reference.
                PrefabUtility.SaveAsPrefabAsset(world,Output+"/RaftAssembly_3D.prefab");
                chestView.ChestPanel=controller.ChestPanel;
                world.transform.position=new Vector3(1000,0,0);

                var render=new RenderTexture(1600,600,24,RenderTextureFormat.ARGB32){name="RaftImported3D_Render",antiAliasing=4};
                AssetDatabase.CreateAsset(render,Output+"/RaftImported3D_Render.renderTexture");
                var camera=new GameObject("3D木筏展示摄像机").AddComponent<Camera>();
                camera.transform.position=new Vector3(1000,6.2f,10.5f);camera.transform.LookAt(new Vector3(1000,.72f,0));
                camera.orthographic=true;camera.orthographicSize=2.62f;camera.nearClipPlane=.1f;camera.farClipPlane=30;
                camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(0,0,0,0);camera.cullingMask=1<<ModelLayer;
                camera.targetTexture=render;camera.depth=-10;camera.allowHDR=false;
                RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.40f,.44f,.49f);
                var light=new GameObject("3D木筏主光").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.1f;
                light.color=new Color(1f,.94f,.82f);light.transform.rotation=Quaternion.Euler(38,-28,0);
                var fill=new GameObject("3D木筏补光").AddComponent<Light>();fill.type=LightType.Directional;fill.intensity=.55f;
                fill.color=new Color(.65f,.78f,1f);fill.transform.rotation=Quaternion.Euler(25,145,0);

                var display=new GameObject("3D木筏实时画面",typeof(RectTransform),typeof(CanvasRenderer),typeof(RawImage));
                display.transform.SetParent(raftUi,false);var rect=(RectTransform)display.transform;
                Bottom(rect,0,8,1460,548);var raw=display.GetComponent<RawImage>();raw.texture=render;raw.uvRect=new Rect(1,0,-1,1);raw.raycastTarget=false;display.transform.SetAsFirstSibling();
                controller.ChestImage.color=Color.clear;controller.ChestButton.targetGraphic=controller.ChestImage;controller.ChestButton.transition=Selectable.Transition.None;controller.ChestImage=null;
                weather.Devices=weather.Devices.Concat(new Graphic[]{raw}).Where(x=>x).Distinct().ToArray();
                foreach(var oldCamera in roots.SelectMany(x=>x.GetComponentsInChildren<Camera>(true)))oldCamera.cullingMask&=~(1<<ModelLayer);

                EditorSceneManager.SaveScene(scene,ScenePath);AssetDatabase.SaveAssets();
                ValidateAndRender(scene,controller,weather,chestView,camera,canvas,raw);
                Debug.Log("[RaftImported3D] PASS: imported raft (wooden_xylophone), imported devices, chest open/close, loot and scene renders.");
            }
            finally {EditorSceneManager.CloseScene(scene,true);if(previous.IsValid()&&previous.isLoaded)SceneManager.SetActiveScene(previous);}
        }

        static GameObject PlaceWidth(string path,Transform parent,string name,Vector3 position,float targetWidth)
        {
            var wrapper=Place(path,parent,name);var b=BoundsOf(wrapper);wrapper.transform.localScale*=targetWidth/b.size.x;b=BoundsOf(wrapper);
            wrapper.transform.position+=new Vector3(position.x-b.center.x,position.y-b.min.y,position.z-b.center.z);return wrapper;
        }
        static GameObject PlaceHeight(string path,Transform parent,string name,Vector3 position,float targetHeight)
        {
            var wrapper=Place(path,parent,name);var b=BoundsOf(wrapper);wrapper.transform.localScale*=targetHeight/b.size.y;b=BoundsOf(wrapper);
            wrapper.transform.position+=new Vector3(position.x-b.center.x,position.y-b.min.y,position.z-b.center.z);return wrapper;
        }
        static GameObject Place(string path,Transform parent,string name)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(!prefab)throw new FileNotFoundException(path);
            var wrapper=new GameObject(name);wrapper.transform.SetParent(parent,false);
            var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab);instance.transform.SetParent(wrapper.transform,false);return wrapper;
        }
        static Bounds BoundsOf(GameObject go)
        {
            var rs=go.GetComponentsInChildren<Renderer>(true);if(rs.Length==0)throw new Exception("没有Renderer: "+go.name);
            var b=rs[0].bounds;foreach(var r in rs.Skip(1))b.Encapsulate(r.bounds);return b;
        }
        static void SetLayer(GameObject go,int layer){foreach(var t in go.GetComponentsInChildren<Transform>(true))t.gameObject.layer=layer;}
        static void Bottom(RectTransform r,float x,float y,float w,float h)
        {r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,0);r.anchoredPosition=new Vector2(x,y);r.sizeDelta=new Vector2(w,h);}
        static void Invoke(object target,string method){target.GetType().GetMethod(method,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(target,null);}
        static void ValidateAndRender(Scene scene,DivePointController c,RaftWeatherController weather,Chest3DView view,Camera modelCamera,Canvas canvas,RawImage raw)
        {
            foreach(var go in scene.GetRootGameObjects())foreach(var t in go.GetComponentsInChildren<Transform>(true))
                if(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)>0)throw new Exception("Missing script: "+t.name);
            if(view.GetComponentsInChildren<Renderer>(true).Length!=21)throw new Exception("箱子渲染部件数量不符");
            var raft=view.transform.parent.Find("木筏3D_原名wooden_xylophone");
            if(!raft||raft.GetComponentsInChildren<Renderer>(true).Length!=37)throw new Exception("木筏渲染部件数量不符");
            Invoke(c,"Awake");Invoke(weather,"Awake");
            c.Player.anchoredPosition=new Vector2(500,c.Player.anchoredPosition.y);c.TryOpenChest();if(view.IsOpen)throw new Exception("距离门禁失效");
            c.Player.anchoredPosition=new Vector2(c.Chest.anchoredPosition.x,c.Player.anchoredPosition.y);c.ChestButton.onClick.Invoke();if(!view.IsOpen)throw new Exception("箱盖未关联开箱UI");
            c.TakeAllButton.onClick.Invoke();var bag=(IList)typeof(DivePointController).GetField("_bagItems",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(c);
            int total=0;foreach(var item in bag)total+=(int)item.GetType().GetField("Count").GetValue(item);if(total!=11||bag.Count!=3)throw new Exception("背包转移失败");
            Directory.CreateDirectory("Imported3DPreview");view.SetAngle(0);Capture(modelCamera,"Imported3DPreview/RaftClosed.png");
            view.SetAngle(view.OpenAngle);Capture(modelCamera,"Imported3DPreview/RaftOpen.png");c.CloseChest();view.SetAngle(0);
            c.Player.anchoredPosition=new Vector2(40,93);c.Status.text="3D木筏 · 靠近木箱后按 E 打开";weather.RainAmount=0;weather.Rainy=false;
            modelCamera.Render();var sceneCamera=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<Camera>()).First(x=>x!=modelCamera);
            var rt=new RenderTexture(1600,900,24);rt.Create();sceneCamera.targetTexture=rt;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=sceneCamera;canvas.planeDistance=10;
            try {Canvas.ForceUpdateCanvases();Capture(sceneCamera,"Imported3DPreview/InScene.png");}
            finally {sceneCamera.targetTexture=null;rt.Release();Object.DestroyImmediate(rt);}
        }
        static void Capture(Camera camera,string path)
        {
            camera.Render();var old=RenderTexture.active;RenderTexture.active=camera.targetTexture;int w=camera.targetTexture.width,h=camera.targetTexture.height;
            var tex=new Texture2D(w,h,TextureFormat.RGBA32,false);
            try {tex.ReadPixels(new Rect(0,0,w,h),0,0);tex.Apply();File.WriteAllBytes(path,tex.EncodeToPNG());}
            finally {RenderTexture.active=old;Object.DestroyImmediate(tex);}
        }
    }
}
