using UnityEngine;
using System.Collections.Generic;

namespace BiopunkRV
{
    /// <summary>
    /// 灵气刃技能 — 无属性基础攻击。
    /// 发射弧形灵气刃，弹射到附近敌人（弹射2次）。
    /// 升级路线 Lv1-Lv10。
    /// </summary>
    public class QiBladeSpell : MonoBehaviour
    {
        [Header("Qi Blade Settings")]
        public Sprite bladeSprite;
        public float fireInterval = 1.2f;
        public float bladeSpeed = 16f;
        public float baseDamageMultiplier = 1.0f;  // 技能伤害倍率 100%
        public float bladeLifetime = 2f;
        public float baseDetectionRange = 12f;     // 技能基础攻击范围
        public float hitRadius = 2.4f;
        public int maxBounces = 2;                 // 弹射次数
        public Element element = Element.None;
        public Vector3 spawnOffset = new Vector3(0, 2f, 0);

        [Header("Visual")]
        public float bladeScale = 0.1f;
        public int sortingOrder = 55;

        [Header("Level System")]
        public int skillLevel = 1;
        public float upgradeDamageMult = 1f;
        public float speedMultiplier = 1f;
        public float sizeMultiplier = 1f;
        public float rangeMultiplier = 1f;
        public float cooldownMult = 1f;
        public int extraBounces = 0;

        [Header("Affix System")]
        public AffixSystem affixSystem;
        public int evolutionCount = 0;
        public string currentDaoId = "";

        private float _fireTimer;
        private List<QiBlade> _activeBlades = new List<QiBlade>();
        private Transform _player;
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

        private void Awake()
        {
            _player = transform;

            // Ensure AffixSystem exists (每技能独立子物体)
            if (affixSystem == null)
                affixSystem = AffixSystem.GetOrCreateFor(this);

            // Load skill data from asset
            var data = SkillLibrary.LoadByName("QiBlade");
            if (data != null)
            {
                baseDamageMultiplier = data.damageMultiplier;
                baseDetectionRange = data.baseRange;
                fireInterval = data.cooldown;
                bladeSpeed = data.projectileSpeed;
                bladeLifetime = data.projectileLifetime;
                bladeScale = data.projectileScale;
                hitRadius = data.damageRadius;
                element = data.element;
            }
        }

        private void Update()
        {
            if (affixSystem != null)
            {
                affixSystem.Tick();
                affixSystem.UpdateAffixes(skillLevel);
            }

            // 属性变化时加载对应VFX帧
            Element currentEl = SpiritRootManager.GetEffectiveElement(element);
            if (currentEl != Element.None && currentEl != _vfxElement)
            {
                _vfxElement = currentEl;
                string folder = GetVfxFolderFor(currentEl);
                if (folder != null)
                {
                    _vfxFrames = Resources.LoadAll<Sprite>(folder);
                    Debug.Log($"[QiBlade] VFX frames loaded: {folder} = {(_vfxFrames != null ? _vfxFrames.Length : 0)}");
                }
            }

            _fireTimer += Time.deltaTime;
            float actualInterval = fireInterval * cooldownMult;
            var psc = PlayerStatsComponent.Instance;
            if (psc != null) actualInterval = psc.stats.GetScaledInterval(actualInterval);

            if (_fireTimer >= actualInterval)
            {
                _fireTimer = 0f;
                TryFire();
            }

            // Update active blades
            for (int i = _activeBlades.Count - 1; i >= 0; i--)
            {
                QiBlade blade = _activeBlades[i];
                blade.Update();

                if (!blade.hit)
                {
                    float effectiveRadius = hitRadius * rangeMultiplier;
                    if (CheckEnemyHit(blade.position, effectiveRadius, blade))
                    {
                        // Hit! Try bounce to next enemy
                        blade.bouncesRemaining--;
                        if (blade.bouncesRemaining >= 0)
                        {
                            // Find next target
                            Transform nextTarget = FindNearestEnemy(blade.position, blade.lastHitEnemy);
                            if (nextTarget != null)
                            {
                                // 弹射瞬间小星爆 (弹向反馈)
                                AffixVisuals.PlayStarPop(blade.position, new Color(0.4f, 0.9f, 0.85f), 0.6f);
                                blade.direction = (nextTarget.position - blade.position).normalized;
                                blade.direction.y = 0;
                                blade.direction.Normalize();
                                blade.hit = false;
                                blade.lastHitEnemy = nextTarget;
                            }
                            else
                            {
                                blade.hit = true;
                            }
                        }
                        else
                        {
                            blade.hit = true;
                        }
                    }
                }

                if (blade.hit || blade.age >= bladeLifetime)
                {
                    if (blade.gameObject != null)
                        Destroy(blade.gameObject);
                    _activeBlades.RemoveAt(i);
                }
            }
        }

