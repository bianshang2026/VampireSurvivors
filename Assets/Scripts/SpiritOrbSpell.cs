using UnityEngine;
using System.Collections.Generic;

namespace BiopunkRV
{
    /// <summary>
    /// 灵力弹技能 — 无属性基础攻击。
    /// 自动发射灵气球追踪敌人，可升级提升数量/伤害/速度/分裂等。
    /// 升级路线 Lv1-Lv10，对应不同强化阶段。
    /// Lv10满级后触发悟道（道系统），技能替换为新功法。
    /// </summary>
    public class SpiritOrbSpell : MonoBehaviour
    {
        [Header("Spirit Orb Settings")]
        public Sprite orbSprite;
        public float fireInterval = 2f;
        public float orbSpeed = 12f;
        public float baseDamageMultiplier = 1.3f;  // 技能伤害倍率 130%
        public float orbLifetime = 3f;
        public float baseDetectionRange = 8f;      // 技能基础攻击范围
        public float hitRadius = 2.4f;
        public Element element = Element.None;
        public Vector3 spawnOffset = new Vector3(0, 2f, 0);

        [Header("Visual")]
        public float orbScale = 0.3f;
        public int sortingOrder = 55;
        public float trailTime = 0.2f;
        public Color trailColor = new Color(0.85f, 0.95f, 1f, 0.4f);
        public Color glowColor = new Color(0.8f, 0.9f, 1f, 1f);

        [Header("Level System")]
        public int skillLevel = 1;
        public int orbCount = 1;
        public float upgradeDamageMult = 1f;  // 升级伤害加成 (ApplyLevelEffects设置)
        public float speedMultiplier = 1f;
        public float sizeMultiplier = 1f;
        public float rangeMultiplier = 1f;
        public bool canSplit = false;
        public float splitDamageRatio = 0.3f;
        public float shockRadius = 0f;

        [Header("Affix System")]
        public AffixSystem affixSystem;
        public int evolutionCount = 0;  // 悟道次数 (0=基础, 1=道进化, 2=神通)
        public string currentDaoId = "";  // 当前道的ID

        private float _fireTimer;
        private List<SpiritOrb> _activeOrbs = new List<SpiritOrb>();
        private Transform _player;
        private Sprite[] _thunderFrames;
        private bool _thunderFramesLoaded;
        private Sprite[] _waterFrames;
        private bool _waterFramesLoaded;
        private Sprite _waterOrbSprite;
        private Sprite _windOrbSprite;
        private Sprite _flameWindOrbSprite;
        private Sprite _frostOrbSprite;
        private Sprite _woodOrbSprite;
        private Sprite _metalFusionOrbSprite;
        private Sprite[] _vfxFrames;
        private Element _vfxElement = Element.None;
        // 主灵球贴图的单位宽度(世界单位)。orbScale是相对它的localScale系数,
        // 雷球序列帧等不同分辨率贴图都归一化到同一世界尺寸
        private float _mainOrbUnitWidth = 20.48f;

        private void Awake()
        {
            _player = transform;

            // Ensure AffixSystem exists (每技能独立子物体)
            if (affixSystem == null)
                affixSystem = AffixSystem.GetOrCreateFor(this);

            // Load skill data from asset
            var data = SkillLibrary.LoadByName("SpiritOrb");
            if (data != null)
            {
                baseDamageMultiplier = data.damageMultiplier;
                baseDetectionRange = data.baseRange;
                fireInterval = data.cooldown;
                orbSpeed = data.projectileSpeed;
                orbLifetime = data.projectileLifetime;
                orbScale = Mathf.Max(data.projectileScale, 0.15f);  // 最低0.15，防止灵球太小
                hitRadius = data.damageRadius;
                element = data.element;
            }

            if (orbSprite != null && orbSprite.pixelsPerUnit > 0)
                _mainOrbUnitWidth = orbSprite.rect.width / orbSprite.pixelsPerUnit;
        }

