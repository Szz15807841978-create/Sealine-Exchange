using System.Collections.Generic;
using UnityEngine;

namespace Fishing
{
    /// <summary>渔获分解与资源入包（对应 index.html 的 splitValue / decompose / grantLoot / awardBag）。</summary>
    public static class Loot
    {
        private static readonly string[] SplitPool = { "tuna", "seabass", "octopus", "yellowfin", "sardine", "minnow" };
        private static readonly int[] SplitFaceValue = { 50, 25, 20, 10, 5, 1 };
        private static readonly string[] PlanIds = { "grouper", "tuna", "seabass", "octopus", "yellowfin", "sardine" };
        private static readonly int[] PlanValues = { 100, 50, 25, 20, 10, 5 };

        /// <summary>
        /// 把价值拆成多种鱼：先按面额贪心，再从同一网里挑最多一条大鱼换成若干小鱼。
        /// 全程保证价值守恒。
        /// </summary>
        public static List<int> SplitValue(int v)
        {
            var outList = new List<int>();
            int rest = v;
            int guard = 0;

            while (rest > 0 && guard++ < 300)
            {
                var cands = new List<int>();
                for (int i = 0; i < SplitFaceValue.Length; i++)
                {
                    if (SplitFaceValue[i] <= rest) cands.Add(SplitFaceValue[i]);
                }
                if (cands.Count == 0) break;

                int pick = Rng.Value() < 0.6f ? cands[0] : cands[Rng.Index(cands.Count)];
                outList.Add(pick);
                rest -= pick;
            }

            if (rest > 0) outList.Add(rest);
            return outList;
        }

        public static void Add(Dictionary<string, int> bag, string id, int n)
        {
            if (n == 0) return;

            int cur;
            bag.TryGetValue(id, out cur);
            int next = cur + n;

            if (next <= 0) bag.Remove(id);
            else bag[id] = next;
        }

        /// <summary>对应 JS 里的 delete bag[id]（数量归零时删掉这条）。</summary>
        public static void RemoveIfEmpty(Dictionary<string, int> bag, string id)
        {
            int cur;
            if (bag.TryGetValue(id, out cur) && cur <= 0) bag.Remove(id);
        }

        public static void Merge(Dictionary<string, int> target, Dictionary<string, int> bag)
        {
            foreach (var kv in bag)
            {
                int cur;
                target.TryGetValue(kv.Key, out cur);
                target[kv.Key] = cur + kv.Value;
            }
        }

        public static void Remove(Dictionary<string, int> bag, string id)
        {
            bag.Remove(id);
        }

        /// <summary>对应 decompose(value, bag)：按价值拆鱼，并随机把大鱼拆成小鱼制造"一网多种鱼"的观感。</summary>
        public static Dictionary<string, int> Decompose(int value, Dictionary<string, int> bag = null)
        {
            if (bag == null) bag = new Dictionary<string, int>();

            int rest = value;
            for (int i = 0; i < PlanIds.Length; i++)
            {
                int n = rest / PlanValues[i];
                if (n > 0)
                {
                    Add(bag, PlanIds[i], n);
                    rest -= n * PlanValues[i];
                }
            }

            int w = rest / 2;
            Add(bag, "worm", w);
            rest -= w * 2;
            Add(bag, "minnow", rest);

            // 随机把部分大鱼拆成小鱼（价值不变）
            string[] splitTargets = { "grouper", "tuna", "seabass", "octopus", "yellowfin" };
            int[] splitValues = { 100, 50, 25, 20, 10 };
            for (int i = 0; i < splitTargets.Length; i++)
            {
                string id = splitTargets[i];
                int n;
                if (!bag.TryGetValue(id, out n) || n <= 0) continue;

                int splittable = Mathf.Min(n, Rng.Value() < 0.55f ? 1 : 0);
                for (int k = 0; k < splittable; k++)
                {
                    Add(bag, id, -1);                      // 拿掉一条大鱼
                    var pieces = SplitValue(splitValues[i]);
                    for (int j = 0; j < pieces.Count; j++)
                    {
                        var f = FishDefs.ByValue(pieces[j]);
                        if (f != null) Add(bag, f.Id, 1);
                    }
                }
                RemoveIfEmpty(bag, id);
            }

            return bag;
        }

        public static float BagValue(Dictionary<string, int> bag)
        {
            float t = 0f;
            foreach (var kv in bag)
            {
                var f = FishDefs.ById(kv.Key);
                if (f != null) t += f.Value * kv.Value;
            }
            return t;
        }

        /// <summary>渔获价值（不含线虫）。对应 sellableValue()。</summary>
        public static float SellableValue(Dictionary<string, int> bag)
        {
            float v = 0f;
            foreach (var kv in bag)
            {
                if (kv.Key == "worm") continue;
                var f = FishDefs.ById(kv.Key);
                if (f != null) v += f.Value * kv.Value;
            }
            return v;
        }

        /// <summary>鱼的条数（不含线虫）。对应 fishCount()。</summary>
        public static int FishCount(Dictionary<string, int> bag)
        {
            int n = 0;
            foreach (var kv in bag)
            {
                if (kv.Key != "worm") n += kv.Value;
            }
            return n;
        }

        /// <summary>
        /// 资源入包：线虫单独进修补材料（每条 5%），其余进鱼袋。
        /// 对应 awardBag()。
        /// </summary>
        public static Dictionary<string, int> AwardBag(GameState s, Dictionary<string, int> bag, bool session)
        {
            int w;
            if (bag.TryGetValue("worm", out w) && w > 0)
            {
                s.Worms += w;
                if (session)
                {
                    s.SessionWorms += w;
                    s.SessionLootWorms += w;
                }
                bag.Remove("worm");
            }
            Merge(s.Fish, bag);
            return bag;
        }

        /// <summary>对应 grantLoot(value)：带上场次零头，拆成整数值的鱼。</summary>
        public static Dictionary<string, int> GrantLoot(GameState s, float value, bool session)
        {
            float total = value + s.Litter;
            int whole = Mathf.FloorToInt(total + 1e-9f);
            s.Litter = Mathf.Max(0f, total - whole);
            var bag = Decompose(whole);
            return AwardBag(s, bag, session);
        }
    }
}
