using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;

namespace BiopunkRV
{
    /// <summary>
    /// 功法编辑系统 — 局外自定义(术法+灵根+5词缀+道)。
    /// 5个官方预设(只读) + 1个玩家自定义(JSON持久化)。
    /// MethodRuntime.Apply 把功法应用到Player。
    /// UI: MethodEditorPanel (秘境F2开关, 挂HUD_Canvas)。
    /// </summary>
    public static class MethodEditor
    {
        public static List<CultivationMethod> Presets = new List<CultivationMethod>();
        public static CultivationMethod PlayerCustom;      // 运行时自定义实例
        public static CultivationMethod Selected;          // 当前编辑中的功法

        private static string CustomPath => Application.persistentDataPath + "/method_custom.json";

        [System.Serializable]
        private class CustomData
        {
            public string methodId, displayName, description, baseSkillId, daoId;
            public int spiritRoot, spiritRootLevel;
            public int[] affixes = new int[5];
            public float damageBonus, attackSpeedBonus;
        }

        /// <summary>加载全部预设+玩家自定义 (幂等)</summary>
        public static void LoadAll()
        {
            Presets.Clear();
            foreach (var m in Resources.LoadAll<CultivationMethod>("CultivationMethods"))
                if (m.isPreset) Presets.Add(m);
            Presets = Presets.OrderBy(p => p.displayName).ToList();
            LoadCustom();
            if (Selected == null) Selected = Presets.Count > 0 ? Presets[0] : PlayerCustom;
        }

        /// <summary>从JSON加载玩家自定义</summary>
        public static void LoadCustom()
        {
            PlayerCustom = ScriptableObject.CreateInstance<CultivationMethod>();
            PlayerCustom.methodId = "custom_player";
            PlayerCustom.displayName = "自定义功法";
            PlayerCustom.isPreset = false;
            PlayerCustom.description = "自由编辑的功法";
            if (!System.IO.File.Exists(CustomPath)) return;
            try
            {
                var d = JsonUtility.FromJson<CustomData>(System.IO.File.ReadAllText(CustomPath));
                PlayerCustom.methodId = d.methodId;
                PlayerCustom.displayName = d.displayName;
                PlayerCustom.description = d.description;
                PlayerCustom.baseSkillId = d.baseSkillId;
                PlayerCustom.spiritRoot = (Element)d.spiritRoot;
                PlayerCustom.spiritRootLevel = d.spiritRootLevel;
                PlayerCustom.daoId = d.daoId;
                PlayerCustom.damageBonus = d.damageBonus;
                PlayerCustom.attackSpeedBonus = d.attackSpeedBonus;
                for (int i = 0; i < 5; i++)
                    if (d.affixes != null && i < d.affixes.Length && System.Enum.IsDefined(typeof(AffixType), d.affixes[i])) PlayerCustom.affixes[i] = (AffixType)d.affixes[i];
            }
            catch (System.Exception e) { Debug.LogWarning($"[MethodEditor] 自定义功法加载失败: {e.Message}"); }
        }

        /// <summary>保存玩家自定义为JSON</summary>
        public static void SaveCustom()
        {
            if (PlayerCustom == null) return;
            var d = new CustomData
            {
                methodId = PlayerCustom.methodId,
                displayName = PlayerCustom.displayName,
                description = PlayerCustom.description,
                baseSkillId = PlayerCustom.baseSkillId,
                spiritRoot = (int)PlayerCustom.spiritRoot,
                spiritRootLevel = PlayerCustom.spiritRootLevel,
                daoId = PlayerCustom.daoId,
                damageBonus = PlayerCustom.damageBonus,
                attackSpeedBonus = PlayerCustom.attackSpeedBonus,
                affixes = PlayerCustom.affixes.Select(a => (int)a).ToArray(),
            };
            System.IO.File.WriteAllText(CustomPath, JsonUtility.ToJson(d, true));
            Debug.Log("[MethodEditor] 自定义功法已保存");
        }

