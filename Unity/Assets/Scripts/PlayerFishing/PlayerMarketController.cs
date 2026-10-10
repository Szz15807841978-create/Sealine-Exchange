using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Fishing.Player
{
    /// <summary>Only binds existing scene objects; never builds or lays out the player UI at runtime.</summary>
    [DefaultExecutionOrder(-100)]
    [AddComponentMenu("Fishing/Player/Market Controller")]
    public sealed class PlayerMarketController : MonoBehaviour
    {
        public PlayerMarketView View;
        public PlayerMarketSettings Settings;
        [Tooltip("独立于旧捕捞、木筏及 HTML 的存档键。")]
        public string SaveKey="Fishing.PlayerMarket.Unity.v1";
        public bool UsePersistentSave=true;
        [Tooltip("0 = 新开局使用时间种子；已有存档不重抽。")]
        public int NewGameSeed;
        [Header("木筏准备 / 搜打撤接口")]
        public bool UseRaftPreparation;
        [Tooltip("仅供搜打撤尚未接入的 Demo。正式接入后关闭，由搜打撤设置 hasNet。")]
        public bool DemoNetAvailable;
        // Test harness override is never serialized; ordinary gameplay always uses the scene's SaveKey.
        public static string TestSaveKeyOverride;
        public PlayerMarket Market { get; private set; }
        readonly Dictionary<string,Text> texts=new Dictionary<string,Text>();
        readonly Dictionary<string,Button> buttons=new Dictionary<string,Button>();
        bool bound,storageOk=true,leaving;
        double saveTimer,refreshTimer;
        float toastUntil,tapAt;int taps;
        MarketGauge renderedGauge;
        static string N(long v) { return v.ToString("N0",CultureInfo.InvariantCulture); }
        static string Signed(long v) { return (v>=0?"+":"")+N(v); }
        MarketState S { get { return Market.S; } }
        uint Seed() { return NewGameSeed!=0?unchecked((uint)NewGameSeed):unchecked((uint)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()); }
        void Awake()
        {
            // Saved disabled to avoid even a transient OnEnable registration on additive loading.
            // Standalone Main activates its own system; over the raft it shares the existing one.
            foreach(var inputSystem in GetComponentsInChildren<EventSystem>(true)){
                var current=EventSystem.current;
                inputSystem.enabled=!(current&&current!=inputSystem&&current.isActiveAndEnabled&&current.gameObject.scene!=gameObject.scene);
            }
            if(!View||!Settings){Debug.LogError("Player Main is missing serialized View/Settings.",this);enabled=false;return;}
            if(!string.IsNullOrEmpty(TestSaveKeyOverride))SaveKey=TestSaveKeyOverride;
            View.EnsureFonts();foreach(var b in View.Texts)if(b.Target)texts[b.Key]=b.Target;foreach(var b in View.Buttons)if(b.Target)buttons[b.Action]=b.Target;
            var station=UseRaftPreparation?FindObjectOfType<RaftMarketStation>():null;
            if(station){Market=station.AcquireMarket();SaveKey=station.ActiveSaveKey;}
            else Market=new PlayerMarket(Seed(),Settings.Rules);
            if(!station&&UsePersistentSave&&PlayerPrefs.HasKey(SaveKey)){
                try{var state=PlayerMarketSave.Decode(PlayerPrefs.GetString(SaveKey));Market=new PlayerMarket(state,Settings.Rules);}
                catch(Exception e){Debug.LogWarning("Player save was invalid; preserved under .recovery. "+e.Message,this);PlayerPrefs.SetString(SaveKey+".recovery",PlayerPrefs.GetString(SaveKey));}
            }
            if(UseRaftPreparation&&DemoNetAvailable)Market.S.hasNet=true;
            ApplyPreparationDefaults();
            foreach(var b in View.Buttons){string action=b.Action;if(b.Target)b.Target.onClick.AddListener(()=>Dispatch(action));}
            View.WeightInput.onEndEdit.AddListener(_=>ReadDrafts());View.BaitInput.onEndEdit.AddListener(_=>ReadDrafts());View.PercentInput.onEndEdit.AddListener(_=>ReadDrafts());
            View.WeightInput.onValueChanged.AddListener(_=>SyncTypedDrafts());View.BaitInput.onValueChanged.AddListener(_=>SyncTypedDrafts());View.PercentInput.onValueChanged.AddListener(_=>SyncTypedDrafts());
            View.CleanSlider.onValueChanged.AddListener(v=>Put("debugClean",((int)v).ToString()));
            View.ShapeDropdown.onValueChanged.AddListener(v=>Act(()=>Market.SetShape(v-1)));
            View.RevealToggle.onValueChanged.AddListener(_=>Refresh());
            View.DebugPanel.SetActive(false);View.HelpPanel.SetActive(false);Set(View.DivePanel,false);View.ReturnPanel.SetActive(false);View.ResetPanel.SetActive(false);View.ChallengePanel.SetActive(false);Set(View.RadioExpanded,false);
            if(RaftFishingFlow.FishingActive&&View.Background){var c=View.Background.color;c.a=View.RaftBackdropOpacity;View.Background.color=c;}
            bound=true;Refresh();Save();
        }
        void Update()
        {
            if(!bound||leaving)return;Keys();Market.Advance(Math.Min(.2,Time.unscaledDeltaTime));refreshTimer+=Time.unscaledDeltaTime;saveTimer+=Time.unscaledDeltaTime;
            if(refreshTimer>.09||S.gauge!=null){Refresh();refreshTimer=0;}if(saveTimer>1){Save();saveTimer=0;}
        }
        static bool Active(GameObject panel) { return panel&&panel.activeSelf; }
        static void Set(GameObject panel,bool active) { if(panel)panel.SetActive(active); }
        bool Modal { get { return Active(View.DebugPanel)||Active(View.HelpPanel)||Active(View.DivePanel)||Active(View.ReturnPanel)||Active(View.ResetPanel)||RaftMarketStation.DialogVisible; } }
        void Keys()
        {
            bool ctrl=Input.GetKey(KeyCode.LeftControl)||Input.GetKey(KeyCode.RightControl),shift=Input.GetKey(KeyCode.LeftShift)||Input.GetKey(KeyCode.RightShift);
            if(ctrl&&shift&&Input.GetKeyDown(KeyCode.D)&&!Modal){OpenDebug();return;}
            if(Input.GetKeyDown(KeyCode.Escape)&&Modal){if(Active(View.DivePanel))Act(()=>Market.DiveAction("return"));CloseModals();return;}
            if(Modal||ctrl||Input.GetKey(KeyCode.LeftAlt)||Input.GetKey(KeyCode.RightAlt))return;
            if(S.gauge!=null&&S.gauge.speed){if(Input.GetKeyDown(KeyCode.A))Market.SpeedPress(0);if(Input.GetKeyDown(KeyCode.D))Market.SpeedPress(1);}
            var selected=EventSystem.current?EventSystem.current.currentSelectedGameObject:null;
            if(selected&&selected.GetComponent<InputField>())return;
            if(Input.GetKeyDown(KeyCode.Space))Act(()=>Market.TogglePause());
            if(Input.GetKeyDown(KeyCode.Q))Act(()=>Market.JudgeTiming());
            if(Input.GetKeyDown(KeyCode.A)&&(S.gauge==null||!S.gauge.speed))Act(()=>Market.Collect(S.collectPercent));
        }
        public void Dispatch(string action)
        {
            if(!bound||leaving)return;
            if(UseRaftPreparation&&(action.StartsWith("map")||action=="survey"||action.StartsWith("dive")||action=="observe"||action=="npc"||action=="cultivate")){Toast("请在木筏、搜打撤或小岛玩法中完成准备。");return;}
            switch(action){
                case "radioToggle":Set(View.RadioExpanded,!Active(View.RadioExpanded));Refresh();return;
                case "logo":if(Time.unscaledTime-tapAt>1.5f)taps=0;tapAt=Time.unscaledTime;if(++taps>=5){taps=0;OpenDebug();}return;
                case "help":Open(View.HelpPanel);return;
                case "helpClose":case "debugClose":case "returnCancel":case "resetCancel":CloseModals();return;
                case "return":Open(View.ReturnPanel);return;
                case "returnConfirm":ReturnToRaft();return;
                case "reset":Open(View.ResetPanel);return;
                case "resetConfirm":Market.Reset(Seed());if(UseRaftPreparation&&DemoNetAvailable)S.hasNet=true;renderedGauge=null;CloseModals();Save();Refresh();return;
                case "survey":if(Act(()=>Market.BeginDive()))Open(View.DivePanel);return;
                case "diveDown":Act(()=>Market.DiveAction("down"));return;
                case "diveScan":Act(()=>Market.DiveAction("scan"));return;
                case "diveReturn":Act(()=>Market.DiveAction("return"));View.DivePanel.SetActive(false);return;
                case "observe":Act(()=>Market.Observe());return;
                case "npc":Act(()=>Market.AskNpc());return;
                case "cultivate":Act(()=>Market.Cultivate());return;
                case "max":S.stakeDraft=Math.Min(Market.Rules.MaxInput,S.fish);View.WeightInput.SetTextWithoutNotify(S.stakeDraft.ToString());Refresh();return;
                case "start":if(ReadDrafts())Act(()=>Market.Start(!UseRaftPreparation));return;
                case "pause":Act(()=>Market.TogglePause());return;
                case "cast":if(ReadDrafts())Act(()=>Market.Cast(S.stakeDraft));return;
                case "add":if(ReadDrafts())Act(()=>Market.Add(S.stakeDraft));return;
                case "collect":int percent;if(int.TryParse(View.PercentInput.text,out percent)&&percent>=1&&percent<=100){S.collectPercent=percent;Act(()=>Market.Collect(percent));}else Toast("收网比例必须为 1–100 的整数");return;
                case "finish":Act(()=>Market.Finish());return;
                case "next":if(Act(()=>Market.Next())){ApplyPreparationDefaults();Save();Refresh();}return;
                case "gaugeStop":Act(()=>Market.JudgeTiming());return;
                case "speedA":Market.SpeedPress(0);Refresh();Save();return;
                case "speedD":Market.SpeedPress(1);Refresh();Save();return;
                case "giveFish":Credit(true);return;
                case "giveBait":Credit(false);return;
                case "applyClean":Act(()=>Market.SetClean((int)View.CleanSlider.value));return;
                case "debugSurvey":Act(()=>Market.DebugSurvey());return;
                case "debugGauge":Act(()=>Market.OpenGauge());return;
                case "debugShark":Act(()=>Market.TriggerEvent(false));return;
                case "debugWeather":Act(()=>Market.TriggerEvent(true));return;
                case "debugEnd":Act(()=>Market.Finish());return;
            }
            if(action.StartsWith("map")){int index;if(int.TryParse(action.Substring(3),out index))Act(()=>Market.ChooseMap(index));}
            if(action.StartsWith("percent")){int value;if(int.TryParse(action.Substring(7),out value)){S.collectPercent=value;View.PercentInput.SetTextWithoutNotify(value.ToString());Refresh();}}
        }
        void SyncTypedDrafts()
        {
            long weight;int bait,percent;
            if(long.TryParse(View.WeightInput.text,out weight)&&weight>=0&&weight<=Market.Rules.MaxInput)S.stakeDraft=weight;
            if(S.phase=="preopen"&&int.TryParse(View.BaitInput.text,out bait)&&bait>=0&&bait<=Market.Rules.MaxInput)S.baitDraft=bait;
            if(int.TryParse(View.PercentInput.text,out percent)&&percent>=1&&percent<=100)S.collectPercent=percent;
            Refresh();
        }
        bool ReadDrafts()
        {
            long weight;int bait,percent;
            if(!long.TryParse(View.WeightInput.text,out weight)||weight<0||weight>Market.Rules.MaxInput){Toast("重量必须是有效的整数 kg");return false;}
            if(!int.TryParse(View.BaitInput.text,out bait)||bait<0||bait>Market.Rules.MaxInput){Toast("诱饵必须为整数份");return false;}
            if(!int.TryParse(View.PercentInput.text,out percent)||percent<1||percent>100){Toast("收网比例必须为 1–100 的整数");return false;}
            S.stakeDraft=weight;if(S.phase=="preopen")S.baitDraft=bait;S.collectPercent=percent;Save();Refresh();return true;
        }
        bool Act(Func<string> fn) { string error=fn();if(error!="")Toast(error);Save();Refresh();return error==""; }
        void Credit(bool fish) { long value;if(!long.TryParse(fish?View.DebugFishInput.text:View.DebugBaitInput.text,out value)){Toast("请输入整数数量");return;}if(Act(()=>Market.Credit(fish,value)))Toast("补给已到账"); }
        void Toast(string value) { Put("toast",value);toastUntil=Time.unscaledTime+3; }
        void Open(GameObject panel) { Market.Pause();CloseModals();panel.SetActive(true);Save();Refresh(); }
        void CloseModals() { View.DebugPanel.SetActive(false);View.HelpPanel.SetActive(false);View.ReturnPanel.SetActive(false);View.ResetPanel.SetActive(false);Set(View.DivePanel,false); }
        void OpenDebug() { if(Modal)return;View.CleanSlider.SetValueWithoutNotify(S.clean);View.ShapeDropdown.SetValueWithoutNotify(S.shapeChoice+1);Open(View.DebugPanel); }
        void Put(string key,string value) { Text t;if(texts.TryGetValue(key,out t)&&t.text!=value)t.text=value; }
        void StateColor(string key,bool good) { Text t;if(texts.TryGetValue(key,out t))t.color=good?View.Positive:View.Negative; }
        void Enable(string key,bool value) { Button b;if(buttons.TryGetValue(key,out b))b.interactable=value; }
        void ShowButton(string key,bool value) { Button b;if(buttons.TryGetValue(key,out b))b.gameObject.SetActive(value); }
        void ButtonText(string key,string value) { Button b;if(buttons.TryGetValue(key,out b)){var t=b.GetComponentInChildren<Text>(true);if(t&&t.text!=value)t.text=value;} }
        public void Refresh()
        {
            if(Market==null||!View)return;var m=Market.Map;bool pre=S.phase=="preopen",live=Market.Live,paused=S.phase=="paused",locked=Market.ChallengeLocked;var q=Market.Quote(S.collectPercent);var all=Market.Quote();
            Put("walletFish",N(S.fish)+" kg");Put("walletBait",N(S.bait)+" 份");Put("net",S.hasNet?(S.position!=null?"捕捞中":"可用 · 1 张"):"尚未带回");Put("lifetimePL",Signed(S.totalPL)+" kg");StateColor("lifetimePL",S.totalPL>=0);
            Put("day","第 "+S.day+" 天 · 第 "+S.session+" 场");Put("marketTitle",m.name);Put("price",S.price.ToString("F2"));Put("priceChange",((S.price-100)>=0?"+":"")+(S.price-100).ToString("F1")+"% 相对开盘");StateColor("priceChange",S.price>=100);
            Put("remaining",(Market.Rules.Duration-S.elapsed).ToString("F1")+" s");Put("phase",pre?"准备出航":locked?"挑战中":paused?"已暂停":live?"捕捞中":"已收市");
            for(int i=0;i<3;i++){var map=S.maps[i];Enable("map"+i,pre&&S.dive==null);if(i<View.MapEstimates.Length&&View.MapEstimates[i])View.MapEstimates[i].text=map.name+(i==S.mapIndex?"  <":"")+"\n"+Estimate(map);}
            Put("surveyNote",m.survey==null?"下潜采样并返回，带回情报与捕捞网。":"情报已带回，可比较其他水域再下网。");
            Put("weather",PlayerMarket.WeatherNames[m.weather.type]);Put("temperature",m.observed?m.weather.temp.ToString("F1")+" ℃":"— ℃");Put("wind",m.observed?m.weather.wind.ToString("F1")+" m/s":"— m/s");
            if(View.TemperatureFill)View.TemperatureFill.fillAmount=m.observed?(float)PlayerMarket.Clamp(m.weather.temp/34,0,1):0;if(View.WindNeedle)View.WindNeedle.localEulerAngles=new Vector3(0,0,m.observed?-(float)m.weather.wind*31:0);
            Put("npc",!m.heard?"老渔民似乎知道一些鱼群动向。":m.signal>.35?"“最近有鱼群聚集的迹象，消息不一定准。”":m.signal<-.35?"“鱼群似乎在往外游，多留个心眼。”":"“鱼汛不明，多看看风和水温。”");
            Put("clean",S.clean>65?"清澈":S.clean>30?"浑浊":"污染严重");Put("cultivation",S.cultivation==0?"尚未开始":S.cultivation<25?"刚刚起步":S.cultivation<75?"渐有生机":"持续养护");
            Enable("survey",pre);Enable("observe",pre&&!m.observed);Enable("npc",pre&&!m.heard);Enable("cultivate",!live&&S.bait>=5);
            if(!View.WeightInput.isFocused)View.WeightInput.SetTextWithoutNotify(S.stakeDraft.ToString());if(!View.BaitInput.isFocused)View.BaitInput.SetTextWithoutNotify(S.baitDraft.ToString());if(!View.PercentInput.isFocused)View.PercentInput.SetTextWithoutNotify(S.collectPercent.ToString());
            View.WeightInput.interactable=(pre||paused)&&!locked;View.BaitInput.interactable=pre;View.PercentInput.interactable=S.position!=null;Enable("max",View.WeightInput.interactable);
            Put("weightLabel",S.position!=null?"追加重量 · 本次增量 kg":"下网股本 · 整数 kg");Put("position",all==null?"—":N(all.total)+" kg");Put("return",q==null?"—":N(q.gross)+" kg");Put("remainder",q==null?"—":N(q.remaining)+" kg");
            Put("fee",(S.phase=="closed"?"本场手续费 ":"暂记手续费 ")+N(Market.PendingFee())+" kg · 收市统一交"+(S.feeDebt>0?" / 待缴 "+N(S.feeDebt)+" kg":""));
            ShowButton("start",pre);Enable("start",pre&&(UseRaftPreparation||m.survey!=null)&&S.hasNet&&S.dive==null);ShowButton("pause",live);ShowButton("cast",live&&S.position==null);ShowButton("add",live&&S.position!=null);ShowButton("collect",live);ShowButton("finish",live);
            ButtonText("pause",paused?"继续 Space":"暂停 Space");ButtonText("collect","收 "+S.collectPercent+"% · A");
            long typedWeight;int typedPercent;bool validWeight=long.TryParse(View.WeightInput.text,out typedWeight)&&Market.ValidateWeight(typedWeight)=="",validPercent=int.TryParse(View.PercentInput.text,out typedPercent)&&typedPercent>=1&&typedPercent<=100;
            Enable("cast",paused&&!locked&&S.position==null&&validWeight);Enable("add",paused&&!locked&&S.position!=null&&validWeight);Enable("collect",paused&&!locked&&S.cd<=1e-6&&q!=null&&validPercent&&(S.collectPercent==100||q.gross>=1));Enable("finish",live&&!locked);
            foreach(var p in new[]{25,50,100})Enable("percent"+p,S.position!=null);
            View.CooldownFill.fillAmount=S.cdTotal>0?(float)(S.cd/S.cdTotal):0;
            Put("tradeHint",locked?"完成挑战后解锁交易。":S.cd>0?"收网冷却 "+S.cd.ToString("F1")+" s；暂停时仍可追加或重新下网。":live?"暂停后交易；收回的鱼可以立即再次投入。":"下网仅扣本金；诱饵开市消耗一次。双边 0.5% 收市统一交。");
            View.ResultPanel.SetActive(S.phase=="closed");if(S.result!=null){Put("resultPL",Signed(S.sessionPL)+" kg");StateColor("resultPL",S.sessionPL>=0);Put("resultDetail","手续费 "+N(S.result.fees)+" kg · 实缴 "+N(S.result.paid)+" kg\n诱饵消耗 "+S.baitUsed+" 份"+(S.feeDebt>0?" · 待缴 "+N(S.feeDebt)+" kg":""));ButtonText("next",S.session==1?"准备第二场":"进入下一天");}
            RefreshGauge();RefreshDive();RefreshRows();RefreshDebug();View.Chart.Refresh(S,Market.Rules.Duration);
            Put("save",storageOk?"本机自动存档":"存档失败 · 请勿关闭");if(Time.unscaledTime>toastUntil)Put("toast","");
        }
        static string Estimate(MarketMap m)
        {
            if(m.survey==null)return "尚未调查";var q=m.survey;double error=(1-q.quality)*.5;return "鱼量 "+(q.estimate*(1-error)*100).ToString("F0")+"–"+(q.estimate*(1+error)*100).ToString("F0")+" · 相对量\n"+(q.samples>=3?"调查充分":q.samples==2?"有所了解":"初步观察");
        }
        void RefreshGauge()
        {
            var g=S.gauge;View.ChallengePanel.SetActive(g!=null);if(g==null){renderedGauge=null;return;}
            View.TimingPanel.SetActive(!g.speed);View.SpeedPanel.SetActive(g.speed);ShowButton("gaugeStop",!g.speed);Put("gaugeTitle",g.speed?"收网拼手速 · A / D":"潮流仪表盘 · Q 停针");Put("gaugeNumber",g.number+" / 2");
            View.ChallengeTimeFill.fillAmount=(float)PlayerMarket.Clamp(1-g.u/(g.speed?Market.Rules.SpeedWindow:Market.Rules.GaugeWindow),0,1);
            if(g.speed){Put("speedCount",g.count+" 次有效交替");Put("speedTime",Math.Max(0,Market.Rules.SpeedWindow-g.u).ToString("F1")+" s");Put("speedGrade",Market.SpeedGrade(g.count));StateColor("speedGrade",g.count>=Market.Rules.SpeedBreakEven);View.SpeedFill.fillAmount=(float)PlayerMarket.Clamp(g.count/(double)Market.Rules.SpeedMaxCount,0,1);Enable("speedA",S.phase=="running"&&!g.judged);Enable("speedD",S.phase=="running"&&!g.judged);Put("gaugeHint",g.judged?"判定完成 · "+Market.SpeedGrade(g.count):S.phase=="paused"?"已暂停，继续后计时。":"下一次："+(g.lastKey<0?"A 或 D":g.lastKey==0?"D":"A")+" · 长按与同键连按不计数");}
            else{
                if(renderedGauge!=g){Color[] colors={View.GaugeBest,View.GaugeGood,View.GaugeNeutral,View.Negative};for(int i=0;i<g.layout.Count&&i<View.TimingSegments.Length;i++){var seg=g.layout[i];var im=View.TimingSegments[i];var rt=im.rectTransform;rt.anchorMin=new Vector2((float)seg.from/100,0);rt.anchorMax=new Vector2((float)seg.to/100,1);rt.offsetMin=rt.offsetMax=Vector2.zero;im.color=colors[seg.kind];}renderedGauge=g;}
                View.TimingNeedle.anchorMin=new Vector2((float)g.pos/100,0);View.TimingNeedle.anchorMax=new Vector2((float)g.pos/100,1);View.TimingNeedle.anchoredPosition=Vector2.zero;
                Put("gaugeHint",g.judged?"停针结果 · "+PlayerMarket.GaugeNames[g.verdict]:S.phase=="paused"?"已暂停，继续后停针":"5 秒内停下指针，金色最好。 ");Enable("gaugeStop",S.phase=="running"&&!g.judged);
            }
        }
        void RefreshDive()
        {
            var d=S.dive;if(d==null)return;Put("diveDepth",d.depth+" m");Put("diveStamina",d.stamina.ToString());Put("diveSamples",d.samples+" / 3");View.StaminaFill.fillAmount=d.stamina/100f;View.Diver.anchorMin=View.Diver.anchorMax=new Vector2(.45f,.85f-d.depth/40f);View.Diver.anchoredPosition=Vector2.zero;Enable("diveDown",d.depth<30);Enable("diveScan",d.depth>=10&&d.samples<3);Put("diveHint",d.samples>0?"返回水面后才能带出情报。":"下潜至 10 m，再搜索鱼群痕迹。");
        }
        void RefreshRows()
        {
            Put("radioToggle",Active(View.RadioExpanded)?"海上电台  ·  收起 ▾":"海上电台  ·  展开 ▴");
            var events=S.events.Where(e=>!e.hidden).Take(View.RadioRows.Length).ToArray();for(int i=0;i<View.RadioRows.Length;i++)if(View.RadioRows[i]){var text=View.RadioRows[i];text.text=i<events.Length?events[i].t.ToString("F0")+"s  "+events[i].title+"\n"+events[i].detail:"";}
            for(int i=0;i<View.LedgerRows.Length;i++)if(View.LedgerRows[i]){View.LedgerRows[i].text=i<S.history.Count?"D"+S.history[i].day+"-"+S.history[i].session+" / "+S.history[i].t.ToString("F0")+"s    收 "+S.history[i].percent+"%    本金 "+N(S.history[i].stake)+"    到账 "+N(S.history[i].gross)+"    净增 "+Signed(S.history[i].pl):"";}
            Put("ledgerFee",S.feeStatements.Count>0?"最近收市手续费 "+N(S.feeStatements[0].fees)+" kg · 费后净增 "+Signed(S.feeStatements[0].pl)+" kg":"每批记录为费前；手续费收市统一缴纳。");
        }
        void RefreshDebug()
        {
            if(!View.DebugPanel.activeSelf)return;bool live=Market.Live;Enable("applyClean",!live);View.CleanSlider.interactable=!live;View.ShapeDropdown.interactable=S.phase=="preopen";Enable("debugSurvey",S.phase=="preopen"&&S.dive==null);Enable("debugGauge",live&&S.gauge==null&&S.gaugeIndex<2);Enable("debugShark",live&&S.eventCount<3);Enable("debugWeather",live&&S.eventCount<3);Enable("debugEnd",live&&!Market.ChallengeLocked);
            Put("debugClean",((int)View.CleanSlider.value).ToString());Put("debugSecrets",View.RevealToggle.isOn?"种子 "+S.seed+"  |  鱼量 "+Market.Map.stock.ToString("F3")+"\n最适水温 "+Market.Rules.TemperatureOptimum+"℃ / 风速 "+Market.Rules.WindOptimum+" m/s\n最适诱饵 "+Market.Map.baitOptimal+" 份 / 当前引鱼增益 "+((Market.BaitFactor(S.phase=="preopen"?S.baitDraft:S.baitUsed)-1)*100).ToString("F1")+"%\n形态 "+PlayerMarket.ShapeNames[S.shape!=null?S.shape.type:S.shapeChoice>=0?S.shapeChoice:Market.Map.curve.type]+" / K "+Market.Baseline().ToString("F2")+"\n手速："+Market.Rules.SpeedWindow+" 秒，"+Market.Rules.SpeedBreakEven+" 次零增益，"+Market.Rules.SpeedMaxCount+" 次满增益\n事件 "+S.eventCount+"/3 / 行为反馈 "+S.behaviorCount+"/2":"");
        }
        public void Save()
        {
            if(!UsePersistentSave||Market==null)return;try{PlayerPrefs.SetString(SaveKey,PlayerMarketSave.Encode(S));PlayerPrefs.Save();storageOk=true;}catch(Exception e){storageOk=false;Debug.LogWarning("Player save failed: "+e.Message,this);}
        }
        public void PrepareForLeave() { Market.Pause();Save(); }
        void ApplyPreparationDefaults()
        {
            if(!UseRaftPreparation||Market==null||S.phase!="preopen")return;
            var station=FindObjectOfType<RaftMarketStation>();if(station)station.ApplyPreparationDefaults();else{S.clean=80;S.cultivation=0;}
        }
        public bool BlocksRaftInput(Vector2 point)
        {
            if(Modal||Active(View.ResultPanel))return true;
            if(View.RaftInputBlockers!=null)foreach(var r in View.RaftInputBlockers)if(r&&r.gameObject.activeInHierarchy&&RectTransformUtility.RectangleContainsScreenPoint(r,point,null))return true;
            return false;
        }
        void ReturnToRaft()
        {
            // Returning pauses/saves the whole position or challenge; it cannot skip a penalty or reroll a market.
            PrepareForLeave();var flow=FindObjectOfType<RaftFishingFlow>();
            if(flow&&RaftFishingFlow.FishingActive){leaving=true;flow.ReturnFromPlayerMarket();return;}
            const string raft="Assets/Scenes/Current/DivePointRealisticOceanSky_Unified.unity";
            if(!Application.CanStreamedLevelBeLoaded(raft)){Toast("木筏场景未加入 Build Settings");return;}
            leaving=true;SceneManager.LoadSceneAsync(raft,LoadSceneMode.Single);
        }
        void OnApplicationFocus(bool focused) { if(!focused&&Market!=null){Market.Pause();Save();} }
        void OnApplicationPause(bool paused) { if(paused&&Market!=null){Market.Pause();Save();} }
        void OnDestroy() { if(Market!=null){Market.Pause();Save();} }
    }
}
