using UnityEngine;

namespace BiopunkRV
{
    /// <summary>
    /// 火灵根 — 被动技能，挂在Player上。
    /// Lv1-10成长路线，加成作用于技能基础倍率上（传入CalculateSkillDamage之前）。
    /// 灼烧参数由EnemyController读取。
    /// </summary>
    public class FireSpiritRoot : MonoBehaviour
    {
        public static FireSpiritRoot Instance { get; private set; }

        [Header("等级")]
        public int level = 0; // 0=未获得

        [Header("灼烧参数 (EnemyController读取)")]
        public float burnChance = 0f;        // 触发概率
        public float burnDpsRatio = 0.06f;  // 灼烧DPS = 攻击力×此比例
        public float burnDuration = 3f;      // 持续时间
        public int burnMaxStacks = 1;        // 最大叠加层数
        public float burnDamageMult = 1f;    // 灼烧伤害倍率
        public float burnBonusDamage = 0f;   // 灼烧目标受到额外伤害比例
        public bool burnSpread = false;      // 击杀后传播
        public bool burnExplode = false;     // 灼烧敌人死亡爆炸
        public float explodeDamageRatio = 0f; // 爆炸伤害=攻击力×此比例
        public float explodeRadius = 2f;     // 爆炸范围
        public float explodeRadiusMult = 1f; // 爆炸范围倍率

        [Header("火属性伤害加成 (累积)")]
        public float fireDamageBonus = 0f;   // 额外倍率

        [Header("火属性范围加成 (累积)")]
        public float fireRangeBonus = 0f;

        [Header("解锁技能")]
        public bool unlockYangyanLun = false; // Lv5
        public bool unlockFentian = false;    // Lv10

        private void Awake()
        {
            Instance = this;
        }

        public void LevelUp()
        {
            level++;
            ApplyLevelEffects();
        }

        public static bool IsActive => Instance != null && Instance.level > 0;

        /// <summary>
        /// 获取技能的有效属性。
        /// 有火灵根时，无属性(Element.None)技能变为火属性。
        /// 已有属性的技能保持不变。
        /// </summary>
        public static Element GetEffectiveElement(Element baseElement)
        {
            if (IsActive && baseElement == Element.None) return Element.Fire;
            return baseElement;
        }

        /// <summary>
        /// 获取火属性技能的额外伤害倍率。
        /// 非火属性或无火灵根时返回1。
        /// </summary>
        public static float GetFireDamageMult(Element element)
        {
            if (!IsActive || element != Element.Fire) return 1f;
            return 1f + Instance.fireDamageBonus;
        }

        /// <summary>
        /// 获取火属性技能的额外范围倍率。
        /// </summary>
        public static float GetFireRangeMult(Element element)
        {
            if (!IsActive || element != Element.Fire) return 1f;
            return 1f + Instance.fireRangeBonus;
        }

        /// <summary>
        /// 灼烧触发判定
        /// </summary>
        public static bool ShouldBurn(Element attackElement, Element enemyElement)
        {
            if (!IsActive) return false;
            if (attackElement != Element.Fire || enemyElement == Element.Fire) return false;
            return Random.value < Instance.burnChance;
        }

        private void ApplyLevelEffects()
        {
            switch (level)
            {
                case 1: // 火种初生 — 灵球术变为火球术
                    burnChance = 0.2f;
                    burnDpsRatio = 0.06f;
                    burnDuration = 3f;
                    burnMaxStacks = 1;
                    // 禁用灵球术，火球术接管
                    var orb = GetComponent<SpiritOrbSpell>();
                    if (orb != null) orb.enabled = false;
                    break;
                case 2: // 炎息
                    fireDamageBonus += 0.15f;
                    burnDamageMult = 1.2f;
                    break;
                case 3: // 火炼
                    burnMaxStacks = 2;
                    break;
                case 4: // 炎流
                    fireRangeBonus += 0.2f;
                    burnDuration += 1f;
                    break;
                case 5: // 爆炎
                    burnExplode = true;
                    explodeDamageRatio = 0.5f;
                    explodeRadius = 2f;
                    unlockYangyanLun = true;
                    break;
                case 6: // 赤焰之体
                    fireDamageBonus += 0.3f;
                    break;
                case 7: // 焚心
                    burnDamageMult *= 1.5f;
                    burnBonusDamage = 0.2f;
                    break;
                case 8: // 炎域
                    fireRangeBonus += 0.3f;
                    burnSpread = true;
                    break;
                case 9: // 真火
                    burnChance = 1f;
                    burnMaxStacks = 5;
                    fireDamageBonus += 0.5f;
                    break;
                case 10: // 焚天灵火
                    fireDamageBonus += 0.8f;
                    burnDamageMult *= 2f;
                    explodeRadiusMult = 1.5f;
                    unlockFentian = true;
                    break;
            }
        }
    }
}
