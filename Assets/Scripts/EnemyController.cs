using UnityEngine;

namespace BiopunkRV
{
    /// <summary>
    /// Base enemy controller with movement, health, hit flash, and health bar.
    /// Override AnimateMovement() for custom animation (hop, float, etc).
    /// </summary>
    public abstract class EnemyController : MonoBehaviour
    {
        [Header("Movement")]
        public float moveSpeed = 7f;
        public float detectRange = 9999f;
        public float stopDistance = 2.4f;
        public float separationRadius = 3f;
        public float separationForce = 3f;
        public float obstacleCheckRadius = 1.6f;
        public Transform target;

        [Header("Health")]
        public float maxHealth = 20f;
        public float currentHealth = 20f;

        [Header("Combat")]
        public float attack = 5f;              // 攻击力
        public float defense = 20f;           // 防御
        public Element element = Element.None; // 属性
        public float elementDefense = 0f;      // 属性防御
        public bool isRanged = false;          // 是否远程攻击
        public float attackInterval = 2f;      // 攻击间隔
        public bool isElite = false;           // 精英怪(必掉灵石+碎片)
        public float attackRange = 16f;         // 攻击范围
        public GameObject projectilePrefab;     // 远程攻击投射物
        public float projectileSpeed = 10f;

        private float _attackTimer;

        // 减速状态 (水灵根)
        private float _enemySlowFactor = 1f;   // 当前移速倍率 (1=正常)
        private float _enemySlowTimer;
        private GameObject _slowVfx;           // 减速水滴特效实例
        private static Sprite[] _slowVfxFrames; // 减速水滴序列帧(全体敌人共享缓存)

        // 晕眩/麻痹状态 (土灵根晕眩 / 雷灵根麻痹)
        private bool _isStunned;
        private float _stunTimer;
        private GameObject _stunVfx;           // 麻痹闪电特效实例
        private static Sprite[] _stunVfxFrames; // 麻痹闪电序列帧(全体敌人共享缓存)

        // 燃烧状态
        private bool _isBurning;
        private int _burnStacks;
        private float _burnDamagePerSecond;
        private float _burnTimer;
        private float _burnDuration;
        private GameObject _burnEffect;
        private BurnSpark[] _sparks;
        private float _sparkSpawnTimer;
        private float _sparkSize;
        private float _sparkRadius;

        // 火花数据结构（用 SpriteRenderer 代替 ParticleSystem，保证可见性）
        private struct BurnSpark
        {
            public GameObject obj;
            public SpriteRenderer sr;
            public Vector3 velocity;
            public float life;
            public float maxLife;
            public bool active;
        }

        protected Material _mat;
        protected Vector3 _baseScale;
        protected float _baseY;
        protected bool _facingLeft;
        protected bool _isMoving;
        protected bool _isAttacking;
        protected float _animTimer;
        protected float _collisionRadius = 0.5f;
        protected Transform _healthBar;
        protected Transform _fillTransform;
        protected static EnemyController[] _allEnemies;
        private static float _allEnemiesRefreshTimer;

        private void Awake()
        {
            var mr = GetComponent<MeshRenderer>();
            if (mr != null && mr.material != null) _mat = mr.material;
            var sr = GetComponent<SpriteRenderer>();
            if (sr != null && sr.material != null) _mat = sr.material;
            
            _baseScale = transform.localScale;
            _baseY = transform.position.y;

            EnsurePhysicsBody();

            OnAwake();
        }

        protected virtual void OnAwake() { }

        private void Start()
        {
            if (target == null)
            {
                var player = GameObject.Find("Player");
                if (player != null) target = player.transform;
            }

            // Find health bar child
            for (int i = 0; i < transform.childCount; i++)
            {
                if (transform.GetChild(i).name == "HealthBar")
                {
                    _healthBar = transform.GetChild(i);
                    for (int j = 0; j < _healthBar.childCount; j++)
                    {
                        Transform child = _healthBar.GetChild(j);
                        if (child.name.StartsWith("Fill"))
                        {
                            _fillTransform = child;
                            child.localRotation = Quaternion.identity;
                            break;
                        }
                    }
                    break;
                }
            }
            OnStart();
        }

        protected virtual void OnStart() { }

