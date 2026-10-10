using System.Collections.Generic;
using UnityEngine;

namespace WhaleFall
{
    /// <summary>
    /// 搜打撤规则层：下潜-搜集-战斗-撤离。只依赖 DiveState / DiveConfig / DiveDefs，
    /// 不碰 UI。控制器只负责灌输入、调方法、刷新视图。
    /// </summary>
    public static class DiveSim
    {
        public static event System.Action<string> Toast;

        private static void Say(string msg)
        {
            var h = Toast;
            if (h != null) h(msg);
        }

        // =====================================================================
        //  深度分层 / 地形
        // =====================================================================

        public static float DepthM(DiveState s) { return -s.Player.pos.y; }

        public static LayerKind LayerAtDepth(float depthM)
        {
            if (depthM < 20f) return LayerKind.Shallow;
            if (depthM < 50f) return LayerKind.Mid;
            if (depthM < 80f) return LayerKind.Deep;
            return LayerKind.Abyss;
        }

        public static float LayerStaminaMul(LayerKind l)
        {
            switch (l)
            {
                case LayerKind.Mid: return 1.2f;
                case LayerKind.Deep: return 1.5f;
                case LayerKind.Abyss: return 2.0f;
                default: return 1.0f;
            }
        }

        public static float LayerEnemyMul(LayerKind l)
        {
            switch (l)
            {
                case LayerKind.Mid: return 1.5f;
                case LayerKind.Deep: return 2.0f;
                case LayerKind.Abyss: return 2.6f;
                default: return 1.0f;
            }
        }

        public static float TerrainStaminaMul(TerrainKind k)
        {
            return k == TerrainKind.Trench ? DiveConfig.TrenchExtra : 1.0f;
        }

        // =====================================================================
        //  主循环
        // =====================================================================

        public static void Tick(DiveState s, float dt)
        {
            if (s.Over) return;
            dt = Mathf.Min(dt, 0.25f);
            s.Time += dt;

            float depth = DepthM(s);
            if (depth > DiveConfig.DiveThresholdM) s.HasDived = true;
            if (depth > s.Stats.MaxDepthM) s.Stats.MaxDepthM = depth;

            UpdateAim(s);
            if (s.Player.sprintCd > 0f) s.Player.sprintCd = Mathf.Max(0f, s.Player.sprintCd - dt);
            if (s.Player.hurtFlash > 0f) s.Player.hurtFlash = Mathf.Max(0f, s.Player.hurtFlash - dt);
            if (s.Player.hpFlash > 0f) s.Player.hpFlash = Mathf.Max(0f, s.Player.hpFlash - dt);
            if (s.Player.surfaceLock > 0f) s.Player.surfaceLock = Mathf.Max(0f, s.Player.surfaceLock - dt);

            bool locked = s.EatingActive || s.MiningIndex >= 0 || s.GatheringIndex >= 0;

            if (!locked)
            {
                if (s.Player.dashing) TickDash(s, dt);
                else MovePlayer(s, dt);
            }

            // 海面是硬顶：不许游到水面之上。否则出水锁那 0.6 秒里玩家会飞出海面约 4—5 米，
            // 深度读数变成负数（Play 实测踩到过）。
            if (s.Player.pos.y > 0f)
            {
                s.Player.pos.y = 0f;
                if (s.Player.vel.y > 0f) s.Player.vel.y = 0f;
            }

            if (s.SpearCd > 0f) s.SpearCd = Mathf.Max(0f, s.SpearCd - dt);
            if (s.PickCd > 0f) s.PickCd = Mathf.Max(0f, s.PickCd - dt);

            TickGather(s, dt);
            TickMine(s, dt);
            TickEat(s, dt);
            TickStamina(s, dt);
            TickProjectiles(s, dt);
            TickEnemies(s, dt);
            TickSurface(s);
        }

        private static void UpdateAim(DiveState s)
        {
            if (!s.AimValid) return;
            Vector2 d = s.AimWorld - s.Player.pos;
            if (d.sqrMagnitude < 0.0001f) return;
            s.Player.aim = Mathf.Atan2(d.y, d.x);
            s.Player.face = Mathf.Cos(s.Player.aim) >= 0f ? 1f : -1f;
        }

        // =====================================================================
        //  移动
        // =====================================================================

