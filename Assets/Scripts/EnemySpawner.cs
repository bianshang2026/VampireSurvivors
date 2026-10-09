using System.Collections;
using UnityEngine;

namespace BiopunkRV
{
    /// <summary>
    /// Infinite monster spawner for 秘境 (secret realm) scene.
    /// Supports multiple enemy types with weighted spawning and time windows.
    /// </summary>
    public class EnemySpawner : MonoBehaviour
    {
        [System.Serializable]
        public struct EnemySpawnEntry
        {
            [Tooltip("敌人预制体")]
            public GameObject prefab;
            [Tooltip("刷新权重(越大越常见)")]
            public float weight;
            [Tooltip("开始刷新时间(秒, 0=一开始就可刷)")]
            public float minTime;
            [Tooltip("停止刷新时间(秒, 0=一直可刷)")]
            public float maxTime;
        }

        [Header("多敌人刷新列表")]
        public EnemySpawnEntry[] enemyEntries;

        [Header("Boss Settings")]
        public GameObject bossPrefab;
        public float bossInterval = 300f; // 每5分钟刷新一次Boss
        public int maxBossAlive = 1;
        private float _nextBossTime = -1f;
        private int _bossCount = 0;

        [Header("旧单预制体(兼容, 列表为空时使用)")]
        public GameObject enemyPrefab;

        [Header("Spawn Settings")]
        public float minInterval = 0.3f;
        public float maxInterval = 1.0f;
        public int maxConcurrent = 50;

        [Header("Map Bounds")]
        public float mapMinX = -120f;
        public float mapMaxX = 130f;
        public float mapMinZ = -110f;
        public float mapMaxZ = 150f;
        public float edgeOffset = 10f;
        public float minSpawnDistFromPlayer = 20f;

        [Header("Difficulty Scaling")]
        public float minIntervalDecreasePerMin = 0.05f;
        public float maxIntervalDecreasePerMin = 0.1f;
        public float minMinInterval = 0.1f;
        public float minMaxInterval = 0.3f;

        [Header("法阵效果")]
        public Sprite spawnCircleSprite;
        public float circleDuration = 0.8f;
        public float circleScale = 3f;

        private Transform _player;
        private float _elapsedTime;
        private int _aliveCount;

        private void Start()
        {
            var player = GameObject.Find("Player");
            if (player != null) _player = player.transform;

            // 初始化Boss刷新时间
            if (bossPrefab != null && _nextBossTime < 0f)
                _nextBossTime = bossInterval;

            StartCoroutine(SpawnLoop());
        }

        private void Update()
        {
            _elapsedTime += Time.deltaTime;

            // 统计所有活着的敌人 (用基类, 包含所有子类)
            var allEnemies = FindObjectsOfType<EnemyController>();
            _aliveCount = allEnemies != null ? allEnemies.Length : 0;

            // Boss刷新逻辑
            if (bossPrefab != null && _nextBossTime > 0f && _elapsedTime >= _nextBossTime)
            {
                int aliveBosses = 0;
                var allBosses = FindObjectsOfType<CrimsonFlameBossController>();
                if (allBosses != null) aliveBosses = allBosses.Length;

                if (aliveBosses < maxBossAlive)
                {
                    StartCoroutine(SpawnBoss());
                    _bossCount++;
                    _nextBossTime = _elapsedTime + bossInterval;
                }
            }
        }

        private IEnumerator SpawnLoop()
        {
            yield return new WaitForSeconds(2f);

            while (true)
            {
                float minutesElapsed = _elapsedTime / 60f;
                float currentMin = Mathf.Max(minMinInterval, minInterval - minutesElapsed * minIntervalDecreasePerMin);
                float currentMax = Mathf.Max(minMaxInterval, maxInterval - minutesElapsed * maxIntervalDecreasePerMin);

                float multiplier = DevPanel.spawnRateMultiplier;
                if (multiplier <= 0f) multiplier = 0.01f;

                float waitTime = Random.Range(currentMin, currentMax) / multiplier;
                yield return new WaitForSeconds(waitTime);

                if (_aliveCount < maxConcurrent && _player != null)
                {
                    StartCoroutine(SpawnWithCircle());
                }
            }
        }

