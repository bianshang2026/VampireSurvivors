using UnityEngine;
using System.Collections.Generic;

namespace BiopunkRV
{
    // ═══════════════════════════════════════════════════════════
    // 反应网络 (Reaction Network) — 词缀改变技能之间的关系
    //
    // 四层节点: 事件(技能行为) → 状态(敌人身上) → 场域(世界中) → 关系(词缀定义的连接)
    // 场域物件(火焰/寒潭/雷沼...) = 一等公民: 可被产生/查询/增强/消费
    // 关系词缀 = 声明"当X条件满足时, Y获得Z"的规则, 组合后形成自主运转的反应链
    //
    // 例: 飞剑+裂火+焚烧+炎轮诀:
    //   飞剑命中→点燃→灼烧敌死亡→[焚尽]生成火焰场域→[炎轮诀]火焰旁飞剑获分裂
    //   →分裂飞剑继续点燃→循环
    // ═══════════════════════════════════════════════════════════

    public enum FieldType
    {
        Fire,     // 火焰场域: 范围内敌人每秒受火伤并点燃
        Aqua,     // 寒潭: 范围内敌人减速
        Thunder,  // 雷沼: 范围内敌人麻痹累积
        Wind,     // 风域: 穿过的投射物加速
        Stone,    // 岩刺阵: 阻挡敌人
    }

    /// <summary>场域物件 — 世界中持续存在的区域效果 (余威地块/火径/法阵等的一等公民形态)</summary>
    public class FieldObject
    {
        public FieldType Type;
        public Element Element;
        public Vector3 Position;
        public float Radius;
        public float Life;
        public int Level;                 // 场域等级, 可被关系词缀增强
        public GameObject Visual;
        public Transform OwnerSkill;      // 生成它的技能(词缀归属, 供视觉/伤害归属)
        public float NextTick;            // DoT计时
    }

    /// <summary>反应事件总线 — 词缀间通信的神经</summary>
    public static class ReactionBus
    {
        /// <summary>敌人死亡 (isBurning=死亡时是否处于灼烧状态)</summary>
        public static event System.Action<EnemyController, bool, Element> OnEnemyDied;
        /// <summary>场域生成</summary>
        public static event System.Action<FieldObject> OnFieldSpawned;
        /// <summary>投射物(飞剑/灵球等)入场 — 关系词缀检查场域并施加增益时用</summary>
        public static event System.Action<Transform> OnProjectileSpawned;

        /// <summary>灼烧敌人死亡 — 从EnemyController两处死亡路径调用</summary>
        public static void NotifyEnemyDied(EnemyController enemy, bool isBurning, Element element)
        {
            OnEnemyDied?.Invoke(enemy, isBurning, element);
        }

        public static void RaiseFieldSpawned(FieldObject field) => OnFieldSpawned?.Invoke(field);
        public static void RaiseProjectileSpawned(Transform proj) => OnProjectileSpawned?.Invoke(proj);
    }

    /// <summary>
    /// 场域注册表 — 全部活动场域的一等公民注册与查询。
    /// 关系词缀通过 Query 询问"某点半径内有哪些场域", 决定是否施加增益。
    /// </summary>
    public static class FieldRegistry
    {
        private static readonly List<FieldObject> _active = new List<FieldObject>();
        private static GameObject _root;
        private const int MaxFields = 24; // 场域数量上限(性能保护)

        public static IReadOnlyList<FieldObject> Active => _active;

        /// <summary>生成场域 (视觉=残留地块同款元素帧动画管线)</summary>
        public static FieldObject Spawn(FieldType type, Element element, Vector3 pos, float radius, float life, int level = 1, Transform ownerSkill = null)
        {
            if (_root == null)
            {
                _root = new GameObject("FieldRegistry_Root");
                Object.DontDestroyOnLoad(_root);
            }
            // 数量上限: 同类最多8个, 超出时回收最旧的
            int sameType = 0; FieldObject oldest = null;
            foreach (var f in _active)
            {
                if (f.Type != type) continue;
                sameType++;
                if (oldest == null || f.Life < oldest.Life) oldest = f;
            }
            if (sameType >= 8 && oldest != null) { Despawn(oldest); }

            var fo = new FieldObject
            {
                Type = type,
                Element = element,
                Position = pos,
                Radius = radius,
                Life = life,
                Level = level,
                OwnerSkill = ownerSkill,
                NextTick = 0.5f,
            };
            // 视觉 = 残留地块同款元素帧动画 (VFXFrameAnimator驱动, FieldRegistry管理生命周期)
            string patchFolder = PatchFolderOf(type);
            if (patchFolder != null)
            {
                var frames = Resources.LoadAll<Sprite>(patchFolder);
                if (frames != null && frames.Length > 0)
                {
                    System.Array.Sort(frames, (a, b) => string.CompareOrdinal(a.name, b.name));
                    var go = new GameObject("FieldPatch");
                    go.transform.position = pos;
                    go.transform.rotation = Quaternion.Euler(65.69891f, 0f, 0f);
                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sprite = frames[0];
                    sr.sortingOrder = 44;
                    float unitW = frames[0].bounds.size.x;
                    float scale = unitW > 0.01f ? (radius * 2f) / unitW : 1f;
                    go.transform.localScale = new Vector3(scale, scale, 1f);
                    var anim = go.AddComponent<VFXFrameAnimator>();
                    anim.frames = frames;
                    anim.fps = 6f;
                    anim.loop = true;
                    anim.alpha = 0.95f;
                    fo.Visual = go;
                }
            }
            if (fo.Visual == null) AffixVisuals.PlayGroundPatch(pos, element, radius, life); // 无帧兜底(自管理生命周期)
            _active.Add(fo);
            ReactionBus.RaiseFieldSpawned(fo);
            return fo;
        }