        private static void MovePlayer(DiveState s, float dt)
        {
            Vector2 axis = s.MoveAxis;
            if (axis.sqrMagnitude > 1f) axis = axis.normalized;

            float loadFraction = s.Bag.UsedSlots() / (float)DiveConfig.BagSlots;
            float speedMul = 1f - loadFraction * DiveConfig.LoadSpeedPenalty;
            float speed = DiveConfig.PlayerSpeed * speedMul;
            Vector2 target = axis * speed;

            // 指数趋近目标速度（拖拽感），无输入时靠 drag 衰减
            Vector2 v = s.Player.vel;
            v = Vector2.Lerp(v, target, 1f - Mathf.Exp(-DiveConfig.Accel * dt / speed));
            if (axis.sqrMagnitude < 0.01f)
            {
                v *= Mathf.Exp(-DiveConfig.Drag * dt);
            }
            s.Player.vel = v;

            MoveCircle(s.World, ref s.Player.pos.x, ref s.Player.pos.y, v.x * dt, v.y * dt, DiveConfig.PlayerR);
        }

        private static void TickDash(DiveState s, float dt)
        {
            s.Player.dashT -= dt;
            if (s.Player.dashT <= 0f)
            {
                s.Player.dashing = false;
                s.Player.dashT = 0f;
                s.Player.vel = new Vector2(s.Player.dashVx, s.Player.dashVy) * 0.15f;
                return;
            }
            float elapsed = DiveConfig.DashTime - s.Player.dashT;
            float spd = DiveConfig.DashSpeed0 * Mathf.Exp(-DiveConfig.DashDecay * elapsed);
            Vector2 dir = new Vector2(s.Player.dashVx, s.Player.dashVy);
            if (dir.sqrMagnitude < 0.0001f) dir = Vector2.right * s.Player.face;
            dir = dir.normalized;
            MoveCircle(s.World, ref s.Player.pos.x, ref s.Player.pos.y, dir.x * spd * dt, dir.y * spd * dt, DiveConfig.PlayerR);
        }

