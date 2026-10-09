using UnityEngine;

namespace BiopunkRV
{
    /// <summary>
    /// Fire Spirit enemy. Floats and bobs toward player like a living flame.
    /// Uses SpriteRenderer (not MeshRenderer). No side sprite — single sprite only.
    /// </summary>
    public class FireSpiritController : EnemyController
    {
        [Header("Fire Spirit Settings")]
        public float fireSpiritHealth = 25f;
        public float fireSpiritAttack = 7f;
        public float fireSpiritDefense = 6f;
        public float fireSpiritElementDefense = 15f;

        [Header("Float Animation")]
        public float floatHeight = 0.3f;
        public float floatSpeed = 3f;
        public float wobble = 0.1f;

        private SpriteRenderer _sr;

        protected override void OnAwake()
        {
            _sr = GetComponent<SpriteRenderer>();
            if (_sr != null && _sr.material != null) _mat = _sr.material;

            // Fire Spirit stats
            maxHealth = fireSpiritHealth;
            currentHealth = fireSpiritHealth;
            attack = fireSpiritAttack;
            defense = fireSpiritDefense;
            element = Element.Fire;
            elementDefense = fireSpiritElementDefense;
            isRanged = true;
            attackRange = 16f;
            attackInterval = 2.5f;
            projectileSpeed = 10f;
        }

        protected override void AnimateMovement()
        {
            _animTimer += Time.deltaTime * floatSpeed;

            // Float up and down smoothly (sine wave)
            float bob = Mathf.Sin(_animTimer) * floatHeight;
            
            // Wobble rotation slightly
            float rotZ = Mathf.Sin(_animTimer * 1.3f) * wobble * 10f;

            Vector3 pos = transform.position;
            pos.y = _baseY + bob;
            transform.position = pos;

            // Apply rotation wobble
            transform.rotation = Quaternion.Euler(0, 0, rotZ);

            // Scale with facing direction
            Vector3 scale = _baseScale;
            scale.x = _facingLeft ? -Mathf.Abs(scale.x) : Mathf.Abs(scale.x);
            transform.localScale = scale;

            // Subtle size pulse when moving
            if (_isMoving)
            {
                float pulse = 1f + Mathf.Sin(_animTimer * 2f) * wobble;
                scale.y *= pulse;
                transform.localScale = scale;
            }
        }
    }
}
