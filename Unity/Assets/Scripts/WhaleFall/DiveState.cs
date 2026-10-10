using System.Collections.Generic;
using UnityEngine;

namespace WhaleFall
{
    /// <summary>确定性随机源（世界生成与玩法随机共用，同种子同结果）。</summary>
    public class WfRandom
    {
        private System.Random _r;
        public WfRandom(int seed) { _r = new System.Random(seed); }
        public float Value() { return (float)_r.NextDouble(); }
        public float Range(float min, float max) { return min + (float)_r.NextDouble() * (max - min); }
        public int Range(int min, int max) { return _r.Next(min, max); }
        public int Next(int max) { return _r.Next(max); }
        public int Next(int min, int max) { return _r.Next(min, max); }
    }

    /// <summary>背包条目。</summary>
    public struct BagItem
    {
        public string Key;      // 内部键（FOOD_xxx / MAT_xxx / TOOL_xxx）
        public string Name;
        public string Kind;     // food / mat / tool / weapon
        public SizeClass Size;
        public int Qty;
        public float Heal;
    }

    /// <summary>6 格背包：小型可堆 3，中/大/工具各占一格。</summary>
    public class Backpack
    {
        public BagItem[] Slots = new BagItem[DiveConfig.BagSlots];

        public int UsedSlots()
        {
            int n = 0;
            for (int i = 0; i < Slots.Length; i++) if (!string.IsNullOrEmpty(Slots[i].Key)) n++;
            return n;
        }

        public int StackMax(SizeClass size)
        {
            return size == SizeClass.Small ? DiveConfig.StackSmall : 1;
        }

        /// <summary>入包成功返回 true；满包返回 false（不产生任何副作用）。</summary>
        public bool Add(string key, string name, string kind, SizeClass size, float heal, int qty = 1)
        {
            int stackMax = StackMax(size);
            for (int i = 0; i < Slots.Length; i++)
            {
                if (Slots[i].Key == key && Slots[i].Qty < stackMax)
                {
                    int room = stackMax - Slots[i].Qty;
                    int put = Mathf.Min(room, qty);
                    Slots[i].Qty += put;
                    if (put == qty) return true;
                    qty -= put;
                }
            }
            for (int i = 0; i < Slots.Length; i++)
            {
                if (string.IsNullOrEmpty(Slots[i].Key))
                {
                    Slots[i] = new BagItem { Key = key, Name = name, Kind = kind, Size = size, Qty = qty, Heal = heal };
                    return true;
                }
            }
            return false;
        }

        /// <summary>消耗 1 份指定键；不足返回 false。</summary>
        public bool ConsumeOne(string key)
        {
            for (int i = 0; i < Slots.Length; i++)
            {
                if (Slots[i].Key == key && Slots[i].Qty > 0)
                {
                    Slots[i].Qty--;
                    if (Slots[i].Qty <= 0) Slots[i] = default(BagItem);
                    return true;
                }
            }
            return false;
        }

        public int CountOf(string key)
        {
            int n = 0;
            for (int i = 0; i < Slots.Length; i++) if (Slots[i].Key == key) n += Slots[i].Qty;
            return n;
        }
    }

    /// <summary>玩家状态。</summary>
    public struct PlayerState
    {
        public Vector2 pos;      // 米，y 向上，海面 y=0，越深越负
        public Vector2 vel;
        public float aim;        // 瞄准弧度
        public float face;       // ±1
        public float stamina;
        public float staminaMax;
        public bool dashing;
        public float dashT;
        public float dashVx, dashVy;
        public float sprintCd;
        public float hurtFlash;
        public float surfaceLock;
        public float hp;
        public float hpMax;
        public bool hpActive;    // 首领区才启用生命值
        public float hpFlash;
    }

    /// <summary>可采集资源（物种鱼）。</summary>
    public struct Resource
    {
        public string speciesKey;
        public Vector2 pos;
        public int state;        // 0 wander / 1 flee / 2 still
        public float panic;
        public float dir;
        public bool alive;
    }

    /// <summary>生物矿点。</summary>
    public struct Mine
    {
        public int col0, col1, row0, row1;
        public int nodes;
        public bool depleted;
        public Vector2 center;
        public string bossKey;   // 守矿者（空 = 无首领）
    }

