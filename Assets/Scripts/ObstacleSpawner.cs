using System.Collections;
using UnityEngine;

namespace BiopunkRV
{
    /// <summary>
    /// Randomly spawns obstacle prefabs from the right edge that fly left.
    /// Like Gunman Taco Truck style debris flying past.
    /// </summary>
    public class ObstacleSpawner : MonoBehaviour
    {
        [Header("Obstacles")]
        public GameObject[] obstaclePrefabs;

        [Header("Spawn Settings")]
        public float minInterval = 0.5f;
        public float maxInterval = 2.0f;
        public float obstacleSpeed = 15f;
        public float minScale = 0.9f;
        public float maxScale = 1.1f;

        [Header("Position")]
        public float spawnY = -2f;
        public float spawnZ = 0f;
        public float destroyX = -20f;

        private Camera _cam;

        private void Start()
        {
            _cam = Camera.main;
            if (_cam == null) _cam = FindObjectOfType<Camera>();
            StartCoroutine(SpawnLoop());
        }

        private IEnumerator SpawnLoop()
        {
            while (true)
            {
                float waitTime = Random.Range(minInterval, maxInterval);
                yield return new WaitForSeconds(waitTime);

                if (obstaclePrefabs == null || obstaclePrefabs.Length == 0) continue;

                SpawnObstacle();
            }
        }

        private void SpawnObstacle()
        {
            int idx = Random.Range(0, obstaclePrefabs.Length);
            GameObject prefab = obstaclePrefabs[idx];
            if (prefab == null) return;

            // Spawn at right edge of screen
            float spawnX = _cam != null
                ? _cam.ViewportToWorldPoint(new Vector3(1.15f, 0, 0)).x
                : 15f;

            // Random Y near road level
            float y = spawnY + Random.Range(-0.5f, 0.5f);

            GameObject obj = Instantiate(prefab, new Vector3(spawnX, y, spawnZ), Quaternion.identity);
            obj.transform.SetParent(transform, false);
            obj.transform.position = new Vector3(spawnX, y, spawnZ);

            // Random scale
            float s = Random.Range(minScale, maxScale);
            obj.transform.localScale = new Vector3(s, s, 1f);

            // Random rotation for variety
            obj.transform.Rotate(0, 0, Random.Range(-5f, 5f));

            // Add movement component
            var mover = obj.GetComponent<MovingObstacle>();
            if (mover == null)
                mover = obj.AddComponent<MovingObstacle>();
            mover.speed = obstacleSpeed + Random.Range(-2f, 3f);
            mover.destroyX = destroyX;
        }
    }
}