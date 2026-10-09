using UnityEngine;

namespace BiopunkRV
{
    /// <summary>
    /// 金甲卫士 — 高防高血近战精英怪，金属性
    /// 攻击动画在靠近玩家时提前播放，播放3秒后停止，7秒冷却后再触发。
    /// </summary>
    public class GoldenArmorController : EnemyController
    {
        [Header("动画")]
        public float bobHeight = 0.12f;
        public float bobSpeed = 4f;
        public float squash = 0.08f;
        public float attackAnimDur = 3f;       // 动画播放时长
        public float attackCooldown = 7f;      // 攻击冷却时间
        public float attackMoveSpeed = 10f;
        public float attackAnimFPS = 12f;
        public float attackTriggerRange = 8f;   // 距离玩家多近时开始播放动画
        public float attackScaleBonus = 1.3f;  // 攻击动画时放大倍数

        private SpriteRenderer _sr;
        private Sprite[] _attackFrames;
        private Sprite _idleSprite;
        private int _currentFrame;
        private float _frameTimer;
        private float _attackAnimTimer;
        private float _cooldownTimer;
        private float _attackScaleMult = 1f;

        protected override void OnAwake()
        {
            maxHealth = 120f;
            currentHealth = 120f;
            attack = 20f;
            defense = 40f;
            element = Element.Metal;
            elementDefense = 15f;
            isRanged = false;
            moveSpeed = 4f;
            stopDistance = 2.4f;
            separationRadius = 3.5f;
            attackInterval = 7f; // 同步冷却
            isElite = true;      // 近战精英 — 必掉灵石+碎片
            _sr = GetComponent<SpriteRenderer>();
            _baseY += 1f;
        }

        protected override void OnStart()
        {
            if (_sr != null && _sr.sprite != null)
                _idleSprite = _sr.sprite;

            // Load attack frames from Resources
            var textures = Resources.LoadAll<Texture2D>("GoldenArmorAttackFrames");
            if (textures != null && textures.Length > 0)
            {
                System.Array.Sort(textures, (a, b) => a.name.CompareTo(b.name));
                _attackFrames = new Sprite[textures.Length];
                for (int i = 0; i < textures.Length; i++)
                {
                    _attackFrames[i] = Sprite.Create(textures[i],
                        new Rect(0, 0, textures[i].width, textures[i].height),
                        new Vector2(0.5f, 0.5f), 100f);
                }
                Debug.Log($"[GoldenArmor] Loaded {_attackFrames.Length} attack frames");

                // Calculate scale to match idle sprite's world size
                if (_idleSprite != null && _attackFrames.Length > 0)
                {
                    float idleW = _idleSprite.bounds.size.x;
                    float frameW = _attackFrames[0].bounds.size.x;
                    if (frameW > 0.001f)
                        _attackScaleMult = idleW / frameW;
                    Debug.Log($"[GoldenArmor] idleW={idleW:F2} frameW={frameW:F2} scaleMult={_attackScaleMult:F3}");
                }
            }
            else
            {
                Debug.Log("[GoldenArmor] No attack frames found in Resources!");
            }
        }

        protected override void AnimateMovement()
        {
            // 计时冷却
            _cooldownTimer += Time.deltaTime;

            // 检查是否应该开始攻击动画（距离够近 + 冷却完成 + 不在攻击中）
            if (!_isAttacking && _cooldownTimer >= attackCooldown && target != null)
            {
                Vector3 toTarget = target.position - transform.position;
                toTarget.y = 0f;
                float dist = toTarget.magnitude;
                if (dist <= attackTriggerRange)
                {
                    StartAttack();
                }
            }

            // 攻击动画播放中
            if (_isAttacking)
            {
                _attackAnimTimer += Time.deltaTime;
                _frameTimer += Time.deltaTime;
                float frameDur = 1f / attackAnimFPS;

                if (_frameTimer >= frameDur)
                {
                    _frameTimer -= frameDur;
                    _currentFrame = (_currentFrame + 1) % _attackFrames.Length;
                    if (_sr != null && _attackFrames != null)
                        _sr.sprite = _attackFrames[_currentFrame];
                }

                // 攻击动画时放大 + 保持比例
                Vector3 atkScale = _baseScale * _attackScaleMult * attackScaleBonus;
                atkScale.x = _facingLeft ? -Mathf.Abs(atkScale.x) : Mathf.Abs(atkScale.x);
                transform.localScale = atkScale;

                // 攻击时继续向玩家移动
                if (target != null)
                {
                    Vector3 dir = target.position - transform.position;
                    dir.y = 0f;
                    if (dir.sqrMagnitude > 0.01f)
                    {
                        dir.Normalize();
                        Vector3 newPos = transform.position + dir * attackMoveSpeed * Time.deltaTime;
                        Vector3 tryX = new Vector3(newPos.x, transform.position.y, transform.position.z);
                        if (!CheckObstacle(tryX)) transform.position = tryX;
                        Vector3 tryZ = new Vector3(transform.position.x, transform.position.y, newPos.z);
                        if (!CheckObstacle(tryZ)) transform.position = tryZ;
                    }
                }

                // 动画播放完毕，停止
                if (_attackAnimTimer >= attackAnimDur)
                {
                    EndAttack();
                }

                _isMoving = true;
                return;
            }

            // 正常移动动画
            if (_isMoving)
            {
                _animTimer += Time.deltaTime * bobSpeed;
                float cycle = Mathf.Sin(_animTimer * Mathf.PI);
                float bob = Mathf.Max(0, cycle) * bobHeight;
                float absCycle = Mathf.Abs(cycle);
                float scaleY = 1f + absCycle * squash;
                float scaleX = 1f - absCycle * squash * 0.5f;

                Vector3 pos = transform.position;
                pos.y = _baseY + bob;
                transform.position = pos;

                Vector3 scale = _baseScale;
                scale.x *= scaleX;
                scale.y *= scaleY;
                scale.x = _facingLeft ? -Mathf.Abs(scale.x) : Mathf.Abs(scale.x);
                transform.localScale = scale;
            }
            else
            {
                _animTimer = 0f;
                Vector3 pos = transform.position;
                pos.y = _baseY;
                transform.position = pos;
                Vector3 scale = _baseScale;
                scale.x = _facingLeft ? -Mathf.Abs(scale.x) : Mathf.Abs(scale.x);
                transform.localScale = scale;
            }
        }

        private void StartAttack()
        {
            _isAttacking = true;
            _attackAnimTimer = 0f;
            _frameTimer = 0f;
            _currentFrame = 0;
            if (_sr != null && _attackFrames != null && _attackFrames.Length > 0)
                _sr.sprite = _attackFrames[0];
        }

        private void EndAttack()
        {
            _isAttacking = false;
            _cooldownTimer = 0f; // 开始冷却
            _currentFrame = 0;
            if (_sr != null && _idleSprite != null)
                _sr.sprite = _idleSprite;

            // 恢复正常scale
            Vector3 scale = _baseScale;
            scale.x = _facingLeft ? -Mathf.Abs(scale.x) : Mathf.Abs(scale.x);
            transform.localScale = scale;
        }

        protected override void OnMeleeAttack()
        {
            // 由AnimateMovement中距离检测自动触发，这里不需要额外操作
            // 基类的DoMeleeAttack仍然会在近战距离造成伤害
        }
    }
}