        private IEnumerator SpawnWithCircle()
        {
            Vector3 spawnPos = GetEdgeSpawnPosition();
            GameObject prefabToSpawn = PickEnemyPrefab();
            if (prefabToSpawn == null) prefabToSpawn = enemyPrefab;
            if (prefabToSpawn == null) yield break;

            // Load sprite if not assigned
            if (spawnCircleSprite == null)
                spawnCircleSprite = Resources.Load<Sprite>("SpawnCircle");

            // Calculate circle scale: slightly larger than enemy world size
            float enemyWorldSize = 2f; // fallback
            var prefabRenderer = prefabToSpawn.GetComponent<Renderer>();
            if (prefabRenderer != null && prefabRenderer.bounds.size.magnitude > 0.1f)
            {
                var size = prefabRenderer.bounds.size;
                enemyWorldSize = Mathf.Max(size.x, size.z);
            }
            // Circle sprite is 2048px at 100ppu = 20.48 world units at scale 1
            // Target: enemy size * 1.3 (slightly larger than enemy)
            float circleTexWorldSize = spawnCircleSprite != null ? spawnCircleSprite.rect.width / spawnCircleSprite.pixelsPerUnit : 20.48f;
            float finalCircleScale = (enemyWorldSize * 1.3f) / circleTexWorldSize;

            // Show magic circle
            GameObject circle = null;
            SpriteRenderer sr = null;
            if (spawnCircleSprite != null)
            {
                circle = new GameObject("SpawnCircle");
                circle.transform.position = spawnPos;
                circle.transform.rotation = Quaternion.Euler(90f, 0, 0);
                circle.transform.localScale = Vector3.zero;

                sr = circle.AddComponent<SpriteRenderer>();
                sr.sprite = spawnCircleSprite;
                sr.sortingOrder = 5;
                sr.color = new Color(1f, 1f, 1f, 0f);

                // Animate: scale up + fade in + rotate
                float elapsed = 0f;
                while (elapsed < circleDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / circleDuration;
                    float s = finalCircleScale * Mathf.SmoothStep(0f, 1f, t);
                    circle.transform.localScale = new Vector3(s, s, 1f);
                    circle.transform.Rotate(0, 0, 180f * Time.deltaTime, Space.Self);
                    sr.color = new Color(1f, 1f, 1f, Mathf.SmoothStep(0f, 1f, t));
                    yield return null;
                }
            }
            else
            {
                yield return new WaitForSeconds(circleDuration);
            }

            // Spawn enemy
            Quaternion spawnRot = prefabToSpawn.transform.rotation;
            GameObject enemy = Instantiate(prefabToSpawn, spawnPos, spawnRot);
            enemy.name = "Enemy_" + Time.frameCount;
            enemy.transform.SetParent(transform, true);

            // 防卡死: 出生点若在装饰物碰撞体内, 推出到装饰物表面外 (2026-09-02 装饰物加碰撞后)
            foreach (var col in Physics.OverlapSphere(enemy.transform.position, 0.5f))
            {
                var deco = col.GetComponentInParent<DecoObstacle>();
                if (deco == null) continue;
                Vector3 away = enemy.transform.position - deco.transform.position;
                away.y = 0f;
                float d = away.magnitude;
                float minD = col.bounds.extents.x + 1f;
                if (d < 0.01f) away = Random.insideUnitSphere;
                enemy.transform.position = deco.transform.position + away.normalized * minD;
                break;
            }

            var ctrl = enemy.GetComponent<EnemyController>();
            if (ctrl != null && _player != null)
                ctrl.target = _player;

            // Fade out circle
            if (circle != null && sr != null)
            {
                float fadeTime = 0.3f;
                float elapsed = 0f;
                while (elapsed < fadeTime)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / fadeTime;
                    sr.color = new Color(1f, 1f, 1f, 1f - t);
                    circle.transform.Rotate(0, 0, 360f * Time.deltaTime, Space.Self);
                    yield return null;
                }
                Destroy(circle);
            }
        }

