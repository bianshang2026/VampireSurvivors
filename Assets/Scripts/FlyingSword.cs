using UnityEngine;
using System.Collections.Generic;

namespace BiopunkRV
{
    /// <summary>
    /// 御剑术 — 飞剑环绕玩家，自动攻击敌人。
    /// Lv1-10升级路线：
    /// Lv1 初窥剑意 | Lv2 御剑 | Lv3 双剑 | Lv4 剑锋 | Lv5 剑影
    /// Lv6 三剑归元 | Lv7 回剑 | Lv8 剑气 | Lv9 剑意圆满 | Lv10 万剑归宗
    /// </summary>
    public class FlyingSword : MonoBehaviour
    {
        [Header("Orbit")]
        public Transform owner;
        public float orbitRadius = 5f;
        public float orbitSpeed = 3f;
        public float heightOffset = 3f;
        public int swordIndex = 0;
        public int totalSwords = 3;
        public float radiusVariance = 1f;

        [Header("Attack")]
        public float baseAttackRange = 6f;      // 技能基础攻击范围
        public float attackSpeed = 16f;
        public float attackCooldown = 2f;
        public float returnSpeed = 12f;
        public float damageRadius = 2.4f;
        public Element element = Element.None;

        [Header("Trail")]
        public float trailTime = 0.15f;
        public float trailStartWidth = 0.06f;
        public Color trailColor = new Color(0.8f, 0.95f, 1f, 0.5f);

        [Header("Level System")]
        public int skillLevel = 1;
        public float damageMultiplier = 1.2f;   // 技能伤害倍率 (asset覆盖)
        public float cooldownMultiplier = 1f;
        public float speedMultiplier = 1f;
        private float _upgradeDamageMult = 1f;  // 升级伤害加成 (ApplyLevelEffects设置)
        public int pierceCount = 0;           // Lv4: 穿透数量
        public float swordShadowChance = 0f;  // Lv5: 剑影触发概率
        public float swordShadowDamage = 0.3f;// Lv5: 剑影伤害比例
        public bool returnDamage = false;     // Lv7: 回剑额外伤害
        public float returnDamageRatio = 0.1f;
        public bool hasSwordQi = false;       // Lv8: 剑气
        public float swordQiDamage = 0.4f;
        public float swordQiRadius = 0f;

        [Header("Affix System")]
        public AffixSystem affixSystem;
        public int evolutionCount = 0;
        public string currentDaoId = "";

        private float _orbitAngle;
        private float _attackTimer;
        private Transform _target;
        private bool _hasHit;
        private float _actualRadius;
        private float _actualHeight;
        private enum State { Orbiting, Attacking, Returning, Skyfalling }
        private State _state = State.Orbiting;
        private TrailRenderer _trail;
        private float _fixedRotX = 60.5f;
        private float _fixedRotZ = 0f;
        private bool _skyfalling; // 坠落词缀天降中(协程控制姿态, LateUpdate跳过)
        private AudioSource _swordHitAudio;
        private int _pierceRemaining;
        private List<Transform> _hitTargets = new List<Transform>();
        private int _affixSwordApplied = 0; // 剑阵词缀已补充数; -1=非主剑(SpawnExtraSword创建)

        private void Awake()
        {
            // Ensure AffixSystem exists (每技能独立子物体)
            if (affixSystem == null)
                affixSystem = AffixSystem.GetOrCreateFor(this);

            // Load skill data from asset
            var data = SkillLibrary.LoadByName("FlyingSword");
            if (data != null)
            {
                damageMultiplier = data.damageMultiplier;
                baseAttackRange = data.baseRange;
                attackCooldown = data.cooldown;
                damageRadius = data.damageRadius;
                element = data.element;
            }

            _trail = gameObject.AddComponent<TrailRenderer>();
            _trail.time = trailTime;
            _trail.startWidth = trailStartWidth;
            _trail.endWidth = 0f;
            // 属性着色
            Element swordEl = SpiritRootManager.GetEffectiveElement(element);
            Color tint = ElementColors.GetTint(swordEl);
            _trail.startColor = new Color(tint.r, tint.g, tint.b, 0.5f);
            _trail.endColor = new Color(tint.r, tint.g, tint.b, 0f);
            _trail.material = new Material(Shader.Find("Sprites/Default"));

            // 飞剑精灵着色
            var srSelf = GetComponent<SpriteRenderer>();
            if (srSelf != null)
            {
                srSelf.color = tint;
                if (srSelf.sprite != null)
                    SkyfallStrike.SwordSprite = srSelf.sprite; // 万剑天降神通贴图
            }

            var player = GameObject.Find("Player");
            if (player != null)
            {
                var sfx = player.transform.Find("SFX_SwordHit");
                if (sfx != null) _swordHitAudio = sfx.GetComponent<AudioSource>();
            }
        }

