using UnityEngine;

namespace BiopunkRV
{
    /// <summary>
    /// 风灵蝶敌人: 风属性高速近战小怪, 漂浮+振翅动画。
    /// 继承 EnemyController 基类的移动/分离/血条/受击/死亡系统。
    /// 使用 SpriteRenderer。
    /// </summary>
    public class WindButterflyController : EnemyController
    {
        [Header("风灵蝶动画")]
        public float floatHeight = 0.18f;
        public float flapSpeed = 2.8f;
        public float flyHeight = 2.5f;   // 离地飞行高度 (2026-09-03: 蝴蝶要飞高一点)

        private SpriteRenderer _sr;
        private float _bobTimer;

        protected override void OnAwake()
        {
            // 风灵蝶属性 — 风属性, 低血量高速
            maxHealth = 18f;
            currentHealth = 18f;
            attack = 5f;
            defense = 5f;
            element = Element.Wind;
            elementDefense = 0f;
            isRanged = false;
            moveSpeed = 9f;
            stopDistance = 2.4f;
            separationRadius = 3f;
            separationForce = 3f;

            _sr = GetComponent<SpriteRenderer>();
            if (_sr == null)
                _sr = gameObject.AddComponent<SpriteRenderer>();
            _sr.sortingOrder = 15;
        }

        /// <summary>
        /// 风灵蝶动画: 漂浮 + 振翅(X轴缩放脉动模拟翅膀开合)
        /// </summary>
        protected override void AnimateMovement()
        {
            _bobTimer += Time.deltaTime * (_isMoving ? 5f : 3f);

            Vector3 pos = transform.position;
            pos.y = _baseY + flyHeight + Mathf.Sin(_bobTimer) * floatHeight;
            transform.position = pos;

            float flap = 0.85f + Mathf.Abs(Mathf.Sin(_bobTimer * flapSpeed)) * 0.3f;
            Vector3 scale = _baseScale;
            scale.x *= flap;
            scale.x = _facingLeft ? -Mathf.Abs(scale.x) : Mathf.Abs(scale.x);
            transform.localScale = scale;
        }
    }
}
