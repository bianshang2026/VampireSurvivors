using UnityEngine;

namespace BiopunkRV
{
    /// <summary>
    /// 土灵根 — 被动技能，挂在Player上。
    /// Lv0=未获得，通过LevelUpSystem升级选项"厚土"获得。
    /// 土灵根效果：击退/晕眩/护盾/减伤。
    /// 灵球→石弹(直线+击退+晕眩)。
    /// </summary>
    public class EarthSpiritRoot : MonoBehaviour
    {
        public static EarthSpiritRoot Instance { get; private set; }

        [Header("等级")]
        public int level = 0;

        [Header("击退参数 (EnemyController读取)")]
        public float knockbackChance = 0f;      // 击退触发概率
        public float knockbackForce = 2f;        // 击退力度

        [Header("晕眩参数 (EnemyController读取)")]
        [Range(0f, 1f)] public float stunChance = 0f;    // 晕眩触发概率
        public float stunDuration = 0.5f;                  // 晕眩持续时间

        [Header("护盾 (Lv3+)")]
        public float shieldMax = 0f;            // 护盾最大值
        public float shieldCurrent = 0f;        // 当前护盾
        public float shieldRegenPerSec = 0f;    // 每秒护盾回复

        [Header("减伤 (Lv7+)")]
        public float damageReduction = 0f;      // 受到伤害减免比例

        [Header("土属性伤害加成 (累积)")]
        public float earthDamageBonus = 0f;

        [Header("土属性范围加成 (累积)")]
        public float earthRangeBonus = 0f;

        [Header("终极 (Lv10)")]
        public bool ultimateUnlocked = false;

        private float _shieldRegenTimer;

        private void Awake()
        {
            Instance = this;
        }

        private void Update()
        {
            // 护盾回复
            if (shieldRegenPerSec > 0f && shieldCurrent < shieldMax)
            {
                shieldCurrent = Mathf.Min(shieldMax, shieldCurrent + shieldRegenPerSec * Time.deltaTime);
            }

            // Lv10 不灭山河 — 全屏减伤已通过 damageReduction 字段实现
        }

        public void LevelUp()
        {
            level++;
            ApplyLevelEffects();
        }

        public static bool IsActive => Instance != null && Instance.level > 0;

        /// <summary>
        /// 获取技能的有效属性。
        /// 有土灵根时，无属性(Element.None)技能变为土属性。
        /// </summary>
        public static Element GetEffectiveElement(Element baseElement)
        {
            if (IsActive && baseElement == Element.None) return Element.Earth;
            return baseElement;
        }

        public static float GetEarthDamageMult(Element element)
        {
            if (!IsActive || element != Element.Earth) return 1f;
            return 1f + Instance.earthDamageBonus;
        }

        public static float GetEarthRangeMult(Element element)
        {
            if (!IsActive || element != Element.Earth) return 1f;
            return 1f + Instance.earthRangeBonus;
        }

        /// <summary>
        /// 击退触发判定
        /// </summary>
        public static bool ShouldKnockback(Element attackElement)
        {
            if (!IsActive) return false;
            if (attackElement != Element.Earth) return false;
            return Random.value < Instance.knockbackChance;
        }

        /// <summary>
        /// 晕眩触发判定
        /// </summary>
        public static bool ShouldStun(Element attackElement)
        {
            if (!IsActive) return false;
            if (attackElement != Element.Earth) return false;
            return Random.value < Instance.stunChance;
        }

        /// <summary>
        /// 护盾吸收伤害，返回穿透后的伤害
        /// </summary>
        public float AbsorbDamage(float incomingDamage)
        {
            if (shieldCurrent <= 0f) return incomingDamage;

            float absorbed = Mathf.Min(shieldCurrent, incomingDamage);
            shieldCurrent -= absorbed;
            return incomingDamage - absorbed;
        }

        private void ApplyLevelEffects()
        {
            switch (level)
            {
                case 1: // 厚土 — 灵球术变为石弹
                    knockbackChance = 0.1f;
                    knockbackForce = 2f;
                    stunChance = 0.05f;
                    stunDuration = 0.5f;
                    break;
                case 2: // 岩甲
                    earthDamageBonus += 0.15f;
                    break;
                case 3: // 岩壁 — 护盾
                    shieldMax = 20f;
                    shieldCurrent = 20f;
                    shieldRegenPerSec = 2f;
                    break;
                case 4: // 地裂
                    earthRangeBonus += 0.2f;
                    stunDuration += 0.5f;
                    break;
                case 5: // 山岳之盾 — 护盾强化
                    shieldMax += 30f;
                    shieldCurrent = shieldMax;
                    shieldRegenPerSec += 3f;
                    break;
                case 6: // 土灵精通
                    earthDamageBonus += 0.3f;
                    break;
                case 7: // 反震 — 减伤
                    damageReduction = 0.15f;
                    break;
                case 8: // 地裂扩展
                    earthRangeBonus += 0.3f;
                    knockbackForce *= 1.5f;
                    break;
                case 9: // 大地之怒
                    stunChance = 1f;
                    earthDamageBonus += 0.5f;
                    break;
                case 10: // 不灭山河
                    ultimateUnlocked = true;
                    damageReduction = 0.3f;
                    shieldMax += 100f;
                    shieldCurrent = shieldMax;
                    shieldRegenPerSec += 10f;
                    earthDamageBonus += 0.5f;
                    break;
            }
        }
    }
}
