using UnityEngine;
using System.Collections.Generic;

namespace BiopunkRV
{
    /// <summary>
    /// Fireball spell that auto-fires at nearest enemy.
    /// Attaches to Player, periodically spawns fireballs toward enemies.
    /// Supports sprite sequence animation for burning effect.
    /// </summary>
    public class FireballSpell : MonoBehaviour
    {
        [Header("Fireball Settings")]
        public Sprite fireballSprite; // fallback single sprite
        public Sprite[] fireballFrames; // animation frames (burning effect)
        public float fireInterval = 1.5f;
        public float fireballSpeed = 16f;
        public float damageMultiplier = 1.6f;   // 技能伤害倍率 160%
        public float fireballLifetime = 3f;
        public float baseDetectionRange = 8f;   // 技能基础攻击范围
        public float hitRadius = 2.4f;
        public Element element = Element.Fire;
        public Vector3 spawnOffset = new Vector3(0, 2f, 0);

        [Header("Affix System")]
        public AffixSystem affixSystem;

        [Header("Visual")]
        public float fireballScale = 3f;
        public int sortingOrder = 60;
        public float animFPS = 12f;

        [Header("Level System")]
        public int skillLevel = 1;
        public int projectileCount = 1;
        public int pierceCount = 0;
        public float speedMultiplier = 1f;
        public float sizeMultiplier = 1f;
        public float damageMult = 1f;      // 升级伤害加成
        public float cooldownMult = 1f;    // 升级冷却缩减
        public int scatterCount = 0;       // Lv7: 每次攻击额外散射小火球数
        public float scatterDamageRatio = 0.5f;
        public bool canSplit = false;      // Lv9: 命中后分裂
        public float splitChance = 0f;
        public int splitCount = 2;
        public float splitDamageRatio = 0.4f;
        public float splitScale = 0.5f;

        private float _fireTimer;
        private List<Fireball> _activeFireballs = new List<Fireball>();
        private Transform _player;

        private void Awake()
        {
            _player = transform;

            // Ensure AffixSystem exists (每技能独立子物体)
            if (affixSystem == null)
                affixSystem = AffixSystem.GetOrCreateFor(this);

            // Load skill data from asset
            var data = SkillLibrary.LoadByName("Fireball");
            if (data != null)
            {
                damageMultiplier = data.damageMultiplier;
                baseDetectionRange = data.baseRange;
                fireInterval = data.cooldown;
                fireballSpeed = data.projectileSpeed;
                fireballLifetime = data.projectileLifetime;
                fireballScale = data.projectileScale;
                hitRadius = data.damageRadius;
                element = data.element;
            }

            if (fireballSprite == null)
            {
                fireballSprite = Resources.Load<Sprite>("FireballSpell");
            }
        }

        private void Update()
        {
            if (affixSystem != null)
            {
                affixSystem.Tick();
                affixSystem.UpdateAffixes(skillLevel);
            }

            _fireTimer += Time.deltaTime;
            float actualInterval = fireInterval * cooldownMult;
            var psc = PlayerStatsComponent.Instance;
            if (psc != null) actualInterval = psc.stats.GetScaledInterval(actualInterval);
            if (_fireTimer >= actualInterval)
            {
                _fireTimer = 0f;
                TryFireAtNearestEnemy();
            }

            for (int i = _activeFireballs.Count - 1; i >= 0; i--)
            {
                Fireball fb = _activeFireballs[i];
                fb.Update(animFPS, fireballFrames, fireballSprite, fireballScale * sizeMultiplier, false);

                if (!fb.hit)
                {
                    bool hitSomething = CheckFireballHit(fb);
                    if (hitSomething && !fb.isSmall)
                    {
                        // Lv9: 分裂弹 — 命中后概率分裂
                        if (canSplit && Random.value < splitChance)
                            SpawnSplitFireballs(fb.position);

                        // 穿透: 不标记hit，继续飞行
                        if (fb.pierceRemaining > 0)
                        {
                            fb.pierceRemaining--;
                            fb.hitCooldown = 0.2f;
                        }
                        else
                        {
                            fb.hit = true;
                        }
                    }
                }
                else
                {
                    fb.hitCooldown -= Time.deltaTime;
                }

                if (fb.hit || fb.age >= fireballLifetime)
                {
                    if (fb.gameObject != null)
                        Destroy(fb.gameObject);
                    _activeFireballs.RemoveAt(i);
                }
            }
        }