        /// <summary>
        /// 延迟加载元素专属球体资产（仅对应灵根激活时才加载）
        /// 雷=16帧序列帧动画, 水/风=静态贴图
        /// </summary>
        private void LoadElementOrbAssetsIfNeeded()
        {
            Element el = SpiritRootManager.GetEffectiveElement(element);
            if (el == Element.Thunder && !_thunderFramesLoaded)
            {
                _thunderFrames = Resources.LoadAll<Sprite>("ThunderOrbFrames");
                _thunderFramesLoaded = true;
                if (_thunderFrames != null && _thunderFrames.Length > 0)
                    Debug.Log($"ThunderOrb frames loaded: {_thunderFrames.Length} frames");
                else
                    Debug.LogWarning("ThunderOrb frames not found in Resources/ThunderOrbFrames");
            }
            else if (el == Element.Water && !_waterFramesLoaded)
            {
                // 水球序列帧优先, 缺失时回退静态贴图
                _waterFrames = Resources.LoadAll<Sprite>("WaterOrbFrames");
                if (_waterFrames != null && _waterFrames.Length > 0)
                {
                    System.Array.Sort(_waterFrames, (a, b) => string.CompareOrdinal(a.name, b.name));
                    Debug.Log($"WaterOrb frames loaded: {_waterFrames.Length} frames");
                }
                else
                {
                    _waterFrames = null;
                }
                if (_waterOrbSprite == null)
                    _waterOrbSprite = Resources.Load<Sprite>("WaterOrb");
                _waterFramesLoaded = true;
            }
            else if (el == Element.Wind && _windOrbSprite == null)
            {
                _windOrbSprite = Resources.Load<Sprite>("WindOrb");
                Debug.Log(_windOrbSprite != null ? "WindOrb sprite loaded" : "WindOrb not found in Resources");
            }
            else if (el == Element.FlameWind && _flameWindOrbSprite == null)
            {
                _flameWindOrbSprite = Resources.Load<Sprite>("FlameWindOrb");
                Debug.Log(_flameWindOrbSprite != null ? "FlameWindOrb sprite loaded" : "FlameWindOrb not found in Resources");
            }
            else if (el == Element.Frost && _frostOrbSprite == null)
            {
                _frostOrbSprite = Resources.Load<Sprite>("FrostOrb");
                Debug.Log(_frostOrbSprite != null ? "FrostOrb sprite loaded" : "FrostOrb not found in Resources");
            }
            else if (el == Element.Wood && _woodOrbSprite == null)
            {
                _woodOrbSprite = Resources.Load<Sprite>("WoodOrb");
                Debug.Log(_woodOrbSprite != null ? "WoodOrb sprite loaded" : "WoodOrb not found in Resources");
            }
            else if (el == Element.MetalFusion && _metalFusionOrbSprite == null)
            {
                _metalFusionOrbSprite = Resources.Load<Sprite>("MetalFusionOrb");
                Debug.Log(_metalFusionOrbSprite != null ? "MetalFusionOrb sprite loaded" : "MetalFusionOrb not found in Resources");
            }
        }

        /// <summary>
        /// 按当前有效属性选择球体贴图（雷=帧数组首帧, 水/风/融合=专属静态贴图, 其他=默认灵力弹）
        /// </summary>
        private Sprite GetOrbSpriteForElement(Element el)
        {
            if (el == Element.Thunder && _thunderFrames != null && _thunderFrames.Length > 0) return _thunderFrames[0];
            if (el == Element.Water)
            {
                if (_waterFrames != null && _waterFrames.Length > 0) return _waterFrames[0];
                if (_waterOrbSprite != null) return _waterOrbSprite;
            }
            if (el == Element.Wind && _windOrbSprite != null) return _windOrbSprite;
            if (el == Element.FlameWind && _flameWindOrbSprite != null) return _flameWindOrbSprite;
            if (el == Element.Frost && _frostOrbSprite != null) return _frostOrbSprite;
            if (el == Element.Wood && _woodOrbSprite != null) return _woodOrbSprite;
            if (el == Element.MetalFusion && _metalFusionOrbSprite != null) return _metalFusionOrbSprite;
            return orbSprite;
        }

