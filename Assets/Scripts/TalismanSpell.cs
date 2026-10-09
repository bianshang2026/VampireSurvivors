using UnityEngine;
using System.Collections.Generic;

namespace BiopunkRV
{
    /// <summary>
    /// 符箓术技能 — 符箓环绕玩家飞行，自动飞向敌人攻击后消失，一段时间后重新生成。
    /// 类似飞剑环绕模式，但符箓打完消失，需等待重生。
    /// 升级路线 Lv1-Lv10。
    /// </summary>
    public class TalismanSpell : MonoBehaviour
    {
        [Header("Talisman Settings")]
        public Sprite talismanSprite;
        public Sprite symbolSprite;               // 符号精灵(释放虚影用)
        public float baseDamageMultiplier = 1.5f;  // 技能伤害倍率 150%
        public float baseDetectionRange = 10f;     // 技能基础攻击范围
        public float talismanSpeed = 14f;          // 飞向敌人的速度
        public float burstRadius = 3f;             // 命中爆发范围
        public Element element = Element.None;

        [Header("Orbit")]
        public float orbitRadius = 3f;             // 环绕半径
        public float orbitSpeed = 2.5f;            // 环绕角速度
        public float heightOffset = 2f;            // 环绕高度
        public float attackCooldown = 1.8f;        // 每个符箓的攻击间隔
        public float respawnTime = 3f;             // 消失后重生时间

        [Header("Visual")]
        public float talismanScale = 0.08f;
        public int sortingOrder = 55;
        public float burstScale = 2.5f;
        public float burstDuration = 0.4f;

        [Header("Attach Effect")]
        public float attachDuration = 0.5f;       // 附身持续时间
        public float symbolReleaseDuration = 0.4f; // 符号释放动画时间

        [Header("Level System")]
        public int skillLevel = 1;
        public float upgradeDamageMult = 1f;
        public float sizeMultiplier = 1f;
        public float rangeMultiplier = 1f;
        public float cooldownMult = 1f;
        public float speedMultiplier = 1f;
        public int extraTargets = 0;               // 额外符箓数 (升级增加)
        public int pierceCount = 0;                // 穿透(爆发二次伤害)

        [Header("Affix System")]
        public AffixSystem affixSystem;
        public int evolutionCount = 0;
        public string currentDaoId = "";

        private Transform _player;
        private List<TalismanOrbiter> _talismans = new List<TalismanOrbiter>();
        private Sprite _tempSprite;
        private Sprite[] _vfxFrames;
        private Element _vfxElement = Element.None;

        /// <summary>属性→VFX帧目录 (null=无对应帧)</summary>
        private static string GetVfxFolderFor(Element el)
        {
            switch (el)
            {
                case Element.Water: return "WaterVFX_Frames";
                case Element.Wind: return "WindVFX_Frames";
                case Element.Thunder: return "ThunderVFX_Frames";
                case Element.Earth: return "EarthVFX_Frames";
                case Element.Fire: return "FireVFX_Frames";
                case Element.FlameWind: return "FlameWindVFX_Frames";
                case Element.Frost: return "FrostVFX_Frames";
                case Element.Wood: return "WoodVFX_Frames";
                case Element.MetalFusion: return "MetalFusionVFX_Frames";
                default: return null;
            }
        }

        private enum TalismanState { Orbiting, Attacking, Attaching, Releasing, Dead }

        private void Awake()
        {
            _player = transform;

            // Ensure AffixSystem exists (每技能独立子物体)
            if (affixSystem == null)
                affixSystem = AffixSystem.GetOrCreateFor(this);

            var data = SkillLibrary.LoadByName("Talisman");
            if (data != null)
            {
                baseDamageMultiplier = data.damageMultiplier;
                baseDetectionRange = data.baseRange;
                attackCooldown = data.cooldown;
                talismanSpeed = data.projectileSpeed;
                talismanScale = data.projectileScale;
                burstRadius = data.damageRadius;
                element = data.element;
            }
        }

