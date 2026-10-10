using System.Collections.Generic;

namespace Fishing
{
    /// <summary>
    /// index.html 顶部 CFG 的 1:1 移植。所有可调参数都在这里。
    /// </summary>
    public static class Cfg
    {
        public const float Duration = 90f;        // 单次捕捞时长（秒）
        public const float Tick = 0.1f;           // 曲线采样间隔（秒）
        public const int SessionsPerDay = 2;      // 一个雨天两场
        public const float Fee = 0f;              // 买入手续费：买入价 = 市价 × (1+FEE)
        public const int StartNets = 1;           // 初始完整网
        public const float CdMin = 3f;            // 收网 CD 下限（秒）
        public const float CdMax = 5f;            // 收网 CD 上限（秒）
        public const float MidLine = 100f;        // 图上基准线
        public const float BonusLine = 200f;      // >= 200：额外线虫
        public const int BonusWorms = 5;          // >= 200 额外线虫
        public const float NetValue = 100f;       // 1 张网的价值，多回收的网按这个价从收益里扣
        public const float WipeRatio = 0.7f;      // 跌到买入价的 70%（亏损 30%）收网 → 网永久丢失
        public const float RepairPerWorm = 5f;    // 1 条线虫恢复 5% 完整度
        public const int WormByproductMin = 3;    // 每张成功结算的网产出的线虫（副产物）下限
        public const int WormByproductMax = 6;    // 上限
        public const float LootScale = 1f;        // 资源发放系数

        /* ---- 曲线波动幅度 ---- */
        public const float VolSigma = 0.05f;      // 随机噪声强度
        public const float VolClamp = 0.30f;      // 噪声上下限 ±30%
        public const float PatternScale = 1.3f;   // 形态幅度缩放：100 + (v-100)×SCALE

        /* ---- 仪表盘小玩法 ---- */
        public const int GaugePerSession = 2;     // 每场出现几次
        public const float GaugeWindow = 5f;      // 秒：到点没按就自动判为"差"
        public const float GaugeSweep = 1.4f;     // 秒：指针单程扫过的时间
        public const bool GaugeFreeze = true;     // 仪表盘未判定时冻结市场
        public const float GaugeBest = 8f;        // "最佳"区段宽度（%），只有一份
        public const bool GaugeShuffle = true;    // 每次弹出重新洗牌位置

        /// <summary>区段宽度（%），每个出现两份。对应 CFG.GAUGE_HALF。</summary>
        public static readonly KeyValuePair<string, float>[] GaugeHalf =
        {
            new KeyValuePair<string, float>("bad", 8f),
            new KeyValuePair<string, float>("normal", 26f),
            new KeyValuePair<string, float>("good", 12f),
        };

        /// <summary>两次触发的随机时间窗（秒）。对应 CFG.GAUGE_PLAN。</summary>
        public static readonly float[][] GaugePlan =
        {
            new[] { 12f, 32f },
            new[] { 52f, 72f },
        };

        /// <summary>各区间对曲线施加的冲击：Mag = 幅度，Dur = 爬升秒数。</summary>
        public static ShockCfg ShockOf(string kind)
        {
            switch (kind)
            {
                case "best":   return new ShockCfg(0.12f, 4.0f);
                case "good":   return new ShockCfg(0.05f, 4.0f);
                case "normal": return new ShockCfg(0f, 0f);
                default:       return new ShockCfg(-0.15f, 0.9f);
            }
        }

        public struct ShockCfg
        {
            public readonly float Mag;
            public readonly float Dur;

            public ShockCfg(float mag, float dur)
            {
                Mag = mag;
                Dur = dur;
            }
        }
    }
}