        /// <summary>属性→VFX帧目录 (null=无对应帧)</summary>
        private static string GetVfxFolderFor(Element el)
        {
            switch (el)
            {
                case Element.Water: return null; // 水球本体已是序列帧动画, 不再叠加水VFX子特效
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

        private void Update()
        {
            LoadElementOrbAssetsIfNeeded();

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
                    Debug.Log($"[SpiritOrb] VFX frames loaded: {folder} = {(_vfxFrames != null ? _vfxFrames.Length : 0)}");
                }
                else
                {
                    _vfxFrames = null; // 该属性无VFX(如水), 清掉旧帧防止误挂
                }
            }

            _fireTimer += Time.deltaTime;
            float actualInterval = fireInterval;
            var psc = PlayerStatsComponent.Instance;
            if (psc != null) actualInterval = psc.stats.GetScaledInterval(fireInterval);
            if (_fireTimer >= actualInterval)
            {
                _fireTimer = 0f;
                TryFire();
            }

            // 按当前有效属性选择球体序列帧 (雷/水), 其余属性传null不播帧动画
            Element animEl = SpiritRootManager.GetEffectiveElement(element);
            Sprite[] orbAnimFrames = null;
            if (animEl == Element.Thunder) orbAnimFrames = _thunderFrames;
            else if (animEl == Element.Water) orbAnimFrames = _waterFrames;

            for (int i = _activeOrbs.Count - 1; i >= 0; i--)
            {
                SpiritOrb orb = _activeOrbs[i];
                orb.Update(orbScale * sizeMultiplier * _mainOrbUnitWidth, orbAnimFrames);
                // 聚能词缀: 球体同步变大 (伤害+的视觉体现)
                if (affixSystem != null && affixSystem.HasAffix(AffixType.OrbCharge) && orb.spriteRenderer != null)
                {
                    var c = orb.spriteRenderer.color;
                    orb.spriteRenderer.color = new Color(c.r * 1f, c.g * 0.97f, c.b * 0.9f, c.a); // 微暖色提示聚能
                }

                if (!orb.hit)
                {
                    float effectiveRadius = hitRadius * rangeMultiplier;
                    if (affixSystem != null) effectiveRadius *= affixSystem.GetRadiusMult();
                    if (CheckEnemyHit(orb.position, effectiveRadius, orb))
                    {
                        // 穿透词缀 — 命中后不消失(白闪), 可继续穿透
                        if (affixSystem != null && affixSystem.HasAffix(AffixType.Pierce) && !orb.isSplit)
                        {
                            orb.pierceRemaining--;
                            if (orb.pierceRemaining > 0 && orb.spriteRenderer != null)
                            {
                                AffixVisuals.PlayFlashWhite(orb.spriteRenderer, 0.1f);
                                // 折射词缀 — 穿透瞬间随机偏折±15°继续飞
                                if (orb.refractMode)
                                {
                                    float deflect = Random.Range(-15f, 15f) * Mathf.Deg2Rad;
                                    Vector3 dir = orb.velocity.normalized;
                                    float cos = Mathf.Cos(deflect), sin = Mathf.Sin(deflect);
                                    Vector3 newDir = new Vector3(
                                        dir.x * cos - dir.z * sin, 0f,
                                        dir.x * sin + dir.z * cos);
                                    orb.velocity = newDir * orb.velocity.magnitude;
                                }
                                // 不标记hit, 继续飞行; 防重复命中同一敌人
                                orb.lastHitTime = Time.time;
                                orb.hitCooldown = 0.3f;
                                continue;
                            }
                        }
                        orb.hit = true;
                        OnHit(orb);
                    }
                }

                if (orb.hit || orb.age >= orbLifetime)
                {
                    if (orb.gameObject != null)
                        Destroy(orb.gameObject);
                    _activeOrbs.RemoveAt(i);
                }
            }
        }

        private bool CheckEnemyHit(Vector3 pos, float radius, SpiritOrb orb)
        {
            // 穿透冷却 — 刚命中过的敌人短时间不再命中
            if (orb.lastHitTime > 0 && Time.time - orb.lastHitTime < orb.hitCooldown)
                return false;

            Element el = SpiritRootManager.GetEffectiveElement(element);
            var enemies = FindObjectsOfType<EnemyController>();
            foreach (var enemy in enemies)
            {
                if (enemy == null || !enemy.gameObject.activeInHierarchy) continue;
                float dx = pos.x - enemy.transform.position.x;
                float dz = pos.z - enemy.transform.position.z;
                if (dx * dx + dz * dz < radius * radius)
                {
                    float mult = orb.isSplit ? baseDamageMultiplier * upgradeDamageMult * splitDamageRatio : baseDamageMultiplier * upgradeDamageMult;
                    // 词缀伤害倍率
                    if (affixSystem != null) mult *= affixSystem.GetDamageMult();
                    // 增幅词缀: 飞行距离越远伤害越高 (每10单位+15%, 上限2倍)
                    if (orb.ampMode && affixSystem != null)
                    {
                        float flyDist = Vector3.Distance(orb.spawnPos, orb.position);
                        mult *= Mathf.Min(1f + flyDist * 0.015f, 2f);
                    }
                    // 贯穿强化词缀: 每次穿透伤害递增20% (穿透第2次×1.2, 第3次×1.4...)
                    if (affixSystem != null && affixSystem.deepPierceMode && orb.pierceRemaining < 1 + affixSystem.GetBonusPierce())
                    {
                        int pierceCount = (1 + affixSystem.GetBonusPierce()) - orb.pierceRemaining;
                        mult *= 1f + pierceCount * 0.2f;
                    }
                    int dmg = CalculateDamage(mult, out bool isCrit);
                    // 崩解词缀: 命中满血敌人额外+50%
                    if (affixSystem != null && affixSystem.shatterMode && enemy.currentHealth >= enemy.maxHealth * 0.999f)
                    {
                        dmg = Mathf.Max(1, Mathf.CeilToInt(dmg * (1f + affixSystem.shatterBonus)));
                        AffixVisuals.PlayStarPop(enemy.transform.position, new Color(1f, 0.6f, 0.15f), 1.1f); // 崩解橙色星爆
                    }
                    enemy.TakeDamage(dmg, el);
                    DamageNumber.CreateAt(enemy.transform.position + new Vector3(Random.Range(-0.5f, 0.5f), 0, 0), dmg, el, isCrit);
                    // 词缀命中回调
                    if (affixSystem != null)
                    {
                        affixSystem.OnHitEnemy(enemy.transform, dmg, el);
                        if (enemy.currentHealth <= 0) affixSystem.OnKillEnemy();
                    }
                    return true;
                }
            }

            var rabbits = FindObjectsOfType<GhostRabbitController>();
            foreach (var rabbit in rabbits)
            {
                if (rabbit == null || !rabbit.gameObject.activeInHierarchy) continue;
                float dx = pos.x - rabbit.transform.position.x;
                float dz = pos.z - rabbit.transform.position.z;
                if (dx * dx + dz * dz < radius * radius)
                {
                    float mult = orb.isSplit ? baseDamageMultiplier * upgradeDamageMult * splitDamageRatio : baseDamageMultiplier * upgradeDamageMult;
                    int dmg = CalculateDamage(mult, out bool isCrit);
                    rabbit.TakeDamage(dmg, el);
                    DamageNumber.CreateAt(rabbit.transform.position + new Vector3(Random.Range(-0.5f, 0.5f), 0, 0), dmg, el, isCrit);
                    return true;
                }
            }

            return false;
        }

