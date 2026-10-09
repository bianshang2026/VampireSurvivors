using UnityEngine;

namespace BiopunkRV
{
    /// <summary>
    /// 敌人投射物 — 由远程敌人发射，命中玩家造成伤害。
    /// </summary>
    public class EnemyProjectile : MonoBehaviour
    {
        public Vector3 velocity;
        public float damage = 5f;
        public Element element = Element.None;
        public float lifetime = 5f;

        private void Update()
        {
            Vector3 move = velocity * Time.deltaTime;
            move.y = 0f;
            transform.position += move;

            lifetime -= Time.deltaTime;
            if (lifetime <= 0f) Destroy(gameObject);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.name == "Player")
            {
                var psc = other.GetComponent<PlayerStatsComponent>();
                if (psc != null && !psc.godMode)
                {
                    bool isElemental = element != Element.None;
                    float actual = psc.stats.TakeDamage(damage, isElemental);
                    Debug.Log($"Enemy projectile hit Player for {actual} damage ({element})");
                }
                Destroy(gameObject);
            }
        }
    }
}