        private void Start()
        {
            int count = 1 + extraTargets + (affixSystem != null ? affixSystem.GetBonusProjectiles() : 0);
            for (int i = 0; i < count; i++)
                SpawnOrbiter(i, count);
        }

        private void Update()
        {
            if (affixSystem != null)
            {
                affixSystem.Tick();
                affixSystem.UpdateAffixes(skillLevel);
            }

            // 属性变化时加载对应VFX帧并挂到已有符箓上
            Element currentEl = SpiritRootManager.GetEffectiveElement(element);
            if (currentEl != Element.None && currentEl != _vfxElement)
            {
                _vfxElement = currentEl;
                string folder = GetVfxFolderFor(currentEl);
                if (folder != null)
                {
                    _vfxFrames = Resources.LoadAll<Sprite>(folder);
                    Debug.Log($"[Talisman] VFX frames loaded: {folder} = {(_vfxFrames != null ? _vfxFrames.Length : 0)}");
                    if (_vfxFrames != null && _vfxFrames.Length > 0)
                    {
                        foreach (var t in _talismans)
                            if (t != null) t.AttachVfx(_vfxFrames);
                    }
                }
            }

            int expected = 1 + extraTargets + (affixSystem != null ? affixSystem.GetBonusProjectiles() : 0);

            // Add new orbiters when extraTargets increases
            while (_talismans.Count < expected)
            {
                int idx = _talismans.Count;
                SpawnOrbiter(idx, expected);
            }

            // Remove excess when extraTargets decreases (shouldn't happen normally)
            while (_talismans.Count > expected)
            {
                int last = _talismans.Count - 1;
                if (_talismans[last] != null && _talismans[last].gameObject != null)
                    Destroy(_talismans[last].gameObject);
                _talismans.RemoveAt(last);
            }

            // Update totalCount on all orbiters (for even spacing)
            for (int i = 0; i < _talismans.Count; i++)
            {
                if (_talismans[i] != null)
                {
                    _talismans[i].totalCount = _talismans.Count;
                    _talismans[i].Update();
                }
            }
        }

        private void OnDestroy()
        {
            foreach (var t in _talismans)
            {
                if (t != null && t.gameObject != null)
                    Destroy(t.gameObject);
            }
            _talismans.Clear();
        }

        private void SpawnOrbiter(int index, int totalCount)
        {
            var orbiter = new TalismanOrbiter();
            orbiter.Init(this, index, totalCount);
            _talismans.Add(orbiter);
        }

        // === Combat ===

        /// <summary>
        /// 引爆 — 对目标周围敌人造成范围伤害
        /// </summary>
        public void Burst(Vector3 pos)
        {
            Element el = SpiritRootManager.GetEffectiveElement(element);
            float radius = burstRadius * sizeMultiplier;

            var allEnemies = FindObjectsOfType<EnemyController>();
            foreach (var enemy in allEnemies)
            {
                if (enemy == null || !enemy.gameObject.activeInHierarchy) continue;
                float dx = pos.x - enemy.transform.position.x;
                float dz = pos.z - enemy.transform.position.z;
                if (dx * dx + dz * dz < radius * radius)
                {
                    int damage;
                    bool isCrit = false;
                    var psc = PlayerStatsComponent.Instance;
                    if (psc != null)
                        damage = psc.CalculateSkillDamage(baseDamageMultiplier * upgradeDamageMult * SpiritRootManager.GetDamageMult(el), el, out isCrit);
                    else
                        damage = Mathf.Max(1, Mathf.CeilToInt(baseDamageMultiplier * upgradeDamageMult * 5));

                    enemy.TakeDamage(damage, el);
                    DamageNumber.CreateAt(enemy.transform.position + new Vector3(Random.Range(-0.3f, 0.3f), 0, 0), damage, el, isCrit);
                }
            }

            // 穿透: 额外命中 (更大范围, 50%伤害)
            if (pierceCount > 0)
            {
                float biggerRadius = radius * 1.4f;
                var psc = PlayerStatsComponent.Instance;
                bool isCrit = false;
                int pierceDmg = psc != null
                    ? psc.CalculateSkillDamage(baseDamageMultiplier * 0.5f * upgradeDamageMult * SpiritRootManager.GetDamageMult(el), el, out isCrit)
                    : Mathf.Max(1, Mathf.CeilToInt(baseDamageMultiplier * 0.5f * upgradeDamageMult * 5));
                foreach (var enemy in allEnemies)
                {
                    if (enemy == null || !enemy.gameObject.activeInHierarchy) continue;
                    float dx = pos.x - enemy.transform.position.x;
                    float dz = pos.z - enemy.transform.position.z;
                    if (dx * dx + dz * dz < biggerRadius * biggerRadius)
                        enemy.TakeDamage(pierceDmg, el);
                }
            }

            SpawnBurstEffect(pos, el);
        }

