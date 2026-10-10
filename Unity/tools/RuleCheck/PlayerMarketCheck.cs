using System;
using System.Globalization;
using System.Linq;
using Fishing.Player;

static class PlayerMarketCheck
{
    static int count;
    static void Check(bool condition,string name){count++;if(!condition)throw new Exception(name);}
    static void Ok(string error){Check(error=="",error);}
    static void Near(double a,double b,string name){Check(Math.Abs(a-b)<1e-8,name+": "+a+" != "+b);}
    static PlayerMarket Ready(uint seed=1234567){var e=new PlayerMarket(seed);Ok(e.DebugSurvey());return e;}
    static PlayerMarket Start(long amount=100,uint seed=1234567){var e=Ready(seed);e.S.stakeDraft=amount;Ok(e.Start());return e;}
    static PlayerMarket Speed(){var e=Start();e.S.gaugeIndex=1;Ok(e.OpenGauge());return e;}
    static void Main()
    {
        CultureInfo.CurrentCulture=CultureInfo.InvariantCulture;
        var external=new PlayerMarket(42);Check(external.Start(false)!="","raft mode still requires a net");external.S.hasNet=true;
        Check(external.S.maps.All(m=>m.survey==null),"raft mode does not invent underwater findings");Ok(external.ChooseMap(2));Ok(external.Start(false));
        Check(external.S.mapIndex==2&&external.Map.survey==null&&external.S.position!=null,"external destination starts with real net and no compulsory survey UI");
        Check(external.ChooseMap(0)!="","destination locked during capture");
        var surveyed=Ready(42);Ok(surveyed.ChooseMap(2));Ok(surveyed.DebugSurvey());Ok(surveyed.Start());external.S.gaugeIndex=surveyed.S.gaugeIndex=2;external.Advance(90);surveyed.Advance(90);
        Check(external.S.series.Zip(surveyed.S.series,(x,y)=>Math.Abs(x.p-y.p)<1e-10).All(v=>v),"relocating investigation changes no market trajectory");
        var initial=new PlayerMarket(1);Check(initial.S.fish==100&&initial.S.bait==60&&!initial.S.hasNet,"inventory");Check(initial.Start()!="","survey gate");
        Ok(initial.BeginDive());Check(initial.DiveAction("scan")!="","depth gate");Ok(initial.DiveAction("down"));Ok(initial.DiveAction("scan"));Check(initial.Map.survey==null,"survey requires extraction");Ok(initial.DiveAction("return"));Check(initial.S.hasNet&&initial.Map.survey!=null,"single net from extraction");
        var e=Ready();Ok(e.Credit(true,900));e.S.stakeDraft=1000;Ok(e.Start());Check(e.S.fish==0,"principal only");e.Pause();Ok(e.Collect(33));Check(e.S.fish==330&&e.Quote().total==670,"33 percent");double cd=e.S.cd;Ok(e.Add(330));Check(e.S.fish==0&&e.Quote().total==1000,"T+0 proceeds reinvest");Near(cd,e.S.cd,"add preserves CD");Check(e.Collect(100)!="","harvest CD persists");Ok(e.Finish());Check(e.S.fish==987&&e.S.result.fees==13,"aggregated fees");Check(e.Finish()!="","idempotent finish");
        e=Start();e.Pause();Ok(e.Collect(100));cd=e.S.cd;Ok(e.Cast(100));Check(e.S.fish==0&&e.S.position.stake==100,"T+0 full reentry");Near(cd,e.S.cd,"cast preserves CD");Check(e.S.bait==50,"bait once");Check(e.Collect(100)!="","new position cooldown");Ok(e.Finish());Check(e.S.fish==98,"two round trips fee");
        e=Start();e.S.price=120;e.Pause();Ok(e.Collect(100));Check(e.S.fish==120&&e.S.totalFeesPaid==0,"gross payout");Ok(e.Finish());Check(e.S.fish==119&&e.S.totalPL==19,"profit after fee");
        e=Ready();Ok(e.Credit(true,1500));e.S.stakeDraft=1000;Ok(e.Start());e.S.price=120;e.Pause();Ok(e.Add(600));Check(e.Quote().total==1800,"addition is at current index");Check(e.S.position.stake==1600&&e.S.price==120,"addition doesn't raise index");Ok(e.Finish());Check(e.S.fish==1783&&e.S.totalPL==183,"addition ledger");
        e=Start();e.S.price=.000001;Ok(e.Finish());Check(e.S.fish==0&&e.S.hasNet,"no net damage");
        e=Ready();Ok(e.Credit(true,900));e.S.stakeDraft=1000;Ok(e.Start());e.S.price=.00001;Ok(e.Finish());Check(e.S.feeDebt==5&&e.S.totalPL== -1005,"explicit fee debt");Ok(e.Next());Ok(e.Credit(true,100));Ok(e.DebugSurvey());e.S.stakeDraft=100;Ok(e.Start());Ok(e.Finish());Check(e.S.feeDebt==0&&e.S.fish==94,"debt not double counted");
        e=Start();e.Pause();foreach(int percent in new[]{0,-1,101})Check(e.Collect(percent)!="","reject percent");foreach(long amount in new long[]{0,-1,1000001,101})Check(e.Add(amount)!="","reject amount");Ok(e.Collect(100));Check(e.Add(1)!="","empty net cannot add");
        foreach(int n in new[]{0,12,24,42,60,100}){e=Speed();for(int i=0;i<n;i++)Ok(e.SpeedPress(i%2));double t=e.S.elapsed;Check(e.Finish()!="","challenge cannot skip");e.Advance(6);Near(e.S.elapsed,t,"market frozen");Near(e.S.gauge.gain,e.SpeedGain(n),"speed verdict");Check(e.ChallengeLocked,"verdict locks");e.Advance(2);Check(!e.ChallengeLocked,"verdict clears");}
        e=Speed();Near(e.SpeedGain(0),-.12,"speed floor");Near(e.SpeedGain(24),0,"harder break even");Near(e.SpeedGain(42),.09,"midgain");Near(e.SpeedGain(60),.18,"harder cap");Ok(e.SpeedPress(0));Check(e.SpeedPress(0)!="","same key rejected");Check(e.SpeedPress(1,true)!="","repeat rejected");Ok(e.SpeedPress(1));e.Pause();Check(e.SpeedPress(0)!="","pause freezes input");e.Advance(100);Check(e.S.gauge.u==0&&e.S.gauge.count==2,"pause freezes challenge");
        e=Start();Ok(e.OpenGauge());Near(e.S.gauge.layout.Where(v=>v.kind==0).Sum(v=>v.to-v.from),8,"best segment width");Near(e.S.gauge.layout.Where(v=>v.kind==1).Sum(v=>v.to-v.from),24,"good width");e.Advance(5);Check(e.S.gauge.verdict==3,"timing timeout penalty");
        e=Ready();double k=e.Baseline();e.Map.weather.temp=17;e.Map.weather.wind=4;double top=e.Baseline();e.Map.weather.temp=31;Check(e.Baseline()<top,"temperature penalty");Near(e.BaitFactor(e.Map.baitOptimal),1.1,"bait reduced");Check(e.BaitFactor(0)<1.1&&e.BaitFactor(100)<1.1,"bait bell curve");
        var a=Ready(5);var b=Ready(5);Ok(a.BeginDive());Ok(a.DiveAction("down"));Ok(a.DiveAction("scan"));Ok(a.DiveAction("return"));Near(a.Map.stock,b.Map.stock,"survey doesn't change fish");Near(a.Baseline(),b.Baseline(),"survey doesn't change trajectory factor");
        for(uint seed=1;seed<=100;seed++){
            e=Start(50,seed);e.Advance(120);Check(e.S.phase=="closed","automatic end");Check(e.S.series.Count==91,"fixed ticks");Check(e.S.gaugeIndex==2,"two challenges");Check(e.S.series.All(v=>v.p>0&&PlayerMarket.Finite(v.p)),"finite prices");Check(e.S.fish-e.S.feeDebt==100+e.S.giftFish+e.S.totalPL,"conservation");Check(e.S.hasNet,"permanent net");Ok(e.Next());Check(e.S.session==2,"two sessions");Check(e.S.maps.All(m=>m.survey==null),"new survey each round");}
        // Export seeded trajectories for JS/C# parity; no challenge input involved.
        foreach(uint seed in new uint[]{1,5,37,345,1234567}){
            e=Start(50,seed);e.S.gaugeIndex=2;e.Advance(91);foreach(var p in e.S.series)Console.WriteLine("CURVE "+seed+" "+p.t+" "+p.p.ToString("R"));Console.WriteLine("CASH "+seed+" "+e.S.fish+" "+e.S.totalPL);
        }
        Console.WriteLine("PASS "+count+" player model assertions");
    }
}
