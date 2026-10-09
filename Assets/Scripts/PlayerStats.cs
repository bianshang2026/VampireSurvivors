using UnityEngine;

namespace BiopunkRV
{
    /// <summary>
    /// 玩家基础属性系统。
    /// 攻击: 每点加50%自身数值作为额外伤害，小数向上取整。
    /// 防御: 每点减10%自身数值作为伤害减免。
    /// 属性伤害/防御: 同上规则，但仅对有属性的攻击/伤害生效。
    /// 闪避: 成功则免疫本次伤害。
    /// 幸运: 影响升级选项品质（凡/灵/仙/神）。
    /// </summary>
    [System.Serializable]
    public class PlayerStats
    {
        [Header("生命与防御")]
        public float maxHP = 100f;
        public float currentHP = 100f;
        public float defense = 40f;
        public float attack = 40f;        // 玩家攻击力，公式: attack × 0.15 = 基础攻击伤害

        [Header("暴击")]
        [Range(0f, 1f)] public float critRate = 0.05f;
        public float critDamage = 0.5f;

        [Header("闪避")]
        [Range(0f, 1f)] public float dodgeRate = 0.02f;

        [Header("攻击速度")]
        public float attackSpeed = 5f;

        [Header("移动与范围")]
        public float moveSpeed = 60f;
        public float attackRange = 20f;    // 玩家攻击范围，影响技能检测范围
        public float pickupRange = 100f;

        [Header("幸运")]
        public float luck = 0f;

        [Header("属性伤害与防御")]
        public float elementDamage = 10f;
        public float elementDefense = 10f;

        /// <summary>
        /// 属性伤害加成 = ceil(elementDamage * 0.5)，仅对有属性攻击生效
        /// </summary>
        public int GetElementDamageBonus()
        {
            return Mathf.CeilToInt(elementDamage * 0.5f);
        }

        /// <summary>
        /// 攻速缩减 = attackSpeed * 0.03 * 0.2
        /// 实际冷却 = 基础冷却 - 攻速缩减
        /// </summary>
        public float GetScaledInterval(float baseInterval)
        {
            return Mathf.Max(0.05f, baseInterval - attackSpeed * 0.03f * 0.2f);
        }

        /// <summary>
        /// 防御减免 = defense * 0.1
        /// </summary>
        public float GetDefenseReduction()
        {
            return defense * 0.1f;
        }

        /// <summary>
        /// 属性防御减免 = elementDefense * 0.1
        /// </summary>
        public float GetElementDefenseReduction()
        {
            return elementDefense * 0.1f;
        }

        /// <summary>
        /// 基础攻击伤害 = attack × 0.15
        /// </summary>
        public float GetBaseAttackDamage()
        {
            return attack * 0.15f;
        }

        /// <summary>
        /// 计算技能最终伤害。
        /// 公式: (attack × 0.15) × skillMultiplier × (1 + 暴击伤害) 如果暴击
        /// 结果向上取整，最低为1。
        /// </summary>
        public int CalculateSkillDamage(float skillMultiplier, bool isElemental, out bool isCrit)
        {
            float dmg = GetBaseAttackDamage() * skillMultiplier;

            if (isElemental)
                dmg += GetElementDamageBonus();

            isCrit = Random.value < critRate;
            if (isCrit)
                dmg *= (1f + critDamage);

            return Mathf.Max(1, Mathf.CeilToInt(dmg));
        }

        /// <summary>
        /// 计算技能实际攻击范围。
        /// 公式: skillBaseRange + (attackRange × 0.2 × 0.1)
        /// </summary>
        public float CalculateSkillRange(float skillBaseRange)
        {
            return skillBaseRange + attackRange * 0.2f * 0.1f;
        }

        /// <summary>
        /// 暴击判定
        /// </summary>
        public bool RollCrit() => Random.value < critRate;

        /// <summary>
        /// 闪避判定 — 成功则免疫本次伤害
        /// </summary>
        public bool RollDodge() => Random.value < dodgeRate;

        /// <summary>
        /// 治疗 — 草融合命中回血等系统调用
        /// </summary>
        public void Heal(int amount)
        {
            if (amount <= 0) return;
            currentHP = Mathf.Min(maxHP, currentHP + amount);
        }

        /// <summary>
        /// 受到伤害：先扣防御减免，再扣血。属性伤害额外扣属性防御。
        /// 结果向上取整，最低为1。
        /// </summary>
        public int TakeDamage(float rawDamage, bool isElemental = false)
        {
            if (RollDodge()) return 0;

            float reduction = GetDefenseReduction();
            if (isElemental)
                reduction += GetElementDefenseReduction();

            int finalDamage = Mathf.Max(1, Mathf.CeilToInt(rawDamage - reduction));
            currentHP -= finalDamage;
            return finalDamage;
        }
    }

    /// <summary>
    /// 升级选项品质分级 — 受幸运值影响。
    /// </summary>
    public enum UpgradeRarity
    {
        Common,    // 凡品 — 白色
        Spirit,    // 灵品 — 蓝色
        Immortal,  // 仙品 — 金色
        Divine     // 神品 — 红色
    }

    public static class RaritySystem
    {
        public static readonly Color ColorCommon = new Color(0.9f, 0.9f, 0.9f, 1f);   // 白色
        public static readonly Color ColorSpirit = new Color(0.3f, 0.5f, 0.95f, 1f);   // 蓝色
        public static readonly Color ColorImmortal = new Color(0.95f, 0.8f, 0.2f, 1f); // 金色
        public static readonly Color ColorDivine = new Color(0.9f, 0.2f, 0.15f, 1f);  // 红色

        /// <summary>
        /// 根据幸运值随机品质。
        /// 幸运值越高，高品质概率越大。
        /// </summary>
        public static UpgradeRarity RollRarity(float luck)
        {
            float roll = Random.value;

            // 幸运值影响各品质概率
            float divineChance = 0.02f + luck * 0.005f;   // 基础2% + 每点幸运0.5%
            float immortalChance = 0.08f + luck * 0.01f;   // 基础8% + 每点幸运1%
            float spiritChance = 0.25f + luck * 0.015f;    // 基础25% + 每点幸运1.5%
            // 剩余为凡品

            if (roll < divineChance) return UpgradeRarity.Divine;
            if (roll < divineChance + immortalChance) return UpgradeRarity.Immortal;
            if (roll < divineChance + immortalChance + spiritChance) return UpgradeRarity.Spirit;
            return UpgradeRarity.Common;
        }

        /// <summary>
        /// 品质对应颜色
        /// </summary>
        public static Color GetColor(UpgradeRarity rarity)
        {
            switch (rarity)
            {
                case UpgradeRarity.Divine: return ColorDivine;
                case UpgradeRarity.Immortal: return ColorImmortal;
                case UpgradeRarity.Spirit: return ColorSpirit;
                default: return ColorCommon;
            }
        }

        /// <summary>
        /// 品质对应中文名
        /// </summary>
        public static string GetName(UpgradeRarity rarity)
        {
            switch (rarity)
            {
                case UpgradeRarity.Divine: return "神品";
                case UpgradeRarity.Immortal: return "仙品";
                case UpgradeRarity.Spirit: return "灵品";
                default: return "凡品";
            }
        }

        /// <summary>
        /// 品质对应数值倍率（影响升级数值大小）
        /// </summary>
        public static float GetMultiplier(UpgradeRarity rarity)
        {
            switch (rarity)
            {
                case UpgradeRarity.Divine: return 3f;
                case UpgradeRarity.Immortal: return 2f;
                case UpgradeRarity.Spirit: return 1.5f;
                default: return 1f;
            }
        }
    }
}