        private bool CheckFireballHit(Fireball fb)
        {
            if (fb.hitCooldown > 0f) return false;
            float radiusMult = affixSystem != null ? affixSystem.GetRadiusMult() : 1f;

            var enemies = Object.FindObjectsOfType<EnemyController>();
            foreach (var enemy in enemies)
            {
                if (enemy == null || !enemy.gameObject.activeInHierarchy) continue;
                if (fb.hitTargets.Contains(enemy.transform)) continue;
                float dx = fb.position.x - enemy.transform.position.x;
                float dz = fb.position.z - enemy.transform.position.z;
                float r = hitRadius * (fb.isSmall ? 0.6f : 1f) * radiusMult;
                if (dx * dx + dz * dz < r * r)
                {
                    int dmg = CalculateDamage(out bool isCrit, fb.isSmall);
                    enemy.TakeDamage(dmg, element);
                    DamageNumber.CreateAt(enemy.transform.position + new Vector3(Random.Range(-0.5f, 0.5f), 0, 0), dmg, element, isCrit);
                    fb.hitTargets.Add(enemy.transform);
                    if (affixSystem != null)
                    {
                        affixSystem.OnHitEnemy(enemy.transform, dmg, element);
                        if (enemy.currentHealth <= 0) affixSystem.OnKillEnemy();
                    }
                    return true;
                }
            }

            var rabbits = Object.FindObjectsOfType<GhostRabbitController>();
            foreach (var rabbit in rabbits)
            {
                if (rabbit == null || !rabbit.gameObject.activeInHierarchy) continue;
                if (fb.hitTargets.Contains(rabbit.transform)) continue;
                float dx = fb.position.x - rabbit.transform.position.x;
                float dz = fb.position.z - rabbit.transform.position.z;
                float r = hitRadius * (fb.isSmall ? 0.6f : 1f) * radiusMult;
                if (dx * dx + dz * dz < r * r)
                {
                    int dmg = CalculateDamage(out bool isCrit, fb.isSmall);
                    rabbit.TakeDamage(dmg, element);
                    DamageNumber.CreateAt(rabbit.transform.position + new Vector3(Random.Range(-0.5f, 0.5f), 0, 0), dmg, element, isCrit);
                    fb.hitTargets.Add(rabbit.transform);
                    if (affixSystem != null)
                    {
                        affixSystem.OnHitEnemy(rabbit.transform, dmg, element);
                        if (rabbit.currentHealth <= 0) affixSystem.OnKillEnemy();
                    }
                    return true;
                }
            }

            return false;
        }

        private float GetEffectiveRange()
        {
            var psc = PlayerStatsComponent.Instance;
            if (psc != null) return psc.CalculateSkillRange(baseDetectionRange) * FireSpiritRoot.GetFireRangeMult(element);
            return baseDetectionRange;
        }

        private int CalculateDamage(out bool isCrit, bool isSmall = false)
        {
            float mult = damageMultiplier * damageMult;
            if (affixSystem != null) mult *= affixSystem.GetDamageMult();
            if (isSmall) mult *= scatterDamageRatio;
            var psc = PlayerStatsComponent.Instance;
            if (psc != null) return psc.CalculateSkillDamage(mult * FireSpiritRoot.GetFireDamageMult(element), element, out isCrit);
            isCrit = false;
            return Mathf.Max(1, Mathf.CeilToInt(40f * 0.15f * mult));
        }

