using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace BiopunkRV
{
    [Serializable]
    public class CultivationSave
    {
        public int version = 1;
        public int stones, methodFragments, affixFragments, cultivation, artifactTier, refinement;
        public int[] upgrades = new int[6];
        public int[] facilities = new int[3];
        public int[] talents = new int[3];
        public List<string> methods = new List<string>();
        public List<string> daos = new List<string>();
        public string mainMethod = "", supportMethod = "", forbiddenMethod = "";
        public int selectedRoot;
    }

    /// <summary>One wallet and durable save for all lobby systems. Run loot is settled once.</summary>
    public static class MetaProgression
    {
        private static CultivationSave data;
        public static CultivationSave Data { get { EnsureLoaded(); return data; } }
        public static string SavePath => Path.Combine(Application.persistentDataPath, "cultivation_v1.json");
        public static readonly string[] UpgradeNames = { "气血", "攻击", "防御", "速度", "幸运", "暴击" };
        public static readonly int[] UpgradeCosts = { 5, 5, 5, 8, 10, 10 };
        public static readonly int[] UpgradeLimits = { 89, 479, 479, 30, 100, 45 };
        public static readonly string[] FacilityNames = { "悟道台", "灵脉", "演武场" };
        public static readonly string[] TalentNames = { "术 · 灵力", "法 · 根基", "道 · 悟性" };

        public static void EnsureLoaded()
        {
            if (data != null) return;
            data = ReadSave(SavePath) ?? ReadSave(SavePath + ".bak") ?? new CultivationSave();
            data.stones = Mathf.Max(0, data.stones);
            data.methodFragments = Mathf.Max(0, data.methodFragments);
            data.affixFragments = Mathf.Max(0, data.affixFragments);
            data.cultivation = Mathf.Max(0, data.cultivation);
            if (data.selectedRoot != (int)Element.Fire && data.selectedRoot != (int)Element.Water && data.selectedRoot != (int)Element.Earth && data.selectedRoot != (int)Element.Wind && data.selectedRoot != (int)Element.Thunder) data.selectedRoot = 0;
            data.upgrades = Normalize(data.upgrades, 6, 479);
            for (int i = 0; i < 6; i++) data.upgrades[i] = Mathf.Min(data.upgrades[i], UpgradeLimits[i]);
            data.facilities = Normalize(data.facilities, 3, 10);
            data.talents = Normalize(data.talents, 3, 10);
            if (data.methods == null) data.methods = new List<string>();
            if (data.daos == null) data.daos = new List<string>();
            RealmSystem.Restore(data.cultivation);
            ArtifactSystem.Restore(data.artifactTier, data.refinement);
        }

        private static CultivationSave ReadSave(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                var result = JsonUtility.FromJson<CultivationSave>(File.ReadAllText(path));
                if (result == null || result.version != 1) throw new InvalidDataException("Unsupported save version");
                return result;
            }
            catch (Exception e) { Debug.LogWarning("修炼存档读取失败: " + e.Message); return null; }
        }

        private static int[] Normalize(int[] source, int count, int limit)
        {
            var result = new int[count];
            if (source != null)
                for (int i = 0; i < Mathf.Min(source.Length, count); i++) result[i] = Mathf.Clamp(source[i], 0, limit);
            return result;
        }

        public static void Save()
        {
            EnsureLoaded();
            data.cultivation = RealmSystem.TotalCultivationPoints;
            data.artifactTier = (int)ArtifactSystem.CurrentTier;
            data.refinement = ArtifactSystem.RefineLevel;
            try
            {
                Directory.CreateDirectory(Application.persistentDataPath);
                string temp = SavePath + ".tmp";
                File.WriteAllText(temp, JsonUtility.ToJson(data, true));
                if (File.Exists(SavePath)) File.Replace(temp, SavePath, SavePath + ".bak");
                else File.Move(temp, SavePath);
            }
            catch (Exception e) { Debug.LogError("修炼进度保存失败: " + e.Message); }
        }

        public static bool Spend(int cost)
        {
            if (cost < 0 || Data.stones < cost) return false;
            data.stones -= cost;
            return true;
        }

        public static void SettleRun()
        {
            Data.stones += Mathf.Max(0, RunInventory.spiritStones);
            data.methodFragments += Mathf.Max(0, RunInventory.methodFragments);
            data.affixFragments += Mathf.Max(0, RunInventory.affixFragments);
            RunInventory.Reset();
            Save();
        }

        public static bool Upgrade(int index)
        {
            if (index < 0 || index >= 6 || Data.upgrades[index] >= UpgradeLimits[index]) return false;
            if (!Spend(UpgradeCosts[index])) return false;
            data.upgrades[index]++;
            Save();
            return true;
        }

        public static bool ImproveFacility(int index)
        {
            if (index < 0 || index >= 3 || Data.facilities[index] >= 10) return false;
            if (!Spend(20 * (data.facilities[index] + 1))) return false;
            data.facilities[index]++;
            Save();
            return true;
        }

        public static bool ImproveTalent(int index)
        {
            if (index < 0 || index >= 3 || Data.talents[index] >= 10) return false;
            int level = data.talents[index];
            if (data.facilities[0] <= level / 2 || data.affixFragments < level + 1) return false;
            if (!Spend(10 * (level + 1))) return false;
            data.affixFragments -= level + 1;
            data.talents[index]++;
            Save();
            return true;
        }

        public static bool IsMethodUnlocked(string id) => !string.IsNullOrEmpty(id) && Data.methods.Contains(id);

        public static bool UnlockMethod(string id, int slot, bool useFragments)
        {
            if (slot < 0 || slot > 2 || IsMethodUnlocked(id) || MethodEditor.ResolveById(id) == null) return false;
            if (useFragments)
            {
                if (Data.methodFragments < 5) return false;
                data.methodFragments -= 5;
            }
            else if (!Spend(slot == 0 ? 50 : slot == 1 ? 20 : 100)) return false;
            data.methods.Add(id);
            Save();
            return true;
        }

        public static bool EquipMethod(string id, int slot)
        {
            if (slot < 0 || slot > 2 || !IsMethodUnlocked(id)) return false;
            var method = MethodEditor.ResolveById(id);
            if (method == null) return false;
            string[] slots = { Data.mainMethod, data.supportMethod, data.forbiddenMethod };
            for (int i = 0; i < 3; i++)
            {
                var other = MethodEditor.ResolveById(slots[i]);
                if (i != slot && other != null && other.baseSkillId == method.baseSkillId) return false;
            }
            if (slot == 0) data.mainMethod = id;
            else if (slot == 1) data.supportMethod = id;
            else data.forbiddenMethod = id;
            Save();
            return true;
        }

        public static bool UnlockDao(string id)
        {
            if (string.IsNullOrEmpty(id) || Data.daos.Contains(id) || data.facilities[0] == 0 || data.methodFragments < 3) return false;
            if (!Spend(25)) return false;
            data.methodFragments -= 3;
            data.daos.Add(id);
            Save();
            return true;
        }

        public static void ApplyStats(PlayerStats stats)
        {
            var d = Data;
            stats.maxHP = Mathf.Min(999, stats.maxHP + 10 * d.upgrades[0]);
            stats.attack = Mathf.Min(999, stats.attack + 2 * d.upgrades[1]);
            stats.defense = Mathf.Min(999, stats.defense + 2 * d.upgrades[2]);
            stats.moveSpeed = Mathf.Min(120, stats.moveSpeed + 2 * d.upgrades[3]);
            stats.luck = Mathf.Min(100, stats.luck + d.upgrades[4]);
            stats.critRate = Mathf.Min(.5f, stats.critRate + .01f * d.upgrades[5]);
            stats.attack += 2 * d.facilities[2] + 2 * d.talents[0];
            stats.maxHP += 10 * d.talents[1];
            stats.luck += d.talents[2];
            stats.pickupRange += 5 * d.facilities[1];
            stats.currentHP = stats.maxHP;
        }
    }
}