        private void Start()
        {
            if (owner == null)
            {
                var player = GameObject.Find("Player");
                if (player != null) owner = player.transform;
            }
            _orbitAngle = (Mathf.PI * 2f / totalSwords) * swordIndex;
            _actualRadius = orbitRadius + (swordIndex % 2 == 0 ? radiusVariance : -radiusVariance);
            _actualHeight = heightOffset + (swordIndex * 0.15f);
        }

        private void LateUpdate()
        {
            if (_skyfalling) return; // 坠落词缀: 协程直接控制姿态
            transform.rotation = Quaternion.Euler(_fixedRotX, 0, _fixedRotZ);
        }

        private void Update()
        {
            if (owner == null) return;

            if (affixSystem != null)
            {
                affixSystem.Tick();
                affixSystem.UpdateAffixes(skillLevel);

                // 剑阵/连发词缀 — 补充飞剑 (仅主剑驱动: _affixSwordApplied>=0)
                if (_affixSwordApplied >= 0)
                {
                    int bonus = affixSystem.GetBonusProjectiles();
                    if (bonus > _affixSwordApplied)
                    {
                        while (_affixSwordApplied < bonus)
                        {
                            totalSwords++;
                            SpawnExtraSword();
                            _affixSwordApplied++;
                        }
                    }
                }
            }

            float effectiveCooldown = attackCooldown * cooldownMultiplier;
            var psc = PlayerStatsComponent.Instance;
            if (psc != null) effectiveCooldown = psc.stats.GetScaledInterval(effectiveCooldown);
            _attackTimer -= Time.deltaTime;

            switch (_state)
            {
                case State.Orbiting:
                    OrbitUpdate();
                    if (_attackTimer <= 0) { TryFindTarget(); _attackTimer = effectiveCooldown; }
                    break;
                case State.Attacking:
                    AttackUpdate();
                    break;
                case State.Returning:
                    ReturnUpdate();
                    break;
                case State.Skyfalling:
                    break; // 坠落词缀: 协程驱动
            }
        }

        private void OrbitUpdate()
        {
            _orbitAngle += orbitSpeed * Time.deltaTime;
            float x = Mathf.Cos(_orbitAngle) * _actualRadius;
            float z = Mathf.Sin(_orbitAngle) * _actualRadius;
            Vector3 targetPos = owner.position + new Vector3(x, _actualHeight, z);
            transform.position = Vector3.Lerp(transform.position, targetPos, 8f * Time.deltaTime);
            _fixedRotZ = Mathf.Sin(_orbitAngle) * 10f;
        }

        private float GetEffectiveAttackRange()
        {
            var psc = PlayerStatsComponent.Instance;
            Element el = SpiritRootManager.GetEffectiveElement(element);
            if (psc != null) return psc.CalculateSkillRange(baseAttackRange) * SpiritRootManager.GetRangeMult(el);
            return baseAttackRange;
        }

        private float GetUpgradeDamageMult() => _upgradeDamageMult;

        private void TryFindTarget()
        {
            var allGOs = UnityEngine.Object.FindObjectsOfType<GameObject>();
            float closestDist = GetEffectiveAttackRange();
            Transform closest = null;
            foreach (var go in allGOs)
            {
                if (go == null || !go.activeInHierarchy) continue;
                if (!go.name.StartsWith("Enemy_")) continue;
                float dist = Vector3.Distance(transform.position, go.transform.position);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closest = go.transform;
                }
            }

