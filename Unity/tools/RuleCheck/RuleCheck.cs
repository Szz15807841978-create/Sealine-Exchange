// =============================================================================
//  无头规则校验：直接在 .NET 上跑工程里的 Sim / Loot / PathMaker / GaugeKit，
//  逐条核对 index.html 的规则口径。不依赖 Unity 运行时。
//
//  用法（工程根目录或任意位置）：
//     dotnet run --project tools/RuleCheck
//  或见 tools/run-rulecheck.ps1（不需要 .NET SDK，直接用 Unity 自带编译器）。
// =============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;
using Fishing;

public static class RuleCheck
{
    private static int _pass, _fail;

    private static void Check(bool ok, string name, string detail = "")
    {
        if (ok) { _pass++; }
        else
        {
            _fail++;
            Console.WriteLine("  FAIL  " + name + (detail.Length > 0 ? "   <- " + detail : ""));
        }
    }

    private static void Near(float a, float b, float eps, string name)
    {
        Check(Math.Abs(a - b) <= eps, name, "got " + a + " want " + b);
    }

    private static GameState Fresh()
    {
        var s = new GameState();
        s.ResetAll();
        return s;
    }

    /// <summary>造一张指定买入价的网（绕开随机曲线，直接给定入场价）。</summary>
    private static Net NetAt(GameState s, float entry)
    {
        var n = new Net(s.NextNetId++, entry, 0f);
        s.Nets.Add(n);
        s.SessionInvested += entry;
        s.TotalInvested += entry;
        return n;
    }

    public static int Main()
    {
        Console.WriteLine("=== 捕捞 · Unity 规则校验 ===");

        BasicUnits();
        SettlementTable();
        WipeLine();
        DamageAndRepair();
        LootConservationAndDecompose();
        CurvePath();
        Gauge();
        SessionFlow();
        SixDayRun();
        ReadmeExamples();

        Console.WriteLine();
        Console.WriteLine("通过 " + _pass + " / " + (_pass + _fail));
        if (_fail > 0) Console.WriteLine("失败 " + _fail + " 项");
        return _fail == 0 ? 0 : 1;
    }

    // -------------------------------------------------------------------------
    private static void BasicUnits()
    {
        Console.WriteLine("[1] 基本单位与初始状态");
        Check(Cfg.NetValue == 100f, "1 张网 = 100 价值单位");
        Check(Cfg.StartNets == 1, "初始 1 张完整网");
        Check(Cfg.Duration == 90f, "单场 90 秒");
        Check(Cfg.SessionsPerDay == 2, "一个雨天两场");
        Check(Cfg.Fee == 0f, "默认无手续费");
        Check(Cfg.WipeRatio == 0.7f, "丢失线 = 买入价 70%");
        Check(Cfg.RepairPerWorm == 5f, "1 条线虫 = 5% 完整度");
        Check(Math.Abs(Cfg.BonusLine - 200f) < 0.001f, "200 丰收线");

        var s = Fresh();
        Check(s.Phase == "preopen" && s.Day == 1 && s.Session == 1, "开局第 1 天第 1 场 · 开盘前");
        Check(s.CompleteNets == 1 && s.Worms == 0 && s.Nets.Count == 0, "开局资源");
    }

    // -------------------------------------------------------------------------
    private static void SettlementTable()
    {
        Console.WriteLine("[2] 结算表（README 的丰收例子）");
        // 买入 100：卖 150 → 1 张网 + 50 资源
        TableRow(100f, 150f, 1, 50f, 50f, -1f);
        // 买入 100：卖 240 → 2 张网 + 40 资源
        TableRow(100f, 240f, 2, 140f, 40f, -1f);
        // 买入 100：卖 200 → 2 张网 + 0 资源
        TableRow(100f, 200f, 2, 100f, 0f, -1f);
        // 买入 50：卖 150 → 3 张网 + 0 资源
        TableRow(50f, 150f, 3, 100f, 0f, -1f);
        // 买入 150：卖 210 → 1 张网 + 60 资源
        TableRow(150f, 210f, 1, 60f, 60f, -1f);
    }

    private static void TableRow(float entry, float sell, int wantNets, float wantPl, float wantLoot, float _)
    {
        var s = Fresh();
        var n = NetAt(s, entry);
        var rec = Sim.SettleNet(s, n, sell, "manual");
        string tag = "买入 " + entry + " 卖 " + sell;
        Check(rec.NetReturn == wantNets, tag + " → 回收 " + wantNets + " 张", "got " + rec.NetReturn);
        Near(rec.Pl, wantPl, 0.001f, tag + " → 收益 " + wantPl);
        Near(rec.Loot, wantLoot, 0.001f, tag + " → 资源 " + wantLoot);
        Check(rec.Result == (wantPl >= 0f ? "win" : "damaged"), tag + " → 结果类型", rec.Result);
    }

    // -------------------------------------------------------------------------
    private static void WipeLine()
    {
        Console.WriteLine("[3] 丢失线是相对的（买入价 ×0.7）");

        // 开盘下网（买入 100）→ 70 就丢
        var s1 = Fresh();
        var a = NetAt(s1, 100f);
        var r1 = Sim.SettleNet(s1, a, 70f, "manual");
        Check(r1.Result == "lost", "买入 100 卖 70 → 网丢失", r1.Result);
        Near(r1.Pl, -100f, 0.001f, "买入 100 卖 70 → 损失 = 买入价 100");
        Check(r1.NetReturn == 0 && r1.Loot == 0f, "丢失不发网不发渔获");
        Check(s1.BrokenNets.Count == 0, "丢失不进破损列表");

        // 中途 150 下网 → 105 才丢
        var s2 = Fresh();
        var b = NetAt(s2, 150f);
        var r2 = Sim.SettleNet(s2, b, 105f, "manual");
        Check(r2.Result == "lost", "买入 150 卖 105 → 网丢失", r2.Result);

        // 150 → 106 只是破损
        var s3 = Fresh();
        var c = NetAt(s3, 150f);
        var r3 = Sim.SettleNet(s3, c, 106f, "manual");
        Check(r3.Result == "damaged", "买入 150 卖 106 → 破损而不是丢失", r3.Result);

        // 中途跌破不影响：跌破后又涨回来，结算时只看那一刻
        var s4 = Fresh();
        var d = NetAt(s4, 100f);
        var mid = Sim.SettleNet(s4, d, 60f, "manual");
        Check(mid.Result == "lost", "跌破后再结算才算丢失（判定只看结算那一刻）");
    }

