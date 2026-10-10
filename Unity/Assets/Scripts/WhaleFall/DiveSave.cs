using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace WhaleFall
{
    // =====================================================================
    //  存档结构（移植规格 §5.4）
    //  只用公开字段 + [Serializable]，交给 Unity 的 JsonUtility 序列化。
    //  不要直接序列化运行时对象（World / DiveState 有循环引用，不适合存）。
    // =====================================================================

    [System.Serializable]
    public class UpgradeSave
    {
        public float staminaMax;
        public float moveSpeed;
        public float hpMax;
    }

    [System.Serializable]
    public class ChestSlotSave
    {
        public string itemKey;
        public int count;
    }

    [System.Serializable]
    public class DeviceSave
    {
        public string deviceKey;    // STILL / BENCH / STOVE / RACK / FERMENT / YOGURT / CHEST
        public float x;
        public float y;
    }

    [System.Serializable]
    public class RaftSave
    {
        public List<DeviceSave> devices = new List<DeviceSave>();
        public int expandLevel;
    }

    [System.Serializable]
    public class WorldSave
    {
        public int day = 1;
        public int weatherToday;
        public List<int> forecast = new List<int>();
        public string location = "A01";
        /// <summary>世界随机种子。读档后地图/天气不得变化，所以必须存。</summary>
        public int seed;
    }

    [System.Serializable]
    public class QuestSave
    {
        public List<string> flags = new List<string>();
        public string active = "";
    }

    [System.Serializable]
    public class BossSave
    {
        public List<string> guardianDefeatedAt = new List<string>();
    }

    [System.Serializable]
    public class UnlockSave
    {
        public List<string> weapons = new List<string>();
        public List<string> tools = new List<string>();
    }

    [System.Serializable]
    public class EndingSave
    {
        public bool qualifiedTrue;
        public bool qualifiedFalse;
        public List<string> achieved = new List<string>();
    }

    [System.Serializable]
    public class SaveData
    {
        public int version = DiveSave.CurrentVersion;
        public string playerName = "";
        public UpgradeSave upgrades = new UpgradeSave();
        public List<ChestSlotSave> chest = new List<ChestSlotSave>();
        public RaftSave raft = new RaftSave();
        public WorldSave world = new WorldSave();
        public QuestSave quests = new QuestSave();
        public BossSave bosses = new BossSave();
        public UnlockSave unlocked = new UnlockSave();
        public EndingSave ending = new EndingSave();
    }

    /// <summary>
    /// 存档读写。JSON + 版本号（对应策划案 §14「存档污染」对策）。
    /// 存档点与手动存档共用同一结构，只是写入时机不同（§5.3）。
    /// </summary>
    public static class DiveSave
    {
        public const int CurrentVersion = 1;
        private const string FileName = "whalefall_save.json";

        /// <summary>任务旗标常量（流程文档「9 任务旗标与状态清单」）。</summary>
        public static class Flags
        {
            public const string PlayerNamed = "PLAYER_NAMED";
            public const string FirstWhalefallDone = "FIRST_WHALEFALL_DONE";
            public const string TranslatorOwned = "TRANSLATOR_OWNED";
            public const string FinalBossWin = "FINAL_BOSS_WIN";
            public const string EndingTrue = "ENDING_TRUE";
        }

        public static string FilePath { get { return Path.Combine(Application.persistentDataPath, FileName); } }

        public static bool Exists() { return File.Exists(FilePath); }

        public static SaveData NewGame()
        {
            var d = new SaveData();
            d.world.seed = Random.Range(int.MinValue, int.MaxValue);
            d.quests.flags.Clear();
            return d;
        }

        public static SaveData Load()
        {
            if (!File.Exists(FilePath)) return NewGame();
            try
            {
                string json = File.ReadAllText(FilePath);
                var d = JsonUtility.FromJson<SaveData>(json);
                if (d == null) return NewGame();
                Migrate(d);
                return d;
            }
            catch (System.Exception e)
            {
                // 存档坏了不能让游戏起不来：退回新档，并把坏档留一份
                Debug.LogError("[鲸落] 存档读取失败，改用新档：" + e.Message);
                try { File.Copy(FilePath, FilePath + ".bad", true); } catch { }
                return NewGame();
            }
        }

        public static void Save(SaveData d)
        {
            if (d == null) return;
            d.version = CurrentVersion;
            try
            {
                string json = JsonUtility.ToJson(d, true);
                var dir = Path.GetDirectoryName(FilePath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                // 先写临时文件再替换，避免写一半崩掉留个坏档
                string tmp = FilePath + ".tmp";
                File.WriteAllText(tmp, json);
                if (File.Exists(FilePath)) File.Delete(FilePath);
                File.Move(tmp, FilePath);
            }
            catch (System.Exception e)
            {
                Debug.LogError("[鲸落] 存档写入失败：" + e.Message);
            }
        }

        public static void Delete()
        {
            try { if (File.Exists(FilePath)) File.Delete(FilePath); } catch { }
        }

        /// <summary>版本迁移。新增字段时在这里补默认值。</summary>
        private static void Migrate(SaveData d)
        {
            if (d.version <= 0) d.version = 1;
            if (d.upgrades == null) d.upgrades = new UpgradeSave();
            if (d.chest == null) d.chest = new List<ChestSlotSave>();
            if (d.raft == null) d.raft = new RaftSave();
            if (d.world == null) d.world = new WorldSave();
            if (d.quests == null) d.quests = new QuestSave();
            if (d.bosses == null) d.bosses = new BossSave();
            if (d.unlocked == null) d.unlocked = new UnlockSave();
            if (d.ending == null) d.ending = new EndingSave();
            if (d.quests.flags == null) d.quests.flags = new List<string>();
            if (d.world.forecast == null) d.world.forecast = new List<int>();
            d.version = CurrentVersion;
        }

        // =====================================================================
        //  便捷操作
        // =====================================================================

        public static bool HasFlag(SaveData d, string flag)
        {
            return d != null && d.quests != null && d.quests.flags != null && d.quests.flags.Contains(flag);
        }

        public static void SetFlag(SaveData d, string flag)
        {
            if (d == null || d.quests == null) return;
            if (d.quests.flags == null) d.quests.flags = new List<string>();
            if (!d.quests.flags.Contains(flag)) d.quests.flags.Add(flag);
        }

        /// <summary>往储物箱里塞东西（跨时代唯一保留的库存）。返回是否成功。</summary>
        public static bool ChestAdd(SaveData d, string itemKey, int count)
        {
            if (d == null || string.IsNullOrEmpty(itemKey) || count <= 0) return false;
            if (d.chest == null) d.chest = new List<ChestSlotSave>();
            foreach (var s in d.chest)
            {
                if (s.itemKey == itemKey) { s.count += count; return true; }
            }
            d.chest.Add(new ChestSlotSave { itemKey = itemKey, count = count });
            return true;
        }

        /// <summary>从储物箱取东西。返回实际取到的数量。</summary>
        public static int ChestTake(SaveData d, string itemKey, int count)
        {
            if (d == null || d.chest == null || count <= 0) return 0;
            for (int i = 0; i < d.chest.Count; i++)
            {
                if (d.chest[i].itemKey != itemKey) continue;
                int take = Mathf.Min(count, d.chest[i].count);
                d.chest[i].count -= take;
                if (d.chest[i].count <= 0) d.chest.RemoveAt(i);
                return take;
            }
            return 0;
        }
    }
}
