using System;
using System.Collections.Generic;
using System.Linq;

namespace Fishing.Player
{
    // Native C# port of player.html. No Unity dependencies: deterministic and headlessly testable.
    [Serializable] public class MarketRules
    {
        public int Duration = 90, StartFish = 100, StartBait = 60, MaxInput = 1000000;
        public double Fee = .005, CdMin = 3, CdMax = 5;
        public double BaitGain = .10, TemperatureOptimum = 17, WindOptimum = 4;
        public int BaitOptimalMin = 4, BaitOptimalMax = 24;
        public double SpeedWindow = 6, SpeedMinGain = -.12, SpeedMaxGain = .18;
        public int SpeedBreakEven = 24, SpeedMaxCount = 60;
        public double GaugeWindow = 5, GaugeSweep = 1.4;
    }
    [Serializable] public class MarketPoint { public double t, p; public MarketPoint(double time, double price) { t=time;p=price; } }
    [Serializable] public class MarketMarker { public double t,p; public string type; }
    [Serializable] public class MarketWeather { public int type; public double temp,wind; }
    [Serializable] public class MarketSurvey { public double quality,estimate; public int samples; }
    [Serializable] public class MarketShape { public int type,sign; public double ampRoll,periodRoll,trendRoll; }
    [Serializable] public class MarketMap
    {
        public string name; public double stock,info,quality,signal; public int regime,baitOptimal;
        public MarketWeather weather; public MarketSurvey survey; public MarketShape curve; public bool observed,heard;
    }
    [Serializable] public class MarketPosition { public long stake; public double units,entry,at; }
    [Serializable] public class MarketDive { public int mapIndex,depth,stamina=100,samples; }
    [Serializable] public class MarketShock { public double at,mag,dur,amp; }
    [Serializable] public class MarketGaugeSegment { public int kind; public double from,to; }
    [Serializable] public class MarketGauge
    {
        public bool speed,judged; public int count,lastKey=-1,verdict=-1,number;
        public double u,pos,hold,gain; public List<MarketGaugeSegment> layout=new List<MarketGaugeSegment>();
    }
    [Serializable] public class MarketEvent { public double t; public string title,detail,kind; public bool hidden; }
    [Serializable] public class MarketQuote { public long total,gross,costBasis,pl,remaining; public int percent; }
    [Serializable] public class MarketTrade
    {
        public int day,session,percent; public double t,entry,exit; public long stake,gross,pl; public bool forced;
    }
    [Serializable] public class MarketResult
    {
        public int day,session,bait,trades; public long pl,fees,paid,debt,previousDebt,buy,sell; public double elapsed;
    }
    [Serializable] public class MarketState
    {
        public int version=1; public uint seed; public uint[] rng=new uint[8];
        public string phase="preopen"; public long fish,totalPL,totalFees,totalFeesPaid,feeDebt,giftFish;
        public int bait,giftBait,clean=80,cultivation,day=1,session=1,mapIndex;
        public bool hasNet,flatChecked; public int shapeChoice=-1,regime,baitUsed,lastTick,eventCount,behaviorCount,gaugeIndex,sessionTrades;
        public double elapsed,price=100,x,h=.001225,eco,cd,cdTotal,activity,pendingJump,flatSince,lastBehavior=-99;
        public long sessionPL,sessionFees,sessionBuyWeight,sessionSellWeight,stakeDraft=50;
        public int baitDraft=10,collectPercent=100;
        public MarketPosition position; public MarketDive dive; public MarketGauge gauge; public MarketShape shape;
        public MarketResult result; public double[] gaugePlan=new double[2];
        public List<MarketMap> maps=new List<MarketMap>();
        public List<MarketPoint> series=new List<MarketPoint>();
        public List<MarketMarker> markers=new List<MarketMarker>();
        public List<MarketTrade> history=new List<MarketTrade>();
        public List<MarketResult> feeStatements=new List<MarketResult>();
        public List<MarketEvent> events=new List<MarketEvent>();
        public List<MarketShock> gaugeShocks=new List<MarketShock>(),behaviorShocks=new List<MarketShock>();
    }