        private void SpawnBurstEffect(Vector3 pos, Element el)
        {
            GameObject burst = new GameObject("TalismanBurst");
            burst.transform.position = pos + Vector3.up * 1f;
            burst.transform.rotation = Quaternion.Euler(36.08f, 0, 0);

            var burstSr = burst.AddComponent<SpriteRenderer>();
            var circleTex = Resources.Load<Texture2D>("BurstCircle");
            if (circleTex == null)
                circleTex = Texture2D.whiteTexture;
            burstSr.sprite = Sprite.Create(circleTex, new Rect(0, 0, circleTex.width, circleTex.height), new Vector2(0.5f, 0.5f), 100f);
            burstSr.color = GetElementColor();
            burstSr.sortingOrder = 58;

            burst.transform.localScale = new Vector3(burstScale, burstScale, 1f);

            var anim = burst.AddComponent<TalismanBurstAnim>();
            anim.duration = burstDuration;
            Destroy(burst, burstDuration);
        }

        /// <summary>
        /// 符号释放特效 — 符箓消失，符号虚影飞出、上升、淡出
        /// </summary>
        public void SpawnSymbolReleaseEffect(Vector3 pos, float baseScale)
        {
            Sprite sprite = symbolSprite;
            if (sprite == null) { Debug.LogWarning("symbolSprite is null, cannot spawn symbol"); return; }

            // 符号世界尺寸 = 附身符箓的世界尺寸 × 0.7
            float talismanUnitSize = talismanSprite != null ? talismanSprite.bounds.size.x : 20.48f;
            float talismanWorldSize = baseScale * talismanUnitSize;
            float symbolUnitSize = sprite.bounds.size.x;
            float symbolWorldSize = talismanWorldSize * 0.7f;
            float symScale = symbolWorldSize / symbolUnitSize;

            GameObject symObj = new GameObject("TalismanSymbol");
            symObj.transform.position = pos;
            symObj.transform.rotation = Quaternion.Euler(_player.rotation.eulerAngles.x, 0f, 0f);

            var sr = symObj.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = sortingOrder + 15;
            // 虚影效果：半透明白色
            sr.color = new Color(1f, 1f, 1f, 0.9f);

            symObj.transform.localScale = new Vector3(symScale, symScale, 1f);

            var anim = symObj.AddComponent<SymbolReleaseAnim>();
            anim.duration = 1.0f;
            anim.startScale = symScale;
            anim.endScale = symScale * 2.2f;
            anim.riseHeight = 1.5f;
            anim.startAlpha = 0.9f;

            Destroy(symObj, 1.2f);
        }

        public Color GetElementColor()
        {
            Element el = SpiritRootManager.GetEffectiveElement(element);
            return ElementColors.GetTint(el);
        }

        // === Helpers ===

        public Transform FindNearestEnemy()
        {
            Transform nearest = null;
            float nearestDist = GetEffectiveRange();

            var enemies = FindObjectsOfType<EnemyController>();
            foreach (var enemy in enemies)
            {
                if (enemy == null || !enemy.gameObject.activeInHierarchy) continue;
                float dx = _player.position.x - enemy.transform.position.x;
                float dz = _player.position.z - enemy.transform.position.z;
                float dist = Mathf.Sqrt(dx * dx + dz * dz);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = enemy.transform;
                }
            }

