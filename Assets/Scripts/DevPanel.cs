using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;

namespace BiopunkRV
{
    /// <summary>
    /// Developer/Debug panel for adjusting game parameters at runtime.
    /// Toggle with F1 key. Compact two-column layout.
    /// </summary>
    public class DevPanel : MonoBehaviour
    {
        [Header("Sliders")]
        public float minSpawnRate = 0.5f;
        public float maxSpawnRate = 10f;
        public float minMoveSpeed = 1f;
        public float maxMoveSpeed = 30f;

        private GameObject _window;
        private GameObject _panel;
        private bool _isVisible = false;
        private bool _enemiesPaused = false;
        private bool _godMode = false;
        private GameObject _dragonDemo; // 龙形演示对象 (开关式)
        private AffixSystem _labAffix;  // 词缀实验室当前选中的AffixSystem
        private int _labSkillIdx = 0;
        private string _labSkillName = "御剑术";
        private readonly System.Type[] _labSkills = { typeof(FlyingSword), typeof(SpiritOrbSpell), typeof(SpiritArraySpell), typeof(QiBladeSpell), typeof(TalismanSpell) };
        private readonly string[] _labSkillNames = { "御剑术", "灵球术", "灵力阵", "灵气刃", "符箓术" };

        private Slider _spawnSlider;
        private Slider _playerSpeedSlider;
        private Slider _enemySpeedSlider;
        private TextMeshProUGUI _spawnLabel;
        private TextMeshProUGUI _playerSpeedLabel;
        private TextMeshProUGUI _enemySpeedLabel;
        private TextMeshProUGUI _levelLabel;

        public static float spawnRateMultiplier = 1f;
        public static float playerSpeedMultiplier = 1f;
        public static float enemySpeedMultiplier = 1f;

        public static DevPanel Instance { get; private set; }

        private void Awake() { Instance = this; }

