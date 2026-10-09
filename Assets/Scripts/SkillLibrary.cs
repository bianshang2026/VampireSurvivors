using UnityEngine;

namespace BiopunkRV
{
    [CreateAssetMenu(fileName = "SkillData", menuName = "修仙游戏/技能数据", order = 2)]
    public class SkillLibrary : ScriptableObject
    {
        [Header("基础")]
        public string skillName = "技能";
        public SkillType skillType;
        public Element element = Element.None;
        
        [Header("伤害")]
        public float damageMultiplier = 1.0f;  // 伤害倍率
        public float baseRange = 3f;           // 基础攻击范围
        
        [Header("冷却与频率")]
        public float cooldown = 2f;            // 施放间隔
        public float attackSpeed = 5f;         // 攻击速度(影响冷却)
        
        [Header("投射物")]
        public float projectileSpeed = 8f;     // 投射物速度
        public float projectileLifetime = 3f;  // 投射物存活时间
        public float projectileScale = 0.15f;  // 投射物大小
        
        [Header("范围效果")]
        public float damageRadius = 1.2f;      // 命中判定范围
        public int maxProjectiles = 3;         // 同时存在的最大数量
        
        [Header("升级")]
        public int maxLevel = 10;
        public float upgradeDamageMult = 1.3f; // 每级伤害倍率
        public float upgradeCooldownMult = 0.9f; // 每级冷却缩减
        
        [Header("视觉")]
        public Sprite iconSprite;              // 技能图标
        public Sprite projectileSprite;       // 投射物贴图
        
        public enum SkillType
        {
            FlyingSword,    // 御剑术
            FireballSpell,  // 火球术
            SpiritOrbSpell, // 灵球术
            SpiritArray,    // 灵力阵
            Custom
        }
        
        /// <summary>
        /// 计算指定等级的伤害倍率
        /// </summary>
        public float GetDamageMultiplierAtLevel(int level)
        {
            float mult = damageMultiplier;
            for (int i = 1; i < level; i++)
                mult *= upgradeDamageMult;
            return mult;
        }
        
        /// <summary>
        /// 计算指定等级的冷却时间
        /// </summary>
        public float GetCooldownAtLevel(int level)
        {
            float cd = cooldown;
            for (int i = 1; i < level; i++)
                cd *= upgradeCooldownMult;
            return Mathf.Max(0.1f, cd);
        }
        
        /// <summary>
        /// 计算指定等级的攻击范围
        /// </summary>
        public float GetRangeAtLevel(int level)
        {
            return baseRange + (level - 1) * 0.2f;
        }

        /// <summary>
        /// 按名称加载技能数据 asset。
        /// Editor 下从 Assets/Data/Skills/ 加载；Runtime 从 Resources/Skills/ 加载。
        /// </summary>
        public static SkillLibrary LoadByName(string name)
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<SkillLibrary>($"Assets/Data/Skills/{name}.asset");
#else
            return Resources.Load<SkillLibrary>($"Skills/{name}");
#endif
        }
    }
}
