using UnityEngine;

namespace BiopunkRV
{
    /// <summary>
    /// 境界系统 — 练气→筑基→金丹→元婴→化神→炼虚→合体→大乘→渡劫→飞升。
    /// 每个境界提供永久属性加成，跨场景持久化。
    /// 修炼进度通过积累修为点数(RunInventory.spiritStones)提升境界。
    /// </summary>
    public static class RealmSystem
    {
        public enum Realm
        {
            练气 = 0,
            筑基 = 1,
            金丹 = 2,
            元婴 = 3,
            化神 = 4,
            炼虚 = 5,
            合体 = 6,
            大乘 = 7,
            渡劫 = 8,
            飞升 = 9
        }

        // 当前境界（静态持久化）
        public static Realm CurrentRealm { get; private set; } = Realm.练气;

        // 当前境界内的修为进度（0~1）
        public static float RealmProgress { get; private set; } = 0f;

        // 总修为点数（用于显示和计算）
        public static int TotalCultivationPoints { get; private set; } = 0;

        // 升入下一境界所需修为
        private static readonly int[] _realmThresholds = {
            0,      // 练气
            100,    // 筑基
            300,    // 金丹
            600,    // 元婴
            1000,   // 化神
            1500,   // 炼虚
            2200,   // 合体
            3000,   // 大乘
            4000,   // 渡劫
            5500    // 飞升
        };

        /// <summary>境界名称数组</summary>
        public static readonly string[] RealmNames = {
            "练气", "筑基", "金丹", "元婴", "化神",
            "炼虚", "合体", "大乘", "渡劫", "飞升"
        };

        /// <summary>境界描述</summary>
        public static readonly string[] RealmDescriptions = {
            "初入修仙，感悟灵气",
            "根基稳固，灵力初成",
            "金丹大成，法力浑厚",
            "元婴出窍，神通自如",
            "化神归真，参悟大道",
            "炼虚通玄，虚空可破",
            "天人合一，法力无边",
            "大乘圆满，道法自然",
            "渡劫飞升，超凡入圣",
            "飞升成仙，万法归宗"
        };

        // ---- 境界加成 ----
        public struct RealmBonus
        {
            public float maxHP;
            public float attack;
            public float defense;
            public float critRate;
            public float critDamage;
            public float moveSpeed;
            public float attackSpeed;
            public float luck;
            public float elementDamage;
            public float elementDefense;
        }

        /// <summary>获取当前境界加成</summary>
        public static RealmBonus GetRealmBonus()
        {
            return GetRealmBonus(CurrentRealm);
        }

        /// <summary>获取指定境界加成</summary>
        public static RealmBonus GetRealmBonus(Realm realm)
        {
            switch (realm)
            {
                case Realm.练气:
                    // 起始境界无加成 — 基础属性由PlayerStats.asset提供, 这里曾误填完整基础值导致全部属性翻倍
                    return new RealmBonus();
                case Realm.筑基:
                    return new RealmBonus { maxHP = 30, attack = 8, defense = 5, moveSpeed = 2 };
                case Realm.金丹:
                    return new RealmBonus { maxHP = 80, attack = 20, defense = 12, critRate = 0.03f, attackSpeed = 1, elementDamage = 5 };
                case Realm.元婴:
                    return new RealmBonus { maxHP = 150, attack = 40, defense = 25, critRate = 0.05f, critDamage = 0.2f, moveSpeed = 4, attackSpeed = 2, luck = 5, elementDamage = 10, elementDefense = 5 };
                case Realm.化神:
                    return new RealmBonus { maxHP = 250, attack = 70, defense = 40, critRate = 0.08f, critDamage = 0.4f, moveSpeed = 6, attackSpeed = 4, luck = 10, elementDamage = 20, elementDefense = 10 };
                case Realm.炼虚:
                    return new RealmBonus { maxHP = 500, attack = 150, defense = 70, critRate = 0.12f, critDamage = 0.7f, moveSpeed = 10, attackSpeed = 6, luck = 20, elementDamage = 40, elementDefense = 20 };
                case Realm.合体:
                    return new RealmBonus { maxHP = 900, attack = 300, defense = 130, critRate = 0.16f, critDamage = 1.0f, moveSpeed = 14, attackSpeed = 9, luck = 35, elementDamage = 70, elementDefense = 35 };
                case Realm.大乘:
                    return new RealmBonus { maxHP = 1600, attack = 550, defense = 230, critRate = 0.22f, critDamage = 1.5f, moveSpeed = 20, attackSpeed = 14, luck = 55, elementDamage = 120, elementDefense = 60 };
                case Realm.渡劫:
                    return new RealmBonus { maxHP = 3000, attack = 1000, defense = 400, critRate = 0.30f, critDamage = 2.2f, moveSpeed = 28, attackSpeed = 20, luck = 80, elementDamage = 200, elementDefense = 100 };
                case Realm.飞升:
                    return new RealmBonus { maxHP = 6000, attack = 2000, defense = 800, critRate = 0.50f, critDamage = 4.0f, moveSpeed = 40, attackSpeed = 30, luck = 120, elementDamage = 400, elementDefense = 200 };
                default:
                    return new RealmBonus();
            }
        }

