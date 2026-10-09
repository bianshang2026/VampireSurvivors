using UnityEngine;
using System.Collections.Generic;

namespace BiopunkRV
{
    /// <summary>
    /// 机制型灵根词缀 — 灵根Lv3节点奖励, 自动全局生效, 不占技能词缀槽位。
    /// 区别于数值型灵根词缀: 每个机制是一条新的行为规则, 可与通用词缀组合。
    ///   雷·落雷: 雷属性命中 → 命中点召唤天降雷 (Lv5双雷/Lv8三雷)
    ///   火·火径: 火属性投射物飞行路径留下火焰地带 (节点式, 适配曲线路径)
    ///   水·漩涡: 水属性命中 → 命中点生成聚怪漩涡 (精英/Boss免疫)
    ///   土·岩脊: 土属性命中 → 命中点凸起岩刺障碍 (顶开怪+阻挡移动)
    ///   风·风行: 风属性命中 → 把怪沿命中方向推行 (精英/Boss免疫)
    /// DevPanel演示按钮直接调用 DemoXxx 静态方法 (无视灵根门控)。
    /// </summary>
    public static class RootMechanics
    {
        /// <summary>机制解锁所需灵根等级</summary>
        public const int UnlockLevel = 3;

        // ══════════════════════ 命中钩子 (AffixSystem.OnHitEnemy 末尾统一调用) ══════════════════════

        private static readonly Dictionary<AffixSystem, float> _cdThunder = new Dictionary<AffixSystem, float>();
        private static readonly Dictionary<AffixSystem, float> _cdVortex = new Dictionary<AffixSystem, float>();
        private static readonly Dictionary<AffixSystem, float> _cdSpike = new Dictionary<AffixSystem, float>();
        private static readonly Dictionary<AffixSystem, float> _cdGust = new Dictionary<AffixSystem, float>();

        /// <summary>技能命中时的机制触发入口 (每技能限流防刷屏)</summary>
        public static void OnSkillHit(AffixSystem src, Transform enemy, Vector3 hitPos, float baseDamage, Element element)
        {
            if (src == null || enemy == null) return;
            // 落雷自身(SkyThunderStrike)的词缀命中不再触发落雷, 防递归链式刷雷
            if (src.gameObject.name == "Affix_SkyThunderStrike") return;

            float now = Time.time;
            switch (element)
            {
                case Element.Thunder:
                    if (ThunderSpiritRoot.IsActive && ThunderSpiritRoot.Instance.level >= UnlockLevel
                        && CdOk(_cdThunder, src, now, 0.4f))
                    {
                        int lv = ThunderSpiritRoot.Instance.level;
                        int strikes = 1 + (lv >= 5 ? 1 : 0) + (lv >= 8 ? 1 : 0);
                        ThunderStrikeAt(hitPos, Mathf.Max(1f, baseDamage * 0.6f), strikes);
                    }
                    break;

                case Element.Water:
                    if (WaterSpiritRoot.IsActive && WaterSpiritRoot.Instance.level >= UnlockLevel
                        && CdOk(_cdVortex, src, now, 1.2f))
                    {
                        int lv = WaterSpiritRoot.Instance.level;
                        SpawnVortex(hitPos, 4f + (lv >= 5 ? 1f : 0f) + (lv >= 8 ? 1f : 0f), 1.5f);
                    }
                    break;

                case Element.Earth:
                    if (EarthSpiritRoot.IsActive && EarthSpiritRoot.Instance.level >= UnlockLevel
                        && CdOk(_cdSpike, src, now, 1.0f))
                    {
                        SpawnStoneSpine(hitPos);
                    }
                    break;

                case Element.Wind:
                    if (WindSpiritRoot.IsActive && WindSpiritRoot.Instance.level >= UnlockLevel
                        && CdOk(_cdGust, src, now, 0.5f))
                    {
                        var ec = enemy.GetComponent<EnemyController>();
                        if (ec != null)
                        {
                            int lv = WindSpiritRoot.Instance.level;
                            float force = 2f + (lv >= 5 ? 0.75f : 0f) + (lv >= 8 ? 0.75f : 0f);
                            // v1方向: 从玩家径向推出 (无投射物弹道方向时的近似)
                            Vector3 dir = enemy.position - src.transform.position;
                            ApplyGust(ec, dir, force);
                        }
                    }
                    break;
            }
        }

