using UnityEngine;

namespace BiopunkRV
{
    [CreateAssetMenu(fileName = "PlayerStatsData", menuName = "修仙游戏/玩家属性", order = 0)]
    public class PlayerStatsLibrary : ScriptableObject
    {
        [Header("=== 生命与防御 ===")]
        [Tooltip("最大生命值")]
        public float maxHP = 100f;
        [Tooltip("防御: 每点减0.1伤害 (防御减免 = defense × 0.1)")]
        public float defense = 20f;
        [Tooltip("元素防御: 同防御公式，只对与自身属性相同的攻击生效")]
        public float elementDefense = 10f;

        [Header("=== 攻击 ===")]
        [Tooltip("攻击力: 基础攻击伤害 = attack × 0.15")]
        public float attack = 40f;
        [Tooltip("攻速: 实际冷却 = 基础冷却 - (attackSpeed × 0.03 × 0.2)")]
        public float attackSpeed = 5f;

        [Header("=== 暴击 ===")]
        [Range(0f, 1f)]
        [Tooltip("暴击率 (0-1)")]
        public float critRate = 0.05f;
        [Tooltip("暴击伤害加成 (0.5 = +50%)")]
        public float critDamage = 0.5f;

        [Header("=== 闪避 ===")]
        [Range(0f, 1f)]
        [Tooltip("闪避率 (0-1)")]
        public float dodgeRate = 0.02f;

        [Header("=== 移动与范围 ===")]
        [Tooltip("移动速度")]
        public float moveSpeed = 60f;
        [Tooltip("攻击范围: 影响技能范围 (skillRange = baseRange + attackRange × 0.02)")]
        public float attackRange = 200f;
        [Tooltip("拾取范围: 灵珠吸附半径")]
        public float pickupRange = 100f;

        [Header("=== 幸运 ===")]
        [Tooltip("幸运值: 影响掉落品质概率")]
        public float luck = 0f;

        [Header("=== 属性伤害 ===")]
        [Tooltip("属性伤害加成: 仅对有属性的攻击生效 (最终伤害 += elementDamage × 0.1)")]
        public float elementDamage = 10f;

        [Header("=== 计算公式说明 ===")]
        [TextArea(3, 10)]
        public string formulas = 
            "【基础攻击伤害】 = attack × 0.15\n" +
            "【技能伤害】 = (attack × 0.15) × skillMultiplier × upgradeMult × (1 + critDamage if暴击) + (elementDamage × 0.1 if有属性)\n" +
            "【技能范围】 = skillBaseRange + (attackRange × 0.02)\n" +
            "【实际冷却】 = 基础冷却 - (attackSpeed × 0.03 × 0.2)\n" +
            "【防御减免】 = defense × 0.1 (减固定值)\n" +
            "【元素防御减免】 = elementDefense × 0.1 (只对同属性攻击生效)\n" +
            "【最终伤害】 = max(1, 技能伤害 - 防御减免 - 元素防御减免)\n" +
            "【暴击判定】 Random.value < critRate\n" +
            "【闪避判定】 Random.value < dodgeRate\n" +
            "\n" +
            "【升级公式】 maxExp = level² × 100\n" +
            "【经验球】 Basic=20, Medium=200, Advanced=800";

        [Header("=== 升级公式 ===")]
        [Tooltip("升级所需经验 = level² × expBase")]
        public float expBase = 100f;
        [Tooltip("基础经验球经验值")]
        public float basicOrbExp = 20f;
        [Tooltip("中级经验球经验值")]
        public float mediumOrbExp = 200f;
        [Tooltip("高级经验球经验值")]
        public float advancedOrbExp = 800f;

        /// <summary>
        /// 应用数据到PlayerStats
        /// </summary>
        public void ApplyTo(PlayerStats stats)
        {
            stats.maxHP = maxHP;
            stats.currentHP = maxHP;
            stats.defense = defense;
            stats.elementDefense = elementDefense;
            stats.attack = attack;
            stats.attackSpeed = attackSpeed;
            stats.critRate = critRate;
            stats.critDamage = critDamage;
            stats.dodgeRate = dodgeRate;
            stats.moveSpeed = moveSpeed;
            stats.attackRange = attackRange;
            stats.pickupRange = pickupRange;
            stats.luck = luck;
            stats.elementDamage = elementDamage;
        }

        // === 计算公式方法 ===

        /// <summary>
        /// 基础攻击伤害 = attack × 0.15
        /// </summary>
        public float GetBaseAttackDamage()
        {
            return attack * 0.15f;
        }

        /// <summary>
        /// 防御减免 = defense × 0.1
        /// </summary>
        public float GetDefenseReduction()
        {
            return defense * 0.1f;
        }

        /// <summary>
        /// 元素防御减免 = elementDefense × 0.1
        /// </summary>
        public float GetElementDefenseReduction()
        {
            return elementDefense * 0.1f;
        }

        /// <summary>
        /// 技能最终伤害 = (attack×0.15) × skillMultiplier × upgradeMult × (1+critDamage if暴击) + (elementDamage×0.1 if有属性)
        /// </summary>
        public int CalculateSkillDamage(float skillMultiplier, float upgradeMult, bool isElemental, out bool isCrit)
        {
            isCrit = Random.value < critRate;
            float dmg = GetBaseAttackDamage() * skillMultiplier * upgradeMult;

            if (isElemental)
                dmg += elementDamage * 0.1f;

            if (isCrit)
                dmg *= (1f + critDamage);

            return Mathf.Max(1, Mathf.CeilToInt(dmg));
        }

        /// <summary>
        /// 技能范围 = skillBaseRange + (attackRange × 0.02)
        /// </summary>
        public float CalculateSkillRange(float skillBaseRange)
        {
            return skillBaseRange + attackRange * 0.02f;
        }

        /// <summary>
        /// 实际冷却 = baseCooldown - (attackSpeed × 0.03 × 0.2)
        /// </summary>
        public float CalculateCooldown(float baseCooldown)
        {
            return Mathf.Max(0.1f, baseCooldown - (attackSpeed * 0.03f * 0.2f));
        }

        /// <summary>
        /// 升级所需经验 = level² × expBase
        /// </summary>
        public float GetMaxExpForLevel(int level)
        {
            return level * level * expBase;
        }

        /// <summary>
        /// 受到伤害: max(1, damage - defense×0.1 - elementDef×0.1 if同属性)
        /// </summary>
        public int CalculateReceivedDamage(float damage, bool isElemental)
        {
            float reduction = GetDefenseReduction();
            if (isElemental)
                reduction += GetElementDefenseReduction();
            return Mathf.Max(1, Mathf.CeilToInt(damage - reduction));
        }

        /// <summary>
        /// 暴击判定
        /// </summary>
        public bool RollCrit() => Random.value < critRate;

        /// <summary>
        /// 闪避判定
        /// </summary>
        public bool RollDodge() => Random.value < dodgeRate;

        /// <summary>
        /// 按路径加载玩家属性 asset。
        /// </summary>
        public static PlayerStatsLibrary LoadByName()
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<PlayerStatsLibrary>("Assets/Data/Player/PlayerStats.asset");
#else
            return Resources.Load<PlayerStatsLibrary>("PlayerStats");
#endif
        }
    }
}