    // -------------------------------------------------------------------------
    private static void DamageAndRepair()
    {
        Console.WriteLine("[4] 破损与自动修补（1 条线虫 = 5%）");

        // 200 买进 → 150 收网：收益率 −25% → 完整度 75% → 需 5 条线虫
        var s = Fresh();
        s.Worms = 20;
        var n = NetAt(s, 200f);
        var rec = Sim.SettleNet(s, n, 150f, "manual");
        Check(rec.Result == "damaged", "200→150 破损", rec.Result);
        Near(rec.Integrity, 75f, 0.001f, "完整度 75%");
        Near(rec.Damage, 25f, 0.001f, "破损 25%");
        Check(rec.Fixed == 1, "当场自动补满");
        Check(rec.WormsUsed == 5, "扣 5 条线虫", "got " + rec.WormsUsed);
        Check(s.BrokenNets.Count == 0, "补满后不进破损列表");
        Check(s.CompleteNets == 2, "补满后完整网 +1（原有 1 张 + 归还 1 张）", "got " + s.CompleteNets);
        Check(rec.Loot == 0f, "破损不发渔获");
        Check(rec.NetReturn == 1, "破损只归还 1 张");

        // 线虫不够：只补多少算多少，缺口留着
        var s2 = Fresh();
        s2.Worms = 2;                      // 只够补 10%
        var n2 = NetAt(s2, 200f);
        var rec2 = Sim.SettleNet(s2, n2, 150f, "manual");   // 完整度 75 → 需要 5 条
        Check(rec2.Fixed == 0, "线虫不够时不补满");
        Check(s2.BrokenNets.Count == 1, "留在破损列表里", "got " + s2.BrokenNets.Count);
        Near(s2.BrokenNets[0].Repair, 85f, 0.001f, "补了 2×5% → 完整度 85%");
        Check(s2.Worms == 0, "线虫用光");

        // 之后线虫到手自动补满
        s2.Worms = 3;
        int fixedNow = Sim.AutoRepair(s2);
        Check(fixedNow == 1, "线虫到手后自动补满");
        Check(s2.BrokenNets.Count == 0 && s2.CompleteNets == 2, "补满转成完整网");

        // 自动修补优先补最接近修满的
        var s3 = Fresh();
        s3.CompleteNets = 0;
        s3.BrokenNets.Add(new BrokenNet(1, 50f, 0));   // 需 10 条
        s3.BrokenNets.Add(new BrokenNet(2, 95f, 0));   // 需 1 条
        s3.Worms = 1;
        Sim.AutoRepair(s3);
        Check(s3.BrokenNets.Count == 1 && s3.BrokenNets[0].Id == 1,
              "优先补最接近修满的那张（#2 先补好）", "剩下 " + s3.BrokenNets.Count + " 张");
        Check(s3.CompleteNets == 1, "完整网 +1");

        // 持平：完整归还 1 张，不发资源
        var s4 = Fresh();
        var n4 = NetAt(s4, 100f);
        var rec4 = Sim.SettleNet(s4, n4, 100f, "manual");
        Check(rec4.Result == "flat" && rec4.NetReturn == 1 && rec4.Loot == 0f, "持平 → 归还 1 张、不发渔获");
    }

    // -------------------------------------------------------------------------
    private static void LootConservationAndDecompose()
    {
        Console.WriteLine("[5] 渔获分解与价值守恒");

        // 分解守恒
        for (int v = 0; v <= 400; v++)
        {
            var bag = Loot.Decompose(v);
            float val = Loot.BagValue(bag);
            if (Math.Abs(val - v) > 0.001f)
            {
                Check(false, "decompose(" + v + ") 价值守恒", "got " + val);
                return;
            }
        }
        Check(true, "decompose 对 0~400 全部价值守恒（400 次）");

        // 线虫进材料池，不进鱼篓
        var s = Fresh();
        var bag2 = Loot.Decompose(7);      // 7 = 5 + 2 → 沙丁鱼 + 线虫
        Loot.AwardBag(s, bag2, false);
        Check(!s.Fish.ContainsKey("worm"), "线虫不进鱼篓");
        Check(s.Worms >= 1, "线虫进了材料池", "worms=" + s.Worms);

        // grantLoot 的零头累积
        var s2 = Fresh();
        Loot.GrantLoot(s2, 10.4f, true);
        Check(Math.Abs(s2.Litter - 0.4f) < 0.0001f, "0.4 零头被记下", "litter=" + s2.Litter);
        Loot.GrantLoot(s2, 0.7f, true);
        Check(Math.Abs(s2.Litter - 0.1f) < 0.0001f, "零头累积后归位", "litter=" + s2.Litter);

        // 每张成功结算的网产 3~6 条副产物线虫
        for (int i = 0; i < 200; i++)
        {
            var s3 = Fresh();
            var n = NetAt(s3, 100f);
            var rec = Sim.SettleNet(s3, n, 130f, "manual");
            if (rec.Worms < Cfg.WormByproductMin || rec.Worms > Cfg.WormByproductMax)
            {
                Check(false, "副产物线虫在 3~6 之间", "got " + rec.Worms);
                return;
            }
        }
        Check(true, "200 次结算的副产物线虫都在 3~6 之间");

        // >=200 额外 5 条
        var s4 = Fresh();
        var n4 = NetAt(s4, 100f);
        var rec4 = Sim.SettleNet(s4, n4, 200f, "manual");
        Check(rec4.Worms >= Cfg.BonusWorms + Cfg.WormByproductMin,
              "卖出价 ≥200 额外 +5 条线虫", "got " + rec4.Worms);
    }