        /// <summary>增加修为点数，自动检查突破</summary>
        public static void AddCultivationPoints(int points)
        {
            if (points <= 0) return;
            MetaProgression.EnsureLoaded();
            TotalCultivationPoints += points;

            // 检查突破
            while (CurrentRealm < Realm.飞升)
            {
                int threshold = _realmThresholds[(int)CurrentRealm + 1];
                if (TotalCultivationPoints >= threshold)
                {
                    CurrentRealm++;
                    RealmProgress = 0f;
                    Debug.Log($"[RealmSystem] 境界突破！当前境界: {RealmNames[(int)CurrentRealm]}");
                }
                else
                {
                    int prevThreshold = _realmThresholds[(int)CurrentRealm];
                    RealmProgress = (float)(TotalCultivationPoints - prevThreshold) / (threshold - prevThreshold);
                    break;
                }
            }

            if (CurrentRealm >= Realm.飞升)
                RealmProgress = 1f;
            MetaProgression.Save();
        }

        public static void Restore(int points)
        {
            TotalCultivationPoints = Mathf.Max(0, points);
            CurrentRealm = Realm.练气;
            while (CurrentRealm < Realm.飞升 && TotalCultivationPoints >= _realmThresholds[(int)CurrentRealm + 1]) CurrentRealm++;
            int index = (int)CurrentRealm;
            RealmProgress = index == 9 ? 1f : (float)(TotalCultivationPoints - _realmThresholds[index]) / (_realmThresholds[index + 1] - _realmThresholds[index]);
        }

        /// <summary>获取当前境界进度百分比(0~1)</summary>
        public static float GetProgressPercent()
        {
            if (CurrentRealm >= Realm.飞升) return 1f;
            return RealmProgress;
        }

        /// <summary>获取当前境界名称</summary>
        public static string GetRealmName()
        {
            return RealmNames[(int)CurrentRealm];
        }

        /// <summary>获取下一境界名称（已达最高则返回"飞升"）</summary>
        public static string GetNextRealmName()
        {
            if (CurrentRealm >= Realm.飞升) return "飞升";
            return RealmNames[(int)CurrentRealm + 1];
        }

        /// <summary>升级到下一境界所需修为</summary>
        public static int GetPointsToNextRealm()
        {
            if (CurrentRealm >= Realm.飞升) return 0;
            int threshold = _realmThresholds[(int)CurrentRealm + 1];
            return threshold - TotalCultivationPoints;
        }

        /// <summary>将境界加成应用到PlayerStats</summary>
        public static void ApplyBonusToStats(PlayerStats stats)
        {
            var bonus = GetRealmBonus();
            stats.maxHP += bonus.maxHP;
            stats.currentHP += bonus.maxHP;
            stats.attack += bonus.attack;
            stats.defense += bonus.defense;
            stats.critRate += bonus.critRate;
            stats.critDamage += bonus.critDamage;
            stats.moveSpeed += bonus.moveSpeed;
            stats.attackSpeed += bonus.attackSpeed;
            stats.luck += bonus.luck;
            stats.elementDamage += bonus.elementDamage;
            stats.elementDefense += bonus.elementDefense;
        }

        /// <summary>重置境界（新存档/调试用）</summary>
        public static void Reset()
        {
            CurrentRealm = Realm.练气;
            RealmProgress = 0f;
            TotalCultivationPoints = 0;
        }
    }
}
