using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;
using TMPro;

namespace BiopunkRV
{
    /// <summary>
    /// 神通系统 — 悟道后第二次满级不再立即发动, 而是储存为"神通充能",
    /// 玩家按R键手动释放全部充能 (HUD顶部显示就绪提示)。
    /// 神通分派: 御剑系→万剑天降(36柄遮天剑群平分砸敌) / 火属性→陨星雨(40颗遮天陨石平分砸敌)
    ///           / 灵气刃→万刃天落(30道刃快速倾泻) / 法阵不加入 / 其他→通用全屏。
    /// 挂在Player上 (ChargeDivine自动补挂)。
    /// </summary>
    public class DivineSystem : MonoBehaviour
    {
        public class DivineCharge
        {
            public MonoBehaviour sourceSkill;
            public string daoId;
        }

        public static DivineSystem Instance { get; private set; }

        [Header("手动释放按键")]
        public KeyCode releaseKey = KeyCode.R;

        private readonly List<DivineCharge> _charges = new List<DivineCharge>();
        private RectTransform _hudRoot;
        private TextMeshProUGUI _hudText;

        public int ChargeCount => _charges.Count;

        private void Awake()
        {
            Instance = this;
        }

        /// <summary>确保Player上有DivineSystem (技能神通充能入口)</summary>
        public static DivineSystem Ensure(Transform player)
        {
            if (player == null) return null;
            var ds = player.GetComponent<DivineSystem>();
            if (ds == null) ds = player.gameObject.AddComponent<DivineSystem>();
            return ds;
        }

        /// <summary>神通充能入口: 法阵不加入天降神通(用户指定), 其余充能</summary>
        public static void ChargeDivine(MonoBehaviour sourceSkill, string daoId)
        {
            if (sourceSkill is SpiritArraySpell || sourceSkill is SpiritArraySkill)
            {
                Debug.Log("[DivineSystem] 法阵不加入天降神通, 跳过充能");
                return;
            }

            var player = GameObject.Find("Player");
            if (player == null)
            {
                // 找不到Player时退回旧的立即全屏逻辑
                DaoSystem.TriggerDivineImmediate(daoId, sourceSkill != null ? sourceSkill.transform : null);
                return;
            }
            var ds = Ensure(player.transform);
            if (ds != null) ds.AddCharge(sourceSkill, daoId);
        }

        /// <summary>添加充能 (同一技能重复充能只保留最新)</summary>
        public void AddCharge(MonoBehaviour sourceSkill, string daoId)
        {
            _charges.RemoveAll(c => c.sourceSkill == sourceSkill);
            _charges.Add(new DivineCharge { sourceSkill = sourceSkill, daoId = daoId });
            UpdateHud();
            Debug.Log($"[DivineSystem] 神通充能: {DaoSystem.GetDaoName(daoId)} — 按{releaseKey}释放 (共{_charges.Count}个)");
        }

        private void Update()
        {
            // HUD就绪脉冲
            if (_hudRoot != null && _hudRoot.gameObject.activeSelf)
            {
                float s = 1f + 0.04f * Mathf.Sin(Time.time * 4f);
                _hudRoot.localScale = new Vector3(s, s, 1f);
            }

            if (Input.GetKeyDown(releaseKey) && _charges.Count > 0)
                ReleaseAll();
        }

        /// <summary>释放全部神通充能</summary>
        public void ReleaseAll()
        {
            if (_charges.Count == 0) return;
            var snapshot = new List<DivineCharge>(_charges);
            _charges.Clear();
            UpdateHud();
            foreach (var c in snapshot)
                ReleaseOne(c);
        }

        private void ReleaseOne(DivineCharge charge)
        {
            switch (GetKind(charge))
            {
                case DivineKind.SwordRain:
                    StartCoroutine(SwordRainRoutine(charge));
                    break;
                case DivineKind.MeteorRain:
                    StartCoroutine(MeteorRainRoutine(charge));
                    break;
                case DivineKind.BladeStorm:
                    StartCoroutine(BladeStormRoutine(charge));
                    break;
                case DivineKind.TalismanStorm:
                    StartCoroutine(TalismanStormRoutine(charge));
                    break;
                default:
                    DaoSystem.TriggerDivineImmediate(charge.daoId, transform);
                    break;
            }
        }

        private enum DivineKind { SwordRain, MeteorRain, BladeStorm, TalismanStorm, Generic }