        /// <summary>复制当前选中为自定义并切换选中</summary>
        public static void CloneSelectedToCustom()
        {
            if (Selected == null) return;
            var old = PlayerCustom;
            PlayerCustom = Selected.CloneAsCustom("custom_player");
            PlayerCustom.methodId = "custom_player";
            PlayerCustom.displayName = Selected.displayName + "·改";
            if (old != null) Object.Destroy(old);
            Selected = PlayerCustom;
            SaveCustom();
        }

        // ── 每功法独立副本 (书架模式: 每本功法书可直接编辑, 各自存档) ──

        private static readonly Dictionary<string, CultivationMethod> _copies = new Dictionary<string, CultivationMethod>();
        private static string CopyPath(string methodId) => Application.persistentDataPath + "/method_custom_" + methodId + ".json";

        /// <summary>获取功法的可编辑副本: 有存档副本→副本; 否则从预设克隆(保留原名)</summary>
        public static CultivationMethod GetWorkingCopy(CultivationMethod preset)
        {
            if (preset == null) return null;
            if (_copies.TryGetValue(preset.methodId, out var c) && c != null) return c;
            var loaded = LoadCustomCopy(preset.methodId);
            if (loaded != null)
            {
                if (loaded.icon == null) loaded.icon = preset.icon; // JSON不含图标, 从预设补
                _copies[preset.methodId] = loaded;
                return loaded;
            }
            var fresh = preset.CloneAsCustom(preset.methodId);
            fresh.displayName = preset.displayName;
            fresh.isPreset = false;
            _copies[preset.methodId] = fresh;
            return fresh;
        }

        /// <summary>从JSON加载某功法的副本, 无存档返回null</summary>
        public static CultivationMethod LoadCustomCopy(string methodId)
        {
            string path = CopyPath(methodId);
            if (!System.IO.File.Exists(path)) return null;
            try
            {
                var d = JsonUtility.FromJson<CustomData>(System.IO.File.ReadAllText(path));
                var m = ScriptableObject.CreateInstance<CultivationMethod>();
                m.methodId = d.methodId;
                m.displayName = d.displayName;
                m.description = d.description;
                m.isPreset = false;
                m.baseSkillId = d.baseSkillId;
                m.spiritRoot = (Element)d.spiritRoot;
                m.spiritRootLevel = d.spiritRootLevel;
                m.daoId = d.daoId;
                m.damageBonus = d.damageBonus;
                m.attackSpeedBonus = d.attackSpeedBonus;
                for (int i = 0; i < 5; i++)
                    if (d.affixes != null && i < d.affixes.Length && System.Enum.IsDefined(typeof(AffixType), d.affixes[i])) m.affixes[i] = (AffixType)d.affixes[i];
                return m;
            }
            catch (System.Exception e) { Debug.LogWarning($"[MethodEditor] 副本加载失败 {methodId}: {e.Message}"); return null; }
        }

        /// <summary>保存功法副本为JSON (按methodId独立存档)</summary>
        public static void SaveCustomCopy(CultivationMethod m)
        {
            if (m == null) return;
            var d = new CustomData
            {
                methodId = m.methodId,
                displayName = m.displayName,
                description = m.description,
                baseSkillId = m.baseSkillId,
                spiritRoot = (int)m.spiritRoot,
                spiritRootLevel = m.spiritRootLevel,
                daoId = m.daoId,
                damageBonus = m.damageBonus,
                attackSpeedBonus = m.attackSpeedBonus,
                affixes = m.affixes.Select(a => (int)a).ToArray(),
            };
            System.IO.File.WriteAllText(CopyPath(m.methodId), JsonUtility.ToJson(d, true));
            Debug.Log($"[MethodEditor] 功法副本已保存: {m.methodId}");
        }

        /// <summary>解析启用功法ID为实际配置 (优先副本, 回退预设, custom_player为旧版兼容)</summary>
        public static CultivationMethod ResolveById(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (id == "custom_player") return PlayerCustom;
            var preset = Presets.Find(p => p.methodId == id);
            return preset != null ? GetWorkingCopy(preset) : null;
        }
    }