            if (closest != null)
            {
                _target = closest;
                _hasHit = false;
                _pierceRemaining = pierceCount;
                _hitTargets.Clear();

                // 坠落词缀 — 剑升空后从天而降砸向目标
                if (affixSystem != null && affixSystem.fallMode)
                {
                    _state = State.Skyfalling;
                    StartCoroutine(SkyfallRoutine(closest));
                    return;
                }

                _state = State.Attacking;
            }
        }

        /// <summary>
        /// 坠落词缀天降攻击: 升空到目标头顶 → 预警 → 垂直俯冲 → 范围命中 → 归位。
        /// </summary>
        private System.Collections.IEnumerator SkyfallRoutine(Transform target)
        {
            Vector3 ground = target.position;
            float radius = damageRadius * affixSystem.fallAoeMult * affixSystem.GetRadiusMult();
            Element el = SpiritRootManager.GetEffectiveElement(element);

            // 阶段1: 升空到目标头顶 (剑尖转向下, 参考用户演示姿态)
            _skyfalling = true;
            transform.rotation = SkyfallStrike.SwordFallRotation;
            Vector3 apex = ground + new Vector3(0f, affixSystem.fallHeight, 0f);
            Vector3 start = transform.position;
            const float riseTime = 0.22f;
            float rt = 0f;
            while (rt < riseTime)
            {
                rt += Time.deltaTime;
                float p = Mathf.Clamp01(rt / riseTime);
                transform.position = Vector3.Lerp(start, apex, p);
                if (target != null && target.gameObject.activeInHierarchy)
                    ground = target.position; // 活目标跟随
                yield return null;
            }
            apex = ground + new Vector3(0f, affixSystem.fallHeight, 0f);
            transform.position = apex;

            // 阶段2: 预警圈 (共享SkyfallStrike的预警演出, 追踪活目标)
            SpriteRenderer warnSr;
            float warnUnit;
            GameObject warnGo = SkyfallStrike.CreateWarning(ground, el, out warnSr, out warnUnit);
            yield return SkyfallStrike.PlayWarning(warnGo, warnSr, warnUnit, radius, affixSystem.fallWarnTime, target);

            // 阶段3: 垂直俯冲 (重力加速, 追踪活目标)
            Vector3 diveStart = transform.position;
            Vector3 diveEnd = new Vector3(ground.x, ground.y, ground.z);
            float ft = 0f;
            while (ft < affixSystem.fallFallTime)
            {
                ft += Time.deltaTime;
                float p = Mathf.Clamp01(ft / affixSystem.fallFallTime);
                if (target != null && target.gameObject != null && target.gameObject.activeInHierarchy)
                    diveEnd = target.position; // 追踪: 俯冲过程跟随目标
                transform.position = Vector3.Lerp(diveStart, diveEnd, p * p);
                yield return null;
            }
            transform.position = diveEnd;

            // 阶段4: 落地范围命中 + 视觉
            if (warnGo != null) Destroy(warnGo);
            SkyfallImpact(diveEnd, radius);
            _skyfalling = false; // 恢复LateUpdate轨道姿态

            // 归位 (回环绕轨道)
            var psc = PlayerStatsComponent.Instance;
            _attackTimer = psc != null ? psc.stats.GetScaledInterval(attackCooldown * cooldownMultiplier) : attackCooldown * cooldownMultiplier;
            _state = State.Returning;
        }

