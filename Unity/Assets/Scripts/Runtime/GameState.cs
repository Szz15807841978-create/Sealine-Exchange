using System.Collections.Generic;
using UnityEngine;

namespace Fishing
{
    /// <summary>已入场的一张网。pure data —— 颜色由视图按 id 现算，状态层不依赖 UI。</summary>
    public class Net
    {
        public int Id;
        public float Entry;     // 买入价
        public float TEnter;    // 入场时刻（秒）

        public Net(int id, float entry, float tEnter)
        {
            Id = id;
            Entry = entry;
            TEnter = tEnter;
        }

        /// <summary>对应 NET_COLORS[(id-1) % 8]。走 FishDefs 的纯色值，状态层不依赖 UI 配色表。</summary>
        public UnityEngine.Color Color
        {
            get { return FishDefs.NetColorValue(Id); }
        }
    }

    /// <summary>破损网：完整度 repair（100 = 完整）。</summary>
    public class BrokenNet
    {
        public int Id;
        public float Repair;
        public int NetId;

        public BrokenNet(int id, float repair, int netId)
        {
            Id = id;
            Repair = repair;
            NetId = netId;
        }
    }

    /// <summary>一张网的结算明细。</summary>
    public class NetRecord
    {
        public int Id;
        public float Entry;
        public float Price;
        public float Pl;
        public string Result;    // lost | damaged | flat | win
        public string Mode;      // manual | auto
        public float Loot;
        public int Worms;
        public float Damage;
        public float Integrity;
        public int NetReturn;
        public float NetSpend;
        public int Fixed;        // 0/1
        public int WormsUsed;
    }

    /// <summary>本场收市结算结果。</summary>
    public class SessionResult
    {
        public List<NetRecord> Records = new List<NetRecord>();
        public Dictionary<string, int> Bag = new Dictionary<string, int>();
        public float Loot;
        public int Worms;
        public float Pl;
        public float Price;
        public int Auto;
        public bool Early;
    }

    /// <summary>历史每场记录。</summary>
    public class HistoryEntry
    {
        public int Day;
        public int Session;
        public string Pattern;
        public float Price;
        public float Pl;
        public string Loot;
    }

    /// <summary>曲线冲击叠加层。</summary>
    public class Shock
    {
        public float At;
        public string Kind;
        public float Mag;
        public float Dur;
        public string Label;
    }

    public class GaugeSegment
    {
        public string Kind;
        public float From;
        public float To;
    }

    /// <summary>当前仪表盘状态。</summary>
    public class GaugeState
    {
        public float U;          // 已经走了多久
        public float Pos;        // 指针位置 0~100
        public string Verdict;   // null = 还没判定
        public float Hold;       // 判定结果停留时间
    }

    public class GaugeStats
    {
        public int Best, Good, Normal, Bad;

        public void Bump(string kind)
        {
            switch (kind)
            {
                case "best":   Best++;   break;
                case "good":   Good++;   break;
                case "normal": Normal++; break;
                default:       Bad++;    break;
            }
        }

        public void Reset()
        {
            Best = 0; Good = 0; Normal = 0; Bad = 0;
        }
    }

    public class UiState
    {
        public int CastCount = 1;
        public readonly HashSet<int> Selected = new HashSet<int>();
    }

    /// <summary>视图纵向范围（只根据已揭示的数据，避免剧透）。</summary>
    public class ViewRange
    {
        public float Lo = 50f;
        public float Hi = 200f;
        public bool Init;
    }

    /// <summary>
    /// 全局状态：对应 index.html 的 S 对象。
    /// </summary>
    public class GameState
    {
        // ---- 进度 ----
        public int Day = 1;
        public int Session = 1;
        public string Phase = "preopen";    // preopen | running | paused | settlement | evening

        // ---- 资源 ----
        public int CompleteNets = Cfg.StartNets;
        public List<BrokenNet> BrokenNets = new List<BrokenNet>();
        public int Worms;
        public Dictionary<string, int> Fish = new Dictionary<string, int>();
        public float Litter;                // 未满 1 价值的零头
        public int NextBrokenId = 1;

        // ---- 曲线 / 场面 ----
        public float Price = 100f;
        public float Elapsed;
        public MarketPath Path;             // null = 还没开盘
        public float Cd;
        public float CdTotal;
        public List<Net> Nets = new List<Net>();
        public int NextNetId = 1;

        // ---- 本场统计 ----
        public float RealizedPl;
        public List<NetRecord> SessionRecords = new List<NetRecord>();
        public float SessionLoot;
        public Dictionary<string, int> SessionBag = new Dictionary<string, int>();
        public int SessionWorms;
        /// <summary>本场线虫里来自"渔获分解"的那部分（渔获账目核对用）。</summary>
        public int SessionLootWorms;
        public int SessionFixed;
        public int SessionWormsUsed;
        public bool EndDayPending;

        // ---- 历史 ----
        public List<HistoryEntry> History = new List<HistoryEntry>();
        public float TotalPl;
        public float SessionInvested;
        public float TotalInvested;
        public SessionResult LastResult;
        public int DayStartNets = Cfg.StartNets;

        // ---- UI ----
        public UiState Ui = new UiState();
        public ViewRange View = new ViewRange();
        public int Revealed = 1;
        public float FlashUntil;

        // ---- 仪表盘 ----
        public List<Shock> Shocks = new List<Shock>();
        public GaugeState Gauge;
        public List<GaugeSegment> GaugeLayout;
        public List<float> GaugePlan = new List<float>();
        public int GaugeIdx;
        public GaugeStats GaugeStat = new GaugeStats();
        public Shock LastShock;

        public void ResetAll()
        {
            Day = 1;
            Session = 1;
            Phase = "preopen";
            CompleteNets = Cfg.StartNets;
            BrokenNets.Clear();
            Worms = 0;
            Fish.Clear();
            Litter = 0f;
            NextBrokenId = 1;
            NextNetId = 1;
            Path = null;
            Price = 100f;
            Elapsed = 0f;
            Revealed = 1;
            Nets.Clear();
            Cd = 0f;
            CdTotal = 0f;
            RealizedPl = 0f;
            TotalPl = 0f;
            SessionInvested = 0f;
            TotalInvested = 0f;
            Ui.Selected.Clear();
            SessionRecords.Clear();
            SessionLoot = 0f;
            SessionBag.Clear();
            SessionWorms = 0;
            SessionLootWorms = 0;
            SessionFixed = 0;
            SessionWormsUsed = 0;
            Shocks.Clear();
            LastShock = null;
            GaugeStat.Reset();
            GaugePlan.Clear();
            GaugeIdx = 0;
            Gauge = null;
            GaugeLayout = null;
            EndDayPending = false;
            History.Clear();
            LastResult = null;
            View.Init = false;
            View.Lo = 50f;
            View.Hi = 200f;
            Ui.CastCount = Mathf.Clamp(1, 0, CompleteNets);
            DayStartNets = Cfg.StartNets;
        }
    }
}