        private void TryFire()
        {
            // 坠落词缀 — 灵气刃从天而降
            if (affixSystem != null && affixSystem.fallMode)
            {
                FallFire();
                return;
            }

            Transform target = FindNearestEnemy(_player.position, null);
            if (target == null) return;

            float effectiveRange = baseDetectionRange * rangeMultiplier;
            var psc0 = PlayerStatsComponent.Instance;
            if (psc0 != null) effectiveRange = psc0.CalculateSkillRange(effectiveRange);
            float dist = Vector3.Distance(_player.position, target.position);
            if (dist > effectiveRange) return;

            SpawnBlade(target);
        }

        // ── 坠落词缀: 天降灵气刃 ──

        /// <summary>坠落词缀发射: 灵气刃从天而降砸向敌人</summary>
        private void FallFire()
        {
            var targets = CollectEnemiesByDistance(0);
            if (targets.Count == 0) return;

            int count = 1 + (affixSystem != null ? affixSystem.GetBonusProjectiles() : 0);

            Sprite spr = bladeSprite != null ? bladeSprite : CreateTempSprite();
            float unit = spr.pixelsPerUnit > 0 ? spr.rect.width / spr.pixelsPerUnit : 1f;
            float worldSize = unit * bladeScale * sizeMultiplier;
            float aoeRadius = hitRadius * rangeMultiplier * affixSystem.fallAoeMult * affixSystem.GetRadiusMult();
            Element el = SpiritRootManager.GetEffectiveElement(element);
            Color tint = ElementColors.GetTint(el);

            for (int i = 0; i < count; i++)
            {
                Transform target = targets[i % targets.Count];
                Vector3 pos = target.position + new Vector3(Random.Range(-1.5f, 1.5f), 0f, Random.Range(-1.5f, 1.5f));
                SkyfallStrike.Strike(pos, spr, null, tint, el,
                    worldSize, affixSystem.fallHeight, affixSystem.fallWarnTime, affixSystem.fallFallTime,
                    aoeRadius, FallImpact, FallStyle.Blade, trackTarget: target);
            }
        }

        /// <summary>收集按距离升序的敌人</summary>
        private List<Transform> CollectEnemiesByDistance(int max)
        {
            var list = new List<Transform>();
            var enemies = FindObjectsOfType<EnemyController>();
            foreach (var e in enemies)
                if (e != null && e.gameObject.activeInHierarchy) list.Add(e.transform);
            var rabbits = FindObjectsOfType<GhostRabbitController>();
            foreach (var r in rabbits)
                if (r != null && r.gameObject.activeInHierarchy) list.Add(r.transform);
            list.Sort((a, b) => (a.position - _player.position).sqrMagnitude.CompareTo((b.position - _player.position).sqrMagnitude));
            if (max > 0 && list.Count > max) list.RemoveRange(max, list.Count - max);
            return list;
        }