        /// <summary>御剑系→万剑天降, 符箓→万符龙卷, 火属性→陨星雨, 灵气刃→万刃天落, 其他→通用全屏</summary>
        private DivineKind GetKind(DivineCharge charge)
        {
            if (charge.sourceSkill is FlyingSword) return DivineKind.SwordRain;
            if (charge.sourceSkill is TalismanSpell) return DivineKind.TalismanStorm;
            Element el = GetSkillElement(charge.sourceSkill);
            if (el == Element.Fire) return DivineKind.MeteorRain;
            if (charge.sourceSkill is QiBladeSpell) return DivineKind.BladeStorm;
            return DivineKind.Generic;
        }

        /// <summary>读技能element字段(反射) → 有效属性</summary>
        private static Element GetSkillElement(MonoBehaviour skill)
        {
            if (skill == null) return Element.None;
            var f = skill.GetType().GetField("element");
            if (f != null && f.FieldType == typeof(Element))
            {
                try { return SpiritRootManager.GetEffectiveElement((Element)f.GetValue(skill)); }
                catch { }
            }
            return Element.None;
        }

        // ── 陨星雨: 遮天蔽日的陨石群, 平分砸向每个敌人 (火属性神通) ──

        private System.Collections.IEnumerator MeteorRainRoutine(DivineCharge charge)
        {
            // 陨石贴图: 优先场景火球术序列帧, 回退Resources静态火球
            Sprite[] frames = null;
            Sprite spr = null;
            var fb = FindObjectOfType<FireballSpell>();
            if (fb != null && fb.fireballFrames != null && fb.fireballFrames.Length > 0)
                frames = fb.fireballFrames;
            else
                spr = Resources.Load<Sprite>("FireballSpell");
            if (frames == null && spr == null)
            {
                DaoSystem.TriggerDivineImmediate(charge.daoId, transform);
                yield break;
            }

            Debug.Log($"[DivineSystem] 陨星雨: {DaoSystem.GetDaoName("Divine_" + charge.daoId)}");
            // 遮天蔽日: 40颗陨石平分到每个敌人(单敌全砸/多敌均分), 先悬浮聚集再齐落
            var drops = DistributeTargets(40, 2.5f);
            for (int i = 0; i < drops.Count; i++)
            {
                SkyfallStrike.Strike(drops[i].pos, spr, frames, Color.white, Element.Fire,
                    3.5f, 22f, 0.6f, 0.22f, 4.5f, MeteorImpact, FallStyle.Ball, i * 0.02f, true, drops[i].target, 0.9f);
            }
        }

        /// <summary>陨石落地AoE (Element.Fire → 非火怪自动灼烧)</summary>
        private void MeteorImpact(Vector3 pos)
        {
            var psc = PlayerStatsComponent.Instance;
            var enemies = FindObjectsOfType<EnemyController>();
            foreach (var e in enemies)
            {
                if (e == null || !e.gameObject.activeInHierarchy) continue;
                float dx = pos.x - e.transform.position.x;
                float dz = pos.z - e.transform.position.z;
                if (dx * dx + dz * dz >= 4.5f * 4.5f) continue;
                int dmg = 1;
                bool isCrit = false;
                if (psc != null) dmg = psc.CalculateSkillDamage(0.4f, Element.Fire, out isCrit);
                e.TakeDamage(dmg, Element.Fire);
                DamageNumber.CreateAt(e.transform.position + new Vector3(Random.Range(-0.5f, 0.5f), 0, 0), dmg, Element.Fire, isCrit);
            }
            var rabbits = FindObjectsOfType<GhostRabbitController>();
            foreach (var r in rabbits)
            {
                if (r == null || !r.gameObject.activeInHierarchy) continue;
                float dx = pos.x - r.transform.position.x;
                float dz = pos.z - r.transform.position.z;
                if (dx * dx + dz * dz >= 4.5f * 4.5f) continue;
                int dmg = 1;
                bool isCrit = false;
                if (psc != null) dmg = psc.CalculateSkillDamage(0.4f, Element.Fire, out isCrit);
                r.TakeDamage(dmg, Element.Fire);
                DamageNumber.CreateAt(r.transform.position + new Vector3(Random.Range(-0.5f, 0.5f), 0, 0), dmg, Element.Fire, isCrit);
            }
        }