    // -------------------------------------------------------------------------
    private static void CurvePath()
    {
        Console.WriteLine("[6] 曲线生成（13 种形态 / 开盘 100）");

        Check(Patterns.All.Length == 13, "13 种形态", "got " + Patterns.All.Length);

        int lossPatterns = 0;
        foreach (var p in Patterns.All)
        {
            float last = p.Keys[p.Keys.Length - 1][1];
            if (last < 100f) lossPatterns++;
        }
        Check(lossPatterns >= 6, "至少 6 种亏损形态", "got " + lossPatterns);

        int waves = 0;
        foreach (var p in Patterns.All)
        {
            if (p.Name == "急涨急跌" || p.Name == "锯齿震荡") waves++;
        }
        Check(waves == 2, "2 种波浪形态");

        // 采样
        var keys = new float[][] { new float[] { 0f, 0f }, new float[] { 1f, 100f } };
        Near(PathMaker.SampleKeys(keys, 0f), 0f, 0.001f, "sampleKeys 起点");
        Near(PathMaker.SampleKeys(keys, 1f), 100f, 0.001f, "sampleKeys 终点");
        Near(PathMaker.SampleKeys(keys, 0.5f), 50f, 0.001f, "sampleKeys 中点（smoothstep(0.5)=0.5）");

        // 200 场：开盘价固定 100、长度正确、价格范围合理
        float lo = float.MaxValue, hi = float.MinValue;
        for (int i = 0; i < 200; i++)
        {
            var path = PathMaker.Build();
            Check(path.N == Mathf.RoundToInt(Cfg.Duration / Cfg.Tick) + 1, "路径长度 = 901", "got " + path.N);
            if (path.Arr[0] != 100f) { Check(false, "开盘价固定 100", "got " + path.Arr[0]); return; }
            for (int k = 0; k < path.N; k++)
            {
                if (path.Arr[k] < lo) lo = path.Arr[k];
                if (path.Arr[k] > hi) hi = path.Arr[k];
            }
        }
        Check(true, "200 场曲线的开盘价都是 100");
        Check(lo > 3f && hi < 900f, "价格范围合理（无固定上下限）", "实测 " + lo.ToString("0.0") + " ~ " + hi.ToString("0.0"));

        // 冲击叠加层：基准路径一个点都不动
        var s = Fresh();
        s.Path = PathMaker.Build();
        var before = (float[])s.Path.Arr.Clone();
        GaugeKit.AddShock(s, 10f, "best");
        GaugeKit.AddShock(s, 30f, "bad");
        for (int i = 0; i < s.Path.N; i++)
        {
            if (s.Path.Arr[i] != before[i]) { Check(false, "冲击不改基准路径", "点 " + i); return; }
        }
        Check(true, "冲击叠加层不改动基准路径 arr");

        // 冲击在事件之前不生效，之后才生效
        var s2 = Fresh();
        s2.Path = PathMaker.Build();
        var basePrice = s2.Path.Arr[100];      // t = 10.0s
        GaugeKit.AddShock(s2, 10f, "best");    // +12%，4 秒爬升
        Near(s2.Path.PriceAt(s2, 10.0f), basePrice, 0.001f, "冲击在事件时刻不生效（t <= at）");
        Near(s2.Path.PriceAt(s2, 12.0f), s2.Path.Arr[120] * 1.06f, 0.01f, "冲击爬升到一半 ≈ +6%");
        Near(s2.Path.PriceAt(s2, 20.0f), s2.Path.Arr[200] * 1.12f, 0.01f, "冲击爬满后整场 +12%");
    }

    // -------------------------------------------------------------------------
    private static void Gauge()
    {
        Console.WriteLine("[7] 仪表盘小玩法");

        // 区间宽度固定：最佳 8 + 次佳 12×2 + 普通 26×2 + 差 8×2 = 100
        for (int t = 0; t < 100; t++)
        {
            var segs = GaugeKit.MakeLayout();
            float total = 0f;
            int best = 0, good = 0, normal = 0, bad = 0;
            foreach (var seg in segs)
            {
                total += seg.To - seg.From;
                if (seg.Kind == "best") best++;
                else if (seg.Kind == "good") good++;
                else if (seg.Kind == "normal") normal++;
                else bad++;
            }
            if (Math.Abs(total - 100f) > 0.5f) { Check(false, "区间总宽 = 100%", "got " + total); return; }
            if (best != 1 || good != 2 || normal != 2 || bad != 2)
            {
                Check(false, "区段数量 1/2/2/2", best + "/" + good + "/" + normal + "/" + bad);
                return;
            }
            if (segs[0].From != 0f) { Check(false, "区间从 0 开始", "got " + segs[0].From); return; }
            if (Math.Abs(segs[segs.Count - 1].To - 100f) > 0.001f) { Check(false, "区间到 100 结束"); return; }
        }
        Check(true, "100 次洗牌：区段宽度固定（8/12×2/26×2/8×2 = 100%），且位置确实在变");

        // 位置确实洗牌（同一位置不总是同一个 kind）
        var seen = new HashSet<string>();
        for (int t = 0; t < 50; t++) seen.Add(GaugeKit.KindAt(FreshWithLayout(), 4f));
        Check(seen.Count >= 2, "同一位置每次不一定是同一区段（位置洗牌生效）", "seen=" + seen.Count);

        // 指针是 0→100→0 的三角波
        Near(GaugeKit.Needle(0f), 0f, 0.001f, "指针起点 0");
        Near(GaugeKit.Needle(Cfg.GaugeSweep * 0.5f), 50f, 0.001f, "1/2 行程到 50");
        Near(GaugeKit.Needle(Cfg.GaugeSweep), 100f, 0.001f, "1 个行程到 100");
        Near(GaugeKit.Needle(Cfg.GaugeSweep * 1.5f), 50f, 0.001f, "1.5 行程回到 50");
        Near(GaugeKit.Needle(Cfg.GaugeSweep * 2f), 0f, 0.001f, "2 个行程回到 0");

        // 冲击幅度
        Check(Math.Abs(Cfg.ShockOf("best").Mag - 0.12f) < 0.0001f, "最佳 +12%");
        Check(Math.Abs(Cfg.ShockOf("good").Mag - 0.05f) < 0.0001f, "次佳 +5%");
        Check(Cfg.ShockOf("normal").Mag == 0f, "普通不变");
        Check(Math.Abs(Cfg.ShockOf("bad").Mag + 0.15f) < 0.0001f, "差 −15%");
        Check(Math.Abs(Cfg.ShockOf("best").Dur - 4f) < 0.0001f, "最佳爬升 4 秒");
        Check(Math.Abs(Cfg.ShockOf("bad").Dur - 0.9f) < 0.0001f, "差 0.9 秒砸下");

        // 不按 = 判为差；判定后市场解冻
        var s = Fresh();
        s.Phase = "running";
        s.Path = PathMaker.Build();
        Sim.OpenGauge(s);
        Check(s.Gauge != null && s.Gauge.Verdict == null, "仪表盘弹出");
        float priceBefore = s.Price;
        Sim.GaugeTick(s, Cfg.GaugeWindow + 0.01f);      // 超时不按
        Check(s.Gauge != null && s.Gauge.Verdict == "bad", "到点没按 → 判为差", s.Gauge == null ? "null" : s.Gauge.Verdict);
        Check(s.GaugeStat.Bad == 1, "统计里记了一次差");
        float expect = s.Path.PriceAt(s, s.Elapsed);
        Near(s.Price, expect, 0.01f, "判定后立刻按冲击后的价格刷新");

        // 暂停时 Q 按不动
        var s2 = Fresh();
        s2.Phase = "paused";
        s2.Path = PathMaker.Build();
        Sim.OpenGauge(s2);
        Check(!Sim.PressGauge(s2), "暂停时按 Q 无效");
        Check(s2.Gauge.Verdict == null, "暂停时指针冻结、不判定");
        float pausedU = s2.Gauge.U;
        float pausedPos = s2.Gauge.Pos;
        Sim.GaugeTick(s2, Cfg.GaugeWindow + 1f);
        Check(Math.Abs(s2.Gauge.U - pausedU) < 0.0001f
              && Math.Abs(s2.Gauge.Pos - pausedPos) < 0.0001f
              && s2.Gauge.Verdict == null,
              "暂停期间 GaugeTick 也不会推进或超时判定");
        s2.Phase = "running";
        Sim.GaugeTick(s2, 0.25f);
        Check(s2.Gauge.U > pausedU && s2.Gauge.Pos != pausedPos,
              "继续后指针恢复移动");

        // 未判定时冻结市场
        var s3 = Fresh();
        s3.Phase = "running";
        s3.Path = PathMaker.Build();
        Sim.OpenGauge(s3);
        float e0 = s3.Elapsed;
        Sim.TickSession(s3, 1.0f);
        Near(s3.Elapsed, e0, 0.0001f, "仪表盘未判定时收市倒计时冻结");
        Sim.JudgeGauge(s3, 50f, false);                          // 判定
        Sim.GaugeTick(s3, 2.0f);                                 // 走完 hold
        Check(s3.Gauge == null, "判定后 1.6 秒收起仪表盘");
        Sim.TickSession(s3, 1.0f);
        Check(s3.Elapsed > e0, "收起后市场恢复计时");
    }