        /// <summary>坠落落地AoE伤害 (弹射在坠落后不再触发)</summary>
        private void FallImpact(Vector3 pos)
        {
            float radius = hitRadius * rangeMultiplier * affixSystem.fallAoeMult * affixSystem.GetRadiusMult();
            Element el = SpiritRootManager.GetEffectiveElement(element);

            var enemies = FindObjectsOfType<EnemyController>();
            foreach (var enemy in enemies)
            {
                if (enemy == null || !enemy.gameObject.activeInHierarchy) continue;
                float dx = pos.x - enemy.transform.position.x;
                float dz = pos.z - enemy.transform.position.z;
                if (dx * dx + dz * dz >= radius * radius) continue;

                int damage;
                bool isCrit = false;
                var psc = PlayerStatsComponent.Instance;
                float mult = baseDamageMultiplier * upgradeDamageMult * (affixSystem != null ? affixSystem.GetDamageMult() : 1f);
                if (psc != null)
                    damage = psc.CalculateSkillDamage(mult, el, out isCrit);
                else
                    damage = Mathf.Max(1, Mathf.CeilToInt(mult * 5));

                enemy.TakeDamage(damage, el);
                DamageNumber.CreateAt(enemy.transform.position + new Vector3(Random.Range(-0.5f, 0.5f), 0, 0), damage, el, isCrit);
                if (affixSystem != null)
                {
                    affixSystem.OnHitEnemy(enemy.transform, damage, el);
                    if (enemy.currentHealth <= 0) affixSystem.OnKillEnemy();
                }
            }

            var rabbits = FindObjectsOfType<GhostRabbitController>();
            foreach (var rabbit in rabbits)
            {
                if (rabbit == null || !rabbit.gameObject.activeInHierarchy) continue;
                float dx = pos.x - rabbit.transform.position.x;
                float dz = pos.z - rabbit.transform.position.z;
                if (dx * dx + dz * dz >= radius * radius) continue;

                int damage;
                bool isCrit = false;
                var psc = PlayerStatsComponent.Instance;
                float mult = baseDamageMultiplier * upgradeDamageMult * (affixSystem != null ? affixSystem.GetDamageMult() : 1f);
                if (psc != null)
                    damage = psc.CalculateSkillDamage(mult, el, out isCrit);
                else
                    damage = Mathf.Max(1, Mathf.CeilToInt(mult * 5));

                rabbit.TakeDamage(damage, el);
                DamageNumber.CreateAt(rabbit.transform.position + new Vector3(Random.Range(-0.5f, 0.5f), 0, 0), damage, el, isCrit);
                if (affixSystem != null)
                {
                    affixSystem.OnHitEnemy(rabbit.transform, damage, el);
                    if (rabbit.currentHealth <= 0) affixSystem.OnKillEnemy();
                }
            }
        }

        private void SpawnBlade(Transform target)
        {
            Vector3 spawnPos = _player.position + spawnOffset;
            Vector3 direction = (target.position - spawnPos);
            direction.y = 0;
            direction.Normalize();

            GameObject bladeObj = new GameObject("QiBlade");
            bladeObj.transform.position = spawnPos;
            bladeObj.transform.rotation = _player.rotation;
            bladeObj.transform.localScale = new Vector3(bladeScale * sizeMultiplier, bladeScale * sizeMultiplier, 1f);

            SpriteRenderer sr = bladeObj.AddComponent<SpriteRenderer>();
            if (bladeSprite != null)
                sr.sprite = bladeSprite;
            else
                sr.sprite = CreateTempSprite();
            sr.sortingOrder = sortingOrder;
            Element bladeEl = SpiritRootManager.GetEffectiveElement(element);
            sr.color = ElementColors.GetTint(bladeEl);

            QiBlade blade = new QiBlade
            {
                gameObject = bladeObj,
                transform = bladeObj.transform,
                direction = direction,
                speed = bladeSpeed * speedMultiplier,
                bouncesRemaining = maxBounces + extraBounces + (affixSystem != null ? affixSystem.GetBonusBounce() : 0),
                lastHitEnemy = null,
                baseRotX = _player.rotation.eulerAngles.x
            };

            _activeBlades.Add(blade);
            RootMechanics.AttachFireTrail(bladeObj, bladeEl); // 火灵根Lv3+: 火径

            // 属性VFX附加
            if (_vfxFrames != null && _vfxFrames.Length > 0)
            {
                var vfxObj = new GameObject("BladeVFX");
                vfxObj.transform.SetParent(bladeObj.transform, false);
                vfxObj.transform.localPosition = Vector3.zero;
                // 补偿父物体缩放: localScale=主贴图单位宽度/VFX帧单位宽度,
                // 使VFX世界尺寸与刃体一致(不受父物体0.1缩放影响)
                float mainUnit = sr.sprite != null && sr.sprite.pixelsPerUnit > 0 ? sr.sprite.rect.width / sr.sprite.pixelsPerUnit : 1f;
                float vfxUnit = _vfxFrames[0].pixelsPerUnit > 0 ? _vfxFrames[0].rect.width / _vfxFrames[0].pixelsPerUnit : 1f;
                vfxObj.transform.localScale = Vector3.one * (vfxUnit > 0.01f ? mainUnit / vfxUnit : 1f);
                var vfxSr = vfxObj.AddComponent<SpriteRenderer>();
                vfxSr.sprite = _vfxFrames[0];
                vfxSr.sortingOrder = sortingOrder + 1;
                var vfxAnim = vfxObj.AddComponent<VFXFrameAnimator>();
                vfxAnim.frames = _vfxFrames;
                vfxAnim.fps = 12f;
            }
        }

