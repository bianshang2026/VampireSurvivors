using UnityEngine;

namespace BiopunkRV
{
    /// <summary>
    /// 玩家属性组件 — 挂在Player上，提供全局属性访问和伤害计算。
    /// </summary>
    public class PlayerStatsComponent : MonoBehaviour
    {
        public PlayerStats stats = new PlayerStats();
        public bool godMode = false;

        public static PlayerStatsComponent Instance { get; private set; }

        private void Start()
        {
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "秘境")
                MethodRuntime.ApplySavedLoadout(gameObject);
        }

        private void Awake()
        {
            Instance = this;

            // Load player stats from asset
            var data = PlayerStatsLibrary.LoadByName();
            if (data != null)
                data.ApplyTo(stats);

            MetaProgression.EnsureLoaded();
            MetaProgression.ApplyStats(stats);

            // Apply realm bonuses (permanent, from RealmSystem)
            RealmSystem.ApplyBonusToStats(stats);

            // Apply artifact bonuses (permanent, from ArtifactSystem)
            ArtifactSystem.ApplyBonusToStats(stats);
        }

        /// <summary>
        /// 计算技能伤害（新公式: attack×0.15 × skillMultiplier）
        /// </summary>
        public int CalculateSkillDamage(float skillMultiplier, Element element)
        {
            bool isElemental = element != Element.None;
            return stats.CalculateSkillDamage(skillMultiplier, isElemental, out _);
        }

        public int CalculateSkillDamage(float skillMultiplier, Element element, out bool isCrit)
        {
            bool isElemental = element != Element.None;
            return stats.CalculateSkillDamage(skillMultiplier, isElemental, out isCrit);
        }

        /// <summary>
        /// 计算技能实际攻击范围
        /// 公式: skillBaseRange + (attackRange × 0.2 × 0.1)
        /// </summary>
        public float CalculateSkillRange(float skillBaseRange)
        {
            return stats.CalculateSkillRange(skillBaseRange);
        }

        /// <summary>
        /// 获取攻速缩减后的实际冷却
        /// </summary>
        public float GetScaledInterval(float baseInterval)
        {
            return stats.GetScaledInterval(baseInterval);
        }

        /// <summary>
        /// 暴击判定
        /// </summary>
        public bool IsCrit() => stats.RollCrit();

        /// <summary>
        /// 闪避判定
        /// </summary>
        public bool IsDodged() => stats.RollDodge();

        /// <summary>
        /// 获取攻击范围（用于技能检测距离）
        /// </summary>
        public float GetAttackRange() => stats.attackRange;

        /// <summary>
        /// 获取拾取范围
        /// </summary>
        public float GetPickupRange() => stats.pickupRange;
    }
}