        private void OnHit(SpiritOrb orb)
        {
            if (shockRadius > 0f)
                SpawnShockwave(orb.position, shockRadius);

            // 分裂词缀 — 命中时分裂小球
            if (affixSystem != null && affixSystem.HasAffix(AffixType.Split) && !orb.isSplit)
                SpawnSplitOrbs(orb.position);

            if (canSplit && !orb.isSplit)
                SpawnSplitOrbs(orb.position);
        }

        private float GetEffectiveRange()
        {
            var psc = PlayerStatsComponent.Instance;
            Element el = SpiritRootManager.GetEffectiveElement(element);
            float baseRange = baseDetectionRange * rangeMultiplier;
            if (psc != null) return psc.CalculateSkillRange(baseRange) * SpiritRootManager.GetRangeMult(el);
            return baseRange;
        }

        private int CalculateDamage(float mult, out bool isCrit)
        {
            Element el = SpiritRootManager.GetEffectiveElement(element);
            var psc = PlayerStatsComponent.Instance;
            if (psc != null) return psc.CalculateSkillDamage(mult * SpiritRootManager.GetDamageMult(el), el, out isCrit);
            isCrit = false;
            return Mathf.Max(1, Mathf.CeilToInt(40f * 0.15f * mult));
        }

        private void TryFire()
        {
            // 坠落词缀 — 灵球从天而降砸向敌人
            if (affixSystem != null && affixSystem.fallMode)
            {
                FallFire();
                return;
            }

            Transform nearest = FindNearestEnemy();
            if (nearest == null) return;

            // 词缀影响数量
            int count = orbCount;
            if (affixSystem != null) count += affixSystem.GetBonusProjectiles();
            count = Mathf.Max(1, count);

            // 连发词缀=扇形展开, 普通=环形均布
            bool fanMode = affixSystem != null && affixSystem.HasAffix(AffixType.MultiShot);
            float fanAngle = 40f; // 总扇形角度

            for (int i = 0; i < count; i++)
            {
                Vector3 offset = Vector3.zero;
                if (fanMode && count > 1)
                {
                    // 扇形: -fanAngle/2 ~ +fanAngle/2
                    float t = (float)i / (count - 1) - 0.5f;
                    float angleRad = t * fanAngle * Mathf.Deg2Rad;
                    offset = new Vector3(Mathf.Sin(angleRad), 0, Mathf.Cos(angleRad)) * 0.5f;
                }
                else if (orbCount > 1)
                {
                    float angle = (Mathf.PI * 2f / orbCount) * i;
                    offset = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * 0.5f;
                }
                SpawnOrb(nearest, offset, false);
            }
        }

        // ── 坠落词缀: 天降灵球 ──

        /// <summary>收集按距离升序的敌人 (多颗天降球分散目标用)</summary>
        private List<Transform> CollectEnemiesByDistance(int max)
        {
            var list = new List<Transform>();
            var enemies = FindObjectsOfType<EnemyController>();
            foreach (var e in enemies)
                if (e != null && e.gameObject.activeInHierarchy) list.Add(e.transform);
            var rabbits = FindObjectsOfType<GhostRabbitController>();
            foreach (var r in rabbits)
                if (r != null && r.gameObject.activeInHierarchy) list.Add(r.transform);
            list.Sort((a, b) => (a.position - transform.position).sqrMagnitude.CompareTo((b.position - transform.position).sqrMagnitude));
            if (max > 0 && list.Count > max) list.RemoveRange(max, list.Count - max);
            return list;
        }