        private void Update()
        {
            if (_mat == null) return;

            // Refresh enemy list periodically (new spawns join, dead entries are skipped in loops)
            // 原bug: 只在列表空时刷新一次, 后续刷出的怪永远不进分离列表 → 大量重叠
            _allEnemiesRefreshTimer -= Time.deltaTime;
            if (_allEnemies == null || _allEnemies.Length == 0 || _allEnemiesRefreshTimer <= 0f)
            {
                _allEnemiesRefreshTimer = 1f;
                _allEnemies = FindObjectsOfType<EnemyController>();
            }

            _facingLeft = false;
            _isMoving = false;

            // 减速水滴特效同步 (减速中显示, 减速结束销毁)
            if (_enemySlowTimer > 0f)
            {
                if (_slowVfx == null) EnsureSlowVfx();
            }
            else if (_slowVfx != null)
            {
                Destroy(_slowVfx);
                _slowVfx = null;
            }

            // 麻痹闪电特效同步 (晕眩/麻痹中显示, 结束销毁)
            if (_isStunned)
            {
                if (_stunVfx == null) EnsureStunVfx();
            }
            else if (_stunVfx != null)
            {
                Destroy(_stunVfx);
                _stunVfx = null;
            }

            // 晕眩/麻痹倒计时
            if (_isStunned)
            {
                _stunTimer -= Time.deltaTime;
                if (_stunTimer <= 0f)
                {
                    _isStunned = false;
                    _stunTimer = 0f;
                }
            }

            // 被晕眩时跳过移动和攻击
            if (_isStunned) return;

            // 跳跃移动: 状态机推进; 空中时跳过追击/分离/近战 (落地后恢复)
            bool airborne = false;
            if (hopMovement)
                airborne = UpdateHopMovement();

            if (!airborne && target != null)
            {
                Vector3 toTarget = target.position - transform.position;
                toTarget.y = 0f;
                float dist = toTarget.magnitude;

                Vector3 moveDir = Vector3.zero;

                if (dist > stopDistance && dist < detectRange && !hopMovement)
                {
                    // 远程怪在攻击范围内停下，不再靠近
                    if (isRanged && dist <= attackRange)
                        _isMoving = false;
                    else
                    {
                        moveDir = toTarget.normalized;
                        _isMoving = true;
                    }
                }

                // Separation from other enemies (soft push + hard de-overlap)
                if (_allEnemies != null)
                {
                    foreach (var other in _allEnemies)
                    {
                        if (other == this || other == null || !other.gameObject.activeInHierarchy) continue;
                        Vector3 toOther = transform.position - other.transform.position;
                        toOther.y = 0f;
                        float d = toOther.magnitude;
                        if (d < separationRadius && d > 0.01f)
                            moveDir += toOther.normalized * (separationForce * (1f - d / separationRadius));
                    }
                }

                if (moveDir.sqrMagnitude > 0.01f)
                {
                    moveDir = moveDir.normalized;
                    float actualSpeed = moveSpeed;
                    if (_enemySlowTimer > 0f)
                    {
                        _enemySlowTimer -= Time.deltaTime;
                        if (_enemySlowTimer <= 0f) _enemySlowFactor = 1f;
                        actualSpeed *= _enemySlowFactor;
                    }
                    Vector3 newPos = transform.position + moveDir * actualSpeed * Time.deltaTime;

                    Vector3 tryX = new Vector3(newPos.x, transform.position.y, transform.position.z);
                    if (!CheckObstacle(tryX)) transform.position = tryX;
                    Vector3 tryZ = new Vector3(transform.position.x, transform.position.y, newPos.z);
                    if (!CheckObstacle(tryZ)) transform.position = tryZ;

                    _facingLeft = moveDir.x < -0.01f;
                }
            }

            // Hard separation: always prevent enemy overlap based on collision radius
            // (跳跃空中时跳过 — 空中不推挤, 落地后恢复)
            if (!airborne && _allEnemies != null)
            {
                Vector3 pushSum = Vector3.zero;
                int pushCount = 0;
                foreach (var other in _allEnemies)
                {
                    if (other == this || other == null || !other.gameObject.activeInHierarchy) continue;
                    Vector3 toOther = transform.position - other.transform.position;
                    toOther.y = 0f;
                    float d = toOther.magnitude;
                    float minDist = _collisionRadius + other._collisionRadius;
                    if (d < minDist && d > 0.001f)
                    {
                        // 推完整重叠量，不分半
                        Vector3 push = toOther / d * (minDist - d);
                        pushSum.x += push.x;
                        pushSum.z += push.z;
                        pushCount++;
                    }
                    else if (d <= 0.001f)
                    {
                        // 完全重叠时随机方向推开
                        Vector3 randomPush = new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f)).normalized * minDist;
                        pushSum += randomPush;
                        pushCount++;
                    }
                }
                if (pushSum.sqrMagnitude > 0.0001f)
                {
                    // 平均推力（避免多怪围攻时推力叠加过大）
                    if (pushCount > 1) pushSum /= pushCount;
                    // 加大推力倍数确保生效
                    pushSum *= 1.5f;
                    Vector3 newPos = transform.position + pushSum * Time.deltaTime * 20f; // 快速推开
                    Vector3 tryX = new Vector3(newPos.x, transform.position.y, transform.position.z);
                    if (!CheckObstacle(tryX)) transform.position = tryX;
                    Vector3 tryZ = new Vector3(transform.position.x, transform.position.y, newPos.z);
                    if (!CheckObstacle(tryZ)) transform.position = tryZ;
                }
            }

            // Melee attack (before animation so AnimateMovement sees _isAttacking)
            // (跳跃空中时跳过近战 — 落地冲击即攻击)
            if (!airborne && !isRanged && target != null)
            {
                Vector3 toTargetXZ = target.position - transform.position;
                toTargetXZ.y = 0f;
                float distXZ = toTargetXZ.magnitude;
                _attackTimer += Time.deltaTime;
                if (distXZ <= stopDistance + 1f && _attackTimer >= attackInterval)
                {
                    _attackTimer = 0f;
                    _isAttacking = true;
                    DoMeleeAttack();
                }
            }

            // Custom animation (hop, float, etc)
            // (跳跃空中时跳过 — 空中姿态由跳跃状态机驱动)
            if (!airborne) AnimateMovement();

            // 燃烧持续伤害
            UpdateBurn();

            // Ranged attack
            if (isRanged && target != null)
            {
                float distToTarget = Vector3.Distance(transform.position, target.position);
                _attackTimer += Time.deltaTime;
                if (distToTarget <= attackRange && _attackTimer >= attackInterval)
                {
                    _attackTimer = 0f;
                    FireProjectile();
                }
            }

