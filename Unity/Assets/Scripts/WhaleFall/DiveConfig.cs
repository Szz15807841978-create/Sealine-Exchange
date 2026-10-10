using UnityEngine;

namespace WhaleFall
{
    /// <summary>
    /// 搜打撤的全部可调数值（对应移植规格 §4.2 的 CFG 表）。
    /// 换算基准：32 px = 1 m。玩法层只读这里，不写。
    /// </summary>
    public static class DiveConfig
    {
        // =====================================================================
        //  坐标与网格
        // =====================================================================

        public const float PxPerM = 32f;
        public static float M(float px) { return px / PxPerM; }
        public static float Px(float m) { return m * PxPerM; }

        public const int COLS = 80;
        public const int ROWS = 120;
        public const float Cell = 1.25f;      // 40 px
        public const float WorldW = 100f;
        public const float WorldH = 150f;

        // =====================================================================
        //  体力
        // =====================================================================

        public const float StaminaMax = 100f;
        public const float BaseDrain = 0.45f;
        public const float SprintExtra = 1.5f;
        public const float LowThreshold = 20f;
        public const float ExhaustCountdown = 5f;
        public const float TrenchExtra = 1.1f;
        public const float DiveThresholdM = 0.5f;

        /// <summary>贴到水面后要停留这么久才判定撤离，避免刚出水就被结算。</summary>
        public const float SurfaceLock = 0.6f;

        // =====================================================================
        //  负重
        // =====================================================================

        public const int BagSlots = 6;
        public const int StackSmall = 3;
        public const float LoadMulEmpty = 1.0f;
        public const float LoadMulFull = 1.25f;
        public const float LoadSpeedPenalty = 0.15f;

        // =====================================================================
        //  进食
        // =====================================================================

        public const float EatTime = 5f;
        public const float EatTimeEmergency = 2.2f;
        public const float EatNoisePx = 620f;

        // =====================================================================
        //  移动
        // =====================================================================

        public const float PlayerSpeed = 250f / PxPerM;
        public const float SprintSpeed = 470f / PxPerM;
        public const float Accel = 1500f / PxPerM;
        public const float Drag = 7.5f;

        /// <summary>角色放大到 5 m 后，15px（0.47 m）的碰撞圆太小，视觉上会陷进岩体；抬到 30px。</summary>
        public const float PlayerR = 30f / PxPerM;

        // =====================================================================
        //  闪避
        // =====================================================================

        public const float DashDistance = 160f / PxPerM;
        public const float DashSpeed0 = 1450f / PxPerM;
        public const float DashDecay = 8.2f;
        public const float DashTime = 0.26f;
        public const float DashCooldown = 0.75f;

        // =====================================================================
        //  生命与鱼叉
        // =====================================================================

        public const float HpMax = 100f;
        public const float SpearDmg = 20f;
        public const float SpearCd = 1.5f;
        public const float SpearRangePx = 250f;
        public const float SpearSpeedPx = 780f;
        public const float SpearWindup = 0.22f;
        public const float SpearStun = 1.2f;

        // =====================================================================
        //  挖矿与徒手
        // =====================================================================

        public const float PickMineTime = 2.0f;
        public const float PickCd = 3.0f;
        public const int PickYieldMin = 1;
        public const int PickYieldMax = 3;
        public const float PickRangePx = 96f;

        public const float HandGatherTime = 0.35f;
        public const float HandRangePx = 64f;
        public const float HandReleaseMarginPx = 32f;
        public const float HandEscape = 0.40f;
        public const float HandEscapeSprint = 0.12f;
        public const float HandPanicTime = 1.5f;

        // =====================================================================
        //  噪音与惊动
        // =====================================================================

        public const float MineNoisePx = 900f;
        public const float GuardianWakePx = 760f;
        public const float MineBonusChance = 0.16f;
    }

    public enum DiveResult
    {
        None,
        Extract,
        Exhausted,
        Killed,
        Aborted,
    }

    public enum ControlMode
    {
        Weapon,
        Tool,
    }

    public enum ToolKind
    {
        Spear,
        Pick,
        Hand,
    }

    public enum EnemyState
    {
        Patrol,
        Lured,
        Chase,
        Attack,
        Flee,
        Return,
    }

    public enum TerrainKind
    {
        Reef,    // 礁石
        Trench,  // 深沟
        Coral,   // 珊瑚
        High,    // 海底高地
        Mine,    // 生物矿
    }

    public enum LayerKind
    {
        Shallow,  // 0–20 m
        Mid,      // 20–50 m
        Deep,     // 50–80 m
        Abyss,    // 80 m+
    }

    public enum SizeClass
    {
        Small,
        Medium,
        Large,
    }
}
