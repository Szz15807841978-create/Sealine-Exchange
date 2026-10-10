using System;
using System.IO;
using System.Linq;
using Fishing;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace FishingEditor
{
    [InitializeOnLoad]
    public static class Chest3DBuilder
    {
        const string Folder = "Assets/Art/Chest3D";
        const string ScenePath = "Assets/Scenes/DivePointChest3D.unity";
        const string Request = "Assets/Editor/Chest3D.request";
        const int ModelLayer = 30;
        static Chest3DBuilder() { EditorApplication.delayCall += RunRequest; }
        static void RunRequest()
        {
            if(!File.Exists(Request)) return;
            if(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            try { if(File.Exists(ScenePath)) ValidateSaved(); else Build(); AssetDatabase.DeleteAsset(Request); }
            catch(Exception e) { Debug.LogException(e); }
        }

        static void ValidateSaved()
        {
            var loaded=SceneManager.GetSceneByPath(ScenePath);
            if(loaded.IsValid() && loaded.isLoaded) throw new InvalidOperationException("请保存并关闭3D木箱场景后再验证。");
            var previous=SceneManager.GetActiveScene();
            var scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Additive);
            try
            {
                var roots=scene.GetRootGameObjects();
                var view=roots.SelectMany(x=>x.GetComponentsInChildren<Chest3DView>(true)).Single();
                var c=roots.SelectMany(x=>x.GetComponentsInChildren<DivePointController>(true)).Single();
                var canvas=c.Chest.GetComponentInParent<Canvas>();
                var weather=canvas.GetComponent<RaftWeatherController>();
                var modelCamera=roots.SelectMany(x=>x.GetComponentsInChildren<Camera>()).Single(x=>x.targetTexture);
                modelCamera.orthographicSize=1.45f;
                modelCamera.transform.LookAt(view.transform.position+new Vector3(0,.9f,0));
                var raw=c.Chest.Find("3D木箱显示_由摄像机实时渲染").GetComponent<RawImage>();
                weather.Devices=weather.Devices.Concat(new Graphic[]{raw}).Distinct().ToArray();
                c.ChestButton.transition=Selectable.Transition.None;
                EditorSceneManager.SaveScene(scene);
                // Tests below operate on the temporary loaded copy and are never saved.
                Invoke(c,"Awake");Invoke(weather,"Awake");
                c.Player.anchoredPosition=new Vector2(500,c.Player.anchoredPosition.y);c.TryOpenChest();
                if(view.IsOpen)throw new Exception("Distance gate failed");
                c.Player.anchoredPosition=new Vector2(c.Chest.anchoredPosition.x,c.Player.anchoredPosition.y);
                c.ChestButton.onClick.Invoke();
                if(!view.IsOpen)throw new Exception("3D chest not bound to inventory");
                c.TakeAllButton.onClick.Invoke();
                var bag=(System.Collections.IList)typeof(DivePointController).GetField("_bagItems",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(c);
                int total=0;foreach(var item in bag)total+=(int)item.GetType().GetField("Count").GetValue(item);
                if(total!=11 || bag.Count!=3)throw new Exception("Loot transfer failed");
                view.SetAngle(view.OpenAngle);Capture(modelCamera,"Chest3DPreview/Open.png");
                c.CloseChest();if(view.IsOpen)throw new Exception("Close binding failed");
                view.SetAngle(0);Capture(modelCamera,"Chest3DPreview/Closed.png");
                c.Player.anchoredPosition=new Vector2(40,93);c.Status.text="3D木箱试验 · 靠近后按 E 打开";
                var sceneCamera=roots.SelectMany(x=>x.GetComponentsInChildren<Camera>()).First(x=>!x.targetTexture);
                var rt=new RenderTexture(1600,900,24);rt.Create();sceneCamera.targetTexture=rt;
                canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=sceneCamera;canvas.planeDistance=10;
                try {Canvas.ForceUpdateCanvases();Capture(sceneCamera,"Chest3DPreview/InScene.png");}
                finally {sceneCamera.targetTexture=null;rt.Release();Object.DestroyImmediate(rt);}
                Debug.Log("[Chest3D] PASS: distance gate, click open, 11 items to backpack, close binding, model and scene rendering.");
            }
            finally {EditorSceneManager.CloseScene(scene,true);if(previous.IsValid()&&previous.isLoaded)SceneManager.SetActiveScene(previous);}
        }
        static void Invoke(object obj,string method)
        {obj.GetType().GetMethod(method,System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(obj,null);}

        [MenuItem("Fishing/下水点/创建3D木箱试验场景（保留原版）")]
        public static void Build()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请退出 Play。");
            if(File.Exists(ScenePath) || File.Exists(Folder+"/Chest_LowPoly.prefab"))
                throw new InvalidOperationException("3D木箱资产已存在，为保护手动调整不自动覆盖。");
            Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();
            var shader=Shader.Find("Whalefall/ChestFlat");
            if(!shader || ShaderUtil.ShaderHasError(shader)) throw new Exception("Chest shader unavailable");
            var wood=Mat("木板_蜂蜜黄",new Color(.70f,.43f,.18f),shader);
            var light=Mat("木板_浅色",new Color(.84f,.58f,.28f),shader);
            var dark=Mat("缝隙与内壁",new Color(.23f,.14f,.075f),shader);
            var metal=Mat("包角_藏青",new Color(.16f,.27f,.31f),shader);
            var brass=Mat("铆钉与锁扣",new Color(.79f,.66f,.38f),shader);
            var previous=SceneManager.GetActiveScene();
            if(!AssetDatabase.CopyAsset("Assets/Scenes/DivePointWeather.unity",ScenePath)) throw new Exception("Cannot copy weather scene");
            var scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                var controller=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<DivePointController>(true)).Single();
                foreach(var cam in scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<Camera>(true)))
                    cam.cullingMask &= ~(1<<ModelLayer);
                var root=new GameObject("木箱3D_独立模型");
                var body=new GameObject("箱体_空心").transform; body.SetParent(root.transform,false);
                Cube(body,"底板",new Vector3(0,.08f,0),new Vector3(1.8f,.16f,1.02f),dark);
                for(int row=0;row<3;row++)
                {
                    float y=.235f+row*.205f;
                    foreach(int side in new[]{-1,1})
                    {
                        Cube(body,"前后木板_"+row+"_"+side,new Vector3(0,y,side*.46f),new Vector3(1.8f,.195f,.12f),row%2==0?wood:light);
                        Cube(body,"侧面木板_"+row+"_"+side,new Vector3(side*.84f,y,0),new Vector3(.12f,.195f,.8f),row%2==0?light:wood);
                    }
                }
                foreach(int x in new[]{-1,1}) foreach(int z in new[]{-1,1})
                {
                    Cube(body,"金属包角",new Vector3(x*.84f,.42f,z*.465f),new Vector3(.16f,.60f,.14f),metal);
                    foreach(float y in new[]{.2f,.64f})
                        Cube(body,"黄铜铆钉",new Vector3(x*.84f,y,z*.543f),new Vector3(.05f,.05f,.018f),brass);
                    Cube(body,"底脚",new Vector3(x*.73f,.055f,z*.37f),new Vector3(.21f,.11f,.20f),metal);
                }
                var hinge=new GameObject("箱盖转轴_绕X轴开合").transform;
                hinge.SetParent(root.transform,false); hinge.localPosition=new Vector3(0,.78f,.5f);
                Cube(hinge,"箱盖暗色底",new Vector3(0,.015f,-.5f),new Vector3(1.9f,.06f,1.12f),dark);
                for(int i=0;i<5;i++)
                    Cube(hinge,"箱盖木板_"+i,new Vector3(0,.075f,-.944f+i*.222f),new Vector3(1.9f,.10f,.212f),i%2==0?light:wood);
                foreach(int x in new[]{-1,1})
                {
                    Cube(hinge,"箱盖压条",new Vector3(x*.79f,.138f,-.5f),new Vector3(.13f,.03f,1.13f),metal);
                    foreach(float z in new[]{-.95f,-.5f,-.05f})
                        Cube(hinge,"压条铆钉",new Vector3(x*.79f,.16f,z),new Vector3(.045f,.02f,.045f),brass);
                }
                Cube(hinge,"随盖锁舌",new Vector3(0,-.09f,-1.07f),new Vector3(.17f,.3f,.065f),metal);
                Cube(hinge,"锁舌铆钉",new Vector3(0,-.04f,-1.11f),new Vector3(.055f,.055f,.02f),brass);
                Cube(body,"锁孔底座",new Vector3(0,.55f,-.54f),new Vector3(.25f,.20f,.04f),brass);
                Cube(body,"锁孔",new Vector3(0,.55f,-.565f),new Vector3(.06f,.075f,.015f),dark);
                // Rectangular side handles remain separate editable meshes.
                foreach(int x in new[]{-1,1})
                {
                    Cube(body,"侧提手上沿",new Vector3(x*.96f,.55f,0),new Vector3(.065f,.065f,.32f),brass);
                    Cube(body,"侧提手下沿",new Vector3(x*.96f,.36f,0),new Vector3(.065f,.065f,.32f),brass);
                    foreach(int z in new[]{-1,1}) Cube(body,"侧提手竖边",new Vector3(x*.96f,.455f,z*.16f),new Vector3(.065f,.25f,.065f),brass);
                }
                var collider=root.AddComponent<BoxCollider>(); collider.center=new Vector3(0,.43f,0);collider.size=new Vector3(1.9f,.86f,1.12f);
                var view=root.AddComponent<Chest3DView>(); view.LidHinge=hinge;
                // Prefab is saved at origin on Default layer for reuse in future fully 3D scenes.
                PrefabUtility.SaveAsPrefabAsset(root,Folder+"/Chest_LowPoly.prefab");
                root.transform.position=new Vector3(1000,0,0);
                foreach(var tr in root.GetComponentsInChildren<Transform>()) tr.gameObject.layer=ModelLayer;
                view.ChestPanel=controller.ChestPanel;
                var render=new RenderTexture(512,512,24,RenderTextureFormat.ARGB32);
                render.name="Chest3D_Render";render.antiAliasing=4;
                AssetDatabase.CreateAsset(render,Folder+"/Chest3D_Render.renderTexture");
                var camera=new GameObject("木箱3D_展示摄像机").AddComponent<Camera>();
                camera.transform.position=root.transform.position+new Vector3(2.6f,2.0f,-4.5f);
                camera.transform.LookAt(root.transform.position+new Vector3(0,.8f,0));
                camera.orthographic=true;camera.orthographicSize=1.28f;camera.nearClipPlane=.1f;camera.farClipPlane=20;
                camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.clear;
                camera.cullingMask=1<<ModelLayer;camera.targetTexture=render;camera.depth=-10;camera.allowHDR=false;
                // Preserve the original icon, transform, interaction button and user layout.
                controller.ChestImage.enabled=false;
                var display=new GameObject("3D木箱显示_由摄像机实时渲染",typeof(RectTransform),typeof(CanvasRenderer),typeof(RawImage));
                display.transform.SetParent(controller.Chest,false);
                var rect=(RectTransform)display.transform;
                rect.anchorMin=rect.anchorMax=new Vector2(.5f,0);rect.pivot=new Vector2(.5f,0);
                rect.sizeDelta=new Vector2(220,220);rect.anchoredPosition=new Vector2(0,-32);
                var raw=display.GetComponent<RawImage>();raw.texture=render;raw.raycastTarget=false;
                // Keep invisible original graphic raycastable for click interaction.
                controller.ChestImage.enabled=true; controller.ChestImage.color=Color.clear;
                var hit=controller.ChestButton;
                if(hit) {hit.targetGraphic=raw;}
                // Controller no longer tints the old icon when opening/closing.
                controller.ChestImage=null;
                EditorSceneManager.SaveScene(scene,ScenePath);
                AssetDatabase.SaveAssets();
                Directory.CreateDirectory("Chest3DPreview");
                Capture(camera,"Chest3DPreview/Closed.png");
                view.SetAngle(105);Capture(camera,"Chest3DPreview/Open.png");view.SetAngle(0);
                if(root.GetComponentsInChildren<MeshRenderer>().Length<30 || !view.LidHinge)throw new Exception("Incomplete model");
                Debug.Log("[Chest3D] PASS: prefab, independent scene, closed/open mesh previews saved. Original scenes unchanged.");
            }
            finally {EditorSceneManager.CloseScene(scene,true);if(previous.IsValid()&&previous.isLoaded)SceneManager.SetActiveScene(previous);}
        }
        static Material Mat(string name,Color color,Shader shader)
        {var m=new Material(shader){name=name,color=color};AssetDatabase.CreateAsset(m,Folder+"/"+name+".mat");return m;}
        static void Cube(Transform parent,string name,Vector3 p,Vector3 s,Material material)
        {
            var obj=GameObject.CreatePrimitive(PrimitiveType.Cube);obj.name=name;
            obj.transform.SetParent(parent,false);obj.transform.localPosition=p;obj.transform.localScale=s;
            Object.DestroyImmediate(obj.GetComponent<Collider>());obj.GetComponent<MeshRenderer>().sharedMaterial=material;
        }
        static void Capture(Camera camera,string path)
        {
            camera.Render();var old=RenderTexture.active;RenderTexture.active=camera.targetTexture;
            int width=camera.targetTexture.width,height=camera.targetTexture.height;
            var tex=new Texture2D(width,height,TextureFormat.RGBA32,false);
            try {tex.ReadPixels(new Rect(0,0,width,height),0,0);tex.Apply();File.WriteAllBytes(path,tex.EncodeToPNG());}
            finally {RenderTexture.active=old;Object.DestroyImmediate(tex);}
        }
    }
}
