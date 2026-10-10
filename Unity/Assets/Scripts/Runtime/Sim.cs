using System;
using System.Collections.Generic;
using UnityEngine;

namespace Fishing
{
    /// <summary>
    /// 纯规则层：所有捕捞规则都在这里，不碰任何 UI。
    ///
    /// 抽出这一层有两个好处：
    ///   1. GameController 只负责"驱动 + 显示"，规则集中在一处；
    ///   2. 这些方法只依赖 GameState / Cfg / Rng / Loot / PathMaker，
    ///      可以在无头环境下单独跑断言（见 tools/ 里的校验脚本）。
    ///
    /// 方法名与 index.html 里的同名函数一一对应。
    /// </summary>
    public static class Sim
    {
        /// <summary>需要提示玩家时触发（对应 index.html 的 toast）。</summary>
        public static event Action<string> Toast;

        public static void Say(string msg)
        {
            var h = Toast;
            if (h != null) h(msg);
        }

        // =====================================================================
        //  下网 / CD
        // =====================================================================

        /// <summary>对应 addNet(tEnter)：一张网永远只花 1 张完整网。</summary>
        public static float AddNet(GameState s, float tEnter)
        {
            float price = tEnter <= 0f ? 100f : s.Price;
            float entry = price * (1f + Cfg.Fee);
            int id = s.NextNetId++;
            s.SessionInvested += entry;
            s.TotalInvested += entry;
            s.Nets.Add(new Net(id, entry, tEnter));
            return entry;
        }

        /// <summary>对应 startCD()。</summary>
        public static void StartCD(GameState s)
        {
            s.CdTotal = Rng.Range(Cfg.CdMin, Cfg.CdMax);
            s.Cd = s.CdTotal;
            Say("收网 CD " + Num.F1(s.Cd) + "s");
        }

        // =====================================================================
        //  单张网结算
        // =====================================================================

        /// <summary>
        /// 对应 settleNet(net, price, mode)。
        ///  - 回收网数 = 向下取整(卖出价 ÷ 买入价)，至少 1 张 —— 涨一倍多回收一张
        ///  - 多回收的网要按 NET_VALUE 从收益里扣钱
        ///  - 收益率 ≥ 0         ：完整归还
        ///  - −30% &lt; 收益率 &lt; 0 ：按亏损比例破损，结算后立刻自动扣线虫补满
        ///  - 跌到买入价的 70% 及以下：收网即永久丢失，损失 = 买入价
        /// </summary>
        public static NetRecord SettleNet(GameState s, Net net, float price, string mode)
        {
            float pl0 = price - net.Entry;
            float ratio = net.Entry > 0f ? price / net.Entry : 1f;

            string result;
            float pl = pl0, damage = 0f, integrity = 100f;

            if (ratio <= Cfg.WipeRatio)
            {
                result = "lost";
                pl = -net.Entry;                                    // 永久丢失，损失 = 买入价
            }
            else if (pl0 < 0f)
            {
                result = "damaged";
                integrity = Mathf.Round(Mathf.Max(0f, ratio) * 1000f) / 10f;  // 完整度，75 = 破了 25%
                damage = Mathf.Round((100f - integrity) * 10f) / 10f;
            }
            else if (pl0 == 0f)
            {
                result = "flat";
            }
            else
            {
                result = "win";
            }

            float lootValue = 0f;
            int worms = 0, bonusWorms = 0, netReturn = 0;
            float netSpend = 0f;
            BrokenNet brokenEntry = null;

            if (result != "lost")
            {
                netReturn = (result == "damaged" || result == "flat")
                    ? 1
                    : Mathf.Max(1, Mathf.FloorToInt(ratio + 1e-9f));
                int extra = Mathf.Max(0, netReturn - 1);

                if (result == "damaged")
                {
                    brokenEntry = new BrokenNet(s.NextBrokenId++, integrity, net.Id);
                    s.BrokenNets.Add(brokenEntry);
                }
                else
                {
                    s.CompleteNets += netReturn;
                }

                if (pl > 0f)
                {
                    netSpend = extra * Cfg.NetValue;                 // 多拿的网要付钱
                    lootValue = Mathf.Max(0f, pl - netSpend) * Cfg.LootScale;
                    if (lootValue > 0f)
                    {
                        var bag = Loot.GrantLoot(s, lootValue, true);
                        s.SessionLoot += lootValue;
                        Loot.Merge(s.SessionBag, bag);
                    }
                    worms = Rng.Range(Cfg.WormByproductMin, Cfg.WormByproductMax);
                }

                if (price >= Cfg.BonusLine) bonusWorms = Cfg.BonusWorms;
                if (worms + bonusWorms > 0)
                {
                    s.Worms += worms + bonusWorms;
                    s.SessionWorms += worms + bonusWorms;
                }
            }

            s.RealizedPl += pl;
            s.TotalPl += pl;

            // 结算后立刻自动补网（线虫不够就先欠着，下次线虫到手再补）
            AutoRepair(s);

            // 这张网是不是当场就被线虫补满了？（对应 JS 里的 wasFixed）
            bool wasFixed = brokenEntry != null && !s.BrokenNets.Contains(brokenEntry);
            var rec = new NetRecord
            {
                Id = net.Id,
                Entry = net.Entry,
                Price = price,
                Pl = pl,
                Result = result,
                Mode = mode,
                Loot = lootValue,
                Worms = worms + bonusWorms,
                Damage = damage,
                Integrity = integrity,
                NetReturn = netReturn,
                NetSpend = netSpend,
                Fixed = wasFixed ? 1 : 0,
                // 补满这张网消耗的线虫 = ceil(破损% / 每条的%)，对应 JS 的 Math.ceil(damage / REPAIR_PER_WORM)
                WormsUsed = wasFixed ? Mathf.CeilToInt(damage / Cfg.RepairPerWorm) : 0,
            };
            s.SessionRecords.Add(rec);
            return rec;
        }