        /// <summary>坠落词缀发射: 每颗灵球锁定一个敌人从天而降</summary>
        private void FallFire()
        {
            var targets = CollectEnemiesByDistance(0);
            if (targets.Count == 0) return;

            int count = orbCount;
            if (affixSystem != null) count += affixSystem.GetBonusProjectiles();
            count = Mathf.Max(1, count);

            Element el = SpiritRootManager.GetEffectiveElement(element);
            Sprite orbSpr = GetOrbSpriteForElement(el);
            Sprite[] animFrames = null;
            if (el == Element.Thunder) animFrames = _thunderFrames;
            else if (el == Element.Water) animFrames = _waterFrames;
            bool hasElementSprite = orbSpr != null && orbSpr != orbSprite;
            Color tint = hasElementSprite ? Color.white : ElementColors.GetTint(el);

            float worldSize = orbScale * sizeMultiplier * _mainOrbUnitWidth
                * (affixSystem.HasAffix(AffixType.OrbCharge) ? 1.3f : 1f);
            float aoeRadius = hitRadius * rangeMultiplier * affixSystem.fallAoeMult * affixSystem.GetRadiusMult();

            for (int i = 0; i < count; i++)
            {
                Transform target = targets[i % targets.Count];
                // 多球砸同目标时散开落点 (灵球不留地块残余, 只保留落地爆发+受击反馈)
                Vector3 pos = target.position + new Vector3(Random.Range(-1.5f, 1.5f), 0f, Random.Range(-1.5f, 1.5f));
                SkyfallStrike.Strike(pos, orbSpr, animFrames, tint, el,
                    worldSize, affixSystem.fallHeight, affixSystem.fallWarnTime, affixSystem.fallFallTime,
                    aoeRadius, FallImpact, trackTarget: target, leavePatch: false);
            }
        }