    /// <summary>
    /// 功法局内应用 — 把功法配置落到Player技能/灵根/词缀/属性上。
    /// </summary>
    public static class MethodRuntime
    {
        /// <summary>技能ID → 组件类型</summary>
        public static System.Type GetSkillType(string skillId)
        {
            switch (skillId)
            {
                case "SpiritOrb": return typeof(SpiritOrbSpell);
                case "Fireball": return typeof(FireballSpell);
                case "QiBlade": return typeof(QiBladeSpell);
                case "Talisman": return typeof(TalismanSpell);
                case "SpiritArray": return typeof(SpiritArraySkill);
                case "FlyingSword": return typeof(FlyingSword);
                default: return null;
            }
        }

        /// <summary>应用功法 (可重复调用, 灵根绝对重置)</summary>
        public static void Apply(CultivationMethod m, GameObject player, bool append = false)
        {
            if (m == null || player == null) return;

            // 1) 灵根: 全部移除后重建到目标等级 (确保累积状态正确)
            string[] rootTypes = { "FireSpiritRoot", "WaterSpiritRoot", "EarthSpiritRoot", "WindSpiritRoot", "ThunderSpiritRoot" };
            foreach (var rt in rootTypes)
            {
                if (append) break;
                var type = System.Type.GetType("BiopunkRV." + rt);
                if (type == null) continue;
                var existing = player.GetComponent(type);
                if (existing != null)
                {
                    var level = type.GetField("level");
                    if (level != null) level.SetValue(existing, 0);
                    Object.Destroy(existing);
                }
            }
            if (m.spiritRoot != Element.None && m.spiritRoot != Element.Metal && m.spiritRoot < Element.FlameWind)
            {
                string rootName = RootNameFor(m.spiritRoot);
                var type = System.Type.GetType("BiopunkRV." + rootName);
                if (type != null)
                {
                    var root = append ? player.GetComponent(type) : null;
                    if (root == null) root = player.AddComponent(type);
                    var lvlUp = type.GetMethod("LevelUp");
                    int target = Mathf.Clamp(m.spiritRootLevel, 1, 10);
                    int current = (int)type.GetField("level").GetValue(root);
                    for (int i = current; i < target; i++) lvlUp.Invoke(root, null);
                    Debug.Log($"[MethodRuntime] 灵根: {rootName} Lv{target}");
                }
            }

            // 2) 词缀: 先清除所有技能的旧功法设计 (新功法只覆盖目标技能, 其余恢复默认随机词缀)
            string[] allSkillIds = { "SpiritOrb", "Fireball", "QiBlade", "Talisman", "SpiritArray", "FlyingSword" };
            foreach (var sid in allSkillIds)
            {
                if (append) break;
                var st = GetSkillType(sid);
                if (st == null) continue;
                Component sc = st == typeof(FlyingSword) ? (Component)Object.FindObjectOfType(st) : player.GetComponent(st);
                if (sc == null) continue;
                var afChild = sc.gameObject.transform.Find("Affix_" + st.Name);
                var oldAffix = afChild != null ? afChild.GetComponent<AffixSystem>() : null;
                if (oldAffix != null && oldAffix.plannedMode)
                {
                    oldAffix.plannedMode = false;
                    oldAffix.ResetPlannedSlots();
                    var olf = st.GetField("skillLevel");
                    oldAffix.UpdateAffixes(olf != null ? (int)olf.GetValue(sc) : 1);
                }
            }

            // 词缀: 写入目标技能的专属AffixSystem (组件缺失时自动补挂)
            // plannedMode=true — 词缀不随等级自动激活, 由局内升级选项按设计顺序逐个解锁
            var skillType = GetSkillType(m.baseSkillId);
            if (skillType != null)
            {
                Component skillComp = player.GetComponent(skillType);
                if (skillType == typeof(FlyingSword) && skillComp == null)
                    skillComp = Object.FindObjectOfType<FlyingSword>();
                if (skillComp == null)
                {
                    // 技能未挂(如灵阵被MISSING SCRIPT挤掉) — 自动补挂, Awake会从SkillLibrary载默认值
                    var added = player.AddComponent(skillType);
                    skillComp = added;
                    Debug.Log($"[MethodRuntime] 技能组件缺失, 已自动补挂: {skillType.Name}");
                    // 灵阵需要arraySprite等场景引用, 默认贴图从Resources兜底
                    var spriteField = skillType.GetField("arraySprite");
                    if (spriteField != null && spriteField.GetValue(skillComp) == null)
                    {
                        var tex = Resources.Load<Texture2D>("SpawnCircle");
                        if (tex != null)
                            spriteField.SetValue(skillComp, Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f));
                    }
                }

                var affix = AffixSystem.GetOrCreateFor(skillType, skillComp.gameObject);
                for (int i = 0; i < 5; i++)
                    if (m.affixes != null && i < m.affixes.Length) affix.SetPresetAffix(i, m.affixes[i]);
                var lvlField = skillType.GetField("skillLevel");
                int lvl = lvlField != null ? (int)lvlField.GetValue(skillComp) : 1;
                // 进入功法计划模式: 设计的5个词缀按槽位顺序出现在局内升级选项中, 选取后解锁
                affix.plannedMode = true;
                affix.ResetPlannedSlots();
                affix.UpdateAffixes(lvl);
                Debug.Log($"[MethodRuntime] 词缀已写入 {skillType.Name} (Lv{lvl}), 计划模式=待局内升级解锁");
            }