    /// <summary>敌对生物运行时实例。</summary>
    public struct EnemyInst
    {
        public string key;
        public Vector2 pos;
        public EnemyState state;
        public float hp, hpMax;
        public float windup, windupMax;
        public float atkCd;
        public float stun;
        public float searchT;
        public bool lured;
        public float luredT;
        public Vector2 lureTarget;
        public Vector2 house;
        public float homeR;
        public float leash;
        public float patrolA, patrolB;
        public float dir;
        public bool aggro;
        public bool dead;
    }

    /// <summary>鱼叉弹体。</summary>
    public struct Projectile
    {
        public Vector2 pos;
        public Vector2 dir;
        public float rangeLeft;  // 剩余射程（米）
        public bool dead;
    }

    /// <summary>掉落在地上的食物。</summary>
    public struct FreeFood
    {
        public string foodKey;
        public string name;
        public SizeClass size;
        public float heal;
        public Vector2 pos;
        public bool dead;
    }

    /// <summary>世界（由种子生成，确定性）。</summary>
    public class World
    {
        public TerrainKind[] Kind;
        public readonly List<Collider> TerrainColliders = new List<Collider>();
        public List<Resource> Resources = new List<Resource>();
        public List<Mine> Mines = new List<Mine>();
        public List<EnemyInst> Enemies = new List<EnemyInst>();
        public Vector2 RaftPos;
        /// <summary>渲染用的游泳平面 Z（玩家要画在地形前面）。</summary>
        public const float SwimZ = -2f;
        /// <summary>
        /// 碰撞查询平面 Z，由地图包围盒的 Z 中心给出，**与 SwimZ 解耦**。
        /// 地形是贴在 z≈0 的薄片（实测 Z 仅 −0.34~0.30），拿渲染用的 SwimZ(−2) 去查，
        /// 查询球和地形完全不重叠，一帧都碰不上。
        /// </summary>
        public float TerrainQueryZ = 0f;
        public float MapMinX = 0f;
        public float MapMaxX = DiveConfig.WorldW;
        public float MapBottomY = -DiveConfig.WorldH;

        /// <summary>不可通行的判定余量：取玩家体量级，避免资源点/矿点贴着岩面。</summary>
        public const float IsSolidProbe = 0.5f;

        private readonly HashSet<Collider> _terrainSet = new HashSet<Collider>();
        private static readonly Collider[] QueryHits = new Collider[32];

        public void ClearTerrainColliders()
        {
            TerrainColliders.Clear();
            _terrainSet.Clear();
        }

        public void AddTerrainCollider(Collider c)
        {
            if (!c) return;
            TerrainColliders.Add(c);
            _terrainSet.Add(c);
        }

        // 玩家在 XY 平面游动，障碍是真实 3D 碰撞体，不是网格格。
        // 查询走 PhysX 宽相（静态 MeshCollider 有 AABB 树）：
        //   · Collider.ClosestPoint 对非凸 MeshCollider 是非法的 —— Unity 会刷
        //     "Physics.ClosestPoint can only be used with a BoxCollider, SphereCollider,
        //      CapsuleCollider and a convex MeshCollider."，且返回值不可用；
        //   · 手动遍历 54 个碰撞体，每帧要几百次查询。
        /// <summary>自身节点：其下所有碰撞体都不算地形，避免角色自己的碰撞箱把自己挡住。</summary>
        public Transform SelfRoot;

        public bool HitsTerrain(float x, float y, float radius)
        {
            if (_terrainSet.Count == 0) return false;
            Vector3 p = new Vector3(x, y, TerrainQueryZ);
            int n = Physics.OverlapSphereNonAlloc(
                p, radius + 0.02f, QueryHits, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var c = QueryHits[i];
                if (!c) continue;
                if (SelfRoot != null && c.transform.IsChildOf(SelfRoot)) continue;
                if (_terrainSet.Contains(c)) return true;
            }
            return false;
        }

        /// <summary>
        /// 该点是否不可通行（用于撒资源/矿点/敌人，不在每帧热路径上）。
        /// 探针带 <see cref="IsSolidProbe"/> 的余量，所以它判的是"离岩面太近"，
        /// 而不是严格意义上的"在岩体内部"——深埋在厚岩体内部的点仍可能判为可通行。
        /// 要精确，得在加载时把地形烘成一张二维占用位图。
        /// </summary>
        public bool IsSolid(float x, float y)
        {
            if (x < MapMinX || x > MapMaxX || y > 0 || y < MapBottomY) return true;
            return HitsTerrain(x, y, IsSolidProbe);
        }


