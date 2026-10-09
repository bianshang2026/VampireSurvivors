using UnityEngine;

namespace BiopunkRV
{
    /// <summary>
    /// 风灵根 — 被动技能，挂在Player上。
    /// Lv0=未获得，通过LevelUpSystem升级选项"疾风"获得。
    /// 风灵根效果：攻速/弹射/移速。
    /// 灵气刃→风刃(弹射+2, 速度+30%)。
    /// </summary>
    public class WindSpiritRoot : MonoBehaviour
    {
        public static WindSpiritRoot Instance { get; private set; }

        [Header("等级")]
        public int level = 0;

        [Header("弹射参数 (技能脚本读取)")]
        public int bonusBounceCount = 0;       // 额外弹射次数
        public float projectileSpeedMult = 1f;  // 投射物速度倍率

        [Header("攻速参数")]
        public float attackSpeedBonus = 0f;     // 攻速加成 (减少冷却)

        [Header("移速参数 (CharacterController2D读取)")]
        public float moveSpeedBonus = 0f;       // 移速加成比例

        [Header("额外投射物 (Lv5)")]
        public int bonusProjectiles = 0;        // 额外投射物数量

        [Header("风属性伤害加成 (累积)")]
        public float windDamageBonus = 0f;

        [Header("风属性范围加成 (累积)")]
        public float windRangeBonus = 0f;

        [Header("终极 (Lv10)")]
        public bool ultimateUnlocked = false;
        public float ultimateBladeInterval = 5f; // 每5秒全屏风刃

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
                if (_ultimateTimer >= ultimateBladeInterval)
                {
                    _ultimateTimer = 0f;
                    UltimateWindBladeBurst();
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
            if (IsActive && baseElement == Element.None) return Element.Wind;
            return baseElement;
        }

        public static float GetWindDamageMult(Element element)
        {
            if (!IsActive || element != Element.Wind) return 1f;
            return 1f + Instance.windDamageBonus;
        }

        public static float GetWindRangeMult(Element element)
        {
            if (!IsActive || element != Element.Wind) return 1f;
            return 1f + Instance.windRangeBonus;
        }

        /// <summary>
        /// 获取攻速倍率（减少冷却）
        /// </summary>
        public static float GetAttackSpeedMult()
        {
            if (!IsActive) return 1f;
            return 1f + Instance.attackSpeedBonus;
        }

        /// <summary>
        /// 获取移速加成
        /// </summary>
        public static float GetMoveSpeedMult()
        {
            if (!IsActive) return 1f;
            return 1f + Instance.moveSpeedBonus;
        }

        private void ApplyLevelEffects()
        {
            switch (level)
            {
                case 1: // 疾风 — 灵气刃变为风刃
                    bonusBounceCount = 2;
                    projectileSpeedMult = 1.3f;
                    break;
                case 2: // 风行
                    moveSpeedBonus += 0.1f;
                    break;
                case 3: // 风切
                    bonusBounceCount += 1;
                    break;
                case 4: // 风域
                    windRangeBonus += 0.2f;
                    break;
                case 5: // 风刃
                    bonusProjectiles = 1;
                    break;
                case 6: // 风灵精通
                    windDamageBonus += 0.3f;
                    break;
                case 7: // 风暴之眼
                    attackSpeedBonus += 0.3f;
                    break;
                case 8: // 风域扩展
                    windRangeBonus += 0.3f;
                    bonusBounceCount += 1;
                    break;
                case 9: // 风之化身
                    moveSpeedBonus += 0.5f;
                    attackSpeedBonus += 0.5f;
                    break;
                case 10: // 风暴大道
                    ultimateUnlocked = true;
                    windDamageBonus += 0.5f;
                    bonusProjectiles += 1;
                    break;
            }
        }

        private void UltimateWindBladeBurst()
        {
            // 全屏风刃：对所有敌人造成风属性伤害
            var psc = PlayerStatsComponent.Instance;
            float baseDmg = psc != null ? psc.stats.GetBaseAttackDamage() : 6f;
            int bladeDmg = Mathf.Max(1, Mathf.CeilToInt(baseDmg * 0.5f));

            var enemies = FindObjectsOfType<EnemyController>();
            foreach (var enemy in enemies)
            {
                if (enemy == null || !enemy.gameObject.activeInHierarchy) continue;
                enemy.TakeDamage(bladeDmg, Element.Wind);
                DamageNumber.CreateAt(enemy.transform.position, bladeDmg, Element.Wind);
            }
        }
    }
}
