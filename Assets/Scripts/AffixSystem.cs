using UnityEngine;
using System.Collections.Generic;

namespace BiopunkRV
{
    // ── 词缀类别 ──
    public enum AffixCategory
    {
        Generic,        // 通用词缀 (20种)
        SpiritRoot,     // 灵根词缀 (15种)
        SkillSpecific   // 术法专属 (10种)
    }

    // ── 词缀类型 (45种) ──
    public enum AffixType
    {
        // 通用词缀 (20)
        Split, Pierce, Track, Orbit, Amplify, Bounce, Return, Multiply,
        Focus, Spread, Lingering, Combo, Execute, SpiritDrain, Charge,
        Delay, Swift, Heavy, MultiShot, Burst, Refract, Hunt, DeepPierce, Shatter, Fall,
        // 灵根词缀 — 火 (3)
        BurnEnhance, BurnExplode, BurnSpread,
        // 灵根词缀 — 水 (3)
        SlowEnhance, Ripple, Freeze,
        // 灵根词缀 — 土 (3)
        Quake, ShieldBody, KnockbackEnhance,
        // 灵根词缀 — 风 (3)
        Haste, ProjectileSpeed, WindBlade,
        // 灵根词缀 — 雷 (3)
        Chain, ThunderCrit, Paralyze,
        // 术法专属 — 御剑术 (2)
        SwordArray, SwordQi,
        // 术法专属 — 灵球术 (2)
        OrbCharge, OrbBurst,
        // 术法专属 — 灵阵 (2)
        ArrayExpand, ArrayStack,
        // 术法专属 — 灵刃 (2)
        BladeSlash, BladeWind,
        // 术法专属 — 符箓术 (2)
        TalismanChain, TalismanScatter,
        // 反应词缀 — 关系型 (词缀改变技能之间的关系: 触发→场域→增益)
        BurnReap,          // 焚尽: 灼烧敌死亡→原地生成火焰场域 (需裂火)
        YanLunJue,         // 炎轮诀: 火焰场域生成时→半径内飞剑获得分裂 (需剑阵)
        Prairie,           // 燎原: 火焰场域点燃的敌人死亡→同样走焚尽 (联动焚尽, 零额外代码)
        WindFire,          // 风火轮: 穿过火焰场域的飞剑附加焚烧 (需风刃)

        // 融合属性词缀 — 炎 (3, 预留)
        FlameSpread, FlameStorm, FlameDomain,
        // 融合属性词缀 — 冰 (3, 预留)
        FrostSeal, FrostShatter, FrostChain,
        // 融合属性词缀 — 草 (3, 预留)
        WoodGrowth, WoodBind, WoodParasite,
        // 融合属性词缀 — 金 (3, 预留)
        MetalBreak, MetalEdge, MetalWall
    }

    /// <summary>
    /// 单个词缀实例
    /// </summary>
    [System.Serializable]
    public class AffixInstance
    {
        public AffixType type;
        public AffixCategory category;
        public int unlockLevel;  // 在哪一级解锁 (2/4/6/8/10)
    }

    /// <summary>
    /// 词缀系统核心 — 挂在每个技能脚本上，管理该技能的5个词缀槽位。
    /// Lv2/4/6/8/10各解锁1个词缀槽位。
    /// </summary>
    public class AffixSystem : MonoBehaviour
    {
        // 预设的5个词缀 (局外功法编辑系统设置, 或局内升级时选择)
        public AffixType[] presetAffixes = new AffixType[5]
        {
            AffixType.Split, AffixType.Pierce, AffixType.Multiply,
            AffixType.Track, AffixType.Lingering
        };

        /// <summary>词缀槽位解锁等级 (槽位0-4 ↔ Lv2/4/6/8/10)</summary>
        public static readonly int[] UnlockLevels = { 2, 4, 6, 8, 10 };

        [Header("功法计划模式")]
        [Tooltip("true=词缀由局外功法设计决定, 不随等级自动激活, 由局内升级选项逐个解锁")]
        public bool plannedMode = false;

        // 功法计划模式下各槽位是否已通过局内升级选项解锁
        private readonly bool[] _unlockedSlots = new bool[5];

        /// <summary>重置全部槽位为未解锁 (应用新功法时调用)</summary>
        public void ResetPlannedSlots()
        {
            for (int i = 0; i < 5; i++) _unlockedSlots[i] = false;
        }

        /// <summary>解锁指定槽位 (局内升级选项被选取时调用)</summary>
        public void UnlockPlannedSlot(int slot)
        {
            if (slot >= 0 && slot < 5) _unlockedSlots[slot] = true;
        }

        /// <summary>下一个未解锁槽位索引, -1=全部解锁</summary>
        public int NextPlannedSlot()
        {
            for (int i = 0; i < 5; i++) if (!_unlockedSlots[i]) return i;
            return -1;
        }

        /// <summary>
        /// 获取/创建技能专属的AffixSystem。
        /// 多个技能组件常挂在同一GameObject(Player)上, 必须每技能一个独立子物体,
        /// 否则GetComponent只会返回第一个导致词缀全局共享。
        /// </summary>
        public static AffixSystem GetOrCreateFor(System.Type skillType, GameObject host)
        {
            string childName = "Affix_" + skillType.Name;
            var existing = host.transform.Find(childName);
            if (existing != null)
            {
                var found = existing.GetComponent<AffixSystem>();
                if (found != null) return found;
            }
            var obj = new GameObject(childName);
            obj.transform.SetParent(host.transform, false);
            return obj.AddComponent<AffixSystem>();
        }

        public static AffixSystem GetOrCreateFor(Component skill)
        {
            return GetOrCreateFor(skill.GetType(), skill.gameObject);
        }

        /// <summary>玩家是否拥有某词缀 (跨全部技能检查, 反应网络/升级权重用)</summary>
        public static bool AnyOwned(AffixType type)
        {
            foreach (var a in AllActive())
                if (a.HasAffix(type)) return true;
            return false;
        }