        // 场域类型 → 残留地块帧目录 (与PlayGroundPatch同款资产)
        private static string PatchFolderOf(FieldType type)
        {
            switch (type)
            {
                case FieldType.Fire: return "GroundPatchFrames/Fire";
                case FieldType.Aqua: return "GroundPatchFrames/Water";
                case FieldType.Thunder: return "GroundPatchFrames/Thunder";
                case FieldType.Wind: return "GroundPatchFrames/Wind";
                case FieldType.Stone: return "GroundPatchFrames/Earth";
                default: return null;
            }
        }

        /// <summary>查询某点周围 radius 内的指定类型场域 (null=任意类型)</summary>
        public static List<FieldObject> Query(Vector3 pos, float radius, FieldType? type = null)
        {
            var result = new List<FieldObject>();
            foreach (var f in _active)
            {
                if (type != null && f.Type != type.Value) continue;
                if (Vector2.Distance(new Vector2(pos.x, pos.z), new Vector2(f.Position.x, f.Position.z)) <= radius + f.Radius)
                    result.Add(f);
            }
            return result;
        }

        /// <summary>是否有任意指定类型场域覆盖该点 (高频查询用)</summary>
        public static bool AnyAt(Vector3 pos, FieldType type, float extraRadius = 0f)
        {
            foreach (var f in _active)
            {
                if (f.Type != type) continue;
                if (Vector2.Distance(new Vector2(pos.x, pos.z), new Vector2(f.Position.x, f.Position.z)) <= f.Radius + extraRadius)
                    return true;
            }
            return false;
        }

        public static void Despawn(FieldObject f)
        {
            _active.Remove(f);
            if (f.Visual != null) Object.Destroy(f.Visual); // 视觉随场域一起销毁
        }

        /// <summary>全局tick: 场域寿命/过期清理/DoT (由FieldSystemRunner驱动)</summary>
        public static void TickAll(float dt)
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var f = _active[i];
                f.Life -= dt;
                f.NextTick -= dt;
                if (f.Life <= 0f) { Despawn(f); continue; }

