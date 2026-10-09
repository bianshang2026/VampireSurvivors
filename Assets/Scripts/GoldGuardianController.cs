using UnityEngine;
using System.Collections.Generic;

namespace BiopunkRV
{
    /// <summary>
    /// Gold Guardian enemy. When player enters attack range,
    /// plays spinning attack animation while moving toward player for 4 seconds.
    /// Contact damage on touch during attack.
    /// </summary>
    public class GoldGuardianController : EnemyController
    {
        [Header("Attack Animation")]
        public Sprite[] attackFrames;
        public float attackAnimFPS = 12f;
        public float attackDuration = 4f;
        public float attackMoveSpeed = 6f;
        public float attackRange = 8f;
        public float contactDamage = 15f;
        public float contactCooldown = 0.5f;

        [Header("States")]
        public bool isAttacking = false;
        
        private SpriteRenderer _sr;
        private float _attackTimer;
        private float _animTimer;
        private int _currentFrame;
        private float _contactTimer;
        private Sprite _idleSprite;
        private bool _wasAttacking = false;

        protected override void OnAwake()
        {
            _sr = GetComponent<SpriteRenderer>();
            if (_sr != null && _sr.sprite != null)
                _idleSprite = _sr.sprite;
        }

        protected override void OnStart()
        {
            // Load attack frames from Resources if not assigned
            if (attackFrames == null || attackFrames.Length == 0)
            {
                var textures = Resources.LoadAll<Texture2D>("GoldGuardianAttackFrames");
                if (textures != null && textures.Length > 0)
                {
                    System.Array.Sort(textures, (a, b) => a.name.CompareTo(b.name));
                    attackFrames = new Sprite[textures.Length];
                    for (int i = 0; i < textures.Length; i++)
                    {
                        attackFrames[i] = Sprite.Create(textures[i], 
                            new Rect(0, 0, textures[i].width, textures[i].height), 
                            new Vector2(0.5f, 0.5f), 100f);
                    }
                }
            }
        }

        protected override void AnimateMovement()
        {
            // Check if player is in attack range
            if (target != null && !isAttacking)
            {
                float dist = Vector3.Distance(transform.position, target.position);
                if (dist < attackRange)
                {
                    StartAttack();
                }
            }

            if (isAttacking)
            {
                UpdateAttack();
            }
            else
            {
                // Normal idle/move animation - gentle float like rabbit but smaller
                _animTimer += Time.deltaTime;
                float bob = Mathf.Sin(_animTimer * 3f) * 0.03f;  // 幅度0.03（兔子是0.25）
                Vector3 pos = transform.position;
                pos.y = _baseY + bob;
                transform.position = pos;

                // Face player
                Vector3 scale = _baseScale;
                if (_facingLeft)
                    scale.x = -Mathf.Abs(scale.x);
                else
                    scale.x = Mathf.Abs(scale.x);
                transform.localScale = scale;

                // Use idle sprite when not attacking
                if (_sr != null && _idleSprite != null && !isAttacking)
                    _sr.sprite = _idleSprite;
            }
        }

        private void StartAttack()
        {
            isAttacking = true;
            _attackTimer = 0f;
            _animTimer = 0f;
            _currentFrame = 0;
            _contactTimer = 0f;
        }

        private void UpdateAttack()
        {
            _attackTimer += Time.deltaTime;
            _contactTimer += Time.deltaTime;

            // Play attack animation frames
            if (attackFrames != null && attackFrames.Length > 0)
            {
                _animTimer += Time.deltaTime;
                float frameDur = 1f / attackAnimFPS;
                if (_animTimer >= frameDur)
                {
                    _animTimer -= frameDur;
                    _currentFrame = (_currentFrame + 1) % attackFrames.Length;
                    if (_sr != null)
                        _sr.sprite = attackFrames[_currentFrame];
                }
            }

            // Move toward player during attack
            if (target != null)
            {
                Vector3 dir = (target.position - transform.position).normalized;
                dir.y = 0;
                transform.position += dir * attackMoveSpeed * Time.deltaTime;
            }

            // Contact damage
            if (target != null && _contactTimer >= contactCooldown)
            {
                float dist = Vector3.Distance(transform.position, target.position);
                if (dist < 2f) // contact radius
                {
                    // Damage player via ExpBar system
                    ExpBar.TakeDamage(contactDamage);
                    _contactTimer = 0f;
                }
            }

            // End attack after duration
            if (_attackTimer >= attackDuration)
            {
                EndAttack();
            }
        }

        private void EndAttack()
        {
            isAttacking = false;
            _attackTimer = 0f;
            if (_sr != null && _idleSprite != null)
                _sr.sprite = _idleSprite;
        }

        public override void TakeDamage(float damage)
        {
            base.TakeDamage(damage);
            // Hit flash on attack frames too
            if (isAttacking && _sr != null)
            {
                // Flash effect handled by base class material
            }
        }
    }
}