        // ── 万剑天降: 遮天蔽日的剑群, 平分砸向每个敌人 (御剑系神通) ──

        private System.Collections.IEnumerator SwordRainRoutine(DivineCharge charge)
        {
            Sprite swordSpr = SkyfallStrike.SwordSprite;
#if UNITY_EDITOR
            if (swordSpr == null)
                swordSpr = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/TJGenerators/History/FlyingSword_v2.png");
#endif
            if (swordSpr == null)
            {
                DaoSystem.TriggerDivineImmediate(charge.daoId, transform);
                yield break;
            }

            Element el = GetSkillElement(charge.sourceSkill);
            Color tint = ElementColors.GetTint(el);
            const float worldSize = 4.5f; // 巨剑(世界单位; 贴图unit≈20.48, 按unit×倍数会45单位贯穿全屏)

            Debug.Log($"[DivineSystem] 万剑天降: {DaoSystem.GetDaoName("Divine_" + charge.daoId)}");
            // 遮天蔽日: 36柄巨剑平分到每个敌人, 每敌头顶环形编队精确悬停, 先悬浮成阵再齐落
            // (fallHeight 14: 悬浮高度适中, 俯视相机的透视偏移小, 剑阵视觉上正对怪物头顶)
            var drops = DistributeSwords(36);
            for (int i = 0; i < drops.Count; i++)
            {
                SkyfallStrike.Strike(drops[i].pos, swordSpr, null, tint, el,
                    worldSize, 14f, 0.6f, 0.2f, 3.5f, p => SwordImpact(p, el), FallStyle.Sword, i * 0.02f, true, drops[i].target, 0.9f);
            }
            yield return null;
        }

        /// <summary>巨剑落地AoE</summary>
        private void SwordImpact(Vector3 pos, Element el)
        {
            var psc = PlayerStatsComponent.Instance;
            var enemies = FindObjectsOfType<EnemyController>();
            foreach (var e in enemies)
            {
                if (e == null || !e.gameObject.activeInHierarchy) continue;
                float dx = pos.x - e.transform.position.x;
                float dz = pos.z - e.transform.position.z;
                if (dx * dx + dz * dz >= 3.5f * 3.5f) continue;
                int dmg = 1;
                bool isCrit = false;
                if (psc != null) dmg = psc.CalculateSkillDamage(0.35f, el, out isCrit);
                e.TakeDamage(dmg, el);
                DamageNumber.CreateAt(e.transform.position + new Vector3(Random.Range(-0.5f, 0.5f), 0, 0), dmg, el, isCrit);
            }
            var rabbits = FindObjectsOfType<GhostRabbitController>();
            foreach (var r in rabbits)
            {
                if (r == null || !r.gameObject.activeInHierarchy) continue;
                float dx = pos.x - r.transform.position.x;
                float dz = pos.z - r.transform.position.z;
                if (dx * dx + dz * dz >= 3.5f * 3.5f) continue;
                int dmg = 1;
                bool isCrit = false;
                if (psc != null) dmg = psc.CalculateSkillDamage(0.35f, el, out isCrit);
                r.TakeDamage(dmg, el);
                DamageNumber.CreateAt(r.transform.position + new Vector3(Random.Range(-0.5f, 0.5f), 0, 0), dmg, el, isCrit);
            }
        }

        // ── 目标选取 ──

        /// <summary>
        /// 遮天蔽日式平分: totalCount个落点均匀分配到每个存活敌人
        /// (单敌=全部砸它, 两敌=各半, 多敌=均分; 余数给靠前的敌人),
        /// 每个落点在各自敌人周围spread散布并携带追踪目标; 无敌人时围绕玩家散布。
        /// </summary>
        private List<(Vector3 pos, Transform target)> DistributeTargets(int totalCount, float spread)
        {
            var enemies = new List<Transform>();
            foreach (var e in FindObjectsOfType<EnemyController>())
                if (e != null && e.gameObject.activeInHierarchy && e.currentHealth > 0) enemies.Add(e.transform);
            foreach (var r in FindObjectsOfType<GhostRabbitController>())
                if (r != null && r.gameObject.activeInHierarchy && r.currentHealth > 0) enemies.Add(r.transform);

            var result = new List<(Vector3, Transform)>(totalCount);
            if (enemies.Count == 0)
            {
                // 无敌人: 围绕玩家散布
                for (int i = 0; i < totalCount; i++)
                    result.Add((transform.position + new Vector3(Random.Range(-12f, 12f), 0f, Random.Range(-12f, 12f)), null));
                return result;
            }

            int per = totalCount / enemies.Count;
            int remainder = totalCount % enemies.Count;
            for (int e = 0; e < enemies.Count; e++)
            {
                int n = per + (e < remainder ? 1 : 0);
                for (int i = 0; i < n; i++)
                {
                    Vector3 pos = enemies[e].position + new Vector3(Random.Range(-spread, spread), 0f, Random.Range(-spread, spread));
                    result.Add((pos, enemies[e]));
                }
            }
            return result;
        }