        private static bool CdOk(Dictionary<AffixSystem, float> map, AffixSystem key, float now, float cd)
        {
            float last;
            if (map.TryGetValue(key, out last) && now - last < cd) return false;
            map[key] = now;
            return true;
        }

        // ══════════════════════ 雷·落雷 ══════════════════════

        /// <summary>在指定位置召唤落雷 (AoE伤害+竖直闪电束闪3次+紫色冲击环)</summary>
        public static void ThunderStrikeAt(Vector3 pos, float strikeDamage, int strikes)
        {
            for (int i = 0; i < strikes; i++)
            {
                Vector3 p = pos + new Vector3(Random.Range(-0.9f, 0.9f) * i, 0f, Random.Range(-0.9f, 0.9f) * i);
                AffixVisuals.RunFx(ThunderBoltRoutine(p, Mathf.Max(1, Mathf.CeilToInt(strikeDamage)), i * 0.1f));
            }
        }

        private static System.Collections.IEnumerator ThunderBoltRoutine(Vector3 targetPos, int dmg, float delay)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);

            const float aoeRadius = 2.5f;
            foreach (var e in Object.FindObjectsOfType<EnemyController>())
            {
                if (e == null || !e.gameObject.activeInHierarchy || e.currentHealth <= 0) continue;
                float dx = targetPos.x - e.transform.position.x;
                float dz = targetPos.z - e.transform.position.z;
                if (dx * dx + dz * dz >= aoeRadius * aoeRadius) continue;
                e.TakeDamage(dmg, Element.Thunder);
                DamageNumber.CreateAt(e.transform.position + new Vector3(Random.Range(-0.5f, 0.5f), 0f, 0f), dmg, Element.Thunder);
            }

            AffixVisuals.PlayBurst(targetPos, Element.Thunder, aoeRadius, 0.3f);
            AudioManager.PlayEnemyHit();

            var beamTex = Resources.Load<Texture2D>("SkyThunderBolt");
            if (beamTex == null) beamTex = Resources.Load<Texture2D>("LightningBeam");
            if (beamTex == null) yield break;

            const float beamLen = 20f;
            var cam = Camera.main;
            Vector3 camUp = cam != null ? cam.transform.up : Vector3.up;
            Vector3 camRight = cam != null ? cam.transform.right : Vector3.right;
            Quaternion billboardRot = cam != null ? cam.transform.rotation : Quaternion.Euler(65.7f, 0f, 0f);

            var go = new GameObject("RootMechThunderBolt");
            go.transform.position = targetPos + camUp * (beamLen * 0.5f) + camRight * Random.Range(-0.4f, 0.4f);
            go.transform.rotation = billboardRot;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Sprite.Create(beamTex, new Rect(0, 0, beamTex.width, beamTex.height), new Vector2(0.5f, 0.5f), 100f);
            sr.color = Color.white; // SkyThunderBolt贴图自带紫色
            sr.sortingOrder = 54;

            float unitH = beamTex.height / 100f;
            go.transform.localScale = new Vector3(1f, beamLen / unitH, 1f);