            // Update health bar fill
            if (_healthBar != null)
            {
                float healthPercent = currentHealth / maxHealth;

                if (_fillTransform != null)
                {
                    _fillTransform.localScale = new Vector3(healthPercent, 1f, 1f);
                    _fillTransform.localPosition = new Vector3(-(1f - healthPercent) * 0.5f, 0f, 0f);
                }

                _healthBar.gameObject.SetActive(healthPercent < 0.999f);
            }
        }

        // ── 跳跃移动 (通用组件: 任意怪 hopMovement=true 即用跳跃代替行走) ──
        [Header("跳跃移动 (HopMovement)")]
        public bool hopMovement = false;          // 开启后移动方式变为跳跃
        public float hopArcHeight = 1.8f;         // 弧顶高度
        public float hopDuration = 0.45f;         // 每跳时长
        public float hopPause = 0.2f;             // 落地停顿
        public float hopDistance = 4.5f;          // 每跳水平距离
        public bool hopLandsDamage = true;        // 落地冲击: 对周围造成伤害+击退玩家
        public float hopLandDamage = 8f;          // 落地伤害
        public float hopLandRadius = 3f;          // 落地AoE半径
        public float hopLandKnockback = 6f;       // 落地击退玩家力度
        protected bool _hopping;                  // 空中状态
        protected float _hopTimer;
        protected Vector3 _hopFrom, _hopTo;
        protected float _hopSquashT;              // 落地压扁恢复计时

        /// <summary>跳跃移动状态机 (hopMovement=true时由Update调用)。返回true=空中(跳过分离/近战)</summary>
        protected bool UpdateHopMovement()
        {
            if (target == null) return false;
            _hopTimer += Time.deltaTime;

            if (_hopping)
            {
                // 空中: 抛物线弧顶 + 水平位移已锁定
                float p = Mathf.Clamp01(_hopTimer / hopDuration);
                float arc = Mathf.Sin(p * Mathf.PI) * hopArcHeight;
                Vector3 pos = Vector3.LerpUnclamped(_hopFrom, _hopTo, p);
                pos.y = _baseY + arc;
                transform.position = pos;
                _isMoving = true;
                _facingLeft = (_hopTo.x - _hopFrom.x) < -0.01f;

                // 空中拉伸
                float stretch = Mathf.Sin(p * Mathf.PI) * 0.15f;
                Vector3 scale = _baseScale;
                scale.y *= 1f + stretch;
                transform.localScale = scale;

                if (p >= 1f)
                {
                    // 落地
                    _hopping = false;
                    _hopTimer = 0f;
                    transform.position = new Vector3(_hopTo.x, _baseY, _hopTo.z);
                    _hopSquashT = 0.15f; // 落地压扁恢复
                    if (hopLandsDamage) HopLandImpact();
                }
                return true; // 空中
            }
            else
            {
                // 地面停顿: 恢复缩放 → 蓄力下一跳
                if (_hopSquashT > 0f)
                {
                    _hopSquashT -= Time.deltaTime;
                    float k = Mathf.Max(0f, _hopSquashT / 0.15f);
                    Vector3 scale = _baseScale;
                    scale.y *= 1f - k * 0.2f; // 压扁
                    transform.localScale = scale;
                }

                if (_hopTimer >= hopPause && _hopSquashT <= 0f)
                {
                    // 瞄准玩家跳过去; 已在攻击距离内则原地不动(走近战逻辑)
                    Vector3 dir = target.position - transform.position;
                    dir.y = 0f;
                    float d = dir.magnitude;
                    if (d > stopDistance)
                    {
                        dir.Normalize();
                        _hopFrom = transform.position;
                        _hopTo = transform.position + dir * hopDistance;
                        _hopping = true;
                        _hopTimer = 0f;
                        _facingLeft = dir.x < -0.01f;
                    }
                    else
                    {
                        _hopTimer = 0f; // 已贴脸, 停顿循环等待近战
                    }
                }
                return false; // 地面
            }
        }

        /// <summary>落地冲击: AoE伤害周围敌人(不含自己)+击退玩家</summary>
        protected void HopLandImpact()
        {
            var psc = PlayerStatsComponent.Instance;
            // 对玩家: 范围内造成伤害+击退
            if (target != null)
            {
                float d = Vector3.Distance(transform.position, target.position);
                if (d <= hopLandRadius + 1f)
                {
                    if (psc != null && !psc.godMode)
                        psc.stats.TakeDamage(hopLandDamage, element != Element.None);
                    var cc = target.GetComponent<CharacterController2D>();
                    if (cc != null)
                        cc.ApplyKnockback(target.position - transform.position, hopLandKnockback);
                }
            }
            // 对其他敌人: 无伤害 (仅演出)
            AffixVisuals.PlayBurst(transform.position, element != Element.None ? element : Element.Thunder, hopLandRadius, 0.25f);
        }

        /// <summary>
        /// Override for custom movement animation (hop, float, wobble, etc).
        /// Use _isMoving, _animTimer, _baseScale, _baseY, _facingLeft.
        /// </summary>
        protected abstract void AnimateMovement();

        public virtual void TakeDamage(float damage)
        {
            TakeDamage(damage, Element.None);
        }

        public virtual void TakeDamage(float damage, Element attackElement)
        {
            if (currentHealth <= 0) return; // 已死亡，不再受伤

            // Lv7 焚心 — 灼烧目标受到额外伤害
            if (_isBurning && FireSpiritRoot.IsActive)
                damage *= (1f + FireSpiritRoot.Instance.burnBonusDamage);

            // Lv7 深水 — 减速目标受到额外伤害
            if (IsSlowed && WaterSpiritRoot.IsActive)
                damage *= (1f + WaterSpiritRoot.Instance.slowBonusDamage);

            // 防御减免: defense × 0.1 (和玩家公式一致)
            // 金融合破甲: 无视50%防御
            float defFactor = ElementFusionSystem.GetDefensePenaltyFactor(attackElement);
            float defReduction = defense * 0.1f * defFactor;
            
            // 元素防御减免: elementDefense × 0.1, 只对与怪物自身属性相同的攻击生效
            float elemDefReduction = 0f;
            if (attackElement != Element.None && attackElement == element && elementDefense > 0f)
            {
                elemDefReduction = elementDefense * 0.1f;
            }
            
            // 最终伤害 = max(1, 原伤害 - 防御减免 - 元素防御减免)
            float finalDamage = Mathf.Max(1f, damage - defReduction - elemDefReduction);
            int actualDamage = Mathf.Max(1, Mathf.CeilToInt(finalDamage));
            
            currentHealth = Mathf.Max(0, currentHealth - actualDamage);

            // 火灵根灼烧触发
            if (FireSpiritRoot.ShouldBurn(attackElement, element))
            {
                var fsr = FireSpiritRoot.Instance;
                _burnStacks = Mathf.Min(_burnStacks + 1, fsr.burnMaxStacks);
                var psc = PlayerStatsComponent.Instance;
                float baseAtk = psc != null ? psc.stats.GetBaseAttackDamage() : 6f;
                _burnDamagePerSecond = Mathf.Max(1, baseAtk * fsr.burnDpsRatio * fsr.burnDamageMult * _burnStacks);
                _burnDuration = fsr.burnDuration;
                if (!_isBurning) { _isBurning = true; _burnTimer = 0f; UpdateBurnEffect(true); }
            }

            // 水灵根减速触发
            if (WaterSpiritRoot.ShouldSlow(attackElement))
            {
                var wsr = WaterSpiritRoot.Instance;
                ApplySlow(wsr.GetEffectiveSlowFactor(), wsr.slowDuration);
            }

            // 融合属性命中效果 (炎=灼烧/冰=冻结/草=回血/金=破甲已并入伤害计算)
            if (attackElement >= Element.FlameWind)
            {
                ElementFusionSystem.OnHitFusionEffect(this, attackElement);
            }

            // Hit flash (红闪0.15s — SpriteRenderer用color直接tint, MeshRenderer走材质_Color)
            var hitSr = GetComponent<SpriteRenderer>();
            if (hitSr != null)
            {
                hitSr.color = new Color(1f, 0.35f, 0.35f, 1f);
                CancelInvoke(nameof(ResetHitColor));
                Invoke(nameof(ResetHitColor), 0.15f);
            }
            else if (_mat != null)
            {
                _mat.SetColor("_Color", new Color(1f, 0.3f, 0.3f, 1f));
                CancelInvoke(nameof(ResetHitColor));
                Invoke(nameof(ResetHitColor), 0.15f);
            }
            AudioManager.PlayEnemyHit();

            if (_healthBar != null)
            {
                _healthBar.gameObject.SetActive(true);
                if (_fillTransform != null)
                {
                    float pct = currentHealth / maxHealth;
                    _fillTransform.localScale = new Vector3(pct, 1f, 1f);
                    _fillTransform.localPosition = new Vector3(-(1f - pct) * 0.5f, 0f, 0f);
                }
            }

            if (currentHealth <= 0)
            {
                // Lv5 爆炎 — 灼烧敌人死亡时爆炸
                if (_isBurning && FireSpiritRoot.IsActive && FireSpiritRoot.Instance.burnExplode)
                {
                    ExplodeOnDeath();
                }
                SpawnDeathEffect();
                SpawnSpiritOrb(transform.position);
                DropSystem.OnEnemyDeath(transform.position, isElite);
                ReactionBus.NotifyEnemyDied(this, _isBurning, Element.Fire); // 反应网络: 灼烧死亡→火焰场域
                AudioManager.PlayEnemyDeath();
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// 对敌人施加减速效果（水灵根用）。
        /// factor=0.5表示移速降为50%，duration持续秒数。
        /// 如果可叠加，取更低的factor；否则取更长的duration。
        /// </summary>
        public void ApplySlow(float factor, float duration)
        {
            if (factor < _enemySlowFactor)
            {
                _enemySlowFactor = factor;
                _enemySlowTimer = duration;
            }
            else if (_enemySlowTimer < duration)
            {
                _enemySlowTimer = duration;
            }
        }

        /// <summary>
        /// 当前是否被减速
        /// </summary>
        public bool IsSlowed => _enemySlowTimer > 0f;

        /// <summary>
        /// 创建减速水滴特效 — 参考灼烧火花: 多个小水滴序列帧分布在怪物身体周围,
        /// 水滴尺寸和分布半径都按怪物体型缩放
        /// </summary>
        private void EnsureSlowVfx()
        {
            if (_slowVfx != null) return;
            var frames = LoadStatusFrames("WaterSlowVFX_Frames", ref _slowVfxFrames);
            if (frames == null) return;
            _slowVfx = BuildStatusVfx("SlowVFX", frames, 0.45f, 0.35f, 4, 0.85f);
        }

        /// <summary>
        /// 创建麻痹闪电特效 — 同减速水滴, 紫色闪电帧分布在怪物身体周围
        /// </summary>
        private void EnsureStunVfx()
        {
            if (_stunVfx != null) return;
            var frames = LoadStatusFrames("ThunderVFX_Frames", ref _stunVfxFrames);
            if (frames == null) return;
            _stunVfx = BuildStatusVfx("StunVFX", frames, 0.45f, 0.35f, 4, 0.85f);
        }

        /// <summary>加载状态特效帧(静态缓存, 按名字排序)</summary>
        private static Sprite[] LoadStatusFrames(string folder, ref Sprite[] cache)
        {
            if (cache != null) return cache;
            var loaded = Resources.LoadAll<Sprite>(folder);
            if (loaded == null || loaded.Length == 0) return null;
            System.Array.Sort(loaded, (a, b) => string.CompareOrdinal(a.name, b.name));
            cache = loaded;
            return cache;
        }

        /// <summary>
        /// 在怪物身体周围分布式创建多个序列帧特效子物体(参考灼烧火花布局)。
        /// dropletSizeFactor=单个特效世界尺寸占体型比例, radiusFactor=分布半径占体型比例。
        /// </summary>
        private GameObject BuildStatusVfx(string name, Sprite[] frames, float dropletSizeFactor, float radiusFactor, int count, float alpha)
        {
            var container = new GameObject(name);
            container.transform.SetParent(transform, false);
            container.transform.localPosition = Vector3.zero;
            container.transform.localRotation = Quaternion.identity;

            float body = 3f;
            var rend = GetComponentInChildren<Renderer>();
            if (rend != null) body = Mathf.Max(rend.bounds.size.x, rend.bounds.size.y);
            float radius = body * radiusFactor;
            float parentScale = Mathf.Max(0.0001f, transform.lossyScale.x);
            float frameUnit = frames[0].pixelsPerUnit > 0 ? frames[0].rect.width / frames[0].pixelsPerUnit : 2.56f;
            float dropletLocal = frameUnit > 0.01f ? (body * dropletSizeFactor / frameUnit) / parentScale : 1f;

            int order = 20;
            var mySr = GetComponent<SpriteRenderer>();
            if (mySr != null) order = mySr.sortingOrder + 1;

            for (int i = 0; i < count; i++)
            {
                float ang = (360f / count) * i * Mathf.Deg2Rad + Mathf.PI / 4f;
                Vector3 worldOff = new Vector3(Mathf.Cos(ang) * radius, Mathf.Sin(ang) * radius, 0f);
                var d = new GameObject(name + "_" + i);
                d.transform.SetParent(container.transform, false);
                d.transform.localPosition = worldOff / parentScale;
                d.transform.localScale = Vector3.one * dropletLocal;
                var sr = d.AddComponent<SpriteRenderer>();
                sr.sortingOrder = order;
                var anim = d.AddComponent<VFXFrameAnimator>();
                anim.frames = frames;
                anim.fps = 10f;
                anim.loop = true;
                anim.alpha = alpha;
                anim.startFrame = i * 4; // 错开相位
            }
            return container;
        }

        /// <summary>
        /// 施加晕眩/麻痹 (土灵根晕眩 / 雷灵根麻痹)
        /// </summary>
        public void ApplyStun(float duration)
        {
            if (duration > _stunTimer)
            {
                _isStunned = true;
                _stunTimer = duration;
            }
        }

        /// <summary>
        /// 当前是否被晕眩
        /// </summary>
        public bool IsStunned => _isStunned;

        /// <summary>
        /// 当前是否灼烧 (词缀系统查询用)
        /// </summary>
        public bool IsBurning => _isBurning;

        /// <summary>
        /// 击退 (土灵根击退)
        /// </summary>
        public void ApplyKnockback(Vector3 direction, float force)
        {
            Vector3 knockbackPos = transform.position + direction.normalized * force;
            knockbackPos.y = transform.position.y; // 保持Y不变
            if (!CheckObstacle(new Vector3(knockbackPos.x, transform.position.y, transform.position.z)))
                transform.position = new Vector3(knockbackPos.x, transform.position.y, transform.position.z);
        }

        private void UpdateBurnEffect(bool start)
        {
            if (start)
            {
                if (_burnEffect == null)
                {
                    _burnEffect = new GameObject("BurnEffect");
                    _burnEffect.transform.SetParent(transform, false);
                    _burnEffect.transform.localPosition = Vector3.zero;

                    // 根据怪物体型决定火花大小（比怪物小很多）
                    float bodySize = Mathf.Max(_baseScale.x, _baseScale.y);
                    _sparkSize = bodySize * 0.15f;     // 单个火花尺寸
                    _sparkRadius = bodySize * 0.35f;    // 火花飞溅范围

                    // 微弱橙色光源
                    Light fireLight = _burnEffect.AddComponent<Light>();
                    fireLight.type = LightType.Point;
                    fireLight.color = new Color(1f, 0.4f, 0.1f);
                    fireLight.intensity = 3f;
                    fireLight.range = bodySize * 2.5f;

                    // 创建火花贴图
                    var sparkTex = Resources.Load<Texture2D>("BurnSpark");
#if UNITY_EDITOR
                    if (sparkTex == null)
                        sparkTex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/BurnSpark.png");
#endif
                    Sprite sparkSprite;
                    if (sparkTex != null)
                        sparkSprite = Sprite.Create(sparkTex, new Rect(0, 0, sparkTex.width, sparkTex.height), new Vector2(0.5f, 0.5f), sparkTex.height);
                    else
                    {
                        sparkTex = new Texture2D(16, 16, TextureFormat.RGBA32, false);
                        float c = 7.5f;
                        var px = new Color32[256];
                        for (int y = 0; y < 16; y++)
                            for (int x = 0; x < 16; x++)
                            {
                                float dx = x - c, dy = y - c;
                                float d = Mathf.Sqrt(dx * dx + dy * dy) / c;
                                byte a = (byte)(Mathf.Clamp01(1f - d) * 255);
                                px[y * 16 + x] = new Color32(255, 255, 255, a);
                            }
                        sparkTex.SetPixels32(px);
                        sparkTex.Apply();
                        sparkSprite = Sprite.Create(sparkTex, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f), 16f);
                    }

                    // 创建火花池（用 SpriteRenderer 保证可见）
                    int poolSize = 8;
                    _sparks = new BurnSpark[poolSize];
                    for (int i = 0; i < poolSize; i++)
                    {
                        var go = new GameObject("Spark_" + i);
                        go.transform.SetParent(_burnEffect.transform, false);
                        go.transform.localPosition = Vector3.zero;
                        go.transform.localScale = Vector3.one * _sparkSize;
                        go.SetActive(false);

                        var sr = go.AddComponent<SpriteRenderer>();
                        sr.sprite = sparkSprite;
                        sr.sortingOrder = 75;
                        sr.color = new Color(1f, 0.6f, 0.15f, 1f);

                        _sparks[i] = new BurnSpark
                        {
                            obj = go,
                            sr = sr,
                            velocity = Vector3.zero,
                            life = 0f,
                            maxLife = 0.8f,
                            active = false
                        };
                    }
                }
                _burnEffect.SetActive(true);
                _sparkSpawnTimer = 0f;
            }
            else
            {
                if (_burnEffect != null)
                    _burnEffect.SetActive(false);
            }
        }

        private void UpdateBurn()
        {
            if (!_isBurning) return;

            _burnTimer += Time.deltaTime;

            // 每秒造成燃烧伤害
            if (_burnTimer >= 1f)
            {
                _burnTimer -= 1f;
                int burnDmg = Mathf.Max(1, Mathf.CeilToInt(_burnDamagePerSecond));
                currentHealth = Mathf.Max(0, currentHealth - burnDmg);
                DamageNumber.CreateAt(transform.position + new Vector3(Random.Range(-0.5f, 0.5f), 0.5f, 0), burnDmg, Element.Fire);

                if (currentHealth <= 0)
                {
                    if (FireSpiritRoot.IsActive && FireSpiritRoot.Instance.burnExplode)
                        ExplodeOnDeath();
                    SpawnDeathEffect();
                    SpawnSpiritOrb(transform.position);
                    Destroy(gameObject);
                    return;
                }
            }

            // 燃烧结束
            if (_burnDuration <= 0)
            {
                _isBurning = false;
                _burnStacks = 0;
                UpdateBurnEffect(false);
            }
            _burnDuration -= Time.deltaTime;

            // 更新火花动画
            UpdateSparks();
        }

        private void UpdateSparks()
        {
            if (_sparks == null) return;

            // 定时生成新火花
            _sparkSpawnTimer += Time.deltaTime;
            float spawnInterval = 0.08f;  // 每0.08秒生成一个火花
            if (_sparkSpawnTimer >= spawnInterval)
            {
                _sparkSpawnTimer = 0f;
                SpawnSpark();
            }

            // 更新已激活的火花
            for (int i = 0; i < _sparks.Length; i++)
            {
                if (!_sparks[i].active) continue;

                var s = _sparks[i];
                s.life += Time.deltaTime;
                float t = s.life / s.maxLife;

                if (t >= 1f)
                {
                    s.active = false;
                    s.obj.SetActive(false);
                    _sparks[i] = s;
                    continue;
                }

                // 移动（受重力影响）
                s.velocity.y -= 6f * Time.deltaTime;  // 重力
                s.obj.transform.position += s.velocity * Time.deltaTime;

                // 缩小
                float scale = _sparkSize * (1f - t * 0.7f);
                s.obj.transform.localScale = Vector3.one * scale;

                // 颜色渐变：亮橙→暗红→透明
                Color c;
                if (t < 0.5f)
                    c = Color.Lerp(new Color(1f, 0.8f, 0.2f, 1f), new Color(1f, 0.3f, 0.05f, 0.9f), t * 2f);
                else
                    c = Color.Lerp(new Color(1f, 0.3f, 0.05f, 0.9f), new Color(0.5f, 0.1f, 0f, 0f), (t - 0.5f) * 2f);
                s.sr.color = c;

                _sparks[i] = s;
            }
        }

        private void SpawnSpark()
        {
            // 找到未激活的火花
            for (int i = 0; i < _sparks.Length; i++)
            {
                if (!_sparks[i].active)
                {
                    var s = _sparks[i];
                    s.active = true;
                    s.life = 0f;

                    // 随机起始位置（在怪物周围）
                    Vector3 offset = new Vector3(
                        Random.Range(-_sparkRadius, _sparkRadius),
                        Random.Range(0f, _sparkRadius * 0.5f),
                        Random.Range(-_sparkRadius, _sparkRadius));
                    s.obj.transform.position = transform.position + offset;
                    s.obj.transform.localScale = Vector3.one * _sparkSize;

                    // 向上飞溅的速度
                    s.velocity = new Vector3(
                        Random.Range(-2f, 2f),
                        Random.Range(3f, 5f),
                        Random.Range(-2f, 2f));

                    s.sr.color = new Color(1f, 0.8f, 0.2f, 1f);
                    s.obj.SetActive(true);
                    _sparks[i] = s;
                    return;
                }
            }
        }

        protected void ResetHitColor()
        {
            var hitSr = GetComponent<SpriteRenderer>();
            if (hitSr != null) hitSr.color = Color.white;
            else if (_mat != null)
                _mat.SetColor("_Color", Color.white);
        }

        /// <summary>
        /// 根据Renderer bounds自动添加碰撞体，用于怪物间碰撞检测。
        /// </summary>
        private void EnsurePhysicsBody()
        {
            Renderer rend = GetComponent<Renderer>();
            if (rend == null) rend = GetComponentInChildren<Renderer>();
            float radius = 1.5f; // 默认较大碰撞半径
            if (rend != null)
            {
                var bounds = rend.bounds;
                float worldW = bounds.size.x;
                float worldH = bounds.size.z;
                radius = Mathf.Max(worldW, worldH) * 0.5f; // 0.5倍世界尺寸 (2026-09-02: 0.6太大, 玩家离怪可视边缘太远就被挡)
                radius = Mathf.Clamp(radius, 0.8f, 4.5f);
            }
            _collisionRadius = radius;

            if (GetComponent<Collider>() == null)
            {
                var sc = gameObject.AddComponent<SphereCollider>();
                sc.radius = radius / Mathf.Max(Mathf.Abs(_baseScale.x), Mathf.Abs(_baseScale.y), 0.01f);
                sc.isTrigger = false;
            }
        }

        protected virtual bool CheckObstacle(Vector3 position)
        {
            Collider[] hits = Physics.OverlapSphere(position, obstacleCheckRadius);
            foreach (var hit in hits)
            {
                if (hit == null || hit.transform == transform) continue;
                // 装饰物障碍(DecoSpawner生成, 除骨头外): 怪不能穿过 (2026-09-02)
                if (hit.GetComponentInParent<DecoObstacle>() != null) return true;
                // 场景保存的树木 (旧名字过滤)
                if (hit.name.StartsWith("Deco_SpiritTree") || hit.name.StartsWith("Deco_RareTree") ||
                    hit.name.StartsWith("Deco_GoldenTree") || hit.name.StartsWith("Deco_TreasureTree") ||
                    hit.name.StartsWith("Deco_TreeFill") || hit.name.StartsWith("Deco_RockLarge"))
                    return true;
            }
            return false;
        }

        protected virtual void FireProjectile()
        {
            if (target == null) return;

            Vector3 dir = target.position - transform.position;
            dir.y = 0f;
            dir.Normalize();

            GameObject proj = new GameObject("EnemyProjectile");
            proj.transform.position = transform.position + Vector3.up * 0.5f;

            SpriteRenderer sr = proj.AddComponent<SpriteRenderer>();
            sr.color = element == Element.Fire ? new Color(1f, 0.4f, 0.1f) : Color.white;
            sr.sortingOrder = 55;

            // Fireball sprite
            var fireballTex = Resources.Load<Texture2D>("EnemyFireball");
            if (fireballTex != null)
                sr.sprite = Sprite.Create(fireballTex, new Rect(0, 0, fireballTex.width, fireballTex.height), new Vector2(0.5f, 0.5f), fireballTex.height);
            else
                sr.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
            proj.transform.localScale = new Vector3(1.5f, 1.5f, 1f);

            // 底部对向目标（和玩家火球一致）
            float angle = Mathf.Atan2(dir.z, dir.x) * Mathf.Rad2Deg + 90f;
            proj.transform.rotation = Quaternion.Euler(65.69891f, 0, angle);

            Light glow = proj.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = sr.color;
            glow.intensity = 1.5f;
            glow.range = 2f;

            EnemyProjectile ep = proj.AddComponent<EnemyProjectile>();
            ep.velocity = dir * projectileSpeed;
            ep.damage = attack;
            ep.element = element;
            ep.lifetime = 15f;
            Destroy(proj, 15f);
        }

        /// <summary>
        /// 近战攻击：对玩家造成伤害，子类可重写 OnMeleeAttack 播放攻击动画。
        /// </summary>
        protected virtual void DoMeleeAttack()
        {
            if (target == null) return;
            var psc = target.GetComponent<PlayerStatsComponent>();
            if (psc != null && !psc.godMode)
            {
                bool isElemental = element != Element.None;
                psc.stats.TakeDamage(attack, isElemental);
            }
            OnMeleeAttack();
        }

        /// <summary>
        /// 近战攻击动画钩子，子类重写以播放攻击帧/特效。
        /// </summary>
        protected virtual void OnMeleeAttack() { }

        protected virtual void SpawnSpiritOrb(Vector3 pos)
        {
            var orbGO = new GameObject("SpiritOrb");
            orbGO.transform.position = pos;
            orbGO.transform.rotation = Quaternion.Euler(77.38111f, 0, 0);
            orbGO.transform.localScale = new Vector3(0.4f, 0.4f, 1f);

            var sr = orbGO.AddComponent<SpriteRenderer>();
            var tex = Resources.Load<Texture2D>("SpiritOrb_Drop");
            if (tex != null)
                sr.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            sr.color = Color.white;
            sr.sortingOrder = 50;

            var light = orbGO.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.36f, 0.72f, 0.63f);
            light.intensity = 1.5f;
            light.range = 3f;

            orbGO.AddComponent<SpiritOrbPickup>();
            Destroy(orbGO, 30f);
        }

        /// <summary>
        /// Lv5 爆炎 — 灼烧敌人死亡时产生小范围爆炸。
        /// Lv8 炎域 — 爆炸时传播灼烧给附近敌人。
        /// </summary>
        private void ExplodeOnDeath()
        {
            var fsr = FireSpiritRoot.Instance;
            if (fsr == null) return;

            float radius = fsr.explodeRadius * fsr.explodeRadiusMult;
            var psc = PlayerStatsComponent.Instance;
            float baseAtk = psc != null ? psc.stats.GetBaseAttackDamage() : 6f;
            int explodeDmg = Mathf.Max(1, Mathf.CeilToInt(baseAtk * fsr.explodeDamageRatio));

            // 爆炸视觉效果
            GameObject boom = new GameObject("BurnExplosion");
            boom.transform.position = transform.position;
            boom.transform.rotation = Quaternion.Euler(90f, 0, 0);
            var boomSR = boom.AddComponent<SpriteRenderer>();
            boomSR.color = new Color(1f, 0.5f, 0.1f, 0.6f);
            boomSR.sortingOrder = 45;
            boomSR.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
            boom.transform.localScale = new Vector3(radius * 2, radius * 2, 1f);

            Light boomLight = boom.AddComponent<Light>();
            boomLight.type = LightType.Point;
            boomLight.color = new Color(1f, 0.5f, 0.1f);
            boomLight.intensity = 5f;
            boomLight.range = radius * 3f;
            Destroy(boom, 0.3f);

            // 对范围内敌人造成伤害
            var allEnemies = FindObjectsOfType<EnemyController>();
            foreach (var e in allEnemies)
            {
                if (e == null || !e.gameObject.activeInHierarchy || e == this) continue;
                float d = Vector3.Distance(transform.position, e.transform.position);
                if (d < radius)
                {
                    e.TakeDamage(explodeDmg, Element.Fire);

                    // Lv8 炎域 — 灼烧传播
                    if (fsr.burnSpread && e.element != Element.Fire)
                    {
                        e.ApplyBurnFromSpread();
                    }
                }
            }

            var allRabbits = FindObjectsOfType<GhostRabbitController>();
            foreach (var r in allRabbits)
            {
                if (r == null || !r.gameObject.activeInHierarchy || r == this) continue;
                float d = Vector3.Distance(transform.position, r.transform.position);
                if (d < radius)
                {
                    r.TakeDamage(explodeDmg, Element.Fire);
                    if (fsr.burnSpread && r.element != Element.Fire)
                        r.ApplyBurnFromSpread();
                }
            }
        }

        /// <summary>
        /// 被灼烧传播感染（Lv8 炎域）
        /// </summary>
        public void ApplyBurnFromSpread()
        {
            var fsr = FireSpiritRoot.Instance;
            if (fsr != null)
            {
                _burnStacks = Mathf.Min(_burnStacks + 1, fsr.burnMaxStacks);
                var psc = PlayerStatsComponent.Instance;
                float baseAtk = psc != null ? psc.stats.GetBaseAttackDamage() : 6f;
                _burnDamagePerSecond = Mathf.Max(1, baseAtk * fsr.burnDpsRatio * fsr.burnDamageMult * _burnStacks);
                _burnDuration = fsr.burnDuration;
            }
            else
            {
                // 调试模式：无火灵根时使用默认参数
                _burnStacks = 1;
                var psc = PlayerStatsComponent.Instance;
                float baseAtk = psc != null ? psc.stats.GetBaseAttackDamage() : 6f;
                _burnDamagePerSecond = Mathf.Max(1, baseAtk * 0.06f);
                _burnDuration = 3f;
            }
            if (!_isBurning) { _isBurning = true; _burnTimer = 0f; UpdateBurnEffect(true); }
        }

        /// <summary>
        /// 炎融合灼烧 (ElementFusionSystem调用, 不依赖火灵根)
        /// </summary>
        public void ApplyBurnFromFusion(float dpsRatio, float duration)
        {
            var psc = PlayerStatsComponent.Instance;
            float baseAtk = psc != null ? psc.stats.GetBaseAttackDamage() : 6f;
            _burnStacks = 1;
            _burnDamagePerSecond = Mathf.Max(1, baseAtk * dpsRatio);
            _burnDuration = duration;
            if (!_isBurning) { _isBurning = true; _burnTimer = 0f; UpdateBurnEffect(true); }
        }

        /// <summary>
        /// 死亡特效：怪物碎裂成碎片向外飞散 + 灵气粒子上升消散
        /// </summary>
        private static Sprite _deathParticleSprite;

        /// <summary>调试：仅播放死亡特效，不杀死怪物</summary>
        public void TriggerDeathEffectDebug()
        {
            SpawnDeathEffect();
        }

        /// <summary>调试：强制杀死怪物</summary>
        public void ForceKill()
        {
            currentHealth = 0;
            if (_isBurning && FireSpiritRoot.IsActive && FireSpiritRoot.Instance.burnExplode)
                ExplodeOnDeath();
            SpawnDeathEffect();
            SpawnSpiritOrb(transform.position);
            DropSystem.OnEnemyDeath(transform.position, isElite);
            ReactionBus.NotifyEnemyDied(this, _isBurning, Element.Fire); // 反应网络: 灼烧死亡→火焰场域
            AudioManager.PlayEnemyDeath();
            Destroy(gameObject);
        }

        protected void SpawnDeathEffect()
        {
            Vector3 pos = transform.position;
            float bodySize = Mathf.Max(_baseScale.x, _baseScale.y) * 1.5f;
            Texture2D tex = null;
            if (_mat != null && _mat.mainTexture != null)
                tex = _mat.mainTexture as Texture2D;

            // 加载灵气粒子贴图（缓存）
            if (_deathParticleSprite == null)
            {
                var particleTex = Resources.Load<Texture2D>("DeathParticle");
#if UNITY_EDITOR
                if (particleTex == null)
                    particleTex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/TJGenerators/History/Sprite_20260810_200531.png");
#endif
                if (particleTex != null)
                    _deathParticleSprite = Sprite.Create(particleTex, new Rect(0, 0, particleTex.width, particleTex.height), new Vector2(0.5f, 0.5f), particleTex.height);
            }
            Sprite particleSprite = _deathParticleSprite;
            if (particleSprite == null)
                particleSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);

            // 创建独立的特效容器，不挂在怪物身上（怪物即将被Destroy）
            GameObject effectHolder = new GameObject("DeathEffect");
            effectHolder.transform.position = pos;

            // --- 碎片：5块从怪物身上飞出，带旋转和重力，0.6s内淡出 ---
            int shardCount = 5;
            if (tex != null)
            {
                int halfW = tex.width / 2;
                int halfH = tex.height / 2;
                Sprite[] shardSprites = new Sprite[4];
                shardSprites[0] = Sprite.Create(tex, new Rect(0, halfH, halfW, halfH), new Vector2(0.5f, 0.5f), tex.height);
                shardSprites[1] = Sprite.Create(tex, new Rect(halfW, halfH, halfW, halfH), new Vector2(0.5f, 0.5f), tex.height);
                shardSprites[2] = Sprite.Create(tex, new Rect(0, 0, halfW, halfH), new Vector2(0.5f, 0.5f), tex.height);
                shardSprites[3] = Sprite.Create(tex, new Rect(halfW, 0, halfW, halfH), new Vector2(0.5f, 0.5f), tex.height);

                for (int i = 0; i < shardCount; i++)
                {
                    Sprite s = shardSprites[i % 4];
                    GameObject shard = new GameObject("DeathShard_" + i);
                    shard.transform.position = pos + new Vector3(Random.Range(-0.2f, 0.2f), Random.Range(0f, 0.3f), 0);
                    shard.transform.rotation = transform.rotation;
                    float shardScale = bodySize * 0.4f;
                    shard.transform.localScale = new Vector3(shardScale, shardScale, 1f);

                    var sr = shard.AddComponent<SpriteRenderer>();
                    sr.sprite = s;
                    sr.sortingOrder = 65;
                    sr.color = Color.white;

                    AnimateShardAsync(shard, new Vector3(
                        Random.Range(-3f, 3f), Random.Range(2f, 5f), Random.Range(-3f, 3f)));
                }
            }

            // --- 灵气粒子：8个小光点向上飘升+淡出 ---
            int particleCount = 8;
            for (int i = 0; i < particleCount; i++)
            {
                GameObject particle = new GameObject("DeathParticle_" + i);
                particle.transform.position = pos + new Vector3(
                    Random.Range(-bodySize * 0.3f, bodySize * 0.3f),
                    Random.Range(0f, bodySize * 0.3f),
                    Random.Range(-bodySize * 0.2f, bodySize * 0.2f));
                particle.transform.rotation = Quaternion.Euler(90f, 0, 0);
                particle.transform.localScale = Vector3.one * bodySize * 0.15f;

                var sr = particle.AddComponent<SpriteRenderer>();
                sr.sprite = particleSprite;
                sr.sortingOrder = 70;
                sr.color = new Color(0.6f, 0.9f, 1f, 0.8f);

                AnimateParticleAsync(particle, sr, Random.Range(1.5f, 3f), bodySize * 0.3f);
            }

            // --- 光环：一圈扩散的光圈 ---
            GameObject ring = new GameObject("DeathRing");
            ring.transform.position = pos;
            ring.transform.rotation = Quaternion.Euler(90f, 0, 0);
            var ringSR = ring.AddComponent<SpriteRenderer>();
            ringSR.sprite = particleSprite;
            ringSR.color = new Color(0.7f, 0.95f, 1f, 0.5f);
            ringSR.sortingOrder = 60;
            AnimateRingAsync(ring, ringSR, bodySize * 2.5f);

            // 自动清理整个特效容器
            Destroy(effectHolder, 2.0f);
        }

        private void AnimateShardAsync(GameObject shard, Vector3 velocity)
        {
            var runner = shard.AddComponent<DeathEffectRunner>();
            runner.Run(AnimateShardCoroutine(shard, velocity));
        }

        private System.Collections.IEnumerator AnimateShardCoroutine(GameObject shard, Vector3 velocity)
        {
            float duration = 1.1f;
            float elapsed = 0f;
            Vector3 rotSpeed = new Vector3(0, 0, Random.Range(-360f, 360f));
            var sr = shard.GetComponent<SpriteRenderer>();
            Color startColor = sr.color;
            Vector3 startScale = shard.transform.localScale;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                velocity.y -= 8f * Time.deltaTime;
                shard.transform.position += velocity * Time.deltaTime;
                shard.transform.Rotate(rotSpeed * Time.deltaTime, Space.Self);

                float s = 1f - t * 0.5f;
                shard.transform.localScale = startScale * s;
                sr.color = new Color(startColor.r, startColor.g, startColor.b, 1f - t);

                yield return null;
            }
            Destroy(shard);
        }

        private void AnimateParticleAsync(GameObject particle, SpriteRenderer sr, float riseSpeed, float driftRange)
        {
            var runner = particle.AddComponent<DeathEffectRunner>();
            runner.Run(AnimateParticleCoroutine(particle, sr, riseSpeed, driftRange));
        }

        private System.Collections.IEnumerator AnimateParticleCoroutine(GameObject particle, SpriteRenderer sr, float riseSpeed, float driftRange)
        {
            float duration = 1.3f;
            float elapsed = 0f;
            Vector3 drift = new Vector3(Random.Range(-driftRange, driftRange), 0, Random.Range(-driftRange * 0.5f, driftRange * 0.5f));
            Color startColor = sr.color;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                particle.transform.position += new Vector3(
                    drift.x * Time.deltaTime * 0.3f,
                    riseSpeed * Time.deltaTime,
                    drift.z * Time.deltaTime * 0.3f);

                float scaleT = t < 0.3f ? t / 0.3f : 1f - (t - 0.3f) / 0.7f;
                float s = 0.1f * (0.5f + scaleT * 0.8f);
                particle.transform.localScale = Vector3.one * s * 10f;
                sr.color = new Color(startColor.r, startColor.g, startColor.b, (1f - t) * 0.8f);

                yield return null;
            }
            Destroy(particle);
        }

        private void AnimateRingAsync(GameObject ring, SpriteRenderer sr, float targetScale)
        {
            var runner = ring.AddComponent<DeathEffectRunner>();
            runner.Run(AnimateRingCoroutine(ring, sr, targetScale));
        }

        private System.Collections.IEnumerator AnimateRingCoroutine(GameObject ring, SpriteRenderer sr, float targetScale)
        {
            float duration = 0.9f;
            float elapsed = 0f;
            Color startColor = sr.color;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                float s = Mathf.Lerp(0.1f, targetScale, t);
                ring.transform.localScale = new Vector3(s, s, 1f);
                sr.color = new Color(startColor.r, startColor.g, startColor.b, (1f - t) * 0.5f);

                yield return null;
            }
            Destroy(ring);
        }
    }
}