        public TerrainKind KindAt(float x, float y)
        {
            int col = Mathf.Clamp(Mathf.FloorToInt(x / DiveConfig.Cell), 0, DiveConfig.COLS - 1);
            return Kind[col];
        }
    }

    /// <summary>本局统计。</summary>
    public struct Stats
    {
        public float MaxDepthM;
        public float Time;
        public int Mined;
        public int Caught;
        public int Kills;
    }

    /// <summary>
    /// 单次下潜的完整状态容器（可整体丢弃重建，对应移植规格 §2.2）。
    /// 坐标：米，y 向上，海面 y=0。
    /// </summary>
    public class DiveState
    {
        public int Seed;
        public World World;
        public PlayerState Player;
        public Backpack Bag = new Backpack();
        public ControlMode Mode = ControlMode.Weapon;
        public ToolKind ActiveTool = ToolKind.Spear;

        public List<Projectile> Projectiles = new List<Projectile>();
        public List<FreeFood> FreeFoods = new List<FreeFood>();

        public float SpearCd;
        public float PickCd;

        public int MiningIndex = -1;
        public float MiningT;
        public int GatheringIndex = -1;
        public float GatheringT;

        public bool EatingActive;
        public string EatingFoodKey;
        public float EatingT;
        public float EatingTotal;
        public bool EatingEmergency;

        public float Exhaust;             // 力竭倒计时已过秒数
        public bool Over;
        public DiveResult Result = DiveResult.None;
        public string DeathReason;
        public bool HasDived;

        public Stats Stats = new Stats();
        public float Time;
        public WfRandom Rng;

        // 输入帧（由控制器写入，DiveSim 消费；便于无头测试直接灌）
        public Vector2 MoveAxis;       // 归一化移动方向
        public Vector2 AimWorld;       // 鼠标世界坐标
        public bool AimValid;
        public bool UseHeld;           // 鼠标左键是否按住
        public bool UsePressed;        // 本帧刚按下
        public bool UseReleased;       // 本帧刚松开

        public bool HpActive { get { return Player.hpActive; } }

        public void Reset(int seed)
        {
            Seed = seed;
            World = DiveWorldGen.Generate(seed);
            Rng = new WfRandom(seed ^ 0x5eed);

            Player = new PlayerState();
            Player.pos = new Vector2(World.RaftPos.x, -0.5f);
            Player.stamina = DiveConfig.StaminaMax;
            Player.staminaMax = DiveConfig.StaminaMax;
            Player.hp = DiveConfig.HpMax;
            Player.hpMax = DiveConfig.HpMax;
            Player.face = 1f;
            Player.aim = 0f;

            Bag = new Backpack();
            Mode = ControlMode.Weapon;
            ActiveTool = ToolKind.Spear;
            Projectiles.Clear();
            FreeFoods.Clear();
            SpearCd = 0f; PickCd = 0f;
            MiningIndex = -1; MiningT = 0f;
            GatheringIndex = -1; GatheringT = 0f;
            EatingActive = false; EatingFoodKey = null; EatingT = 0f; EatingTotal = 0f; EatingEmergency = false;
            Exhaust = 0f;
            Over = false; Result = DiveResult.None; DeathReason = null;
            HasDived = false;
            Stats = new Stats();
            Time = 0f;
        }
    }

    /// <summary>世界生成（同种子同结果）。</summary>
    public static class DiveWorldGen
    {
        public static World Generate(int seed)
        {
            var rng = new WfRandom(seed);
            var w = new World();
            w.Kind = new TerrainKind[DiveConfig.COLS];
            w.RaftPos = new Vector2(DiveConfig.WorldW * 0.5f, 0f);
            for (int c = 0; c < w.Kind.Length; c++) w.Kind[c] = TerrainKind.Reef;
            return w;
        }

        // Called only after the complete map and its MeshColliders have been built.
        public static void Populate(World w, int seed)
        {
            w.Resources.Clear();
            w.Mines.Clear();
            w.Enemies.Clear();
            var rng = new WfRandom(seed ^ 0x5eed);
            SpawnMines(w, rng);
            SpawnResources(w, rng);
            SpawnEnemies(w, rng);
        }