        /// <summary>坠落落地AoE伤害 (预警+坠落演出由SkyfallStrike处理)</summary>
        private void FallImpact(Vector3 pos)
        {
            float radius = hitRadius * rangeMultiplier * affixSystem.fallAoeMult * affixSystem.GetRadiusMult();
            Element el = SpiritRootManager.GetEffectiveElement(element);
            float mult = baseDamageMultiplier * upgradeDamageMult;
            if (affixSystem != null) mult *= affixSystem.GetDamageMult();

            var enemies = FindObjectsOfType<EnemyController>();
            foreach (var enemy in enemies)
            {
                if (enemy == null || !enemy.gameObject.activeInHierarchy) continue;
                float dx = pos.x - enemy.transform.position.x;
                float dz = pos.z - enemy.transform.position.z;
                if (dx * dx + dz * dz >= radius * radius) continue;

                int dmg = CalculateDamage(mult, out bool isCrit);
                // 崩解词缀: 命中满血敌人额外+50%
                if (affixSystem != null && affixSystem.shatterMode && enemy.currentHealth >= enemy.maxHealth * 0.999f)
                {
                    dmg = Mathf.Max(1, Mathf.CeilToInt(dmg * (1f + affixSystem.shatterBonus)));
                    AffixVisuals.PlayStarPop(enemy.transform.position, new Color(1f, 0.6f, 0.15f), 1.1f);
                }
                enemy.TakeDamage(dmg, el);
                DamageNumber.CreateAt(enemy.transform.position + new Vector3(Random.Range(-0.5f, 0.5f), 0, 0), dmg, el, isCrit);
                if (affixSystem != null)
                {
                    affixSystem.OnHitEnemy(enemy.transform, dmg, el);
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

                int dmg = CalculateDamage(mult, out bool isCrit);
                rabbit.TakeDamage(dmg, el);
                DamageNumber.CreateAt(rabbit.transform.position + new Vector3(Random.Range(-0.5f, 0.5f), 0, 0), dmg, el, isCrit);
                if (affixSystem != null)
                {
                    affixSystem.OnHitEnemy(rabbit.transform, dmg, el);
                    if (rabbit.currentHealth <= 0) affixSystem.OnKillEnemy();
                }
            }
        }

        private Transform FindNearestEnemy()
        {
            Transform nearest = null;
            float nearestDist = GetEffectiveRange();

            var enemies = FindObjectsOfType<EnemyController>();
            foreach (var enemy in enemies)
            {
                if (enemy == null || !enemy.gameObject.activeInHierarchy) continue;
                float dist = Vector3.Distance(transform.position, enemy.transform.position);
                if (dist < nearestDist) { nearestDist = dist; nearest = enemy.transform; }
            }

            var rabbits = FindObjectsOfType<GhostRabbitController>();
            foreach (var rabbit in rabbits)
            {
                if (rabbit == null || !rabbit.gameObject.activeInHierarchy) continue;
                float dist = Vector3.Distance(transform.position, rabbit.transform.position);
                if (dist < nearestDist) { nearestDist = dist; nearest = rabbit.transform; }
            }

            return nearest;
        }

        private void SpawnOrb(Transform target, Vector3 offset, bool isSplit)
        {
            Vector3 spawnPos = transform.position + spawnOffset + offset;
            Vector3 direction = target.position - spawnPos;
            direction.y = 0f;
            direction.Normalize();

            GameObject orbObj = new GameObject("SpiritOrb");
            orbObj.transform.position = spawnPos;

            SpriteRenderer sr = orbObj.AddComponent<SpriteRenderer>();
            Element el = SpiritRootManager.GetEffectiveElement(element);
            sr.sprite = GetOrbSpriteForElement(el);
            sr.sortingOrder = sortingOrder;
            Element spawnEl = SpiritRootManager.GetEffectiveElement(element);
            // 元素专属贴图(雷帧/水/风)本身带颜色不tint, 默认灵力弹才按属性tint
            bool hasElementSprite = sr.sprite != null && sr.sprite != orbSprite;
            if (!hasElementSprite)
                sr.color = ElementColors.GetTint(spawnEl);
            else
                sr.color = Color.white;

            float angle = Mathf.Atan2(direction.z, direction.x) * Mathf.Rad2Deg + 90f;
            orbObj.transform.rotation = _player.rotation * Quaternion.Euler(0, 0, angle);
            // 统一世界尺寸: 目标世界尺寸=orbScale×主贴图单位宽度(与直接用localScale等价),
            // 再按当前贴图(雷球帧等)单位宽度换算, 保证不同分辨率贴图视觉大小一致
            float targetWorldSize = orbScale * sizeMultiplier * _mainOrbUnitWidth * (affixSystem != null && affixSystem.HasAffix(AffixType.OrbCharge) ? 1.3f : 1f);
            float unitWidth = sr.sprite != null && sr.sprite.pixelsPerUnit > 0
                ? sr.sprite.rect.width / sr.sprite.pixelsPerUnit : _mainOrbUnitWidth;
            float uniformScale = unitWidth > 0.01f ? targetWorldSize / unitWidth : targetWorldSize;
            orbObj.transform.localScale = new Vector3(uniformScale, uniformScale, 1f);

            Light glow = orbObj.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = ElementColors.GetGlow(spawnEl);
            glow.intensity = 1.5f;
            glow.range = 2f;

            TrailRenderer trail = orbObj.AddComponent<TrailRenderer>();
            trail.time = trailTime;
            trail.startWidth = 0.08f * sizeMultiplier;
            trail.endWidth = 0f;
            trail.startColor = ElementColors.GetTrail(spawnEl);
            trail.endColor = new Color(trailColor.r, trailColor.g, trailColor.b, 0f);
            trail.material = new Material(Shader.Find("Sprites/Default"));

            SpiritOrb orb = new SpiritOrb
            {
                gameObject = orbObj,
                transform = orbObj.transform,
                velocity = direction * orbSpeed * speedMultiplier * (affixSystem != null ? affixSystem.GetSpeedMult() : 1f),
                spriteRenderer = sr,
                isSplit = isSplit
            };

            // 词缀行为参数传递
            if (affixSystem != null)
            {
                orb.canTrack = affixSystem.canTrack;
                orb.trackRange = affixSystem.trackRange;
                orb.refractMode = affixSystem.refractMode;
                orb.huntMode = affixSystem.huntMode;
                orb.huntTurnRate = affixSystem.huntTurnRate;
                orb.ampMode = affixSystem.HasAffix(AffixType.Amplify);
                orb.spawnPos = orbObj.transform.position; // 出生点 (增幅距离基准)
                int bonusPierce = affixSystem.GetBonusPierce();
                orb.pierceRemaining = 1 + bonusPierce; // 基础1次+穿透加成
            }

            _activeOrbs.Add(orb);
            RootMechanics.AttachFireTrail(orbObj, spawnEl); // 火灵根Lv3+: 火径

            // 属性VFX附加
            if (!isSplit && _vfxFrames != null && _vfxFrames.Length > 0)
            {
                var vfxObj = new GameObject("OrbVFX");
                vfxObj.transform.SetParent(orbObj.transform, false);
                vfxObj.transform.localPosition = Vector3.zero;
                // 补偿父物体缩放: 使VFX世界尺寸与灵球本体一致(主贴图10.24单位 vs 雷球帧2.56单位路径都正确)
                float vfxUnit = _vfxFrames[0].pixelsPerUnit > 0 ? _vfxFrames[0].rect.width / _vfxFrames[0].pixelsPerUnit : 1f;
                vfxObj.transform.localScale = Vector3.one * (vfxUnit > 0.01f ? unitWidth / vfxUnit : 1f);
                var vfxSr = vfxObj.AddComponent<SpriteRenderer>();
                vfxSr.sprite = _vfxFrames[0];
                vfxSr.sortingOrder = sortingOrder + 1;
                var vfxAnim = vfxObj.AddComponent<VFXFrameAnimator>();
                vfxAnim.frames = _vfxFrames;
                vfxAnim.fps = 12f;
                vfxAnim.alpha = 0.7f;
            }
        }

        private void SpawnSplitOrbs(Vector3 pos)
        {
            // 分裂瞬间小星爆 (可感知的分裂反馈)
            AffixVisuals.PlayStarPop(pos, new Color(0.85f, 0.95f, 1f), 0.7f);

            float angle = Random.Range(0f, Mathf.PI * 2f);
            Vector3 dir = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));

