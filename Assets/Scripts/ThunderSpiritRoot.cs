using UnityEngine;

namespace BiopunkRV
{
    /// <summary>
    /// 雷灵根 — 被动技能，挂在Player上。
    /// Lv0=未获得，通过LevelUpSystem升级选项"惊雷"获得。
    /// 雷灵根效果：暴击/连锁/麻痹。不参与属性融合。
    /// 灵球→雷球(直线+连锁1个60%伤害)。
    /// </summary>
    public class ThunderSpiritRoot : MonoBehaviour
    {
        public static ThunderSpiritRoot Instance { get; private set; }

        [Header("等级")]
        public int level = 0;

        [Header("连锁参数 (技能脚本读取)")]
        public int chainCount = 0;             // 连锁次数
        public float chainDamageMult = 0.6f;    // 连锁伤害倍率

        [Header("暴击参数 (PlayerStatsComponent读取)")]
        public float bonusCritRate = 0f;        // 额外暴击率
        public float bonusCritDamage = 0f;      // 额外暴击伤害

        [Header("麻痹参数 (EnemyController读取)")]
        [Range(0f, 1f)] public float paralyzeChance = 0f;  // 麻痹触发概率
        public float paralyzeDuration = 1f;                   // 麻痹持续时间

        [Header("雷属性伤害加成 (累积)")]
        public float thunderDamageBonus = 0f;

        [Header("雷属性范围加成 (累积)")]
        public float thunderRangeBonus = 0f;

        [Header("终极 (Lv10)")]
        public bool ultimateUnlocked = false;
        public float ultimateStrikeInterval = 4f; // 每4秒全屏雷击

        private float _ultimateTimer;

        private void Awake()
        {
            Instance = this;
        }

        private void Update()
        {
            if (ultimateUnlocked)
            {
                _ultimateTimer += Time.deltaTime;
                if (_ultimateTimer >= ultimateStrikeInterval)
                {
                    _ultimateTimer = 0f;
                    UltimateThunderStrike();
                }
            }
        }

        public void LevelUp()
        {
            level++;
            ApplyLevelEffects();
        }

        public static bool IsActive => Instance != null && Instance.level > 0;

        public static Element GetEffectiveElement(Element baseElement)
        {
            if (IsActive && baseElement == Element.None) return Element.Thunder;
            return baseElement;
        }

        public static float GetThunderDamageMult(Element element)
        {
            if (!IsActive || element != Element.Thunder) return 1f;
            return 1f + Instance.thunderDamageBonus;
        }

        public static float GetThunderRangeMult(Element element)
        {
            if (!IsActive || element != Element.Thunder) return 1f;
            return 1f + Instance.thunderRangeBonus;
        }

        /// <summary>
        /// 获取额外暴击率
        /// </summary>
        public static float GetBonusCritRate()
        {
            if (!IsActive) return 0f;
            return Instance.bonusCritRate;
        }

        /// <summary>
        /// 麻痹触发判定
        /// </summary>
        public static bool ShouldParalyze(Element attackElement)
        {
            if (!IsActive) return false;
            if (attackElement != Element.Thunder) return false;
            return Random.value < Instance.paralyzeChance;
        }

        private void ApplyLevelEffects()
        {
            switch (level)
            {
                case 1: // 惊雷 — 灵球术变为雷球
                    chainCount = 1;
                    chainDamageMult = 0.6f;
                    break;
                case 2: // 雷光
                    bonusCritRate += 0.05f;
                    break;
                case 3: // 连锁强化
                    chainCount += 1;
                    break;
                case 4: // 雷域
                    thunderRangeBonus += 0.2f;
                    break;
                case 5: // 雷霆万钧
                    bonusCritRate += 0.15f;
                    chainDamageMult = 0.8f;
                    break;
                case 6: // 雷灵精通
                    thunderDamageBonus += 0.3f;
                    break;
                case 7: // 麻痹
                    paralyzeChance = 0.05f;
                    paralyzeDuration = 1f;
                    break;
                case 8: // 雷域扩展
                    thunderRangeBonus += 0.3f;
                    chainCount += 1;
                    break;
                case 9: // 雷帝
                    bonusCritRate += 0.15f;
                    bonusCritDamage += 0.3f;
                    chainCount += 2;
                    break;
                case 10: // 九霄雷狱
                    ultimateUnlocked = true;
                    thunderDamageBonus += 0.5f;
                    paralyzeChance = 0.15f;
                    break;
            }
        }

        private void UltimateThunderStrike()
        {
            // 全屏雷击：对所有敌人造成雷属性伤害
            var psc = PlayerStatsComponent.Instance;
            float baseDmg = psc != null ? psc.stats.GetBaseAttackDamage() : 6f;
            int strikeDmg = Mathf.Max(1, Mathf.CeilToInt(baseDmg * 0.6f));

            var enemies = FindObjectsOfType<EnemyController>();
            foreach (var enemy in enemies)
            {
                if (enemy == null || !enemy.gameObject.activeInHierarchy) continue;
                enemy.TakeDamage(strikeDmg, Element.Thunder);
                DamageNumber.CreateAt(enemy.transform.position, strikeDmg, Element.Thunder);
            }
        }
    }
}
