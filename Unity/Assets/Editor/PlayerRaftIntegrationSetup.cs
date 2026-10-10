using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Fishing;
using Fishing.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace FishingEditor
{
    // Incremental migration of the user's current scene. Never invokes the full scene generator.
    [InitializeOnLoad]
    public static class PlayerRaftIntegrationSetup
    {
        const string MainPath="Assets/Scenes/Main.unity",RaftPath="Assets/Scenes/Current/DivePointRealisticOceanSky_Unified.unity";
        const string Request="tools/PlayerRaftIntegration.request",Report="tools/PlayerRaftIntegration-report.txt";
        const string Marker="Assets/PlayerFishing/PlayerRaftIntegration.installed.txt";
        static bool busy;
        static Font font;
        static readonly Color Panel=new Color(.04f,.09f,.12f,.97f),TextColor=new Color(.9f,.95f,.96f),Accent=new Color(.45f,.88f,.72f);
        static PlayerRaftIntegrationSetup() { EditorApplication.update+=CheckRequest; }
        static void CheckRequest()
        {
            if(busy||!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
            busy=true;
            try{Install();File.Move(Request,Request+".done-"+DateTime.Now.ToString("yyyyMMdd-HHmmss"));}
            catch(Exception ex){File.WriteAllText(Report,"FAILED (request retained)\n"+ex);Debug.LogException(ex);EditorApplication.update-=CheckRequest;}
            finally{busy=false;}
        }
        [MenuItem("Fishing/玩家版/局部调整用户布局并安装木筏观测装置")]
        public static void FromMenu() { Install(); }
        public static void FromCommandLine() { Install(); }
        public static void Install()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("请先退出 Play 模式");
            if(File.Exists(Marker)){RefineInstalled();return;}
            foreach(string path in new[]{MainPath,RaftPath}){var open=SceneManager.GetSceneByPath(path);if(open.IsValid()&&open.isLoaded&&open.isDirty)throw new InvalidOperationException("请先保存场景，未覆盖未保存的修改："+path);}
            string backup="tools/backups/before-raft-observations-"+DateTime.Now.ToString("yyyyMMdd-HHmmss");Directory.CreateDirectory(backup);
            File.Copy(MainPath,backup+"/Main.unity");File.Copy(MainPath+".meta",backup+"/Main.unity.meta");File.Copy(RaftPath,backup+"/Raft.unity");File.Copy(RaftPath+".meta",backup+"/Raft.unity.meta");
            var previous=SceneManager.GetActiveScene();var main=SceneManager.GetSceneByPath(MainPath);var raft=SceneManager.GetSceneByPath(RaftPath);bool openedMain=!main.IsValid()||!main.isLoaded,openedRaft=!raft.IsValid()||!raft.isLoaded;
            if(openedMain)main=EditorSceneManager.OpenScene(MainPath,OpenSceneMode.Additive);if(openedRaft)raft=EditorSceneManager.OpenScene(RaftPath,OpenSceneMode.Additive);
            try{
                font=FontUtil.Get();var controller=All<PlayerMarketController>(main).Single();var view=controller.View;
                var originalHeader=SnapshotBranch(view.Canvas.transform.Cast<Transform>().Single(t=>t.name.StartsWith("01_Header")));
                var oldRaft=All<Transform>(raft).ToDictionary(t=>t.GetInstanceID(),t=>SnapshotTransform(t));
                string chart=NormalizePanel(view.Canvas.transform,"03_Market"),trade=NormalizePanel(view.Canvas.transform,"04_Trading");
                // Retired controls are not merely parked outside the screen. Their data stays in the model.
                foreach(var prefix in new[]{"02_Exploration","05_Island","06_Rumor","11_Survey"}){var t=Child(view.Canvas.transform,prefix);if(t)Object.DestroyImmediate(t.gameObject);}
                view.Texts=view.Texts.Where(x=>x.Target).ToArray();view.Buttons=view.Buttons.Where(x=>x.Target).ToArray();view.MapEstimates=new Text[0];
                view.DivePanel=null;view.StaminaFill=null;view.Diver=null;view.TemperatureFill=null;view.WindNeedle=null;
                controller.UseRaftPreparation=true;controller.DemoNetAvailable=true;
                BuildRadio(view);
                view.RaftInputBlockers=new[]{Child(view.Canvas.transform,"01_Header") as RectTransform,Child(view.Canvas.transform,"03_Market") as RectTransform,Child(view.Canvas.transform,"04_Trading") as RectTransform,Child(view.Canvas.transform,"07_Radio") as RectTransform,view.RadioExpanded.transform as RectTransform};
                var station=BuildStation(raft,controller.Settings);
                view.EnsureFonts();station.EnsureFonts();
                if(originalHeader!=SnapshotBranch(Child(view.Canvas.transform,"01_Header")))throw new Exception("Header was changed unexpectedly");
                foreach(var t in All<Transform>(raft))if(oldRaft.TryGetValue(t.GetInstanceID(),out var expected)&&expected!=SnapshotTransform(t))throw new Exception("Existing raft transform changed: "+t.name);
                Validate(main,raft);
                EditorUtility.SetDirty(view);EditorUtility.SetDirty(controller);EditorUtility.SetDirty(station);
                EditorSceneManager.MarkSceneDirty(main);EditorSceneManager.MarkSceneDirty(raft);
                if(!EditorSceneManager.SaveScene(main,MainPath)||!EditorSceneManager.SaveScene(raft,RaftPath))throw new IOException("Scene save failed; original files remain in "+backup);
                AssetDatabase.SaveAssets();
                File.WriteAllText(Marker,"Incremental raft observations installed; never rebuild Main to adjust this layout.\nBackup="+backup+"\n");
                File.WriteAllText(Report,"PASS\nBackup="+backup+"\n"+chart+"\n"+trade+"\nHeaderUnchanged=True\nExistingRaftTransformsUnchanged=True\nExplorationIslandRumorPanelsRemoved=True\nRadioDefaultCollapsed=True\nSharedSession=True\nProps="+station.Targets.Length+"\nMainObjects="+All<Transform>(main).Length+"\n");
                Preview(main,raft,"tools/PlayerRaftIntegration-preview.png");
                Debug.Log("[Player] 已在用户布局上局部调整，安装木筏 3D 观测装置。备份："+backup);
            }finally{if(openedMain&&main.IsValid()&&main.isLoaded)EditorSceneManager.CloseScene(main,true);if(openedRaft&&raft.IsValid()&&raft.isLoaded)EditorSceneManager.CloseScene(raft,true);if(previous.IsValid()&&previous.isLoaded)SceneManager.SetActiveScene(previous);}
        }
        static T[] All<T>(Scene scene) where T:Component { return scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<T>(true)).ToArray(); }
        static Transform Child(Transform parent,string prefix) { return parent.Cast<Transform>().FirstOrDefault(t=>t.name.StartsWith(prefix,StringComparison.Ordinal)); }
        static string SnapshotTransform(Transform t) { return t.localPosition.ToString("R")+t.localRotation.ToString("R")+t.localScale.ToString("R")+t.gameObject.activeSelf; }
        static string SnapshotBranch(Transform root) { return string.Join("\n",root.GetComponentsInChildren<Transform>(true).Select(t=>EditorJsonUtility.ToJson(t)+string.Join("",t.GetComponents<Text>().Select(c=>EditorJsonUtility.ToJson(c))))); }
        static string NormalizePanel(Transform parent,string prefix)
        {
            var panel=(RectTransform)Child(parent,prefix);if(!panel)throw new Exception("Missing user panel "+prefix);
            var scale=panel.localScale;var pos=panel.anchoredPosition;var oldSize=panel.rect.size;var factors=new Vector2(scale.x,scale.y);
            if(scale.x<=0||scale.y<=0||panel.localRotation!=Quaternion.identity)throw new Exception("Unexpected rotated/scaled panel; no guessed layout: "+prefix);
            foreach(var rt in panel.GetComponentsInChildren<RectTransform>(true)){
                rt.sizeDelta=Vector2.Scale(rt.sizeDelta,factors);if(rt!=panel)rt.anchoredPosition=Vector2.Scale(rt.anchoredPosition,factors);
                var text=rt.GetComponent<Text>();if(text){text.resizeTextForBestFit=true;text.resizeTextMinSize=Math.Min(text.fontSize,12);text.resizeTextMaxSize=text.fontSize;}
            }
            panel.localScale=Vector3.one;panel.anchoredPosition=pos;
            // Preserve the user's custom percentage row but keep its enlarged caption away from the input.
            if(prefix=="04_Trading"){
                var label=Child(panel,"PercentLabel") as RectTransform;var input=Child(panel,"PercentInput") as RectTransform;
                if(label&&input){float available=input.anchoredPosition.x-label.anchoredPosition.x-8;label.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,Mathf.Max(30,available/label.localScale.x));}
            }
            return prefix+" position="+pos+" footprint="+Vector2.Scale(oldSize,factors)+" scale="+panel.localScale;
        }
        static RectTransform Rect(Transform parent,string name,float x,float y,float w,float h)
        {
            var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);return r;
        }
        static Image Box(Transform parent,string name,float x,float y,float w,float h,Color color) { var r=Rect(parent,name,x,y,w,h);var im=r.gameObject.AddComponent<Image>();im.color=color;im.raycastTarget=false;return im; }
        static Text Label(Transform parent,string name,string text,float x,float y,float w,float h,int size=20)
        {
            var r=Rect(parent,name,x,y,w,h);var t=r.gameObject.AddComponent<Text>();t.font=font;t.text=text;t.fontSize=size;t.color=TextColor;t.alignment=TextAnchor.MiddleLeft;t.raycastTarget=false;t.supportRichText=false;return t;
        }
        static Button Button(Transform parent,string name,string text,float x,float y,float w,float h)
        {
            var im=Box(parent,name,x,y,w,h,new Color(.12f,.26f,.31f));im.raycastTarget=true;var b=im.gameObject.AddComponent<Button>();b.targetGraphic=im;var nav=b.navigation;nav.mode=Navigation.Mode.None;b.navigation=nav;
            Label(im.transform,"Label",text,10,0,w-20,h,18).alignment=TextAnchor.MiddleCenter;return b;
        }
        static void BuildRadio(PlayerMarketView view)
        {
            var old=(RectTransform)Child(view.Canvas.transform,"07_Radio");if(!old)throw new Exception("Missing original radio panel");
            var frame=Rect(view.Canvas.transform,"07_Radio · 可折叠海上电台",0,0,420,48);frame.anchorMin=frame.anchorMax=frame.pivot=Vector2.zero;frame.anchoredPosition=new Vector2(30,24);
            old.SetParent(frame,false);old.name="Expanded · 电台记录（默认收起）";old.anchorMin=old.anchorMax=old.pivot=Vector2.zero;old.anchoredPosition=new Vector2(0,58);old.localScale=Vector3.one;old.sizeDelta=new Vector2(420,354);
            var title=Child(old,"Title") as RectTransform;if(title)title.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,380);
            var scroll=old.GetComponentInChildren<ScrollRect>(true);if(scroll){var rt=(RectTransform)scroll.transform;rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,384);foreach(var row in view.RadioRows)if(row)row.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,368);}
            var toggle=Button(frame,"Toggle · 展开与收起","海上电台  ·  展开 ▴",0,-48,420,48);
            view.Buttons=view.Buttons.Concat(new[]{new PlayerButtonBinding{Action="radioToggle",Target=toggle}}).ToArray();view.Texts=view.Texts.Concat(new[]{new PlayerTextBinding{Key="radioToggle",Target=toggle.GetComponentInChildren<Text>()}}).ToArray();
            view.RadioExpanded=old.gameObject;old.gameObject.SetActive(false);
        }
        static Material MaterialAsset(string name,Color color)
        {
            const string folder="Assets/PlayerFishing/RaftObservations";if(!AssetDatabase.IsValidFolder(folder))AssetDatabase.CreateFolder("Assets/PlayerFishing","RaftObservations");
            string path=folder+"/"+name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);if(material)return material;
            material=new Material(Shader.Find("Standard")){name=name,color=color};material.SetFloat("_Glossiness",.32f);AssetDatabase.CreateAsset(material,path);return material;
        }
        static Transform Part(Transform parent,string name,PrimitiveType type,Vector3 pos,Vector3 scale,Material material)
        {
            var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=pos;go.transform.localScale=scale;go.GetComponent<Renderer>().sharedMaterial=material;var collider=go.GetComponent<Collider>();if(collider)Object.DestroyImmediate(collider);return go.transform;
        }
        static TextMesh WorldLabel(Transform parent,string name,string text,Vector3 position,float size=.07f)
        {
            var go=new GameObject(name,typeof(TextMesh));go.transform.SetParent(parent,false);go.transform.localPosition=position;var label=go.GetComponent<TextMesh>();label.text=text;label.font=font;label.fontSize=64;label.characterSize=size;label.anchor=TextAnchor.MiddleCenter;label.alignment=TextAlignment.Center;label.color=TextColor;go.GetComponent<Renderer>().sharedMaterial=font.material;return label;
        }
        static RaftObservationTarget Target(Transform parent,RaftMarketStation station,string name,RaftObservationKind kind,Vector3 pos,Vector3 hitCenter,Vector3 hitSize)
        {
            var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.localPosition=pos;var t=go.AddComponent<RaftObservationTarget>();t.Station=station;t.Kind=kind;t.Caption=name;var c=go.AddComponent<BoxCollider>();c.isTrigger=true;c.center=hitCenter;c.size=hitSize;return t;
        }
        static RaftMarketStation BuildStation(Scene scene,PlayerMarketSettings settings)
        {
            var flow=All<RaftFishingFlow>(scene).Single();var rider=flow.PlayerBridge.DeckRider;var raft=rider.Buoyancy.RaftRoot;
            var root=new GameObject("捕捞观测装置 · 可编辑 3D 与会话");root.transform.SetParent(raft,false);var station=root.AddComponent<RaftMarketStation>();station.Settings=settings;station.Flow=flow;station.OceanCamera=flow.OceanCamera;station.SelectedWater=0;station.DemoNetAvailable=true;
            station.OceanViewport=All<RawImage>(scene).FirstOrDefault(im=>im.texture&&im.texture==flow.OceanCamera.targetTexture);if(!station.OceanViewport)throw new Exception("Cannot resolve existing ocean RenderTexture viewport; no guessed input mapping");
            var wood=MaterialAsset("ObservationWood",new Color(.32f,.17f,.08f));var metal=MaterialAsset("ObservationMetal",new Color(.13f,.22f,.27f));var mint=MaterialAsset("ObservationMint",Accent);var white=MaterialAsset("ObservationIvory",new Color(.85f,.84f,.69f));var red=MaterialAsset("ThermometerRed",new Color(.88f,.15f,.1f));
            float deck=rider.DeckSurfaceLocalY,z=rider.DeckZForRider;
            var wind=Target(root.transform,station,"01_风向标 · 点击查看风速",RaftObservationKind.Wind,new Vector3(1.8f,deck,z+.45f),new Vector3(0,1.25f,0),new Vector3(1.2f,2.5f,1.0f));
            Part(wind.transform,"Base",PrimitiveType.Cylinder,new Vector3(0,.07f,0),new Vector3(.48f,.07f,.48f),wood);
            Part(wind.transform,"Mast",PrimitiveType.Cylinder,new Vector3(0,1.1f,0),new Vector3(.07f,1.1f,.07f),metal);
            var pointer=new GameObject("Pointer · 风向指针").transform;pointer.SetParent(wind.transform,false);pointer.localPosition=new Vector3(0,2.18f,0);station.WindPointer=pointer;
            Part(pointer,"ArrowShaft",PrimitiveType.Cube,Vector3.zero,new Vector3(1.05f,.055f,.07f),mint);var fin=Part(pointer,"ArrowHead",PrimitiveType.Cube,new Vector3(.47f,0,0),new Vector3(.25f,.055f,.25f),mint);fin.localRotation=Quaternion.Euler(0,45,0);
            var rotor=new GameObject("Rotor · 风速转杯").transform;rotor.SetParent(wind.transform,false);rotor.localPosition=new Vector3(0,1.75f,0);station.WindRotor=rotor;
            for(int i=0;i<3;i++){float a=i*Mathf.PI*2/3;Part(rotor,"Cup_"+i,PrimitiveType.Sphere,new Vector3(Mathf.Cos(a)*.38f,0,Mathf.Sin(a)*.38f),Vector3.one*.18f,white);var arm=Part(rotor,"Arm_"+i,PrimitiveType.Cube,new Vector3(Mathf.Cos(a)*.19f,0,Mathf.Sin(a)*.19f),new Vector3(.4f,.025f,.025f),metal);arm.localRotation=Quaternion.Euler(0,-i*120,0);}
            station.WindReading=WorldLabel(wind.transform,"Reading · 实时风速","— m/s",new Vector3(0,2.6f,0));
            var temp=Target(root.transform,station,"02_水下温度计 · 点击查看水温",RaftObservationKind.Temperature,new Vector3(-.1f,deck,z-.8f),new Vector3(0,-.3f,0),new Vector3(.9f,3.1f,.8f));
            Part(temp.transform,"Mount",PrimitiveType.Cube,new Vector3(0,.18f,0),new Vector3(.45f,.36f,.4f),wood);Part(temp.transform,"Rope",PrimitiveType.Cylinder,new Vector3(0,-.68f,0),new Vector3(.026f,.87f,.026f),white);
            Part(temp.transform,"ProbeBacking",PrimitiveType.Cube,new Vector3(0,-1.6f,0),new Vector3(.25f,1.05f,.09f),white);
            station.TemperatureColumn=Part(temp.transform,"Mercury · 水温动态液柱",PrimitiveType.Cube,new Vector3(0,-1.6f,-.06f),new Vector3(.04f,.82f,.04f),red);
            Part(temp.transform,"Bulb",PrimitiveType.Sphere,new Vector3(0,-2.04f,-.06f),Vector3.one*.13f,red);
            for(int i=0;i<6;i++)Part(temp.transform,"Tick_"+i,PrimitiveType.Cube,new Vector3(.076f,-2.0f+i*.16f,-.053f),new Vector3(.07f,.017f,.02f),metal);
            station.TemperatureReading=WorldLabel(temp.transform,"Reading · 甲板端水温","— °C",new Vector3(0,.68f,0));
            var weather=Target(root.transform,station,"03_海况观测牌 · 点击选择目的地",RaftObservationKind.Station,new Vector3(3.0f,deck,z-.45f),new Vector3(0,.75f,0),new Vector3(1.25f,1.5f,.75f));
            Part(weather.transform,"Post",PrimitiveType.Cylinder,new Vector3(0,.45f,0),new Vector3(.08f,.45f,.08f),wood);Part(weather.transform,"Board",PrimitiveType.Cube,new Vector3(0,1.0f,0),new Vector3(1.05f,.62f,.09f),metal);
            for(int i=0;i<3;i++)Part(weather.transform,"Route_"+i,PrimitiveType.Cube,new Vector3(-.3f+i*.3f,1.0f,-.07f),new Vector3(.18f,.1f,.025f),mint);
            var rumor=Target(root.transform,station,"04_渔讯通讯器 · 点击听老渔民消息",RaftObservationKind.Rumor,new Vector3(-2.7f,deck,z+.15f),new Vector3(0,.6f,0),new Vector3(1.1f,1.3f,.75f));
            Part(rumor.transform,"Crate",PrimitiveType.Cube,new Vector3(0,.22f,0),new Vector3(.9f,.44f,.65f),wood);Part(rumor.transform,"Receiver",PrimitiveType.Cube,new Vector3(0,.61f,0),new Vector3(.72f,.38f,.44f),metal);Part(rumor.transform,"Antenna",PrimitiveType.Cylinder,new Vector3(.22f,1.05f,0),new Vector3(.028f,.3f,.028f),metal);
            Part(rumor.transform,"Speaker",PrimitiveType.Cube,new Vector3(-.12f,.61f,-.24f),new Vector3(.29f,.23f,.035f),white);Part(rumor.transform,"Knob",PrimitiveType.Sphere,new Vector3(.21f,.59f,-.24f),Vector3.one*.13f,mint);
            station.Targets=new[]{wind,temp,weather,rumor};var captions=new[]{"风向标 · 点击 / F","温度计 · 点击 / F","海况 / 目的地 · 点击 / F","渔民消息 · 点击 / F"};var heights=new[]{3.0f,1.02f,1.55f,1.5f};var labels=new List<TextMesh>();
            for(int i=0;i<station.Targets.Length;i++){var label=WorldLabel(station.Targets[i].transform,"Label · 交互提示",captions[i],new Vector3(0,heights[i],0),.048f);labels.Add(label);station.Targets[i].LabelAnchor=label.transform;}station.PropLabels=labels.ToArray();
            BuildDialog(station,flow.InteractionCanvas.transform);ConfigurePropRendering(station);return station;
        }
        static void ConfigurePropRendering(RaftMarketStation station)
        {
            var raft=station.Flow.PlayerBridge.DeckRider.Buoyancy.RaftRoot;
            var reference=raft.GetComponentsInChildren<Renderer>(true).First(r=>!r.transform.IsChildOf(station.transform)&&(station.OceanCamera.cullingMask&(1<<r.gameObject.layer))!=0);
            foreach(var t in station.GetComponentsInChildren<Transform>(true))t.gameObject.layer=reference.gameObject.layer;
            float deck=station.Flow.PlayerBridge.DeckRider.DeckSurfaceLocalY;
            // Existing workbench/chest/stove occupy the centre row; use the near gunwale instead.
            Vector3[] places={new Vector3(-4.35f,deck,-.85f),new Vector3(-3.15f,deck,-1.25f),new Vector3(-.85f,deck,-1.1f),new Vector3(-5.45f,deck,-1.25f)};
            for(int i=0;i<station.Targets.Length;i++)station.Targets[i].transform.localPosition=places[i];
            float mirror=station.OceanViewport.uvRect.width*station.OceanViewport.rectTransform.lossyScale.x;
            foreach(var label in station.GetComponentsInChildren<TextMesh>(true)){label.transform.rotation=station.OceanCamera.transform.rotation;var scale=label.transform.localScale;scale.x=Mathf.Abs(scale.x)*(mirror<0?-1:1);label.transform.localScale=scale;}
        }
        static void RefineInstalled()
        {
            const string done="Assets/PlayerFishing/PlayerRaftIntegration.polished.txt";if(File.Exists(done))return;
            var old=SceneManager.GetActiveScene();var main=SceneManager.GetSceneByPath(MainPath);var raft=SceneManager.GetSceneByPath(RaftPath);bool openM=!main.IsValid()||!main.isLoaded,openR=!raft.IsValid()||!raft.isLoaded;
            if(!openM&&main.isDirty||!openR&&raft.isDirty)throw new InvalidOperationException("请先保存场景，装置渲染修正未覆盖未保存修改。");
            if(openM)main=EditorSceneManager.OpenScene(MainPath,OpenSceneMode.Additive);if(openR)raft=EditorSceneManager.OpenScene(RaftPath,OpenSceneMode.Additive);
            try{
                var station=All<RaftMarketStation>(raft).Single();ConfigurePropRendering(station);station.EnsureFonts();
                var view=All<PlayerMarketView>(main).Single();var trade=Child(view.Canvas.transform,"04_Trading");
                foreach(var text in trade.GetComponentsInChildren<Text>(true)){
                    var r=text.rectTransform;float s=r.localScale.x;if(Mathf.Abs(s-r.localScale.y)>.001f||Mathf.Abs(s-1)<.001f)continue;
                    r.sizeDelta*=s;r.localScale=Vector3.one;text.fontSize=Mathf.RoundToInt(text.fontSize*s);text.resizeTextMinSize=Mathf.RoundToInt(text.resizeTextMinSize*s);text.resizeTextMaxSize=Mathf.RoundToInt(text.resizeTextMaxSize*s);
                }
                EditorUtility.SetDirty(station);EditorSceneManager.MarkSceneDirty(main);EditorSceneManager.MarkSceneDirty(raft);EditorSceneManager.SaveScene(main);EditorSceneManager.SaveScene(raft);AssetDatabase.SaveAssets();
                File.WriteAllText(done,"Props inherit the existing raft rendering layer; labels compensate for the mirrored ocean viewport.\n");File.AppendAllText(Report,"PropCameraVisibility=True\nExistingRaftUnmodifiedByPolish=True\n");Preview(main,raft,"tools/PlayerRaftIntegration-preview.png");
            }finally{if(openM)EditorSceneManager.CloseScene(main,true);if(openR)EditorSceneManager.CloseScene(raft,true);if(old.IsValid()&&old.isLoaded)SceneManager.SetActiveScene(old);}
        }
        static void BuildDialog(RaftMarketStation station,Transform parent)
        {
            var root=Box(parent,"捕捞观测对话框 · 可编辑 UI",0,0,0,0,new Color(.01f,.03f,.05f,.7f)).rectTransform;root.anchorMin=Vector2.zero;root.anchorMax=Vector2.one;root.offsetMin=root.offsetMax=Vector2.zero;root.GetComponent<Image>().raycastTarget=true;
            var canvas=root.gameObject.AddComponent<Canvas>();canvas.overrideSorting=true;canvas.sortingOrder=400;root.gameObject.AddComponent<GraphicRaycaster>();
            var panel=Box(root,"Panel · 调整此面板布局",0,0,730,438,Panel).rectTransform;panel.anchorMin=panel.anchorMax=panel.pivot=new Vector2(.5f,.5f);panel.anchoredPosition=Vector2.zero;
            station.DialogTitle=Label(panel,"Title","海况观测站",28,18,610,45,28);station.DialogBody=Label(panel,"Body","",28,79,674,158,22);station.DialogBody.alignment=TextAnchor.UpperLeft;
            station.CloseButton=Button(panel,"Close","关闭",602,20,100,42);
            var choices=Rect(panel,"Destination · 仅木筏开市前可选",28,250,674,56);station.DestinationChoices=choices.gameObject;station.DestinationButtons=new Button[3];station.DestinationLabels=new Text[3];
            for(int i=0;i<3;i++){var b=Button(choices,"Water_"+i,PlayerMarket.MapNames[i],i*228,0,218,54);station.DestinationButtons[i]=b;station.DestinationLabels[i]=b.GetComponentInChildren<Text>();}
            station.DialogStatus=Label(panel,"Status","地点在木筏上确定，进入捕捞后不再切换。",28,338,674,65,17);station.Dialog=root.gameObject;root.gameObject.SetActive(false);
        }
        static void Validate(Scene main,Scene raft)
        {
            var c=All<PlayerMarketController>(main).Single();var v=c.View;var station=All<RaftMarketStation>(raft).Single();
            if(!c.UseRaftPreparation||!c.DemoNetAvailable||v.RadioExpanded.activeSelf||v.MapEstimates.Length!=0||v.DivePanel)throw new Exception("Invalid main migration state");
            if(v.Buttons.Any(b=>!b.Target)||v.Texts.Any(t=>!t.Target))throw new Exception("Dangling UI bindings");
            foreach(string prefix in new[]{"02_Exploration","05_Island","06_Rumor","11_Survey"})if(Child(v.Canvas.transform,prefix))throw new Exception("Retired panel remains: "+prefix);
            foreach(string prefix in new[]{"03_Market","04_Trading"})if(Child(v.Canvas.transform,prefix).localScale!=Vector3.one)throw new Exception("Text remains nonuniformly scaled");
            if(station.Targets.Length!=4||station.Targets.Any(t=>!t.GetComponent<Collider>()||t.Station!=station)||!station.OceanViewport||!station.Dialog||station.Dialog.activeSelf)throw new Exception("Invalid raft equipment");
        }
        static void Preview(Scene main,Scene raft,string path)
        {
            var cam=All<Camera>(main).First();var station=All<RaftMarketStation>(raft).Single();station.EnsureFonts();
            // Editing preview only; all changed render modes and targets are restored without saving again.
            station.OceanCamera.Render();var canvases=All<Canvas>(main).Concat(All<Canvas>(raft)).Where(v=>v.isRootCanvas&&v.gameObject.activeInHierarchy).ToArray();
            var modes=canvases.Select(v=>v.renderMode).ToArray();var cameras=canvases.Select(v=>v.worldCamera).ToArray();var planes=canvases.Select(v=>v.planeDistance).ToArray();
            var meshes=Object.FindObjectsOfType<Renderer>().Where(r=>r.enabled).ToArray();var rt=new RenderTexture(1920,1080,24);var previous=cam.targetTexture;var active=RenderTexture.active;var image=new Texture2D(1920,1080,TextureFormat.RGB24,false);
            var view=All<PlayerMarketView>(main).Single();var bg=view.Background.color;
            try{foreach(var r in meshes)r.enabled=false;foreach(var canvas in canvases){canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=cam;canvas.planeDistance=canvas.gameObject.scene==main?1:2;}view.Background.color=new Color(bg.r,bg.g,bg.b,view.RaftBackdropOpacity);cam.targetTexture=rt;Canvas.ForceUpdateCanvases();cam.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1920,1080),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());}
            finally{for(int i=0;i<canvases.Length;i++){canvases[i].renderMode=modes[i];canvases[i].worldCamera=cameras[i];canvases[i].planeDistance=planes[i];}foreach(var r in meshes)if(r)r.enabled=true;view.Background.color=bg;cam.targetTexture=previous;RenderTexture.active=active;Object.DestroyImmediate(rt);Object.DestroyImmediate(image);}
        }
    }
}