        /// <summary>天降落地范围命中 (伤害口径与HitTarget一致)</summary>
        private void SkyfallImpact(Vector3 center, float radius)
        {
            SlashEffect.CreateAt(center, 0);
            if (_swordHitAudio != null) _swordHitAudio.Play();

            int actualDamage;
            bool isCrit = false;
            var psc = PlayerStatsComponent.Instance;
            Element el = SpiritRootManager.GetEffectiveElement(element);
            if (psc != null)
                actualDamage = psc.CalculateSkillDamage(damageMultiplier * GetUpgradeDamageMult() * SpiritRootManager.GetDamageMult(el), el, out isCrit);
            else
                actualDamage = Mathf.Max(1, Mathf.CeilToInt(40f * 0.15f * damageMultiplier));

            _hitTargets.Clear();
            var enemies = FindObjectsOfType<EnemyController>();
            foreach (var e in enemies)
            {
                if (e == null || !e.gameObject.activeInHierarchy) continue;
                if (_hitTargets.Contains(e.transform)) continue;
                float dx = center.x - e.transform.position.x;
                float dz = center.z - e.transform.position.z;
                if (dx * dx + dz * dz >= radius * radius) continue;

                e.TakeDamage((float)actualDamage, el);
                DamageNumber.CreateAt(e.transform.position + new Vector3(Random.Range(-0.5f, 0.5f), 0, 0), actualDamage, el, isCrit);
                _hitTargets.Add(e.transform);
                if (affixSystem != null)
                {
                    affixSystem.OnHitEnemy(e.transform, actualDamage, el);
                    if (e.currentHealth <= 0) affixSystem.OnKillEnemy();
                }
            }

            var rabbits = FindObjectsOfType<GhostRabbitController>();
            foreach (var r in rabbits)
            {
                if (r == null || !r.gameObject.activeInHierarchy) continue;
                if (_hitTargets.Contains(r.transform)) continue;
                float dx = center.x - r.transform.position.x;
                float dz = center.z - r.transform.position.z;
                if (dx * dx + dz * dz >= radius * radius) continue;

                r.TakeDamage((float)actualDamage, el);
                DamageNumber.CreateAt(r.transform.position + new Vector3(Random.Range(-0.5f, 0.5f), 0, 0), actualDamage, el, isCrit);
                _hitTargets.Add(r.transform);
                if (affixSystem != null)
                {
                    affixSystem.OnHitEnemy(r.transform, actualDamage, el);
                    if (r.currentHealth <= 0) affixSystem.OnKillEnemy();
                }
            }

            // 落地视觉: 冲击环+属性VFX+地块残留
            AffixVisuals.PlayBurst(center, el, radius * 1.1f, 0.3f);
            AffixVisuals.PlayVfxBurst(center, el, radius * 1.6f, 0.7f);
            AffixVisuals.PlayGroundPatch(center, el, radius * 0.8f, 1.5f);
        }

        private void AttackUpdate()
        {
            if (_target == null || !_target.gameObject.activeInHierarchy)
            {
                _target = FindNextPierceTarget();
                if (_target == null) { _state = State.Returning; return; }
            }

            // 检查目标是否已死亡
            var targetEnemy = _target.GetComponent<EnemyController>();
            if (targetEnemy != null && targetEnemy.currentHealth <= 0)
            {
                _target = FindNextPierceTarget();
                if (_target == null) { _state = State.Returning; return; }
            }

            Vector3 dir = (_target.position - transform.position);
            dir.y = 0;
            float dist = dir.magnitude;

            if (dist < damageRadius)
            {
                if (!_hitTargets.Contains(_target))
                {
                    HitTarget(_target);
                    _hitTargets.Add(_target);

                    // Pierce to next target
                    if (_pierceRemaining > 0)
                    {
                        _pierceRemaining--;
                        _target = FindNextPierceTarget();
                        if (_target == null) { _state = State.Returning; }
                        return;
                    }

                    _target = null;
                    _state = State.Returning;
                    var psc2 = PlayerStatsComponent.Instance;
                    _attackTimer = psc2 != null ? psc2.stats.GetScaledInterval(attackCooldown * cooldownMultiplier) : attackCooldown * cooldownMultiplier;
                    return;
                }
            }

            transform.position += dir.normalized * attackSpeed * speedMultiplier * Time.deltaTime;
            _fixedRotZ = 0f;

            // 反应网络·风火轮: 飞剑穿过火焰场域时点燃目标 (焚尽链的点火引擎)
            if (targetEnemy != null)
                FireReactions.TryIgniteSwordTarget(transform, targetEnemy);
        }