        /// <summary>剑阵编队偏移: 第k柄剑在敌人头顶的阵型位置 (0=正上空, 1-6内环, 7-18中环, 19+外环)</summary>
        private static Vector3 SwordFormationOffset(int k)
        {
            if (k <= 0) return Vector3.zero;
            if (k <= 6)
            {
                float ang = (k - 1) * 60f * Mathf.Deg2Rad;
                return new Vector3(Mathf.Cos(ang) * 2.2f, 0f, Mathf.Sin(ang) * 2.2f);
            }
            if (k <= 18)
            {
                int j = k - 7;
                float ang = j * 30f * Mathf.Deg2Rad;
                return new Vector3(Mathf.Cos(ang) * 3.8f, 0f, Mathf.Sin(ang) * 3.8f);
            }
            int j2 = k - 19;
            float ang2 = j2 * 22.5f * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(ang2) * 5.2f, 0f, Mathf.Sin(ang2) * 5.2f);
        }

        /// <summary>
        /// 剑专用分配: totalCount柄剑平分到每个敌人, 每只敌人的剑按环形编队精确悬停在其头顶正上方。
        /// </summary>
        private List<(Vector3 pos, Transform target)> DistributeSwords(int totalCount)
        {
            var enemies = new List<Transform>();
            foreach (var e in FindObjectsOfType<EnemyController>())
                if (e != null && e.gameObject.activeInHierarchy && e.currentHealth > 0) enemies.Add(e.transform);
            foreach (var r in FindObjectsOfType<GhostRabbitController>())
                if (r != null && r.gameObject.activeInHierarchy && r.currentHealth > 0) enemies.Add(r.transform);

            var result = new List<(Vector3, Transform)>(totalCount);
            if (enemies.Count == 0)
            {
                // 无敌人: 编队悬停在玩家上空
                for (int i = 0; i < totalCount; i++)
                    result.Add((transform.position + SwordFormationOffset(i), null));
                return result;
            }

            int per = totalCount / enemies.Count;
            int remainder = totalCount % enemies.Count;
            for (int e = 0; e < enemies.Count; e++)
            {
                int n = per + (e < remainder ? 1 : 0);
                for (int i = 0; i < n; i++)
                    result.Add((enemies[e].position + SwordFormationOffset(i), enemies[e]));
            }
            return result;
        }

        // ── 万刃天落: 灵气刃快速倾泻一大堆 (灵气刃神通) ──

        private System.Collections.IEnumerator BladeStormRoutine(DivineCharge charge)
        {
            // 灵气刃贴图: 优先场景灵气刃组件, 回退History贴图
            Sprite bladeSpr = null;
            var qb = FindObjectOfType<QiBladeSpell>();
            if (qb != null) bladeSpr = qb.bladeSprite;
#if UNITY_EDITOR
            if (bladeSpr == null)
                bladeSpr = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/TJGenerators/History/Sprite_20260812_200505.png");
#endif
            if (bladeSpr == null)
            {
                DaoSystem.TriggerDivineImmediate(charge.daoId, transform);
                yield break;
            }

            Element el = GetSkillElement(charge.sourceSkill);
            Color tint = ElementColors.GetTint(el);

            Debug.Log($"[DivineSystem] 万刃天落: {DaoSystem.GetDaoName("Divine_" + charge.daoId)}");
            // 快速倾泻: 30道灵气刃, 短预警+短悬浮+快速坠落, 连续砸落不停歇
            var drops = DistributeTargets(30, 1.8f);
            for (int i = 0; i < drops.Count; i++)
            {
                SkyfallStrike.Strike(drops[i].pos, bladeSpr, null, tint, el,
                    2.8f, 20f, 0.3f, 0.15f, 2.5f, p => BladeStormImpact(p, el), FallStyle.Blade, i * 0.03f, true, drops[i].target, 0.15f);
            }
        }