            // 3) 功法加成
            var psc = player.GetComponent<PlayerStatsComponent>();
            if (psc != null && !append)
            {
                var bonusState = player.GetComponent<MethodBonusState>() ?? player.AddComponent<MethodBonusState>();
                bonusState.Apply(psc.stats, m.damageBonus, m.attackSpeedBonus);
            }

            Debug.Log($"[MethodRuntime] 功法「{m.displayName}」已应用");
        }

        public static void ApplySavedLoadout(GameObject player)
        {
            if (player == null) return;
            if (MethodEditor.Presets.Count == 0) MethodEditor.LoadAll();
            var d = MetaProgression.Data;
            bool first = true;
            float damage = 0f, speed = 0f;
            foreach (string id in new[] { d.mainMethod, d.supportMethod, d.forbiddenMethod })
            {
                if (!MetaProgression.IsMethodUnlocked(id)) continue;
                var method = MethodEditor.ResolveById(id);
                if (method == null) continue;
                Apply(method, player, !first);
                first = false;
                damage += Mathf.Max(0, method.damageBonus);
                speed += Mathf.Max(0, method.attackSpeedBonus);
            }
            if (first)
            {
                var starter = ScriptableObject.CreateInstance<CultivationMethod>();
                starter.spiritRoot = (Element)d.selectedRoot;
                Apply(starter, player);
                Object.Destroy(starter);
            }
            else
            {
                var psc = player.GetComponent<PlayerStatsComponent>();
                if (psc != null) player.GetComponent<MethodBonusState>().Apply(psc.stats, damage, speed);
                if (d.selectedRoot != 0)
                {
                    var root = ScriptableObject.CreateInstance<CultivationMethod>();
                    root.spiritRoot = (Element)d.selectedRoot;
                    Apply(root, player, true);
                    Object.Destroy(root);
                }
            }
        }

        private static string RootNameFor(Element e)
        {
            switch (e)
            {
                case Element.Fire: return "FireSpiritRoot";
                case Element.Water: return "WaterSpiritRoot";
                case Element.Earth: return "EarthSpiritRoot";
                case Element.Wind: return "WindSpiritRoot";
                case Element.Thunder: return "ThunderSpiritRoot";
                default: return "";
            }
        }
    }
}