    public sealed class PlayerMarket
    {
        public MarketState S { get; private set; }
        public readonly MarketRules Rules;
        public static readonly string[] MapNames={"珊瑚浅湾","海藻回廊","蓝潮海沟"};
        public static readonly string[] WeatherNames={"晴天","雨天","暴风雨"};
        public static readonly string[] ShapeNames={"横盘震荡","反复穿零","趋势大涨","趋势大跌","大幅拉扯"};
        public static readonly string[] GaugeNames={"最佳","次佳","普通","差"};
        public static readonly double[] GaugeGains={.12,.05,0,-.15};
        // amp min/max, period min/max, trend min/max, pull, volatility, ecology.
        static readonly double[][] Profiles={
            new[]{.10,.18,18,28,0,0,.24,.85,.7},new[]{.25,.38,20,30,0,0,.34,1.05,.12},
            new[]{.12,.20,20,35,.85,1.25,.20,1.15,.5},new[]{.12,.20,20,35,-1.25,-.85,.20,1.15,.5},
            new[]{.45,.65,28,40,-.12,.12,.30,1.5,.35}};
        const int World=0,Market=1,Events=2,BehaviorRng=3,GaugeRng=4,SurveyRng=5,ShapeRng=6,BaitRng=7;
        public PlayerMarket(uint seed,MarketRules rules=null) { Rules=rules??new MarketRules();ValidateRules();Reset(seed); }
        public PlayerMarket(MarketState state,MarketRules rules=null)
        {
            Rules=rules??new MarketRules();ValidateRules();if(!ValidState(state))throw new ArgumentException("Invalid player save");S=state;
            if(S.phase=="running")S.phase="paused";S.dive=null;
        }
        void ValidateRules()
        {
            if(Rules.Duration<1||Rules.SpeedWindow<=0||Rules.SpeedBreakEven<1||Rules.SpeedMaxCount<=Rules.SpeedBreakEven||Rules.CdMin<0||Rules.CdMax<Rules.CdMin||Rules.Fee<0||Rules.Fee>1||Rules.BaitOptimalMin<1||Rules.BaitOptimalMax<Rules.BaitOptimalMin)
                throw new ArgumentException("Invalid PlayerMarket rules");
        }
        public static bool ValidState(MarketState s)
        {
            if(s==null||s.version!=1||!new[]{"preopen","running","paused","closed"}.Contains(s.phase)||s.rng==null||s.rng.Length!=8||s.maps==null||s.maps.Count!=3||s.mapIndex<0||s.mapIndex>2||s.series==null||s.series.Count<1||s.series.Count>2000||s.history==null||s.events==null||s.feeStatements==null||s.gaugeShocks==null||s.behaviorShocks==null||s.markers==null||s.gaugePlan==null||s.gaugePlan.Length!=2)return false;
            if(s.fish<0||s.bait<0||s.feeDebt<0||s.sessionBuyWeight<0||s.sessionSellWeight<0||s.clean<0||s.clean>100||s.cultivation<0||s.elapsed<0||s.cd<0||!Finite(s.elapsed)||!Finite(s.price)||s.price<=0||!Finite(s.x)||!Finite(s.eco)||!Finite(s.h)||s.h<=0||s.day<1||s.session<1||s.session>2)return false;
            if(s.position!=null&&(s.position.stake<0||s.position.units<=0||!Finite(s.position.units)||!Finite(s.position.entry)))return false;
            foreach(var m in s.maps)if(m==null||m.weather==null||m.weather.type<0||m.weather.type>2||!Finite(m.weather.temp)||!Finite(m.weather.wind)||m.curve==null||m.curve.type<0||m.curve.type>4||m.baitOptimal<1||!Finite(m.stock)||m.stock<=0)return false;
            if(s.gauge!=null&&(!Finite(s.gauge.u)||s.gauge.u<0||s.gauge.count<0||s.gauge.lastKey< -1||s.gauge.lastKey>1||s.gauge.layout==null||(!s.gauge.speed&&s.gauge.layout.Count!=7)))return false;
            return s.series.All(p=>p!=null&&Finite(p.t)&&Finite(p.p)&&p.p>0);
        }
        public static bool Finite(double v) { return !double.IsNaN(v)&&!double.IsInfinity(v); }
        public static double Clamp(double v,double a,double b) { return Math.Max(a,Math.Min(b,v)); }
        static double Round(double v) { return Math.Floor(v*1000000+.5)/1000000; }
        public static long Floor(double v)
        {
            if(!Finite(v)||v>long.MaxValue||v<long.MinValue)throw new ArithmeticException("Weight out of bounds");
            double nearest=Math.Round(v),tolerance=2.220446049250313e-16*Math.Max(1,Math.Abs(v))*8;
            return (long)Math.Floor(Math.Abs(v-nearest)<=tolerance?nearest:v);
        }
        double Random(int stream)
        {
            unchecked { uint a=S.rng[stream]+0x6D2B79F5u;S.rng[stream]=a;uint t=(a^(a>>15))*(a|1u);t^=t+((t^(t>>7))*(t|61u));return (t^(t>>14))/4294967296.0; }
        }
        double Normal(int stream) { return Math.Sqrt(-2*Math.Log(Math.Max(1e-12,Random(stream))))*Math.Cos(2*Math.PI*Random(stream)); }
        void Log(string title,string detail="",string kind="info",bool hidden=false) { S.events.Insert(0,new MarketEvent{t=S.elapsed,title=title,detail=detail,kind=kind,hidden=hidden});if(S.events.Count>36)S.events.RemoveAt(36); }
        void Mark(string type) { S.markers.Add(new MarketMarker{t=S.elapsed,p=S.price,type=type}); }
        public MarketMap Map { get { return S.maps[S.mapIndex]; } }
        public bool Live { get { return S.phase=="running"||S.phase=="paused"; } }
        public bool ChallengeLocked { get { return S.gauge!=null&&(!S.gauge.judged||S.gauge.speed); } }
        public void Reset(uint seed)
        {
            S=new MarketState{seed=seed,fish=Rules.StartFish,bait=Rules.StartBait};
            uint[] salts={12345,76543,45454,99887,55667,33771,10101,90210};for(int i=0;i<8;i++)S.rng[i]=seed^salts[i];Prepare();
        }
        MarketWeather Weather(int type)
        {
            double shared=Normal(World),other=Normal(World);double[] temp={19,17,13},wind={4,4.5,8};
            return new MarketWeather{type=type,temp=Clamp(temp[type]+1.8*shared,4,31),wind=Clamp(wind[type]+1.2*(-.45*shared+.893*other),0,15)};
        }
        void Prepare()
        {
            S.phase="preopen";S.elapsed=S.lastTick=0;S.price=100;S.x=S.eco=0;S.h=.001225;S.position=null;S.shape=null;S.regime=0;
            S.cd=S.cdTotal=S.activity=S.pendingJump=S.flatSince=0;S.flatChecked=false;S.lastBehavior=-99;S.eventCount=S.behaviorCount=S.gaugeIndex=S.baitUsed=S.sessionTrades=0;
            S.sessionBuyWeight=S.sessionSellWeight=S.sessionFees=S.sessionPL=0;S.gauge=null;S.result=null;S.dive=null;S.collectPercent=100;
            S.gaugeShocks.Clear();S.behaviorShocks.Clear();S.series.Clear();S.markers.Clear();S.events.Clear();S.maps.Clear();S.series.Add(new MarketPoint(0,100));
            double[] bases={.88,1.04,1.20};for(int i=0;i<3;i++){
                double roll=Random(World);var w=Weather(roll<.46?0:roll<.84?1:2);double info=Normal(World),quality=.55+.3*Random(World);
                S.maps.Add(new MarketMap{name=MapNames[i],stock=bases[i]*Math.Exp(.18*Normal(World)),weather=w,info=info,quality=quality,signal=info+(1-quality)*1.7*Normal(World),regime=(int)Math.Floor(Random(World)*3)-1});
            }
            foreach(var m in S.maps)m.baitOptimal=Rules.BaitOptimalMin+(int)Math.Floor(Random(BaitRng)*(Rules.BaitOptimalMax-Rules.BaitOptimalMin+1));
            foreach(var m in S.maps)m.curve=new MarketShape{type=(int)(Random(ShapeRng)*5),ampRoll=Random(ShapeRng),periodRoll=Random(ShapeRng),trendRoll=Random(ShapeRng),sign=Random(ShapeRng)<.5?-1:1};
            S.baitDraft=Math.Min(S.baitDraft,S.bait);S.stakeDraft=Math.Min(S.stakeDraft,S.fish);Log("码头开放","调查海域后开市。");
        }
        public double BaitFactor(int bait) { double b=Math.Max(0,bait)/(double)Map.baitOptimal;return 1+Rules.BaitGain*b*Math.Exp(1-b); }
        public double Baseline()
        {
            var w=Map.weather;double[] factors={1,1.15,.65};
            return Clamp(140*(.25+.75*S.clean/100)*(1+.5*S.cultivation/(S.cultivation+50.0))*Map.stock*factors[w.type]*Math.Exp(-.5*Math.Pow((w.temp-Rules.TemperatureOptimum)/5,2))*Math.Exp(-.5*Math.Pow((w.wind-Rules.WindOptimum)/2.5,2))*BaitFactor(S.phase=="preopen"?S.baitDraft:S.baitUsed)*Math.Exp(.1*Map.info),5,600);
        }
        public double ShapeTarget(double t)
        {
            if(S.shape==null)return 0;var p=Profiles[S.shape.type];var q=S.shape;
            return (p[4]+(p[5]-p[4])*q.trendRoll)*t/Rules.Duration+(p[0]+(p[1]-p[0])*q.ampRoll)*Math.Sin(2*Math.PI*t/(p[2]+(p[3]-p[2])*q.periodRoll))*q.sign*(1-Math.Exp(-t/6));
        }
        public string ChooseMap(int index) { if(S.phase!="preopen"||S.dive!=null)return "只能在开市前、调查外选择海域";if(index<0||index>2)return "无效海域";S.mapIndex=index;return ""; }
        public string Observe() { if(S.phase!="preopen")return "请在开市前读取仪器";Map.observed=true;return ""; }
        public string AskNpc() { if(S.phase!="preopen")return "请在开市前询问消息";Map.heard=true;return ""; }
        public string Cultivate() { if(Live)return "请在局间投入";if(S.bait<5)return "需要 5 份诱饵";S.bait-=5;S.cultivation+=5;S.baitDraft=Math.Min(S.baitDraft,S.bait);Log("培育投入","投下 5 份诱饵，继续养护这片海。");return ""; }
        public string BeginDive() { if(S.phase!="preopen")return "只能在开市前调查";if(S.dive==null)S.dive=new MarketDive{mapIndex=S.mapIndex};return ""; }
        public string DiveAction(string action)
        {
            var d=S.dive;if(d==null)return "当前不在水下";
            if(action=="down"){if(d.depth>=30)return "已到调查深度上限";if(d.stamina<8+(d.depth+10)/10*3)return "体力不足，请返回";d.depth+=10;d.stamina-=8;return "";}
            if(action=="scan"){if(d.depth<10)return "先下潜至 10 m 再搜索";if(d.samples>=3)return "样本已足够，请返回";if(d.stamina<12+d.depth/10*3)return "体力不足，请返回";d.samples++;d.stamina-=12;return "";}
            if(action!="return")return "未知调查操作";
            if(d.samples>0){double quality=.3+.2*d.samples;S.maps[d.mapIndex].survey=new MarketSurvey{quality=quality,estimate=S.maps[d.mapIndex].stock*Math.Exp((1-quality)*.35*Normal(SurveyRng)),samples=d.samples};S.hasNet=true;Log("调查情报已带回",S.maps[d.mapIndex].name+"，捕捞网已就绪。");}S.dive=null;return "";
        }
        public string ValidateWeight(long value) { if(value<1||value>Rules.MaxInput)return "请输入 1–1,000,000 的整数 kg";return value>S.fish?"可用活鱼不足；手续费收市交":""; }
        string ValidateCast(long value) { if(!S.hasNet)return "需要先调查并带回网";if(S.position!=null)return "只有一张网，请追加或先全部收回";return ValidateWeight(value); }
        void CastInternal(long value,bool opening=false)
        {
            S.fish-=value;S.position=new MarketPosition{stake=value,units=value/S.price,entry=S.price,at=S.elapsed};S.sessionBuyWeight+=value;S.sessionFees=PendingFee();S.activity=Clamp(S.activity+.8,0,6);Mark("cast");if(!opening)Behavior("cast");Log("下网","投入 "+value+" kg；手续费收市交。");
        }
        public string Start(bool requireSurvey=true)
        {
            if(S.phase!="preopen"||S.dive!=null)return "当前不能开市";if(requireSurvey&&Map.survey==null)return "请先调查当前水域并返回";
            string error=ValidateCast(S.stakeDraft);if(error!="")return error;if(S.baitDraft<0||S.baitDraft>S.bait)return "诱饵不足";
            S.baitUsed=S.baitDraft;S.bait-=S.baitUsed;S.phase="running";S.regime=Map.regime;var c=Map.curve;S.shape=new MarketShape{type=S.shapeChoice>=0?S.shapeChoice:c.type,ampRoll=c.ampRoll,periodRoll=c.periodRoll,trendRoll=c.trendRoll,sign=c.sign};
            S.gaugePlan=new[]{12+20*Random(GaugeRng),52+20*Random(GaugeRng)};CastInternal(S.stakeDraft,true);Log("开市","本场消耗 "+S.baitUsed+" 份诱饵。");return "";
        }
        public string Cast(long value) { if(S.phase!="paused")return "请先暂停再下网";if(ChallengeLocked)return "请先完成挑战";string error=ValidateCast(value);if(error!="")return error;CastInternal(value);return ""; }
        public string Add(long value)
        {
            if(S.phase!="paused")return "请先暂停再追加";if(!S.hasNet||S.position==null)return "当前没有在途捕捞";if(ChallengeLocked)return "请先完成挑战";string error=ValidateWeight(value);if(error!="")return error;
            var p=S.position;S.fish-=value;p.units+=value/S.price;p.stake+=value;p.entry=p.stake/p.units;S.sessionBuyWeight+=value;S.sessionFees=PendingFee();S.activity=Clamp(S.activity+.8,0,6);Mark("add");Behavior("cast");Log("追加重量","追加 "+value+" kg，不重复消耗诱饵。");return "";
        }
        public MarketQuote Quote(int percent=100)
        {
            var p=S.position;if(p==null||percent<1||percent>100)return null;double raw=p.units*S.price;long total=Floor(raw),gross=percent==100?total:Floor(total*percent/100.0),cost=percent==100?p.stake:Floor(p.stake*gross/raw);
            return new MarketQuote{total=total,gross=gross,costBasis=cost,pl=gross-cost,percent=percent,remaining=percent==100?0:total-gross};
        }
        public long PendingFee() { return Floor((S.sessionBuyWeight+S.sessionSellWeight)*Rules.Fee); }
        public string Collect(int percent,bool forced=false)
        {
            if(!forced&&S.phase!="paused")return "请先暂停再收网";if(S.position==null)return "当前没有在途捕捞";if(!forced&&S.cd>1e-6)return "收网冷却尚未结束";if(!forced&&ChallengeLocked)return "请先完成挑战";if(forced)percent=100;if(percent<1||percent>100)return "请输入 1–100 的整数百分比";
            var p=S.position;var q=Quote(percent);if(percent<100&&q.gross<1)return "本批不足 1 kg，请提高比例或全部收回";
            S.fish+=q.gross;S.sessionPL+=q.pl;S.totalPL+=q.pl;S.sessionSellWeight+=q.gross;S.sessionFees=PendingFee();S.sessionTrades++;
            S.history.Insert(0,new MarketTrade{day=S.day,session=S.session,t=S.elapsed,stake=q.costBasis,entry=p.entry,exit=S.price,gross=q.gross,pl=q.pl,percent=percent,forced=forced});if(S.history.Count>200)S.history.RemoveAt(200);Mark("collect");
            if(percent==100){S.position=null;S.flatSince=S.elapsed;S.flatChecked=false;}else{p.units-=q.gross/S.price;p.stake-=q.costBasis;p.entry=p.stake/p.units;}
            S.activity=Clamp(S.activity+1,0,6);if(!forced){S.cdTotal=Rules.CdMin+(Rules.CdMax-Rules.CdMin)*Random(Events);S.cd=S.cdTotal;Behavior("collect");}
            Log(forced?"收市：全部收回":"分批收网 · "+percent+"%","到账 "+q.gross+" kg，余量 "+q.remaining+" kg；手续费收市交。",q.pl>=0?"good":"bad");return "";
        }
        public void Pause() { if(S.phase=="running")S.phase="paused"; }
        public string TogglePause() { if(!Live)return "市场尚未运行";S.phase=S.phase=="running"?"paused":"running";return ""; }
        public string Finish()
        {
            if(!Live)return "市场尚未开市";if(ChallengeLocked)return "请先完成挑战，不能跳过判定";if(S.position!=null)Collect(100,true);
            long fee=PendingFee(),previous=S.feeDebt,paid=Math.Min(S.fish,fee+previous);S.fish-=paid;S.feeDebt=fee+previous-paid;S.sessionFees=fee;S.sessionPL-=fee;S.totalPL-=fee;S.totalFees+=fee;S.totalFeesPaid+=paid;S.phase="closed";S.gauge=null;S.cd=0;
            S.result=new MarketResult{day=S.day,session=S.session,pl=S.sessionPL,fees=fee,paid=paid,debt=S.feeDebt,previousDebt=previous,buy=S.sessionBuyWeight,sell=S.sessionSellWeight,bait=S.baitUsed,trades=S.sessionTrades,elapsed=S.elapsed};
            S.feeStatements.Insert(0,S.result);if(S.feeStatements.Count>100)S.feeStatements.RemoveAt(100);Log("收市统一缴费","应缴 "+fee+" kg，实缴 "+paid+" kg"+(S.feeDebt>0?"，待缴 "+S.feeDebt+" kg":"")+"。");return "";
        }
        public string Next() { if(S.phase!="closed")return "请先完成当前场次";if(S.session==1)S.session=2;else{S.day++;S.session=1;}Prepare();return ""; }
        public string OpenGauge()
        {
            if(!Live)return "开市后才能挑战";if(S.gauge!=null)return "挑战正在进行";if(S.gaugeIndex>=2)return "本场挑战已结束";
            bool speed=S.gaugeIndex==1;var g=new MarketGauge{speed=speed,number=++S.gaugeIndex};S.gauge=g;
            if(speed){Log("收网拼手速","快交替按 A / D，用力收紧渔网！");return "";}
            int[] kinds={3,3,2,2,1,1,0};double[] widths={8,8,26,26,12,12,8};var list=new List<MarketGaugeSegment>();for(int i=0;i<7;i++)list.Add(new MarketGaugeSegment{kind=kinds[i],to=widths[i]});
            for(int i=6;i>0;i--){int j=(int)(Random(GaugeRng)*(i+1));var v=list[i];list[i]=list[j];list[j]=v;}double cursor=0;foreach(var v in list){double width=v.to;v.from=cursor;v.to=cursor+width;cursor+=width;}g.layout=list;Log("潮流仪表盘","按 Q 或点击停针，抓准金色区域。");return "";
        }
        public double SpeedGain(int count) { return count<Rules.SpeedBreakEven?Rules.SpeedMinGain*(1-count/(double)Rules.SpeedBreakEven):Rules.SpeedMaxGain*Clamp((count-Rules.SpeedBreakEven)/(double)(Rules.SpeedMaxCount-Rules.SpeedBreakEven),0,1); }
        public string SpeedGrade(int count) { return count<Rules.SpeedBreakEven?"力道不足":count==Rules.SpeedBreakEven?"稳住了":count<Rules.SpeedMaxCount?"收网有力":"势不可挡"; }
        public string SpeedPress(int key,bool repeated=false)
        {
            var g=S.gauge;if(g==null||!g.speed||g.judged)return "当前没有拼手速挑战";if(S.phase!="running")return "暂停中不计数";if(repeated||key<0||key>1||g.lastKey==key)return "请交替按 A 和 D";g.count++;g.lastKey=key;return "";
        }
        void JudgeSpeed()
        {
            var g=S.gauge;g.judged=true;g.verdict=4;g.gain=SpeedGain(g.count);g.hold=1.6;if(g.gain!=0)S.gaugeShocks.Add(new MarketShock{at=S.elapsed,mag=g.gain,dur=g.gain<0?.9:4});Log("拼手速结算",g.count+" 次有效交替 · "+SpeedGrade(g.count),g.gain>=0?"good":"bad");
        }
        public string JudgeTiming(bool timeout=false)
        {
            var g=S.gauge;if(g==null||g.speed||g.judged)return "当前没有待停针仪表盘";if(!timeout&&S.phase!="running")return "暂停中，请先继续";
            int kind=timeout?3:(g.layout.FirstOrDefault(v=>g.pos>=v.from&&g.pos<=v.to)?.kind??3);g.verdict=kind;g.judged=true;g.gain=GaugeGains[kind];g.hold=1.6;
            if(g.gain!=0)S.gaugeShocks.Add(new MarketShock{at=S.elapsed,mag=g.gain,dur=kind==3?.9:4});Log("仪表盘 · "+GaugeNames[kind],kind==3?"鱼群受到惊扰。":"稳住渔网，继续留意鱼群！",kind==3?"bad":"good");return "";
        }
        public double Overlay(double t)
        {
            double g=0,a=0;foreach(var q in S.gaugeShocks)g+=Math.Log(1+q.mag*Clamp((t-q.at)/q.dur,0,1));foreach(var q in S.behaviorShocks){double u=(t-q.at)/2;if(u>0)a+=q.amp*u*Math.Exp(1-u);}return g+Clamp(a,-Math.Log(1.15),Math.Log(1.15));
        }
        bool Behavior(string action)
        {
            if(S.elapsed<8||S.behaviorCount>=2||S.elapsed-S.lastBehavior<14)return false;var recent=S.series.Where(p=>p.t>=S.elapsed-12).ToList();double lo=recent.Min(p=>p.p),hi=recent.Max(p=>p.p),first=recent[0].p;if(hi-lo<1)return false;
            double rank=(S.price-lo)/(hi-lo);int sign=0;if(action=="collect"&&(rank>.8||rank<.2))sign=1;if(action=="cast"&&rank>.8&&S.price/first>1.025)sign=-1;if(action=="flat"&&S.price/first>1.025)sign=1;
            if(sign==0||Random(BehaviorRng)>.30)return false;S.behaviorCount++;S.lastBehavior=S.elapsed;S.behaviorShocks.Add(new MarketShock{at=S.elapsed,amp=sign*(.045+.045*Random(BehaviorRng))});Mark("event");return true;
        }
        public string TriggerEvent(bool weather)
        {
            if(!Live)return "开市后才能触发";if(S.eventCount>=3)return "本场事件已用完";S.eventCount++;
            if(weather){Map.weather=Weather((Map.weather.type+1+(Random(Events)<.25?1:0))%3);Log("天气突变 · "+WeatherNames[Map.weather.type],"海况变了，留意仪器读数。");}
            else{double loss=Clamp(.12+.1*Random(Events)-S.regime*.025,.08,.27);S.pendingJump+=Math.Log(1-loss);Log("鲨鱼袭击","鱼群受惊，渔网仍然完好。","bad");}Mark("event");return "";
        }
        void MarketTick(int n)
        {
            S.regime=Math.Sign(ShapeTarget(n)-ShapeTarget(n-1));double intensity=Map.weather.type==2?.035:.012;if(S.eventCount<3&&Random(Events)<1-Math.Exp(-intensity))TriggerEvent(Random(Events)<.5);
            double k=Baseline(),u=Math.Sqrt(S.h)*Normal(Market);var p=Profiles[S.shape.type];S.eco+=.018*(.5*Math.Log(k/100)-S.eco);S.x+=p[6]*(ShapeTarget(n)+p[8]*S.eco-S.x)-.5*S.h*p[7]*p[7]+p[7]*u+S.pendingJump;S.h=Clamp(.0000735*(1+.2*S.activity)+.10*u*u+.84*S.h,.000144,.0064);S.pendingJump=0;
            S.price=100*Math.Exp(Clamp(S.x+Overlay(n),-12,12));S.series.Add(new MarketPoint(n,S.price));if(S.position==null&&!S.flatChecked&&S.elapsed-S.flatSince>=8){S.flatChecked=true;Behavior("flat");}
        }
        public void Advance(double dt)
        {
            if(!Finite(dt)||dt<0)return;double remaining=Math.Min(dt,600);
            while(remaining>1e-8&&S.phase=="running"){
                double step=Math.Min(.05,remaining);remaining-=step;var g=S.gauge;
                if(g!=null){if(!g.judged){g.u=Round(g.u+step);if(g.speed){if(g.u>=Rules.SpeedWindow)JudgeSpeed();}else{double phase=(g.u/Rules.GaugeSweep)%2;g.pos=(phase<=1?phase:2-phase)*100;if(g.u>=Rules.GaugeWindow)JudgeTiming(true);}continue;}g.hold-=step;if(g.hold<=0)S.gauge=null;}
                S.elapsed=Math.Min(Rules.Duration,Round(S.elapsed+step));S.cd=Math.Max(0,S.cd-step);S.activity*=Math.Exp(-step/12);while(S.lastTick+1<=S.elapsed+1e-7){S.lastTick++;MarketTick(S.lastTick);}
                if(S.elapsed>=Rules.Duration){Finish();break;}if(S.gauge==null&&S.gaugeIndex<2&&S.elapsed>=S.gaugePlan[S.gaugeIndex])OpenGauge();
            }
        }
        public string Credit(bool fish,long amount) { if(amount<1||amount>Rules.MaxInput)return "请输入有效补给数量";if(fish){S.fish+=amount;S.giftFish+=amount;}else{S.bait+=(int)amount;S.giftBait+=(int)amount;}return ""; }
        public string SetClean(int clean) { if(Live)return "请在局间修改环境";if(clean<0||clean>100)return "无效清洁度";S.clean=clean;return ""; }
        public string DebugSurvey() { if(S.phase!="preopen"||S.dive!=null)return "请在开市前、调查外使用";Map.survey=new MarketSurvey{quality=1,estimate=Map.stock,samples=3};S.hasNet=true;return ""; }
        public string SetShape(int type) { if(S.phase!="preopen")return "请在开市前设置";if(type< -1||type>4)return "无效形态";S.shapeChoice=type;return ""; }
    }
}