        /// <summary>万刃天落落地AoE (单发0.3×, 30发连砸)</summary>
        private void BladeStormImpact(Vector3 pos, Element el)
        {
            var psc = PlayerStatsComponent.Instance;
            var enemies = FindObjectsOfType<EnemyController>();
            foreach (var e in enemies)
            {
                if (e == null || !e.gameObject.activeInHierarchy) continue;
                float dx = pos.x - e.transform.position.x;
                float dz = pos.z - e.transform.position.z;
                if (dx * dx + dz * dz >= 2.5f * 2.5f) continue;
                int dmg = 1;
                bool isCrit = false;
                if (psc != null) dmg = psc.CalculateSkillDamage(0.3f, el, out isCrit);
                e.TakeDamage(dmg, el);
                DamageNumber.CreateAt(e.transform.position + new Vector3(Random.Range(-0.5f, 0.5f), 0, 0), dmg, el, isCrit);
            }
            var rabbits = FindObjectsOfType<GhostRabbitController>();
            foreach (var r in rabbits)
            {
                if (r == null || !r.gameObject.activeInHierarchy) continue;
                float dx = pos.x - r.transform.position.x;
                float dz = pos.z - r.transform.position.z;
                if (dx * dx + dz * dz >= 2.5f * 2.5f) continue;
                int dmg = 1;
                bool isCrit = false;
                if (psc != null) dmg = psc.CalculateSkillDamage(0.3f, el, out isCrit);
                r.TakeDamage(dmg, el);
                DamageNumber.CreateAt(r.transform.position + new Vector3(Random.Range(-0.5f, 0.5f), 0, 0), dmg, el, isCrit);
            }
        }

        /// <summary>随机存活敌人位置+扰动, 无敌人时玩家周围随机</summary>
        private Vector3 RandomEnemyPos(float jitter)
        {
            var enemies = FindObjectsOfType<EnemyController>();
            var valid = new List<Vector3>();
            foreach (var e in enemies)
                if (e != null && e.gameObject.activeInHierarchy) valid.Add(e.transform.position);
            if (valid.Count > 0)
                return valid[Random.Range(0, valid.Count)] + new Vector3(Random.Range(-jitter, jitter), 0f, Random.Range(-jitter, jitter));
            return transform.position + new Vector3(Random.Range(-10f, 10f), 0f, Random.Range(-10f, 10f));
        }

        // ── 万符龙卷: 一大堆符箓成龙卷状环绕角色旋转, 持续绞杀周围敌人 (符箓神通) ──

        private System.Collections.IEnumerator TalismanStormRoutine(DivineCharge charge)
        {
            // 符箓贴图: 优先场景符箓组件, 回退History贴图
            Sprite tSpr = null;
            var ts = FindObjectOfType<TalismanSpell>();
            if (ts != null) tSpr = ts.talismanSprite;
#if UNITY_EDITOR
            if (tSpr == null)
                tSpr = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/TJGenerators/History/Sprite_20260812_163612.png");
#endif
            if (tSpr == null)
            {
                DaoSystem.TriggerDivineImmediate(charge.daoId, transform);
                yield break;
            }

            Element el = GetSkillElement(charge.sourceSkill);
            Color tint = ElementColors.GetTint(el);

            const int count = 24;           // 符箓数量
            const float duration = 5f;      // 龙卷持续时间
            const float tornadoRadius = 7f; // 绞杀判定半径(以玩家为中心)

            float unitW = tSpr.bounds.size.x;
            float sc = unitW > 0.01f ? 1.8f / unitW : 0.08f;
            var talismans = new List<Transform>();
            var spins = new float[count];
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("DivineTalisman");
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = tSpr;
                sr.sortingOrder = 55;
                sr.color = tint;
                go.transform.localScale = new Vector3(sc, sc, 1f);
                go.transform.rotation = Quaternion.Euler(65.69891f, 0f, 0f);
                talismans.Add(go.transform);
                spins[i] = Random.Range(220f, 420f) * (Random.value < 0.5f ? 1f : -1f);
            }