        /// <summary>遍历全部活动的AffixSystem (反应网络: 关系词缀需要跨技能检查组成词缀)</summary>
        public static IEnumerable<AffixSystem> AllActive()
        {
            foreach (var a in FindObjectsOfType<AffixSystem>())
                if (a != null && a.gameObject.activeInHierarchy) yield return a;
        }

        // 已激活的词缀 (根据当前等级解锁)
        private List<AffixInstance> _activeAffixes = new List<AffixInstance>(5);

        // 词缀效果参数 (供技能脚本读取)
        [Header("分裂")]
        public int splitCount = 2;
        public float splitDamageRatio = 0.5f;
        public float splitScaleRatio = 0.5f;

        [Header("穿透")]
        public int bonusPierce = 2;

        [Header("追踪")]
        public bool canTrack = false;
        public float trackRange = 8f;

        [Header("环绕")]
        public bool orbitMode = false;
        public float orbitRadius = 5f;

        [Header("增幅")]
        public bool ampMode = false;
        public float ampDamagePerDist = 0.015f; // 每1单位飞行距离+1.5%伤害

        [Header("折射")]
        public float refractAngle = 15f; // 命中后随机偏折±15°

        [Header("寻敌")]
        public float huntTurnRate = 2.5f; // 持续小幅转向速率(弧度/秒)

        [Header("贯穿强化")]
        public float deepPierceBonus = 0.2f; // 每次穿透伤害递增20%

        [Header("崩解")]
        public float shatterBonus = 0.5f; // 命中满血敌人+50%

        // 词缀行为模式标志 (供技能脚本读取)
        public bool refractMode = false;    // 折射: 命中后弹道随机偏折±15°继续飞
        public bool huntMode = false;       // 寻敌: 飞行中持续小幅转向追踪
        public bool deepPierceMode = false; // 贯穿强化: 每次穿透伤害递增20%
        public bool shatterMode = false;    // 崩解: 命中满血敌人额外+50%

        [Header("坠落")]
        public bool fallMode = false;       // 坠落: 投射物从天而降砸向敌人
        public float fallAoeMult = 1.5f;    // 坠落落地AoE半径倍率(相对技能命中半径)
        public float fallHeight = 22f;      // 坠落起始高度
        public float fallWarnTime = 0.5f;   // 预警圈时长
        public float fallFallTime = 0.28f;  // 坠落时长

        [Header("弹射")]
        public int bonusBounce = 2;

        [Header("回旋")]
        public bool returnPhase = false;
        public float returnDamageRatio = 0.6f;

        [Header("增殖")]
        public int killBonus = 0;
        public int maxKillBonus = 5;

        [Header("聚灵")]
        public bool focusMode = false;
        public float focusDamageMult = 1.8f;

        [Header("扩散")]
        public float spreadRadiusMult = 1.5f;

        [Header("余威")]
        public bool lingeringArea = false;
        public float lingeringDuration = 3f;
        public float lingeringDpsRatio = 0.2f;

        [Header("连杀")]
        public float comboTimer = 0f;
        public float comboBonus = 0f;
        public float comboMaxBonus = 1f;

        [Header("斩杀")]
        public float executeThreshold = 0.05f;

        [Header("吸灵")]
        public int spiritDrainExp = 5;

        [Header("蓄力")]
        public bool chargeMode = false;
        public float chargeTime = 1f;
        public float chargeDamageMult = 2f;

        [Header("回响(原延迟)")]
        public float delayTime = 3f;           // 回响印记3秒后爆发
        public float delayDamageBonus = 1.3f;  // 回响伤害1.3倍

        [Header("追风")]
        public float speedMult = 1.5f;

        [Header("重击")]
        public float knockbackForce = 3f;

        [Header("连发")]
        public int extraProjectiles = 1;

        [Header("灵爆")]
        public float burstDamageRatio = 0.3f;
        public float burstRadius = 3f;

        [Header("焚烧强化")]
        public float burnEnhanceRatio = 0.3f;

        [Header("爆裂(火)")]
        public float burnExplodeRadius = 4f;
        public float burnExplodeRatio = 0.6f;

        [Header("燃传播")]
        public float burnSpreadRadius = 5f;

        [Header("深流(水)")]
        public float slowEnhanceFactor = 0.25f;
        public float slowEnhanceDuration = 2.5f;

        [Header("水波")]
        public float rippleRadius = 4f;
        public float rippleDamageRatio = 0.35f;

        [Header("冻结")]
        public float freezeChance = 0.25f;
        public float freezeDuration = 1.5f;

        [Header("震击(土)")]
        public float quakeChance = 0.2f;
        public float quakeDuration = 1f;

        [Header("护体")]
        public float shieldAuraRadius = 4.5f;

        [Header("震退")]
        public float knockbackEnhanceForce = 5f;

        [Header("疾风/追风(风)")]
        public float hasteSpeedMult = 1.3f;

        [Header("风刃")]
        public float windBladeChance = 0.3f;
        public float windBladeDamageRatio = 0.5f;

        [Header("连锁(雷)")]
        public int chainCount = 2;
        public float chainDamageRatio = 0.6f;
        public float chainRadius = 9f;

        [Header("雷暴")]
        public float thunderCritBonus = 0.15f;

        [Header("麻痹")]
        public float paralyzeChance = 0.2f;
        public float paralyzeDuration = 0.8f;

        [Header("剑阵")]
        public int swordArrayBonus = 1;

        [Header("剑气")]
        public float swordQiRadius = 3.5f;
        public float swordQiDamageRatio = 0.4f;

        [Header("聚能(球)")]
        public float orbChargeDamageMult = 1.5f;

        [Header("灵爆(球)")]
        public float orbBurstRadiusMult = 1.5f;

        [Header("扩阵")]
        public float arrayExpandMult = 1.5f;

        [Header("叠阵")]
        public int arrayStackBonus = 1;

        [Header("切裂(刃)")]
        public float bladeSlashDamageRatio = 0.3f;

        [Header("刃风")]
        public float bladeWindRadius = 3f;
        public float bladeWindDamageRatio = 0.25f;

        [Header("连符/散符")]
        public int talismanChainBonus = 1;
        public int talismanScatterBonus = 2;

        // 击杀计数 (增殖词缀用)
        [System.NonSerialized] public int killCount = 0;