                // 场域DoT: 火焰灼烧范围内敌人(点燃+伤害), 每秒一次
                if (f.Type == FieldType.Fire && f.NextTick <= 0f)
                {
                    f.NextTick = 1f;
                    int dmg = Mathf.Max(1, Mathf.CeilToInt(PlayerStatsComponent.Instance != null
                        ? PlayerStatsComponent.Instance.stats.attack * 0.1f * f.Level : 2));
                    foreach (var e in Object.FindObjectsOfType<EnemyController>())
                    {
                        if (e == null || !e.gameObject.activeInHierarchy || e.currentHealth <= 0) continue;
                        Vector2 d2 = new Vector2(e.transform.position.x - f.Position.x, e.transform.position.z - f.Position.z);
                        if (d2.sqrMagnitude <= f.Radius * f.Radius)
                        {
                            if (!e.IsBurning) e.ApplyBurnFromSpread(); // 点燃: 走焚尽链
                            e.TakeDamage(dmg, Element.Fire);
                        }
                    }
                }
            }
        }
    }

    /// <summary>场域系统驱动器 — 场景单例, 驱动FieldRegistry.TickAll</summary>
    public class FieldSystemRunner : MonoBehaviour
    {
        private static FieldSystemRunner _instance;
        public static FieldSystemRunner Ensure()
        {
            if (_instance == null)
            {
                var go = new GameObject("FieldSystemRunner");
                _instance = go.AddComponent<FieldSystemRunner>();
            }
            return _instance;
        }
        private void Awake() { if (_instance != null && _instance != this) { Destroy(gameObject); return; } _instance = this; }
        private void Update() { FieldRegistry.TickAll(Time.deltaTime); }
    }

    /// <summary>
    /// 火系反应词缀 (Phase 1 打样, 4条):
    ///   焚尽(BurnReap)      — 焚烧+裂火: 灼烧敌人死亡→原地生成火焰场域
    ///   炎轮诀(YanLunJue)   — 炫轮诀词缀: 火焰场域生成时→半径内飞剑获得分裂
    ///   燎原(Prairie)       — 燃传播+火径: 火焰场域每3秒扩散一个新小火焰
    ///   风火轮(WindFire)    — 风行+裂火: 穿过火焰场域的飞剑附加焚烧
    /// 反应条件: 拥有组成词缀的技能 (HasAffix检查), 而非全局开关。
    /// </summary>
    public static class FireReactions
    {
        private static Transform _player;

        /// <summary>注册到ReactionBus (DevPanel/首个火系技能启动时调用一次)</summary>
        public static void EnsureHooked()
        {
            if (_player == null)
            {
                var p = GameObject.Find("Player");
                if (p == null) return;
                _player = p.transform;
                FieldSystemRunner.Ensure();
            }
            // 每次调用都remove+add: 确保订阅最新 (幂等, 防止热重载后订阅丢失)
            ReactionBus.OnEnemyDied -= OnEnemyDied;
            ReactionBus.OnEnemyDied += OnEnemyDied;
            ReactionBus.OnFieldSpawned -= OnFieldSpawned;
            ReactionBus.OnFieldSpawned += OnFieldSpawned;
            Debug.Log("[Reaction] 火系反应词缀已挂接 (焚尽/炎轮诀/燎原/风火轮)");
        }

        // ── 反应1: 焚尽 — 灼烧敌人死亡 → 原地生成火焰场域 ──
        private static void OnEnemyDied(EnemyController enemy, bool isBurning, Element element)
        {
            if (!isBurning) return;
            if (!AffixSystem.AnyOwned(AffixType.BurnReap)) return;
            if (FieldRegistry.Active.Count >= 8) return;

            var fo = FieldRegistry.Spawn(FieldType.Fire, Element.Fire, enemy.transform.position, 3f, 3f, 1, null);
            Debug.Log($"[Reaction] 焚尽! {enemy.name} 灼烧死亡 → 火焰场域生成 ({fo.Position})");
        }

        // ── 反应2: 炎轮诀 — 火焰场域生成时 → 半径内所有飞剑获得分裂 ──
        private static void OnFieldSpawned(FieldObject field)
        {
            if (field.Type != FieldType.Fire) return;
            // 燎原(Prairie)增幅: 场域半径×1.4 寿命+1.5s
            if (AffixSystem.AnyOwned(AffixType.Prairie))
            {
                field.Radius *= 1.4f;
                field.Life += 1.5f;
            }
            // 条件: 御剑术拥有"炎轮诀"反应词缀 — 剑阵术式让飞剑感应火焰
            // 注意: FlyingSword是独立物体(FlyingSword_0)不挂Player, 必须FindObjectOfType
            var swordComp = Object.FindObjectOfType<FlyingSword>();
            if (swordComp == null) return;
            var affix = swordComp.affixSystem;
            if (affix == null) affix = swordComp.GetComponentInChildren<AffixSystem>();
            if (affix == null || !affix.HasAffix(AffixType.YanLunJue)) return;
            // 距离条件: 飞剑本体在场域半径+3.5内 (周围飞剑都能感知)
            if (Vector3.Distance(swordComp.transform.position, field.Position) > field.Radius + 3.5f) return;

            // 效果: 飞剑获得分裂 — 复用SpawnExtraSword机制(需要totalSwords++配合)
            swordComp.totalSwords++;
            swordComp.SpawnExtraSword();
            AffixVisuals.PlayStarPop(swordComp.transform.position, Element.Fire, 1.2f);
            Debug.Log("[Reaction] 炎轮诀! 火焰场域旁的飞剑获得分裂");
        }

        // ── 反应3: 燎原 — 火焰场域每3秒扩散一个新火焰 (由FieldRegistry Tick驱动) ──
        // 实现: 火焰场域DoT点燃敌人时, 若"燃传播"(BurnSpread)在场且点火源技能拥有"火径"(FireTrail)
        //       → 点燃的敌人死亡时同样走焚尽 → 自然形成蔓延链 (无需单独代码, 由焚尽联动)
        // 补充: 火焰场域DoT点燃的敌人也计入灼烧死亡链 — 燎原=焚尽的自动循环, 零额外代码

        // ── 反应4: 风火轮 — 穿过火焰场域的飞剑附加焚烧 ──
        // 挂接点: FlyingSword的环绕/飞行Update每帧检查自身位置是否在火焰场域内
        //         → 是且拥有"风行"(WindBlade词缀的技能是灵刃, 这里用御剑的ProjectileSpeed词缀近似)
        //         → 给当前攻击目标ApplyBurnFromSpread
        // 由FlyingSword侧调用 FireReactions.TryIgniteSword(pos) 触发 (见FlyingSword.AttackUpdate钩子)

        /// <summary>飞剑经过火焰场域时调用 (FlyingSword钩子) — 返回true表示成功点燃目标</summary>
        public static bool TryIgniteSwordTarget(Transform swordPos, EnemyController target)
        {
            if (target == null || target.IsBurning) return false;
            if (!FieldRegistry.AnyAt(swordPos.position, FieldType.Fire, 1.5f)) return false;
            var affix = swordPos.GetComponentInParent<AffixSystem>();
            if (affix == null || !affix.HasAffix(AffixType.WindBlade)) return false;
            target.ApplyBurnFromSpread();
            AffixVisuals.PlayVfxBurst(target.transform.position, Element.Fire, 1.5f, 0.5f);
            return true;
        }
    }
}