        private bool CheckEnemyHit(Vector3 pos, float radius, QiBlade blade)
        {
            Element el = SpiritRootManager.GetEffectiveElement(element);
            var enemies = FindObjectsOfType<EnemyController>();
            GameObject hitEnemy = null;

            foreach (var enemy in enemies)
            {
                if (enemy == null || !enemy.gameObject.activeInHierarchy) continue;
                if (blade.lastHitEnemy != null && enemy.transform == blade.lastHitEnemy) continue;

                float dx = pos.x - enemy.transform.position.x;
                float dz = pos.z - enemy.transform.position.z;
                if (dx * dx + dz * dz < radius * radius)
                {
                    // Calculate damage
                    int damage;
                    bool isCrit;
                    var psc = PlayerStatsComponent.Instance;
                    if (psc != null)
                        damage = psc.CalculateSkillDamage(baseDamageMultiplier * upgradeDamageMult, el, out isCrit);
                    else
                        damage = Mathf.Max(1, Mathf.CeilToInt(baseDamageMultiplier * upgradeDamageMult * 5));

                    enemy.TakeDamage(damage, el);
                    DamageNumber.CreateAt(enemy.transform.position + new Vector3(Random.Range(-0.5f, 0.5f), 0, 0), damage, el);
                    blade.lastHitEnemy = enemy.transform;
                    hitEnemy = enemy.gameObject;
                    if (affixSystem != null)
                    {
                        affixSystem.OnHitEnemy(enemy.transform, damage, el);
                        if (enemy.currentHealth <= 0) affixSystem.OnKillEnemy();
                    }
                    return true;
                }
            }

            // Also check GhostRabbitController
            if (hitEnemy == null)
            {
                var rabbits = FindObjectsOfType<GhostRabbitController>();
                foreach (var rabbit in rabbits)
                {
                    if (rabbit == null || !rabbit.gameObject.activeInHierarchy) continue;
                    if (blade.lastHitEnemy != null && rabbit.transform == blade.lastHitEnemy) continue;

                    float dx = pos.x - rabbit.transform.position.x;
                    float dz = pos.z - rabbit.transform.position.z;
                    if (dx * dx + dz * dz < radius * radius)
                    {
                        int damage;
                        bool isCrit;
                        var psc = PlayerStatsComponent.Instance;
                        if (psc != null)
                            damage = psc.CalculateSkillDamage(baseDamageMultiplier * upgradeDamageMult, el, out isCrit);
                        else
                            damage = Mathf.Max(1, Mathf.CeilToInt(baseDamageMultiplier * upgradeDamageMult * 5));

                        rabbit.TakeDamage(damage, el);
                        DamageNumber.CreateAt(rabbit.transform.position + new Vector3(Random.Range(-0.5f, 0.5f), 0, 0), damage, el);
                        blade.lastHitEnemy = rabbit.transform;
                        return true;
                    }
                }
            }

            return false;
        }

