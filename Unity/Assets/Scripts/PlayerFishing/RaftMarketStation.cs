using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Fishing.Player
{
    /// <summary>Raft owns pre-market observations and the destination; Main borrows the same session.</summary>
    [DefaultExecutionOrder(-150)]
    [AddComponentMenu("Fishing/Player/Raft Market Station")]
    public sealed class RaftMarketStation : MonoBehaviour
    {
        public PlayerMarketSettings Settings;
        public string SaveKey="Fishing.PlayerMarket.Unity.v1";
        public bool UsePersistentSave=true;
        [Header("目的地由木筏决定；开市后锁定，不能通过往返重抽")]
        [Range(0,2)] public int SelectedWater;
        public bool DemoNetAvailable=true;
        [Tooltip("小岛玩法未接入时使用清洁度 80、培育 0。正式同步时关闭此开关。")]
        public bool UseIslandDefaults=true;
        public int NewGameSeed;
        [Header("现有木筏引用（不改动其布局/浮力/天空）")]
        public RaftFishingFlow Flow;
        public Camera OceanCamera;
        public RawImage OceanViewport;
        public RaftObservationTarget[] Targets;
        [Header("已实体化的装置与 UI")]
        public Transform WindPointer,WindRotor,TemperatureColumn;
        public TextMesh WindReading,TemperatureReading;
        public TextMesh[] PropLabels;
        public GameObject Dialog;
        public Text DialogTitle,DialogBody,DialogStatus;
        public Button CloseButton;
        public GameObject DestinationChoices;
        public Button[] DestinationButtons;
        public Text[] DestinationLabels;
        public float InspectDistance=3f;
        public PlayerMarket Market { get; private set; }
        public string ActiveSaveKey { get; private set; }
        public static bool DialogVisible { get; private set; }
        RaftObservationKind currentKind;
        bool bindingsReady,oldDiveEnabled,oldWeatherEnabled,ownsInputLock;
        Vector3 columnScale,columnPosition;
        Quaternion windRest;

        public PlayerMarket AcquireMarket()
        {
            if(Market!=null)return Market;
            if(!Settings)throw new InvalidOperationException("Raft observation station has no market settings");
            ActiveSaveKey=string.IsNullOrEmpty(PlayerMarketController.TestSaveKeyOverride)?SaveKey:PlayerMarketController.TestSaveKeyOverride;
            uint seed=NewGameSeed!=0?unchecked((uint)NewGameSeed):unchecked((uint)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            Market=new PlayerMarket(seed,Settings.Rules);
            bool restored=false;
            if(UsePersistentSave&&PlayerPrefs.HasKey(ActiveSaveKey)){
                try{Market=new PlayerMarket(PlayerMarketSave.Decode(PlayerPrefs.GetString(ActiveSaveKey)),Settings.Rules);restored=true;}
                catch(Exception ex){PlayerPrefs.SetString(ActiveSaveKey+".recovery",PlayerPrefs.GetString(ActiveSaveKey));Debug.LogWarning("Raft market save preserved for recovery: "+ex.Message,this);}
            }
            if(Market.S.phase=="preopen"&&!restored)Market.ChooseMap(SelectedWater);else SelectedWater=Market.S.mapIndex;
            if(DemoNetAvailable)Market.S.hasNet=true;
            ApplyPreparationDefaults();
            return Market;
        }
        public void ApplyPreparationDefaults() { if(Market!=null&&Market.S.phase=="preopen"&&UseIslandDefaults){Market.S.clean=80;Market.S.cultivation=0;} }
        void Awake()
        {
            AcquireMarket();EnsureFonts();if(Dialog)Dialog.SetActive(false);DialogVisible=false;
            if(CloseButton)CloseButton.onClick.AddListener(Close);
            for(int i=0;i<DestinationButtons.Length;i++){int index=i;if(DestinationButtons[i])DestinationButtons[i].onClick.AddListener(()=>SelectDestination(index));}
            if(TemperatureColumn){columnScale=TemperatureColumn.localScale;columnPosition=TemperatureColumn.localPosition;}
            if(WindPointer)windRest=WindPointer.localRotation;
            bindingsReady=true;RefreshReadings();Save();
        }
        public void EnsureFonts()
        {
            if(Dialog)foreach(var text in Dialog.GetComponentsInChildren<Text>(true))if(!text.font||text.font.name=="PlayerChinese")text.font=FontUtil.Get();
            foreach(var text in GetComponentsInChildren<TextMesh>(true)){
                text.font=FontUtil.Get();var renderer=text.GetComponent<MeshRenderer>();if(renderer)renderer.sharedMaterial=text.font.material;
            }
        }
        void Update()
        {
            if(!bindingsReady)return;
            RefreshReadings();
            if(DialogVisible){RefreshDialog();if(Input.GetKeyDown(KeyCode.Escape))Close();return;}
            if(Input.GetKeyDown(KeyCode.F)){
                if(Flow&&Flow.PlayerBridge){RaftObservationTarget nearest=null;float distance=InspectDistance;
                    foreach(var target in Targets)if(target){float d=Vector3.Distance(target.transform.position,Flow.PlayerBridge.transform.position);if(d<distance){distance=d;nearest=target;}}
                    if(nearest)nearest.Inspect();
                }
            }
            if(Input.GetMouseButtonDown(0)){
                var main=FindObjectOfType<PlayerMarketController>();if(main&&main.BlocksRaftInput(Input.mousePosition))return;
                if(IsButtonUnderPointer())return;
                Ray ray;if(!TryScreenRay(Input.mousePosition,out ray))return;
                // The ocean/raft has other colliders in front; inspect only explicitly tagged equipment.
                var hits=Physics.RaycastAll(ray,2000,~0,QueryTriggerInteraction.Collide);Array.Sort(hits,(a,b)=>a.distance.CompareTo(b.distance));
                foreach(var hit in hits){var target=hit.collider.GetComponentInParent<RaftObservationTarget>();if(target&&target.Station==this){target.Inspect();break;}}
            }
        }
        bool IsButtonUnderPointer()
        {
            if(!EventSystem.current)return false;var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=Input.mousePosition},hits);
            foreach(var hit in hits)if(hit.gameObject.GetComponentInParent<Button>()||hit.gameObject.GetComponentInParent<InputField>())return true;return false;
        }
        public bool TryScreenRay(Vector2 screen,out Ray ray)
        {
            ray=default;if(!OceanCamera||!OceanViewport)return false;
            var canvas=OceanViewport.canvas;Camera uiCamera=canvas&&canvas.renderMode!=RenderMode.ScreenSpaceOverlay?canvas.worldCamera:null;
            Vector2 local;if(!RectTransformUtility.ScreenPointToLocalPointInRectangle(OceanViewport.rectTransform,screen,uiCamera,out local))return false;
            var rect=OceanViewport.rectTransform.rect;float x=(local.x-rect.xMin)/rect.width,y=(local.y-rect.yMin)/rect.height;if(x<0||x>1||y<0||y>1)return false;
            var uv=OceanViewport.uvRect;x=uv.x+x*uv.width;y=uv.y+y*uv.height;
            ray=OceanCamera.ViewportPointToRay(new Vector3(x,y,0));return true;
        }
        public string SelectDestination(int index)
        {
            if(RaftFishingFlow.FishingActive)return "请先返回木筏再选择下一场水域";
            var error=AcquireMarket().ChooseMap(index);if(error!=""){if(DialogStatus)DialogStatus.text="本场水域已锁定，请收市后再选择。";return error;}
            SelectedWater=index;Save();RefreshReadings();RefreshDialog();return "";
        }
        // The future underwater extraction scene can pass its actual findings here. No free survey or RNG roll.
        public string ReceiveUnderwaterFindings(int index,bool acquiredNet,double estimate,double quality,int samples)
        {
            var market=AcquireMarket();if(market.Live)return "本场进行中，调查结果请在收市后交接";
            if(index<0||index>2||!PlayerMarket.Finite(estimate)||estimate<=0||!PlayerMarket.Finite(quality)||samples<1)return "无效调查数据";
            market.S.maps[index].survey=new MarketSurvey{estimate=estimate,quality=PlayerMarket.Clamp(quality,0,1),samples=samples};if(acquiredNet)market.S.hasNet=true;Save();return "";
        }
        public string ReceiveIslandState(int cleanliness,int cultivation)
        {
            var market=AcquireMarket();if(market.Live)return "本场结束后再同步岛屿生态";
            UseIslandDefaults=false;market.S.clean=Mathf.Clamp(cleanliness,0,100);market.S.cultivation=Math.Max(0,cultivation);Save();return "";
        }
        public void Open(RaftObservationKind kind)
        {
            if(!Dialog)return;
            // Do not interrupt the existing raft entry/exit animation or its confirmation dialog.
            if(Flow&&!Flow.CanInspectMarketEquipment)return;
            currentKind=kind;AcquireMarket().Pause();
            if(Market.S.phase=="preopen"){if(kind==RaftObservationKind.Rumor)Market.AskNpc();else Market.Observe();}
            if(!ownsInputLock&&Flow){oldDiveEnabled=Flow.DivePoint&&Flow.DivePoint.enabled;oldWeatherEnabled=Flow.Weather&&Flow.Weather.enabled;
                if(Flow.DivePoint)Flow.DivePoint.enabled=false;if(Flow.Weather)Flow.Weather.enabled=false;ownsInputLock=true;}
            Dialog.SetActive(true);DialogVisible=true;RefreshDialog();Save();
        }
        public void Close()
        {
            if(Dialog)Dialog.SetActive(false);DialogVisible=false;
            if(ownsInputLock&&Flow){if(Flow.DivePoint)Flow.DivePoint.enabled=oldDiveEnabled;if(Flow.Weather)Flow.Weather.enabled=oldWeatherEnabled;}ownsInputLock=false;Save();
        }
        void RefreshReadings()
        {
            if(Market==null)return;var w=Market.Map.weather;
            if(WindReading)WindReading.text=w.wind.ToString("F1")+" m/s";
            if(TemperatureReading)TemperatureReading.text=w.temp.ToString("F1")+" °C";
            if(WindPointer)WindPointer.localRotation=windRest*Quaternion.Euler(0,(float)w.wind*13+Mathf.Sin(Time.unscaledTime*.7f)*7,0);
            if(WindRotor)WindRotor.Rotate(Vector3.up,(float)w.wind*70*Time.unscaledDeltaTime,Space.Self);
            if(TemperatureColumn){float f=Mathf.Clamp01((float)w.temp/34);var scale=columnScale;scale.y=columnScale.y*Mathf.Max(.025f,f);TemperatureColumn.localScale=scale;TemperatureColumn.localPosition=columnPosition+Vector3.down*(columnScale.y-scale.y)*.5f;}
            if(OceanCamera){foreach(var label in PropLabels)Face(label);Face(WindReading);Face(TemperatureReading);}
        }
        void Face(TextMesh text)
        {
            if(!text||!OceanCamera)return;text.transform.rotation=OceanCamera.transform.rotation;
            // Unified raft uses a mirrored RenderTexture; compensate labels only, not the actual prop.
            var scale=text.transform.localScale;float mirror=OceanViewport?OceanViewport.uvRect.width*OceanViewport.rectTransform.lossyScale.x:1;
            scale.x=Mathf.Abs(scale.x)*(mirror<0?-1:1);text.transform.localScale=scale;
        }
        void RefreshDialog()
        {
            if(!Dialog||!Dialog.activeSelf||Market==null)return;var map=Market.Map;var w=map.weather;
            string weather=PlayerMarket.WeatherNames[w.type];
            if(DialogTitle)DialogTitle.text=currentKind==RaftObservationKind.Wind?"风向标":currentKind==RaftObservationKind.Temperature?"水下温度计":currentKind==RaftObservationKind.Rumor?"渔讯通讯器 · 老渔民":"海况观测站";
            if(DialogBody){
                if(currentKind==RaftObservationKind.Wind)DialogBody.text=map.name+"\n\n风速  "+w.wind.ToString("F1")+" m/s\n天气  "+weather;
                else if(currentKind==RaftObservationKind.Temperature)DialogBody.text=map.name+"\n\n水温  "+w.temp.ToString("F1")+" ℃\n探头悬挂在木筏下方，读数由甲板端传回。";
                else if(currentKind==RaftObservationKind.Rumor)DialogBody.text=map.name+"\n\n"+(map.signal>.35?"“最近有鱼群聚集的迹象，消息不一定准。”":map.signal<-.35?"“鱼群似乎在往外游，多留个心眼。”":"“鱼汛不明，多看看风和水温。”");
                else DialogBody.text="目标水域："+map.name+"\n\n天气  "+weather+"     水温  "+w.temp.ToString("F1")+" ℃\n风速  "+w.wind.ToString("F1")+" m/s";
            }
            if(DestinationChoices)DestinationChoices.SetActive(currentKind==RaftObservationKind.Station);
            for(int i=0;i<DestinationButtons.Length;i++){if(DestinationButtons[i])DestinationButtons[i].interactable=Market.S.phase=="preopen"&&!RaftFishingFlow.FishingActive;if(i<DestinationLabels.Length&&DestinationLabels[i])DestinationLabels[i].text=PlayerMarket.MapNames[i]+(i==Market.S.mapIndex?" · 当前":"");}
            if(DialogStatus)DialogStatus.text=Market.Live?"捕捞已暂停；本场水域锁定。关闭后可在交易面板继续。":RaftFishingFlow.FishingActive?"已进入捕捞；如需更换地点，请先返回木筏。":"地点在木筏上确定，进入捕捞后不再切换。";
        }
        public void Save()
        {
            if(!UsePersistentSave||Market==null||string.IsNullOrEmpty(ActiveSaveKey))return;
            try{PlayerPrefs.SetString(ActiveSaveKey,PlayerMarketSave.Encode(Market.S));PlayerPrefs.Save();}catch(Exception e){Debug.LogWarning("Raft observations could not be saved: "+e.Message,this);}
        }
        void OnApplicationPause(bool paused) { if(paused)Save(); }
        void OnDestroy() { if(Dialog&&Dialog.activeSelf)Close();Save(); }
    }
}