        /// <summary>
        /// 对应 autoRepair()：优先把最接近修满的网补完整
        /// （用最少线虫换回 1 张可用网）。1 条线虫 = REPAIR_PER_WORM % 完整度；
        /// 线虫不足就只补多少算多少。返回这次修好了几张。
        /// </summary>
        public static int AutoRepair(GameState s)
        {
            int fixedCount = 0, used = 0;

            if (s.BrokenNets.Count > 0 && s.Worms > 0)
            {
                s.BrokenNets.Sort((a, b) => b.Repair.CompareTo(a.Repair));

                for (int i = 0; i < s.BrokenNets.Count; i++)
                {
                    var b = s.BrokenNets[i];
                    int need = Mathf.CeilToInt((100f - b.Repair) / Cfg.RepairPerWorm);
                    int use = Mathf.Min(need, s.Worms);
                    if (use <= 0) break;

                    s.Worms -= use;
                    used += use;
                    b.Repair = Mathf.Min(100f, Mathf.Round((b.Repair + use * Cfg.RepairPerWorm) * 10f) / 10f);

                    if (b.Repair >= 100f)
                    {
                        s.BrokenNets.RemoveAt(i);
                        i--;
                        s.CompleteNets += 1;
                        fixedCount++;
                    }
                }
            }

            s.SessionFixed += fixedCount;
            s.SessionWormsUsed += used;
            return fixedCount;
        }

        // =====================================================================
        //  玩家操作
        // =====================================================================

        /// <summary>对应 tryCollect(id)。返回 null 表示这次操作被拒绝。</summary>
        public static NetRecord TryCollect(GameState s, int id)
        {
            if (s.Phase != "paused") { Say("先暂停"); return null; }
            if (s.Cd > 0f) { Say("CD " + Num.F1(s.Cd) + "s"); return null; }

            int idx = s.Nets.FindIndex(n => n.Id == id);
            if (idx < 0) return null;

            var net = s.Nets[idx];
            s.Nets.RemoveAt(idx);

            float price = s.Price;
            var rec = SettleNet(s, net, price, "manual");
            StartCD(s);

            Say("网#" + net.Id + " " + Num.F1(net.Entry) + "→" + Num.F1(price)
                + " · 收益 " + Num.Signed(rec.Pl) + CollectNote(rec));
            return rec;
        }

        /// <summary>对应 tryCollect 里拼的那段尾注。</summary>
        public static string CollectNote(NetRecord rec)
        {
            if (rec.Result == "lost") return " · 网丢失";
            if (rec.Result == "damaged")
            {
                return " · 破损 " + Num.F1(rec.Damage) + "%" + (rec.Fixed > 0 ? " 已补满" : " 缺线虫");
            }
            if (rec.NetReturn > 1) return " · 得 " + rec.NetReturn + " 张网 · 资源 " + Num.F1(rec.Loot);
            return rec.Loot > 0f ? " · 资源 " + Num.F1(rec.Loot) : "";
        }

