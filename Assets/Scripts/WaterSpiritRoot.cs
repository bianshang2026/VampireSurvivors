using UnityEngine;

namespace BiopunkRV
{
    /// <summary>
    /// 水灵根 — 被动技能，挂在Player上。
    /// Lv0=未获得，通过LevelUpSystem升级选项"寒泉"获得。
    /// 水灵根的效果是水(减速/水波)，不是冰(冻结)。冰是水+风融合后的产物。
    /// 减速参数由EnemyController读取。
    /// </summary>
    public class WaterSpiritRoot : MonoBehaviour
    {
        public static WaterSpiritRoot Instance { get; private set; }

        [Header("等级")]
        public int level = 0;

        [Header("减速参数 (EnemyController读取)")]
        [Range(0f, 1f)] public float slowChance = 0f;       // 触发概率
        public float slowFactor = 0.5f;                      // 移速倍率 (0.5=减速50%)
        public float slowDuration = 2f;                     // 持续时间
        public bool canStack = false;                        // 减速可叠加
        public float maxSlowFactor = 0.3f;                   // 最高叠加到30%移速
        public float slowBonusDamage = 0f;                   // 减速目标受到额外伤害比例

        [Header("水波扩散 (Lv5)")]
        public bool canRipple = false;
        public float rippleRadius = 2.4f;
        public float rippleDamageMult = 0.3f;

        [Header("水属性伤害加成 (累积)")]
        public float waterDamageBonus = 0f;

        [Header("水属性范围加成 (累积)")]
        public float waterRangeBonus = 0f;

        [Header("终极 (Lv10)")]
        public bool ultimateUnlocked = false;
        public float ultimateSlowRadius = 999f;     // 全屏
        public float ultimateSlowFactor = 0.7f;      // 30%减速
        public float ultimateRippleInterval = 5f;    // 每5秒水波爆发

        private float _ultimateTimer;

        private void Awake()
        {
            Instance = this;
        }

        private void Update()
        {
            // Lv10 沧海领域 — 全屏减速 + 定期水波爆发
            if (ultimateUnlocked)
            {
                _ultimateTimer += Time.deltaTime;

                // 持续全屏减速
                var enemies = FindObjectsOfType<EnemyController>();
                foreach (var enemy in enemies)
                {
                    if (enemy == null || !enemy.gameObject.activeInHierarchy) continue;
                    enemy.ApplySlow(ultimateSlowFactor, 1f);
                }
                var rabbits = FindObjectsOfType<GhostRabbitController>();
                foreach (var rabbit in rabbits)
                {
                    if (rabbit == null || !rabbit.gameObject.activeInHierarchy) continue;
                    rabbit.ApplySlow(ultimateSlowFactor, 1f);
                }

                // 每5秒水波爆发
                if (_ultimateTimer >= ultimateRippleInterval)
                {
                    _ultimateTimer = 0f;
                    UltimateRippleBurst();
                }
            }
        }

        public void LevelUp()
        {
            level++;
            ApplyLevelEffects();
        }

        public static bool IsActive => Instance != null && Instance.level > 0;

        /// <summary>
        /// 获取技能的有效属性。
        /// 有水灵根时，无属性(Element.None)技能变为水属性。
        /// 已有属性的技能保持不变。
        /// </summary>
        public static Element GetEffectiveElement(Element baseElement)
        {
            if (IsActive && baseElement == Element.None) return Element.Water;
            return baseElement;
        }

        /// <summary>
        /// 获取水属性技能的额外伤害倍率。
        /// </summary>
        public static float GetWaterDamageMult(Element element)
        {
            if (!IsActive || element != Element.Water) return 1f;
            return 1f + Instance.waterDamageBonus;
        }

        /// <summary>
        /// 获取水属性技能的额外范围倍率。
        /// </summary>
        public static float GetWaterRangeMult(Element element)
        {
            if (!IsActive || element != Element.Water) return 1f;
            return 1f + Instance.waterRangeBonus;
        }

        /// <summary>
        /// 减速触发判定
        /// </summary>
        public static bool ShouldSlow(Element attackElement)
        {
            if (!IsActive) return false;
            if (attackElement != Element.Water) return false;
            return Random.value < Instance.slowChance;
        }

        /// <summary>
        /// 获取实际减速倍率（考虑叠加）
        /// </summary>
        public float GetEffectiveSlowFactor()
        {
            return canStack ? maxSlowFactor : slowFactor;
        }

        private void ApplyLevelEffects()
        {
            switch (level)
            {
                case 1: // 寒泉 — 灵球术变为水弹术
                    slowChance = 0.2f;
                    slowFactor = 0.5f;
                    slowDuration = 2f;
                    // 水灵根不需要禁用灵球术，GetEffectiveElement会将None→Water
                    // 如果火灵根已激活（灵球已禁用+火球已启用），不覆盖
                    break;
                case 2: // 水脉
                    waterDamageBonus += 0.15f;
                    slowChance += 0.1f;
                    break;
                case 3: // 水心
                    canStack = true;
                    break;
                case 4: // 水域
                    waterRangeBonus += 0.2f;
                    slowDuration += 1f;
                    break;
                case 5: // 水波
                    canRipple = true;
                    break;
                case 6: // 水灵精通
                    waterDamageBonus += 0.3f;
                    break;
                case 7: // 深水
                    slowBonusDamage = 0.2f;
                    break;
                case 8: // 水域扩展
                    waterRangeBonus += 0.3f;
                    rippleRadius *= 1.5f;
                    break;
                case 9: // 水帝
                    slowChance = 1f;
                    waterDamageBonus += 0.5f;
                    break;
                case 10: // 沧海领域
                    ultimateUnlocked = true;
                    waterDamageBonus += 0.5f;
                    break;
            }
        }

        private void UltimateRippleBurst()
        {
            // 全屏水波爆发：对所有敌人造成30%伤害
            var psc = PlayerStatsComponent.Instance;
            float baseDmg = psc != null ? psc.stats.GetBaseAttackDamage() : 6f;
            int rippleDmg = Mathf.Max(1, Mathf.CeilToInt(baseDmg * rippleDamageMult));

            var enemies = FindObjectsOfType<EnemyController>();
            foreach (var enemy in enemies)
            {
                if (enemy == null || !enemy.gameObject.activeInHierarchy) continue;
                enemy.TakeDamage(rippleDmg, Element.Water);
                DamageNumber.CreateAt(enemy.transform.position, rippleDmg, Element.Water);
            }

            var rabbits = FindObjectsOfType<GhostRabbitController>();
            foreach (var rabbit in rabbits)
            {
                if (rabbit == null || !rabbit.gameObject.activeInHierarchy) continue;
                rabbit.TakeDamage(rippleDmg, Element.Water);
                DamageNumber.CreateAt(rabbit.transform.position, rippleDmg, Element.Water);
            }
        }
    }
}
