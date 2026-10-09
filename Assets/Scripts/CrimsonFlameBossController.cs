using UnityEngine;

namespace BiopunkRV
{
    /// <summary>
    /// 赤焰妖王 Boss — 多阶段AI (70%HP阶段1 / 30%HP阶段2)
    /// 阶段1: 近战追击+环形火球
    /// 阶段2: 狂暴模式+加速+扇形火球+灼烧地面
    /// </summary>
    public class CrimsonFlameBossController : EnemyController
    {
        [Header("Boss Settings")]
        public float phase2Threshold = 0.3f;  // 30% HP进入阶段2
        public float meleeAttackInterval = 3f;
        public float rangedAttackInterval = 5f;
        public int orbCount = 8;               // 环形火球数量
        public int fanCount = 5;                // 扇形火球数量
        public float orbSpeed = 12f;
        public float orbDamage = 15f;

        private bool _isPhase2 = false;
        private float _meleeTimer;
        private float _rangedTimer;
        private SpriteRenderer _sr;
        private bool _originalFlip;

        protected override void OnAwake()
        {
            _sr = GetComponent<SpriteRenderer>();
            if (_sr != null) _originalFlip = _sr.flipX;
            isRanged = false; // 基础近战
            attackInterval = meleeAttackInterval;
            AudioManager.PlayBossRoar(); // 出场吼叫

            // 跳跃移动配置 (2026-09-03: Boss用跳跃移动, 落地冲击=攻击手段)
            hopMovement = true;
            hopArcHeight = 2.5f;
            hopDuration = 0.55f;
            hopPause = 0.45f;
            hopDistance = 6f;
            hopLandDamage = attack;  // 落地伤害=攻击力
            hopLandRadius = 4f;
            hopLandKnockback = 8f;
        }

        protected override void AnimateMovement()
        {
            // Boss bob animation
            _animTimer += Time.deltaTime * 2f;
            float bob = Mathf.Sin(_animTimer) * 0.15f;
            Vector3 pos = transform.position;
            pos.y = _baseY + bob;
            transform.position = pos;

            // Phase 2 visual: red tint pulse
            if (_isPhase2 && _sr != null)
            {
                float pulse = 0.5f + Mathf.Sin(_animTimer * 3f) * 0.2f;
                _sr.color = new Color(1f, 1f - pulse * 0.3f, 1f - pulse * 0.3f, 1f);
            }
        }

        private void Update()
        {
            // Phase transition check
            if (!_isPhase2 && currentHealth <= maxHealth * phase2Threshold)
            {
                EnterPhase2();
            }

            // Ranged attack timer
            if (_isPhase2 || currentHealth < maxHealth * 0.6f)
            {
                _rangedTimer += Time.deltaTime;
                if (_rangedTimer >= rangedAttackInterval)
                {
                    _rangedTimer = 0f;
                    if (_isPhase2)
                        FireFanOrbs();
                    else
                        FireRingOrbs();
                }
            }

            // 跳跃移动 (2026-09-03: Boss用跳跃移动, 落地冲击有攻击性)
            // (注意: 本类Update遮蔽基类Update, 基类移动/分离对本Boss无效, 跳跃状态机在这里显式驱动)
            if (hopMovement)
            {
                if (IsStunned) return; // 被晕眩/麻痹时无法跳跃
                UpdateHopMovement();
            }
            else
            {
                AnimateMovement(); // 原有bob + phase2红光脉动
            }
        }

        private void EnterPhase2()
        {
            _isPhase2 = true;
            moveSpeed *= 1.5f;
            attack = Mathf.CeilToInt(attack * 1.3f);
            meleeAttackInterval *= 0.7f;
            attackInterval = meleeAttackInterval;
            rangedAttackInterval *= 0.8f;

            // Phase 2 visual effect: red burst
            if (_sr != null)
                _sr.color = new Color(1f, 0.7f, 0.7f, 1f);

            AudioManager.PlayBossRoar(); // 狂暴吼叫
            Debug.Log("[Boss] 进入阶段2: 狂暴模式!");
        }

        protected override void OnMeleeAttack()
        {
            // Phase 2 melee attack: explosion around boss
            if (_isPhase2)
            {
                // TODO: 灼烧地面效果
            }
        }

        private void FireRingOrbs()
        {
            // 360度环形火球
            for (int i = 0; i < orbCount; i++)
            {
                float angle = (360f / orbCount) * i * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                SpawnBossOrb(dir, orbDamage);
            }
        }

        private void FireFanOrbs()
        {
            // 朝玩家方向的扇形火球
            if (target == null) return;
            Vector3 toPlayer = target.position - transform.position;
            toPlayer.y = 0;
            toPlayer.Normalize();

            float baseAngle = Mathf.Atan2(toPlayer.z, toPlayer.x);
            float spread = 60f * Mathf.Deg2Rad; // 60度扇形

            for (int i = 0; i < fanCount; i++)
            {
                float t = fanCount == 1 ? 0.5f : (float)i / (fanCount - 1);
                float angle = baseAngle - spread / 2 + spread * t;
                Vector3 dir = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                SpawnBossOrb(dir, orbDamage * 1.2f);
            }
        }

        private void SpawnBossOrb(Vector3 direction, float damage)
        {
            GameObject orbObj = new GameObject("BossOrb");
            orbObj.transform.position = transform.position + new Vector3(0, 2f, 0);
            orbObj.transform.rotation = Quaternion.Euler(65.699f, 0, 0);

            var sr = orbObj.AddComponent<SpriteRenderer>();
            var orbSprite = Resources.Load<Sprite>("EnemyFireball");
            if (orbSprite == null) orbSprite = Resources.Load<Sprite>("SpiritOrb_Drop");
            sr.sprite = orbSprite;
            sr.sortingOrder = 20;
            sr.color = new Color(1f, 0.5f, 0.3f, 1f); // orange-red tint

            // Scale to reasonable size
            if (orbSprite != null)
            {
                float unitW = orbSprite.rect.width / orbSprite.pixelsPerUnit;
                orbObj.transform.localScale = Vector3.one * (2f / unitW);
            }

            var proj = orbObj.AddComponent<EnemyProjectile>();
            proj.velocity = direction * orbSpeed;
            proj.damage = damage;
            proj.lifetime = 5f;
            proj.element = Element.Fire;
        }

        public override void TakeDamage(float damage, Element element = Element.None)
        {
            base.TakeDamage(damage, element);
            // Boss immune to stun (uses ApplyStun internally)
        }
    }
}