        private void TryFireAtNearestEnemy()
        {
            // 坠落词缀 — 火球从天而降
            if (affixSystem != null && affixSystem.fallMode)
            {
                FallFire();
                return;
            }

            Transform nearest = null;
            float nearestDist = GetEffectiveRange();

            var enemies = Object.FindObjectsOfType<EnemyController>();
            foreach (var enemy in enemies)
            {
                if (enemy == null || !enemy.gameObject.activeInHierarchy) continue;
                float dist = Vector3.Distance(transform.position, enemy.transform.position);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = enemy.transform;
                }
            }

            var rabbits = Object.FindObjectsOfType<GhostRabbitController>();
            foreach (var rabbit in rabbits)
            {
                if (rabbit == null || !rabbit.gameObject.activeInHierarchy) continue;
                float dist = Vector3.Distance(transform.position, rabbit.transform.position);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = rabbit.transform;
                }
            }

            if (nearest != null)
            {
                SpawnFireball(nearest, false);

                // Lv3+: 多发火球 (含词缀连发/增殖加成)
                int totalCount = projectileCount;
                if (affixSystem != null) totalCount += affixSystem.GetBonusProjectiles();
                if (totalCount > 1)
                {
                    for (int i = 1; i < totalCount; i++)
                    {
                        // 找其他目标或同目标偏移
                        float angleOffset = (Mathf.PI * 2f / totalCount) * i;
                        SpawnFireballWithOffset(nearest, angleOffset, false);
                    }
                }

                // Lv7: 散射 — 额外小火球
                for (int i = 0; i < scatterCount; i++)
                {
                    float randAngle = Random.Range(0f, Mathf.PI * 2f);
                    SpawnFireballWithOffset(nearest, randAngle, true);
                }
            }
        }

        // ── 坠落词缀: 天降火球 ──

        /// <summary>收集按距离升序的敌人</summary>
        private List<Transform> CollectEnemiesByDistance(int max)
        {
            var list = new List<Transform>();
            var enemies = Object.FindObjectsOfType<EnemyController>();
            foreach (var e in enemies)
                if (e != null && e.gameObject.activeInHierarchy) list.Add(e.transform);
            var rabbits = Object.FindObjectsOfType<GhostRabbitController>();
            foreach (var r in rabbits)
                if (r != null && r.gameObject.activeInHierarchy) list.Add(r.transform);
            list.Sort((a, b) => (a.position - transform.position).sqrMagnitude.CompareTo((b.position - transform.position).sqrMagnitude));
            if (max > 0 && list.Count > max) list.RemoveRange(max, list.Count - max);
            return list;
        }

        /// <summary>坠落词缀发射: 火球从天而降砸向敌人</summary>
        private void FallFire()
        {
            var targets = CollectEnemiesByDistance(0);
            if (targets.Count == 0) return;

            int totalCount = projectileCount;
            if (affixSystem != null) totalCount += affixSystem.GetBonusProjectiles();
            totalCount = Mathf.Max(1, totalCount);

            // 与普通火球视觉一致: 世界尺寸=贴图单位宽度×fireballScale
            Sprite fbSpr = (fireballFrames != null && fireballFrames.Length > 0) ? fireballFrames[0] : fireballSprite;
            if (fbSpr == null) return;
            float unit = fbSpr.pixelsPerUnit > 0 ? fbSpr.rect.width / fbSpr.pixelsPerUnit : 1f;
            float worldSize = unit * fireballScale * sizeMultiplier;
            float aoeRadius = hitRadius * affixSystem.fallAoeMult * affixSystem.GetRadiusMult();

            for (int i = 0; i < totalCount; i++)
            {
                Transform target = targets[i % targets.Count];
                Vector3 pos = target.position + new Vector3(Random.Range(-1.5f, 1.5f), 0f, Random.Range(-1.5f, 1.5f));
                SkyfallStrike.Strike(pos, fireballSprite, fireballFrames, Color.white, element,
                    worldSize, affixSystem.fallHeight, affixSystem.fallWarnTime, affixSystem.fallFallTime,
                    aoeRadius, p => FallImpact(p, false), trackTarget: target);
            }

            // Lv7: 散射 — 额外小火球也从天而降
            for (int i = 0; i < scatterCount; i++)
            {
                Transform target = targets[Random.Range(0, targets.Count)];
                Vector3 pos = target.position + new Vector3(Random.Range(-2.5f, 2.5f), 0f, Random.Range(-2.5f, 2.5f));
                SkyfallStrike.Strike(pos, fireballSprite, fireballFrames, Color.white, element,
                    worldSize * 0.5f, affixSystem.fallHeight, affixSystem.fallWarnTime, affixSystem.fallFallTime,
                    aoeRadius * 0.6f, p => FallImpact(p, true), trackTarget: target);
            }
        }

        /// <summary>坠落落地AoE伤害</summary>
        private void FallImpact(Vector3 pos, bool isSmall)
        {
            float radiusMult = affixSystem != null ? affixSystem.fallAoeMult * affixSystem.GetRadiusMult() : 1.5f;
            float radius = hitRadius * (isSmall ? 0.6f : 1f) * radiusMult;

            var enemies = Object.FindObjectsOfType<EnemyController>();
            foreach (var enemy in enemies)
            {
                if (enemy == null || !enemy.gameObject.activeInHierarchy) continue;
                float dx = pos.x - enemy.transform.position.x;
                float dz = pos.z - enemy.transform.position.z;
                if (dx * dx + dz * dz >= radius * radius) continue;

                int dmg = CalculateDamage(out bool isCrit, isSmall);
                enemy.TakeDamage(dmg, element);
                DamageNumber.CreateAt(enemy.transform.position + new Vector3(Random.Range(-0.5f, 0.5f), 0, 0), dmg, element, isCrit);
                if (affixSystem != null)
                {
                    affixSystem.OnHitEnemy(enemy.transform, dmg, element);
                    if (enemy.currentHealth <= 0) affixSystem.OnKillEnemy();
                }
            }

            var rabbits = Object.FindObjectsOfType<GhostRabbitController>();
            foreach (var rabbit in rabbits)
            {
                if (rabbit == null || !rabbit.gameObject.activeInHierarchy) continue;
                float dx = pos.x - rabbit.transform.position.x;
                float dz = pos.z - rabbit.transform.position.z;
                if (dx * dx + dz * dz >= radius * radius) continue;

                int dmg = CalculateDamage(out bool isCrit, isSmall);
                rabbit.TakeDamage(dmg, element);
                DamageNumber.CreateAt(rabbit.transform.position + new Vector3(Random.Range(-0.5f, 0.5f), 0, 0), dmg, element, isCrit);
                if (affixSystem != null)
                {
                    affixSystem.OnHitEnemy(rabbit.transform, dmg, element);
                    if (rabbit.currentHealth <= 0) affixSystem.OnKillEnemy();
                }
            }
        }

        private void SpawnFireball(Transform target, bool isSmall)
        {
            Vector3 spawnPos = transform.position + spawnOffset;
            Vector3 direction = target.position - spawnPos;
            direction.y = 0f;
            direction.Normalize();
            int bonusPierce = affixSystem != null ? affixSystem.GetBonusPierce() : 0;
            SpawnFireballInternal(spawnPos, direction, isSmall, isSmall ? 0 : pierceCount + bonusPierce);
        }

        private void SpawnFireballWithOffset(Transform target, float angleOffset, bool isSmall)
        {
            Vector3 spawnPos = transform.position + spawnOffset;
            Vector3 direction = target.position - spawnPos;
            direction.y = 0f;
            direction.Normalize();
            // 旋转方向
            float cos = Mathf.Cos(angleOffset);
            float sin = Mathf.Sin(angleOffset);
            direction = new Vector3(direction.x * cos - direction.z * sin, 0, direction.x * sin + direction.z * cos);
            SpawnFireballInternal(spawnPos, direction, isSmall, isSmall ? 0 : pierceCount);
        }

        private void SpawnFireballInternal(Vector3 spawnPos, Vector3 direction, bool isSmall, int pierce)
        {
            float scale = fireballScale * sizeMultiplier * (isSmall ? 0.5f : 1f);

            GameObject fbObj = new GameObject(isSmall ? "Fireball_Small" : "Fireball");
            fbObj.transform.position = spawnPos;

            SpriteRenderer sr = fbObj.AddComponent<SpriteRenderer>();
            if (fireballFrames != null && fireballFrames.Length > 0)
                sr.sprite = fireballFrames[0];
            else if (fireballSprite != null)
                sr.sprite = fireballSprite;
            sr.sortingOrder = sortingOrder;
            sr.color = isSmall ? new Color(1f, 0.7f, 0.3f, 0.8f) : Color.white;

            float angle = Mathf.Atan2(direction.z, direction.x) * Mathf.Rad2Deg + 90f;
            fbObj.transform.rotation = _player.rotation * Quaternion.Euler(0, 0, angle);
            fbObj.transform.localScale = new Vector3(scale, scale, 1f);

            Light glow = fbObj.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = new Color(1f, 0.6f, 0.2f, 1f);
            glow.intensity = isSmall ? 1f : 2f;
            glow.range = isSmall ? 1.5f : 3f;

            Fireball fb = new Fireball
            {
                gameObject = fbObj,
                transform = fbObj.transform,
                velocity = direction * fireballSpeed * speedMultiplier * (affixSystem != null ? affixSystem.GetSpeedMult() : 1f),
                spriteRenderer = sr,
                isSmall = isSmall,
                pierceRemaining = pierce
            };

            _activeFireballs.Add(fb);
            RootMechanics.AttachFireTrail(fbObj, Element.Fire); // 火灵根Lv3+: 火径
        }

        /// <summary>
        /// Lv9: 分裂弹 — 命中后分裂成小火球
        /// </summary>
        private void SpawnSplitFireballs(Vector3 pos)
        {
            for (int i = 0; i < splitCount; i++)
            {
                float angle = Random.Range(0f, Mathf.PI * 2f);
                Vector3 dir = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                GameObject fbObj = new GameObject("Fireball_Split");
                fbObj.transform.position = pos;

                SpriteRenderer sr = fbObj.AddComponent<SpriteRenderer>();
                if (fireballFrames != null && fireballFrames.Length > 0)
                    sr.sprite = fireballFrames[0];
                else if (fireballSprite != null)
                    sr.sprite = fireballSprite;
                sr.sortingOrder = sortingOrder;
                sr.color = new Color(1f, 0.7f, 0.3f, 0.8f);

                fbObj.transform.rotation = _player.rotation * Quaternion.Euler(0, 0, angle * Mathf.Rad2Deg + 90f);
                fbObj.transform.localScale = new Vector3(fireballScale * sizeMultiplier * splitScale, fireballScale * sizeMultiplier * splitScale, 1f);

                Light glow = fbObj.AddComponent<Light>();
                glow.type = LightType.Point;
                glow.color = new Color(1f, 0.6f, 0.2f);
                glow.intensity = 1f;
                glow.range = 1.5f;

                Fireball fb = new Fireball
                {
                    gameObject = fbObj,
                    transform = fbObj.transform,
                    velocity = dir * fireballSpeed * speedMultiplier * 0.7f * (affixSystem != null ? affixSystem.GetSpeedMult() : 1f),
                    spriteRenderer = sr,
                    isSmall = true,
                    pierceRemaining = 0
                };
                _activeFireballs.Add(fb);
            }
        }

        private class Fireball
        {
            public GameObject gameObject;
            public Transform transform;
            public Vector3 velocity;
            public SpriteRenderer spriteRenderer;
            public float age;
            public bool hit;
            public bool isSmall;
            public int pierceRemaining;
            public float hitCooldown;
            public List<Transform> hitTargets = new List<Transform>();
            private int _frameIndex;
            private float _frameTimer;
            private int _pingDir = 1; // 1=正向播放, -1=反向播放

            public Vector3 position => transform.position;

            public void Update(float fps, Sprite[] frames, Sprite fallbackSprite, float fixedScale, bool flipX)
            {
                if (hit) return;
                age += Time.deltaTime;

                Vector3 move = velocity * Time.deltaTime;
                move.y = 0f;
                transform.position += move;

                transform.localScale = new Vector3(fixedScale, fixedScale, 1f);

                if (frames != null && frames.Length > 0)
                {
                    _frameTimer += Time.deltaTime;
                    if (_frameTimer >= 1f / fps)
                    {
                        _frameTimer = 0f;
                        // Ping-pong: 0→15→0→15... 避免首尾不衔接的跳帧
                        if (_pingDir > 0)
                        {
                            _frameIndex++;
                            if (_frameIndex >= frames.Length - 1)
                            {
                                _frameIndex = frames.Length - 1;
                                _pingDir = -1;
                            }
                        }
                        else
                        {
                            _frameIndex--;
                            if (_frameIndex <= 0)
                            {
                                _frameIndex = 0;
                                _pingDir = 1;
                            }
                        }
                        if (spriteRenderer != null)
                            spriteRenderer.sprite = frames[_frameIndex];
                    }
                }
            }
        }

        private static Dictionary<Sprite, Vector2> _pivotCache = new Dictionary<Sprite, Vector2>();
        
        private static Vector2 CalculateSpriteCenter(Sprite sprite)
        {
            if (sprite == null || sprite.texture == null) return new Vector2(0.5f, 0.5f);
            
            if (_pivotCache.TryGetValue(sprite, out Vector2 cached))
                return cached;
            
            // Calculate actual pixel center of non-transparent content
            Texture2D tex = sprite.texture;
            Rect rect = sprite.rect;
            bool readable = tex.isReadable;
            
            if (!readable)
            {
                // Can't read pixels - use sprite's own pivot as fallback
                Vector2 fallback = sprite.pivot / new Vector2(rect.width, rect.height);
                _pivotCache[sprite] = fallback;
                return fallback;
            }
            
            Color32[] pixels = tex.GetPixels32();
            int texW = tex.width;
            int rx = (int)rect.x, ry = (int)rect.y, rw = (int)rect.width, rh = (int)rect.height;
            long sumX = 0, sumY = 0, count = 0;
            
            for (int y = ry; y < ry + rh; y++)
            {
                for (int x = rx; x < rx + rw; x++)
                {
                    if (pixels[y * texW + x].a > 10)
                    {
                        sumX += x - rx;
                        sumY += y - ry;
                        count++;
                    }
                }
            }
            
            Vector2 pivot;
            if (count > 0)
            {
                pivot = new Vector2((float)sumX / count / rw, (float)sumY / count / rh);
            }
            else
            {
                pivot = new Vector2(0.5f, 0.5f);
            }
            
            _pivotCache[sprite] = pivot;
            return pivot;
        }

        public void LevelUp()
        {
            skillLevel++;
            ApplyLevelEffects();
        }

        private void ApplyLevelEffects()
        {
            switch (skillLevel)
            {
                case 2: // 凝聚 — 伤害+20% 飞行速度+15%
                    damageMult *= 1.2f;
                    speedMultiplier *= 1.15f;
                    break;
                case 3: // 双炎 — 2枚火球
                    projectileCount = 2;
                    break;
                case 4: // 强化弹 — 大小+25% 伤害+25%
                    sizeMultiplier *= 1.25f;
                    damageMult *= 1.25f;
                    break;
                case 5: // 贯穿 — 穿透+1
                    pierceCount = 1;
                    break;
                case 6: // 连发 — 攻速+30%
                    cooldownMult *= 0.7f;
                    break;
                case 7: // 散射 — 额外2枚小火球(50%伤害)
                    scatterCount = 2;
                    break;
                case 8: // 旋炎 — 旋转轨迹(范围覆盖增加)
                    // 简化: 增加2枚额外火球覆盖更多方向
                    projectileCount += 1;
                    break;
                case 9: // 分裂弹 — 命中后概率分裂
                    canSplit = true;
                    splitChance = 0.5f;
                    break;
                case 10: // 天火流星 — 数量+3 攻速+50% 伤害+50%
                    projectileCount += 3;
                    cooldownMult *= 0.5f;
                    damageMult *= 1.5f;
                    break;
            }
        }
    }
}