            var rabbits = FindObjectsOfType<GhostRabbitController>();
            foreach (var rabbit in rabbits)
            {
                if (rabbit == null || !rabbit.gameObject.activeInHierarchy) continue;
                float dx = _player.position.x - rabbit.transform.position.x;
                float dz = _player.position.z - rabbit.transform.position.z;
                float dist = Mathf.Sqrt(dx * dx + dz * dz);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = rabbit.transform;
                }
            }

            return nearest;
        }

        public float GetEffectiveRange()
        {
            float baseRange = baseDetectionRange * rangeMultiplier;
            var psc = PlayerStatsComponent.Instance;
            if (psc != null) baseRange = psc.CalculateSkillRange(baseRange);
            return baseRange;
        }

        internal Sprite CreateTempSprite()
        {
            if (_tempSprite == null)
            {
                var tex = Texture2D.whiteTexture;
                _tempSprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
            }
            return _tempSprite;
        }

        // === Level Up ===

        public void LevelUp()
        {
            skillLevel = Mathf.Min(10, skillLevel + 1);
            ApplyLevelEffects(skillLevel);

            if (skillLevel >= 10 && evolutionCount == 0)
            {
                Element el = SpiritRootManager.GetEffectiveElement(element);
                string[] options = DaoSystem.GetDaoOptions("Talisman", el);
                if (options.Length == 0) return;
                EnlightenmentChoice.Show(options, selectedDao =>
                {
                    currentDaoId = selectedDao;
                    evolutionCount = 1;
                    skillLevel = 1;
                    baseDamageMultiplier *= 1.5f;
                    baseDetectionRange *= 1.2f;
                    DaoSystem.ApplyDaoEffect(this, currentDaoId);
                    Debug.Log($"[Talisman] 悟道: {DaoSystem.GetDaoName(currentDaoId)}");
                });
            }
            else if (skillLevel >= 10 && evolutionCount >= 1)
            {
                evolutionCount = 2;
                DaoSystem.TriggerDivine(this, currentDaoId, _player);
                Debug.Log($"[Talisman] 神通发动: {DaoSystem.GetDaoName("Divine_" + currentDaoId)}");
            }
        }

        private void ApplyLevelEffects(int level)
        {
            switch (level)
            {
                case 2: // 符力强化
                    upgradeDamageMult *= 1.2f;
                    break;
                case 3: // 双符 — +1符箓
                    extraTargets += 1;
                    break;
                case 4: // 符箓膨胀 — 范围+25%
                    sizeMultiplier *= 1.25f;
                    break;
                case 5: // 灵符穿透
                    pierceCount += 1;
                    break;
                case 6: // 符速 — 飞行+30%, 冷却-15%
                    speedMultiplier *= 1.3f;
                    cooldownMult *= 0.85f;
                    break;
                case 7: // 三符 — +2符箓
                    extraTargets += 2;
                    break;
                case 8: // 符箓范围+35%
                    sizeMultiplier *= 1.35f;
                    break;
                case 9: // 符意圆满 — 伤害+30%, 冷却-10%
                    upgradeDamageMult *= 1.3f;
                    cooldownMult *= 0.9f;
                    break;
                case 10: // 万符归一 — +4符箓, 伤害+50%, 范围+30%
                    extraTargets += 4;
                    upgradeDamageMult *= 1.5f;
                    sizeMultiplier *= 1.3f;
                    break;
            }
        }

        // === Inner Classes ===

        /// <summary>
        /// 符箓环绕体 — 管理单个符箓的环绕/攻击/消失/重生
        /// </summary>
        private class TalismanOrbiter
        {
            public GameObject gameObject;
            public Transform transform;
            public int totalCount;
            public int index;

            private TalismanSpell _spell;
            private SpriteRenderer _sr;
            private SpriteRenderer _vfxSr;
            private TalismanState _state = TalismanState.Orbiting;
            private float _orbitAngle;
            private float _attackTimer;
            private float _respawnTimer;
            private Transform _target;
            private float _baseRotX;
            private float _attachTimer;
            private float _attachStartScale;
            private float _attachTargetScale;
            private Vector3 _attachPos;

            public void Init(TalismanSpell spell, int index, int totalCount)
            {
                _spell = spell;
                this.index = index;
                this.totalCount = totalCount;
                _baseRotX = spell._player.rotation.eulerAngles.x;
                _orbitAngle = (Mathf.PI * 2f / totalCount) * index;
                _attackTimer = spell.attackCooldown * (0.3f + 0.4f * index); // stagger attacks

                gameObject = new GameObject("Talisman_" + index);
                transform = gameObject.transform;

                _sr = gameObject.AddComponent<SpriteRenderer>();
                _sr.sprite = spell.talismanSprite != null ? spell.talismanSprite : spell.CreateTempSprite();
                _sr.sortingOrder = spell.sortingOrder;
                _sr.color = spell.GetElementColor();

                float s = spell.talismanScale * spell.sizeMultiplier;
                transform.localScale = new Vector3(s, s, 1f);

                UpdateOrbitPosition();
            }

            /// <summary>
            /// 挂载属性VFX子物体(灵根激活后调用)。localScale补偿父缩放, 使VFX世界尺寸与符箓一致。
            /// </summary>
            public void AttachVfx(Sprite[] frames)
            {
                if (frames == null || frames.Length == 0 || _sr == null || _sr.sprite == null) return;
                if (_vfxSr == null)
                {
                    var vfxObj = new GameObject("TalismanVFX");
                    vfxObj.transform.SetParent(transform, false);
                    vfxObj.transform.localPosition = Vector3.zero;
                    _vfxSr = vfxObj.AddComponent<SpriteRenderer>();
                    _vfxSr.sortingOrder = _spell.sortingOrder + 1;
                    var anim = vfxObj.AddComponent<VFXFrameAnimator>();
                    anim.fps = 12f;
                    anim.alpha = 0.7f;
                }
                float mainUnit = _sr.sprite.pixelsPerUnit > 0 ? _sr.sprite.rect.width / _sr.sprite.pixelsPerUnit : 1f;
                float vfxUnit = frames[0].pixelsPerUnit > 0 ? frames[0].rect.width / frames[0].pixelsPerUnit : 1f;
                _vfxSr.transform.localScale = Vector3.one * (vfxUnit > 0.01f ? mainUnit / vfxUnit : 1f);
                _vfxSr.sprite = frames[0];
                _vfxSr.enabled = _sr.enabled;
                var vfxAnim = _vfxSr.GetComponent<VFXFrameAnimator>();
                if (vfxAnim != null) vfxAnim.frames = frames;
            }

            public void Update()
            {
                switch (_state)
                {
                    case TalismanState.Orbiting:
                        UpdateOrbit();
                        break;
                    case TalismanState.Attacking:
                        UpdateAttack();
                        break;
                    case TalismanState.Attaching:
                        UpdateAttach();
                        break;
                    case TalismanState.Releasing:
                        UpdateRelease();
                        break;
                    case TalismanState.Dead:
                        UpdateDead();
                        break;
                }
            }

            private void UpdateOrbit()
            {
                _orbitAngle += _spell.orbitSpeed * Time.deltaTime;
                UpdateOrbitPosition();

                _attackTimer -= Time.deltaTime;
                if (_attackTimer <= 0f)
                {
                    float cd = _spell.attackCooldown * _spell.cooldownMult;
                    var psc = PlayerStatsComponent.Instance;
                    if (psc != null) cd = psc.stats.GetScaledInterval(cd);
                    _attackTimer = cd;

                    Transform enemy = _spell.FindNearestEnemy();
                    if (enemy != null)
                    {
                        float range = _spell.GetEffectiveRange();
                        float dx = _spell._player.position.x - enemy.position.x;
                        float dz = _spell._player.position.z - enemy.position.z;
                        if (dx * dx + dz * dz <= range * range)
                        {
                            _target = enemy;
                            _state = TalismanState.Attacking;
                        }
                    }
                }
            }

            private void UpdateOrbitPosition()
            {
                float x = Mathf.Cos(_orbitAngle) * _spell.orbitRadius;
                float z = Mathf.Sin(_orbitAngle) * _spell.orbitRadius;
                transform.position = _spell._player.position + new Vector3(x, _spell.heightOffset, z);
                transform.rotation = Quaternion.Euler(_baseRotX, 0f, 0f);
            }

            private void UpdateAttack()
            {
                // 目标消失或死亡 → 原地引爆消失
                if (_target == null || !_target.gameObject.activeInHierarchy)
                {
                    _spell.Burst(transform.position);
                    Die();
                    return;
                }

                var enemy = _target.GetComponent<EnemyController>();
                if (enemy != null && enemy.currentHealth <= 0)
                {
                    _spell.Burst(transform.position);
                    Die();
                    return;
                }

                Vector3 toTarget = _target.position - transform.position;
                toTarget.y = 0f;
                float dist = toTarget.magnitude;

                // 到达目标 → 进入附身状态
                if (dist < _spell.burstRadius * 0.6f)
                {
                    StartAttach();
                    return;
                }

                // 飞向目标
                Vector3 dir = toTarget.normalized;
                transform.position += dir * _spell.talismanSpeed * _spell.speedMultiplier * Time.deltaTime;
                transform.rotation = Quaternion.Euler(_baseRotX, 0f, 0f);
            }

            /// <summary>
            /// 附身 — 符箓贴合敌人大小缩放
            /// </summary>
            private void StartAttach()
            {
                _state = TalismanState.Attaching;
                _attachTimer = 0f;

                // 计算敌人视觉尺寸
                float enemySize = EstimateEnemySize(_target);
                _attachTargetScale = enemySize * 0.9f;  // 贴合敌人 90%
                _attachStartScale = transform.localScale.x;

                // 贴到敌人身体上
                _attachPos = _target.position + new Vector3(0f, _spell.heightOffset * 0.5f, 0f);
                transform.position = _attachPos;
            }

            private void UpdateAttach()
            {
                _attachTimer += Time.deltaTime;
                float t = _attachTimer / _spell.attachDuration;

                if (_target != null && _target.gameObject.activeInHierarchy)
                {
                    // 跟随敌人位置
                    _attachPos = _target.position + new Vector3(0f, _spell.heightOffset * 0.5f, 0f);
                    transform.position = _attachPos;
                }

                // 缩放贴合
                float s = Mathf.Lerp(_attachStartScale, _attachTargetScale, t);
                transform.localScale = new Vector3(s, s, 1f);
                transform.rotation = Quaternion.Euler(_baseRotX, 0f, 0f);

                if (t >= 1f)
                {
                    StartRelease();
                }
            }

            /// <summary>
            /// 释放符号 — 符箓消失，中央符号飞出
            /// </summary>
            private void StartRelease()
            {
                // 先造成伤害
                _spell.Burst(_target != null ? _target.position : transform.position);

                // 隐藏符箓本体
                if (_sr != null) _sr.enabled = false;
                if (_vfxSr != null) _vfxSr.enabled = false;

                // 生成符号释放特效
                _spell.SpawnSymbolReleaseEffect(transform.position, transform.localScale.x);

                _state = TalismanState.Releasing;
                _attachTimer = 0f;
            }

            private void UpdateRelease()
            {
                _attachTimer += Time.deltaTime;
                if (_attachTimer >= _spell.symbolReleaseDuration)
                {
                    Die();
                }
            }

            /// <summary>
            /// 估算敌人视觉尺寸 (用于符箓缩放贴合)
            /// </summary>
            /// <summary>
            /// 估算敌人视觉尺寸并转换为符箓localScale
            /// </summary>
            private float EstimateEnemySize(Transform enemy)
            {
                // 用 Renderer.bounds 获取敌人世界尺寸 (对 MeshRenderer/SpriteRenderer 都有效)
                float enemyWorldSize = 0f;
                var rends = enemy.GetComponentsInChildren<Renderer>();
                foreach (var r in rends)
                {
                    if (r is TrailRenderer) continue;
                    float s = Mathf.Max(r.bounds.size.x, r.bounds.size.y);
                    if (s > enemyWorldSize) enemyWorldSize = s;
                }
                if (enemyWorldSize <= 0f)
                    enemyWorldSize = 2f; // 兜底

                // 符箓sprite在scale=1时的世界尺寸
                float talismanUnitSize = 1f;
                if (_spell.talismanSprite != null)
                    talismanUnitSize = Mathf.Max(_spell.talismanSprite.bounds.size.x, _spell.talismanSprite.bounds.size.y);

                // localScale = 敌人世界尺寸 / 符箓单位尺寸 × 0.9
                return (enemyWorldSize / talismanUnitSize) * 0.9f;
            }

            private void Die()
            {
                _state = TalismanState.Dead;
                _respawnTimer = _spell.respawnTime * _spell.cooldownMult;
                if (_sr != null) _sr.enabled = false;
                if (_vfxSr != null) _vfxSr.enabled = false;
            }

            private void UpdateDead()
            {
                _respawnTimer -= Time.deltaTime;
                if (_respawnTimer <= 0f)
                {
                    // 重生
                    _state = TalismanState.Orbiting;
                    _attackTimer = 0.5f;
                    if (_sr != null)
                    {
                        _sr.enabled = true;
                        _sr.color = _spell.GetElementColor();
                    }
                    if (_vfxSr != null) _vfxSr.enabled = true;
                    float s = _spell.talismanScale * _spell.sizeMultiplier;
                    transform.localScale = new Vector3(s, s, 1f);
                    _orbitAngle = (Mathf.PI * 2f / totalCount) * index;
                    UpdateOrbitPosition();
                }
            }
        }

        /// <summary>
        /// 爆发特效动画 — 扩散淡出
        /// </summary>
        public class TalismanBurstAnim : MonoBehaviour
        {
            public float duration = 0.4f;
            private Vector3 _start;
            private float _t;

            private void Start()
            {
                _start = transform.localScale;
            }

            private void Update()
            {
                _t += Time.deltaTime / duration;
                if (_t >= 1f)
                {
                    var r = GetComponent<SpriteRenderer>();
                    if (r != null) r.color = new Color(r.color.r, r.color.g, r.color.b, 0);
                    return;
                }
                float scale = Mathf.Lerp(1f, 1.6f, _t);
                transform.localScale = _start * scale;
                var sr = GetComponent<SpriteRenderer>();
                if (sr != null)
                    sr.color = new Color(sr.color.r, sr.color.g, sr.color.b, 1f - _t);
            }
        }

        /// <summary>
        /// 符号释放动画 — 虚影放大+上升+淡出
        /// </summary>
        public class SymbolReleaseAnim : MonoBehaviour
        {
            public float duration = 0.8f;
            public float startScale = 0.5f;
            public float endScale = 1.25f;
            public float riseHeight = 2f;
            public float startAlpha = 0.6f;
            private float _t;
            private Vector3 _startPos;

            private void Start()
            {
                _startPos = transform.position;
            }

            private void Update()
            {
                _t += Time.deltaTime / duration;
                if (_t >= 1f)
                {
                    var sr = GetComponent<SpriteRenderer>();
                    if (sr != null) sr.color = new Color(sr.color.r, sr.color.g, sr.color.b, 0);
                    return;
                }

                float s = Mathf.Lerp(startScale, endScale, _t);
                transform.localScale = new Vector3(s, s, 1f);
                transform.position = _startPos + new Vector3(0f, Mathf.Lerp(0f, riseHeight, _t), 0f);

                // 虚影淡出：前60%保持高透明度，后40%淡出
                var r = GetComponent<SpriteRenderer>();
                if (r != null)
                {
                    float alpha;
                    if (_t < 0.6f)
                        alpha = startAlpha;
                    else
                        alpha = startAlpha * (1f - (_t - 0.6f) / 0.4f);
                    r.color = new Color(r.color.r, r.color.g, r.color.b, alpha);
                }
            }
        }
    }
}



