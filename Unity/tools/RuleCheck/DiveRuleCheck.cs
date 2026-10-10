using System;
using System.Collections.Generic;
using WhaleFall;

namespace DiveRuleCheck
{
    public static class DiveRuleCheckMain
    {
        private static int _passed;
        private static int _failed;
        private static readonly List<string> _failures = new List<string>();

        public static int Main()
        {
            WorldGen_Deterministic();
            WorldGen_NotInSolid();
            Bag_SmallStacks();
            Stamina_SurfaceNoDrain();
            Stamina_UnderwaterDrain();
            Dash_ConsumesAndMoves();
            Dash_Cooldown();
            Mine_Yields();
            Enemy_ChaseOnProximity();
            Enemy_DamagesStamina();
            Boss_DamagesHp();
            Extract_RequiresDive();
            Surface_HardCeiling();
            Exhaust_Death();
            Abort_Result();

            Console.WriteLine();
            Console.WriteLine("DIVE RULE CHECK: " + _passed + " passed, " + _failed + " failed");
            foreach (var f in _failures) Console.WriteLine("  FAIL " + f);
            return _failed == 0 ? 0 : 1;
        }

        private static void Ok(bool cond, string name)
        {
            if (cond) { _passed++; }
            else { _failed++; _failures.Add(name); }
        }

        private static void Approx(float a, float b, float eps, string name)
        {
            Ok(Math.Abs(a - b) <= eps, name + " (" + a + " vs " + b + ")");
        }

        private static DiveState NewState(int seed)
        {
            var s = new DiveState();
            s.Reset(seed);
            return s;
        }

        private static World EmptyWorld()
        {
            var w = new World();
            w.Solid = new bool[DiveConfig.COLS, DiveConfig.ROWS];
            w.Kind = new TerrainKind[DiveConfig.COLS];
            for (int i = 0; i < DiveConfig.COLS; i++) w.Kind[i] = TerrainKind.Coral;
            w.SeafloorDepth = new float[DiveConfig.COLS];
            for (int i = 0; i < DiveConfig.COLS; i++) w.SeafloorDepth[i] = 110f;
            w.RaftPos = new UnityEngine.Vector2(80f, 0f);
            return w;
        }

        // 1
        private static void WorldGen_Deterministic()
        {
            var a = DiveWorldGen.Generate(123);
            var b = DiveWorldGen.Generate(123);
            Ok(a.Resources.Count == b.Resources.Count, "gen: resource count same");
            Ok(a.Mines.Count == b.Mines.Count, "gen: mine count same");
            Ok(a.Enemies.Count == b.Enemies.Count, "gen: enemy count same");
            bool mineSame = true;
            for (int i = 0; i < a.Mines.Count; i++)
                if (a.Mines[i].center.x != b.Mines[i].center.x || a.Mines[i].center.y != b.Mines[i].center.y) mineSame = false;
            Ok(mineSame, "gen: mine centers same");
        }

        // 2
        private static void WorldGen_NotInSolid()
        {
            bool ok = true;
            for (int seed = 1; seed <= 20; seed++)
            {
                var w = DiveWorldGen.Generate(seed);
                for (int i = 0; i < w.Enemies.Count; i++)
                    if (w.IsSolid(w.Enemies[i].pos.x, w.Enemies[i].pos.y))
                    { ok = false; Console.WriteLine("  seed=" + seed + " enemy " + w.Enemies[i].key + " @ " + w.Enemies[i].pos); }
                for (int i = 0; i < w.Resources.Count; i++)
                    if (w.IsSolid(w.Resources[i].pos.x, w.Resources[i].pos.y))
                    {
                        ok = false;
                        int col = (int)Math.Floor(w.Resources[i].pos.x / DiveConfig.Cell);
                        Console.WriteLine("  seed=" + seed + " res " + w.Resources[i].speciesKey + " @ " + w.Resources[i].pos
                            + " col=" + col + " seafloor=" + w.SeafloorDepth[col] + " kind=" + w.Kind[col]);
                    }
            }
            Ok(ok, "gen: 20 seeds, entities not in solid");
        }