            Debug.Log($"[DivineSystem] 万符龙卷: {count}张符箓环绕角色旋转{duration}秒");
            float t = 0f, tick = 0f;
            var player = transform;
            while (t < duration)
            {
                float dt = Time.deltaTime;
                t += dt;
                for (int i = 0; i < count; i++)
                {
                    if (talismans[i] == null) continue;
                    // 螺旋上升: 高度相位0→1循环上升, 半径随高度变宽(锥形龙卷)
                    float hNorm = Mathf.Repeat(t * 0.5f + i * (1f / count), 1f);
                    float phase = t * 1.6f + i * (Mathf.PI * 2f / count);
                    float radius = 2.5f + hNorm * 4.5f;
                    float height = 1f + hNorm * 9f;
                    talismans[i].position = player.position + new Vector3(Mathf.Cos(phase) * radius, height, Mathf.Sin(phase) * radius);
                    talismans[i].Rotate(0f, 0f, spins[i] * dt);
                }

                // 龙卷绞杀: 每0.4s对玩家周围tornadoRadius内敌人造成伤害
                tick -= dt;
                if (tick <= 0f)
                {
                    tick = 0.4f;
                    var psc = PlayerStatsComponent.Instance;
                    bool isCrit = false;
                    foreach (var e in FindObjectsOfType<EnemyController>())
                    {
                        if (e == null || !e.gameObject.activeInHierarchy || e.currentHealth <= 0) continue;
                        float dx = e.transform.position.x - player.position.x;
                        float dz = e.transform.position.z - player.position.z;
                        if (dx * dx + dz * dz >= tornadoRadius * tornadoRadius) continue;
                        int dmg = 1;
                        if (psc != null) dmg = psc.CalculateSkillDamage(0.7f, el, out isCrit);
                        e.TakeDamage(dmg, el);
                        DamageNumber.CreateAt(e.transform.position, dmg, el, isCrit);
                    }
                    foreach (var r in FindObjectsOfType<GhostRabbitController>())
                    {
                        if (r == null || !r.gameObject.activeInHierarchy || r.currentHealth <= 0) continue;
                        float dx = r.transform.position.x - player.position.x;
                        float dz = r.transform.position.z - player.position.z;
                        if (dx * dx + dz * dz >= tornadoRadius * tornadoRadius) continue;
                        int dmg = 1;
                        if (psc != null) dmg = psc.CalculateSkillDamage(0.7f, el, out isCrit);
                        r.TakeDamage(dmg, el);
                        DamageNumber.CreateAt(r.transform.position, dmg, el, isCrit);
                    }
                }
                yield return null;
            }

