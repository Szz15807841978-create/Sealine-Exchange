using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Fishing;
using Fishing.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace FishingEditor
{
    [InitializeOnLoad]
    public static class PlayerMarketPlayCheck
    {
        const string Session="Fishing.PlayerMain.PlayCheck",TestSave="Fishing.PlayerMain.PlayCheck.Temporary";
        const string Report="tools/PlayerMainPlayCheck.txt",RaftPath="Assets/Scenes/Current/DivePointRealisticOceanSky_Unified.unity";
        static double started;
        static int phase,checks;
        static string expectedSave;
        static Vector3 cameraBefore;
        static RaftFishingFlow flow;
        static byte[] raftBytes;
        static bool finishing;
        static PlayerMarketPlayCheck()
        {
            if(SessionState.GetBool(Session,false)){PlayerMarketController.TestSaveKeyOverride=TestSave;started=EditorApplication.timeSinceStartup;EditorApplication.update+=Tick;}
        }
        public static void Start()
        {
            if(File.Exists("Assets/PlayerFishing/PlayerRaftIntegration.installed.txt"))throw new InvalidOperationException("This legacy test regenerates Main. Use PlayerRaftIntegrationPlayCheck to preserve the user's edited layout.");
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Play check starts from edit mode");
            PlayerMarketSceneBuilder.BuildFromCommandLine();
            EditorSceneManager.OpenScene("Assets/Scenes/Main.unity",OpenSceneMode.Single);
            PlayerPrefs.DeleteKey(TestSave);File.WriteAllText(Report,"Player Main actual Play-mode check\n");SessionState.SetBool(Session,true);SessionState.SetString(Session+".originalSave",PlayerPrefs.GetString("Fishing.PlayerMarket.Unity.v1","__MISSING__"));
            PlayerMarketController.TestSaveKeyOverride=TestSave;EditorApplication.update-=Tick;EditorApplication.update+=Tick;started=EditorApplication.timeSinceStartup;EditorApplication.isPlaying=true;
        }
        static void Check(bool condition,string message)
        {
            if(!condition)throw new Exception(message);checks++;File.AppendAllText(Report,"PASS "+message+"\n");
        }
        static PlayerMarketController Controller() { return Object.FindObjectOfType<PlayerMarketController>(); }
        static void Click(PlayerMarketController c,string key)
        {
            var b=c.View.Buttons.First(v=>v.Action==key).Target;Check(b.gameObject.activeInHierarchy&&b.interactable,"enabled action "+key);b.onClick.Invoke();
        }
        static void Tick()
        {
            if(finishing)return;
            try{
                if(EditorApplication.timeSinceStartup-started>180)throw new TimeoutException("Play check stalled at phase "+phase);
                if(!EditorApplication.isPlaying||EditorApplication.isPlayingOrWillChangePlaymode&&!Application.isPlaying)return;
                if(phase==0){var c=Controller();if(!c||c.Market==null)return;MainChecks(c);phase=1;return;}
                if(phase==1){flow=Object.FindObjectOfType<RaftFishingFlow>();if(!flow||!flow.Weather||!flow.PlayerBridge||!flow.FishingZone)return;
                    Check(!Controller(),"standalone Main returns to the existing raft scene");raftBytes=File.ReadAllBytes(RaftPath);cameraBefore=flow.OceanCamera.transform.position;
                    Check(RaftFishingFlow.CanStartFishing(true,RaftTimeOfDay.Day)&&!RaftFishingFlow.CanStartFishing(false,RaftTimeOfDay.Day)&&!RaftFishingFlow.CanStartFishing(true,RaftTimeOfDay.Afternoon),"existing rainy-morning gate retained");
                    flow.Weather.Rainy=true;flow.Weather.TimeOfDay=RaftTimeOfDay.Day;
                    typeof(RaftFishingFlow).GetMethod("ShowQuestion",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(flow,null);
                    var confirm=Object.FindObjectsOfType<Button>().First(b=>b.name=="开始捕捞");confirm.onClick.Invoke();phase=2;return;
                }
                if(phase==2){if(!Controller()||FlowState()!="Playing")return;var c=Controller();c.enabled=false;
                    Check(RaftFishingFlow.FishingActive&&SceneManager.GetSceneByName("Main").isLoaded&&SceneManager.GetSceneByPath(RaftPath).isLoaded,"real raft confirmation additively loads Player Main");
                    Check(!Object.FindObjectOfType<GameController>()&&Object.FindObjectsOfType<PlayerMarketController>().Length==1,"no legacy or duplicate controller");
                    // Compare the actual Unity JSON roundtrip, not the source decimal spelling: JsonUtility
                    // can parse a double one ULP away while preserving all integer weights and RNG words.
                    string expectedRoundtrip=PlayerMarketSave.Encode(PlayerMarketSave.Decode(expectedSave));
                    Check(c.Market.S.phase=="paused"&&PlayerMarketSave.Encode(c.Market.S)==expectedRoundtrip,"return and re-entry preserve position, fees, RNG and cooldown through Unity JSON");
                    Check(c.View.Canvas.sortingOrder==200&&c.View.Background.color.a<.3f,"Main remains an overlay on the preserved raft background");
                    Check(SceneManager.GetSceneByName("Main").GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Camera>()).All(cam=>!cam.enabled),"additive Main camera disabled");
                    Check(Object.FindObjectsOfType<EventSystem>().Count(es=>es.enabled)==1,"one enabled event system in additive gameplay");
                    Check(flow.PlayerBridge.FishingPose&&!flow.DivePoint.enabled&&!flow.Weather.enabled,"raft character fishing pose and input lock retained");
                    Screenshot(c,"tools/PlayerMain-play-overlay.png",true);
                    Check(c.View.Canvas.renderMode==RenderMode.ScreenSpaceOverlay,"preview restores the live overlay render mode");
                    Click(c,"return");Click(c,"returnConfirm");phase=3;return;
                }
                if(phase==3){if(!flow||FlowState()!="Waiting"||Controller())return;
                    Check(!SceneManager.GetSceneByName("Main").isLoaded&&!RaftFishingFlow.FishingActive,"return unloads Main only");
                    Check(flow.DivePoint.enabled&&flow.Weather.enabled&&!flow.PlayerBridge.FishingPose,"raft controls, weather and pose restored");
                    Check(Vector3.Distance(cameraBefore,flow.OceanCamera.transform.position)<.0001f,"raft camera returns to original position");
                    Check(File.ReadAllBytes(RaftPath).SequenceEqual(raftBytes),"raft scene file byte-for-byte unchanged by full round trip");
                    Check(PlayerPrefs.GetString("Fishing.PlayerMarket.Unity.v1","__MISSING__")==SessionState.GetString(Session+".originalSave","__MISSING__"),"real player save never touched by validation");
                    Complete(true,"PASS: "+checks+" Play-mode assertions. Main/raft round trip succeeded.");
                }
            }catch(Exception e){Complete(false,e.ToString());}
        }
        static string FlowState() { return flow?typeof(RaftFishingFlow).GetField("_state",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(flow).ToString():""; }
        static void MainChecks(PlayerMarketController c)
        {
            c.enabled=false;var v=c.View;var e=c.Market;var s=e.S;
            Check(!Object.FindObjectOfType<GameController>(),"standalone Main has no legacy runtime auto-build");
            Check(c.GetComponentsInChildren<Transform>(true).Length>=300,"scene-authored hierarchy is present before interactions");
            Check(v.Chart.GetComponent<CanvasRenderer>()!=null,"chart renderer exists");
            Check(!v.DebugPanel.activeSelf&&!v.ChallengePanel.activeSelf,"debug/challenge default hidden");
            var saved=PlayerMarketSave.Decode(PlayerMarketSave.Encode(s));Check(saved.position==null&&saved.gauge==null&&saved.shape==null&&saved.result==null&&saved.maps.All(m=>m.survey==null),"Unity JSON preserves absent optional state");
            int count=c.GetComponentsInChildren<Transform>(true).Length;
            var edited=v.WeightInput.GetComponent<RectTransform>();var before=edited.anchoredPosition;edited.anchoredPosition+=new Vector2(7,0);c.Refresh();Check(edited.anchoredPosition==before+new Vector2(7,0),"refresh does not overwrite user-edited layout");edited.anchoredPosition=before;
            for(int i=0;i<3;i++){Click(c,"map"+i);Click(c,"survey");Click(c,"diveDown");Click(c,"diveScan");Click(c,"diveReturn");}Check(s.maps.All(m=>m.survey!=null)&&s.hasNet,"real survey buttons unlock three maps and permanent net");
            Click(c,"observe");Click(c,"npc");Click(c,"cultivate");for(int i=0;i<5;i++)Click(c,"logo");Check(v.DebugPanel.activeSelf,"five-tap secret entry opens debug");v.DebugFishInput.text="900";Click(c,"giveFish");Check(s.fish==1000&&s.totalPL==0,"hidden debug supply excludes gifts from profit");v.RevealToggle.isOn=true;c.Refresh();Check(v.Texts.First(t=>t.Key=="debugSecrets").Target.text.Contains("60"),"debug-only difficulty values available");Click(c,"debugClose");
            string visible=string.Join("\n",v.GetComponentsInChildren<Text>().Select(t=>t.text));Check(!Regex.IsMatch(visible,"最适|增益|形态|调试|σ|公式"),"player-visible scene does not expose backstage factors");
            Screenshot(c,"tools/PlayerMain-play-preopen.png");
            Click(c,"max");Click(c,"start");Click(c,"pause");Check(s.fish==0&&s.position.stake==1000,"all-in cast only charges principal");Click(c,"percent50");Click(c,"collect");double cd=s.cd;v.WeightInput.text="500";Click(c,"add");Check(s.fish==0&&e.Quote().total==1000&&s.cd==cd,"real UI T+0 partial proceeds immediately reinvest");Check(!v.Buttons.First(b=>b.Action=="collect").Target.interactable,"next harvest still cooldown-gated");
            s.gaugeIndex=2;e.TogglePause();e.Advance(6);e.Pause();c.Refresh();Click(c,"percent100");Click(c,"collect");Click(c,"max");Click(c,"cast");Check(s.position!=null&&s.cd>0,"real UI immediately recasts after full harvest");
            s.gaugeIndex=0;e.OpenGauge();c.Refresh();Check(v.TimingPanel.activeInHierarchy,"timing challenge visible");e.TogglePause();s.gauge.pos=s.gauge.layout.First(g=>g.kind==0).from+1;c.Refresh();Click(c,"gaugeStop");Check(s.gauge.verdict==0,"real stop button judges gold region");e.Advance(2);e.Pause();e.OpenGauge();c.Refresh();Check(v.SpeedPanel.activeInHierarchy,"second challenge is A/D");Click(c,"pause");for(int i=0;i<60;i++)Click(c,i%2==0?"speedA":"speedD");Check(s.gauge.count==60,"60 real button presses count alternation");e.Advance(6);e.Pause();c.Refresh();Check(Math.Abs(s.gauge.gain-.18)<1e-10,"harder full reward");Check(!v.Texts.First(t=>t.Key=="speedGrade").Target.text.Contains("%"),"challenge result is qualitative");Screenshot(c,"tools/PlayerMain-play-speed.png");
            var restored=new PlayerMarket(PlayerMarketSave.Decode(PlayerMarketSave.Encode(s)),e.Rules);
            Check(restored.S.gauge.count==60&&restored.S.gauge.judged,"active challenge survives Unity JSON roundtrip");
            Check(Math.Abs(restored.S.position.units-s.position.units)<=Math.Max(1,Math.Abs(s.position.units))*1e-14&&restored.Quote().total==e.Quote().total&&restored.Quote().pl==e.Quote().pl,"position roundtrip preserves integer weight and profit despite JSON double rounding");
            e.TogglePause();e.Advance(2);e.Pause();c.Refresh();Click(c,"finish");Check(s.fish-s.feeDebt==100+s.giftFish+s.totalPL,"native UI settlement conserves weight");Screenshot(c,"tools/PlayerMain-play-result.png");
            Check(c.GetComponentsInChildren<Transform>(true).Length==count,"no runtime UI objects created during core gameplay");
            Click(c,"next");e.DebugSurvey();s.stakeDraft=Math.Min(100,s.fish);c.Refresh();Click(c,"start");Click(c,"pause");Click(c,"percent25");Click(c,"collect");expectedSave=PlayerMarketSave.Encode(s);c.Save();
            Click(c,"return");Click(c,"returnConfirm");
        }
        static void Screenshot(PlayerMarketController c,string path,bool includeRaft=false)
        {
            var cam=c.gameObject.scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Camera>(true)).First();
            // Batch Unity has no presented Game view. Composite its root UI canvases offscreen,
            // including the raft's existing ocean RenderTexture; never move or save raft objects.
            var canvases=includeRaft?Object.FindObjectsOfType<Canvas>().Where(v=>v.isRootCanvas&&v.renderMode==RenderMode.ScreenSpaceOverlay).ToArray():new[]{c.View.Canvas};
            var modes=canvases.Select(v=>v.renderMode).ToArray();var worlds=canvases.Select(v=>v.worldCamera).ToArray();var planes=canvases.Select(v=>v.planeDistance).ToArray();
            if(includeRaft&&flow&&flow.OceanCamera)flow.OceanCamera.Render();
            var meshes=Object.FindObjectsOfType<Renderer>().Where(r=>r.enabled).ToArray();
            var rt=new RenderTexture(1920,1080,24);var target=cam.targetTexture;var active=RenderTexture.active;var image=new Texture2D(1920,1080,TextureFormat.RGB24,false);
            try{foreach(var r in meshes)r.enabled=false;foreach(var canvas in canvases){canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=cam;canvas.planeDistance=canvas==c.View.Canvas?1:2;}cam.targetTexture=rt;Canvas.ForceUpdateCanvases();cam.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1920,1080),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());}
            finally{for(int i=0;i<canvases.Length;i++){canvases[i].renderMode=modes[i];canvases[i].worldCamera=worlds[i];canvases[i].planeDistance=planes[i];}foreach(var r in meshes)if(r)r.enabled=true;cam.targetTexture=target;RenderTexture.active=active;Object.Destroy(rt);Object.Destroy(image);}
        }
        static void Complete(bool success,string detail)
        {
            finishing=true;File.AppendAllText(Report,detail+"\n");SessionState.SetBool(Session,false);EditorApplication.update-=Tick;
            // Exit directly in the dedicated batch process. A delayCall can be lost during Play-mode domain reload.
            if(Application.isBatchMode){EditorApplication.Exit(success?0:1);return;}
            EditorApplication.isPlaying=false;
        }
    }
}