        // 3
        private static void Bag_SmallStacks()
        {
            var b = new Backpack();
            b.Add("FOOD_FISH_S", "小型鱼肉", "food", SizeClass.Small, 10f, 1);
            b.Add("FOOD_FISH_S", "小型鱼肉", "food", SizeClass.Small, 10f, 1);
            b.Add("FOOD_FISH_S", "小型鱼肉", "food", SizeClass.Small, 10f, 1);
            Ok(b.UsedSlots() == 1, "bag: small stacks to 1 slot");
            Ok(b.Slots[0].Qty == 3, "bag: small qty 3");
            b.Add("FOOD_FISH_S", "小型鱼肉", "food", SizeClass.Small, 10f, 1);
            Ok(b.UsedSlots() == 2, "bag: 4th small goes to new slot");
        }

        // 4
        private static void Stamina_SurfaceNoDrain()
        {
            var s = NewState(1);
            s.Player.pos = new UnityEngine.Vector2(80f, -0.3f);
            DiveSim.Tick(s, 1.0f);
            Ok(s.Player.stamina == DiveConfig.StaminaMax, "stamina: surface no drain");
        }

        // 5
        private static void Stamina_UnderwaterDrain()
        {
            var s = NewState(1);
            s.World = EmptyWorld();
            s.Player.pos = new UnityEngine.Vector2(80f, -30f);
            DiveSim.Tick(s, 1.0f);
            Ok(s.Player.stamina < DiveConfig.StaminaMax, "stamina: underwater drains");
        }

        // 6
        private static void Dash_ConsumesAndMoves()
        {
            var s = NewState(1);
            s.World = EmptyWorld();
            s.Player.pos = new UnityEngine.Vector2(80f, -30f);
            s.MoveAxis = UnityEngine.Vector2.right;
            float before = s.Player.stamina;
            DiveSim.Dash(s);
            Ok(s.Player.dashing, "dash: starts");
            Ok(s.Player.stamina < before, "dash: consumes stamina");
            UnityEngine.Vector2 p0 = s.Player.pos;
            DiveSim.Tick(s, 0.05f);
            Ok((s.Player.pos - p0).magnitude > 0.001f, "dash: moves");
        }

        // 7
        private static void Dash_Cooldown()
        {
            var s = NewState(1);
            s.World = EmptyWorld();
            s.Player.pos = new UnityEngine.Vector2(80f, -30f);
            DiveSim.Dash(s);
            float cd = s.Player.sprintCd;
            Ok(cd > 0f, "dash: cooldown set");
            DiveSim.Dash(s);
            Ok(s.Player.dashing && s.Player.sprintCd == cd, "dash: second dash blocked during cd");
        }

        // 8
        private static void Mine_Yields()
        {
            var s = NewState(1);
            s.World = EmptyWorld();
            // 单格矿，中心约 (10.625, -31.25)，玩家站下方 1.75m 处（在 3m 镐范围内、不在实心内）
            s.World.Mines.Add(new Mine { col0 = 8, col1 = 9, row0 = 24, row1 = 25, nodes = 5, center = new UnityEngine.Vector2(10.625f, -31.25f) });
            s.World.Solid[8, 24] = true;
            s.Player.pos = new UnityEngine.Vector2(10.625f, -33f);
            s.Mode = ControlMode.Tool;
            DiveSim.UseDown(s);
            Ok(s.MiningIndex >= 0, "mine: use starts mining");
            for (int i = 0; i < 60; i++) DiveSim.Tick(s, 0.05f);   // 3 秒
            Ok(s.Stats.Mined >= 1, "mine: mined at least once");
            Ok(s.Bag.CountOf("MAT_ORE") >= 1, "mine: ore in bag");
        }

        // 9
        private static void Enemy_ChaseOnProximity()
        {
            var s = NewState(1);
            s.World = EmptyWorld();
            var e = new EnemyInst { key = "ammo", pos = new UnityEngine.Vector2(80f, -30f), state = EnemyState.Patrol,
                hp = 75f, hpMax = 75f, windupMax = 0.42f, house = new UnityEngine.Vector2(80f, -30f),
                homeR = 5f, leash = 30f, patrolA = 75f, patrolB = 85f, dir = 1f };
            s.World.Enemies.Add(e);
            s.Player.pos = new UnityEngine.Vector2(82f, -30f);
            for (int i = 0; i < 20; i++) DiveSim.Tick(s, 0.1f);
            Ok(s.World.Enemies[0].state == EnemyState.Chase || s.World.Enemies[0].state == EnemyState.Attack,
                "enemy: chases on proximity");
        }