        private Transform FindNearestEnemy(Vector3 fromPos, Transform exclude)
        {
            Transform nearest = null;
            float nearestDist = baseDetectionRange * rangeMultiplier;

            var enemies = FindObjectsOfType<EnemyController>();
            foreach (var enemy in enemies)
            {
                if (enemy == null || !enemy.gameObject.activeInHierarchy) continue;
                if (exclude != null && enemy.transform == exclude) continue;

                float dx = fromPos.x - enemy.transform.position.x;
                float dz = fromPos.z - enemy.transform.position.z;
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
                if (exclude != null && rabbit.transform == exclude) continue;

                float dx = fromPos.x - rabbit.transform.position.x;
                float dz = fromPos.z - rabbit.transform.position.z;
                float dist = Mathf.Sqrt(dx * dx + dz * dz);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = rabbit.transform;
                }
            }

            return nearest;
        }

        private Sprite _tempSprite;
        private Sprite CreateTempSprite()
        {
            if (_tempSprite == null)
            {
                var tex = Texture2D.whiteTexture;
                _tempSprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
            }
            return _tempSprite;
        }

        // === Level Up System ===
        public void LevelUp()
        {
            skillLevel = Mathf.Min(10, skillLevel + 1);
            ApplyLevelEffects(skillLevel);

            if (skillLevel >= 10 && evolutionCount == 0)
            {
                Element el = SpiritRootManager.GetEffectiveElement(element);
                string[] options = DaoSystem.GetDaoOptions("QiBlade", el);
                if (options.Length == 0) return;
                EnlightenmentChoice.Show(options, selectedDao =>
                {
                    currentDaoId = selectedDao;
                    evolutionCount = 1;
                    skillLevel = 1;
                    baseDamageMultiplier *= 1.5f;
                    baseDetectionRange *= 1.2f;
                    DaoSystem.ApplyDaoEffect(this, currentDaoId);
                    Debug.Log($"[QiBlade] 悟道: {DaoSystem.GetDaoName(currentDaoId)}");
                });
            }
            else if (skillLevel >= 10 && evolutionCount >= 1)
            {
                evolutionCount = 2;
                DaoSystem.TriggerDivine(this, currentDaoId, _player);
                Debug.Log($"[QiBlade] 神通发动: {DaoSystem.GetDaoName("Divine_" + currentDaoId)}");
            }
        }

        private void ApplyLevelEffects(int level)
        {
            switch (level)
            {
                case 2: // 灵气强化
                    upgradeDamageMult *= 1.2f;
                    speedMultiplier *= 1.15f;
                    break;
                case 3: // 二连刃
                    extraBounces += 1;
                    break;
                case 4: // 刃锋
                    upgradeDamageMult *= 1.25f;
                    sizeMultiplier *= 1.2f;
                    break;
                case 5: // 灵气扩散
                    rangeMultiplier *= 1.3f;
                    break;
                case 6: // 三连刃
                    extraBounces += 1;
                    break;
                case 7: // 刃速
                    speedMultiplier *= 1.3f;
                    cooldownMult *= 0.85f;
                    break;
                case 8: // 灵气暴击
                    upgradeDamageMult *= 1.3f;
                    break;
                case 9: // 刃意圆满
                    extraBounces += 2;
                    speedMultiplier *= 1.2f;
                    upgradeDamageMult *= 1.3f;
                    break;
                case 10: // 万刃归一
                    extraBounces += 3;
                    upgradeDamageMult *= 1.5f;
                    cooldownMult *= 0.7f;
                    break;
            }
        }

        private class QiBlade
        {
            public GameObject gameObject;
            public Transform transform;
            public Vector3 direction;
            public float speed;
            public float age;
            public bool hit;
            public int bouncesRemaining;
            public Transform lastHitEnemy;
            public float baseRotX;

            public Vector3 position => transform.position;

            public void Update()
            {
                if (hit) return;
                age += Time.deltaTime;

                Vector3 move = direction * speed * Time.deltaTime;
                move.y = 0;
                transform.position += move;

                // Blade edge faces movement direction (toward enemy)
                // 精灵图0°时刃口朝左(-X)，需要+180°偏移使刃口对准飞行方向
                float angle = Mathf.Atan2(-direction.z, direction.x) * Mathf.Rad2Deg + 180f;
                transform.rotation = Quaternion.Euler(baseRotX, 0f, angle);
            }
        }
    }
}



