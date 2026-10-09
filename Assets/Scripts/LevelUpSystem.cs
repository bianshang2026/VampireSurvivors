using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace BiopunkRV
{
    /// <summary>
    /// Level up upgrade system. Shows 3 random upgrade options when player levels up.
    /// Each option is displayed on a scroll panel with text description.
    /// Player clicks one to apply the upgrade.
    /// </summary>
    public class LevelUpSystem : MonoBehaviour
    {
        [Header("UI References")]
        public GameObject panelRoot;        // Parent panel (hidden by default)
        public GameObject optionPrefab;      // Prefab for each option (scroll + text)
        
        [Header("Layout")]
        public float optionSpacing = 350f;
        public Vector3 panelOffset = new Vector3(0, 0, 0);

        [Header("Upgrades Database")]
        public List<UpgradeData> allUpgrades = new List<UpgradeData>();

        private bool _isShowing = false;
        private int _selectedOption = -1;

        [System.Serializable]
        public class UpgradeData
        {
            public string name;
            public string description;
            public Sprite icon;
            public UpgradeType type;
            public float value;
            public UpgradeRarity rarity = UpgradeRarity.Common;

            // 词缀选项附加数据 (type=SkillAffix时使用)
            public AffixType affixType;
            public string targetSkillName;
            [System.NonSerialized] public System.Type targetSkillType;
            // 功法设计词缀: >=0 表示该选项来自局外功法设计的对应槽位
            public int designedSlot = -1;

            public enum UpgradeType
            {
                MaxHP,
                MoveSpeed,
                AttackDamage,
                AttackSpeed,    // 攻击速度
                PickupRange,
                FireballDamage,
                FireballCooldown,
                SwordDamage,
                SwordCount,
                ExpGain,
                SpiritOrbLevel,   // 灵力弹升级
                SpiritOrbDamage,  // 灵力弹伤害
                SpiritOrbCount,   // 灵力弹数量
                FireSpiritRoot,   // 火灵根升级
                WaterSpiritRoot,  // 水灵根升级
                EarthSpiritRoot,  // 土灵根升级
                WindSpiritRoot,   // 风灵根升级
                ThunderSpiritRoot,// 雷灵根升级
                SkillAffix,       // 词缀镶嵌
            }
        }

        public static LevelUpSystem Instance { get; private set; }

        private Sprite _scrollSprite;

        private void Awake()
        {
            Instance = this;
            
            // Load scroll sprite for option backgrounds
            _scrollSprite = Resources.Load<Sprite>("LevelUpScroll");
#if UNITY_EDITOR
            if (_scrollSprite == null)
                _scrollSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/TJGenerators/History/Sprite_20260808_022833.png");
#endif
            
            // Initialize default upgrades
            if (allUpgrades.Count == 0)
            {
                allUpgrades = new List<UpgradeData>
                {
                    new UpgradeData { name = "凝气突破", description = "灵力弹等级 +1", type = UpgradeData.UpgradeType.SpiritOrbLevel, value = 1f },
                    new UpgradeData { name = "灵力凝聚", description = "灵力弹伤害 +5", type = UpgradeData.UpgradeType.SpiritOrbDamage, value = 5f },
                    new UpgradeData { name = "灵球分身", description = "灵力弹数量 +1", type = UpgradeData.UpgradeType.SpiritOrbCount, value = 1f },
                    new UpgradeData { name = "火种初生", description = "获得火灵根，技能20%概率附加灼烧", type = UpgradeData.UpgradeType.FireSpiritRoot, value = 1f },
                    new UpgradeData { name = "寒泉", description = "获得水灵根，技能20%概率减速敌人", type = UpgradeData.UpgradeType.WaterSpiritRoot, value = 1f },
                    new UpgradeData { name = "厚土", description = "获得土灵根，技能10%击退+5%晕眩", type = UpgradeData.UpgradeType.EarthSpiritRoot, value = 1f },
                    new UpgradeData { name = "疾风", description = "获得风灵根，灵气刃弹射+2速度+30%", type = UpgradeData.UpgradeType.WindSpiritRoot, value = 1f },
                    new UpgradeData { name = "惊雷", description = "获得雷灵根，灵球变雷球+连锁1个", type = UpgradeData.UpgradeType.ThunderSpiritRoot, value = 1f },
                    new UpgradeData { name = "炼体", description = "生命上限 +20", type = UpgradeData.UpgradeType.MaxHP, value = 20f },
                    new UpgradeData { name = "身法", description = "移动速度 +10%", type = UpgradeData.UpgradeType.MoveSpeed, value = 0.10f },
                };
            }
        }

        private void Start()
        {
            if (panelRoot != null)
                panelRoot.SetActive(false);
        }

        /// <summary>
        /// Show 3 random upgrade options. Called when player levels up.
        /// </summary>
        public void ShowUpgrades()
        {
            if (_isShowing) return;
            _isShowing = true;
            _selectedOption = -1;

            // Play level-up golden glow effect on player
            SpawnLevelUpEffect();

            // Pause game
            Time.timeScale = 0f;

            // Create panel if not exists
            if (panelRoot == null)
            {
                CreatePanel();
            }

            panelRoot.SetActive(true);

            // 按企划权重构建3个选项: 技能升级40% / 词缀35% / 灵根15% / 其他10%
            List<UpgradeData> choices = BuildWeightedChoices();

            // Get player luck for rarity rolling
            float luck = 0f;
            var player = GameObject.Find("Player");
            if (player != null) {
                var psc = player.GetComponent<PlayerStatsComponent>();
                if (psc != null) luck = psc.stats.luck;
            }
            foreach (var c in choices)
                c.rarity = RaritySystem.RollRarity(luck);

            // Find existing Option objects in panel (user pre-arranged)
            List<GameObject> existingOptions = new List<GameObject>();
            for (int i = 0; i < panelRoot.transform.childCount; i++) {
                var child = panelRoot.transform.GetChild(i);
                if (child.name.StartsWith("Option_"))
                    existingOptions.Add(child.gameObject);
            }

            if (existingOptions.Count >= 3) {
                // Reuse user's pre-arranged options — just update content
                for (int i = 0; i < choices.Count && i < existingOptions.Count; i++) {
                    UpdateOptionUI(existingOptions[i], choices[i], i);
                }
            } else {
                // Fallback: clear and recreate
                for (int i = panelRoot.transform.childCount - 1; i >= 0; i--) {
                    Transform child = panelRoot.transform.GetChild(i);
                    if (child.name != "Title")
                        Destroy(child.gameObject);
                }
                for (int i = 0; i < choices.Count; i++) {
                    CreateOptionUI(choices[i], i, choices.Count);
                }
            }
        }

        /// <summary>
        /// 按企划权重构建3个升级选项: 技能升级40% / 词缀35% / 灵根15% / 其他10%
        /// </summary>
        private List<UpgradeData> BuildWeightedChoices()
        {
            var skillPool = new List<UpgradeData>();
            var rootPool = new List<UpgradeData>();
            var otherPool = new List<UpgradeData>();

            foreach (var u in allUpgrades)
            {
                if (u.type == UpgradeData.UpgradeType.FireSpiritRoot || u.type == UpgradeData.UpgradeType.WaterSpiritRoot ||
                    u.type == UpgradeData.UpgradeType.EarthSpiritRoot || u.type == UpgradeData.UpgradeType.WindSpiritRoot ||
                    u.type == UpgradeData.UpgradeType.ThunderSpiritRoot)
                    rootPool.Add(u);
                else if (u.type == UpgradeData.UpgradeType.SpiritOrbLevel || u.type == UpgradeData.UpgradeType.SpiritOrbDamage ||
                         u.type == UpgradeData.UpgradeType.SpiritOrbCount)
                    skillPool.Add(u);
                else
                    otherPool.Add(u);
            }

            var affixPool = GenerateAffixOptions(out var designedAffixes);

            var choices = new List<UpgradeData>();
            // 功法设计的词缀必出: 你词缀怎么设计, 局内升级选项就怎么给
            foreach (var d in designedAffixes)
            {
                if (choices.Count >= 3) break;
                choices.Add(d);
            }
            while (choices.Count < 3)
            {
                float roll = Random.value;
                List<UpgradeData> pool;
                if (roll < 0.40f) pool = skillPool;
                else if (roll < 0.75f) pool = affixPool;
                else if (roll < 0.90f) pool = rootPool;
                else pool = otherPool;

                // 目标池为空时降级: 灵根→技能→词缀→其他
                if (pool.Count == 0)
                {
                    if (ReferenceEquals(pool, affixPool)) pool = skillPool.Count > 0 ? skillPool : (rootPool.Count > 0 ? rootPool : otherPool);
                    else if (ReferenceEquals(pool, rootPool)) pool = skillPool.Count > 0 ? skillPool : (affixPool.Count > 0 ? affixPool : otherPool);
                    else if (ReferenceEquals(pool, otherPool)) pool = skillPool.Count > 0 ? skillPool : (affixPool.Count > 0 ? affixPool : rootPool);
                    else pool = affixPool.Count > 0 ? affixPool : rootPool;
                    if (pool.Count == 0) break;
                }

                int idx = Random.Range(0, pool.Count);
                choices.Add(pool[idx]);
                pool.RemoveAt(idx);
            }

            // 兜底: 全空时用任意升级
            if (choices.Count == 0 && allUpgrades.Count > 0)
                choices.Add(allUpgrades[Random.Range(0, allUpgrades.Count)]);

            return choices;
        }

        /// <summary>
        /// 动态生成词缀选项。功法设计的技能按设计顺序给出词缀(必出, 见designed),
        /// 其余技能(无功法)从词缀池随机抽取(最多3条)。
        /// </summary>
        private List<UpgradeData> GenerateAffixOptions(out List<UpgradeData> designed)
        {
            designed = new List<UpgradeData>();
            var result = new List<UpgradeData>();
            var player = GameObject.Find("Player");
            if (player == null) return result;

            // 收集带skillLevel字段的技能, 按等级降序
            var candidates = new List<(System.Type type, int level, string name)>();
            void TryAdd(System.Type t, string displayName)
            {
                // 御剑术在独立GameObject上, 用FindObjectOfType查找
                var comp = t == typeof(FlyingSword) ? (Component)FindObjectOfType(t) : player.GetComponent(t);
                if (comp == null) return;
                var lvlField = t.GetField("skillLevel");
                if (lvlField == null) return;
                candidates.Add((t, (int)lvlField.GetValue(comp), displayName));
            }
            TryAdd(typeof(SpiritOrbSpell), "灵球术");
            TryAdd(typeof(FireballSpell), "火球术");
            TryAdd(typeof(QiBladeSpell), "灵气刃");
            TryAdd(typeof(TalismanSpell), "符箓术");
            TryAdd(typeof(SpiritArraySpell), "灵力阵");
            TryAdd(typeof(FlyingSword), "御剑术");

            if (candidates.Count == 0) return result;
            candidates.Sort((a, b) => b.level.CompareTo(a.level));

            // 功法设计的技能单独处理: 按设计顺序给出下一槽位词缀, 不给随机词缀
            var legacy = new List<(System.Type type, int level, string name)>();
            foreach (var c in candidates)
            {
                var comp = c.type == typeof(FlyingSword) ? (Component)FindObjectOfType(c.type) : player.GetComponent(c.type);
                AffixSystem af = null;
                if (comp != null)
                {
                    var child = comp.gameObject.transform.Find("Affix_" + c.type.Name);
                    if (child != null) af = child.GetComponent<AffixSystem>();
                }
                if (af == null || !af.plannedMode)
                {
                    legacy.Add(c);
                    continue;
                }
                int slot = af.NextPlannedSlot();
                if (slot < 0 || c.level < AffixSystem.UnlockLevels[slot]) continue; // 全部解锁 / 等级未到槽位解锁线
                var affix = af.presetAffixes[slot];
                designed.Add(new UpgradeData
                {
                    name = "词缀·" + AffixSystem.GetAffixName(affix),
                    description = $"功法设计·{c.name}槽位{slot + 1}：{AffixSystem.GetAffixDesc(affix)}",
                    type = UpgradeData.UpgradeType.SkillAffix,
                    value = 1f,
                    affixType = affix,
                    targetSkillType = c.type,
                    targetSkillName = c.name,
                    designedSlot = slot,
                });
            }

            // 可选词缀: 20通用 + 10术法专属 + 灵根词缀(逐技能过滤, 见下方循环) + 反应词缀(火系)
            var affixPool = new List<AffixType>();
            for (AffixType t = AffixType.Split; t <= AffixType.TalismanScatter; t++)
            {
                // 追踪词缀已移除 (用户要求)
                if (t == AffixType.Track) continue;
                affixPool.Add(t);
            }
            // 反应词缀入池 (构筑链路: 前件在场时后件加权, 见下方加权逻辑)
            affixPool.Add(AffixType.BurnReap);
            affixPool.Add(AffixType.YanLunJue);
            affixPool.Add(AffixType.Prairie);
            affixPool.Add(AffixType.WindFire);

            int count = Mathf.Min(3, legacy.Count);
            for (int i = 0; i < count; i++)
            {
                var target = legacy[i];
                // 按目标技能过滤词缀池 (灵根词缀只给匹配属性的技能)
                var pool = new List<AffixType>();
                foreach (var t in affixPool)
                {
                    if (t >= AffixType.BurnEnhance && t <= AffixType.Paralyze)
                    {
                        Element skillEl = GetSkillEffectiveElement(target.type);
                        if (!IsRootAffixMatch(t, skillEl)) continue;
                    }
                    pool.Add(t);
                }
                if (pool.Count == 0) continue;

                // ── 反应链路构筑引导: 前件词缀在场 → 后件反应词缀出现率×3 ──
                // 焚烧/裂火在场 → 焚尽/燎原/风火轮加权; 焚尽在场 → 炎轮诀加权
                var weighted = new List<AffixType>(pool);
                bool hasBurn = AffixSystem.AnyOwned(AffixType.BurnEnhance);
                bool hasSpread = AffixSystem.AnyOwned(AffixType.BurnSpread);
                bool hasReap = AffixSystem.AnyOwned(AffixType.BurnReap);
                if (hasBurn || hasSpread)
                {
                    if (pool.Contains(AffixType.BurnReap)) { for (int w = 0; w < 2; w++) weighted.Add(AffixType.BurnReap); }
                    if (pool.Contains(AffixType.Prairie)) weighted.Add(AffixType.Prairie);
                    if (pool.Contains(AffixType.WindFire)) weighted.Add(AffixType.WindFire);
                }
                if (hasReap && pool.Contains(AffixType.YanLunJue))
                    weighted.Add(AffixType.YanLunJue);

                var affix = weighted[Random.Range(0, weighted.Count)];
                result.Add(new UpgradeData
                {
                    name = "词缀·" + AffixSystem.GetAffixName(affix),
                    description = $"为「{target.name}」镶嵌词缀，下次槽位解锁时生效",
                    type = UpgradeData.UpgradeType.SkillAffix,
                    value = 1f,
                    affixType = affix,
                    targetSkillType = target.type,
                    targetSkillName = target.name,
                });
            }
            return result;
        }

        /// <summary>获取技能当前有效属性 (通过组件的element字段 + 灵根管理器)</summary>
        private Element GetSkillEffectiveElement(System.Type skillType)
        {
            var player = GameObject.Find("Player");
            if (player == null) return Element.None;
            var comp = player.GetComponent(skillType);
            if (comp == null) return Element.None;
            var elField = skillType.GetField("element");
            if (elField == null) return Element.None;
            Element baseEl = (Element)elField.GetValue(comp);
            return SpiritRootManager.GetEffectiveElement(baseEl);
        }

        /// <summary>灵根词缀是否与技能属性匹配 (火系词缀只给火技能, 以此类推)</summary>
        private bool IsRootAffixMatch(AffixType affix, Element skillEl)
        {
            switch (affix)
            {
                case AffixType.BurnEnhance:
                case AffixType.BurnExplode:
                case AffixType.BurnSpread:
                    return skillEl == Element.Fire;
                case AffixType.SlowEnhance:
                case AffixType.Ripple:
                case AffixType.Freeze:
                    return skillEl == Element.Water;
                case AffixType.Quake:
                case AffixType.ShieldBody:
                case AffixType.KnockbackEnhance:
                    return skillEl == Element.Earth;
                case AffixType.Haste:
                case AffixType.ProjectileSpeed:
                case AffixType.WindBlade:
                    return skillEl == Element.Wind;
                case AffixType.Chain:
                case AffixType.ThunderCrit:
                case AffixType.Paralyze:
                    return skillEl == Element.Thunder;
            }
            return false;
        }

        private void CreatePanel()
        {
            // Find HUD Canvas
            Canvas hud = FindObjectOfType<Canvas>();
            if (hud == null) return;

            // Create dark background overlay
            panelRoot = new GameObject("LevelUpPanel");
            panelRoot.transform.SetParent(hud.transform, false);
            
            RectTransform bgRt = panelRoot.AddComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;
            
            Image bg = panelRoot.AddComponent<Image>();
            bg.color = new Color(0, 0, 0, 0.7f);

            // Create title with sprite
            GameObject title = new GameObject("Title");
            title.transform.SetParent(panelRoot.transform, false);
            RectTransform titleRt = title.AddComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0.5f, 1f);
            titleRt.anchorMax = new Vector2(0.5f, 1f);
            titleRt.anchoredPosition = new Vector2(0, -80);
            titleRt.sizeDelta = new Vector2(500, 200);
            
            Image titleImg = title.AddComponent<Image>();
            titleImg.color = Color.white;
            titleImg.preserveAspect = true;
            Sprite titleSprite = null;
#if UNITY_EDITOR
            titleSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/TJGenerators/History/Sprite_20260808_025250.png");
#endif
            if (titleSprite == null) titleSprite = Resources.Load<Sprite>("LevelUpTitle");
            titleImg.sprite = titleSprite;
        }

        private void UpdateOptionUI(GameObject optionObj, UpgradeData upgrade, int index)
        {
            float mult = RaritySystem.GetMultiplier(upgrade.rarity);
            string rarityName = RaritySystem.GetName(upgrade.rarity);
            Color rarityColor = RaritySystem.GetColor(upgrade.rarity);

            // Update outline color
            var outline = optionObj.GetComponent<Outline>();
            if (outline != null) {
                outline.effectColor = rarityColor;
                outline.effectDistance = new Vector2(6, 6);
            }

            // Find or create Rarity label
            var rarityTf = optionObj.transform.Find("Rarity");
            TMPro.TextMeshProUGUI rarityText = null;
            if (rarityTf != null) rarityText = rarityTf.GetComponent<TMPro.TextMeshProUGUI>();
            if (rarityText != null) {
                rarityText.text = $"【{rarityName}】";
                rarityText.color = rarityColor;
            }

            // Update Name
            var nameTf = optionObj.transform.Find("Name");
            if (nameTf != null) {
                var nameText = nameTf.GetComponent<TMPro.TextMeshProUGUI>();
                if (nameText != null) nameText.text = upgrade.name;
            }

            // Update Desc
            var descTf = optionObj.transform.Find("Desc");
            if (descTf != null) {
                var descText = descTf.GetComponent<TMPro.TextMeshProUGUI>();
                if (descText != null)
                    descText.text = upgrade.description + (mult > 1f ? $" ×{mult:0.#}" : "");
            }

            // Update or add UpgradeButton
            var ub = optionObj.GetComponent<UpgradeButton>();
            if (ub == null) {
                ub = optionObj.AddComponent<UpgradeButton>();
                ub.system = this;
                ub.RegisterClick();
            }
            ub.upgrade = upgrade;
        }

        private void CreateOptionUI(UpgradeData upgrade, int index, int total)
        {
            // Apply rarity multiplier to value
            float mult = RaritySystem.GetMultiplier(upgrade.rarity);
            float actualValue = upgrade.value * mult;
            string rarityName = RaritySystem.GetName(upgrade.rarity);
            Color rarityColor = RaritySystem.GetColor(upgrade.rarity);

            // Calculate position
            float spacing = 380f;
            float startX = -((total - 1) * spacing * 0.5f);
            float x = startX + index * spacing;

            // Create option container
            GameObject option = new GameObject("Option_" + index);
            option.transform.SetParent(panelRoot.transform, false);
            
            RectTransform rt = option.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x, 0);
            rt.sizeDelta = new Vector2(300, 400);

            // Background (scroll sprite) — tinted by rarity
            Image bg = option.AddComponent<Image>();
            bg.color = Color.white;
            bg.sprite = _scrollSprite;
            bg.preserveAspect = true;

            // Rarity glow border (Outline with rarity color)
            Outline rarityOutline = option.AddComponent<Outline>();
            rarityOutline.effectColor = rarityColor;
            rarityOutline.effectDistance = new Vector2(6, 6);

            // Add button
            Button btn = option.AddComponent<Button>();
            ColorBlock cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(0.9f, 0.95f, 1f);
            cb.pressedColor = new Color(0.8f, 0.85f, 0.9f);
            btn.colors = cb;

            // Rarity label (top)
            GameObject rarityObj = new GameObject("Rarity");
            rarityObj.transform.SetParent(option.transform, false);
            RectTransform rarityRt = rarityObj.AddComponent<RectTransform>();
            rarityRt.anchorMin = new Vector2(0.5f, 0.85f);
            rarityRt.anchorMax = new Vector2(0.5f, 0.85f);
            rarityRt.pivot = new Vector2(0.5f, 0.5f);
            rarityRt.sizeDelta = new Vector2(280, 35);
            rarityRt.anchoredPosition = Vector2.zero;
            TMPro.TextMeshProUGUI rarityText = rarityObj.AddComponent<TMPro.TextMeshProUGUI>();
            rarityText.fontSize = 24;
            rarityText.alignment = TMPro.TextAlignmentOptions.Center;
            rarityText.color = rarityColor;
            rarityText.text = $"【{rarityName}】";

            // Upgrade name text
            GameObject nameObj = new GameObject("Name");
            nameObj.transform.SetParent(option.transform, false);
            RectTransform nameRt = nameObj.AddComponent<RectTransform>();
            nameRt.anchorMin = new Vector2(0.5f, 0.65f);
            nameRt.anchorMax = new Vector2(0.5f, 0.65f);
            nameRt.sizeDelta = new Vector2(280, 50);
            nameRt.anchoredPosition = Vector2.zero;
            
            TMPro.TextMeshProUGUI nameText = nameObj.AddComponent<TMPro.TextMeshProUGUI>();
            nameText.text = upgrade.name;
            nameText.fontSize = 32;
            nameText.alignment = TMPro.TextAlignmentOptions.Center;
            nameText.color = new Color(0.2f, 0.35f, 0.3f);

            // Description text
            GameObject descObj = new GameObject("Desc");
            descObj.transform.SetParent(option.transform, false);
            RectTransform descRt = descObj.AddComponent<RectTransform>();
            descRt.anchorMin = new Vector2(0.5f, 0.35f);
            descRt.anchorMax = new Vector2(0.5f, 0.35f);
            descRt.sizeDelta = new Vector2(280, 80);
            descRt.anchoredPosition = Vector2.zero;
            
            TMPro.TextMeshProUGUI descText = descObj.AddComponent<TMPro.TextMeshProUGUI>();
            descText.text = upgrade.description + (mult > 1f ? $" ×{mult:0.#}" : "");
            descText.fontSize = 24;
            descText.alignment = TMPro.TextAlignmentOptions.Center;
            descText.color = new Color(0.4f, 0.4f, 0.4f);

            // Store upgrade data on button and register click immediately
            UpgradeButton ub = option.AddComponent<UpgradeButton>();
            ub.upgrade = upgrade;
            ub.system = this;
            ub.RegisterClick();

            // Add simple border outline
            Outline outline = option.AddComponent<Outline>();
            outline.effectColor = new Color(0.8f, 0.7f, 0.4f);
            outline.effectDistance = new Vector2(4, 4);
        }

        private void SpawnLevelUpEffect()
        {
            GameObject player = GameObject.Find("Player");
            if (player == null) return;

            // Create golden glow light
            GameObject glowObj = new GameObject("LevelUpGlow");
            glowObj.transform.SetParent(player.transform, false);
            glowObj.transform.localPosition = Vector3.zero;

            Light glowLight = glowObj.AddComponent<Light>();
            glowLight.type = LightType.Point;
            glowLight.color = new Color(1f, 0.85f, 0.3f, 1f); // golden
            glowLight.intensity = 8f;
            glowLight.range = 6f;

            // Create golden particle system
            GameObject particleObj = new GameObject("LevelUpParticles");
            particleObj.transform.SetParent(player.transform, false);
            particleObj.transform.localPosition = Vector3.zero;

            ParticleSystem ps = particleObj.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 2f;
            main.startLifetime = 1.5f;
            main.startSpeed = 3f;
            main.startSize = 0.15f;
            main.startColor = new Color(1f, 0.85f, 0.2f, 1f);
            main.maxParticles = 80;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0.3f;

            var emission = ps.emission;
            emission.rateOverTime = 40f;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.5f;

            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.y = new ParticleSystem.MinMaxCurve(2f, 5f);

            var sizeOverLife = ps.sizeOverLifetime;
            sizeOverLife.enabled = true;
            var sizeCurve = new AnimationCurve();
            sizeCurve.AddKey(0f, 0.3f);
            sizeCurve.AddKey(0.3f, 1f);
            sizeCurve.AddKey(1f, 0f);
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            var colorOverLife = ps.colorOverLifetime;
            colorOverLife.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(new Color(1f, 0.9f, 0.4f), 0f),
                    new GradientColorKey(new Color(1f, 0.7f, 0.1f), 0.5f),
                    new GradientColorKey(new Color(0.8f, 0.5f, 0f), 1f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0.8f, 0.5f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            colorOverLife.color = grad;

            ps.Play();

            // Auto-destroy after 3 seconds
            Destroy(glowObj, 3f);
            Destroy(particleObj, 3f);
        }

        public void SelectUpgrade(UpgradeData upgrade)
        {
            if (!_isShowing) return;

            ApplyUpgrade(upgrade);

            // Resume game
            Time.timeScale = EnlightenmentChoice.IsOpen ? 0f : 1f;
            panelRoot.SetActive(false);
            _isShowing = false;

            Debug.Log("Upgrade selected: " + upgrade.name);
        }

        private void ApplyUpgrade(UpgradeData upgrade)
        {
            GameObject player = GameObject.Find("Player");
            if (player == null) return;

            // Apply rarity multiplier to value
            float mult = RaritySystem.GetMultiplier(upgrade.rarity);
            float value = upgrade.value * mult;

            switch (upgrade.type)
            {
                case UpgradeData.UpgradeType.MaxHP:
                    ExpBar.SetMaxHP(ExpBar.maxHP + (int)value);
                    Debug.Log("Max HP + " + value);
                    break;

                case UpgradeData.UpgradeType.MoveSpeed:
                    var cc2d = player.GetComponent<CharacterController2D>();
                    if (cc2d != null) cc2d.moveSpeed *= (1f + value);
                    Debug.Log("Move speed + " + (value * 100) + "%");
                    break;

                case UpgradeData.UpgradeType.SwordDamage:
                    FlyingSword[] swords = FindObjectsOfType<FlyingSword>();
                    foreach (var s in swords) s.damageMultiplier *= (1f + value * 0.01f);
                    Debug.Log("Sword damage multiplier + " + value + "%");
                    break;

                case UpgradeData.UpgradeType.SwordCount:
                    FlyingSword existing = FindObjectOfType<FlyingSword>();
                    if (existing != null)
                    {
                        // Add a new flying sword via SpawnExtraSword
                        existing.SpawnExtraSword();
                    }
                    Debug.Log("Sword count + 1");
                    break;

                case UpgradeData.UpgradeType.FireballDamage:
                    FireballSpell spell = player.GetComponent<FireballSpell>();
                    if (spell != null) spell.damageMultiplier *= (1f + value * 0.01f);
                    Debug.Log("Fireball damage multiplier + " + value + "%");
                    break;

                case UpgradeData.UpgradeType.FireballCooldown:
                    spell = player.GetComponent<FireballSpell>();
                    if (spell != null) spell.fireInterval *= (1f - value);
                    Debug.Log("Fireball cooldown - " + (value * 100) + "%");
                    break;

                case UpgradeData.UpgradeType.PickupRange:
                    Debug.Log("Pickup range + " + (value * 100) + "%");
                    break;

                case UpgradeData.UpgradeType.ExpGain:
                    Debug.Log("Exp gain + " + (value * 100) + "%");
                    break;

                case UpgradeData.UpgradeType.AttackSpeed:
                    swords = FindObjectsOfType<FlyingSword>();
                    foreach (var s in swords) s.attackCooldown *= (1f - value);
                    Debug.Log("Attack speed + " + (value * 100) + "%");
                    break;

                case UpgradeData.UpgradeType.SpiritOrbLevel:
                    {
                        var orbSpell = player.GetComponent<SpiritOrbSpell>();
                        var fbSpell = player.GetComponent<FireballSpell>();
                        if (orbSpell != null && orbSpell.enabled) orbSpell.LevelUp();
                        else if (fbSpell != null) fbSpell.LevelUp();
                        Debug.Log("Skill level up");
                        break;
                    }

                case UpgradeData.UpgradeType.SpiritOrbDamage:
                    {
                        var orbSpell2 = player.GetComponent<SpiritOrbSpell>();
                        var fbSpell2 = player.GetComponent<FireballSpell>();
                        if (orbSpell2 != null && orbSpell2.enabled) orbSpell2.upgradeDamageMult *= (1f + value * 0.01f);
                        else if (fbSpell2 != null) fbSpell2.damageMult *= (1f + value * 0.01f);
                        Debug.Log("Skill damage multiplier + " + value + "%");
                        break;
                    }

                case UpgradeData.UpgradeType.SpiritOrbCount:
                    {
                        var orbSpell3 = player.GetComponent<SpiritOrbSpell>();
                        var fbSpell3 = player.GetComponent<FireballSpell>();
                        if (orbSpell3 != null && orbSpell3.enabled) orbSpell3.orbCount += (int)value;
                        else if (fbSpell3 != null) fbSpell3.cooldownMult *= 0.8f;
                        Debug.Log("Skill fire rate boosted");
                        break;
                    }

                case UpgradeData.UpgradeType.FireSpiritRoot:
                    var fsr = player.GetComponent<FireSpiritRoot>();
                    if (fsr == null) fsr = player.AddComponent<FireSpiritRoot>();
                    int before = fsr.level;
                    fsr.LevelUp();
                    Debug.Log($"Fire Spirit Root Lv{before} → Lv{fsr.level}");
                    break;

                case UpgradeData.UpgradeType.WaterSpiritRoot:
                    var wsr = player.GetComponent<WaterSpiritRoot>();
                    if (wsr == null) wsr = player.AddComponent<WaterSpiritRoot>();
                    int wbefore = wsr.level;
                    wsr.LevelUp();
                    Debug.Log($"Water Spirit Root Lv{wbefore} → Lv{wsr.level}");
                    break;

                case UpgradeData.UpgradeType.EarthSpiritRoot:
                    var esr = player.GetComponent<EarthSpiritRoot>();
                    if (esr == null) esr = player.AddComponent<EarthSpiritRoot>();
                    int ebefore = esr.level;
                    esr.LevelUp();
                    Debug.Log($"Earth Spirit Root Lv{ebefore} → Lv{esr.level}");
                    break;

                case UpgradeData.UpgradeType.WindSpiritRoot:
                    var wsr2 = player.GetComponent<WindSpiritRoot>();
                    if (wsr2 == null) wsr2 = player.AddComponent<WindSpiritRoot>();
                    int w2before = wsr2.level;
                    wsr2.LevelUp();
                    Debug.Log($"Wind Spirit Root Lv{w2before} → Lv{wsr2.level}");
                    break;

                case UpgradeData.UpgradeType.ThunderSpiritRoot:
                    var tsr = player.GetComponent<ThunderSpiritRoot>();
                    if (tsr == null) tsr = player.AddComponent<ThunderSpiritRoot>();
                    int tbefore = tsr.level;
                    tsr.LevelUp();
                    Debug.Log($"Thunder Spirit Root Lv{tbefore} → Lv{tsr.level}");
                    break;

                case UpgradeData.UpgradeType.SkillAffix:
                    ApplySkillAffix(player, upgrade);
                    break;
            }
        }

        /// <summary>
        /// 应用词缀 — 写入目标技能下一个未解锁槽位(Lv2/4/6/8/10对应slot0-4)并立即刷新
        /// </summary>
        private void ApplySkillAffix(GameObject player, UpgradeData upgrade)
        {
            if (upgrade.targetSkillType == null) return;
            // 御剑术在独立GameObject上, 用FindObjectOfType查找
            var comp = upgrade.targetSkillType == typeof(FlyingSword)
                ? (Component)FindObjectOfType(upgrade.targetSkillType)
                : player.GetComponent(upgrade.targetSkillType);
            if (comp == null) return;

            // 技能专属AffixSystem (独立子物体, 避免多技能共享; 宿主=技能自身GameObject,
            // 御剑的AffixSystem挂在FlyingSword_0下, 挂Player下飞剑读不到)
            var affix = AffixSystem.GetOrCreateFor(upgrade.targetSkillType, comp.gameObject);

            var lvlField = upgrade.targetSkillType.GetField("skillLevel");
            int lvl = lvlField != null ? (int)lvlField.GetValue(comp) : 1;

            // 功法设计词缀: 解锁设计对应的槽位 (内容来自局外功法设计)
            if (upgrade.designedSlot >= 0)
            {
                affix.SetPresetAffix(upgrade.designedSlot, upgrade.affixType);
                affix.UnlockPlannedSlot(upgrade.designedSlot);
                affix.UpdateAffixes(lvl);
                Debug.Log($"[Affix] {upgrade.targetSkillName} 功法槽位{upgrade.designedSlot + 1}解锁 ← {AffixSystem.GetAffixName(upgrade.affixType)} (Lv{lvl})");
                return;
            }

            // 已解锁槽位数 = 下一个待解锁槽位索引
            int slot = 0;
            for (int i = 0; i < 5; i++)
                if (lvl >= AffixSystem.UnlockLevels[i]) slot = i + 1;
            slot = Mathf.Min(slot, 4); // 满级后替换最后一槽

            affix.SetPresetAffix(slot, upgrade.affixType);
            affix.UpdateAffixes(lvl);
            Debug.Log($"[Affix] {upgrade.targetSkillName} 槽位{slot} ← {AffixSystem.GetAffixName(upgrade.affixType)} (Lv{lvl})");
        }

        private void Update()
        {
            // Close panel with Escape (cancel - pick random)
            if (_isShowing && Input.GetKeyDown(KeyCode.Escape))
            {
                // Pick random
                var buttons = FindObjectsOfType<UpgradeButton>();
                if (buttons.Length > 0)
                {
                    SelectUpgrade(buttons[Random.Range(0, buttons.Length)].upgrade);
                }
            }
        }
    }

    /// <summary>
    /// Helper component for upgrade button clicks.
    /// </summary>
    public class UpgradeButton : MonoBehaviour
    {
        public LevelUpSystem.UpgradeData upgrade;
        public LevelUpSystem system;

        public void RegisterClick()
        {
            Button btn = GetComponent<Button>();
            if (btn != null)
                btn.onClick.AddListener(OnClick);
        }

        private void OnClick()
        {
            if (system != null)
                system.SelectUpgrade(upgrade);
        }
    }
}