            GameObject orbObj = new GameObject("SpiritOrb_Split");
            orbObj.transform.position = pos;

            SpriteRenderer sr = orbObj.AddComponent<SpriteRenderer>();
            if (orbSprite != null)
                sr.sprite = orbSprite;
            sr.sortingOrder = sortingOrder;
            sr.color = new Color(0.8f, 0.9f, 1f, 0.7f);

            orbObj.transform.rotation = _player.rotation * Quaternion.Euler(0, 0, angle * Mathf.Rad2Deg + 90f);
            // 分裂球=主灵球世界尺寸的50%, 同样按贴图单位宽度归一化
            float splitWorldSize = orbScale * sizeMultiplier * _mainOrbUnitWidth * 0.5f;
            float unit = sr.sprite != null && sr.sprite.pixelsPerUnit > 0
                ? sr.sprite.rect.width / sr.sprite.pixelsPerUnit : _mainOrbUnitWidth;
            float s = unit > 0.01f ? splitWorldSize / unit : splitWorldSize;
            orbObj.transform.localScale = new Vector3(s, s, 1f);

            Light glow = orbObj.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = glowColor;
            glow.intensity = 0.8f;
            glow.range = 1.5f;

            SpiritOrb orb = new SpiritOrb
            {
                gameObject = orbObj,
                transform = orbObj.transform,
                velocity = dir * orbSpeed * speedMultiplier * 0.7f * (affixSystem != null ? affixSystem.GetSpeedMult() : 1f),
                spriteRenderer = sr,
                isSplit = true
            };

            _activeOrbs.Add(orb);
        }

        private void SpawnShockwave(Vector3 pos, float radius)
        {
            GameObject waveObj = new GameObject("SpiritShockwave");
            waveObj.transform.position = pos;
            waveObj.transform.rotation = _player.rotation;

            SpriteRenderer sr = waveObj.AddComponent<SpriteRenderer>();
            sr.color = new Color(0.8f, 0.9f, 1f, 0.4f);
            sr.sortingOrder = sortingOrder - 1;

            StartCoroutine(AnimateShockwave(waveObj, radius));
        }

        private System.Collections.IEnumerator AnimateShockwave(GameObject obj, float targetRadius)
        {
            float duration = 0.3f;
            float elapsed = 0f;
            float startScale = 0.1f;
            float endScale = targetRadius * 2f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float s = Mathf.Lerp(startScale, endScale, t);
                obj.transform.localScale = new Vector3(s, s, 1f);
                yield return null;
            }

