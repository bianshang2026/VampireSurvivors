using UnityEngine;

namespace BiopunkRV
{
    /// <summary>
    /// 冰狐敌人: 水属性, 靠近玩家时减速光环。
    /// 继承 EnemyController 基类的移动/分离/血条/受击/死亡/灼烧系统。
    /// 使用 SpriteRenderer (和 FireSpirit 一样), 不用 MeshRenderer。
    /// </summary>
    public class IceFoxController : EnemyController
    {
        [Header("冰狐特有")]
        public float slowAuraRadius = 6f;
        public float slowFactor = 0.5f;
        public float slowDuration = 2f;

        [Header("Sprites")]
        public Sprite idleSprite;
        public Sprite walkSprite;

        private SpriteRenderer _sr;
        private bool _useWalk;
        private float _bobTimer;

        protected override void OnAwake()
        {
            // 冰狐属性 — 水属性, 中等HP, 减速光环
            maxHealth = 50f;
            currentHealth = 50f;
            attack = 12f;
            defense = 10f;
            element = Element.Water;
            elementDefense = 0f;
            isRanged = false;
            moveSpeed = 5f;
            stopDistance = 2.4f;
            separationRadius = 3f;
            separationForce = 3f;

            _sr = GetComponent<SpriteRenderer>();
            if (_sr == null)
            {
                // 如果没有 SpriteRenderer, 创建一个
                _sr = gameObject.AddComponent<SpriteRenderer>();
            }
            _sr.sortingOrder = 10;
        }

        protected override void OnStart()
        {
            if (_sr != null && idleSprite != null)
                _sr.sprite = idleSprite;
        }

        /// <summary>
        /// 冰狐动画: 漂浮+轻微缩放, 水属性蓝色光晕
        /// </summary>
        protected override void AnimateMovement()
        {
            // 切换 idle/walk sprite
            if (_sr != null)
            {
                Sprite target = _isMoving ? walkSprite : idleSprite;
                if (target != null && _sr.sprite != target)
                    _sr.sprite = target;
            }

            if (_isMoving)
            {
                // 漂浮 + 轻微缩放
                _bobTimer += Time.deltaTime * 3f;
                float bob = Mathf.Sin(_bobTimer) * 0.15f;
                float scalePulse = 1f + Mathf.Abs(Mathf.Sin(_bobTimer * 0.5f)) * 0.05f;

                Vector3 pos = transform.position;
                pos.y = _baseY + bob;
                transform.position = pos;

                Vector3 scale = _baseScale;
                scale.x *= scalePulse;
                scale.y *= scalePulse;
                scale.x = _facingLeft ? -Mathf.Abs(scale.x) : Mathf.Abs(scale.x);
                transform.localScale = scale;
            }
            else
            {
                _bobTimer += Time.deltaTime * 2f;
                float bob = Mathf.Sin(_bobTimer) * 0.08f;

                Vector3 pos = transform.position;
                pos.y = _baseY + bob;
                transform.position = pos;

                Vector3 scale = _baseScale;
                scale.x = _facingLeft ? -Mathf.Abs(scale.x) : Mathf.Abs(scale.x);
                transform.localScale = scale;
            }

            // 减速光环: 靠近玩家时降低移速
            if (target != null)
            {
                float distToPlayer = Vector3.Distance(
                    new Vector3(transform.position.x, 0, transform.position.z),
                    new Vector3(target.position.x, 0, target.position.z));

                if (distToPlayer < slowAuraRadius)
                {
                    var cc2d = target.GetComponent<CharacterController2D>();
                    if (cc2d != null)
                    {
                        cc2d.ApplySlow(slowFactor, slowDuration);
                    }
                }
            }
        }

        /// <summary>
        /// 显示减速光环范围 (Gizmo 调试用)
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.2f);
            Gizmos.DrawSphere(transform.position, slowAuraRadius);
        }
    }
}
