using UnityEngine;

namespace BiopunkRV
{
    /// <summary>
    /// 法宝品阶
    /// </summary>
    public enum ArtifactTier
    {
        None = 0,       // 无法宝
        Common = 1,     // 普通
        Spirit = 2,     // 灵器
        Treasure = 3,   // 法宝
        Natal = 4,      // 本命法宝
        Immortal = 5    // 仙器
    }

    /// <summary>
    /// 本命法宝系统 — 普通→灵器→法宝→本命法宝→仙器。
    /// 每阶提供战斗加成，跨场景持久化（静态数据）。
    /// 升阶消耗灵石(RunInventory.spiritStones)。
    /// </summary>
    public static class ArtifactSystem
    {
        // 当前法宝信息（静态持久化）
        public static ArtifactTier CurrentTier { get; private set; } = ArtifactTier.None;
        public static string ArtifactName { get; private set; } = "";
        public static int RefineLevel { get; private set; } = 0; // 炼器等级(0~10)

        // 升阶费用（灵石）
        private static readonly int[] _upgradeCosts = { 0, 30, 80, 200, 500, 1000 };
        private static readonly int _refineCost = 5; // 每级炼器5灵石

        // 法宝名称
        public static readonly string[] TierNames = { "无", "凡器", "灵器", "法宝", "本命法宝", "仙器" };
        public static readonly string[] TierArtifactNames = {
            "",
            "青锋剑",
            "紫电灵器",
            "天罡法宝",
            "混元本命钟",
            "九霄仙剑"
        };
        public static readonly string[] TierDescriptions = {
            "未拥有法宝",
            "一柄普通兵刃，聊胜于无",
            "蕴含灵气的灵器，锋芒初显",
            "法则之力凝聚的法宝，可御敌于外",
            "与性命相系的本命法宝，生死与共",
            "超脱凡尘的仙器，蕴含仙道法则"
        };

        // ---- 法宝加成 ----
        public struct ArtifactBonus
        {
            public float attackMult;       // 攻击力倍率
            public float damageMult;       // 最终伤害倍率
            public float critRate;         // 暴击率加成
            public float critDamage;       // 暴击伤害加成
            public float cooldownMult;     // 冷却倍率(<1=更快)
            public float projectileMult;   // 投射物数量加成
            public float rangeMult;        // 范围倍率
            public float maxHP;            // 生命加成
            public float defense;          // 防御加成
        }

        /// <summary>获取当前法宝加成</summary>
        public static ArtifactBonus GetBonus()
        {
            return GetBonus(CurrentTier, RefineLevel);
        }

        /// <summary>获取指定品阶+炼器等级的加成</summary>
        public static ArtifactBonus GetBonus(ArtifactTier tier, int refineLevel)
        {
            float refineBonus = refineLevel * 0.02f; // 每级炼器+2%全属性

            switch (tier)
            {
                case ArtifactTier.None:
                    return new ArtifactBonus();
                case ArtifactTier.Common:
                    return new ArtifactBonus
                    {
                        attackMult = 1.05f + refineBonus,
                        maxHP = 10,
                        defense = 2
                    };
                case ArtifactTier.Spirit:
                    return new ArtifactBonus
                    {
                        attackMult = 1.15f + refineBonus,
                        damageMult = 0.1f + refineBonus,
                        critRate = 0.02f,
                        rangeMult = 1.05f,
                        maxHP = 30,
                        defense = 5
                    };
                case ArtifactTier.Treasure:
                    return new ArtifactBonus
                    {
                        attackMult = 1.3f + refineBonus,
                        damageMult = 0.25f + refineBonus,
                        critRate = 0.05f,
                        critDamage = 0.15f,
                        cooldownMult = 0.9f,
                        rangeMult = 1.1f,
                        maxHP = 80,
                        defense = 12
                    };
                case ArtifactTier.Natal:
                    return new ArtifactBonus
                    {
                        attackMult = 1.5f + refineBonus,
                        damageMult = 0.5f + refineBonus,
                        critRate = 0.08f,
                        critDamage = 0.3f,
                        cooldownMult = 0.8f,
                        projectileMult = 1f, // +1投射物
                        rangeMult = 1.2f,
                        maxHP = 150,
                        defense = 25
                    };
                case ArtifactTier.Immortal:
                    return new ArtifactBonus
                    {
                        attackMult = 2.0f + refineBonus,
                        damageMult = 1.0f + refineBonus,
                        critRate = 0.15f,
                        critDamage = 0.5f,
                        cooldownMult = 0.65f,
                        projectileMult = 2f, // +2投射物
                        rangeMult = 1.3f,
                        maxHP = 300,
                        defense = 50
                    };
                default:
                    return new ArtifactBonus();
            }
        }

        /// <summary>升级法宝品阶</summary>
        public static bool TryUpgradeTier()
        {
            MetaProgression.EnsureLoaded();
            if (CurrentTier >= ArtifactTier.Immortal) return false;

            int cost = _upgradeCosts[(int)CurrentTier + 1];
            if (!MetaProgression.Spend(cost)) return false;
            CurrentTier++;
            ArtifactName = TierArtifactNames[(int)CurrentTier];
            MetaProgression.Save();
            Debug.Log($"[ArtifactSystem] 法宝升阶！{TierNames[(int)CurrentTier]} - {ArtifactName}");
            return true;
        }

        /// <summary>炼器（提升精炼等级）</summary>
        public static bool TryRefine()
        {
            MetaProgression.EnsureLoaded();
            if (CurrentTier == ArtifactTier.None) return false;
            if (RefineLevel >= 10) return false;

            if (!MetaProgression.Spend(_refineCost)) return false;
            RefineLevel++;
            MetaProgression.Save();
            Debug.Log($"[ArtifactSystem] 炼器+{RefineLevel} — {ArtifactName}");
            return true;
        }

        /// <summary>将法宝加成应用到PlayerStats</summary>
        public static void ApplyBonusToStats(PlayerStats stats)
        {
            if (CurrentTier == ArtifactTier.None) return;

            var bonus = GetBonus();
            stats.attack *= bonus.attackMult;
            stats.maxHP += bonus.maxHP;
            stats.currentHP += bonus.maxHP;
            stats.defense += bonus.defense;
            stats.critRate += bonus.critRate;
            stats.critDamage += bonus.critDamage;
        }

        /// <summary>获取当前品阶名称</summary>
        public static string GetTierName()
        {
            return TierNames[(int)CurrentTier];
        }

        /// <summary>获取升阶费用（灵石）</summary>
        public static int GetUpgradeCost()
        {
            if (CurrentTier >= ArtifactTier.Immortal) return 0;
            return _upgradeCosts[(int)CurrentTier + 1];
        }

        /// <summary>重置法宝（新存档/调试用）</summary>
        public static void Reset()
        {
            CurrentTier = ArtifactTier.None;
            ArtifactName = "";
            RefineLevel = 0;
        }

        public static void Restore(int tier, int refinement)
        {
            CurrentTier = (ArtifactTier)Mathf.Clamp(tier, 0, 5);
            RefineLevel = Mathf.Clamp(refinement, 0, 10);
            ArtifactName = TierArtifactNames[(int)CurrentTier];
        }
    }
}