            for (int i = 0; i < 3; i++)
            {
                sr.enabled = false;
                yield return new WaitForSeconds(0.03f);
                sr.enabled = true;
                yield return new WaitForSeconds(0.08f);
            }
            Object.Destroy(go);
        }

        // ══════════════════════ 火·火径 ══════════════════════

        private static Sprite[] _fireFrames;

        /// <summary>给投射物挂火径发射器 (火灵根Lv3+且投射物为火属性时生效)</summary>
        public static void AttachFireTrail(GameObject projectile, Element el)
        {
            if (projectile == null || el != Element.Fire) return;
            if (!FireSpiritRoot.IsActive || FireSpiritRoot.Instance.level < UnlockLevel) return;
            if (projectile.GetComponent<FireTrailEmitter>() != null) return;
            projectile.AddComponent<FireTrailEmitter>();
        }

        /// <summary>在地面位置落一个火焰节点 (存留2s, 每0.5s灼烧周围敌人)</summary>
        public static void SpawnFireNode(Vector3 groundPos)
        {
            if (_fireFrames == null)
            {
                _fireFrames = Resources.LoadAll<Sprite>("FireVFX_Frames");
                if (_fireFrames != null) System.Array.Sort(_fireFrames, (a, b) => string.CompareOrdinal(a.name, b.name));
            }
            if (_fireFrames == null || _fireFrames.Length == 0) return;

            var go = new GameObject("FireTrailNode");
            go.transform.position = groundPos;
            go.transform.rotation = Quaternion.Euler(65.69891f, 0f, 0f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _fireFrames[0];
            sr.sortingOrder = 44;

            float unitW = _fireFrames[0].bounds.size.x;
            float sc = unitW > 0.01f ? 1.4f / unitW : 1f;
            go.transform.localScale = new Vector3(sc, sc, 1f);

            var anim = go.AddComponent<VFXFrameAnimator>();
            anim.frames = _fireFrames;
            anim.startFrame = Random.Range(0, _fireFrames.Length); // frames后赋值, 首次Update补应用
            anim.fps = 14f;
            anim.loop = true;
            anim.alpha = 0.85f;

            var node = go.AddComponent<FireTrailNode>();
            node.life = 2f;
        }

        // ══════════════════════ 水·漩涡 ══════════════════════

        private static Sprite[] _waterFrames;

        /// <summary>在指定位置生成聚怪漩涡 (水漩涡卷浪帧动画+旋转, 持续把周围敌人拉向中心)</summary>
        public static void SpawnVortex(Vector3 pos, float radius, float duration)
        {
            if (_waterFrames == null)
            {
                _waterFrames = Resources.LoadAll<Sprite>("GroundPatchFrames/Water");
                if (_waterFrames != null) System.Array.Sort(_waterFrames, (a, b) => string.CompareOrdinal(a.name, b.name));
            }
            if (_waterFrames == null || _waterFrames.Length == 0) return;

            var go = new GameObject("RootMechVortex");
            go.transform.position = new Vector3(pos.x, 1.6f, pos.z);
            go.transform.rotation = Quaternion.Euler(65.69891f, 0f, 0f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _waterFrames[0];
            sr.sortingOrder = 44;

            float unitW = _waterFrames[0].bounds.size.x;
            float worldSize = radius * 2f;
            float sc = unitW > 0.01f ? worldSize / unitW : 1f;
            go.transform.localScale = new Vector3(sc, sc, 1f);

            var anim = go.AddComponent<VFXFrameAnimator>();
            anim.frames = _waterFrames;
            anim.fps = 12f;
            anim.loop = true;
            anim.alpha = 0.9f;

            var vortex = go.AddComponent<VortexPull>();
            vortex.radius = radius;
            vortex.duration = duration;
        }

        // ══════════════════════ 土·岩脊 ══════════════════════

        private static Sprite _spikeSprite;
        private static readonly List<StoneSpine> _activeSpikes = new List<StoneSpine>();
        private const int MaxSpikes = 12;

        /// <summary>在指定位置凸起岩刺障碍 (顶开重叠敌人+阻挡移动, 存留3s)</summary>
        public static void SpawnStoneSpine(Vector3 pos)
        {
            if (_spikeSprite == null)
            {
                _spikeSprite = LoadSpriteRobust("Decorations/EnvPropV2_01", "Decorations/Rock_Large");
            }
            if (_spikeSprite == null) return;

            var go = new GameObject("RootMechStoneSpine");
            Vector3 spawnPos = new Vector3(pos.x, 1.5f, pos.z);
            go.transform.position = spawnPos;
            go.transform.rotation = Quaternion.Euler(65.69891f, 0f, 0f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _spikeSprite;
            sr.sortingOrder = 46;

            float unitH = _spikeSprite.bounds.size.y;
            float targetH = 2.2f;
            float sc = unitH > 0.01f ? targetH / unitH : 1f;
            Vector3 targetScale = new Vector3(sc, sc, 1f);
            go.transform.localScale = new Vector3(sc, 0.01f, 1f); // 从地里顶起

            // 碰撞 (阻挡怪物移动 — 复用DecoObstacle标记, CheckObstacle/ResolveSolidCollisions自动生效)
            float worldW = _spikeSprite.bounds.size.x * sc;
            float colR = Mathf.Max(0.7f, worldW * 0.35f);
            var col = go.AddComponent<SphereCollider>();
            col.radius = colR / sc; // local半径补偿缩放
            go.AddComponent<DecoObstacle>();

            var spike = go.AddComponent<StoneSpine>();
            spike.targetScale = targetScale;
            spike.duration = 3f;

            _activeSpikes.Add(spike);
            if (_activeSpikes.Count > MaxSpikes && _activeSpikes[0] != null)
                _activeSpikes[0].duration = Mathf.Min(_activeSpikes[0].duration, _activeSpikes[0].Age + 0.3f);

            // 顶开重叠敌人到岩刺表面 (岩刺从地里顶起把怪顶开)
            foreach (var e in Object.FindObjectsOfType<EnemyController>())
            {
                if (e == null || !e.gameObject.activeInHierarchy || e.currentHealth <= 0) continue;
                Vector3 to = e.transform.position - spawnPos;
                to.y = 0f;
                float d = to.magnitude;
                if (d < colR + 1f)
                {
                    Vector3 push = d > 0.01f ? to.normalized : Vector3.right;
                    e.transform.position = spawnPos + push * (colR + 1f);
                }
            }

            AffixVisuals.PlayVfxBurst(spawnPos, Element.Earth, 2.5f, 0.5f);
        }

        // ══════════════════════ 风·风行 ══════════════════════

        /// <summary>把敌人沿指定方向推行 (精英/Boss免疫), 附风VFX</summary>
        public static void ApplyGust(EnemyController ec, Vector3 dir, float force)
        {
            if (ec == null || ec.isElite || ec.currentHealth <= 0) return;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.001f) dir = Vector3.right;
            ec.ApplyKnockback(dir.normalized, force);
            AffixVisuals.PlayVfxBurst(ec.transform.position, Element.Wind, 2.5f, 0.5f);
        }

        // ══════════════════════ DevPanel 演示入口 (无视灵根门控) ══════════════════════

        public static void DemoThunder()
        {
            var p = GameObject.Find("Player");
            if (p == null) return;
            var e = NearestEnemy(p.transform.position);
            if (e == null) e = SpawnDemoEnemy(p.transform.position + new Vector3(4f, 0f, 0f));
            Vector3 pos = e != null ? e.transform.position : p.transform.position + new Vector3(4f, 0f, 0f);
            float dmg = 20f;
            var psc = PlayerStatsComponent.Instance;
            if (psc != null) dmg = psc.CalculateSkillDamage(1.2f, Element.Thunder, out _);
            ThunderStrikeAt(pos, dmg, 3);
            Debug.Log("[RootMechanics] 落雷演示: 3道雷落向 " + (e != null ? e.name : "空地"));
        }

        public static void DemoFireTrail()
        {
            var p = GameObject.Find("Player");
            if (p == null) return;
            var e = NearestEnemy(p.transform.position);
            Vector3 dir = e != null ? e.transform.position - p.transform.position : Vector3.right;
            dir.y = 0f;
            dir.Normalize();

            var go = new GameObject("RootMechDemoOrb");
            go.transform.position = p.transform.position + new Vector3(0f, 2f, 0f);
            go.transform.rotation = Quaternion.Euler(65.69891f, 0f, 0f);

            var sprite = LoadSpriteRobust("FireballSpell", "BurnSpark");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = 52;
            sr.color = new Color(1f, 0.8f, 0.5f);
            if (sprite != null)
            {
                float unitW = sprite.bounds.size.x;
                float sc = unitW > 0.01f ? 2f / unitW : 0.2f;
                go.transform.localScale = new Vector3(sc, sc, 1f);
            }

            go.AddComponent<FireTrailEmitter>(); // 演示强制挂, 不走灵根门控
            var fly = go.AddComponent<DemoFly>();
            fly.direction = dir;
            fly.speed = 14f;
            fly.life = 3f;
            Debug.Log("[RootMechanics] 火径演示: 演示投射物沿" + dir + "飞行3秒");
        }

        public static void DemoVortex()
        {
            var p = GameObject.Find("Player");
            if (p == null) return;
            var e = NearestEnemy(p.transform.position);
            if (e == null) e = SpawnDemoEnemy(p.transform.position + new Vector3(4f, 0f, 0f));
            Vector3 pos = e != null ? e.transform.position : p.transform.position + new Vector3(4f, 0f, 0f);
            SpawnVortex(pos, 5f, 3f);
            Debug.Log("[RootMechanics] 漩涡演示: 半径5持续3秒");
        }

        public static void DemoStoneSpine()
        {
            var p = GameObject.Find("Player");
            if (p == null) return;
            var e = NearestEnemy(p.transform.position);
            Vector3 center = e != null ? e.transform.position : p.transform.position + new Vector3(4f, 0f, 0f);
            SpawnStoneSpine(center);
            SpawnStoneSpine(center + new Vector3(2.5f, 0f, 1.5f));
            SpawnStoneSpine(center + new Vector3(-2.5f, 0f, -1.5f));
            Debug.Log("[RootMechanics] 岩脊演示: 3根岩刺");
        }

        public static void DemoGust()
        {
            var p = GameObject.Find("Player");
            if (p == null) return;
            var e = NearestEnemy(p.transform.position);
            if (e == null) e = SpawnDemoEnemy(p.transform.position + new Vector3(4f, 0f, 0f));
            if (e == null) return;
            Vector3 dir = e.transform.position - p.transform.position;
            float before = Vector3.Distance(e.transform.position, p.transform.position);
            ApplyGust(e, dir, 3f);
            float after = Vector3.Distance(e.transform.position, p.transform.position);
            Debug.Log($"[RootMechanics] 风行演示: {e.name} 距玩家 {before:F1} → {after:F1}");
        }

        // ══════════════════════ 工具 ══════════════════════

        private static EnemyController[] _enemyCache;
        private static float _enemyCacheTime = -999f;

        /// <summary>共享敌人缓存 (0.5s刷新), 供火径节点/漩涡等高频查询用</summary>
        public static EnemyController[] Enemies
        {
            get
            {
                if (Time.time - _enemyCacheTime > 0.5f)
                {
                    _enemyCache = Object.FindObjectsOfType<EnemyController>();
                    _enemyCacheTime = Time.time;
                }
                return _enemyCache;
            }
        }

        private static EnemyController NearestEnemy(Vector3 pos)
        {
            EnemyController best = null;
            float nd = float.MaxValue;
            foreach (var e in Enemies)
            {
                if (e == null || !e.gameObject.activeInHierarchy || e.currentHealth <= 0) continue;
                float dx = e.transform.position.x - pos.x;
                float dz = e.transform.position.z - pos.z;
                float d2 = dx * dx + dz * dz;
                if (d2 < nd) { nd = d2; best = e; }
            }
            return best;
        }

        private static EnemyController SpawnDemoEnemy(Vector3 pos)
        {
            var prefab = Resources.Load<GameObject>("Enemies/GhostRabbit_Enemy");
            if (prefab == null) { Debug.LogWarning("[RootMechanics] 演示怪预制体缺失"); return null; }
            var go = Object.Instantiate(prefab, pos, prefab.transform.rotation);
            return go.GetComponent<EnemyController>();
        }

        private static Sprite LoadSpriteRobust(string path, string fallbackPath)
        {
            var sp = Resources.Load<Sprite>(path);
            if (sp == null && fallbackPath != null) sp = Resources.Load<Sprite>(fallbackPath);
            if (sp == null)
            {
                var tex = Resources.Load<Texture2D>(path);
                if (tex == null && fallbackPath != null) tex = Resources.Load<Texture2D>(fallbackPath);
                if (tex != null)
                    sp = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            }
            return sp;
        }
    }

    /// <summary>火径节点 — 存留期间每0.5s灼烧周围敌人, 末尾缩小淡出 (VFXFrameAnimator强制写alpha故用缩放淡出)</summary>
    public class FireTrailNode : MonoBehaviour
    {
        public float life = 2f;
        private float _age;
        private float _tick;
        private Vector3 _baseScale;

        private static readonly List<FireTrailNode> _active = new List<FireTrailNode>();
        private const int MaxNodes = 60;

        private void Awake()
        {
            _baseScale = transform.localScale;
            _active.Add(this);
            if (_active.Count > MaxNodes && _active[0] != null && !ReferenceEquals(_active[0], this))
                _active[0].life = Mathf.Min(_active[0].life, _active[0]._age + 0.15f);
        }

        private void OnDestroy() { _active.Remove(this); }

        private void Update()
        {
            _age += Time.deltaTime;
            if (_age >= life) { Destroy(gameObject); return; }

            float fadeStart = life - 0.5f;
            if (_age > fadeStart)
            {
                float k = 1f - (_age - fadeStart) / 0.5f;
                transform.localScale = _baseScale * Mathf.Lerp(0.3f, 1f, k);
            }

            _tick -= Time.deltaTime;
            if (_tick <= 0f)
            {
                _tick = 0.5f;
                DamageTick();
            }
        }

        private void DamageTick()
        {
            int dmg = 3;
            var psc = PlayerStatsComponent.Instance;
            bool isCrit = false;
            if (psc != null) dmg = Mathf.Max(1, psc.CalculateSkillDamage(0.12f, Element.Fire, out isCrit));

            Vector3 pos = transform.position;
            foreach (var e in RootMechanics.Enemies)
            {
                if (e == null || !e.gameObject.activeInHierarchy || e.currentHealth <= 0) continue;
                float dx = pos.x - e.transform.position.x;
                float dz = pos.z - e.transform.position.z;
                if (dx * dx + dz * dz < 1f)
                {
                    e.TakeDamage(dmg, Element.Fire);
                    DamageNumber.CreateAt(e.transform.position, dmg, Element.Fire, isCrit);
                }
            }
        }
    }

    /// <summary>火径发射器 — 挂在投射物上, 每飞行0.5单位在地面落一个火焰节点</summary>
    public class FireTrailEmitter : MonoBehaviour
    {
        public float nodeSpacing = 0.5f;
        private Vector3 _lastNodePos;

        private void Start() { _lastNodePos = transform.position; }

        private void Update()
        {
            Vector3 p = transform.position;
            float dx = p.x - _lastNodePos.x;
            float dz = p.z - _lastNodePos.z;
            if (dx * dx + dz * dz >= nodeSpacing * nodeSpacing)
            {
                _lastNodePos = p;
                RootMechanics.SpawnFireNode(new Vector3(p.x, 1.6f, p.z));
            }
        }
    }

    /// <summary>聚怪漩涡 — 持续把范围内敌人拉向中心 (精英/Boss免疫), 末尾缩小消散</summary>
    public class VortexPull : MonoBehaviour
    {
        public float radius = 4f;
        public float duration = 1.5f;
        public float pullSpeed = 3.5f;
        public float rotateSpeed = 30f;

        private float _age;
        private Vector3 _baseScale;

        private void Awake() { _baseScale = transform.localScale; }

        private void Update()
        {
            _age += Time.deltaTime;
            if (_age >= duration) { Destroy(gameObject); return; }

            transform.Rotate(0f, 0f, rotateSpeed * Time.deltaTime);

            float fadeStart = duration - 0.4f;
            if (_age > fadeStart)
            {
                float k = 1f - (_age - fadeStart) / 0.4f;
                transform.localScale = _baseScale * Mathf.Lerp(0.3f, 1f, k);
            }

            Vector3 center = transform.position;
            foreach (var e in RootMechanics.Enemies)
            {
                if (e == null || e.isElite || !e.gameObject.activeInHierarchy || e.currentHealth <= 0) continue;
                Vector3 to = center - e.transform.position;
                to.y = 0f;
                float d = to.magnitude;
                if (d < radius && d > 0.6f)
                    e.transform.position += to.normalized * pullSpeed * Time.deltaTime;
            }
        }
    }

    /// <summary>岩刺障碍 — 从地里顶起(scaleY 0→1), 存留期间阻挡移动, 末尾缩回地里</summary>
    public class StoneSpine : MonoBehaviour
    {
        public Vector3 targetScale = Vector3.one;
        public float duration = 3f;
        public float Age => _age;

        private float _age;
        private float _growT;

        private void Update()
        {
            _age += Time.deltaTime;
            if (_age >= duration) { Destroy(gameObject); return; }

            // 顶起动画 (0.15s ease-out)
            if (_growT < 1f)
            {
                _growT = Mathf.Min(1f, _growT + Time.deltaTime / 0.15f);
                float ease = 1f - (1f - _growT) * (1f - _growT);
                transform.localScale = new Vector3(targetScale.x, targetScale.y * Mathf.Max(0.01f, ease), 1f);
            }

            // 末尾缩回
            float fadeStart = duration - 0.3f;
            if (_age > fadeStart)
            {
                float k = 1f - (_age - fadeStart) / 0.3f;
                transform.localScale = new Vector3(targetScale.x, targetScale.y * Mathf.Max(0.01f, k), 1f);
            }
        }
    }

    /// <summary>演示用直线飞行物 (DevPanel火径演示)</summary>
    public class DemoFly : MonoBehaviour
    {
        public Vector3 direction = Vector3.right;
        public float speed = 14f;
        public float life = 3f;

        private void Update()
        {
            life -= Time.deltaTime;
            if (life <= 0f) { Destroy(gameObject); return; }
            transform.position += direction * speed * Time.deltaTime;
        }
    }
}
