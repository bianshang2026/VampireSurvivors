using UnityEngine;

namespace BiopunkRV
{
    [CreateAssetMenu(fileName = "EnemyData", menuName = "修仙游戏/怪物数据", order = 1)]
    public class EnemyLibrary : ScriptableObject
    {
        [Header("基础属性")]
        public string enemyName = "怪物";
        public float maxHealth = 20f;
        public float attack = 5f;
        public float defense = 5f;
        public float moveSpeed = 5f;
        public float detectRange = 9999f;
        public float stopDistance = 2.4f;
        public float separationRadius = 3f;
        public float separationForce = 3f;

        [Header("元素属性")]
        public Element element = Element.None;
        public float elementDefense = 0f;

        [Header("远程攻击")]
        public bool isRanged = false;
        public float attackInterval = 2.5f;
        public float attackRange = 16f;
        public float projectileSpeed = 10f;
        public float projectileDamage = 5f;

        [Header("掉落")]
        public int expDrop = 20;
        public SpiritOrbPickup.OrbTier orbTier = SpiritOrbPickup.OrbTier.Basic;

        [Header("时间缩放")]
        public bool affectedByTimeScaling = true;

        /// <summary>
        /// 应用数据到敌人控制器
        /// </summary>
        public void ApplyTo(EnemyController enemy)
        {
            enemy.maxHealth = maxHealth;
            enemy.currentHealth = maxHealth;
            enemy.attack = attack;
            enemy.defense = defense;
            enemy.moveSpeed = moveSpeed;
            enemy.detectRange = detectRange;
            enemy.stopDistance = stopDistance;
            enemy.separationRadius = separationRadius;
            enemy.separationForce = separationForce;
            enemy.element = element;
            enemy.elementDefense = elementDefense;
            enemy.isRanged = isRanged;
            enemy.attackInterval = attackInterval;
            enemy.attackRange = attackRange;
            enemy.projectileSpeed = projectileSpeed;
        }
    }
}