        private static void SpawnResources(World w, WfRandom rng)
        {
            int count = 26;
            for (int i = 0; i < count; i++)
            {
                int attempt = 0;
                while (attempt++ < 40)
                {
                    float x = rng.Range(w.MapMinX + 4f, w.MapMaxX - 4f);
                    float depth = rng.Range(4f, Mathf.Max(5f, -w.MapBottomY - 5f));
                    float y = -depth;
                    if (w.IsSolid(x, y)) continue;
                    string key = PickSpecies(depth, rng);
                    w.Resources.Add(new Resource
                    {
                        speciesKey = key,
                        pos = new Vector2(x, y),
                        state = 0,
                        dir = rng.Value() < 0.5f ? 1f : -1f,
                        alive = true,
                    });
                    break;
                }
            }
        }

        private static string PickSpecies(float depth, WfRandom rng)
        {
            // 按层给不同物种池：浅层多小型，深层偶有大型
            string[] pool;
            if (depth < 20f) pool = new[] { "brach", "mysid", "ostra", "placo", "ortho" };
            else if (depth < 50f) pool = new[] { "placo", "amiif", "coela", "jelly", "ortho", "mysid" };
            else pool = new[] { "amiif", "coela", "jelly", "ortho", "leeds" };
            return pool[rng.Next(pool.Length)];
        }

        private static void SpawnMines(World w, WfRandom rng)
        {
            // Place gathering points on a real platform top; mining does not remove terrain.
            foreach (var collider in w.TerrainColliders)
            {
                if (!collider || collider.bounds.center.y > -40f) continue;
                var bounds = collider.bounds;
                Vector2 position = new Vector2(bounds.center.x, bounds.max.y + 0.8f);
                if (w.HitsTerrain(position.x, position.y, 0.6f)) continue;
                w.Mines.Add(new Mine { nodes = 12 + rng.Next(4), center = position, bossKey = "guardian" });
                if (w.Mines.Count == 2) break;
            }
        }


        private static void SpawnEnemies(World w, WfRandom rng)
        {
            // 每座矿一只守矿者 + 2—3 只普通敌对
            for (int i = 0; i < w.Mines.Count; i++)
            {
                var m = w.Mines[i];
                var d = DiveDefs.EnemyOf("guardian");
                Vector2 guardPos = FindOpenWater(w, m.center + new Vector2(0f, 2f), 14f);
                w.Enemies.Add(MakeEnemy(d, guardPos, true, rng));
            }

            int n = 2 + rng.Next(2);
            for (int i = 0; i < n; i++)
            {
                string key = rng.Value() < 0.55f ? "ammo" : "dunk";
                var d = DiveDefs.EnemyOf(key);
                float x, y;
                int attempt = 0;
                bool found = false;
                do
                {
                    x = rng.Range(w.MapMinX + 6f, w.MapMaxX - 6f);
                    y = -rng.Range(10f, Mathf.Max(11f, -w.MapBottomY - 8f));
                    attempt++;
                    found = !w.IsSolid(x, y);
                } while (!found && attempt < 60);
                if (found) w.Enemies.Add(MakeEnemy(d, new Vector2(x, y), false, rng));
            }
        }

        /// <summary>从起点向上扫描，找到第一格开放水域（守矿者不嵌进矿体）。</summary>
        private static Vector2 FindOpenWater(World w, Vector2 start, float maxUpM)
        {
            float step = DiveConfig.Cell * 0.5f;
            for (float up = 0f; up <= maxUpM; up += step)
            {
                Vector2 p = start + new Vector2(0f, up);
                if (!w.IsSolid(p.x, p.y)) return p;
            }
            return start + new Vector2(0f, maxUpM);
        }

        private static EnemyInst MakeEnemy(EnemyDef d, Vector2 pos, bool isBoss, WfRandom rng)
        {
            float reachM = DiveConfig.M(d.ReachPx);
            float homeR = Mathf.Max(DiveConfig.M(90f), reachM + DiveConfig.M(70f));
            float leash = isBoss ? DiveConfig.M(900f)
                          : Mathf.Max(homeR + DiveConfig.M(120f), DiveConfig.M(d.Key == "dunk" ? 620f : 420f));
            float patrolHalf = 60f;
            return new EnemyInst
            {
                key = d.Key,
                pos = pos,
                state = EnemyState.Patrol,
                hp = d.Hp, hpMax = d.Hp,
                windupMax = d.Windup,
                atkCd = rng.Range(0f, d.AtkCd),
                house = pos,
                homeR = homeR,
                leash = leash,
                patrolA = pos.x - patrolHalf,
                patrolB = pos.x + patrolHalf,
                dir = rng.Value() < 0.5f ? 1f : -1f,
                aggro = false,
                dead = false,
            };
        }
    }
}