        /// <summary>对应 collectAll()。</summary>
        public static void CollectAll(GameState s)
        {
            if (s.Phase != "paused") { Say("先暂停"); return; }
            if (s.Cd > 0f) { Say("CD " + Num.F1(s.Cd) + "s"); return; }
            if (s.Nets.Count == 0) { Say("没有持仓"); return; }

            var list = new List<Net>(s.Nets);
            s.Nets.Clear();

            float sum = 0f, loot = 0f;
            int gone = 0, extraNets = 0;
            for (int i = 0; i < list.Count; i++)
            {
                var r = SettleNet(s, list[i], s.Price, "manual");
                sum += r.Pl;
                loot += r.Loot;
                if (r.Result == "lost") gone++;
                extraNets += r.NetReturn - 1;
            }

            StartCD(s);
            Say("收 " + list.Count + " 张 · 收益 " + Num.Signed(sum) + " · 资源 " + Num.F1(loot)
                + (extraNets > 0 ? " · 得 " + (list.Count + extraNets) + " 张网" : "")
                + (gone > 0 ? " · 消失 " + gone : ""));
        }

        /// <summary>对应 collectSelected()：勾选的几张一次结算，算一次 CD。</summary>
        public static void CollectSelected(GameState s)
        {
            if (s.Phase != "paused") { Say("先暂停"); return; }
            if (s.Cd > 0f) { Say("CD " + Num.F1(s.Cd) + "s"); return; }

            var list = new List<Net>();
            for (int i = 0; i < s.Nets.Count; i++)
            {
                if (s.Ui.Selected.Contains(s.Nets[i].Id)) list.Add(s.Nets[i]);
            }
            if (list.Count == 0) { Say("没勾选"); return; }

            s.Nets.RemoveAll(n => s.Ui.Selected.Contains(n.Id));
            s.Ui.Selected.Clear();

            float sum = 0f, loot = 0f;
            int extra = 0, gone = 0;
            for (int i = 0; i < list.Count; i++)
            {
                var r = SettleNet(s, list[i], s.Price, "manual");
                sum += r.Pl;
                loot += r.Loot;
                extra += r.NetReturn - 1;
                if (r.Result == "lost") gone++;
            }

            StartCD(s);
            Say("收 " + list.Count + " 张 · 收益 " + Num.Signed(sum) + " · 资源 " + Num.F1(loot)
                + (extra > 0 ? " · 得 " + (list.Count + extra) + " 张网" : "")
                + (gone > 0 ? " · 消失 " + gone : ""));
        }

        /// <summary>对应 castNets(count)：中途加网，按当前价各自入场。</summary>
        public static void CastNets(GameState s, int count)
        {
            if (s.Phase != "paused") { Say("先暂停"); return; }
            if (s.CompleteNets <= 0) { Say("没有完整网"); return; }

            int n = Num.Clamp(count, 1, s.CompleteNets);
            for (int i = 0; i < n; i++)
            {
                s.CompleteNets--;
                AddNet(s, s.Elapsed);
            }
            s.Ui.CastCount = 1;
            Say("下网 " + n + " 张");
        }

        // =====================================================================
        //  开场 / 收市 / 场次流转
        // =====================================================================

        /// <summary>对应 startSession()。</summary>
        public static void StartSession(GameState s)
        {
            int n = Num.Clamp(s.Ui.CastCount, 0, s.CompleteNets);
            s.Path = PathMaker.Build();
            s.Elapsed = 0f;
            s.Revealed = 1;
            s.Price = 100f;
            s.View.Init = false;

            s.Nets.Clear();
            s.RealizedPl = 0f;
            s.SessionRecords.Clear();
            s.SessionLoot = 0f;
            s.SessionBag.Clear();
            s.SessionWorms = 0;
            s.SessionLootWorms = 0;
            s.SessionFixed = 0;
            s.SessionWormsUsed = 0;
            s.SessionInvested = 0f;
            s.Ui.Selected.Clear();

            AutoRepair(s);   // 开场前先把攒下的线虫用掉，能修几张修几张

            s.Shocks.Clear();
            s.LastShock = null;
            s.GaugeStat.Reset();
            s.GaugePlan = GaugeKit.Plan();
            s.GaugeIdx = 0;
            s.Gauge = null;
            s.Cd = 0f;
            s.CdTotal = 0f;
            s.EndDayPending = false;

            s.CompleteNets -= n;
            for (int i = 0; i < n; i++) AddNet(s, 0f);

            s.Phase = "running";
            Say(n > 0 ? "下网 " + n + " 张" : "空仓看盘");
        }