        private void HitTarget(Transform target)
        {
            SlashEffect.CreateAt(target.position, 0);
            if (_swordHitAudio != null) _swordHitAudio.Play();

            // Calculate damage: (attack×0.15) × skillMultiplier × (upgrade bonuses already in damageMultiplier)
            int actualDamage;
            bool isCrit = false;
            var psc = PlayerStatsComponent.Instance;
            Element el = SpiritRootManager.GetEffectiveElement(element);
            if (psc != null)
                actualDamage = psc.CalculateSkillDamage(damageMultiplier * GetUpgradeDamageMult() * SpiritRootManager.GetDamageMult(el), el, out isCrit);
            else
                actualDamage = Mathf.Max(1, Mathf.CeilToInt(40f * 0.15f * damageMultiplier));

            var rabbit = target.GetComponent<GhostRabbitController>();
            if (rabbit != null) rabbit.TakeDamage((float)actualDamage, el);
            else
            {
                var enemy = target.GetComponent<EnemyController>();
                if (enemy != null) enemy.TakeDamage((float)actualDamage, el);
            }
            
            DamageNumber.CreateAt(target.position + new Vector3(Random.Range(-0.5f, 0.5f), 0, 0), actualDamage, el, isCrit);
            
            // 词缀命中回调
            if (affixSystem != null)
            {
                affixSystem.OnHitEnemy(target, actualDamage, el);
                var ec = target.GetComponent<EnemyController>();
                if (ec != null && ec.currentHealth <= 0) affixSystem.OnKillEnemy();
            }
            
            // 剑影: 额外伤害
            if (swordShadowChance > 0f && Random.value < swordShadowChance)
            {
                int shadowDmg = Mathf.Max(1, Mathf.CeilToInt(actualDamage * swordShadowDamage));
                var rabbit2 = target.GetComponent<GhostRabbitController>();
                if (rabbit2 != null) rabbit2.TakeDamage(shadowDmg);
                else
                {
                    var enemy2 = target.GetComponent<EnemyController>();
                    if (enemy2 != null) enemy2.TakeDamage(shadowDmg);
                }
                DamageNumber.CreateAt(target.position + new Vector3(0.5f, 0.5f, 0), (int)shadowDmg, el);
            }

            // Lv8: 剑气 — 范围伤害
            if (hasSwordQi)
            {
                int qiDmg = Mathf.Max(1, Mathf.CeilToInt(actualDamage * swordQiDamage));
                float qiRadius = damageRadius + swordQiRadius;
                var allEnemies = FindObjectsOfType<EnemyController>();
                foreach (var e in allEnemies)
                {
                    if (e == null || !e.gameObject.activeInHierarchy) continue;
                    if (e.transform == target) continue;
                    float d = Vector3.Distance(target.position, e.transform.position);
                    if (d < qiRadius)
                    {
                        SlashEffect.CreateAt(e.transform.position, 0);
                        e.TakeDamage(qiDmg, el);
                        DamageNumber.CreateAt(e.transform.position + new Vector3(Random.Range(-0.5f, 0.5f), 0, 0), (int)qiDmg, el);
                    }
                }
                var allRabbits = FindObjectsOfType<GhostRabbitController>();
                foreach (var r in allRabbits)
                {
                    if (r == null || !r.gameObject.activeInHierarchy) continue;
                    if (r.transform == target) continue;
                    float d = Vector3.Distance(target.position, r.transform.position);
                    if (d < qiRadius)
                    {
                        SlashEffect.CreateAt(r.transform.position, 0);
                        r.TakeDamage(qiDmg, el);
                        DamageNumber.CreateAt(r.transform.position + new Vector3(Random.Range(-0.5f, 0.5f), 0, 0), (int)qiDmg, el);
                    }
                }
            }
        }

