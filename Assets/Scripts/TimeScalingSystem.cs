using UnityEngine;

namespace BiopunkRV
{
    /// <summary>
    /// 时间系统 — 随时间流逝怪物越来越强。
    /// 每7分钟怪物血量×2，攻击×2。
    /// </summary>
    public class TimeScalingSystem : MonoBehaviour
    {
        public static TimeScalingSystem Instance { get; private set; }

        [Header("Time Scaling")]
        public float scaleInterval = 420f; // 7分钟 = 420秒
        public float healthMultiplier = 2f;
        public float attackMultiplier = 2f;

        private float _timer;
        private int _scaleLevel;

        public int CurrentScaleLevel => _scaleLevel;
        public float ElapsedTime => _timer;

        private void Awake()
        {
            Instance = this;
        }

        private void Update()
        {
            _timer += Time.deltaTime;

            if (_timer >= scaleInterval)
            {
                _timer -= scaleInterval;
                _scaleLevel++;
                ApplyScalingToAllEnemies();
                Debug.Log($"Time scale level up! Level {_scaleLevel} — enemies HP×{Mathf.Pow(healthMultiplier, _scaleLevel)}, ATK×{Mathf.Pow(attackMultiplier, _scaleLevel)}");
            }
        }

        private void ApplyScalingToAllEnemies()
        {
            var enemies = FindObjectsOfType<EnemyController>();
            foreach (var enemy in enemies)
            {
                if (enemy == null) continue;
                enemy.maxHealth *= healthMultiplier;
                enemy.currentHealth = enemy.maxHealth;
                enemy.attack *= attackMultiplier;
            }
        }

        /// <summary>
        /// 获取当前全局血量倍率
        /// </summary>
        public float GetHealthMultiplier()
        {
            return Mathf.Pow(healthMultiplier, _scaleLevel);
        }

        /// <summary>
        /// 获取当前全局攻击倍率
        /// </summary>
        public float GetAttackMultiplier()
        {
            return Mathf.Pow(attackMultiplier, _scaleLevel);
        }
    }
}