        /// <summary>
        /// 对应 closeMarket(opts)：所有还没收的网按当前价自动结算
        /// （撒下的网不会留到下一场）。early = true 表示"提前收工"。
        /// </summary>
        public static void CloseMarket(GameState s, bool early)
        {
            if (s.Phase != "running" && s.Phase != "paused") return;

            if (!early) s.Elapsed = Cfg.Duration;
            s.Revealed = s.Path != null
                ? Num.Clamp(Mathf.FloorToInt(s.Elapsed / Cfg.Tick) + 1, 1, s.Path.N)
                : 1;
            s.Price = s.Path != null ? s.Path.PriceAt(s, s.Elapsed) : 100f;
            float price = s.Price;

            var list = new List<Net>(s.Nets);
            s.Nets.Clear();
            for (int i = 0; i < list.Count; i++) SettleNet(s, list[i], price, early ? "manual" : "auto");

            s.LastResult = new SessionResult
            {
                Records = new List<NetRecord>(s.SessionRecords),
                Bag = new Dictionary<string, int>(s.SessionBag),
                Loot = s.SessionLoot,
                Worms = s.SessionWorms,
                Pl = s.RealizedPl,
                Price = price,
                Auto = list.Count,
                Early = early,
            };

            s.History.Add(new HistoryEntry
            {
                Day = s.Day,
                Session = s.Session,
                Pattern = s.Path != null ? s.Path.Name : "—",
                Price = price,
                Pl = s.RealizedPl,
                Loot = Num.F1(s.SessionLoot),
            });

            s.Cd = 0f;
            s.CdTotal = 0f;
            s.Phase = "settlement";
            s.Gauge = null;
        }

        /// <summary>对应 endDay()：把手上还没收的网按当前价结掉，直接进傍晚。</summary>
        public static void EndDay(GameState s)
        {
            if (s.Phase == "preopen")
            {
                s.EndDayPending = false;
                ToEvening(s);
                return;
            }
            if (s.Phase != "running" && s.Phase != "paused") return;
            s.EndDayPending = true;
            CloseMarket(s, true);
            Say("收工");
        }

        /// <summary>对应 skipSession()：只前进一场，不跨天。</summary>
        public static void SkipSession(GameState s)
        {
            if (s.Phase == "running" || s.Phase == "paused") { CloseMarket(s, true); return; }
            if (s.Phase != "preopen") return;
            if (s.Session >= Cfg.SessionsPerDay) { ToEvening(s); return; }

            s.Session++;
            ResetSessionFields(s);
            s.Ui.CastCount = Num.Clamp(1, 0, s.CompleteNets);
            Say("跳到第 " + s.Session + " 场");
        }

        /// <summary>对应 nextSession()。</summary>
        public static void NextSession(GameState s)
        {
            s.Session = 2;
            s.EndDayPending = false;
            ResetSessionFields(s);
            s.Phase = "preopen";
            s.Ui.CastCount = Num.Clamp(1, 0, s.CompleteNets);
        }

        /// <summary>对应 toEvening()。</summary>
        public static void ToEvening(GameState s)
        {
            s.EndDayPending = false;
            s.Phase = "evening";
        }

        /// <summary>对应 nextDay()。</summary>
        public static void NextDay(GameState s)
        {
            s.Day++;
            s.Session = 1;
            ResetSessionFields(s);
            s.Phase = "preopen";
            s.Ui.CastCount = Num.Clamp(1, 0, s.CompleteNets);
            Say("第 " + s.Day + " 天");
        }

        private static void ResetSessionFields(GameState s)
        {
            s.Path = null;
            s.Price = 100f;
            s.Elapsed = 0f;
            s.Revealed = 1;
            s.Nets.Clear();
            s.View.Init = false;
            s.Cd = 0f;
            s.CdTotal = 0f;
        }

        /// <summary>对应 handleAct 的 'step' 分支：开盘前 / 持仓窗口的仓位步进。</summary>
        public static void StepCast(GameState s, int delta)
        {
            int min = s.Phase == "paused" ? 1 : 0;
            int max = Mathf.Max(min, s.CompleteNets);
            int cur = s.Ui.CastCount <= 0 && delta > 0 ? 0 : s.Ui.CastCount;
            s.Ui.CastCount = Num.Clamp(cur + delta, min, max);
        }

        // =====================================================================
        //  仪表盘
        // =====================================================================

