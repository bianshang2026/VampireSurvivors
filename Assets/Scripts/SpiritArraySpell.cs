using UnityEngine;

namespace BiopunkRV
{
    /// <summary>
    /// 灵力阵技能控制器 — 挂在Player，自动施放到敌人脚下。
    /// (2026-09-02: 从SpiritArray.cs拆出到独立文件 — 类名必须与文件名一致才能被场景序列化,
    ///  之前在SpiritArray.cs里导致场景保存时组件被静默丢弃变成Missing Script)
    /// </summary>
    public class SpiritArraySpell : MonoBehaviour
    {
        public Sprite arraySprite;
        public float castInterval = 2f;      // 施放间隔
        public float arrayDps = 5f;          // 法阵每秒伤害
        public float castRange = 10f;        // 施放范围
        public float arrayLifetime = 3f;
        public int maxArrays = 3;            // 同时存在的法阵上限

        private float _castTimer;
        private Transform _player;

        private void Awake()
        {
            _player = transform;
#if UNITY_EDITOR
            if (arraySprite == null)
                arraySprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/TJGenerators/History/Sprite_20260809_002731.png");
#endif
        }

        private void Update()
        {
            _castTimer += Time.deltaTime;
            float actualInterval = castInterval;
            var psc = PlayerStatsComponent.Instance;
            if (psc != null) actualInterval = psc.stats.GetScaledInterval(castInterval);

            if (_castTimer >= actualInterval)
            {
                _castTimer = 0f;
                TryCast();
            }
        }

        private void TryCast()
        {
            Transform target = FindRandomEnemyNearPlayer();
            if (target == null) return;

            // 限制法阵数量
            int existing = FindObjectsOfType<SpiritArray>().Length;
            if (existing >= maxArrays) return;

            var arr = SpiritArray.CreateAt(target.position, arraySprite);
            arr.dps = arrayDps;
            arr.lifetime = arrayLifetime;
            arr.maxScale = 0.22f;      // 0.22×贴图20.48单位≈4.5世界直径, 覆盖一只怪物
            arr.damageRadius = 2.2f;   // 伤害半径与视觉半径匹配
            arr.growDuration = 0.5f;   // 生成更快
            arr.rotateSpeed = 30f;
        }

        private Transform FindRandomEnemyNearPlayer()
        {
            var enemies = FindObjectsOfType<EnemyController>();
            Transform best = null;
            float bestDist = castRange;
            foreach (var e in enemies)
            {
                if (e == null || !e.gameObject.activeInHierarchy) continue;
                float d = Vector3.Distance(_player.position, e.transform.position);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = e.transform;
                }
            }
            var rabbits = FindObjectsOfType<GhostRabbitController>();
            foreach (var r in rabbits)
            {
                if (r == null || !r.gameObject.activeInHierarchy) continue;
                float d = Vector3.Distance(_player.position, r.transform.position);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = r.transform;
                }
            }
            return best;
        }
    }
}