        /// <summary>
        /// 根据技能等级更新已激活的词缀
        /// </summary>
        public void UpdateAffixes(int skillLevel)
        {
            _activeAffixes.Clear();
            for (int i = 0; i < 5; i++)
            {
                // 功法计划模式: 只激活已通过局内升级选项解锁的槽位; 否则按等级自动解锁
                bool active = plannedMode ? _unlockedSlots[i] : skillLevel >= UnlockLevels[i];
                if (active)
                {
                    _activeAffixes.Add(new AffixInstance
                    {
                        type = presetAffixes[i],
                        category = GetCategory(presetAffixes[i]),
                        unlockLevel = UnlockLevels[i]
                    });
                }
            }
            ApplyAffixParameters();
            RestoreForceAffixes(); // 词缀实验室: 恢复强制词缀 (防止每帧重建被清掉)

            // 反应网络: 首个词缀系统活动时挂接事件钩子 (幂等, 内部有防重复)
            if (_activeAffixes.Count > 0) FireReactions.EnsureHooked();
        }

        /// <summary>
        /// 是否拥有某词缀
        /// </summary>
        public bool HasAffix(AffixType type)
        {
            foreach (var a in _activeAffixes)
                if (a.type == type) return true;
            return false;
        }

        /// <summary>
        /// 设置预设词缀 (局外功法编辑系统调用)
        /// </summary>
        public void SetPresetAffix(int slot, AffixType type)
        {
            if (slot >= 0 && slot < 5)
                presetAffixes[slot] = type;
        }

        /// <summary>
        /// 获取额外投射物数量 (连发+增殖+剑阵+连符+散符)
        /// </summary>
        public int GetBonusProjectiles()
        {
            int bonus = 0;
            if (HasAffix(AffixType.MultiShot)) bonus += extraProjectiles;
            if (HasAffix(AffixType.Multiply)) bonus += Mathf.Min(killCount, maxKillBonus);
            if (HasAffix(AffixType.Focus)) bonus -= 1; // 聚灵减少数量
            if (HasAffix(AffixType.SwordArray)) bonus += swordArrayBonus;         // 剑阵: +剑
            if (HasAffix(AffixType.TalismanChain)) bonus += talismanChainBonus;   // 连符: +符
            if (HasAffix(AffixType.TalismanScatter)) bonus += talismanScatterBonus; // 散符: +2符
            return Mathf.Max(0, bonus);
        }

        /// <summary>
        /// 获取伤害倍率 (聚灵+连杀+延迟+蓄力+聚能)
        /// </summary>
        public float GetDamageMult()
        {
            float mult = 1f;
            if (HasAffix(AffixType.Focus)) mult *= focusDamageMult;
            if (HasAffix(AffixType.Combo)) mult *= (1f + comboBonus);
            if (HasAffix(AffixType.Delay)) mult *= delayDamageBonus;
            if (HasAffix(AffixType.Charge)) mult *= chargeDamageMult;
            if (HasAffix(AffixType.OrbCharge)) mult *= orbChargeDamageMult; // 聚能: 球伤害+
            return mult;
        }

        /// <summary>
        /// 获取范围倍率 (扩散+扩阵+灵爆球)
        /// </summary>
        public float GetRadiusMult()
        {
            float mult = 1f;
            if (HasAffix(AffixType.Spread)) mult *= spreadRadiusMult;
            if (HasAffix(AffixType.Focus)) mult *= 1.5f; // 聚灵也增大范围
            if (HasAffix(AffixType.ArrayExpand)) mult *= arrayExpandMult;     // 扩阵: 法阵范围+
            if (HasAffix(AffixType.OrbBurst)) mult *= orbBurstRadiusMult;     // 灵爆: 球爆范围+
            return mult;
        }

        /// <summary>
        /// 获取速度倍率 (追风+疾风)
        /// </summary>
        public float GetSpeedMult()
        {
            float mult = 1f;
            if (HasAffix(AffixType.Swift)) mult *= speedMult;
            if (HasAffix(AffixType.Haste)) mult *= hasteSpeedMult;             // 疾风: 弹速+
            if (HasAffix(AffixType.ProjectileSpeed)) mult *= hasteSpeedMult;   // 追风(灵根): 弹速+
            return mult;
        }

        /// <summary>
        /// 获取穿透数 (穿透词缀)
        /// </summary>
        public int GetBonusPierce()
        {
            return HasAffix(AffixType.Pierce) ? bonusPierce : 0;
        }

        /// <summary>
        /// 获取弹射数 (弹射词缀)
        /// </summary>
        public int GetBonusBounce()
        {
            return HasAffix(AffixType.Bounce) ? bonusBounce : 0;
        }

