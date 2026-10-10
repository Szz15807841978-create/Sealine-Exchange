using System;
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
    internal static class RaftWeatherRequest
    {
        static RaftWeatherRequest() {EditorApplication.delayCall+=Run;}
        private static void Run()
        {
            const string request="Assets/Editor/RaftWeather.request";
            if(!File.Exists(request))return;
            if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling) {EditorApplication.delayCall+=Run;return;}
            try {if(File.Exists(RaftWeatherSceneBuilder.Path))RaftWeatherSceneBuilder.RepairAndValidate();else RaftWeatherSceneBuilder.Build();AssetDatabase.DeleteAsset(request);}
            catch(Exception e){Debug.LogException(e);}
        }
    }
    public static class RaftWeatherSceneBuilder
    {
        public const string Path="Assets/Scenes/DivePointWeather.unity";
        private const string Art="Assets/Resources/DivePoint/Weather/";
        [MenuItem("Fishing/下水点/验证晴雨场景")]
        public static void RepairAndValidate()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请先退出 Play。");
            var loaded=SceneManager.GetSceneByPath(Path);
            if(loaded.IsValid() && loaded.isLoaded)
                throw new InvalidOperationException("请先保存并关闭晴雨场景，再执行验证，以免影响你的场景调整。");
            var scene=EditorSceneManager.OpenScene(Path,OpenSceneMode.Additive);
            try
            {
                var canvas=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<Canvas>(true)).Single();
                var c=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<DivePointController>(true)).Single();
                var w=canvas.GetComponent<RaftWeatherController>();
                foreach(var g in canvas.GetComponentsInChildren<RaftWeatherGraphic>(true))
                    if(!g.GetComponent<CanvasRenderer>())g.gameObject.AddComponent<CanvasRenderer>();
                EditorSceneManager.SaveScene(scene);
                ValidateAndRender(scene,c,w,canvas);
                Debug.Log("[RaftWeather] PASS: repaired renderers, loot and weather checks, previews saved.");
            }
            finally {EditorSceneManager.CloseScene(scene,true);}
        }
        [MenuItem("Fishing/下水点/创建晴雨美术场景（保留现有场景）")]
        public static void Build()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请先退出 Play。");
            if(File.Exists(Path)) throw new InvalidOperationException("晴雨场景已存在；为保留手动调整，不自动覆盖。");
            foreach(string file in Directory.GetFiles(Art,"*.png"))
            {
                var importer=AssetImporter.GetAtPath(file.Replace('\\','/')) as TextureImporter;
                if(importer==null) continue;
                importer.alphaIsTransparency=true; importer.mipmapEnabled=false; importer.maxTextureSize=2048;
                importer.wrapMode=TextureWrapMode.Clamp; importer.SaveAndReimport();
            }
            var previous=SceneManager.GetActiveScene();
            // Open a copy, so unsaved objects in the user's loaded scene are never serialized here.
            if(!AssetDatabase.CopyAsset("Assets/Scenes/DivePointDemo.unity",Path))throw new Exception("Cannot copy source scene");
            var scene=EditorSceneManager.OpenScene(Path,OpenSceneMode.Additive);
            try
            {
                if(scene==previous) throw new InvalidOperationException("请先切换到其他场景再执行创建，以保留当前未保存编辑。");
                SceneManager.SetActiveScene(scene);
                var canvas=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<Canvas>(true)).Single();
                var root=(RectTransform)canvas.transform;
                var controller=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<DivePointController>(true)).Single();
                foreach(string name in new[]{"天空","海面"}) {var old=root.Find(name); if(old) old.gameObject.SetActive(false);}
                var env=Rect(root,"环境与天气"); Stretch(env); env.SetAsFirstSibling();
                var sky=Layer(env,"天空渐变",RaftWeatherGraphic.Layer.Sky); Stretch(sky.rectTransform);
                var clouds=Layer(env,"独立云层",RaftWeatherGraphic.Layer.Clouds);
                Place(clouds.rectTransform,0,200,1800,550);
                var sea=Texture(env,"海面贴图", "Ocean.png"); Place(sea.rectTransform,0,-280,1800,430);
                var raft=controller.Player.parent as RectTransform;
                foreach(Transform child in raft) if(child!=controller.Player && child!=controller.Chest && child!=controller.Prompt.transform) child.gameObject.SetActive(false);
                var deck=Texture(raft,"木筏_可替换图层","Raft_Empty.png");
                deck.uvRect=new Rect(0,.25f,1,.42f);
                Bottom(deck.rectTransform,0,20,1400,220); deck.transform.SetAsFirstSibling();
                Bottom(controller.Chest,-170,128,160,160);
                Bottom(controller.Player,40,93,190,208);
                Bottom(controller.Prompt.rectTransform,-170,297,220,38);
                controller.LeftLimit=-580; controller.RightLimit=580;
                string[] ids={"BENCH","RACK","STOVE","STILL","FERMENT","YOGURT"};
                float[] xs={-550,-365,5,200,365,495};
                float[] widths={220,145,175,195,150,125};
                foreach(int i in Enumerable.Range(0,ids.Length))
                {
                    var device=Texture(raft,"装置_ITM_DEV_"+ids[i]+"_01","ITM_DEV_"+ids[i]+"_01.png");
                    Bottom(device.rectTransform,xs[i],128,widths[i],widths[i]);
                    device.transform.SetSiblingIndex(controller.Chest.GetSiblingIndex());
                }
                // Retain the original editable flag pole and sail at the right of the wider raft.
                foreach(string name in new[]{"下水点标杆","下水旗"})
                { var flag=raft.Find(name) as RectTransform; if(flag) {flag.gameObject.SetActive(true); flag.anchoredPosition+=new Vector2(100,0);} }
                controller.Player.SetAsLastSibling(); controller.Prompt.transform.SetAsLastSibling();
                var rain=Layer(root,"雨幕_不阻挡交互",RaftWeatherGraphic.Layer.Rain); Stretch(rain.rectTransform);
                rain.transform.SetSiblingIndex(controller.ChestPanel.transform.GetSiblingIndex());
                var weather=root.gameObject.AddComponent<RaftWeatherController>();
                weather.Sky=sky; weather.Clouds=clouds; weather.Rain=rain; weather.Ocean=sea; weather.Raft=raft;
                weather.Devices=raft.GetComponentsInChildren<RawImage>().Where(x=>x!=controller.ChestImage).Cast<Graphic>().ToArray();
                var toolbar=Rect(root,"天气控制"); Place(toolbar,485,345,570,48);
                weather.SunnyButton=Button(toolbar,"晴天",-140); weather.RainyButton=Button(toolbar,"雨天",-35);
                weather.WeatherLabel=Text(toolbar,"天气状态","晴天 · R 切换",17);
                Place(weather.WeatherLabel.rectTransform,155,0,265,40);
                EditorSceneManager.SaveScene(scene,Path);
                ValidateAndRender(scene,controller,weather,canvas);
                Debug.Log("[RaftWeather] PASS: scene bindings, distance gate, loot conservation, weather endpoints and renders.");
            }
            finally { if(scene!=previous) EditorSceneManager.CloseScene(scene,true); if(previous.IsValid()&&previous.isLoaded)SceneManager.SetActiveScene(previous); }
        }
        private static void ValidateAndRender(Scene scene,DivePointController c,RaftWeatherController w,Canvas canvas)
        {
            foreach(var go in scene.GetRootGameObjects()) foreach(var t in go.GetComponentsInChildren<Transform>(true))
                if(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)>0)throw new Exception("Missing script: "+t.name);
            Invoke(c,"Awake"); Invoke(w,"Awake");
            c.Player.anchoredPosition=new Vector2(500,c.Player.anchoredPosition.y); c.TryOpenChest();
            if(c.ChestPanel.activeSelf)throw new Exception("Distance gate failed");
            c.Player.anchoredPosition=new Vector2(c.Chest.anchoredPosition.x,c.Player.anchoredPosition.y); c.TryOpenChest();
            if(!c.ChestPanel.activeSelf)throw new Exception("Chest open failed");
            c.TakeAllButton.onClick.Invoke();
            var bag=(System.Collections.IList)typeof(DivePointController).GetField("_bagItems",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(c);
            int total=0; foreach(var item in bag)total+=(int)item.GetType().GetField("Count").GetValue(item);
            if(total!=11||bag.Count!=3)throw new Exception("Loot count failed");
            c.TakeAllButton.onClick.Invoke();
            int afterTotal=0; foreach(var item in bag)afterTotal+=(int)item.GetType().GetField("Count").GetValue(item);
            if(bag.Count!=3 || afterTotal!=total)throw new Exception("Duplicate loot");
            c.CloseChest(); c.Player.anchoredPosition=new Vector2(-260,93);
            var cam=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<Camera>()).First();
            string dir="WeatherPreview"; Directory.CreateDirectory(dir);
            var rt=new RenderTexture(1600,900,24); rt.Create(); cam.targetTexture=rt;
            canvas.renderMode=RenderMode.ScreenSpaceCamera; canvas.worldCamera=cam; canvas.planeDistance=10;
            try
            {
                for(int k=0;k<2;k++)
                {
                    w.Rainy=k==1; w.RainAmount=k; Invoke(w,"Apply"); Canvas.ForceUpdateCanvases();
                    cam.Render(); var old=RenderTexture.active; RenderTexture.active=rt;
                    var tex=new Texture2D(1600,900,TextureFormat.RGB24,false); tex.ReadPixels(new Rect(0,0,1600,900),0,0); tex.Apply();
                    File.WriteAllBytes(dir+"/"+(k==0?"Sunny":"Rainy")+".png",tex.EncodeToPNG());
                    Object.DestroyImmediate(tex); RenderTexture.active=old;
                }
            }
            finally {cam.targetTexture=null; rt.Release();Object.DestroyImmediate(rt);}
        }
        private static void Invoke(object obj,string name) {obj.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(obj,null);}
        private static RectTransform Rect(Transform p,string name) {var g=new GameObject(name,typeof(RectTransform));g.transform.SetParent(p,false);return (RectTransform)g.transform;}
        private static void Stretch(RectTransform r) {r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;}
        private static void Place(RectTransform r,float x,float y,float width,float height) {r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,.5f);r.anchoredPosition=new Vector2(x,y);r.sizeDelta=new Vector2(width,height);}
        private static void Bottom(RectTransform r,float x,float y,float width,float height) {Place(r,x,y,width,height);r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,0);}
        private static RawImage Texture(Transform p,string name,string file) {var r=Rect(p,name);var g=r.gameObject.AddComponent<RawImage>();g.texture=AssetDatabase.LoadAssetAtPath<Texture2D>(Art+file);g.raycastTarget=false;if(!g.texture)throw new Exception("Missing texture "+file);return g;}
        private static RaftWeatherGraphic Layer(Transform p,string name,RaftWeatherGraphic.Layer kind) {var r=Rect(p,name);var g=r.gameObject.AddComponent<RaftWeatherGraphic>();g.Kind=kind;g.raycastTarget=false;return g;}
        private static Text Text(Transform p,string name,string text,int size) {var r=Rect(p,name);var t=r.gameObject.AddComponent<Text>();t.text=text;t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");t.fontSize=size;t.color=new Color(.08f,.2f,.29f);t.alignment=TextAnchor.MiddleCenter;t.raycastTarget=false;return t;}
        private static Button Button(Transform p,string label,float x) {var r=Rect(p,label);Place(r,x,0,96,38);var img=r.gameObject.AddComponent<Image>();img.color=new Color(.95f,.88f,.70f);var b=r.gameObject.AddComponent<Button>();b.targetGraphic=img;Stretch(Text(r,"文字",label,17).rectTransform);return b;}
    }
}