        /// <summary>
        /// 根据权重和时间窗口随机选择敌人预制体
        /// </summary>
        private GameObject PickEnemyPrefab()
        {
            if (enemyEntries == null || enemyEntries.Length == 0)
                return null;

            float totalTime = _elapsedTime;
            float totalWeight = 0f;

            foreach (var entry in enemyEntries)
            {
                if (entry.prefab == null) continue;
                bool timeOk = entry.minTime <= totalTime && (entry.maxTime <= 0f || totalTime <= entry.maxTime);
                if (timeOk) totalWeight += entry.weight;
            }

            if (totalWeight <= 0f) return null;

            float roll = Random.Range(0f, totalWeight);
            float accum = 0f;

            foreach (var entry in enemyEntries)
            {
                if (entry.prefab == null) continue;
                bool timeOk = entry.minTime <= totalTime && (entry.maxTime <= 0f || totalTime <= entry.maxTime);
                if (!timeOk) continue;

                accum += entry.weight;
                if (roll <= accum) return entry.prefab;
            }

            return enemyEntries[0].prefab;
        }

        /// <summary>
        /// 在地图范围内随机生成位置，确保离玩家有最小距离
        /// </summary>
        private Vector3 GetEdgeSpawnPosition()
        {
            Vector3 playerPos = _player != null ? _player.position : Vector3.zero;

            Vector3 pos = playerPos;
            int attempts = 0;
            while (attempts < 30)
            {
                attempts++;
                pos.x = Random.Range(mapMinX, mapMaxX);
                pos.z = Random.Range(mapMinZ, mapMaxZ);

                float dx = pos.x - playerPos.x;
                float dz = pos.z - playerPos.z;
                if (dx * dx + dz * dz >= minSpawnDistFromPlayer * minSpawnDistFromPlayer)
                    break;
            }

            pos.y = 1.6f;
            return pos;
        }

        /// <summary>
        /// Boss刷新 — 大法阵+Boss出场
        /// </summary>
        private IEnumerator SpawnBoss()
        {
            Vector3 spawnPos = GetEdgeSpawnPosition();
            spawnPos.y = 2f;

            // 大法阵
            if (spawnCircleSprite != null)
            {
                GameObject circle = new GameObject("BossSpawnCircle");
                circle.transform.position = spawnPos;
                circle.transform.rotation = Quaternion.Euler(90f, 0, 0);
                circle.transform.localScale = Vector3.zero;

                var sr = circle.AddComponent<SpriteRenderer>();
                sr.sprite = spawnCircleSprite;
                sr.sortingOrder = 5;
                sr.color = new Color(1f, 0.3f, 0.1f, 0f);

                float bossCircleDuration = 2f;
                float bossCircleScale = 6f;
                float elapsed = 0f;
                while (elapsed < bossCircleDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / bossCircleDuration;
                    float s = bossCircleScale * Mathf.SmoothStep(0f, 1f, t);
                    circle.transform.localScale = new Vector3(s, s, 1f);
                    circle.transform.Rotate(0, 0, 540f * Time.deltaTime, Space.Self);
                    sr.color = new Color(1f, 0.3f, 0.1f, Mathf.SmoothStep(0f, 1f, t));
                    yield return null;
                }

                // Boss出场
                Quaternion spawnRot = bossPrefab.transform.rotation;
                GameObject boss = Instantiate(bossPrefab, spawnPos, spawnRot);
                boss.name = "Boss_" + _bossCount;
                boss.transform.SetParent(transform, true);

                var ctrl = boss.GetComponent<EnemyController>();
                if (ctrl != null && _player != null)
                    ctrl.target = _player;

                // Boss血条UI
                var bossHealth = boss.GetComponent<CrimsonFlameBossController>();
                if (bossHealth != null)
                {
                    BossHealthBar.Show(bossHealth, "赤焰妖王");
                }

                // 法阵淡出
                float fadeTime = 0.5f;
                elapsed = 0f;
                while (elapsed < fadeTime)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / fadeTime;
                    sr.color = new Color(1f, 0.3f, 0.1f, 1f - t);
                    circle.transform.Rotate(0, 0, 720f * Time.deltaTime, Space.Self);
                    yield return null;
                }
                Destroy(circle);
            }
        }
    }
}