        // 10
        private static void Enemy_DamagesStamina()
        {
            var s = NewState(1);
            s.World = EmptyWorld();
            s.World.Enemies.Add(new EnemyInst { key = "ammo", pos = new UnityEngine.Vector2(80f, -28f), state = EnemyState.Chase,
                hp = 75f, hpMax = 75f, windupMax = 0.42f, house = new UnityEngine.Vector2(80f, -28f), homeR = 5f, leash = 30f,
                patrolA = 75f, patrolB = 85f, dir = 1f });
            s.Player.pos = new UnityEngine.Vector2(80f, -28f);
            float hp0 = s.Player.hp, st0 = s.Player.stamina;
            for (int i = 0; i < 20; i++) DiveSim.Tick(s, 0.1f);
            Ok(s.Player.stamina < st0, "enemy: normal enemy damages stamina");
            Ok(s.Player.hp == hp0, "enemy: normal enemy does not damage hp");
        }

        // 11
        private static void Boss_DamagesHp()
        {
            var s = NewState(1);
            s.World = EmptyWorld();
            s.World.Enemies.Add(new EnemyInst { key = "guardian", pos = new UnityEngine.Vector2(80f, -28f), state = EnemyState.Chase,
                hp = 190f, hpMax = 190f, windupMax = 0.5f, house = new UnityEngine.Vector2(80f, -28f), homeR = 5f, leash = 40f,
                patrolA = 75f, patrolB = 85f, dir = 1f });
            s.Player.pos = new UnityEngine.Vector2(80f, -28f);
            float hp0 = s.Player.hp;
            for (int i = 0; i < 30; i++) DiveSim.Tick(s, 0.1f);
            Ok(s.Player.hp < hp0, "boss: damages hp");
            Ok(s.Player.hpActive, "boss: activates hp bar");
        }

        // 12
        private static void Extract_RequiresDive()
        {
            var s = NewState(1);
            s.World = EmptyWorld();
            s.Player.pos = new UnityEngine.Vector2(80f, -0.1f);
            DiveSim.Tick(s, 0.1f);
            Ok(!s.Over, "extract: no dive no extract");
            s.Player.pos = new UnityEngine.Vector2(80f, -20f);
            DiveSim.Tick(s, 0.1f);
            Ok(s.HasDived, "extract: has dived");
            s.Player.pos = new UnityEngine.Vector2(80f, -0.01f);
            for (int i = 0; i < 12; i++) DiveSim.Tick(s, 0.1f);   // 出水锁 0.6s 后撤离
            Ok(s.Over && s.Result == DiveResult.Extract, "extract: returns surface after dive");
        }

        // 13
        // 13
        private static void Surface_HardCeiling()
        {
            var s = NewState(1);
            s.World = EmptyWorld();
            s.Player.pos = new UnityEngine.Vector2(80f, -6f);
            s.HasDived = true;
            float maxY = float.MinValue;
            for (int i = 0; i < 60; i++)
            {
                s.MoveAxis = new UnityEngine.Vector2(0f, 1f);
                if (s.Over) break;
                DiveSim.Tick(s, 0.05f);
                if (s.Player.pos.y > maxY) maxY = s.Player.pos.y;
            }
            Ok(maxY <= 0.0001f, "surface: y never exceeds 0 (max=" + maxY + ")");
            Ok(s.Over && s.Result == DiveResult.Extract, "surface: still extracts after ceiling");
        }

        private static void Exhaust_Death()
        {
            var s = NewState(1);
            s.World = EmptyWorld();
            s.Player.pos = new UnityEngine.Vector2(80f, -30f);
            s.Player.stamina = 0f;
            s.Exhaust = DiveConfig.ExhaustCountdown - 0.1f;
            DiveSim.Tick(s, 0.2f);
            Ok(s.Over && s.Result == DiveResult.Exhausted, "exhaust: death after countdown");
        }

        // 14
        private static void Abort_Result()
        {
            var s = NewState(1);
            DiveSim.Abort(s);
            Ok(s.Over && s.Result == DiveResult.Aborted, "abort: result set");
        }
    }
}