        private void Start()
        {
            CreatePanel();
            _window.SetActive(false);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F1))
            {
                _isVisible = !_isVisible;
                _window.SetActive(_isVisible);
            }
        }

        // ── Layout constants ──
        const float PanelW = 460f;
        const float PanelH = 720f;    // 可视窗口高度
        const float ContentH = 3600f; // 内容总高度(超出部分滚动)
        const float ColLeft = -110f;
        const float ColRight = 110f;
        const float BtnH = 28f;
        const float BtnGap = 4f;
        const float SectionGap = 8f;

        private void CreatePanel()
        {
            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas == null) return;

            // 清理场景中保存的旧DevPanel (防止Play模式保存导致的残留UI重叠)
            var oldPanel = canvas.transform.Find("DevPanel");
            if (oldPanel != null) Destroy(oldPanel.gameObject);

            // ── 外层窗口: 固定可视区 + 背景 + 标题 + 滚动 ──
            _window = new GameObject("DevPanel");
            _window.transform.SetParent(canvas.transform, false);

            RectTransform windowRt = _window.AddComponent<RectTransform>();
            windowRt.anchorMin = new Vector2(0.5f, 0.5f);
            windowRt.anchorMax = new Vector2(0.5f, 0.5f);
            windowRt.anchoredPosition = Vector2.zero;
            windowRt.sizeDelta = new Vector2(PanelW, PanelH);

            Image bg = _window.AddComponent<Image>();
            bg.color = new Color(0.1f, 0.1f, 0.15f, 0.93f);

            // Title
            var titleObj = CreateText(_window.transform, "Title", "开发者模式  [F1]", 20, new Vector2(0, PanelH * 0.5f - 18f), new Color(0.9f, 0.85f, 0.5f));
            titleObj.GetComponent<RectTransform>().sizeDelta = new Vector2(PanelW - 20, 24);

            // ── 滚动视口 (顶部留出标题高度) ──
            var viewportGo = new GameObject("Viewport");
            viewportGo.transform.SetParent(_window.transform, false);
            RectTransform vpRt = viewportGo.AddComponent<RectTransform>();
            vpRt.anchorMin = Vector2.zero;
            vpRt.anchorMax = Vector2.one;
            vpRt.offsetMin = new Vector2(4f, 6f);
            vpRt.offsetMax = new Vector2(-4f, -40f);
            viewportGo.AddComponent<RectMask2D>();

            // ── 内容: 大高度, 所有行挂这里 (_panel=内容, 现有parenting代码不变) ──
            _panel = new GameObject("Content");
            _panel.transform.SetParent(viewportGo.transform, false);
            RectTransform contentRt = _panel.AddComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.sizeDelta = new Vector2(0f, ContentH);
            contentRt.anchoredPosition = Vector2.zero;

            ScrollRect scroll = _window.AddComponent<ScrollRect>();
            scroll.content = contentRt;
            scroll.viewport = vpRt;
            scroll.vertical = true;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 20f;

            // ── Row cursor (top-down, 相对内容中心) ──
            float y = ContentH * 0.5f - 25f;

            // === 滑块区 ===
            _spawnSlider = CreateSliderRow(_panel.transform, "SpawnRate", ref y, out _spawnLabel);
            _spawnSlider.minValue = minSpawnRate; _spawnSlider.maxValue = maxSpawnRate; _spawnSlider.value = 1f;
            _spawnSlider.onValueChanged.AddListener(v => { spawnRateMultiplier = v; _spawnLabel.text = $"刷怪: {v:F1}x"; });
            _spawnLabel.text = "刷怪: 1.0x";

            _playerSpeedSlider = CreateSliderRow(_panel.transform, "PlayerSpd", ref y, out _playerSpeedLabel);
            _playerSpeedSlider.minValue = minMoveSpeed; _playerSpeedSlider.maxValue = maxMoveSpeed; _playerSpeedSlider.value = 8f;
            _playerSpeedSlider.onValueChanged.AddListener(v => {
                playerSpeedMultiplier = v; _playerSpeedLabel.text = $"移速: {v:F1}";
                var p = GameObject.Find("Player");
                if (p != null) { var c = p.GetComponent<CharacterController2D>(); if (c != null) c.moveSpeed = v; }
            });
            _playerSpeedLabel.text = "移速: 8.0";

            _enemySpeedSlider = CreateSliderRow(_panel.transform, "EnemySpd", ref y, out _enemySpeedLabel);
            _enemySpeedSlider.minValue = 0.5f; _enemySpeedSlider.maxValue = 15f; _enemySpeedSlider.value = 2.5f;
            _enemySpeedSlider.onValueChanged.AddListener(v => {
                enemySpeedMultiplier = v; _enemySpeedLabel.text = $"怪速: {v:F1}";
                foreach (var e in FindObjectsOfType<EnemyController>()) if (e != null) e.moveSpeed = v;
                foreach (var r in FindObjectsOfType<GhostRabbitController>()) if (r != null) r.moveSpeed = v;
            });
            _enemySpeedLabel.text = "怪速: 2.5";

            // === 升级 ===
            y -= SectionGap;
            // 一键升级 + 等级显示 side by side
            var levelBtn = CreateSmallButton(_panel.transform, "LevelUpBtn", "一键升级", ColLeft, ref y);
            levelBtn.onClick.AddListener(() => {
                ExpBar.currentExp = ExpBar.maxExp; ExpBar.AddExp(0);
                _levelLabel.text = $"Lv {(int)ExpBar.currentLevel}";
            });
            _levelLabel = CreateText(_panel.transform, "LevelLabel", $"Lv {(int)ExpBar.currentLevel}", 15, new Vector2(ColRight, y + BtnH * 0.5f), Color.white).GetComponent<TextMeshProUGUI>();
            _levelLabel.GetComponent<RectTransform>().sizeDelta = new Vector2(180, 24);

            // === 技能管理 (双列: 开关 | 升级) ===
            y -= SectionGap + 5f;
            CreateSectionLabel(_panel.transform, "技能管理", ref y);

            string[] skillNames = { "灵力弹", "飞剑", "灵力阵", "灵气刃", "符箓术", "火球术" };
            string[] compNames = { "SpiritOrbSpell", "FlyingSword", "SpiritArraySpell", "QiBladeSpell", "TalismanSpell", "FireballSpell" };
            bool[] onPlayer = { true, false, true, true, true, true };

            for (int i = 0; i < skillNames.Length; i++)
            {
                // 左列: 开关
                CreateSkillToggleCompact(skillNames[i], compNames[i], ColLeft, ref y, onPlayer[i]);
                // 右列: 升级 (same y position)
                CreateSkillLevelUpCompact(skillNames[i] + "↑", compNames[i], ColRight, y, onPlayer[i]);
            }
            // advance y past the last row (CreateSkillToggleCompact already advanced it once)

            // === 灵根 ===
            y -= SectionGap;
            CreateSectionLabel(_panel.transform, "灵根", ref y);
            CreateSpiritRootToggleCompact("火灵根", "FireSpiritRoot", ColLeft, ref y);
            CreateSpiritRootToggleStatic("水灵根", "WaterSpiritRoot", ColRight, y);

            CreateSpiritRootToggleCompact("土灵根", "EarthSpiritRoot", ColLeft, ref y);
            CreateSpiritRootToggleStatic("风灵根", "WindSpiritRoot", ColRight, y);

            CreateSpiritRootToggleCompact("雷灵根", "ThunderSpiritRoot", ColLeft, ref y);

            // === 怪物生成 ===
            y -= SectionGap;
            CreateSectionLabel(_panel.transform, "怪物生成", ref y);

            var spawnRabbitBtn = CreateSmallButton(_panel.transform, "SpawnRabbit", "生成兔子", ColLeft, ref y);
            spawnRabbitBtn.onClick.AddListener(() => { SpawnEnemyByName("GhostRabbit_Enemy"); });

            var spawnFireBtn = CreateSmallButtonStatic(_panel.transform, "SpawnFire", "生成火灵", ColRight, y);
            spawnFireBtn.onClick.AddListener(() => { SpawnEnemyByName("FireSpirit_Enemy"); });

            var spawnIceBtn = CreateSmallButton(_panel.transform, "SpawnIce", "生成冰狐", ColLeft, ref y);
            spawnIceBtn.onClick.AddListener(() => { SpawnEnemyByName("IceFox_Enemy"); });

            var spawnGoldBtn = CreateSmallButtonStatic(_panel.transform, "SpawnGold", "生成金甲", ColRight, y);
            spawnGoldBtn.onClick.AddListener(() => { SpawnEnemyByName("GoldenArmor_Enemy"); });

            var spawnWindBtn = CreateSmallButton(_panel.transform, "SpawnWind", "生成风蝶", ColLeft, ref y);
            spawnWindBtn.onClick.AddListener(() => { SpawnEnemyByName("WindButterfly_Enemy"); });

            var spawnThunderBtn = CreateSmallButtonStatic(_panel.transform, "SpawnThunder", "生成雷球", ColRight, y);
            spawnThunderBtn.onClick.AddListener(() => { SpawnEnemyByName("ThunderOrb_Enemy"); });

            var spawnBossBtn = CreateSmallButton(_panel.transform, "SpawnBoss", "生成Boss", ColLeft, ref y);
            spawnBossBtn.onClick.AddListener(() => { SpawnEnemyByName("CrimsonFlameBoss_Enemy"); });

            var spawnGolemBtn = CreateSmallButtonStatic(_panel.transform, "SpawnGolem", "生成石巨人", ColRight, y);
            spawnGolemBtn.onClick.AddListener(() => { SpawnEnemyByName("StoneGolem_Enemy"); });

            // === 调试 ===
            y -= SectionGap;
            CreateSectionLabel(_panel.transform, "调试工具", ref y);

            var pauseBtn = CreateSmallButton(_panel.transform, "PauseBtn", "暂停怪物", ColLeft, ref y);
            pauseBtn.onClick.AddListener(() => {
                _enemiesPaused = !_enemiesPaused;
                foreach (var e in FindObjectsOfType<EnemyController>()) if (e != null) e.moveSpeed = _enemiesPaused ? 0f : _enemySpeedSlider.value;
            });
            var burnBtn = CreateSmallButtonStatic(_panel.transform, "BurnBtn", "全部燃烧", ColRight, y);
            burnBtn.onClick.AddListener(() => {
                foreach (var e in FindObjectsOfType<EnemyController>()) if (e != null && e.element != Element.Fire) e.ApplyBurnFromSpread();
            });

            var deathBtn = CreateSmallButton(_panel.transform, "DeathBtn", "死亡特效", ColLeft, ref y);
            deathBtn.onClick.AddListener(() => {
                foreach (var e in FindObjectsOfType<EnemyController>()) if (e != null) e.TriggerDeathEffectDebug();
            });
            var killBtn = CreateSmallButtonStatic(_panel.transform, "KillBtn", "杀死全部", ColRight, y);

            var godBtn = CreateSmallButton(_panel.transform, "GodMode", "无敌模式", ColLeft, ref y);
            var godTxt = godBtn.GetComponentInChildren<TextMeshProUGUI>();
            godBtn.onClick.AddListener(() => {
                _godMode = !_godMode;
                var psc = PlayerStatsComponent.Instance;
                if (psc != null) psc.godMode = _godMode;
                Time.timeScale = 1f;
                godTxt.color = _godMode ? Color.green : Color.white;
            });
            killBtn.onClick.AddListener(() => {
                foreach (var e in FindObjectsOfType<EnemyController>()) if (e != null) e.ForceKill();
            });

            // === 天降演示 (坠落词缀效果预览) ===
            y -= SectionGap;
            CreateSectionLabel(_panel.transform, "天降演示", ref y);

            var fallOrbBtn = CreateSmallButton(_panel.transform, "FallOrb", "天降灵球", ColLeft, ref y);
            fallOrbBtn.onClick.AddListener(() => { SkyfallDemo("SpiritOrbSpell"); });

            var fallFireBtn = CreateSmallButtonStatic(_panel.transform, "FallFire", "天降火球", ColRight, y);
            fallFireBtn.onClick.AddListener(() => { SkyfallDemo("FireballSpell"); });

            var fallSwordBtn = CreateSmallButton(_panel.transform, "FallSword", "天降飞剑", ColLeft, ref y);
            fallSwordBtn.onClick.AddListener(() => { SkyfallDemo("FlyingSword"); });

            var fallToggleBtn = CreateSmallButtonStatic(_panel.transform, "FallToggle", "坠落开关", ColRight, y);
            var fallToggleTxt = fallToggleBtn.GetComponentInChildren<TextMeshProUGUI>();
            fallToggleBtn.onClick.AddListener(() => {
                var player = GameObject.Find("Player");
                if (player == null) return;
                foreach (var comp in player.GetComponents<MonoBehaviour>())
                {
                    var af = comp?.GetType().GetField("affixSystem")?.GetValue(comp) as AffixSystem;
                    if (af == null) continue;
                    bool nowOn = !af.fallMode;
                    if (nowOn)
                    {
                        af.SetPresetAffix(0, AffixType.Fall);
                        var slf = comp.GetType().GetField("skillLevel");
                        if (slf != null && (int)slf.GetValue(comp) < 2) slf.SetValue(comp, 2);
                        af.UpdateAffixes((int)slf.GetValue(comp));
                    }
                    else
                    {
                        af.SetPresetAffix(0, AffixType.Pierce);
                        af.UpdateAffixes(comp.GetType().GetField("skillLevel") != null ? (int)comp.GetType().GetField("skillLevel").GetValue(comp) : 1);
                    }
                }
                fallToggleTxt.color = fallToggleTxt.color == Color.green ? Color.white : Color.green;
            });

            var divineBtn = CreateSmallButton(_panel.transform, "DivineCharge", "神通充能", ColLeft, ref y);
            divineBtn.onClick.AddListener(() => {
                var player = GameObject.Find("Player");
                if (player == null) return;
                var ds = DivineSystem.Ensure(player.transform);
                var fb = player.GetComponent<FireballSpell>();
                var qb = player.GetComponent<QiBladeSpell>();
                var sw = Object.FindObjectOfType<FlyingSword>();
                var tsm = player.GetComponent<TalismanSpell>();
                if (fb != null) ds.AddCharge(fb, "FireDragon");          // 陨星雨
                if (sw != null) ds.AddCharge(sw, "CrimsonSword");        // 万剑天降
                if (qb != null) ds.AddCharge(qb, "GaleBlade");           // 万刃天落
                if (tsm != null) ds.AddCharge(tsm, "FireDragon");        // 万符龙卷
                Debug.Log($"[DevPanel] 神通充能完成, 按R释放 (共{ds.ChargeCount}个)");
            });

            var divineHint = CreateText(_panel.transform, "DivineHint", "充能后按 R 释放神通", 11, new Vector2(ColRight, y - BtnH * 0.5f), new Color(0.5f, 0.5f, 0.55f));
            divineHint.GetComponent<RectTransform>().sizeDelta = new Vector2(180, 16);

            // === 灵根机制演示 (机制型灵根词缀预览, 无视门控) ===
            y -= SectionGap;
            CreateSectionLabel(_panel.transform, "灵根机制演示", ref y);

            var rmThunderBtn = CreateSmallButton(_panel.transform, "RMThunder", "落雷演示", ColLeft, ref y);
            rmThunderBtn.onClick.AddListener(() => { RootMechanics.DemoThunder(); });
            var rmFireBtn = CreateSmallButtonStatic(_panel.transform, "RMFire", "火径演示", ColRight, y);
            rmFireBtn.onClick.AddListener(() => { RootMechanics.DemoFireTrail(); });

            var rmWaterBtn = CreateSmallButton(_panel.transform, "RMWater", "漩涡演示", ColLeft, ref y);
            rmWaterBtn.onClick.AddListener(() => { RootMechanics.DemoVortex(); });
            var rmEarthBtn = CreateSmallButtonStatic(_panel.transform, "RMEarth", "岩脊演示", ColRight, y);
            rmEarthBtn.onClick.AddListener(() => { RootMechanics.DemoStoneSpine(); });

            var rmWindBtn = CreateSmallButton(_panel.transform, "RMWind", "风行演示", ColLeft, ref y);
            rmWindBtn.onClick.AddListener(() => { RootMechanics.DemoGust(); });
            var rmHint = CreateText(_panel.transform, "RMHint", "灵根Lv3后局内自动生效", 11, new Vector2(ColRight, y - BtnH * 0.5f), new Color(0.5f, 0.5f, 0.55f));
            rmHint.GetComponent<RectTransform>().sizeDelta = new Vector2(180, 16);

            var rmDragonBtn = CreateSmallButton(_panel.transform, "RMDragon", "龙形演示", ColLeft, ref y);
            var rmDragonTxt = rmDragonBtn.GetComponentInChildren<TextMeshProUGUI>();
            rmDragonBtn.onClick.AddListener(() => {
                // 开关式: 再点一次销毁
                if (_dragonDemo != null) { Object.Destroy(_dragonDemo); _dragonDemo = null; rmDragonTxt.color = Color.white; return; }
                var player = GameObject.Find("Player");
                if (player == null) return;
                var go = new GameObject("DragonSwimmerDemo");
                var swim = go.AddComponent<DragonSwimmer>(); // Ribbon龙: 精灵平面内行波游动, 与角色同视角
                swim.ribbonScale = 0.55f;
                swim.anchorTarget = player.transform;
                swim.sortingOrder = 60;
                _dragonDemo = go;
                rmDragonTxt.color = Color.green;
                Debug.Log("[DevPanel] 龙形演示开启 (Ribbon龙·精灵平面游动), 再点一次关闭");
            });
            var rmDragonHint = CreateText(_panel.transform, "RMDragonHint", "再点一次关闭", 11, new Vector2(ColRight, y - BtnH * 0.5f), new Color(0.5f, 0.5f, 0.55f));
            rmDragonHint.GetComponent<RectTransform>().sizeDelta = new Vector2(180, 16);

            // 龙属性循环: 改EvolvedVisuals当前属性, 龙形演示开着则同步换色
            var rmElemBtn = CreateSmallButton(_panel.transform, "RMElement", "龙属性循环", ColLeft, ref y);
            var rmElemTxt = rmElemBtn.GetComponentInChildren<TextMeshProUGUI>();
            rmElemBtn.onClick.AddListener(() => {
                string name = EvolvedVisuals.CycleElement();
                var dragon = GameObject.Find("DragonSwimmerDemo");
                if (dragon != null) dragon.GetComponent<DragonSwimmer>()?.SetElement(EvolvedVisuals.CurrentElement);
                rmElemTxt.text = "属性:" + name;
                Debug.Log("[DevPanel] 进阶演示属性 → " + name);
            });
            var rmElemHint = CreateText(_panel.transform, "RMElemHint", "当前演示属性: 火", 11, new Vector2(ColRight, y - BtnH * 0.5f), new Color(0.5f, 0.5f, 0.55f));
            rmElemHint.GetComponent<RectTransform>().sizeDelta = new Vector2(180, 16);

            // === 进阶外观演示 (基础外观 × 词缀呈现) ===
            y -= SectionGap;
            CreateSectionLabel(_panel.transform, "进阶外观演示", ref y);

            var evOrbBtn = CreateSmallButton(_panel.transform, "EvDragonOrb", "龙弹演示", ColLeft, ref y);
            evOrbBtn.onClick.AddListener(() => { EvolvedVisuals.DemoDragonOrb(); });
            var evSwordBtn = CreateSmallButtonStatic(_panel.transform, "EvDragonSword", "龙剑演示", ColRight, y);
            evSwordBtn.onClick.AddListener(() => { EvolvedVisuals.DemoDragonSword(); });

            var evWallBtn = CreateSmallButton(_panel.transform, "EvElementWall", "属性墙演示", ColLeft, ref y);
            evWallBtn.onClick.AddListener(() => { EvolvedVisuals.DemoElementWall(); });
            var evBladeBtn = CreateSmallButtonStatic(_panel.transform, "EvBladeOrbit", "刃环绕演示", ColRight, y);
            evBladeBtn.onClick.AddListener(() => { EvolvedVisuals.DemoBladeOrbit(); });

            var evTalisBtn = CreateSmallButton(_panel.transform, "EvTalismanOrbit", "符环绕演示", ColLeft, ref y);
            evTalisBtn.onClick.AddListener(() => { EvolvedVisuals.DemoTalismanOrbit(); });
            var evHint = CreateText(_panel.transform, "EvHint", "基础外观×词缀呈现", 11, new Vector2(ColRight, y - BtnH * 0.5f), new Color(0.5f, 0.5f, 0.55f));
            evHint.GetComponent<RectTransform>().sizeDelta = new Vector2(180, 16);

            // ═══ 词缀实验室 (固定在Content最底部 — 绝对定位, 绝不与其他区域重叠) ═══
            // 位置计算: Content底部向上, 不依赖y追踪
            float labBaseY = -ContentH * 0.5f + 100f; // Content底部上方100px起
            float labSecY = labBaseY + 20f;            // section标签y

            // section标签 (固定位置, 不用ref y)
            var labSecLabel = new GameObject("LabSectionLabel", typeof(RectTransform));
            labSecLabel.transform.SetParent(_panel.transform, false);
            var labSecRt = (RectTransform)labSecLabel.transform;
            labSecRt.anchorMin = new Vector2(0.5f, 0.5f);
            labSecRt.anchorMax = new Vector2(0.5f, 0.5f);
            labSecRt.anchoredPosition = new Vector2(0f, labSecY);
            labSecRt.sizeDelta = new Vector2(PanelW - 20, 24);
            var labSecTxt = labSecLabel.AddComponent<TextMeshProUGUI>();
            labSecTxt.text = "── 词缀实验室 ──";
            labSecTxt.fontSize = 14;
            labSecTxt.alignment = TextAlignmentOptions.Center;
            labSecTxt.color = new Color(0.8f, 0.7f, 0.3f);

            // 技能选择 + 清空 (固定位置)
            float labRow0Y = labSecY - 28f;
            var labSkillGo = new GameObject("LabSkill", typeof(RectTransform));
            labSkillGo.transform.SetParent(_panel.transform, false);
            var labSkillRt = (RectTransform)labSkillGo.transform;
            labSkillRt.anchorMin = new Vector2(0.5f, 0.5f);
            labSkillRt.anchorMax = new Vector2(0.5f, 0.5f);
            labSkillRt.anchoredPosition = new Vector2(ColLeft, labRow0Y);
            labSkillRt.sizeDelta = new Vector2(200f, BtnH);
            var labSkillImg = labSkillGo.AddComponent<Image>();
            labSkillImg.color = new Color(0.2f, 0.2f, 0.3f, 0.9f);
            var labSkillBtn = labSkillGo.AddComponent<Button>();
            var labSkillTxtGo = new GameObject("Txt", typeof(RectTransform));
            txtGoSet(labSkillTxtGo, labSkillGo.transform);
            var labSkillTxt = labSkillTxtGo.AddComponent<TextMeshProUGUI>();
            labSkillTxt.text = "技能:御剑术";
            labSkillTxt.fontSize = 12;
            labSkillTxt.alignment = TextAlignmentOptions.Center;
            labSkillTxt.color = Color.white;
            labSkillBtn.onClick.AddListener(() => {
                _labSkillIdx = (_labSkillIdx + 1) % _labSkills.Length;
                _labSkillName = _labSkillNames[_labSkillIdx];
                labSkillTxt.text = "技能:" + _labSkillName;
                var p2 = GameObject.Find("Player");
                if (p2 != null)
                {
                    var comp = p2.GetComponent(_labSkills[_labSkillIdx]);
                    if (comp != null) _labAffix = AffixSystem.GetOrCreateFor(_labSkills[_labSkillIdx], p2);
                }
            });

            var labClearGo = new GameObject("LabClear", typeof(RectTransform));
            labClearGo.transform.SetParent(_panel.transform, false);
            var labClearRt = (RectTransform)labClearGo.transform;
            labClearRt.anchorMin = new Vector2(0.5f, 0.5f);
            labClearRt.anchorMax = new Vector2(0.5f, 0.5f);
            labClearRt.anchoredPosition = new Vector2(ColRight, labRow0Y);
            labClearRt.sizeDelta = new Vector2(200f, BtnH);
            var labClearImg = labClearGo.AddComponent<Image>();
            labClearImg.color = new Color(0.2f, 0.2f, 0.3f, 0.9f);
            var labClearBtn = labClearGo.AddComponent<Button>();
            var labClearTxtGo = new GameObject("Txt", typeof(RectTransform));
            txtGoSet(labClearTxtGo, labClearGo.transform);
            var labClearTxt = labClearTxtGo.AddComponent<TextMeshProUGUI>();
            labClearTxt.text = "清空反应词缀";
            labClearTxt.fontSize = 12;
            labClearTxt.alignment = TextAlignmentOptions.Center;
            labClearTxt.color = Color.white;
            labClearBtn.onClick.AddListener(() => { _labAffix?.ClearForceAffixes(); });

            // 全词缀toggle按钮网格 (4列, 固定位置)
            var allTypes = (AffixType[])System.Enum.GetValues(typeof(AffixType));
            int labPerRow = 4;
            float labBtnW = 130f, labBtnH = 22f;
            float labX0 = -192f;
            float labGridY0 = labRow0Y - 32f; // 网格起始y
            var labActive = new HashSet<AffixType>();
            for (int i = 0; i < allTypes.Length; i++)
            {
                var t = allTypes[i];
                if (t == AffixType.Track) continue;
                int row = i / labPerRow, col = i % labPerRow;
                var go = new GameObject("LabAffix_" + t, typeof(RectTransform));
                go.transform.SetParent(_panel.transform, false);
                var rt = (RectTransform)go.transform;
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(labX0 + col * labBtnW + labBtnW * 0.5f, labGridY0 - row * (labBtnH + 3f) - labBtnH * 0.5f);
                rt.sizeDelta = new Vector2(labBtnW - 4f, labBtnH);
                var btnImg = go.AddComponent<Image>();
                btnImg.color = new Color(0.15f, 0.15f, 0.2f, 0.9f);
                var btn = go.AddComponent<Button>();
                var txtGo = new GameObject("Txt", typeof(RectTransform));
                txtGo.transform.SetParent(go.transform, false);
                var txtRt = (RectTransform)txtGo.transform;
                txtRt.anchorMin = Vector2.zero;
                txtRt.anchorMax = Vector2.one;
                txtRt.offsetMin = Vector2.zero;
                txtRt.offsetMax = Vector2.zero;
                var txt = txtGo.AddComponent<TextMeshProUGUI>();
                txt.text = AffixSystem.GetAffixName(t);
                txt.fontSize = 10;
                txt.alignment = TextAlignmentOptions.Center;
                txt.color = Color.white;
                txt.raycastTarget = false;
                var capturedT = t;
                var capturedTxt = txt;
                btn.onClick.AddListener(() => {
                    if (_labAffix == null) return;
                    if (labActive.Contains(capturedT))
                    {
                        _labAffix.ForceRemoveAffix(capturedT);
                        labActive.Remove(capturedT);
                        capturedTxt.color = Color.white;
                    }
                    else
                    {
                        _labAffix.ForceAddAffix(capturedT);
                        labActive.Add(capturedT);
                        capturedTxt.color = Color.green;
                    }
                });
            }

        }

        // ── 天降演示: 对最近敌人发射一次天降打击 ──

        /// <summary>在最近敌人位置触发天降打击预览</summary>
        private void SkyfallDemo(string skillName)
        {
            var player = GameObject.Find("Player");
            if (player == null) return;

            // 找最近敌人
            Transform nearest = null;
            float nearestDist = 100f;
            foreach (var e in FindObjectsOfType<EnemyController>())
            {
                if (e == null || !e.gameObject.activeInHierarchy) continue;
                float d = Vector3.Distance(player.transform.position, e.transform.position);
                if (d < nearestDist) { nearestDist = d; nearest = e.transform; }
            }
            if (nearest == null)
            {
                // 没敌人时在玩家前方8单位演示
                nearest = player.transform;
                nearestDist = 0f;
            }

            Vector3 targetPos = nearest.position + (nearestDist < 1f ? player.transform.forward * 8f : Vector3.zero);

            if (skillName == "SpiritOrbSpell")
            {
                var orb = player.GetComponent<SpiritOrbSpell>();
                if (orb == null) return;
                Element el = SpiritRootManager.GetEffectiveElement(orb.element);
                Sprite spr = InvokeGetOrbSprite(orb, el);
                if (spr == null) spr = Resources.Load<Sprite>("SpiritOrb");
                Color tint = (spr != null && orb.orbSprite != null && spr == orb.orbSprite) ? ElementColors.GetTint(el) : Color.white;
                float worldSize = 3f;
                SkyfallStrike.Strike(targetPos, spr, null, tint, el, worldSize, 22f, 0.5f, 0.28f, 3f, null);
            }
            else if (skillName == "FireballSpell")
            {
                var fb = player.GetComponent<FireballSpell>();
                if (fb == null) return;
                Sprite[] frames = fb.fireballFrames;
                Sprite spr = (frames != null && frames.Length > 0) ? frames[0] : fb.fireballSprite;
                if (spr == null) spr = Resources.Load<Sprite>("FireballSpell");
                float unit = spr.pixelsPerUnit > 0 ? spr.rect.width / spr.pixelsPerUnit : 1f;
                float worldSize = unit * fb.fireballScale * fb.sizeMultiplier;
                SkyfallStrike.Strike(targetPos, spr, frames, Color.white, Element.Fire, worldSize, 22f, 0.5f, 0.28f, 3.5f, null);
            }
            else if (skillName == "FlyingSword")
            {
                Sprite spr = SkyfallStrike.SwordSprite;
#if UNITY_EDITOR
                if (spr == null) spr = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/TJGenerators/History/FlyingSword_v2.png");
#endif
                if (spr == null) return;
                float unit = spr.pixelsPerUnit > 0 ? spr.rect.width / spr.pixelsPerUnit : 1f;
                float worldSize = unit * 0.2f;
                SkyfallStrike.Strike(targetPos, spr, null, Color.white, Element.None, worldSize, 22f, 0.5f, 0.28f, 3.5f, null, FallStyle.Sword);
            }
        }

        /// <summary>反射调用SpiritOrbSpell.GetOrbSpriteForElement(private方法)</summary>
        private Sprite InvokeGetOrbSprite(SpiritOrbSpell orb, Element el)
        {
            var mi = typeof(SpiritOrbSpell).GetMethod("GetOrbSpriteForElement",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (mi != null) return mi.Invoke(orb, new object[] { el }) as Sprite;
            return null;
        }

        // ── Compact UI helpers ──

        /// <summary>设置子文本物体铺满父物体</summary>
        private static void txtGoSet(GameObject txtGo, Transform parent)
        {
            txtGo.transform.SetParent(parent, false);
            var rt = (RectTransform)txtGo.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private void CreateSectionLabel(Transform parent, string title, ref float y)
        {
            y -= 10f;
            var obj = CreateText(parent, "Section_" + title, "── " + title + " ──", 15, new Vector2(0, y), new Color(0.8f, 0.75f, 0.4f));
            obj.GetComponent<RectTransform>().sizeDelta = new Vector2(PanelW - 20, 20);
            y -= 22f;
        }

        private Slider CreateSliderRow(Transform parent, string name, ref float y, out TextMeshProUGUI label)
        {
            y -= 8f;
            var labelObj = CreateText(parent, name + "_Lbl", "", 14, new Vector2(ColLeft, y - 6f), Color.white);
            labelObj.GetComponent<RectTransform>().sizeDelta = new Vector2(180, 16);
            label = labelObj.GetComponent<TextMeshProUGUI>();

            GameObject sliderObj = new GameObject(name + "_Slider");
            sliderObj.transform.SetParent(parent, false);
            RectTransform srt = sliderObj.AddComponent<RectTransform>();
            srt.anchorMin = new Vector2(0.5f, 0.5f); srt.anchorMax = new Vector2(0.5f, 0.5f);
            srt.anchoredPosition = new Vector2(ColRight, y - 6f);
            srt.sizeDelta = new Vector2(180, 16);
            Image sbg = sliderObj.AddComponent<Image>(); sbg.color = new Color(0.2f, 0.2f, 0.25f, 1f);

            GameObject fillArea = new GameObject("Fill"); fillArea.transform.SetParent(sliderObj.transform, false);
            RectTransform frt = fillArea.AddComponent<RectTransform>();
            frt.anchorMin = new Vector2(0, 0.25f); frt.anchorMax = new Vector2(1, 0.75f); frt.offsetMin = new Vector2(4, 0); frt.offsetMax = new Vector2(-4, 0);
            Image fillImg = fillArea.AddComponent<Image>(); fillImg.color = new Color(0.3f, 0.7f, 0.5f, 1f);

            GameObject handle = new GameObject("Handle"); handle.transform.SetParent(sliderObj.transform, false);
            RectTransform hrt = handle.AddComponent<RectTransform>(); hrt.sizeDelta = new Vector2(12, 12);
            Image handleImg = handle.AddComponent<Image>(); handleImg.color = Color.white;

            Slider slider = sliderObj.AddComponent<Slider>();
            slider.fillRect = fillImg.GetComponent<RectTransform>();
            slider.handleRect = hrt;
            slider.direction = Slider.Direction.LeftToRight;

            y -= 28f;
            return slider;
        }

        private Button CreateSmallButton(Transform parent, string name, string text, float x, ref float y)
        {
            y -= BtnGap;
            GameObject btnObj = new GameObject(name);
            btnObj.transform.SetParent(parent, false);
            RectTransform rt = btnObj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f); rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(180, BtnH);
            Image img = btnObj.AddComponent<Image>(); img.color = new Color(0.2f, 0.5f, 0.3f, 1f);

            GameObject tObj = new GameObject("Text"); tObj.transform.SetParent(btnObj.transform, false);
            RectTransform trt = tObj.AddComponent<RectTransform>();
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
            TextMeshProUGUI tmp = tObj.AddComponent<TextMeshProUGUI>();
            tmp.text = text; tmp.fontSize = 14; tmp.alignment = TextAlignmentOptions.Center; tmp.color = Color.white;

            Button btn = btnObj.AddComponent<Button>();
            ColorBlock cb = btn.colors;
            cb.normalColor = new Color(0.2f, 0.5f, 0.3f, 1f);
            cb.highlightedColor = new Color(0.3f, 0.6f, 0.4f, 1f);
            cb.pressedColor = new Color(0.15f, 0.4f, 0.25f, 1f);
            btn.colors = cb;

            y -= BtnH;
            return btn;
        }

        // Variant that doesn't advance y (for right column)
        private Button CreateSmallButtonStatic(Transform parent, string name, string text, float x, float yPos)
        {
            GameObject btnObj = new GameObject(name);
            btnObj.transform.SetParent(parent, false);
            RectTransform rt = btnObj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f); rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x, yPos);
            rt.sizeDelta = new Vector2(180, BtnH);
            Image img = btnObj.AddComponent<Image>(); img.color = new Color(0.18f, 0.4f, 0.55f, 1f);

            GameObject tObj = new GameObject("Text"); tObj.transform.SetParent(btnObj.transform, false);
            RectTransform trt = tObj.AddComponent<RectTransform>();
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
            TextMeshProUGUI tmp = tObj.AddComponent<TextMeshProUGUI>();
            tmp.text = text; tmp.fontSize = 14; tmp.alignment = TextAlignmentOptions.Center; tmp.color = Color.white;

            Button btn = btnObj.AddComponent<Button>();
            ColorBlock cb = btn.colors;
            cb.normalColor = new Color(0.18f, 0.4f, 0.55f, 1f);
            cb.highlightedColor = new Color(0.25f, 0.5f, 0.65f, 1f);
            cb.pressedColor = new Color(0.12f, 0.3f, 0.45f, 1f);
            btn.colors = cb;
            return btn;
        }

        /// <summary>查找技能组件 — Player上的直接GetComponent, 独立对象(如飞剑)用FindObjectOfType</summary>
        private static Component FindSkillComp(string compName, bool isOnPlayer)
        {
            var type = System.Type.GetType("BiopunkRV." + compName);
            if (type == null) return null;
            if (isOnPlayer)
            {
                var p = GameObject.Find("Player");
                return p != null ? p.GetComponent(type) : null;
            }
            return Object.FindObjectOfType(type) as Component;
        }

        private void CreateSkillToggleCompact(string displayName, string compName, float x, ref float y, bool isOnPlayer)
        {
            var comp0 = FindSkillComp(compName, isOnPlayer) as Behaviour;
            bool isActive = comp0 != null && comp0.enabled;

            y -= BtnGap;
            GameObject btnObj = new GameObject("Toggle_" + compName);
            btnObj.transform.SetParent(_panel.transform, false);
            RectTransform rt = btnObj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f); rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(180, BtnH);
            Image img = btnObj.AddComponent<Image>();
            img.color = isActive ? new Color(0.2f, 0.6f, 0.3f, 1f) : new Color(0.5f, 0.2f, 0.2f, 0.7f);

            GameObject tObj = new GameObject("Text"); tObj.transform.SetParent(btnObj.transform, false);
            RectTransform trt = tObj.AddComponent<RectTransform>();
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
            TextMeshProUGUI tmp = tObj.AddComponent<TextMeshProUGUI>();
            tmp.text = displayName + (isActive ? " ✓" : " ✗");
            tmp.fontSize = 14; tmp.alignment = TextAlignmentOptions.Center; tmp.color = Color.white;

            Button btn = btnObj.AddComponent<Button>();
            string nc = displayName; string cc = compName; Image ic = img; TextMeshProUGUI tc = tmp;
            btn.onClick.AddListener(() => {
                var comp = FindSkillComp(cc, isOnPlayer) as Behaviour;
                if (comp != null)
                {
                    bool now = !comp.enabled; comp.enabled = now;
                    ic.color = now ? new Color(0.2f, 0.6f, 0.3f, 1f) : new Color(0.5f, 0.2f, 0.2f, 0.7f);
                    tc.text = nc + (now ? " ✓" : " ✗");
                }
            });

            y -= BtnH;
        }

        private void CreateSkillLevelUpCompact(string displayName, string compName, float x, float yPos, bool isOnPlayer)
        {
            var btn = CreateSmallButtonStatic(_panel.transform, "LvlUp_" + compName, displayName, x, yPos);
            string cc = compName;
            btn.onClick.AddListener(() => {
                var comp = FindSkillComp(cc, isOnPlayer);
                if (comp == null) { Debug.LogWarning($"{cc} not found in scene"); return; }
                var m = comp.GetType().GetMethod("LevelUp");
                if (m != null) { m.Invoke(comp, null); Debug.Log($"{cc}.LevelUp()"); }
                else Debug.LogWarning($"{cc} has no LevelUp()");
            });
        }

        private void CreateSpiritRootToggleCompact(string displayName, string compName, float x, ref float y)
        {
            CreateSpiritRootToggleAt(displayName, compName, x, y);
            y -= BtnGap + BtnH;
        }

        private void CreateSpiritRootToggleStatic(string displayName, string compName, float x, float yPos)
        {
            CreateSpiritRootToggleAt(displayName, compName, x, yPos);
        }

        private void CreateSpiritRootToggleAt(string displayName, string compName, float x, float yPos)
        {
            var player = GameObject.Find("Player");
            var rootType = System.Type.GetType("BiopunkRV." + compName);
            var levelField = rootType != null ? rootType.GetField("level") : null;
            bool isActive = false;
            if (player != null && rootType != null)
            {
                var t = player.GetComponent(rootType);
                // level=0的组件是未激活状态(编辑模式残留), 不算✓
                if (t != null)
                    isActive = levelField == null || (int)levelField.GetValue(t) > 0;
            }

            GameObject btnObj = new GameObject("Toggle_" + compName);
            btnObj.transform.SetParent(_panel.transform, false);
            RectTransform rt = btnObj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f); rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x, yPos);
            rt.sizeDelta = new Vector2(180, BtnH);
            Image img = btnObj.AddComponent<Image>();
            img.color = isActive ? new Color(0.5f, 0.2f, 0.7f, 1f) : new Color(0.35f, 0.2f, 0.4f, 0.7f);

            GameObject tObj = new GameObject("Text"); tObj.transform.SetParent(btnObj.transform, false);
            RectTransform trt = tObj.AddComponent<RectTransform>();
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
            TextMeshProUGUI tmp = tObj.AddComponent<TextMeshProUGUI>();
            tmp.text = displayName + (isActive ? " ✓" : " ✗");
            tmp.fontSize = 14; tmp.alignment = TextAlignmentOptions.Center; tmp.color = Color.white;

            Button btn = btnObj.AddComponent<Button>();
            string nc = displayName; string cc = compName; Image ic = img; TextMeshProUGUI tc = tmp;
            btn.onClick.AddListener(() => {
                var p = GameObject.Find("Player"); if (p == null) return;
                var type = System.Type.GetType("BiopunkRV." + cc); if (type == null) return;
                var lvlField = type.GetField("level");
                var comp = p.GetComponent(type);
                if (comp != null)
                {
                    int lvl = lvlField != null ? (int)lvlField.GetValue(comp) : 1;
                    if (lvl > 0)
                    {
                        // 已激活 → 移除
                        if (Application.isPlaying) Destroy(comp); else DestroyImmediate(comp);
                        ic.color = new Color(0.35f, 0.2f, 0.4f, 0.7f); tc.text = nc + " ✗";
                    }
                    else
                    {
                        // level=0的未激活组件(编辑模式残留) → 升到Lv1激活
                        var levelUpMethod = type.GetMethod("LevelUp");
                        if (levelUpMethod != null) levelUpMethod.Invoke(comp, null);
                        ic.color = new Color(0.5f, 0.2f, 0.7f, 1f); tc.text = nc + " ✓";
                        Debug.Log($"{cc} activated: level 0 → 1");
                    }
                }
                else
                {
                    var added = p.AddComponent(type);
                    // 灵根组件添加后自动升到Lv1
                    var levelUpMethod = type.GetMethod("LevelUp");
                    if (levelUpMethod != null) levelUpMethod.Invoke(added, null);
                    ic.color = new Color(0.5f, 0.2f, 0.7f, 1f); tc.text = nc + " ✓";
                }
            });
        }

        private void SpawnEnemyByName(string prefabName)
        {
            GameObject prefab = null;

#if UNITY_EDITOR
            var guids = UnityEditor.AssetDatabase.FindAssets(prefabName + " t:GameObject");
            if (guids.Length > 0)
                prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]));
#endif

            if (prefab == null)
                prefab = Resources.Load<GameObject>($"Enemies/{prefabName}");

            if (prefab == null) return;

            var player = GameObject.Find("Player");
            if (player == null) return;

            Vector3 pos = player.transform.position + Random.onUnitSphere * 15f;
            pos.y = 1.6f;

            var enemy = Instantiate(prefab, pos, prefab.transform.rotation);
            enemy.name = "Enemy_" + Time.frameCount;

            var ctrl = enemy.GetComponent<EnemyController>();
            if (ctrl != null) ctrl.target = player.transform;
        }

        // ── Generic helpers ──

        private GameObject CreateText(Transform parent, string name, string text, float fontSize, Vector2 pos, Color color)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            RectTransform rt = obj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f); rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            TextMeshProUGUI tmp = obj.AddComponent<TextMeshProUGUI>();
            tmp.text = text; tmp.fontSize = fontSize; tmp.alignment = TextAlignmentOptions.Center; tmp.color = color;
            return obj;
        }
    }
}
