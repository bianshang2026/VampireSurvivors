using UnityEngine;

namespace BiopunkRV
{
    /// <summary>
    /// Spawns falling particles (leaves or light dots) above a tree.
    /// Uses a simple pooled approach with MeshRenderer quads.
    /// </summary>
    public class TreeParticleEffect : MonoBehaviour
    {
        [Header("Particle Settings")]
        public Material particleMaterial;
        public int maxParticles = 15;
        public float spawnRate = 0.4f;
        public float fallSpeed = 1.5f;
        public float swayAmount = 0.8f;
        public float lifetime = 4f;
        public float particleSize = 0.4f;

        [Header("Spawn Area")]
        public Vector2 spawnWidth = new Vector2(-2f, 2f);
        public float spawnYOffset = 3f;
        public float despawnY = -3f;

        private GameObject[] particles;
        private float[] particleTimers;
        private float[] particlePhases;
        private float[] particleBaseX;
        private float spawnTimer;

        void Start()
        {
            particles = new GameObject[maxParticles];
            particleTimers = new float[maxParticles];
            particlePhases = new float[maxParticles];
            particleBaseX = new float[maxParticles];

            for (int i = 0; i < maxParticles; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                go.name = $"Particle_{i}";
                go.transform.SetParent(transform);
                go.transform.localScale = Vector3.one * particleSize;
                var mr = go.GetComponent<MeshRenderer>();
                mr.sharedMaterial = particleMaterial;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                Destroy(go.GetComponent<MeshCollider>());
                go.SetActive(false);
                particles[i] = go;
            }
        }

        void Update()
        {
            spawnTimer += Time.deltaTime;

            for (int i = 0; i < maxParticles; i++)
            {
                if (particles[i].activeSelf)
                {
                    particleTimers[i] -= Time.deltaTime;
                    if (particleTimers[i] <= 0)
                    {
                        particles[i].SetActive(false);
                        continue;
                    }

                    float progress = 1f - (particleTimers[i] / lifetime);
                    var pos = particles[i].transform.position;

                    // Fall down with sway
                    pos.y -= fallSpeed * Time.deltaTime;
                    pos.x = particleBaseX[i] + Mathf.Sin(Time.time * 2f + particlePhases[i]) * swayAmount * progress;
                    pos.z += 0.1f * Time.deltaTime; // slight forward drift

                    particles[i].transform.position = pos;

                    // Fade out near end
                    float alpha = particleTimers[i] < 0.5f ? particleTimers[i] / 0.5f : 1f;
                    var c = particles[i].GetComponent<MeshRenderer>().material.color;
                    c.a = alpha;
                    particles[i].GetComponent<MeshRenderer>().material.color = c;

                    if (pos.y < despawnY + transform.position.y)
                    {
                        particles[i].SetActive(false);
                    }
                }
            }

            // Spawn new particles
            if (spawnTimer >= spawnRate)
            {
                spawnTimer = 0f;
                SpawnParticle();
            }
        }

        void SpawnParticle()
        {
            for (int i = 0; i < maxParticles; i++)
            {
                if (!particles[i].activeSelf)
                {
                    float xOffset = Random.Range(spawnWidth.x, spawnWidth.y);
                    var startPos = transform.position + new Vector3(xOffset, spawnYOffset, 0);
                    particles[i].transform.position = startPos;
                    particles[i].SetActive(true);
                    particleTimers[i] = lifetime;
                    particlePhases[i] = Random.Range(0f, Mathf.PI * 2f);
                    particleBaseX[i] = startPos.x;

                    // Reset alpha
                    var c = particles[i].GetComponent<MeshRenderer>().material.color;
                    c.a = 1f;
                    particles[i].GetComponent<MeshRenderer>().material.color = c;
                    break;
                }
            }
        }
    }
}