            Destroy(obj);
        }

        public void LevelUp()
        {
            skillLevel = Mathf.Min(10, skillLevel + 1);
            ApplyLevelEffects();

            // Lv10满级触发悟道
            if (skillLevel >= 10 && evolutionCount == 0)
            {
                TriggerEnlightenment();
            }
            else if (skillLevel >= 10 && evolutionCount >= 1)
            {
                // 第二次满级 → 神通
                TriggerDivinePower();
            }
        }

        /// <summary>
        /// 触发悟道 (Lv10满级)
        /// </summary>
        private void TriggerEnlightenment()
        {
            Element el = SpiritRootManager.GetEffectiveElement(element);
            string[] options = DaoSystem.GetDaoOptions("SpiritOrb", el);
            if (options.Length == 0) return;
            EnlightenmentChoice.Show(options, selectedDao =>
            {
                currentDaoId = selectedDao;
                evolutionCount = 1;
                skillLevel = 1; // 重置为Lv1

                // 道进化后基础倍率提升
                baseDamageMultiplier *= 1.5f;
                baseDetectionRange *= 1.2f;

                DaoSystem.ApplyDaoEffect(this, currentDaoId);
                Debug.Log($"[SpiritOrb] 悟道: {DaoSystem.GetDaoName(currentDaoId)} — 技能进化, 基础倍率→{baseDamageMultiplier}");
            });
        }

        /// <summary>
        /// 触发神通 (第二次满级)
        /// </summary>
        private void TriggerDivinePower()
        {
            evolutionCount = 2;
            DaoSystem.TriggerDivine(this, currentDaoId, transform);
            Debug.Log($"[SpiritOrb] 神通充能: {DaoSystem.GetDaoName("Divine_" + currentDaoId)}");
        }

        private void ApplyLevelEffects()
        {
            switch (skillLevel)
            {
                case 2: // 聚灵
                    upgradeDamageMult = 1.15f;
                    sizeMultiplier = 1.1f;
                    break;
                case 3: // 双生灵球
                    orbCount = 2;
                    break;
                case 4: // 灵速
                    speedMultiplier = 1.25f;
                    break;
                case 5: // 灵震
                    shockRadius = hitRadius * 1.2f;
                    break;
                case 6: // 凝练
                    sizeMultiplier = 1.35f;
                    upgradeDamageMult = 1.35f;
                    break;
                case 7: // 散灵
                    canSplit = true;
                    break;
                case 8: // 灵潮
                    orbCount = 4;
                    break;
                case 9: // 归元
                    fireInterval = 1.5f;
                    break;
                case 10: // 万灵归一
                    upgradeDamageMult = 1.85f;
                    orbCount = 6;
                    speedMultiplier = 1.55f;
                    rangeMultiplier = 1.3f;
                    fireInterval = 1f;
                    break;
            }
        }

        private class SpiritOrb
        {
            public GameObject gameObject;
            public Transform transform;
            public Vector3 velocity;
            public SpriteRenderer spriteRenderer;
            public float age;
            public bool hit;
            public bool isSplit;
            private int _frameIndex;
            private float _frameTimer;
            private const float FrameFPS = 12f;

            // 词缀行为
            public bool canTrack;
            public float trackRange;
            public bool refractMode;      // 折射: 命中后随机偏折±15°
            public bool huntMode;         // 寻敌: 持续小幅转向
            public float huntTurnRate = 2.5f;
            public float ampDamagePerDist = 0.015f;
            public bool ampMode;          // 增幅词缀: 飞得越远伤害越高
            public Vector3 spawnPos;      // 出生点 (增幅距离计算用)
            public int pierceRemaining = 1;
            public float lastHitTime = -1f;
            public float hitCooldown = 0f;

            public Vector3 position => transform.position;

            public void Update(float targetWorldSize, Sprite[] animFrames = null)
            {
                if (hit) return;
                age += Time.deltaTime;

                // 寻敌词缀 — 持续小幅转向最近敌人 (渐进追踪, 每帧小幅转向)
                if (huntMode)
                {
                    Transform nearest = FindNearestEnemy(transform.position, trackRange);
                    if (nearest != null)
                    {
                        Vector3 toEnemy = nearest.position - transform.position;
                        toEnemy.y = 0;
                        if (toEnemy.sqrMagnitude > 0.01f)
                        {
                            toEnemy.Normalize();
                            // 小幅转向: 当前速度方向向目标插值huntTurnRate弧度/秒
                            Vector3 curDir = velocity.normalized;
                            Vector3 newDir = Vector3.RotateTowards(curDir, toEnemy, huntTurnRate * Time.deltaTime, 0f);
                            velocity = newDir * velocity.magnitude;
                        }
                    }
                }

                // 追踪词缀 — 每0.2s转向最近敌人(强追踪)
                if (canTrack)
                {
                    _trackTimer += Time.deltaTime;
                    if (_trackTimer >= 0.2f)
                    {
                        _trackTimer = 0f;
                        Transform nearest = FindNearestEnemy(transform.position, trackRange);
                        if (nearest != null)
                        {
                            Vector3 toEnemy = nearest.position - transform.position;
                            toEnemy.y = 0;
                            if (toEnemy.sqrMagnitude > 0.01f)
                            {
                                toEnemy.Normalize();
                                velocity = toEnemy * velocity.magnitude;
                            }
                        }
                    }
                }

                Vector3 move = velocity * Time.deltaTime;
                move.y = 0f;
                transform.position += move;

                // 雷球序列帧动画
                if (animFrames != null && animFrames.Length > 0 && spriteRenderer != null)
                {
                    _frameTimer += Time.deltaTime;
                    float frameDur = 1f / FrameFPS;
                    if (_frameTimer >= frameDur)
                    {
                        _frameTimer -= frameDur;
                        _frameIndex = (_frameIndex + 1) % animFrames.Length;
                        spriteRenderer.sprite = animFrames[_frameIndex];
                    }
                }

                // 按当前显示贴图的单位宽度归一化localScale, 使世界尺寸恒等于targetWorldSize
                var curSprite = spriteRenderer != null ? spriteRenderer.sprite : null;
                float unit = curSprite != null && curSprite.pixelsPerUnit > 0
                    ? curSprite.rect.width / curSprite.pixelsPerUnit : 1f;
                float s = unit > 0.01f ? targetWorldSize / unit : targetWorldSize;
                transform.localScale = new Vector3(s, s, 1f);
            }

            private float _trackTimer;

            private static Transform FindNearestEnemy(Vector3 pos, float range)
            {
                Transform nearest = null;
                float nearestDist = range * range;
                var enemies = Object.FindObjectsOfType<EnemyController>();
                foreach (var e in enemies)
                {
                    if (e == null || !e.gameObject.activeInHierarchy) continue;
                    float dx = pos.x - e.transform.position.x;
                    float dz = pos.z - e.transform.position.z;
                    float d2 = dx * dx + dz * dz;
                    if (d2 < nearestDist) { nearestDist = d2; nearest = e.transform; }
                }
                return nearest;
            }
        }
    }
}

