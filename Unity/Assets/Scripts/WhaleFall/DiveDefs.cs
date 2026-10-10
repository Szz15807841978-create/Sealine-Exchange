using System.Collections.Generic;

namespace WhaleFall
{
    /// <summary>食物定义（对应注册表 FOOD，7 种已实装）。</summary>
    public struct FoodDef
    {
        public string Key;
        public string Name;
        public float Heal;
        public SizeClass Size;
        public int Value;
    }

    /// <summary>可采集物种定义（对应注册表 SPC，9 种已实装）。</summary>
    public struct SpeciesDef
    {
        public string Key;
        public string Name;
        public float Weight;
        public SizeClass Size;
        public string FoodKey;
    }

    /// <summary>敌对生物 / 首领定义（对应注册表 ENE / BOSS）。</summary>
    public struct EnemyDef
    {
        public string Key;
        public string Name;
        public float Hp;
        public float DmgMin;
        public float DmgMax;
        public float SpeedPx;        // 巡逻 / 移动速度（像素/秒）
        public float ChaseSpeedPx;
        public float DetectPx;       // 感知半径
        public float Windup;         // 攻击前摇（秒）
        public float AtkCd;          // 攻击冷却（秒）
        public float ReachPx;        // 攻击距离
        public float RPx;            // 视觉碰撞半径
        public bool IsBoss;
        public bool DmgHp;           // true=扣生命，false=扣体力
        public bool Flees;
        public float FleeAt;
        public int LayerMin;
        public int LayerMax;
    }

    /// <summary>静态实体表：只做「身份 + 数值」的权威，规则在 DiveSim。</summary>
    public static class DiveDefs
    {
        public static readonly FoodDef[] Foods =
        {
            new FoodDef { Key = "FOOD_FISH_S",  Name = "小型鱼肉", Heal = 10f, Size = SizeClass.Small,  Value = 12 },
            new FoodDef { Key = "FOOD_FISH_M",  Name = "中型鱼肉", Heal = 25f, Size = SizeClass.Medium, Value = 45 },
            new FoodDef { Key = "FOOD_FISH_L",  Name = "大型鱼肉", Heal = 50f, Size = SizeClass.Large,  Value = 120 },
            new FoodDef { Key = "FOOD_SHELL_S", Name = "小型贝肉", Heal = 10f, Size = SizeClass.Small,  Value = 9 },
            new FoodDef { Key = "FOOD_SHELL_M", Name = "中型贝肉", Heal = 25f, Size = SizeClass.Medium, Value = 40 },
            new FoodDef { Key = "FOOD_SHRIMP_S",Name = "小型虾肉", Heal = 10f, Size = SizeClass.Small,  Value = 10 },
            new FoodDef { Key = "FOOD_JELLY_M", Name = "水母胶",   Heal = 25f, Size = SizeClass.Medium, Value = 38 },
        };

        public static readonly SpeciesDef[] Species =
        {
            new SpeciesDef { Key = "brach",  Name = "腕足类", Weight = 7.0f,  Size = SizeClass.Small,  FoodKey = "FOOD_SHELL_S" },
            new SpeciesDef { Key = "ortho",  Name = "角石",   Weight = 21.0f, Size = SizeClass.Medium, FoodKey = "FOOD_SHELL_M" },
            new SpeciesDef { Key = "mysid",  Name = "糠虾",   Weight = 6.0f,  Size = SizeClass.Small,  FoodKey = "FOOD_SHRIMP_S" },
            new SpeciesDef { Key = "ostra",  Name = "介形虫", Weight = 6.0f,  Size = SizeClass.Small,  FoodKey = "FOOD_SHRIMP_S" },
            new SpeciesDef { Key = "placo",  Name = "盾皮鱼", Weight = 7.5f,  Size = SizeClass.Small,  FoodKey = "FOOD_FISH_S" },
            new SpeciesDef { Key = "amiif",  Name = "弓鳍鱼", Weight = 17.0f, Size = SizeClass.Medium, FoodKey = "FOOD_FISH_M" },
            new SpeciesDef { Key = "coela",  Name = "空棘鱼", Weight = 19.0f, Size = SizeClass.Medium, FoodKey = "FOOD_FISH_M" },
            new SpeciesDef { Key = "jelly",  Name = "古水母", Weight = 14.0f, Size = SizeClass.Medium, FoodKey = "FOOD_JELLY_M" },
            new SpeciesDef { Key = "leeds",  Name = "利兹鱼", Weight = 45.0f, Size = SizeClass.Large,  FoodKey = "FOOD_FISH_L" },
        };