        private Transform FindNextPierceTarget()
        {
            var allGOs = FindObjectsOfType<GameObject>();
            float closestDist = GetEffectiveAttackRange();
            Transform closest = null;
            foreach (var go in allGOs)
            {
                if (go == null || !go.activeInHierarchy) continue;
                if (!go.name.StartsWith("Enemy_")) continue;
                if (_hitTargets.Contains(go.transform)) continue;
                float dist = Vector3.Distance(transform.position, go.transform.position);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closest = go.transform;
                }
            }
            return closest;
        }

        private void ReturnUpdate()
        {
            _orbitAngle += orbitSpeed * Time.deltaTime;
            float x = Mathf.Cos(_orbitAngle) * _actualRadius;
            float z = Mathf.Sin(_orbitAngle) * _actualRadius;
            Vector3 targetPos = owner.position + new Vector3(x, _actualHeight, z);

            // Lv7: 回剑 — 返回途中持续对沿途敌人造成伤害
            if (returnDamage)
            {
                var allEnemies = FindObjectsOfType<EnemyController>();
                foreach (var e in allEnemies)
                {
                    if (e == null || !e.gameObject.activeInHierarchy) continue;
                    if (_hitTargets.Contains(e.transform)) continue;
                    float d = Vector3.Distance(transform.position, e.transform.position);
                    if (d < damageRadius)
                    {
                        int retDmg = Mathf.Max(1, Mathf.CeilToInt(40f * 0.15f * damageMultiplier * returnDamageRatio));
                        SlashEffect.CreateAt(e.transform.position, 0);
                        e.TakeDamage(retDmg);
                        DamageNumber.CreateAt(e.transform.position + new Vector3(Random.Range(-0.5f, 0.5f), 0, 0), retDmg, element);
                        _hitTargets.Add(e.transform);
                    }
                }
                var allRabbits = FindObjectsOfType<GhostRabbitController>();
                foreach (var r in allRabbits)
                {
                    if (r == null || !r.gameObject.activeInHierarchy) continue;
                    if (_hitTargets.Contains(r.transform)) continue;
                    float d = Vector3.Distance(transform.position, r.transform.position);
                    if (d < damageRadius)
                    {
                        int retDmg = Mathf.Max(1, Mathf.CeilToInt(40f * 0.15f * damageMultiplier * returnDamageRatio));
                        SlashEffect.CreateAt(r.transform.position, 0);
                        r.TakeDamage(retDmg);
                        DamageNumber.CreateAt(r.transform.position + new Vector3(Random.Range(-0.5f, 0.5f), 0, 0), retDmg, element);
                        _hitTargets.Add(r.transform);
                    }
                }
            }

            float dist = Vector3.Distance(transform.position, targetPos);
            if (dist < 0.5f)
            {
                _state = State.Orbiting;
                return;
            }

            transform.position = Vector3.Lerp(transform.position, targetPos, returnSpeed * Time.deltaTime);
        }

        /// <summary>
        /// 升级飞剑 — 按Lv1-10路线强化
        /// </summary>
        public void LevelUp()
        {
            skillLevel = Mathf.Min(10, skillLevel + 1);
            ApplyLevelEffects();

            if (skillLevel >= 10 && evolutionCount == 0)
            {
                Element el = SpiritRootManager.GetEffectiveElement(element);
                string[] options = DaoSystem.GetDaoOptions("FlyingSword", el);
                if (options.Length == 0) return;
                EnlightenmentChoice.Show(options, selectedDao =>
                {
                    currentDaoId = selectedDao;
                    evolutionCount = 1;
                    skillLevel = 1;
                    damageMultiplier *= 1.5f;
                    baseAttackRange *= 1.2f;
                    DaoSystem.ApplyDaoEffect(this, currentDaoId);
                    Debug.Log($"[FlyingSword] 悟道: {DaoSystem.GetDaoName(currentDaoId)}");
                });
            }
            else if (skillLevel >= 10 && evolutionCount >= 1)
            {
                evolutionCount = 2;
                DaoSystem.TriggerDivine(this, currentDaoId, owner);
                Debug.Log($"[FlyingSword] 神通发动: {DaoSystem.GetDaoName("Divine_" + currentDaoId)}");
            }
        }

        private void ApplyLevelEffects()
        {
            switch (skillLevel)
            {
                case 2: // 御剑 — 冷却-15% 速度+20%
                    cooldownMultiplier = 0.85f;
                    speedMultiplier = 1.2f;
                    break;
                case 3: // 双剑
                    totalSwords = 2;
                    SpawnExtraSword();
                    break;
                case 4: // 剑锋 — 伤害+20% 穿透+1
                    _upgradeDamageMult = 1.2f;
                    pierceCount = 1;
                    break;
                case 5: // 剑影 — 50%概率额外30%伤害
                    swordShadowChance = 0.5f;
                    break;
                case 6: // 三剑归元
                    totalSwords = 3;
                    SpawnExtraSword();
                    break;
                case 7: // 回剑
                    returnDamage = true;
                    break;
                case 8: // 剑气
                    hasSwordQi = true;
                    swordQiRadius = damageRadius * 0.3f;
                    break;
                case 9: // 剑意圆满 — 攻速+30% 伤害+30% 剑影→80%
                    cooldownMultiplier *= 0.7f;
                    _upgradeDamageMult *= 1.3f;
                    swordShadowChance = 0.8f;
                    break;
                case 10: // 万剑归宗 — +5柄 攻速+50% 伤害+50%
                    totalSwords += 5;
                    for (int i = 0; i < 5; i++) SpawnExtraSword();
                    cooldownMultiplier *= 0.5f;
                    _upgradeDamageMult *= 1.5f;
                    swordShadowDamage *= 1.5f;
                    break;
            }
        }

        public void SpawnExtraSword()
        {
            if (owner == null) return;
            GameObject newSword = new GameObject("FlyingSword_Extra");
            newSword.transform.position = owner.position + Vector3.up * heightOffset;
            FlyingSword fs = newSword.AddComponent<FlyingSword>();
            fs.owner = owner;
            fs.orbitRadius = orbitRadius;
            fs.orbitSpeed = orbitSpeed;
            fs.heightOffset = heightOffset;
            fs.swordIndex = totalSwords - 1;
            fs.totalSwords = totalSwords;
            fs.baseAttackRange = baseAttackRange;
            fs.attackSpeed = attackSpeed;
            fs.attackCooldown = attackCooldown;
            fs.damageMultiplier = damageMultiplier;
            fs.element = element;
            fs.skillLevel = skillLevel;
            fs.damageMultiplier = damageMultiplier;
            fs.cooldownMultiplier = cooldownMultiplier;
            fs.speedMultiplier = speedMultiplier;
            fs.pierceCount = pierceCount;
            fs.swordShadowChance = swordShadowChance;
            fs.swordShadowDamage = swordShadowDamage;
            fs.returnDamage = returnDamage;
            fs.hasSwordQi = hasSwordQi;
            fs.swordQiDamage = swordQiDamage;
            fs.swordQiRadius = swordQiRadius;
            fs._affixSwordApplied = -1; // 非主剑: 不驱动词缀补剑

            // Copy visual components
            var sr = newSword.AddComponent<SpriteRenderer>();
            var origSr = GetComponent<SpriteRenderer>();
            Element extraEl = SpiritRootManager.GetEffectiveElement(element);
            Color extraTint = ElementColors.GetTint(extraEl);
            if (origSr != null) { sr.sprite = origSr.sprite; sr.material = origSr.material; }
            sr.color = extraTint;
            sr.sortingOrder = 60;

            var light = newSword.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = ElementColors.GetGlow(extraEl);
            light.intensity = 1.5f;
            light.range = 2f;

            // 出生弹出动画 (从0.3倍弹到1倍, 可感知的加剑反馈)
            StartCoroutine(SwordScaleIn(newSword.transform, 0.25f));
        }

        /// <summary>剑出生弹出动画 — ease-out从0.3倍到1倍</summary>
        private System.Collections.IEnumerator SwordScaleIn(Transform sword, float dur)
        {
            if (sword == null) yield break;
            Vector3 target = sword.localScale;
            float t = 0f;
            while (t < dur && sword != null)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / dur);
                float s = Mathf.Lerp(0.3f, 1f, 1f - (1f - p) * (1f - p));
                sword.localScale = target * s;
                yield return null;
            }
            if (sword != null) sword.localScale = target;
        }
    }
}


