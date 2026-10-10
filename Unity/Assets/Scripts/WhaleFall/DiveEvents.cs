using UnityEngine;

namespace WhaleFall
{
    /// <summary>玩家掉的是体力还是生命（§3.2 OnPlayerDamaged 的 stat 字段）。</summary>
    public enum DamageStat { Stamina, Hp }

    /// <summary>噪音来源（§3.2 OnNoise 的 kind 字段）。</summary>
    public enum NoiseKind { Eat, Mine, Other }

    /// <summary>
    /// 《鲸落》移植规格 §3.2 的事件总线。
    ///
    /// Demo 里所有「事件」都是直接函数调用（toast / pop / Audio2.xxx / hud.renderBag），
    /// 移植到 Unity 后系统一多就会互相渗透。这里把它们显式化成事件：
    ///   · 规则层（DiveSim）只负责 Raise，不认识任何 UI/音频
    ///   · HUD / 音频 / 存档各自订阅，互不引用
    /// 载荷用 struct 传值，避免每帧事件产生 GC。
    ///
    /// 事件名与 §3.2 表格一一对应。
    /// </summary>
    public static class DiveEvents
    {
        // =====================================================================
        //  载荷
        // =====================================================================

        public struct DiveStartEvt { public int seed; }
        public struct DiveEndEvt { public DiveResult result; public string reason; public Stats stats; }
        public struct StaminaEvt { public float cur; public float max; public float drainPerSec; }
        public struct ExhaustEvt { public float countdown; }
        public struct EatEvt { public string foodKey; public float heal; public bool emergency; }
        public struct ItemEvt { public string itemKey; public int count; public int freeSlots; }
        public struct ItemRejectEvt { public string itemKey; public string reason; }
        public struct MineEvt { public Mine mine; public string yieldKey; public int count; }
        public struct GatherEvt { public string targetKey; public bool success; }
        public struct DashEvt { public Vector2 dir; public float cost; public float staminaLeft; }
        public struct DamageEvt { public float amount; public string source; public DamageStat stat; }
        public struct DeathEvt { public string reason; }
        public struct BossEvt { public string bossKey; public Mine mine; }
        public struct EnemyStateEvt { public EnemyInst enemy; public EnemyState oldState; public EnemyState newState; }
        public struct NoiseEvt { public Vector2 pos; public float radius; public NoiseKind kind; }
        public struct LayerEvt { public LayerKind oldLayer; public LayerKind newLayer; }
        public struct QuestEvt { public string title; public string desc; public float progress; }

        // =====================================================================
        //  订阅口
        // =====================================================================

        public static event System.Action<DiveStartEvt> DiveStart;
        public static event System.Action<DiveEndEvt> DiveEnd;
        public static event System.Action<StaminaEvt> StaminaChanged;
        public static event System.Action<ExhaustEvt> ExhaustStart;
        public static event System.Action ExhaustEnd;
        public static event System.Action<EatEvt> EatStart;
        public static event System.Action<EatEvt> EatComplete;
        public static event System.Action<ItemEvt> ItemPicked;
        public static event System.Action<ItemRejectEvt> ItemRejected;
        public static event System.Action<MineEvt> MineHit;
        public static event System.Action<MineEvt> MineDepleted;
        public static event System.Action<GatherEvt> GatherAttempt;
        public static event System.Action<DashEvt> DashUsed;
        public static event System.Action<DamageEvt> PlayerDamaged;
        public static event System.Action<DeathEvt> PlayerDied;
        public static event System.Action<BossEvt> BossEngaged;
        public static event System.Action<BossEvt> BossDefeated;
        public static event System.Action<EnemyStateEvt> EnemyStateChanged;
        public static event System.Action<NoiseEvt> Noise;
        public static event System.Action<LayerEvt> DepthLayerChanged;
        public static event System.Action ExtractPrompt;
        public static event System.Action<QuestEvt> QuestUpdated;

        // =====================================================================
        //  触发口（规则层只调这些）
        // =====================================================================