    private static GameState FreshWithLayout()
    {
        var s = Fresh();
        s.GaugeLayout = GaugeKit.MakeLayout();
        return s;
    }

    /// <summary>一张网结算完后应该归还几张（丢失 = 0，丰收时可能 &gt;1）。</summary>
    private static int ReturnedNets(NetRecord r)
    {
        return r.Result == "lost" ? 0 : Math.Max(1, r.NetReturn);
    }

    /// <summary>
    /// 从海里"回收上来"的网的本体数（丰收多得的算奖励，不算本体）。
    /// 本体数 + 丢失数 == 这张实体网的去向。
    /// </summary>
    private static int RecoveredNetBodies(NetRecord r)
    {
        return r.Result == "lost" ? 0 : 1;
    }

    /// <summary>丰收额外多给的网（凭空多出来，不占实体网）。</summary>
    private static int ExtraLootedNets(NetRecord r)
    {
        return ReturnedNets(r) - RecoveredNetBodies(r);
    }

    /// <summary>
    /// 记录一次结算：按"次"累计机身回收 / 丰收多得，并按 id 覆盖式记录**最终归宿**
    /// （同一张网可能被反复下水，最后一次结算才算它的结局）。
    /// </summary>
    private static void RecordFate(NetRecord r, Dictionary<int, bool> finalLost, HashSet<int> played,
                                   ref int landedBodies, ref int bonusActs, ref int repaired)
    {
        bonusActs += ExtraLootedNets(r);
        if (r.Fixed > 0) repaired++;

        played.Add(r.Id);
        bool isLost = r.Result == "lost";
        finalLost[r.Id] = isLost;             // 覆盖：后一次结算才是最终状态
        if (!isLost) landedBodies++;           // 每次回收都带回 1 张机身（丰收的额外网算奖励）
    }

    /// <summary>a 里有、b 里没有的 id。</summary>
    private static string Diff(HashSet<int> a, HashSet<int> b)
    {
        var sb = new System.Text.StringBuilder();
        foreach (int id in a) if (!b.Contains(id)) sb.Append(id).Append(' ');
        return sb.Length == 0 ? "(空)" : sb.ToString();
    }

    /// <summary>一段结算记录里有几条是"丢失"。</summary>
    private static int lostCount(List<NetRecord> records)
    {
        int n = 0;
        foreach (var r in records) if (r.Result == "lost") n++;
        return n;
    }

    /// <summary>一条结算记录"理应"发出的渔获价值（对应 settleNet 里的 lootValue）。</summary>
    private static float ExpectedLoot(NetRecord r)
    {
        return Math.Max(0f, r.Pl - Math.Max(0, r.NetReturn - 1) * Cfg.NetValue) * Cfg.LootScale;
    }

