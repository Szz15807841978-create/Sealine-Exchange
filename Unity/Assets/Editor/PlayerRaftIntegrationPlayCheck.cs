using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
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
    // Uses saved scenes unchanged, and restores the user's edit-mode scene setup after validation.
    [InitializeOnLoad]
    public static class PlayerRaftIntegrationPlayCheck
    {
        const string MainPath="Assets/Scenes/Main.unity",RaftPath="Assets/Scenes/Current/DivePointRealisticOceanSky_Unified.unity";
        const string Key="Fishing.RaftIntegrationCheck",SaveKey=Key+".Temporary",Request="tools/PlayerRaftPlayCheck.request",Report="tools/PlayerRaftPlayCheck.txt";
        [Serializable] class Setup { public string[] paths;public bool[] loaded,active; }
        static int phase,checks;static double started;static bool finishing;static RaftMarketStation station;static RaftFishingFlow flow;
        static PlayerMarket shared;static Vector3 cameraBefore;static long fishBefore;static double cdBefore;static int dropdownFrame;static double dropdownShown;
        static readonly string[] MaterialPaths={"Assets/Art/Environment/RealisticOceanSky/GameRealisticSky.mat","Assets/Art/Environment/RealisticOceanSky/CrestOcean_GameRealistic.mat"};
        static PlayerRaftIntegrationPlayCheck()
        {
            EditorApplication.playModeStateChanged+=state=>{if(state==PlayModeStateChange.EnteredEditMode&&SessionState.GetBool(Key+".restore",false))EditorApplication.delayCall+=Restore;};
            if(!SessionState.GetBool(Key,false)&&SessionState.GetInt(Key+".materialCopy0",0)!=0)SessionState.SetBool(Key+".restore",true);
            if(SessionState.GetBool(Key+".restore",false))EditorApplication.delayCall+=Restore;
            Application.logMessageReceived+=RecordWarning;
            if(SessionState.GetBool(Key,false)){PlayerMarketController.TestSaveKeyOverride=SaveKey;started=EditorApplication.timeSinceStartup;EditorApplication.update+=Tick;}
            else EditorApplication.update+=Watch;
        }
        static void Watch()
        {
            if(!File.Exists(Request)||EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating||SessionState.GetBool(Key+".restore",false))return;
            EditorApplication.update-=Watch;
            try{Start();File.Move(Request,Request+".done-"+DateTime.Now.ToString("yyyyMMdd-HHmmss"));}
            catch(Exception ex){File.WriteAllText(Report,"NOT STARTED\n"+ex);Debug.LogException(ex);}
        }
        [MenuItem("Fishing/玩家版/验证木筏观测与现有布局（不重建场景）")]
        public static void Start()
        {
            phase=0;checks=0;finishing=false;
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play mode first");
            for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("Save open scenes before validation; no unsaved edit was discarded");
            if(!File.Exists("Assets/PlayerFishing/PlayerRaftIntegration.installed.txt"))throw new InvalidOperationException("Install the incremental scene update first");
            var setup=EditorSceneManager.GetSceneManagerSetup();SessionState.SetString(Key+".setup",JsonUtility.ToJson(new Setup{paths=setup.Select(x=>x.path).ToArray(),loaded=setup.Select(x=>x.isLoaded).ToArray(),active=setup.Select(x=>x.isActive).ToArray()}));
            SessionState.SetString(Key+".main",Hash(MainPath));SessionState.SetString(Key+".raft",Hash(RaftPath));SessionState.SetString(Key+".realSave",PlayerPrefs.GetString("Fishing.PlayerMarket.Unity.v1","__MISSING__"));
            PlayerPrefs.DeleteKey(SaveKey);PlayerMarketController.TestSaveKeyOverride=SaveKey;SessionState.SetBool(Key,true);File.WriteAllText(Report,"Saved-layout raft/main integration Play check\n");
            SessionState.SetInt(Key+".warningCount",0);
            for(int i=0;i<MaterialPaths.Length;i++){
                var material=AssetDatabase.LoadAssetAtPath<Material>(MaterialPaths[i]);if(!material)throw new Exception("Missing environment material: "+MaterialPaths[i]);
                // In-memory restoration only; tests must not leave shared sky/ocean assets changed.
                var copy=new Material(material){hideFlags=HideFlags.HideAndDontSave};
                SessionState.SetInt(Key+".materialCopy"+i,copy.GetInstanceID());SessionState.SetString(Key+".materialJson"+i,EditorJsonUtility.ToJson(material));SessionState.SetString(Key+".materialHash"+i,Hash(MaterialPaths[i]));
            }
            EditorSceneManager.OpenScene(RaftPath,OpenSceneMode.Single);started=EditorApplication.timeSinceStartup;EditorApplication.update-=Tick;EditorApplication.update+=Tick;EditorApplication.isPlaying=true;
        }
        static string Hash(string path) { using(var hash=SHA256.Create())return Convert.ToBase64String(hash.ComputeHash(File.ReadAllBytes(path))); }
        static void RecordWarning(string message,string stack,LogType type)
        {
            if(!SessionState.GetBool(Key,false))return;
            if(message.StartsWith("SendMessage cannot be called")&&stack.Contains("SkyDebugPanel")||message.Contains("referenced script")&&message.Contains("missing")||message.Contains("only one active Event System")||message.Contains("2 event systems"))SessionState.SetInt(Key+".warningCount",SessionState.GetInt(Key+".warningCount",0)+1);
        }
        static void Check(bool ok,string label) { if(!ok)throw new Exception(label);checks++;File.AppendAllText(Report,"PASS "+label+"\n"); }
        static string FlowState() { return typeof(RaftFishingFlow).GetField("_state",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(flow).ToString(); }
        static void Click(PlayerMarketController c,string key)
        {
            var b=c.View.Buttons.Single(x=>x.Action==key).Target;Check(b.gameObject.activeInHierarchy&&b.interactable,"enabled "+key);b.onClick.Invoke();
        }
        static void Enter()
        {
            flow.Weather.Rainy=true;flow.Weather.TimeOfDay=RaftTimeOfDay.Day;typeof(RaftFishingFlow).GetMethod("ShowQuestion",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(flow,null);
            Object.FindObjectsOfType<Button>().Single(b=>b.name=="开始捕捞").onClick.Invoke();
        }
        static void Tick()
        {
            if(finishing)return;
            try{
                if(EditorApplication.timeSinceStartup-started>180)throw new TimeoutException("Stage "+phase);
                if(!Application.isPlaying||!EditorApplication.isPlaying)return;
                if(phase==0){
                    station=Object.FindObjectOfType<RaftMarketStation>();if(!station||station.Market==null||Time.frameCount<5)return;flow=station.Flow;shared=station.Market;cameraBefore=flow.OceanCamera.transform.position;
                    CheckSkyReuse();
                    Check(shared.S.hasNet,"demo supplies one permanent net without a fake survey");Check(shared.S.maps.All(m=>m.survey==null),"underwater data remains unset");Check(shared.S.clean==80&&shared.S.cultivation==0,"island default variables retained");
                    Check(station.Targets.Length==4&&station.Targets.All(t=>(flow.OceanCamera.cullingMask&(1<<t.gameObject.layer))!=0),"all four physical devices visible to the existing ocean camera");
                    Physics.SyncTransforms();foreach(var target in station.Targets){
                        var viewport=flow.OceanCamera.WorldToViewportPoint(target.GetComponent<Collider>().bounds.center);var uv=station.OceanViewport.uvRect;var r=station.OceanViewport.rectTransform;var rect=r.rect;
                        var local=new Vector3(rect.xMin+(viewport.x-uv.x)/uv.width*rect.width,rect.yMin+(viewport.y-uv.y)/uv.height*rect.height,0);var screen=RectTransformUtility.WorldToScreenPoint(null,r.TransformPoint(local));Ray ray;
                        Check(station.TryScreenRay(screen,out ray)&&Physics.RaycastAll(ray,2000,~0,QueryTriggerInteraction.Collide).Any(h=>h.collider.GetComponentInParent<RaftObservationTarget>()==target),"mirrored viewport click ray reaches "+target.Kind);
                    }
                    var rng=shared.S.rng.ToArray();station.Open(RaftObservationKind.Wind);Check(station.Dialog.activeSelf&&!flow.DivePoint.enabled&&!flow.Weather.enabled,"physical observation opens modal and locks raft controls");
                    Check(station.DialogBody.text.Contains(shared.Map.weather.wind.ToString("F1")),"wind dialogue reads exact session data");station.Open(RaftObservationKind.Temperature);Check(station.DialogBody.text.Contains(shared.Map.weather.temp.ToString("F1")),"temperature dialogue reads exact session data");
                    station.Open(RaftObservationKind.Rumor);Check(shared.Map.heard&&station.DialogBody.text.Contains("“"),"3D communicator preserves NPC message");Check(rng.SequenceEqual(shared.S.rng),"observations and NPC do not reroll randomness");
                    station.Open(RaftObservationKind.Station);station.DestinationButtons[2].onClick.Invoke();Check(shared.S.mapIndex==2,"raft dialogue selects destination before Main loads");
                    if(station.Dialog.GetComponent<PaperInkUiScope>()){
                        PlayerPaperSkinSetup.Validate(station.Dialog.transform);
                        Check(station.CloseButton.GetComponent<Image>().sprite.name=="button_paper"&&station.DestinationButtons.All(b=>b.GetComponent<Image>().sprite.name=="button_paper"),"legacy Start does not replace paper observation buttons");
                        Capture("tools/PlayerPaper-dialog.png");
                    }
                    station.Close();Check(flow.DivePoint.enabled&&flow.Weather.enabled&&!RaftMarketStation.DialogVisible,"closing observations restores controls");
                    Capture("tools/PlayerRaftPlay-raft.png");Enter();phase=1;return;
                }
                if(phase==1){
                    var c=Object.FindObjectOfType<PlayerMarketController>();if(!c||FlowState()!="Playing")return;c.enabled=false;
                    Check(ReferenceEquals(c.Market,shared),"Main and raft share one market instance");Check(c.Market.S.mapIndex==2,"selected water preserved across additive load");Check(station.SelectDestination(0)!=""&&shared.S.mapIndex==2,"destination locked as soon as Main loads");
                    Check(c.View.MapEstimates.Length==0&&!c.View.DivePanel&&!c.View.TemperatureFill&&!c.View.WindNeedle,"retired Main panels leave no dangling runtime references");
                    foreach(var prefix in new[]{"03_Market","04_Trading"}){var rect=c.View.Canvas.transform.Cast<Transform>().Single(t=>t.name.StartsWith(prefix)) as RectTransform;Check(rect.localScale==Vector3.one&&Mathf.Abs(rect.anchoredPosition.x-962)<.02f&&Mathf.Abs(rect.rect.width-926.4f)<.02f,"user footprint retained with normal text ratio: "+prefix);}
                    int objectCount=c.GetComponentsInChildren<Transform>(true).Length;Check(!c.View.RadioExpanded.activeSelf,"radio starts collapsed");Click(c,"radioToggle");Check(c.View.RadioExpanded.activeSelf,"radio expands on demand");Capture("tools/PlayerRaftPlay-radio.png");Click(c,"radioToggle");
                    Check(c.GetComponentsInChildren<Transform>(true).Length==objectCount,"radio toggles use pre-authored objects");
                    if(c.View.GetComponent<PaperInkUiScope>())CheckPaperPanels(c);
                    c.View.DebugPanel.SetActive(true);dropdownFrame=Time.frameCount;phase=10;return;
                }
                if(phase==10){
                    if(Time.frameCount<=dropdownFrame)return;
                    var c=Object.FindObjectOfType<PlayerMarketController>();c.View.ShapeDropdown.Show();Canvas.ForceUpdateCanvases();
                    Check(c.View.ShapeDropdown.transform.Find("Dropdown List")!=null,"paper dropdown secondary list opens after Start");PlayerPaperSkinSetup.Validate(c.View.transform);dropdownShown=EditorApplication.timeSinceStartup;phase=11;return;
                }
                if(phase==11){
                    if(EditorApplication.timeSinceStartup-dropdownShown<.3)return;
                    var c=Object.FindObjectOfType<PlayerMarketController>();Capture("tools/PlayerPaper-dropdown.png");c.View.ShapeDropdown.Hide();c.View.DebugPanel.SetActive(false);phase=12;return;
                }
                if(phase==12){
                    var c=Object.FindObjectOfType<PlayerMarketController>();
                    shared.Credit(true,900);c.Refresh();Click(c,"max");Click(c,"start");Click(c,"pause");Check(shared.S.position.stake==1000&&shared.Map.survey==null,"capture works without removed exploration UI");Click(c,"percent25");Click(c,"collect");c.View.WeightInput.text="250";Click(c,"add");Check(shared.S.fish==0&&shared.Quote().total==1000,"T+0 partial harvest can be reinvested");
                    station.Open(RaftObservationKind.Temperature);Check(RaftMarketStation.DialogVisible&&shared.S.phase=="paused","raft observation usable over Main without running the market");station.Close();Check(!flow.DivePoint.enabled&&!flow.Weather.enabled,"observation close preserves Main input lock");
                    c.Refresh();Capture("tools/PlayerRaftPlay-main.png");fishBefore=shared.S.fish;cdBefore=shared.S.cd;Click(c,"return");Click(c,"returnConfirm");phase=2;return;
                }
                if(phase==2){
                    if(FlowState()!="Waiting"||Object.FindObjectOfType<PlayerMarketController>())return;
                    Check(!RaftFishingFlow.FishingActive&&flow.DivePoint.enabled&&flow.Weather.enabled&&!flow.PlayerBridge.FishingPose,"return restores raft input weather and pose");Check(Vector3.Distance(cameraBefore,flow.OceanCamera.transform.position)<.0001f,"camera restored after return");Check(shared.S.fish==fishBefore&&shared.S.cd==cdBefore&&shared.S.position!=null,"return preserves pending position and cooldown");
                    Check(station.SelectDestination(1)!=""&&shared.S.mapIndex==2,"return cannot switch an unsettled position's water");Enter();phase=3;return;
                }
                if(phase==3){
                    var c=Object.FindObjectOfType<PlayerMarketController>();if(!c||FlowState()!="Playing")return;c.enabled=false;Check(ReferenceEquals(c.Market,shared)&&shared.S.fish==fishBefore&&shared.S.cd==cdBefore,"re-entry restores exact shared in-progress state");
                    Click(c,"finish");Check(shared.S.phase=="closed"&&shared.S.fish-shared.S.feeDebt==100+shared.S.giftFish+shared.S.totalPL,"uniform settlement and weight conservation remain intact");Click(c,"next");Click(c,"return");Click(c,"returnConfirm");phase=4;return;
                }
                if(phase==4){
                    if(FlowState()!="Waiting"||Object.FindObjectOfType<PlayerMarketController>())return;
                    Check(station.SelectDestination(1)==""&&shared.S.mapIndex==1,"next destination becomes selectable back on raft");Check(Hash(MainPath)==SessionState.GetString(Key+".main","")&&Hash(RaftPath)==SessionState.GetString(Key+".raft",""),"Play validation did not save or overwrite either scene");
                    Check(PlayerPrefs.GetString("Fishing.PlayerMarket.Unity.v1","__MISSING__")==SessionState.GetString(Key+".realSave","__MISSING__"),"real player save untouched");SceneManager.LoadSceneAsync(MainPath,LoadSceneMode.Single);phase=20;return;
                }
                if(phase==20){
                    var c=Object.FindObjectOfType<PlayerMarketController>();if(!c||c.gameObject.scene.path!=MainPath||Object.FindObjectOfType<RaftMarketStation>())return;c.enabled=false;
                    Check(UnityEngine.EventSystems.EventSystem.current&&UnityEngine.EventSystems.EventSystem.current.gameObject.scene==c.gameObject.scene&&Object.FindObjectsOfType<UnityEngine.EventSystems.EventSystem>().Count(e=>e.isActiveAndEnabled)==1,"standalone Main activates exactly one own input system");
                    Click(c,"help");Check(c.View.HelpPanel.activeSelf,"standalone Main UI remains interactive");Click(c,"helpClose");
                    Check(Hash(MainPath)==SessionState.GetString(Key+".main","")&&Hash(RaftPath)==SessionState.GetString(Key+".raft",""),"standalone validation leaves scene files unchanged");Complete(true,"PASS: "+checks+" raft/Main integration assertions.");
                }
            }catch(Exception ex){Complete(false,ex.ToString());}
        }
        static void CheckSkyReuse()
        {
            var sky=Object.FindObjectOfType<SkyDebugPanel>(true);Check(sky!=null,"sky debug component retained");
            var root=sky.TargetCanvas.transform.Find("天空调试面板");Check(root!=null,"existing sky debug controls retained");
            var transforms=root.GetComponentsInChildren<Transform>(true);var ids=transforms.Select(t=>t.GetInstanceID()).ToArray();var layout=transforms.Select(t=>EditorJsonUtility.ToJson(t)).ToArray();
            var savedWeather=JsonUtility.ToJson(sky.Weather);var savedEnvironment=JsonUtility.ToJson(sky.Environment);bool visible=root.gameObject.activeSelf;
            int calls=0;var button=root.Find("格_0_0").GetComponent<Button>();UnityEngine.Events.UnityAction external=()=>calls++;button.onClick.AddListener(external);
            for(int i=0;i<5;i++){typeof(SkyDebugPanel).GetMethod("OnValidate",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(sky,null);typeof(SkyDebugPanel).GetMethod("EnsureBuilt",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(sky,new object[]{false});}
            Check(root.GetComponentsInChildren<Transform>(true).Select(t=>t.GetInstanceID()).SequenceEqual(ids)&&transforms.Select(t=>EditorJsonUtility.ToJson(t)).SequenceEqual(layout),"sky validation reuses exact object identities and layout");
            Check(root.gameObject.activeSelf==visible&&JsonUtility.ToJson(sky.Weather)==savedWeather&&JsonUtility.ToJson(sky.Environment)==savedEnvironment,"sky validation preserves visibility and weather/environment settings");
            sky.SetVisible(true);button.onClick.Invoke();Check(calls==1,"sky binding preserves external listeners without multiplying them");
            foreach(int time in Enumerable.Range(0,4))foreach(int rain in Enumerable.Range(0,2)){
                root.Find("格_"+time+"_"+rain).GetComponent<Button>().onClick.Invoke();Check((int)sky.Weather.TimeOfDay==time&&sky.Weather.Rainy==(rain==1),"existing sky button works: "+time+" / "+rain);
            }
            button.onClick.RemoveListener(external);JsonUtility.FromJsonOverwrite(savedWeather,sky.Weather);sky.Environment.ApplyForPreview(sky.Weather.TimeOfDay,sky.Weather.RainAmount);sky.SetVisible(visible);
            Check(station.gameObject.scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).All(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)==0),"no missing scripts remain in saved raft scene");
        }
        static void Capture(string path)
        {
            // Offscreen composition for repeatable checking; uses the existing mirrored ocean RT.
            var camera=new GameObject("Validation camera (temporary)",typeof(Camera));var cam=camera.GetComponent<Camera>();cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=Color.black;cam.orthographic=true;cam.transform.position=new Vector3(0,0,-10);
            station.OceanCamera.Render();var canvases=Object.FindObjectsOfType<Canvas>().Where(c=>c.isRootCanvas&&c.renderMode==RenderMode.ScreenSpaceOverlay).ToArray();var modes=canvases.Select(c=>c.renderMode).ToArray();var worlds=canvases.Select(c=>c.worldCamera).ToArray();var planes=canvases.Select(c=>c.planeDistance).ToArray();
            var meshes=Object.FindObjectsOfType<Renderer>().Where(r=>r.enabled).ToArray();var rt=new RenderTexture(1920,1080,24);var old=RenderTexture.active;var image=new Texture2D(1920,1080,TextureFormat.RGB24,false);
            try{foreach(var r in meshes)r.enabled=false;foreach(var canvas in canvases){canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=cam;canvas.planeDistance=canvas.gameObject.scene.name=="Main"?1:2;}cam.targetTexture=rt;Canvas.ForceUpdateCanvases();cam.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1920,1080),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());}
            finally{for(int i=0;i<canvases.Length;i++){canvases[i].renderMode=modes[i];canvases[i].worldCamera=worlds[i];canvases[i].planeDistance=planes[i];}foreach(var r in meshes)if(r)r.enabled=true;RenderTexture.active=old;Object.Destroy(camera);Object.Destroy(rt);Object.Destroy(image);}
        }
        static void CheckPaperPanels(PlayerMarketController c)
        {
            var v=c.View;PlayerPaperSkinSetup.Validate(v.transform);
            Check(v.Chart.CurveColor==PlayerPaperSkinSetup.Teal&&v.Chart.EventColor==PlayerPaperSkinSetup.Rust&&v.Positive==PlayerPaperSkinSetup.Teal&&v.Negative==PlayerPaperSkinSetup.Rust,"chart and live income use paper-readable ink palette");
            Check(v.WeightInput.customCaretColor&&v.WeightInput.caretColor==PlayerPaperSkinSetup.Ink,"input caret is visible on paper");
            var panels=new[]{v.HelpPanel,v.DebugPanel,v.ReturnPanel,v.ResetPanel,v.ResultPanel};var names=new[]{"help","debug","return","reset","result"};
            for(int i=0;i<panels.Length;i++){
                var p=panels[i];bool active=p.activeSelf;p.SetActive(true);Canvas.ForceUpdateCanvases();
                var surface=p.GetComponentsInChildren<Image>(true).First(im=>im.sprite&&im.sprite.name=="panel_paper");
                Check(surface.color.a==1&&surface.fillCenter,"solid paper secondary panel: "+names[i]);
                Capture("tools/PlayerPaper-"+names[i]+".png");
                p.SetActive(active);
            }
            bool challenge=v.ChallengePanel.activeSelf,timing=v.TimingPanel.activeSelf,speed=v.SpeedPanel.activeSelf;
            v.ChallengePanel.SetActive(true);v.TimingPanel.SetActive(true);v.SpeedPanel.SetActive(false);Capture("tools/PlayerPaper-timing.png");
            v.TimingPanel.SetActive(false);v.SpeedPanel.SetActive(true);Capture("tools/PlayerPaper-speed.png");
            v.ChallengePanel.SetActive(challenge);v.TimingPanel.SetActive(timing);v.SpeedPanel.SetActive(speed);
            Check(v.TimingSegments.All(i=>!i.sprite)&&v.SpeedFill.type==Image.Type.Filled&&v.ChallengeTimeFill.type==Image.Type.Filled,"challenge regions and dynamic fill semantics retained");
        }
        static void Complete(bool success,string detail)
        {
            finishing=true;File.AppendAllText(Report,detail+"\n");SessionState.SetBool(Key,false);EditorApplication.update-=Tick;
            if(Application.isBatchMode){EditorApplication.Exit(success?0:1);return;}
            SessionState.SetBool(Key+".restore",true);EditorApplication.isPlaying=false;EditorApplication.delayCall+=Restore;
        }
        static void Restore()
        {
            // isPlayingOrWillChangePlaymode becomes false before the native Play teardown ends.
            if(Application.isPlaying||EditorApplication.isPlayingOrWillChangePlaymode){EditorApplication.delayCall+=Restore;return;}
            if(!SessionState.GetBool(Key+".restore",false))return;
            PlayerMarketController.TestSaveKeyOverride=null;PlayerPrefs.DeleteKey(SaveKey);
            var setup=JsonUtility.FromJson<Setup>(SessionState.GetString(Key+".setup",""));if(setup!=null){var scenes=new SceneSetup[setup.paths.Length];for(int i=0;i<scenes.Length;i++)scenes[i]=new SceneSetup{path=setup.paths[i],isLoaded=setup.loaded[i],isActive=setup.active[i]};EditorSceneManager.RestoreSceneManagerSetup(scenes);}
            for(int i=0;i<MaterialPaths.Length;i++){
                var copy=EditorUtility.InstanceIDToObject(SessionState.GetInt(Key+".materialCopy"+i,0)) as Material;var material=AssetDatabase.LoadAssetAtPath<Material>(MaterialPaths[i]);
                if(!copy||!material)throw new Exception("Environment material restoration unavailable: "+MaterialPaths[i]);
                material.CopyPropertiesFromMaterial(copy);
                Check(EditorJsonUtility.ToJson(material)==SessionState.GetString(Key+".materialJson"+i,""),"shared environment material restored: "+material.name);
                Check(Hash(MaterialPaths[i])==SessionState.GetString(Key+".materialHash"+i,""),"environment material file unchanged: "+material.name);
                Object.DestroyImmediate(copy);SessionState.EraseInt(Key+".materialCopy"+i);
            }
            SessionState.SetBool(Key+".restore",false);
            Check(SessionState.GetInt(Key+".warningCount",0)==0,"no sky lifecycle, missing-script or duplicate-input warnings during Play check");
            File.AppendAllText(Report,"Edit-mode scene setup restored.\n");
        }
    }
}