            // 收尾: 符箓四散飞向最近敌人 (归敌散符, 每张0.25×小AoE)
            for (int i = 0; i < count; i++)
                if (talismans[i] != null) StartCoroutine(TalismanFlyOut(talismans[i], el, i * 0.03f));
        }

        /// <summary>符箓收尾飞行: 追踪最近敌人命中造成0.25×AoE伤害, 无敌人时向外飘散</summary>
        private System.Collections.IEnumerator TalismanFlyOut(Transform talisman, Element el, float delay)
        {
            yield return new WaitForSeconds(delay);
            float life = 2f;
            while (life > 0f && talisman != null)
            {
                float dt = Time.deltaTime;
                life -= dt;

                // 最近敌人 (共享缓存, 防逐帧全场景扫描)
                EnemyController best = null;
                float nd = 25f * 25f;
                foreach (var e in RootMechanics.Enemies)
                {
                    if (e == null || !e.gameObject.activeInHierarchy || e.currentHealth <= 0) continue;
                    float dx = e.transform.position.x - talisman.position.x;
                    float dz = e.transform.position.z - talisman.position.z;
                    float d2 = dx * dx + dz * dz;
                    if (d2 < nd) { nd = d2; best = e; }
                }

                if (best == null)
                {
                    // 无敌人: 向外飘散自毁
                    Vector3 outDir = talisman.position - transform.position;
                    outDir.y = 0f;
                    if (outDir.sqrMagnitude < 0.01f) outDir = Vector3.right;
                    talisman.position += outDir.normalized * 12f * dt;
                    talisman.Rotate(0f, 0f, 300f * dt);
                }
                else
                {
                    Vector3 dir = best.transform.position - talisman.position;
                    dir.y = 0f;
                    float dist = dir.magnitude;
                    dir.Normalize();
                    talisman.position += dir * 28f * dt;
                    // 下降: 从龙卷高度俯冲向敌人所在高度
                    float targetY = best.transform.position.y + 1f;
                    talisman.position = new Vector3(talisman.position.x, Mathf.MoveTowards(talisman.position.y, targetY, 12f * dt), talisman.position.z);
                    talisman.Rotate(0f, 0f, 400f * dt);
                    if (dist < 1.2f)
                    {
                        var psc = PlayerStatsComponent.Instance;
                        bool isCrit = false;
                        int dmg = 1;
                        if (psc != null) dmg = psc.CalculateSkillDamage(0.25f, el, out isCrit);
                        foreach (var e in RootMechanics.Enemies)
                        {
                            if (e == null || !e.gameObject.activeInHierarchy || e.currentHealth <= 0) continue;
                            float dx = e.transform.position.x - talisman.position.x;
                            float dz = e.transform.position.z - talisman.position.z;
                            if (dx * dx + dz * dz < 4f)
                            {
                                e.TakeDamage(dmg, el);
                                DamageNumber.CreateAt(e.transform.position, dmg, el, isCrit);
                            }
                        }
                        AffixVisuals.PlayVfxBurst(talisman.position, el, 2f, 0.5f);
                        Destroy(talisman.gameObject);
                        yield break;
                    }
                }
                yield return null;
            }
            if (talisman != null) Destroy(talisman.gameObject);
        }

        /// <summary>敌人位置质心 (万剑天降阵列中心), 无敌人时玩家前方</summary>
        private Vector3 EnemyClusterCenter()
        {
            var enemies = FindObjectsOfType<EnemyController>();
            Vector3 sum = Vector3.zero;
            int n = 0;
            foreach (var e in enemies)
            {
                if (e == null || !e.gameObject.activeInHierarchy) continue;
                sum += e.transform.position;
                n++;
            }
            if (n > 0) return sum / n;
            return transform.position + transform.forward * 8f;
        }

        // ── HUD 神通就绪指示器 ──

        private void UpdateHud()
        {
            if (_charges.Count > 0)
            {
                if (_hudRoot == null) CreateHud();
                if (_hudRoot != null)
                {
                    _hudRoot.gameObject.SetActive(true);
                    if (_hudText != null)
                    {
                        string divineName = GetDivineDisplayName(_charges[0]);
                        string extra = _charges.Count > 1 ? $" 等{_charges.Count}项神通" : "";
                        _hudText.text = $"⚡ 神通就绪 [{releaseKey}] · {divineName}{extra}";
                    }
                }
            }
            else if (_hudRoot != null)
            {
                _hudRoot.gameObject.SetActive(false);
            }
        }

        private string GetDivineDisplayName(DivineCharge charge)
        {
            switch (GetKind(charge))
            {
                case DivineKind.SwordRain: return "万剑天降";
                case DivineKind.MeteorRain: return "陨星雨";
                case DivineKind.BladeStorm: return "万刃天落";
                case DivineKind.TalismanStorm: return "万符龙卷";
                default: return DaoSystem.GetDaoName("Divine_" + charge.daoId);
            }
        }

        private void CreateHud()
        {
            var canvasGo = GameObject.Find("HUD_Canvas");
            if (canvasGo == null) return;

            var rootGo = new GameObject("DivineReadyIndicator");
            _hudRoot = rootGo.AddComponent<RectTransform>();
            _hudRoot.SetParent(canvasGo.transform, false);
            _hudRoot.anchorMin = new Vector2(0.5f, 1f);
            _hudRoot.anchorMax = new Vector2(0.5f, 1f);
            _hudRoot.pivot = new Vector2(0.5f, 1f);
            _hudRoot.anchoredPosition = new Vector2(0f, -130f);
            _hudRoot.sizeDelta = new Vector2(560f, 56f);

            var bg = rootGo.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.05f, 0.02f, 0.7f);
            bg.raycastTarget = false;

            var textGo = new GameObject("Text");
            var textRt = textGo.AddComponent<RectTransform>();
            textRt.SetParent(_hudRoot, false);
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;

            _hudText = textGo.AddComponent<TextMeshProUGUI>();
            _hudText.fontSize = 26;
            _hudText.alignment = TextAlignmentOptions.Center;
            _hudText.color = new Color(1f, 0.85f, 0.4f); // 金色

            rootGo.SetActive(false);
        }
    }
}