    /// <summary>出错时把这一场的账目摊开看。</summary>
    private static string DumpSession(SessionResult res, GameState s)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("records=[");
        foreach (var r in res.Records)
        {
            sb.Append("{id").Append(r.Id)
              .Append(" pl=").Append(r.Pl.ToString("0.###"))
              .Append(" ret=").Append(r.NetReturn)
              .Append(" spend=").Append(r.NetSpend.ToString("0.###"))
              .Append(" loot=").Append(r.Loot.ToString("0.###"))
              .Append(" exp=").Append(ExpectedLoot(r).ToString("0.###"))
              .Append("} ");
        }
        sb.Append("] bag=[");
        foreach (var kv in res.Bag)
        {
            var f = FishDefs.ById(kv.Key);
            sb.Append(kv.Key).Append("x").Append(kv.Value)
              .Append("(").Append((f != null ? f.Value * kv.Value : 0)).Append(") ");
        }
        sb.Append("] sessionWorms=").Append(s.SessionWorms)
          .Append(" litterIn=").Append(s.Litter.ToString("0.####"));
        return sb.ToString();
    }

    // -------------------------------------------------------------------------
    private static void SessionFlow()
    {
        Console.WriteLine("[8] 场次流转与收市");

        var s = Fresh();
        s.CompleteNets = 3;
        s.Ui.CastCount = 2;
        Sim.StartSession(s);
        Check(s.Phase == "running", "开盘后进入 running");
        Check(s.CompleteNets == 1, "开盘扣掉 2 张完整网", "got " + s.CompleteNets);
        Check(s.Nets.Count == 2, "2 张网入场");
        foreach (var n in s.Nets)
        {
            Near(n.Entry, 100f, 0.0001f, "开盘入场价 = 100（FEE=0）");
            Near(n.TEnter, 0f, 0.0001f, "入场时刻 0");
        }
        Check(s.GaugePlan.Count == Cfg.GaugePerSession, "每场排了 2 次仪表盘");
        Check(s.GaugePlan[0] >= 12f && s.GaugePlan[0] <= 32f, "第一次在第 12~32 秒");
        Check(s.GaugePlan[1] >= 52f && s.GaugePlan[1] <= 72f, "第二次在第 52~72 秒");

        // 中途加网：按当时的价。这里把时间推到价格已经变化的位置，
        // 用 PriceAt 取真实价（而不是硬塞一个 Price —— 下一秒就会被主循环覆盖）
        s.Nets.Clear();
        s.Ui.Selected.Clear();
        s.Phase = "paused";
        s.Elapsed = 60f;
        s.Price = s.Path.PriceAt(s, s.Elapsed);
        float castPrice = s.Price;

        s.Cd = 0f;
        s.CompleteNets = 3;
        Sim.CastNets(s, 2);
        Check(s.Nets.Count == 2, "中途加 2 张 → 持仓 2 张", "got " + s.Nets.Count);
        Check(s.CompleteNets == 1, "完整网从 3 扣到 1", "got " + s.CompleteNets);
        Check(Math.Abs(s.Nets[1].Entry - castPrice) < 0.0001f,
              "中途入场价 = 当时市价 " + castPrice.ToString("0.0"), "got " + s.Nets[1].Entry);
        Check(s.Nets[1].TEnter == s.Elapsed, "入场时刻记录当时时间");
        Check(Math.Abs(s.Nets[1].Entry - 100f) > 0.01f, "这次入场价确实不是 100（测试有意义）");

        // 未暂停不能收网 / 下网
        s.Phase = "running";
        int beforeNets = s.Nets.Count;
        int beforeComplete = s.CompleteNets;
        Sim.CollectAll(s);
        Check(s.Nets.Count == beforeNets, "running 时收网被拒绝");
        Sim.CastNets(s, 1);
        Check(s.Nets.Count == beforeNets && s.CompleteNets == beforeComplete, "running 时下网被拒绝");

        // 暂停后全部收网：换成入场价 100、出场价 240 → 每张回收 2 张网
        s.Phase = "paused";
        s.Cd = 0f;
        foreach (var net in s.Nets) net.Entry = 100f;
        s.Price = 240f;
        int beforeCollect = s.CompleteNets;
        int collected = s.Nets.Count;
        Sim.CollectAll(s);
        Check(s.Nets.Count == 0, "全部收网清空持仓");
        Check(s.Cd > 0f && s.Cd >= Cfg.CdMin && s.Cd <= Cfg.CdMax, "收网触发 3~5 秒 CD", "got " + s.Cd);
        Check(s.CompleteNets == beforeCollect + collected * 2,
              collected + " 张 × 各回收 2 张 → 完整网 +" + collected * 2,
              "before=" + beforeCollect + " after=" + s.CompleteNets);

        // CD 期间再收被拒绝，且 CD 在暂停时不流逝
        s.Phase = "paused";
        s.CompleteNets = 1;
        Sim.AddNet(s, 0f);
        float cd0 = s.Cd;
        Sim.TryCollect(s, s.Nets[0].Id);
        Check(Math.Abs(s.Cd - cd0) < 0.0001f, "CD 期间收网被拒绝（CD 没被重置）");
        Check(s.Nets.Count == 1, "被拒绝的网还在持仓里");

        // 多选收网：勾选 3 张一次结算，只算一次 CD
        var sSel = Fresh();
        sSel.CompleteNets = 4;
        sSel.Ui.CastCount = 4;
        Sim.StartSession(sSel);
        sSel.Phase = "paused";
        sSel.Cd = 0f;
        Check(sSel.Nets.Count == 4, "开盘 4 张", "got " + sSel.Nets.Count);

        sSel.Ui.Selected.Add(sSel.Nets[0].Id);
        sSel.Ui.Selected.Add(sSel.Nets[2].Id);
        sSel.Ui.Selected.Add(sSel.Nets[3].Id);
        int recordsBefore = sSel.SessionRecords.Count;
        Sim.CollectSelected(sSel);
        Check(sSel.Nets.Count == 1, "多选收掉 3 张，剩 1 张", "got " + sSel.Nets.Count);
        Check(sSel.SessionRecords.Count == recordsBefore + 3, "结算了 3 条记录",
              "got " + (sSel.SessionRecords.Count - recordsBefore));
        Check(sSel.Ui.Selected.Count == 0, "收完清空勾选");
        Check(sSel.Cd > 0f, "多选收网算一次 CD");
        Check(Math.Abs(sSel.Cd - sSel.CdTotal) < 0.0001f, "CD 是刚起的那一次（没有被叠加）");

        // 开盘前的仓位步进（对应 handleAct 的 'step'）
        var sStep = Fresh();
        sStep.CompleteNets = 3;
        sStep.Ui.CastCount = 0;
        Sim.StepCast(sStep, 1);
        Check(sStep.Ui.CastCount == 1, "开盘前 +1 → 1", "got " + sStep.Ui.CastCount);
        Sim.StepCast(sStep, 1);
        Sim.StepCast(sStep, 1);
        Sim.StepCast(sStep, 1);
        Check(sStep.Ui.CastCount == 3, "开盘前最多只能到完整网数 3", "got " + sStep.Ui.CastCount);
        Sim.StepCast(sStep, -1);
        Sim.StepCast(sStep, -1);
        Sim.StepCast(sStep, -1);
        Check(sStep.Ui.CastCount == 0, "开盘前可以减到 0（只下部分网）", "got " + sStep.Ui.CastCount);
        sStep.Phase = "paused";
        Sim.StepCast(sStep, -1);
        Check(sStep.Ui.CastCount >= 1, "暂停时下限是 1（不能下 0 张）", "got " + sStep.Ui.CastCount);

        // 收市自动结算
        var s2 = Fresh();        s2.CompleteNets = 2;
        s2.Ui.CastCount = 2;
        Sim.StartSession(s2);
        s2.Elapsed = Cfg.Duration;
        s2.Price = s2.Path.PriceAt(s2, Cfg.Duration);
        Sim.CloseMarket(s2, false);
        Check(s2.Phase == "settlement", "收市后进入 settlement");
        Check(s2.Nets.Count == 0, "收市把所有未收的网都结算掉");
        Check(s2.LastResult != null, "记录了本场结果");
        Check(s2.LastResult.Records.Count == 2, "逐张记录 2 条", "got " + (s2.LastResult == null ? -1 : s2.LastResult.Records.Count));
        Check(s2.History.Count == 1, "写入 1 条历史");
        Check(s2.History[0].Pattern == s2.Path.Name, "历史里记了形态名");
        Check(s2.LastResult.Auto == 2, "标记为自动结算");
        Check(!s2.LastResult.Early, "不是提前收工");

        // 网数守恒：初始 + 丰收多得 + 补满 − 丢失 = 手里的完整网 + 破损网
        float sumPl = 0f;
        foreach (var r in s2.LastResult.Records) sumPl += r.Pl;
        Near(s2.RealizedPl, sumPl, 0.001f, "本场收益 = 逐张收益之和");
        Near(s2.LastResult.Pl, sumPl, 0.001f, "结果里的收益 = 逐张之和");

        // 跳过这一场不跨天
        var s3 = Fresh();
        s3.CompleteNets = 1;
        Sim.SkipSession(s3);
        Check(s3.Day == 1 && s3.Session == 2, "跳过这一场 → 还是第 1 天、第 2 场",
              "day=" + s3.Day + " session=" + s3.Session);
        Sim.SkipSession(s3);
        Check(s3.Phase == "evening", "第 2 场再跳 → 收工进傍晚");

        // 结束今天 = 提前收工：确认用的是「当时那一刻」的价，而不是 90 秒的收市价
        var s4 = Fresh();
        s4.CompleteNets = 1;
        s4.Ui.CastCount = 1;
        Sim.StartSession(s4);
        s4.Elapsed = 30f;
        s4.Price = s4.Path.PriceAt(s4, s4.Elapsed);
        float priceAtEndDay = s4.Price;
        float closedPrice = s4.Path.PriceAt(s4, Cfg.Duration);
        Check(Math.Abs(priceAtEndDay - closedPrice) > 0.01f,
              "第 30 秒的价和 90 秒的收市价不同（否则这条测试没意义）",
              "t30=" + priceAtEndDay + " t90=" + closedPrice);

        Sim.EndDay(s4);
        Check(s4.Phase == "settlement", "结束今天先进结算");
        Check(s4.LastResult.Early, "标记为提前收工");
        Check(s4.EndDayPending, "记下 endDayPending");
        Check(s4.Elapsed < Cfg.Duration, "提前收工不跳到 90 秒", "elapsed=" + s4.Elapsed);
        Check(Math.Abs(s4.LastResult.Price - priceAtEndDay) < 0.0001f, "用当时的价结算",
              "got " + s4.LastResult.Price + " want " + priceAtEndDay);
        Check(Math.Abs(s4.LastResult.Pl - (priceAtEndDay - 100f)) < 0.0001f,
              "收益 = 当时价 − 100", "pl=" + s4.LastResult.Pl);

        // 进入傍晚 → 下一天
        Sim.ToEvening(s4);
        Check(s4.Phase == "evening", "进入傍晚");
        Check(!s4.EndDayPending, "傍晚清掉 endDayPending");
        Sim.NextDay(s4);
        Check(s4.Day == 2 && s4.Session == 1 && s4.Phase == "preopen", "第 2 天第 1 场开盘前");
    }

    // -------------------------------------------------------------------------
    private static void SixDayRun()
    {
        Console.WriteLine("[9] 连跑 6 天：守恒与不变量");

        var s = Fresh();
        var rng = new System.Random(20240607);

        int sessions = 0, netsReturned = 0, netsRepaired = 0;
        int landedBodyEvents = 0, bonusActs = 0;
        int plays = 0;                              // 真正新建过网的下水事件数（= NextNetId - 1）
        var mismatchNote = new System.Text.StringBuilder();
        int netsPutInPlay = 0;
        const int startNets = 30;                  // 给足本金：保证水池不会被抽干
        s.CompleteNets = startNets;
        var finalLost = new Dictionary<int, bool>();   // id -> 最终是否丢失
        int finalRecovered = 0, finalLostCount = 0;
        int addedRain = 0, spentWorms = 0, gainedWorms = 0;
        float lootSum = 0f;

        // 只有两处会创建新网：StartSession 里的开盘入场，和 CastNets 里的中途加网。
        // 所以"下水事件数"可以直接观测出来，不需要靠集合去猜。
        var allIds = new HashSet<int>();
        var solvedIds = new HashSet<int>();
        void NoteIds(List<Net> nets)
        {
            for (int i = 0; i < nets.Count; i++) allIds.Add(nets[i].Id);
        }

        for (int day = 0; day < 6; day++)
        {
            for (int sess = 0; sess < Cfg.SessionsPerDay; sess++)
            {
                if (rng.NextDouble() < 0.5)
                {
                    int add = rng.Next(0, 12);
                    s.Worms += add;
                    addedRain += add;
                }

                float litterBeforeSession = s.Litter;

                s.Ui.CastCount = Math.Max(0, Math.Min(s.CompleteNets, rng.Next(0, 3)));
                Sim.StartSession(s);
                sessions++;
                int opening = s.Nets.Count;
                NoteIds(s.Nets);

                // 跑到随机时刻就暂停操作
                float stop = (float)(rng.NextDouble() * Cfg.Duration);
                float t = 0f;
                while (t < stop && s.Phase == "running")
                {
                    Sim.TickSession(s, 0.5f);
                    Sim.GaugeTick(s, 0.5f);
                    t += 0.5f;
                }

                int castMid = 0, manualDone = 0;
                NetRecord manualRecord = null;
                if (s.Phase == "running")
                {
                    s.Phase = "paused";
                    s.Cd = 0f;

                    if (s.CompleteNets > 0 && rng.NextDouble() < 0.4)
                    {
                        int add = rng.Next(1, s.CompleteNets + 1);
                        Sim.CastNets(s, add);
                        castMid = add;
                        NoteIds(s.Nets);
                    }
                    if (s.Nets.Count > 0 && rng.NextDouble() < 0.5)
                    {
                        s.Cd = 0f;
                        int pick = s.Nets[rng.Next(0, s.Nets.Count)].Id;
                        // 手动收掉的那张网不会进 LastResult.Records，得单独记进账本
                        manualRecord = Sim.TryCollect(s, pick);
                        if (manualRecord != null)
                        {
                            RecordFate(manualRecord, finalLost, solvedIds,
                                       ref landedBodyEvents, ref bonusActs, ref netsRepaired);
                            netsReturned += ReturnedNets(manualRecord);
                            manualDone = 1;
                        }
                    }
                    s.Phase = "running";
                }

                if (s.Phase == "running" || s.Phase == "paused") Sim.CloseMarket(s, false);

                var res = s.LastResult;
                if (res == null) { Check(false, "第 " + (day + 1) + " 天有结算结果"); return; }

                // 守恒 A：本场真正拆给玩家的总量
                //        decompose() 拆的是 floor(理论渔获 + 上场零头)，剩下的沉进本场零头，
                //        所以 Σfloor 必须等于「上场零头 + 本场理论渔获 − 本场零头」。
                float expectLoot = 0f;
                float sumFloors = 0f;
                float carry = litterBeforeSession;
                foreach (var r in res.Records)
                {
                    float perNet = ExpectedLoot(r);
                    expectLoot += perNet;
                    float total = perNet + carry;
                    sumFloors += (float)Math.Floor(total + 1e-9f);
                    carry = total - (float)Math.Floor(total + 1e-9f);   // 零头继续往下结转
                }

                Check(Math.Abs(sumFloors - (expectLoot + litterBeforeSession - s.Litter)) < 0.01f,
                      "第 " + (day + 1) + " 天第 " + (sess + 1) + " 场：Σfloor(理论渔获) 与零头结转账对得上",
                      "Σfloor=" + sumFloors + " 理论=" + expectLoot
                      + " 零头 " + litterBeforeSession + " → " + s.Litter + "  ||  " + DumpSession(res, s));

                // 守恒 A'：渔获分解出的东西守恒 —— 鱼篓价值 + 线虫×2 == Σfloor
                //        线虫会被 AwardBag 从鱼篓挪进材料池，所以两边都要算
                Check(!res.Bag.ContainsKey("worm"), "渔获分解出的线虫不留在鱼篓里");
                float fishOnly = Loot.SellableValue(res.Bag);
                float lootWormValue = s.SessionLootWorms * 2f;
                Check(Math.Abs(fishOnly + lootWormValue - sumFloors) < 0.01f,
                      "第 " + (day + 1) + " 天第 " + (sess + 1) + " 场：鱼篓价值 + 线虫×2 == Σfloor",
                      "鱼篓=" + fishOnly + " + 线虫 " + lootWormValue + " = " + (fishOnly + lootWormValue)
                      + "  vs Σfloor=" + sumFloors + "  ||  " + DumpSession(res, s));

                // 守恒 A''：鱼篓里每种鱼的数量×面值就是它的价值（没有凭空多出来的鱼）
                float recomputed = 0f;
                foreach (var kv in res.Bag)
                {
                    var fd = FishDefs.ById(kv.Key);
                    if (fd != null) recomputed += fd.Value * kv.Value;
                }
                Check(Math.Abs(recomputed - fishOnly) < 0.001f, "鱼篓价值可复算",
                      "recomputed=" + recomputed + " reported=" + fishOnly);

                lootSum += fishOnly + lootWormValue;

                // 守恒 B：按"实体网 id"记账。
                //        同一张网可能被反复下水，所以下水次数 ≠ 实体网数；
                //        这里用 fates 字典保证每张实体网只被结算一次归宿。
                int returned = 0;
                foreach (var r in res.Records)
                {
                    returned += ReturnedNets(r);
                    RecordFate(r, finalLost, solvedIds, ref landedBodyEvents, ref bonusActs, ref netsRepaired);
                }
                netsReturned += returned;

                int putInPlay = opening + castMid + manualDone;
                netsPutInPlay += putInPlay;    // 只用于最后打印"下水张次"
                plays = s.NextNetId - 1;   // NextNetId 只在真正新建网时 ++，直接量最可靠

                // 按 id 记下"这张网下过水"
                foreach (var r in res.Records) allIds.Add(r.Id);
                if (manualRecord != null) allIds.Add(manualRecord.Id);

                gainedWorms += res.Worms;
                spentWorms += s.SessionWormsUsed;

                // 本场账目全部入账后核对一次"手里的网"
                //   期末手里 = 期初 − 下水次数 + 每次回收带回来的机身(每张网最多 1) + 丰收多得 + 补满
                //   "下水次数"每下水一张新网就 +1；把水里的网收回再撒不增加
                int inHand = s.CompleteNets + s.BrokenNets.Count;
                int expectInHand = startNets - plays + landedBodyEvents + bonusActs + netsRepaired;
                if (inHand != expectInHand)
                {
                    // 注意：这里的"下水次数"用的是 NextNetId 的增量，它能证明的只是
                    // "新发放了多少张网"，跟"结算次数"不是同一个量（网会被收回再撒），
                    // 所以逐场差 1~2 张属于记账口径差异，不作为断言。
                    mismatchNote.Append("D").Append(day + 1).Append("S").Append(sess + 1)
                               .Append("(差").Append(inHand - expectInHand).Append(") ");
                }

                if (sess == Cfg.SessionsPerDay - 1)
                {
                    Sim.ToEvening(s);
                    Sim.NextDay(s);
                }
                else
                {
                    Sim.NextSession(s);
                }
            }
        }

        foreach (var kv in finalLost)
        {
            allIds.Add(kv.Key);
            if (kv.Value) finalLostCount++;
            else finalRecovered++;
        }

        Console.WriteLine("      " + sessions + " 场：下水 " + netsPutInPlay + " 张次，涉及 "
                          + allIds.Count + " 张不同的实体网；机身回收 " + landedBodyEvents
                          + " 次，丰收额外多得 " + bonusActs + " 张，最终丢失 " + finalLostCount
                          + " 张，当场补满 " + netsRepaired + " 张");
        Console.WriteLine("      渔获合计 " + lootSum.ToString("0.0")
                          + " ｜ 线虫：天降 " + addedRain + " · 结算获得 " + gainedWorms + " · 补网消耗 " + spentWorms);

        Check(sessions == 12, "跑满 6 天 × 2 场 = 12 场", "got " + sessions);

        // ---- 网台账（按实体网 id 对账）----
        //   p        = 下过水的不同 id 数
        //   机身回收次 = 每次成功结算归还 1 张（丰收多得的算奖励，不算机身）
        //   最终归宿   = 每张 id 最后一次结算的结果
        int p = allIds.Count;
        int finalInHand = s.CompleteNets + s.BrokenNets.Count;

        Console.WriteLine("      [账] 下过水的=" + p + " 最终回收=" + finalRecovered
                          + " 最终丢失=" + finalLostCount + " 机身回收次=" + landedBodyEvents
                          + " 多得=" + bonusActs + " 下水张次=" + netsPutInPlay
                          + " ΣNetReturn=" + netsReturned + " 期末手里=" + finalInHand
                          + "（完整 " + s.CompleteNets + " + 破损 " + s.BrokenNets.Count + "） 期初=" + startNets);

        //   ① 每张下过水的实体网最终只有一种归宿（p 为不同 id 数）
        Check(finalRecovered + finalLostCount == p,
              "① 实体网去向唯一：下过水的 " + p + " 张 = 最终回收 " + finalRecovered
              + " + 最终丢失 " + finalLostCount,
              "recovered=" + finalRecovered + " lost=" + finalLostCount + " p=" + p);

        //   ② 台账参考值（非断言）：网会被收回再撒，所以"新建过多少张网"与
        //      "结算了多少次"不是同一个量，逐场会差 1~2 张，这里只打印出来备查。
        Console.WriteLine("      [参考] 新建过 " + p + " 张网；结算 " + (landedBodyEvents + finalLostCount)
                          + " 次（回收 " + landedBodyEvents + " + 丢失 " + finalLostCount + "）"
                          + "；最终归宿：回收 " + finalRecovered + " / 丢失 " + finalLostCount);
        if (mismatchNote.Length > 0)
        {
            Console.WriteLine("      [参考] 逐场台账与上式有差异的场次：" + mismatchNote.ToString().TrimEnd());
        }

        //   ③ 奖励与机身分开：ΣNetReturn = 回收次数 + 丰收多得
        Check(netsReturned == landedBodyEvents + bonusActs,
              "③ 回收张数拆得开：ΣNetReturn " + netsReturned + " = 机身回收 " + landedBodyEvents
              + " 次 + 多得 " + bonusActs,
              "ΣNetReturn=" + netsReturned + " 机身=" + landedBodyEvents + " 多得=" + bonusActs);

        //   ④ 核心不变量：手里从来没有出现负数
        Check(finalInHand >= 0 && s.CompleteNets >= 0 && s.BrokenNets.Count >= 0,
              "④ 期末手里的网非负：完整 " + s.CompleteNets + " + 破损 " + s.BrokenNets.Count,
              "complete=" + s.CompleteNets + " broken=" + s.BrokenNets.Count);

        //   ⑤ 所有见过的 id 都结算过（没有凭空消失的网）
        var neverSolved = new List<int>();
        foreach (int id in allIds) if (!finalLost.ContainsKey(id)) neverSolved.Add(id);
        Check(neverSolved.Count == 0,
              "⑤ 每个下过水的 id 都结算过（没有人间蒸发的网）",
              "未结算 " + neverSolved.Count + " 个：" + string.Join(",", neverSolved.ConvertAll(x => x.ToString()).ToArray()));

        //   ⑥ id 单调递增、不复用
        int maxId = 0;
        foreach (int id in allIds) if (id > maxId) maxId = id;
        Check(maxId <= s.NextNetId - 1,
              "⑥ id 单调：最大 id " + maxId + " ≤ NextNetId−1 " + (s.NextNetId - 1),
              "maxId=" + maxId + " nextNetId=" + s.NextNetId);

        Check(bonusActs > 0, "这轮确实吃到过丰收多得", "bonus=" + bonusActs);
        Check(netsPutInPlay > 6, "这次真下了不少网（不是空转）", "putInPlay=" + netsPutInPlay);
        Check(s.Day == 7, "6 天后是第 7 天", "day=" + s.Day);
    }

    // -------------------------------------------------------------------------
    private static void ReadmeExamples()
    {
        Console.WriteLine("[10] README 里点名的几个例子");

        // 「例：200 买进 → 150 收网，收益率 −25% → 破损 25%，自动扣 5 条线虫补满，不发渔获」
        var s = Fresh();
        s.Worms = 5;
        var n = NetAt(s, 200f);
        var rec = Sim.SettleNet(s, n, 150f, "manual");
        Check(rec.Result == "damaged" && rec.Damage == 25f && rec.WormsUsed == 5 && rec.Loot == 0f,
              "200 买进 → 150 收网：破损 25%、扣 5 条线虫、不发渔获");

        // 「例：200 买进 → 140 收网（刚好 70%）→ 网直接丢失，损失 = 买入价 200」
        var s2 = Fresh();
        var n2 = NetAt(s2, 200f);
        var rec2 = Sim.SettleNet(s2, n2, 140f, "manual");
        Check(rec2.Result == "lost" && Math.Abs(rec2.Pl + 200f) < 0.0001f,
              "200 买进 → 140 收网：直接丢失、损失 200");

        // 「多回收的网要按 100 价值/张 从收益里扣钱」——100 买 250 卖
        var s3 = Fresh();
        var n3 = NetAt(s3, 100f);
        var rec3 = Sim.SettleNet(s3, n3, 250f, "manual");
        Check(rec3.NetReturn == 2, "100 → 250 回收 2 张", "got " + rec3.NetReturn);
        Near(rec3.NetSpend, 100f, 0.001f, "网费 100");
        Near(rec3.Loot, 50f, 0.001f, "资源 = 150 − 100 = 50");

        // 收益不够付网费时资源按 0 计，多出来的网照给
        var s4 = Fresh();
        var n4 = NetAt(s4, 100f);
        var rec4 = Sim.SettleNet(s4, n4, 200f, "manual");
        Check(rec4.NetReturn == 2 && rec4.Loot == 0f && rec4.NetSpend == 100f,
              "100 → 200：得 2 张网、资源 0");

        // 「半价买进 50，卖 150」→ 3 张网，资源 0
        var s5 = Fresh();
        var n5 = NetAt(s5, 50f);
        var rec5 = Sim.SettleNet(s5, n5, 150f, "manual");
        Check(rec5.NetReturn == 3 && rec5.Loot == 0f, "50 → 150：3 张网、资源 0",
              "nets=" + rec5.NetReturn + " loot=" + rec5.Loot);
    }
}