        public static void RaiseDiveStart(int seed) { var h = DiveStart; if (h != null) h(new DiveStartEvt { seed = seed }); }
        public static void RaiseDiveEnd(DiveResult r, string reason, Stats st) { var h = DiveEnd; if (h != null) h(new DiveEndEvt { result = r, reason = reason, stats = st }); }
        public static void RaiseStamina(float cur, float max, float drain) { var h = StaminaChanged; if (h != null) h(new StaminaEvt { cur = cur, max = max, drainPerSec = drain }); }
        public static void RaiseExhaustStart(float countdown) { var h = ExhaustStart; if (h != null) h(new ExhaustEvt { countdown = countdown }); }
        public static void RaiseExhaustEnd() { var h = ExhaustEnd; if (h != null) h(); }
        public static void RaiseEatStart(string key, float heal, bool emergency) { var h = EatStart; if (h != null) h(new EatEvt { foodKey = key, heal = heal, emergency = emergency }); }
        public static void RaiseEatComplete(string key, float heal, bool emergency) { var h = EatComplete; if (h != null) h(new EatEvt { foodKey = key, heal = heal, emergency = emergency }); }
        public static void RaiseItemPicked(string key, int count, int freeSlots) { var h = ItemPicked; if (h != null) h(new ItemEvt { itemKey = key, count = count, freeSlots = freeSlots }); }
        public static void RaiseItemRejected(string key, string reason) { var h = ItemRejected; if (h != null) h(new ItemRejectEvt { itemKey = key, reason = reason }); }
        public static void RaiseMineHit(Mine m, string yieldKey, int count) { var h = MineHit; if (h != null) h(new MineEvt { mine = m, yieldKey = yieldKey, count = count }); }
        public static void RaiseMineDepleted(Mine m) { var h = MineDepleted; if (h != null) h(new MineEvt { mine = m }); }
        public static void RaiseGather(string targetKey, bool success) { var h = GatherAttempt; if (h != null) h(new GatherEvt { targetKey = targetKey, success = success }); }
        public static void RaiseDash(Vector2 dir, float cost, float left) { var h = DashUsed; if (h != null) h(new DashEvt { dir = dir, cost = cost, staminaLeft = left }); }
        public static void RaiseDamage(float amount, string source, DamageStat stat) { var h = PlayerDamaged; if (h != null) h(new DamageEvt { amount = amount, source = source, stat = stat }); }
        public static void RaisePlayerDied(string reason) { var h = PlayerDied; if (h != null) h(new DeathEvt { reason = reason }); }
        public static void RaiseBossEngaged(string bossKey, Mine m) { var h = BossEngaged; if (h != null) h(new BossEvt { bossKey = bossKey, mine = m }); }
        public static void RaiseBossDefeated(string bossKey, Mine m) { var h = BossDefeated; if (h != null) h(new BossEvt { bossKey = bossKey, mine = m }); }
        public static void RaiseEnemyState(EnemyInst e, EnemyState oldS, EnemyState newS) { var h = EnemyStateChanged; if (h != null) h(new EnemyStateEvt { enemy = e, oldState = oldS, newState = newS }); }
        public static void RaiseNoise(Vector2 pos, float radius, NoiseKind kind) { var h = Noise; if (h != null) h(new NoiseEvt { pos = pos, radius = radius, kind = kind }); }
        public static void RaiseLayerChanged(LayerKind oldL, LayerKind newL) { var h = DepthLayerChanged; if (h != null) h(new LayerEvt { oldLayer = oldL, newLayer = newL }); }
        public static void RaiseExtractPrompt() { var h = ExtractPrompt; if (h != null) h(); }
        public static void RaiseQuest(string title, string desc, float progress) { var h = QuestUpdated; if (h != null) h(new QuestEvt { title = title, desc = desc, progress = progress }); }

        /// <summary>
        /// 清空所有订阅。单次下潜重建时调用，避免静态事件残留旧引用
        /// （Unity 里静态 event 是跨场景存活的，不清理会持有已销毁对象）。
        /// </summary>
        public static void ClearAll()
        {
            DiveStart = null; DiveEnd = null; StaminaChanged = null;
            ExhaustStart = null; ExhaustEnd = null;
            EatStart = null; EatComplete = null;
            ItemPicked = null; ItemRejected = null;
            MineHit = null; MineDepleted = null;
            GatherAttempt = null; DashUsed = null;
            PlayerDamaged = null; PlayerDied = null;
            BossEngaged = null; BossDefeated = null;
            EnemyStateChanged = null; Noise = null;
            DepthLayerChanged = null; ExtractPrompt = null; QuestUpdated = null;
        }
    }

    /// <summary>
    /// §3.3 的音效触发点：把事件映射到 Demo 里原有的音效方法名。
    /// 现在只把名字打出来（还没接音频资源）；接了 AudioSource 之后把 Debug 换成播放即可。
    /// </summary>
    public sealed class DiveAudioBinder
    {
        public bool LogOnly = true;

        public void Attach()
        {
            DiveEvents.DashUsed += e => Play("dash");
            DiveEvents.ItemRejected += e => Play("denied");
            DiveEvents.PlayerDied += e => Play("die");
            DiveEvents.EatComplete += e => Play("eat");
            DiveEvents.DiveEnd += e => { if (e.result == DiveResult.Extract) Play("extract"); };
            DiveEvents.GatherAttempt += e => { if (e.success) Play("hitFish"); };
            DiveEvents.PlayerDamaged += e => Play("hurt");
            DiveEvents.MineHit += e => Play("mine");
            DiveEvents.ItemPicked += e => Play(e.count > 1 ? "pickupS" : "pickupM");
            DiveEvents.DashUsed += e => Play("spear");
            DiveEvents.EnemyStateChanged += e => { if (e.newState == EnemyState.Attack) Play("warn"); };
        }

        public void Detach() { DiveEvents.ClearAll(); }

        private void Play(string clip)
        {
            if (LogOnly) Debug.Log("[音效] " + clip);
            // TODO: 接入 AudioSource 后改成实际播放
        }
    }
}