        private static void MoveCircle(World w, ref float x, ref float y, float dx, float dy, float r)
        {
            // Small steps prevent dashes from crossing a thin 3D collider between frames.
            int steps = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) /
                                                   Mathf.Max(0.05f, r * 0.4f)));
            float sx = dx / steps, sy = dy / steps;

            // 已经嵌在碰撞体里时（给角色挂了碰撞箱、或从很窄的缝里生成），
            // 不能继续一票否决 —— 那样任何方向都判成撞墙，表现就是"黏在碰撞箱上"永远出不来。
            // 此时放行让角色脱离；一旦出来就恢复正常的逐轴阻挡。
            if (w.HitsTerrain(x, y, r))
            {
                for (int i = 0; i < steps; i++) { x += sx; y += sy; }
                return;
            }

            for (int i = 0; i < steps; i++)
            {
                if (!w.HitsTerrain(x + sx, y, r)) x += sx;
                if (!w.HitsTerrain(x, y + sy, r)) y += sy;
            }
        }


        // =====================================================================
        //  闪避
        // =====================================================================

        public static void Dash(DiveState s)
        {
            if (s.Over || s.EatingActive || s.MiningIndex >= 0 || s.GatheringIndex >= 0) return;
            if (s.Player.dashing) return;
            if (s.Player.sprintCd > 0f) { Say("闪避冷却 " + Num1(s.Player.sprintCd) + "s"); return; }

            LayerKind layer = LayerAtDepth(DepthM(s));
            float cost = DiveConfig.SprintExtra * LayerStaminaMul(layer);
            if (s.Player.stamina < cost) { Say("体力不足"); return; }

            s.Player.stamina -= cost;
            Vector2 dir = s.MoveAxis;
            if (dir.sqrMagnitude < 0.01f) dir = s.AimValid ? new Vector2(Mathf.Cos(s.Player.aim), Mathf.Sin(s.Player.aim)) : new Vector2(s.Player.face, 0f);
            dir = dir.normalized;
            s.Player.dashing = true;
            s.Player.dashT = DiveConfig.DashTime;
            s.Player.dashVx = dir.x;
            s.Player.dashVy = dir.y;
            s.Player.sprintCd = DiveConfig.DashCooldown;
        }

        // =====================================================================
        //  模式切换 / 工具解析
        // =====================================================================

        public static void ToggleMode(DiveState s)
        {
            if (s.EatingActive) return;
            s.Mode = s.Mode == ControlMode.Weapon ? ControlMode.Tool : ControlMode.Weapon;
            s.ActiveTool = s.Mode == ControlMode.Weapon ? ToolKind.Spear : ResolveTool(s);
        }

        /// <summary>工具模式下自动选工具：鱼优先于矿点（对应移植规格 1.3）。</summary>
        public static ToolKind ResolveTool(DiveState s)
        {
            int res = NearestResource(s, DiveConfig.M(DiveConfig.HandRangePx), true);
            if (res >= 0) return ToolKind.Hand;
            int mine = NearestMine(s, DiveConfig.M(DiveConfig.PickRangePx));
            if (mine >= 0) return ToolKind.Pick;
            return ToolKind.Hand;
        }

        public static int NearestResource(DiveState s, float range, bool aimed)
        {
            int best = -1;
            float bestD = range * range;
            for (int i = 0; i < s.World.Resources.Count; i++)
            {
                var r = s.World.Resources[i];
                if (!r.alive) continue;
                float d = (r.pos - s.Player.pos).sqrMagnitude;
                if (d < bestD) { bestD = d; best = i; }
            }
            return best;
        }

        public static int NearestMine(DiveState s, float range)
        {
            int best = -1;
            float bestD = range * range;
            for (int i = 0; i < s.World.Mines.Count; i++)
            {
                var m = s.World.Mines[i];
                if (m.depleted) continue;
                float d = (m.center - s.Player.pos).sqrMagnitude;
                if (d < bestD) { bestD = d; best = i; }
            }
            return best;
        }

        // =====================================================================
        //  使用（左键）
        // =====================================================================

        public static void UseDown(DiveState s)
        {
            if (s.Over || s.EatingActive) return;

            if (s.Mode == ControlMode.Weapon) { FireSpear(s); return; }

            ToolKind tool = ResolveTool(s);
            s.ActiveTool = tool;
            if (tool == ToolKind.Pick)
            {
                int m = NearestMine(s, DiveConfig.M(DiveConfig.PickRangePx));
                if (m >= 0) { s.MiningIndex = m; s.MiningT = 0f; }
            }
            else
            {
                int r = NearestResource(s, DiveConfig.M(DiveConfig.HandRangePx), true);
                if (r >= 0) { s.GatheringIndex = r; s.GatheringT = 0f; }
            }
        }

        public static void UseUp(DiveState s)
        {
            // 松开：采集 / 挖掘中断
            if (s.GatheringIndex >= 0)
            {
                int i = s.GatheringIndex;
                s.GatheringIndex = -1;
                s.GatheringT = 0f;
                if (i < s.World.Resources.Count && s.World.Resources[i].alive)
                    PanicResource(s, i);
            }
            if (s.MiningIndex >= 0)
            {
                s.MiningIndex = -1;
                s.MiningT = 0f;
            }
        }

        private static void FireSpear(DiveState s)
        {
            if (s.SpearCd > 0f) return;
            Vector2 dir = s.AimValid ? new Vector2(Mathf.Cos(s.Player.aim), Mathf.Sin(s.Player.aim)) : new Vector2(s.Player.face, 0f);
            dir = dir.normalized;
            s.Projectiles.Add(new Projectile
            {
                pos = s.Player.pos,
                dir = dir,
                rangeLeft = DiveConfig.M(DiveConfig.SpearRangePx),
                dead = false,
            });
            s.SpearCd = DiveConfig.SpearCd;
        }

        // =====================================================================
        //  采集 / 挖掘
        // =====================================================================

        private static void TickGather(DiveState s, float dt)
        {
            if (s.GatheringIndex < 0) return;
            int i = s.GatheringIndex;
            if (i >= s.World.Resources.Count || !s.World.Resources[i].alive)
            {
                s.GatheringIndex = -1; s.GatheringT = 0f; return;
            }

            float maxDist = DiveConfig.M(DiveConfig.HandRangePx + DiveConfig.HandReleaseMarginPx);
            if ((s.World.Resources[i].pos - s.Player.pos).magnitude > maxDist)
            {
                s.GatheringIndex = -1; s.GatheringT = 0f;
                PanicResource(s, i);
                return;
            }

            s.GatheringT += dt;
            if (s.GatheringT >= DiveConfig.HandGatherTime)
            {
                s.GatheringIndex = -1; s.GatheringT = 0f;
                float chance = s.Player.dashing ? DiveConfig.HandEscapeSprint : DiveConfig.HandEscape;
                if (s.Rng.Value() < chance)
                {
                    PanicResource(s, i);
                    Say("挣脱了");
                    return;
                }
                CatchResource(s, i);
            }
        }

        private static void CatchResource(DiveState s, int i)
        {
            var r = s.World.Resources[i];
            r.alive = false;
            s.World.Resources[i] = r;
            s.Stats.Caught++;

            string foodKey = DiveDefs.FoodForSpecies(r.speciesKey);
            var food = DiveDefs.FoodOf(foodKey);
            if (food.Key == null) return;

            bool ok = s.Bag.Add(foodKey, food.Name, "food", food.Size, food.Heal, 1);
            if (ok) Say("拾取 " + food.Name);
            else
            {
                s.FreeFoods.Add(new FreeFood { foodKey = foodKey, name = food.Name, size = food.Size, heal = food.Heal, pos = r.pos, dead = false });
                Say("背包已满，掉落在地");
            }
        }

        private static void PanicResource(DiveState s, int i)
        {
            var r = s.World.Resources[i];
            r.state = 1;   // flee
            r.panic = DiveConfig.HandPanicTime;
            r.dir = -s.Player.face;
            s.World.Resources[i] = r;
        }

        private static void TickMine(DiveState s, float dt)
        {
            if (s.MiningIndex < 0) return;
            int i = s.MiningIndex;
            if (i >= s.World.Mines.Count) { s.MiningIndex = -1; return; }

            var m = s.World.Mines[i];
            float maxDist = DiveConfig.M(DiveConfig.PickRangePx + DiveConfig.HandReleaseMarginPx);
            if ((m.center - s.Player.pos).magnitude > maxDist)
            {
                s.MiningIndex = -1; s.MiningT = 0f; return;
            }

            s.MiningT += dt;
            if (s.MiningT >= DiveConfig.PickMineTime)
            {
                s.MiningT = 0f;
                s.Stats.Mined++;
                AlertEnemies(s, m.center, DiveConfig.MineNoisePx, false);

                int yield = s.Rng.Range(DiveConfig.PickYieldMin, DiveConfig.PickYieldMax + 1);
                for (int k = 0; k < yield; k++)
                    AddMaterial(s, "MAT_ORE", "生物矿结晶", m.center);

                if (s.Rng.Value() < DiveConfig.MineBonusChance)
                    AddMaterial(s, "MAT_ANCIENT", "远古遗骸", m.center);

                m.nodes--;
                if (m.nodes <= 0)
                {
                    m.depleted = true;
                    ClearMineSolid(s, m);
                    Say("矿点挖空");
                }
                s.World.Mines[i] = m;

                s.PickCd = DiveConfig.PickCd;
                if (m.depleted) s.MiningIndex = -1;
            }
        }

        private static void AddMaterial(DiveState s, string key, string name, Vector2 pos)
        {
            bool ok = s.Bag.Add(key, name, "mat", SizeClass.Small, 0f, 1);
            if (!ok)
            {
                s.FreeFoods.Add(new FreeFood { foodKey = key, name = name, size = SizeClass.Small, heal = 0f, pos = pos, dead = false });
            }
        }

        private static void ClearMineSolid(DiveState s, Mine m)
        {
            // A depleted resource node does not delete the supporting 3D rock collider.
        }


        // =====================================================================
        //  进食
        // =====================================================================

        public static bool TryEat(DiveState s, string foodKey)
        {
            if (s.Over || s.EatingActive) return false;
            var food = DiveDefs.FoodOf(foodKey);
            if (food.Key == null) return false;
            if (!s.Bag.ConsumeOne(foodKey)) return false;

            bool emergency = s.Player.stamina <= 0f && s.Exhaust > 0f;
            s.EatingActive = true;
            s.EatingFoodKey = foodKey;
            s.EatingT = 0f;
            s.EatingTotal = emergency ? DiveConfig.EatTimeEmergency : DiveConfig.EatTime;
            s.EatingEmergency = emergency;
            return true;
        }

        private static void TickEat(DiveState s, float dt)
        {
            if (!s.EatingActive) return;
            s.EatingT += dt;
            if (s.EatingT < s.EatingTotal) return;

            var food = DiveDefs.FoodOf(s.EatingFoodKey);
            s.Player.stamina = Mathf.Clamp(s.Player.stamina + food.Heal, 0f, s.Player.staminaMax);
            s.Exhaust = 0f;   // 进食后脱离力竭倒计时
            s.EatingActive = false;
            s.EatingEmergency = false;
            s.EatingFoodKey = null;
            Say("+" + (int)food.Heal + " 体力");
            AlertEnemies(s, s.Player.pos, DiveConfig.EatNoisePx, true);
        }

        // =====================================================================
        //  体力 / 力竭
        // =====================================================================

        private static void TickStamina(DiveState s, float dt)
        {
            float depth = DepthM(s);
            if (depth <= DiveConfig.DiveThresholdM)
            {
                s.Exhaust = 0f;
                return;   // 海面不扣体力
            }

            if (s.Player.stamina > 0f)
            {
                LayerKind layer = LayerAtDepth(depth);
                float loadFraction = s.Bag.UsedSlots() / (float)DiveConfig.BagSlots;
                float loadMul = Mathf.Lerp(DiveConfig.LoadMulEmpty, DiveConfig.LoadMulFull, loadFraction);
                float terrainMul = TerrainStaminaMul(s.World.KindAt(s.Player.pos.x, s.Player.pos.y));
                float drain = DiveConfig.BaseDrain * LayerStaminaMul(layer) * loadMul * terrainMul * dt;
                s.Player.stamina = Mathf.Max(0f, s.Player.stamina - drain);
                if (s.Player.stamina <= 0f) s.Exhaust = 0f;
                return;
            }

            // 力竭倒计时：紧急进食期间暂停
            if (!(s.EatingActive && s.EatingEmergency))
            {
                s.Exhaust += dt;
            }
            if (s.Exhaust >= DiveConfig.ExhaustCountdown)
            {
                Fail(s, DiveResult.Exhausted, "力竭");
            }
        }

        // =====================================================================
        //  弹体
        // =====================================================================

        private static void TickProjectiles(DiveState s, float dt)
        {
            float speed = DiveConfig.M(DiveConfig.SpearSpeedPx);
            for (int i = 0; i < s.Projectiles.Count; i++)
            {
                var p = s.Projectiles[i];
                if (p.dead) continue;
                float step = speed * dt;
                p.pos += p.dir * step;
                p.rangeLeft -= step;
                if (p.rangeLeft <= 0f) p.dead = true;

                for (int e = 0; e < s.World.Enemies.Count; e++)
                {
                    var en = s.World.Enemies[e];
                    if (en.dead) continue;
                    float r = DiveConfig.M(DiveDefs.EnemyOf(en.key).RPx) + 0.4f;
                    if ((en.pos - p.pos).magnitude < r)
                    {
                        HitEnemy(s, e, DiveConfig.SpearDmg);
                        p.dead = true;
                        break;
                    }
                }
                s.Projectiles[i] = p;
            }
            s.Projectiles.RemoveAll(p => p.dead);
        }

        // =====================================================================
        //  敌人
        // =====================================================================

        public static void AlertEnemies(DiveState s, Vector2 pos, float radiusPx, bool asLure)
        {
            float radius = DiveConfig.M(radiusPx);
            for (int i = 0; i < s.World.Enemies.Count; i++)
            {
                var e = s.World.Enemies[i];
                if (e.dead) continue;
                if ((e.pos - pos).magnitude > radius) continue;
                if (asLure)
                {
                    e.state = EnemyState.Lured;
                    e.lured = true;
                    e.luredT = 6.5f;
                    e.lureTarget = pos;
                    e.searchT = 6.5f;
                }
                else
                {
                    e.aggro = true;
                    e.searchT = 4f;
                    if (e.key == "guardian") e.state = EnemyState.Chase;
                }
                s.World.Enemies[i] = e;
            }
        }

        private static void TickEnemies(DiveState s, float dt)
        {
            var p = s.Player;
            for (int i = 0; i < s.World.Enemies.Count; i++)
            {
                var e = s.World.Enemies[i];
                if (e.dead) continue;

                if (e.atkCd > 0f) e.atkCd = Mathf.Max(0f, e.atkCd - dt);
                if (e.stun > 0f) e.stun = Mathf.Max(0f, e.stun - dt);
                if (e.luredT > 0f) e.luredT = Mathf.Max(0f, e.luredT - dt);
                if (e.searchT > 0f) e.searchT = Mathf.Max(0f, e.searchT - dt);

                float dist = (e.pos - p.pos).magnitude;
                var def = DiveDefs.EnemyOf(e.key);
                float detectR = DiveConfig.M(def.DetectPx) * (p.dashing ? 1.25f : 1f);
                bool sees = dist < detectR && ClearLine(s.World, e.pos, p.pos);

                if (sees && e.state != EnemyState.Attack)
                {
                    e.aggro = true;
                    e.searchT = 4.5f;
                    if (e.state != EnemyState.Lured) e.state = EnemyState.Chase;
                }

                // 攻击判定（前摇结束）
                if (e.state == EnemyState.Attack)
                {
                    e.windup -= dt;
                    if (e.windup <= 0f)
                    {
                        float reach = DiveConfig.M(def.ReachPx) + DiveConfig.PlayerR + DiveConfig.M(6f);
                        if (dist < reach && ClearLine(s.World, e.pos, p.pos))
                        {
                            DamagePlayer(s, def);
                        }
                        e.atkCd = def.AtkCd;
                        e.state = EnemyState.Chase;
                    }
                }

                // 进入攻击距离
                if (e.state == EnemyState.Chase)
                {
                    float reach = DiveConfig.M(def.ReachPx) + DiveConfig.PlayerR + DiveConfig.M(6f);
                    if (dist < reach && e.atkCd <= 0f)
                    {
                        e.windup = def.Windup;
                        e.windupMax = def.Windup;
                        e.state = EnemyState.Attack;
                    }
                }

                // 放弃条件
                float homeDist = (e.pos - e.house).magnitude;
                bool giveUp = e.searchT <= 0f || dist > detectR * 1.7f || homeDist > e.leash;
                if (giveUp && e.state != EnemyState.Return && e.state != EnemyState.Patrol)
                {
                    e.aggro = false;
                    e.state = EnemyState.Return;
                }

                // 按状态移动
                MoveEnemy(s, ref e, dt);

                // 离开巢穴过远 → 回巢
                if ((e.pos - e.house).magnitude > e.homeR && e.state == EnemyState.Patrol)
                {
                    e.state = EnemyState.Return;
                }

                s.World.Enemies[i] = e;
            }
        }

        private static void MoveEnemy(DiveState s, ref EnemyInst e, float dt)
        {
            var def = DiveDefs.EnemyOf(e.key);
            Vector2 v = Vector2.zero;

            switch (e.state)
            {
                case EnemyState.Patrol:
                    {
                        float spd = DiveConfig.M(def.SpeedPx);
                        e.dir = (e.pos.x > e.patrolB) ? -1f : (e.pos.x < e.patrolA) ? 1f : e.dir;
                        v = new Vector2(e.dir * spd, Mathf.Sin(s.Time * 0.6f + e.house.x) * spd * 0.3f);
                        break;
                    }
                case EnemyState.Chase:
                    {
                        float spd = DiveConfig.M(def.ChaseSpeedPx);
                        v = (s.Player.pos - e.pos).normalized * spd;
                        break;
                    }
                case EnemyState.Lured:
                    {
                        float spd = DiveConfig.M(def.SpeedPx) * 1.15f;
                        v = (e.lureTarget - e.pos).normalized * spd;
                        if ((e.lureTarget - e.pos).magnitude < 1f) { e.state = EnemyState.Return; }
                        break;
                    }
                case EnemyState.Return:
                    {
                        float spd = DiveConfig.M(def.SpeedPx) * 1.1f;
                        v = (e.house - e.pos).normalized * spd;
                        if ((e.house - e.pos).magnitude < DiveConfig.M(26f)) { e.state = EnemyState.Patrol; }
                        break;
                    }
                case EnemyState.Flee:
                    {
                        float spd = DiveConfig.M(def.ChaseSpeedPx);
                        v = (e.pos - s.Player.pos).normalized * spd;
                        if (e.searchT <= 0f) e.state = EnemyState.Return;
                        break;
                    }
                default:
                    return;
            }

            // Enemies use the same 3D terrain colliders as the player.
            MoveCircle(s.World, ref e.pos.x, ref e.pos.y, v.x * dt, v.y * dt, DiveConfig.M(def.RPx));
        }

        private static bool ClearLine(World w, Vector2 a, Vector2 b)
        {
            float dist = (b - a).magnitude;
            int steps = Mathf.CeilToInt(dist / (DiveConfig.Cell * 0.5f));
            for (int i = 1; i < steps; i++)
            {
                Vector2 p = Vector2.Lerp(a, b, i / (float)steps);
                if (w.IsSolid(p.x, p.y)) return false;
            }
            return true;
        }

        private static void HitEnemy(DiveState s, int index, float dmg)
        {
            var e = s.World.Enemies[index];
            if (e.dead) return;
            e.hp -= dmg;
            e.aggro = true;
            e.searchT = 5f;
            if (e.state != EnemyState.Attack) e.state = EnemyState.Chase;
            e.stun = DiveConfig.SpearStun;

            if (e.hp <= 0f)
            {
                e.dead = true;
                s.Stats.Kills++;
                // 掉落食物（首领掉大型鱼肉，普通敌对掉小型虾肉/贝肉）
                string foodKey = e.key == "guardian" ? "FOOD_FISH_L" : (e.key == "ammo" ? "FOOD_SHRIMP_S" : "FOOD_FISH_M");
                var food = DiveDefs.FoodOf(foodKey);
                s.FreeFoods.Add(new FreeFood { foodKey = foodKey, name = food.Name, size = food.Size, heal = food.Heal, pos = e.pos, dead = false });
                Say("击倒 " + DiveDefs.EnemyOf(e.key).Name);
            }
            s.World.Enemies[index] = e;
        }

        private static void DamagePlayer(DiveState s, EnemyDef def)
        {
            float dmg = s.Rng.Range(def.DmgMin, def.DmgMax);
            s.Player.hurtFlash = 0.3f;
            if (def.DmgHp)
            {
                s.Player.hpActive = true;
                s.Player.hp -= dmg;
                s.Player.hpFlash = 0.3f;
                if (s.Player.hp <= 0f)
                {
                    s.Player.hp = 0f;
                    Fail(s, DiveResult.Killed, "被" + def.Name + "击杀");
                }
            }
            else
            {
                s.Player.stamina = Mathf.Max(0f, s.Player.stamina - dmg);
            }
        }

        // =====================================================================
        //  拾取 / 撤离 / 死亡
        // =====================================================================

        public static void Interact(DiveState s)
        {
            if (s.Over) return;
            int best = -1;
            float bestD = DiveConfig.M(DiveConfig.HandRangePx) * DiveConfig.M(DiveConfig.HandRangePx);
            for (int i = 0; i < s.FreeFoods.Count; i++)
            {
                var f = s.FreeFoods[i];
                if (f.dead) continue;
                float d = (f.pos - s.Player.pos).sqrMagnitude;
                if (d < bestD) { bestD = d; best = i; }
            }
            if (best < 0) return;
            var ff = s.FreeFoods[best];
            if (s.Bag.Add(ff.foodKey, ff.name, ff.KindOf(), ff.size, ff.heal, 1))
            {
                ff.dead = true;
                s.FreeFoods[best] = ff;
                Say("拾取 " + ff.name);
            }
            else
            {
                Say("背包已满");
            }
        }

        private static void TickSurface(DiveState s)
        {
            if (s.Player.pos.y >= -0.05f && s.HasDived)
            {
                if (s.Player.surfaceLock <= 0f)
                {
                    s.Over = true;
                    s.Result = DiveResult.Extract;
                    s.DeathReason = null;
                    Say("撤离成功");
                }
            }
            else if (s.Player.pos.y < -0.05f)
            {
                s.Player.surfaceLock = DiveConfig.SurfaceLock;
            }
        }

        public static void Abort(DiveState s)
        {
            if (s.Over) return;
            Fail(s, DiveResult.Aborted, "主动放弃下潜");
        }

        private static void Fail(DiveState s, DiveResult r, string reason)
        {
            if (s.Over) return;
            s.Over = true;
            s.Result = r;
            s.DeathReason = reason;
            Say(reason);
        }

        // =====================================================================
        //  展示辅助
        // =====================================================================

        public static string Num1(float v) { return v.ToString("0.0"); }
    }

    public static class FreeFoodExt
    {
        public static string KindOf(this FreeFood f)
        {
            if (f.heal > 0f) return "food";
            return "mat";
        }
    }
}