        /// <summary>
        /// 命中回调 (在技能命中敌人时调用)
        /// </summary>
        public void OnHitEnemy(Transform enemy, float baseDamage, Element element)
        {
            Vector3 hitPos = enemy != null ? enemy.position : transform.position;

            // 斩杀 — HP低于阈值直接处决+星爆
            if (HasAffix(AffixType.Execute))
            {
                var ec = enemy.GetComponent<EnemyController>();
                if (ec != null && ec.currentHealth > 0 && ec.currentHealth < ec.maxHealth * executeThreshold)
                {
                    AffixVisuals.PlayStarPop(hitPos, element, 2f);
                    ec.ForceKill();
                    return;
                }
            }

            // 吸灵 — 加经验 + 白点飞向玩家
            if (HasAffix(AffixType.SpiritDrain))
            {
                var t = System.Type.GetType("BiopunkRV.GhostRabbitController");
                if (t != null)
                {
                    var m = t.GetMethod("AddExp", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    if (m != null) m.Invoke(null, new object[] { (float)spiritDrainExp });
                }
                var player = GameObject.Find("Player");
                if (player != null)
                    AffixVisuals.PlayAbsorbDots(hitPos, player.transform.position);
            }

            // 灵爆 — 同心圆范围伤害+视觉
            if (HasAffix(AffixType.Burst))
            {
                AffixVisuals.PlayBurst(hitPos, element, burstRadius);
                var enemies = FindObjectsOfType<EnemyController>();
                foreach (var e in enemies)
                {
                    if (e == null || !e.gameObject.activeInHierarchy) continue;
                    float dx = enemy.position.x - e.transform.position.x;
                    float dz = enemy.position.z - e.transform.position.z;
                    if (dx * dx + dz * dz < burstRadius * burstRadius)
                    {
                        int burstDmg = Mathf.Max(1, Mathf.CeilToInt(baseDamage * burstDamageRatio));
                        e.TakeDamage(burstDmg, element);
                        DamageNumber.CreateAt(e.transform.position, burstDmg, element);
                    }
                }
            }

            // 余威 — 命中点留下属性专属地块特效 (燃火地/水洼/裂地/雷击地...); 无属性不留残留
            if (HasAffix(AffixType.Lingering) && element != Element.None)
            {
                float lRadius = 3f * GetRadiusMult();
                AffixVisuals.PlayGroundPatch(hitPos, element, lRadius, lingeringDuration);
                StartCoroutine(LingeringDpsRoutine(hitPos, lRadius, lingeringDuration, baseDamage * lingeringDpsRatio, element));
            }

            // 回响(原延迟) — 敌人身上留印记, 3秒后跟随该敌人爆发130%伤害
            if (HasAffix(AffixType.Delay) && enemy != null)
            {
                float dRadius = 3f * GetRadiusMult();
                StartCoroutine(EchoBurstRoutine(enemy, dRadius, delayTime, baseDamage, element));
            }

            // 重击 — 击退 + 敌人白闪
            if (HasAffix(AffixType.Heavy))
            {
                var ecH = enemy.GetComponent<EnemyController>();
                if (ecH != null)
                {
                    Vector3 dir = (enemy.position - transform.position).normalized;
                    dir.y = 0;
                    ecH.ApplyKnockback(dir, knockbackForce);
                }
                var enemySr = enemy.GetComponent<SpriteRenderer>();
                if (enemySr != null) AffixVisuals.PlayFlashWhite(enemySr, 0.15f);
            }

            // ═══ 灵根词缀 (命中触发) ═══

            var ecHit = enemy != null ? enemy.GetComponent<EnemyController>() : null;

            // 焚烧 — 命中灼烧中的敌人追加火伤
            if (HasAffix(AffixType.BurnEnhance) && ecHit != null && ecHit.IsBurning)
            {
                int burnBonus = Mathf.Max(1, Mathf.CeilToInt(baseDamage * burnEnhanceRatio));
                ecHit.TakeDamage(burnBonus, Element.Fire);
                DamageNumber.CreateAt(hitPos, burnBonus, Element.Fire);
            }

            // 爆裂 — 命中灼烧敌人时爆炸波及周围
            if (HasAffix(AffixType.BurnExplode) && ecHit != null && ecHit.IsBurning)
            {
                AffixVisuals.PlayBurst(hitPos, Element.Fire, burnExplodeRadius, 0.4f);
                int exDmg = Mathf.Max(1, Mathf.CeilToInt(baseDamage * burnExplodeRatio));
                foreach (var e in FindObjectsOfType<EnemyController>())
                {
                    if (e == null || e == ecHit || !e.gameObject.activeInHierarchy || e.currentHealth <= 0) continue;
                    float dx = e.transform.position.x - hitPos.x;
                    float dz = e.transform.position.z - hitPos.z;
                    if (dx * dx + dz * dz < burnExplodeRadius * burnExplodeRadius)
                    {
                        e.TakeDamage(exDmg, Element.Fire);
                        DamageNumber.CreateAt(e.transform.position, exDmg, Element.Fire);
                    }
                }
            }

            // 燃传播 — 命中灼烧敌人时周围敌人也着火(+火VFX帧蔓延视觉)
            if (HasAffix(AffixType.BurnSpread) && ecHit != null && ecHit.IsBurning)
            {
                foreach (var e in FindObjectsOfType<EnemyController>())
                {
                    if (e == null || e == ecHit || !e.gameObject.activeInHierarchy || e.currentHealth <= 0) continue;
                    float dx = e.transform.position.x - hitPos.x;
                    float dz = e.transform.position.z - hitPos.z;
                    if (dx * dx + dz * dz < burnSpreadRadius * burnSpreadRadius)
                    {
                        e.ApplyBurnFromSpread();
                        AffixVisuals.PlayVfxBurst(e.transform.position, Element.Fire, 2.2f, 0.7f);
                    }
                }
            }

            // 深流 — 命中强化减速 + 脚下小水圈
            if (HasAffix(AffixType.SlowEnhance) && ecHit != null)
            {
                ecHit.ApplySlow(slowEnhanceFactor, slowEnhanceDuration);
                AffixVisuals.PlayGroundCircle(hitPos, Element.Water, 1.8f, 0.8f, false);
            }

            // 水波 — 命中扩散水波伤害周围
            if (HasAffix(AffixType.Ripple))
            {
                AffixVisuals.PlayBurst(hitPos, Element.Water, rippleRadius, 0.4f);
                int rpDmg = Mathf.Max(1, Mathf.CeilToInt(baseDamage * rippleDamageRatio));
                foreach (var e in FindObjectsOfType<EnemyController>())
                {
                    if (e == null || e == ecHit || !e.gameObject.activeInHierarchy || e.currentHealth <= 0) continue;
                    float dx = e.transform.position.x - hitPos.x;
                    float dz = e.transform.position.z - hitPos.z;
                    if (dx * dx + dz * dz < rippleRadius * rippleRadius)
                    {
                        e.TakeDamage(rpDmg, Element.Water);
                        DamageNumber.CreateAt(e.transform.position, rpDmg, Element.Water);
                    }
                }
            }

            // 冻结 — 几率冻结定身 + 冰晶
            if (HasAffix(AffixType.Freeze) && ecHit != null && Random.value < freezeChance)
            {
                ecHit.ApplyStun(freezeDuration);
                AffixVisuals.PlayGroundCircle(hitPos, Element.Water, 2.2f, freezeDuration, false);
                AffixVisuals.PlayIceCrystal(hitPos, 1.6f);
            }

            // 震击 — 几率眩晕 + 土尘VFX帧
            if (HasAffix(AffixType.Quake) && ecHit != null && Random.value < quakeChance)
            {
                ecHit.ApplyStun(quakeDuration);
                AffixVisuals.PlayVfxBurst(hitPos, Element.Earth, 3f, 0.8f);
            }

            // 震退 — 命中击退 + 尘土
            if (HasAffix(AffixType.KnockbackEnhance) && ecHit != null)
            {
                Vector3 kbDir = (enemy.position - transform.position).normalized;
                kbDir.y = 0f;
                ecHit.ApplyKnockback(kbDir, knockbackEnhanceForce);
                AffixVisuals.PlayVfxBurst(hitPos, Element.Earth, 1.6f, 0.4f);
            }

            // 风刃 — 几率飞出风刃追击最近敌人
            if (HasAffix(AffixType.WindBlade) && Random.value < windBladeChance)
            {
                StartCoroutine(WindBladeRoutine(hitPos, baseDamage * windBladeDamageRatio));
            }

            // 连锁 — 电弧依次跳向附近敌人
            if (HasAffix(AffixType.Chain))
            {
                StartCoroutine(ChainRoutine(ecHit, hitPos, baseDamage));
            }

            // 麻痹 — 几率麻痹 + 紫电VFX帧缠绕
            if (HasAffix(AffixType.Paralyze) && ecHit != null && Random.value < paralyzeChance)
            {
                ecHit.ApplyStun(paralyzeDuration);
                AffixVisuals.PlayVfxBurst(hitPos, Element.Thunder, 2.5f, paralyzeDuration + 0.5f);
            }

            // 雷暴 — 命中几率小雷爆(电VFX帧)
            if (HasAffix(AffixType.ThunderCrit) && Random.value < 0.3f)
            {
                AffixVisuals.PlayVfxBurst(hitPos, Element.Thunder, 1.8f, 0.5f);
            }

            // ═══ 术法专属词缀 (命中触发) ═══

            // 剑气 — 剑气波扩散伤害
            if (HasAffix(AffixType.SwordQi))
            {
                AffixVisuals.PlaySlashWave(hitPos, element, swordQiRadius);
                int sqDmg = Mathf.Max(1, Mathf.CeilToInt(baseDamage * swordQiDamageRatio));
                foreach (var e in FindObjectsOfType<EnemyController>())
                {
                    if (e == null || e == ecHit || !e.gameObject.activeInHierarchy || e.currentHealth <= 0) continue;
                    float dx = e.transform.position.x - hitPos.x;
                    float dz = e.transform.position.z - hitPos.z;
                    if (dx * dx + dz * dz < swordQiRadius * swordQiRadius)
                    {
                        e.TakeDamage(sqDmg, element);
                        DamageNumber.CreateAt(e.transform.position, sqDmg, element);
                    }
                }
            }

            // 切裂 — 敌身切割线 + 额外伤害
            if (HasAffix(AffixType.BladeSlash) && ecHit != null)
            {
                AffixVisuals.PlaySlashFlash(hitPos, element);
                int bsDmg = Mathf.Max(1, Mathf.CeilToInt(baseDamage * bladeSlashDamageRatio));
                ecHit.TakeDamage(bsDmg, element);
                DamageNumber.CreateAt(hitPos, bsDmg, element);
            }

            // 刃风 — 命中处风环伤害
            if (HasAffix(AffixType.BladeWind))
            {
                AffixVisuals.PlayBurst(hitPos, Element.Wind, bladeWindRadius, 0.3f);
                int bwDmg = Mathf.Max(1, Mathf.CeilToInt(baseDamage * bladeWindDamageRatio));
                foreach (var e in FindObjectsOfType<EnemyController>())
                {
                    if (e == null || e == ecHit || !e.gameObject.activeInHierarchy || e.currentHealth <= 0) continue;
                    float dx = e.transform.position.x - hitPos.x;
                    float dz = e.transform.position.z - hitPos.z;
                    if (dx * dx + dz * dz < bladeWindRadius * bladeWindRadius)
                    {
                        e.TakeDamage(bwDmg, Element.Wind);
                        DamageNumber.CreateAt(e.transform.position, bwDmg, Element.Wind);
                    }
                }
            }

            // 连杀计时
            if (HasAffix(AffixType.Combo))
            {
                comboTimer = 5f;
                comboBonus = Mathf.Min(comboMaxBonus, comboBonus + 0.1f);
            }

            // ═══ 机制型灵根词缀 (灵根Lv3+自动全局生效, 不占槽位; 落雷/漩涡/岩脊/风行按命中属性触发) ═══
            RootMechanics.OnSkillHit(this, enemy, hitPos, baseDamage, element);
        }

        /// <summary>余威持续伤害协程 — 属性地块上每0.5s对范围内敌人造成伤害</summary>
        private System.Collections.IEnumerator LingeringDpsRoutine(Vector3 pos, float radius, float dur, float dps, Element el)
        {
            float tickInterval = 0.5f;
            float elapsed = 0f;
            int tickDmg = Mathf.Max(1, Mathf.CeilToInt(dps * tickInterval));
            while (elapsed < dur)
            {
                elapsed += tickInterval;
                yield return new WaitForSeconds(tickInterval);
                var enemies = FindObjectsOfType<EnemyController>();
                foreach (var e in enemies)
                {
                    if (e == null || !e.gameObject.activeInHierarchy) continue;
                    float dx = pos.x - e.transform.position.x;
                    float dz = pos.z - e.transform.position.z;
                    if (dx * dx + dz * dz < radius * radius)
                    {
                        e.TakeDamage(tickDmg, el);
                        DamageNumber.CreateAt(e.transform.position, tickDmg, el);
                    }
                }
            }
        }

        /// <summary>回响协程 — 敌人身上留印记(紫色星标记闪烁), 3秒后跟随该敌人爆发130%范围伤害</summary>
        private System.Collections.IEnumerator EchoBurstRoutine(Transform enemy, float radius, float delay, float baseDamage, Element el)
        {
            // 印记闪烁提示 (3秒内每0.5s一次小星爆)
            float markT = 0f;
            while (markT < delay && enemy != null)
            {
                markT += 0.5f;
                if (enemy != null)
                    AffixVisuals.PlayStarPop(enemy.position, el, 0.6f);
                yield return new WaitForSeconds(0.5f);
            }

            // 爆发点 = 敌人死亡时最后位置 / 存活时当前位置
            Vector3 burstPos = enemy != null ? enemy.position : Vector3.zero;
            AffixVisuals.PlayBurst(burstPos, el, radius, 0.35f);

            int burstDmg = Mathf.Max(1, Mathf.CeilToInt(baseDamage * delayDamageBonus));
            var enemies = FindObjectsOfType<EnemyController>();
            foreach (var e in enemies)
            {
                if (e == null || !e.gameObject.activeInHierarchy) continue;
                float dx = burstPos.x - e.transform.position.x;
                float dz = burstPos.z - e.transform.position.z;
                if (dx * dx + dz * dz < radius * radius)
                {
                    e.TakeDamage(burstDmg, el);
                    DamageNumber.CreateAt(e.transform.position, burstDmg, el);
                }
            }
        }

        /// <summary>风刃协程 — 生成小风刃飞向最近敌人命中造成伤害</summary>
        private System.Collections.IEnumerator WindBladeRoutine(Vector3 pos, float damage)
        {
            var windBlade = AffixVisuals.GetWindBladeSprite(); // 优先WindBlade贴图, 缺失回退WindOrb
            if (windBlade == null) yield break;

            var go = new GameObject("AffixWindBlade");
            go.transform.position = pos + new Vector3(0, 1f, 0);
            go.transform.rotation = Quaternion.Euler(65.69891f, 0, 0);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = windBlade;
            sr.sortingOrder = 50;
            float unitW = windBlade.bounds.size.x;
            float sc = 1.6f / unitW; // 世界尺寸1.6单位
            go.transform.localScale = new Vector3(sc, sc, 1);

            float life = 2f;
            float minFlight = 0.15f; // 最短飞行时间, 保证风刃视觉可见
            int dmg = Mathf.Max(1, Mathf.CeilToInt(damage));
            while (life > 0f && go != null)
            {
                life -= Time.deltaTime;
                minFlight -= Time.deltaTime;
                EnemyController nearest = null;
                float nd = 999f;
                foreach (var e in FindObjectsOfType<EnemyController>())
                {
                    if (e == null || !e.gameObject.activeInHierarchy || e.currentHealth <= 0) continue;
                    float dx = e.transform.position.x - go.transform.position.x;
                    float dz = e.transform.position.z - go.transform.position.z;
                    float d2 = dx * dx + dz * dz;
                    if (d2 < 0.64f) continue; // 跳过0.8单位内贴脸敌(风刃出生宿敌)
                    if (d2 < nd) { nd = d2; nearest = e; }
                }
                if (nearest == null) break;
                Vector3 dir = nearest.transform.position - go.transform.position;
                dir.y = 0f;
                dir.Normalize();
                go.transform.position += dir * 22f * Time.deltaTime;
                if (minFlight <= 0f && nd < 6.25f) // 2.5单位半径(平方值)
                {
                    nearest.TakeDamage(dmg, Element.Wind);
                    DamageNumber.CreateAt(nearest.transform.position, dmg, Element.Wind);
                    AffixVisuals.PlayStarPop(nearest.transform.position, Element.Wind, 0.7f);
                    break;
                }
                yield return null;
            }
            if (go != null) Destroy(go);
        }

        /// <summary>连锁协程 — 电弧依次跳向附近敌人(60%伤害)</summary>
        private System.Collections.IEnumerator ChainRoutine(EnemyController first, Vector3 pos, float baseDamage)
        {
            var hitSet = new System.Collections.Generic.HashSet<EnemyController>();
            if (first != null) hitSet.Add(first);
            Vector3 from = pos;
            int dmg = Mathf.Max(1, Mathf.CeilToInt(baseDamage * chainDamageRatio));

            for (int i = 0; i < chainCount; i++)
            {
                EnemyController next = null;
                float nd = chainRadius * chainRadius;
                foreach (var e in FindObjectsOfType<EnemyController>())
                {
                    if (e == null || !e.gameObject.activeInHierarchy || e.currentHealth <= 0 || hitSet.Contains(e)) continue;
                    float dx = e.transform.position.x - from.x;
                    float dz = e.transform.position.z - from.z;
                    float d2 = dx * dx + dz * dz;
                    if (d2 < nd) { nd = d2; next = e; }
                }
                if (next == null) yield break;

                AffixVisuals.PlayLightningBeam(from, next.transform.position);
                yield return new WaitForSeconds(0.09f);
                next.TakeDamage(dmg, Element.Thunder);
                DamageNumber.CreateAt(next.transform.position, dmg, Element.Thunder);
                hitSet.Add(next);
                from = next.transform.position;
            }
        }

        /// <summary>
        /// 击杀回调 (在击杀敌人时调用)
        /// </summary>
        public void OnKillEnemy()
        {
            if (HasAffix(AffixType.Multiply))
                killCount = Mathf.Min(killCount + 1, maxKillBonus);

            if (HasAffix(AffixType.Combo))
            {
                comboTimer = 5f;
                // 连杀金色星爆 — 层数越高星越大, 击杀反馈可感知
                float starScale = 0.8f + comboBonus * 0.8f;
                AffixVisuals.PlayStarPop(transform.position, new Color(1f, 0.84f, 0.2f), starScale);
            }
        }

        /// <summary>
        /// 每帧更新 (连杀计时器/护体光环)
        /// </summary>
        public void Tick()
        {
            if (HasAffix(AffixType.Combo))
            {
                if (comboTimer > 0f)
                {
                    comboTimer -= Time.deltaTime;
                    if (comboTimer <= 0f) comboBonus = 0f;
                }
            }

            // 护体 — 玩家常驻土金旋转法阵圈 (全局唯一, 由拥有词缀的技能管理)
            if (HasAffix(AffixType.ShieldBody))
            {
                if (AffixVisuals.ShieldAuraOwner == null || System.Object.ReferenceEquals(AffixVisuals.ShieldAuraOwner, this))
                {
                    var psc = PlayerStatsComponent.Instance;
                    if (psc != null)
                    {
                        AffixVisuals.ShieldAuraOwner = this;
                        AffixVisuals.PlayShieldAura(psc.transform);
                    }
                }
            }
            else if (System.Object.ReferenceEquals(AffixVisuals.ShieldAuraOwner, this))
            {
                AffixVisuals.StopShieldAura();
            }
        }

        // ── 词缀实验室 (DevPanel用: 绕槽位限制直接增删) ──
        private readonly List<AffixType> _forceAffixes = new List<AffixType>();

        public void ForceAddAffix(AffixType type)
        {
            if (!_forceAffixes.Contains(type)) _forceAffixes.Add(type);
            if (!HasAffix(type))
            {
                _activeAffixes.Add(new AffixInstance { type = type, category = GetCategory(type), unlockLevel = 0 });
                ApplyAffixParameters();
            }
        }

        public void ForceRemoveAffix(AffixType type)
        {
            _forceAffixes.Remove(type);
            _activeAffixes.RemoveAll(a => a.type == type);
            ApplyAffixParameters();
        }

        public void ClearForceAffixes()
        {
            foreach (var fa in _forceAffixes)
                _activeAffixes.RemoveAll(a => a.type == fa);
            _forceAffixes.Clear();
            ApplyAffixParameters();
        }

        /// <summary>UpdateAffixes重建后恢复实验室强制词缀</summary>
        private void RestoreForceAffixes()
        {
            foreach (var fa in _forceAffixes)
                if (!HasAffix(fa))
                    _activeAffixes.Add(new AffixInstance { type = fa, category = GetCategory(fa), unlockLevel = 0 });
        }

        private void ApplyAffixParameters()
        {
            canTrack = HasAffix(AffixType.Track);
            orbitMode = HasAffix(AffixType.Orbit);
            returnPhase = HasAffix(AffixType.Return);
            focusMode = HasAffix(AffixType.Focus);
            lingeringArea = HasAffix(AffixType.Lingering);
            chargeMode = HasAffix(AffixType.Charge);
            ampMode = HasAffix(AffixType.Amplify);
            refractMode = HasAffix(AffixType.Refract);
            huntMode = HasAffix(AffixType.Hunt);
            deepPierceMode = HasAffix(AffixType.DeepPierce);
            shatterMode = HasAffix(AffixType.Shatter);
            fallMode = HasAffix(AffixType.Fall);
        }

        private static AffixCategory GetCategory(AffixType type)
        {
            if (type >= AffixType.BurnEnhance && type <= AffixType.Paralyze) return AffixCategory.SpiritRoot;
            if (type >= AffixType.SwordArray) return AffixCategory.SkillSpecific;
            return AffixCategory.Generic;
        }

        /// <summary>
        /// 获取词缀显示名 (中文)
        /// </summary>
        public static string GetAffixName(AffixType type)
        {
            switch (type)
            {
                // 通用 (20)
                case AffixType.Split: return "分裂";
                case AffixType.Pierce: return "穿透";
                case AffixType.Track: return "追踪";
                case AffixType.Orbit: return "环绕";
                case AffixType.Amplify: return "增幅";
                case AffixType.Refract: return "折射";
                case AffixType.Hunt: return "寻敌";
                case AffixType.DeepPierce: return "贯穿强化";
                case AffixType.Shatter: return "崩解";
                case AffixType.Fall: return "坠落";
                case AffixType.Bounce: return "弹射";
                case AffixType.Return: return "回旋";
                case AffixType.Multiply: return "增殖";
                case AffixType.Focus: return "聚灵";
                case AffixType.Spread: return "扩散";
                case AffixType.Lingering: return "余威";
                case AffixType.Combo: return "连杀";
                case AffixType.Execute: return "斩杀";
                case AffixType.SpiritDrain: return "吸灵";
                case AffixType.Charge: return "蓄力";
                case AffixType.Delay: return "延迟";
                case AffixType.Swift: return "追风";
                case AffixType.Heavy: return "重击";
                case AffixType.MultiShot: return "连发";
                case AffixType.Burst: return "灵爆";
                // 火灵根
                case AffixType.BurnEnhance: return "焚烧";
                case AffixType.BurnExplode: return "爆裂";
                case AffixType.BurnSpread: return "燃传播";
                // 水灵根
                case AffixType.SlowEnhance: return "深流";
                case AffixType.Ripple: return "水波";
                case AffixType.Freeze: return "冻结";
                // 土灵根
                case AffixType.Quake: return "震击";
                case AffixType.ShieldBody: return "护体";
                case AffixType.KnockbackEnhance: return "震退";
                // 风灵根
                case AffixType.Haste: return "疾风";
                case AffixType.ProjectileSpeed: return "追风";
                case AffixType.WindBlade: return "风刃";
                // 雷灵根
                case AffixType.Chain: return "连锁";
                case AffixType.ThunderCrit: return "雷暴";
                case AffixType.Paralyze: return "麻痹";
                // 术法专属
                case AffixType.SwordArray: return "剑阵";
                case AffixType.SwordQi: return "剑气";
                case AffixType.OrbCharge: return "聚能";
                case AffixType.OrbBurst: return "灵爆";
                case AffixType.ArrayExpand: return "扩阵";
                case AffixType.ArrayStack: return "叠阵";
                case AffixType.BladeSlash: return "切裂";
                case AffixType.BladeWind: return "刃风";
                case AffixType.TalismanChain: return "连符";
                case AffixType.TalismanScatter: return "散符";
                // 反应词缀 (关系型)
                case AffixType.BurnReap: return "焚尽";
                case AffixType.YanLunJue: return "炎轮诀";
                case AffixType.Prairie: return "燎原";
                case AffixType.WindFire: return "风火轮";
                // 融合属性 (预留)
                case AffixType.FlameSpread: return "炎扩散";
                case AffixType.FlameStorm: return "炎风暴";
                case AffixType.FlameDomain: return "炎域";
                case AffixType.FrostSeal: return "冰封";
                case AffixType.FrostShatter: return "冰碎";
                case AffixType.FrostChain: return "冰传";
                case AffixType.WoodGrowth: return "生长";
                case AffixType.WoodBind: return "缠绕";
                case AffixType.WoodParasite: return "寄生";
                case AffixType.MetalBreak: return "破甲";
                case AffixType.MetalEdge: return "金锋";
                case AffixType.MetalWall: return "铁壁";
                default: return type.ToString();
            }
        }

        /// <summary>
        /// 获取词缀效果说明 (功法选择器/升级选项描述用)
        /// </summary>
        public static string GetAffixDesc(AffixType type)
        {
            switch (type)
            {
                // 通用 (20)
                case AffixType.Split: return "命中后分裂出2个小投射物（50%伤害）";
                case AffixType.Pierce: return "投射物额外穿透2个敌人";
                case AffixType.Track: return "投射物自动追踪敌人";
                case AffixType.Orbit: return "投射物环绕自身持续伤害";
                case AffixType.Amplify: return "飞行越远伤害越高（每10单位+15%，上限2倍）";
                case AffixType.Bounce: return "投射物命中后额外弹射2次";
                case AffixType.Return: return "投射物飞出后回旋，返程造成60%伤害";
                case AffixType.Multiply: return "每击杀1个敌人投射物+1（最多+5）";
                case AffixType.Focus: return "数量-1，伤害×1.8，范围×1.5";
                case AffixType.Spread: return "作用范围×1.5";
                case AffixType.Lingering: return "命中点留下属性地块，持续造成伤害";
                case AffixType.Combo: return "5秒内连续命中伤害递增（最高+100%）";
                case AffixType.Execute: return "直接处决生命低于5%的敌人";
                case AffixType.SpiritDrain: return "命中额外获得5点经验";
                case AffixType.Charge: return "蓄力后释放，伤害×2";
                case AffixType.Delay: return "命中留下印记，3秒后跟随敌人爆发130%伤害";
                case AffixType.Swift: return "投射物速度×1.5";
                case AffixType.Heavy: return "命中击退敌人并使其白闪";
                case AffixType.MultiShot: return "投射物数量+1";
                case AffixType.Burst: return "命中产生3格范围爆炸（30%伤害）";
                case AffixType.Refract: return "穿透瞬间弹道随机偏折±15°继续飞行";
                case AffixType.Hunt: return "飞行中持续小幅转向追踪敌人";
                case AffixType.DeepPierce: return "每次穿透伤害递增20%";
                case AffixType.Shatter: return "命中满血敌人额外+50%伤害";
                case AffixType.Fall: return "释放形式改为天降，落地1.5倍范围伤害";
                // 火灵根
                case AffixType.BurnEnhance: return "命中灼烧中的敌人追加30%火伤";
                case AffixType.BurnExplode: return "灼烧敌人被命中时爆炸，波及4格（60%伤害）";
                case AffixType.BurnSpread: return "灼烧向5格内的敌人蔓延";
                // 水灵根
                case AffixType.SlowEnhance: return "命中强化减速25%，持续2.5秒";
                case AffixType.Ripple: return "命中扩散水波，4格范围35%伤害";
                case AffixType.Freeze: return "25%几率冻结敌人1.5秒";
                // 土灵根
                case AffixType.Quake: return "20%几率眩晕敌人1秒";
                case AffixType.ShieldBody: return "玩家常驻护体法阵光环";
                case AffixType.KnockbackEnhance: return "命中强力击退敌人";
                // 风灵根
                case AffixType.Haste: return "投射物速度+30%";
                case AffixType.ProjectileSpeed: return "投射物速度+30%";
                case AffixType.WindBlade: return "30%几率发射追踪小风刃（50%伤害）";
                // 雷灵根
                case AffixType.Chain: return "电弧依次跳向2个附近敌人（60%伤害）";
                case AffixType.ThunderCrit: return "暴击率+15%，命中几率触发小雷爆";
                case AffixType.Paralyze: return "20%几率麻痹敌人0.8秒";
                // 术法专属
                case AffixType.SwordArray: return "飞剑数量+1";
                case AffixType.SwordQi: return "命中扩散剑气波，3.5格范围40%伤害";
                case AffixType.OrbCharge: return "灵球伤害×1.5";
                case AffixType.OrbBurst: return "灵球爆炸范围×1.5";
                case AffixType.ArrayExpand: return "法阵范围×1.5";
                case AffixType.ArrayStack: return "法阵可同时存在数量+1";
                case AffixType.BladeSlash: return "命中追加30%切割伤害";
                case AffixType.BladeWind: return "命中处产生风环，3格范围25%伤害";
                case AffixType.TalismanChain: return "符箓数量+1";
                case AffixType.TalismanScatter: return "符箓数量+2";
                // 反应词缀 (关系型)
                case AffixType.BurnReap: return "灼烧的敌人死亡时原地生成火焰，持续点燃周围敌人（需裂火）";
                case AffixType.YanLunJue: return "火焰场域生成时，附近飞剑获得分裂（需剑阵）";
                case AffixType.Prairie: return "火焰点燃的敌人死亡同样触发焚尽（与焚尽联动）";
                case AffixType.WindFire: return "穿过火焰的飞剑命中附加焚烧（需风刃）";
                // 融合属性 (预留)
                case AffixType.FlameSpread: return "炎属性词缀（预留）";
                case AffixType.FlameStorm: return "炎属性词缀（预留）";
                case AffixType.FlameDomain: return "炎属性词缀（预留）";
                case AffixType.FrostSeal: return "冰属性词缀（预留）";
                case AffixType.FrostShatter: return "冰属性词缀（预留）";
                case AffixType.FrostChain: return "冰属性词缀（预留）";
                case AffixType.WoodGrowth: return "草属性词缀（预留）";
                case AffixType.WoodBind: return "草属性词缀（预留）";
                case AffixType.WoodParasite: return "草属性词缀（预留）";
                case AffixType.MetalBreak: return "金属性词缀（预留）";
                case AffixType.MetalEdge: return "金属性词缀（预留）";
                case AffixType.MetalWall: return "金属性词缀（预留）";
                default: return "";
            }
        }
    }
}