        public static readonly EnemyDef[] Enemies =
        {
            new EnemyDef { Key = "dunk", Name = "邓氏鱼", Hp = 150f, DmgMin = 22f, DmgMax = 30f,
                SpeedPx = 158f, ChaseSpeedPx = 334f, DetectPx = 560f, Windup = 0.48f, AtkCd = 1.2f,
                ReachPx = 86f, RPx = 32f, IsBoss = false, DmgHp = false, Flees = false, FleeAt = 0.35f,
                LayerMin = 2, LayerMax = 3 },
            new EnemyDef { Key = "ammo", Name = "菊石", Hp = 75f, DmgMin = 12f, DmgMax = 20f,
                SpeedPx = 130f, ChaseSpeedPx = 296f, DetectPx = 360f, Windup = 0.42f, AtkCd = 1.6f,
                ReachPx = 74f, RPx = 24f, IsBoss = false, DmgHp = false, Flees = false, FleeAt = 0.35f,
                LayerMin = 1, LayerMax = 3 },
        };

        public static readonly EnemyDef[] Bosses =
        {
            new EnemyDef { Key = "guardian", Name = "奇虾", Hp = 190f, DmgMin = 25f, DmgMax = 30f,
                SpeedPx = 150f, ChaseSpeedPx = 330f, DetectPx = 600f, Windup = 0.5f, AtkCd = 1.1f,
                ReachPx = 90f, RPx = 34f, IsBoss = true, DmgHp = true, Flees = false, FleeAt = 0.35f,
                LayerMin = 2, LayerMax = 3 },
        };

        private static readonly Dictionary<string, FoodDef> _foodByKey = BuildFood();
        private static readonly Dictionary<string, SpeciesDef> _speciesByKey = BuildSpecies();
        private static readonly Dictionary<string, EnemyDef> _enemyByKey = BuildEnemy();

        private static Dictionary<string, FoodDef> BuildFood()
        {
            var m = new Dictionary<string, FoodDef>();
            for (int i = 0; i < Foods.Length; i++) m[Foods[i].Key] = Foods[i];
            return m;
        }

        private static Dictionary<string, SpeciesDef> BuildSpecies()
        {
            var m = new Dictionary<string, SpeciesDef>();
            for (int i = 0; i < Species.Length; i++) m[Species[i].Key] = Species[i];
            return m;
        }

        private static Dictionary<string, EnemyDef> BuildEnemy()
        {
            var m = new Dictionary<string, EnemyDef>();
            for (int i = 0; i < Enemies.Length; i++) m[Enemies[i].Key] = Enemies[i];
            for (int i = 0; i < Bosses.Length; i++) m[Bosses[i].Key] = Bosses[i];
            return m;
        }

        public static FoodDef FoodOf(string key) { return _foodByKey.ContainsKey(key) ? _foodByKey[key] : default(FoodDef); }
        public static SpeciesDef SpeciesOf(string key) { return _speciesByKey.ContainsKey(key) ? _speciesByKey[key] : default(SpeciesDef); }
        public static EnemyDef EnemyOf(string key) { return _enemyByKey.ContainsKey(key) ? _enemyByKey[key] : default(EnemyDef); }

        /// <summary>物种内部键 → 食物内部键（没有就返回空）。</summary>
        public static string FoodForSpecies(string speciesKey)
        {
            SpeciesDef s = SpeciesOf(speciesKey);
            return s.Key != null ? s.FoodKey : null;
        }
    }
}