        /// <summary>对应 openGauge()。</summary>
        public static void OpenGauge(GameState s)
        {
            s.GaugeLayout = GaugeKit.MakeLayout();   // 每次弹出重新洗牌
            s.Gauge = new GaugeState { U = 0f, Pos = 0f, Verdict = null, Hold = 0f };
            Say("仪表盘 · 按 Q 停下指针");
        }

        /// <summary>对应 judgeGauge(pos, timedOut)，返回本次的冲击（可能为 null）。</summary>
        public static Shock JudgeGauge(GameState s, float pos, bool timedOut)
        {
            var g = s.Gauge;
            if (g == null || g.Verdict != null) return null;

            string kind = timedOut ? "bad" : GaugeKit.KindAt(s, pos);
            g.Verdict = kind;
            g.Pos = pos;
            g.Hold = 1.6f;
            s.GaugeStat.Bump(kind);

            var shock = GaugeKit.AddShock(s, s.Elapsed, kind);
            if (shock != null && s.Path != null) s.Price = s.Path.PriceAt(s, s.Elapsed);

            Say(GaugeKit.Label[kind] + "：" + GaugeKit.ShockMessage(shock)
                + (timedOut ? "（没按 → 判为差）" : ""));
            return shock;
        }

        /// <summary>对应 pressGauge()。</summary>
        public static bool PressGauge(GameState s)
        {
            var g = s.Gauge;
            if (g == null || g.Verdict != null) return false;
            if (s.Phase != "running")
            {
                Say("暂停中指针已冻结，继续后再按 Q");
                return false;
            }
            JudgeGauge(s, g.Pos, false);
            return true;
        }

        /// <summary>
        /// 对应 gaugeTick(dt)。返回 true 表示仪表盘这一帧刚刚关闭
        /// （控制器据此把 gaugeIdx 往前推）。
        /// </summary>
        public static bool GaugeTick(GameState s, float dt)
        {
            var g = s.Gauge;
            if (g == null) return false;

            // 网页版只在 running 分支里推进 gaugeTick：暂停时不但指针冻结，
            // 已经判定后的 1.6 秒停留时间也一起冻结。
            if (s.Phase != "running") return false;

            if (g.Verdict != null)
            {
                g.Hold -= dt;
                if (g.Hold <= 0f)
                {
                    s.Gauge = null;
                    return true;
                }
                return false;
            }

            g.U += dt;
            g.Pos = GaugeKit.Needle(g.U);
            if (g.U >= Cfg.GaugeWindow) JudgeGauge(s, g.Pos, true);   // 不按 → 自动判为差
            return false;
        }

        /// <summary>对应主循环里那一帧的推进。返回 true 表示本帧触发了收市。</summary>
        public static bool TickSession(GameState s, float dt)
        {
            if (s.Phase != "running") return false;

            // 仪表盘未判定时冻结市场：曲线、倒计时、收网 CD 全停，只有指针在走
            bool frozen = Cfg.GaugeFreeze && s.Gauge != null && s.Gauge.Verdict == null;
            if (!frozen)
            {
                s.Elapsed += dt;
                if (s.Elapsed >= Cfg.Duration)
                {
                    s.Elapsed = Cfg.Duration;
                    s.Price = s.Path != null ? s.Path.PriceAt(s, s.Elapsed) : 100f;
                    s.Revealed = s.Path != null ? s.Path.N : 1;
                    CloseMarket(s, false);
                    return true;
                }

                s.Price = s.Path != null ? s.Path.PriceAt(s, s.Elapsed) : 100f;
                s.Revealed = s.Path != null
                    ? Num.Clamp(Mathf.FloorToInt(s.Elapsed / Cfg.Tick) + 1, 1, s.Path.N)
                    : 1;
                if (s.Cd > 0f) s.Cd = Mathf.Max(0f, s.Cd - dt);

                if (s.Gauge == null && s.GaugeIdx < s.GaugePlan.Count && s.Elapsed >= s.GaugePlan[s.GaugeIdx])
                {
                    OpenGauge(s);
                }
            }
            return false;
        }

        // =====================================================================
        //  调试面板
        // =====================================================================

        public static void DamageOneNet(GameState s)
        {
            if (s.CompleteNets <= 0) { Say("没有完整网可以弄破"); return; }
            s.CompleteNets -= 1;
            s.BrokenNets.Add(new BrokenNet(s.NextBrokenId++, 75f, 0));
            int fixedNow = AutoRepair(s);
            Say("破损网 +1" + (fixedNow > 0 ? " · 已自动补满" : " · 缺线虫"));
        }
    }
}
