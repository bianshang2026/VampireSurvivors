using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace BiopunkRV
{
    /// <summary>
    /// 技能查看面板 — 点击卷轴图标打开/关闭。
    /// 显示当前局内技能（最多6个），包含图标、名称、等级、属性。
    /// </summary>
    [ExecuteAlways]
public class SkillPanelManager : MonoBehaviour
    {
        [Header("Sprites")]
        public Sprite panelBgSprite;       // 卷轴展开背景
        public Sprite slotSprite;          // 单个格子背景
        public Sprite closedScrollSprite;  // 闭合卷轴图标
        public Sprite openScrollSprite;    // 打开状态图标

        [Header("Skill Icons")]
        public Sprite swordIcon;           // 御剑术图标
        public Sprite fireballIcon;        // 火球术图标
        public Sprite spiritOrbIcon;       // 灵力弹图标
        public Sprite arrayIcon;           // 灵力阵图标
        public Sprite qiBladeIcon;         // 灵气刃图标
        public Sprite talismanIcon;        // 符箓术图标

        [Header("Layout")]
        public int maxSlots = 6;
        public int columns = 3;
        public float slotSize = 120f;
        public float slotSpacing = 20f;

        private GameObject _toggleButton;
        private GameObject _panel;
        private bool _isOpen = false;

        private void Awake()
        {
#if UNITY_EDITOR
            if (panelBgSprite == null)
                panelBgSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/TJGenerators/History/Sprite_20260808_022833.png");
            if (closedScrollSprite == null)
                closedScrollSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/TJGenerators/History/Sprite_20260808_221056.png");
            if (spiritOrbIcon == null)
                spiritOrbIcon = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/TJGenerators/History/Sprite_20260808_193011.png");
            if (fireballIcon == null)
                fireballIcon = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/TJGenerators/History/Sprite_20260808_164635.png");
            if (swordIcon == null)
                swordIcon = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/TJGenerators/History/Sprite_20260808_185133.png");
            if (slotSprite == null)
                slotSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/TJGenerators/History/Sprite_20260808_213435.png");
            if (qiBladeIcon == null)
                qiBladeIcon = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/TJGenerators/History/Sprite_20260812_163613.png");
            if (talismanIcon == null)
                talismanIcon = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/TJGenerators/History/Sprite_20260812_163612.png");
#endif
            // Find or create UI
            if (transform.Find("SkillToggleButton") == null)
            {
                CreateToggleButton();
                CreatePanel();
                _panel.SetActive(false);
            }
            else
            {
                _toggleButton = transform.Find("SkillToggleButton").gameObject;
                _panel = transform.Find("SkillViewPanel")?.gameObject;
                if (_panel == null)
                {
                    CreatePanel();
                    _panel.SetActive(false);
                }
            }
        }

        private void CreateToggleButton()
        {
            _toggleButton = new GameObject("SkillToggleButton");
            _toggleButton.transform.SetParent(transform, false);

            RectTransform rt = _toggleButton.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 0.5f);
            rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.anchoredPosition = new Vector2(-40, 0);
            rt.sizeDelta = new Vector2(80, 80);

            Image img = _toggleButton.AddComponent<Image>();
            img.sprite = closedScrollSprite;
            img.color = Color.white;
            img.preserveAspect = true;
            img.raycastTarget = true;

            Button btn = _toggleButton.AddComponent<Button>();
            btn.onClick.AddListener(TogglePanel);

            ColorBlock cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(0.9f, 1f, 0.9f);
            cb.pressedColor = new Color(0.7f, 0.85f, 0.7f);
            btn.colors = cb;
        }

        private void CreatePanel()
        {
            _panel = new GameObject("SkillViewPanel");
            _panel.transform.SetParent(transform, false);

            RectTransform rt = _panel.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 0.5f);
            rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.anchoredPosition = new Vector2(-130, 0);

            int rows = Mathf.CeilToInt((float)maxSlots / columns);
            float panelW = columns * slotSize + (columns + 1) * slotSpacing + 40f;
            float panelH = rows * slotSize + (rows + 1) * slotSpacing + 80f;
            rt.sizeDelta = new Vector2(panelW, panelH);

            Image bg = _panel.AddComponent<Image>();
            bg.sprite = panelBgSprite;
            bg.color = new Color(1f, 1f, 1f, 0.95f);
            bg.preserveAspect = false;
            bg.raycastTarget = true;

            CreateSlots();
        }

        private void CreateSlots()
        {
            int rows = Mathf.CeilToInt((float)maxSlots / columns);
            float startX = -((columns - 1) * (slotSize + slotSpacing)) * 0.5f;
            float startY = ((rows - 1) * (slotSize + slotSpacing)) * 0.5f + 30f;

            for (int i = 0; i < maxSlots; i++)
            {
                int col = i % columns;
                int row = i / columns;

                GameObject slot = new GameObject($"Slot_{i}");
                slot.transform.SetParent(_panel.transform, false);

                RectTransform srt = slot.AddComponent<RectTransform>();
                srt.anchorMin = new Vector2(0.5f, 0.5f);
                srt.anchorMax = new Vector2(0.5f, 0.5f);
                srt.pivot = new Vector2(0.5f, 0.5f);
                float x = startX + col * (slotSize + slotSpacing);
                float y = startY - row * (slotSize + slotSpacing);
                srt.anchoredPosition = new Vector2(x, y);
                srt.sizeDelta = new Vector2(slotSize, slotSize);

                Image slotBg = slot.AddComponent<Image>();
                slotBg.sprite = slotSprite;
                slotBg.color = new Color(0.9f, 0.85f, 0.7f, 0.5f);
                slotBg.preserveAspect = true;
                slotBg.raycastTarget = false;

                // Icon child
                GameObject iconObj = new GameObject("Icon");
                iconObj.transform.SetParent(slot.transform, false);
                RectTransform iconRt = iconObj.AddComponent<RectTransform>();
                iconRt.anchorMin = new Vector2(0.5f, 0.6f);
                iconRt.anchorMax = new Vector2(0.5f, 0.6f);
                iconRt.pivot = new Vector2(0.5f, 0.5f);
                iconRt.sizeDelta = new Vector2(slotSize * 0.6f, slotSize * 0.6f);
                Image iconImg = iconObj.AddComponent<Image>();
                iconImg.color = Color.white;
                iconImg.preserveAspect = true;
                iconImg.raycastTarget = false;

                // Name label
                GameObject nameObj = new GameObject("Name");
                nameObj.transform.SetParent(slot.transform, false);
                RectTransform nameRt = nameObj.AddComponent<RectTransform>();
                nameRt.anchorMin = new Vector2(0.5f, 0.2f);
                nameRt.anchorMax = new Vector2(0.5f, 0.2f);
                nameRt.pivot = new Vector2(0.5f, 0.5f);
                nameRt.sizeDelta = new Vector2(slotSize, 30f);
                TMP_Text nameText = nameObj.AddComponent<TextMeshProUGUI>();
                nameText.fontSize = 18;
                nameText.alignment = TextAlignmentOptions.Center;
                nameText.color = new Color(0.2f, 0.35f, 0.3f);
                nameText.text = "空";

                // Level label
                GameObject lvlObj = new GameObject("Level");
                lvlObj.transform.SetParent(slot.transform, false);
                RectTransform lvlRt = lvlObj.AddComponent<RectTransform>();
                lvlRt.anchorMin = new Vector2(1f, 1f);
                lvlRt.anchorMax = new Vector2(1f, 1f);
                lvlRt.pivot = new Vector2(1f, 1f);
                lvlRt.sizeDelta = new Vector2(40, 30);
                lvlRt.anchoredPosition = new Vector2(-5, -5);
                TMP_Text lvlText = lvlObj.AddComponent<TextMeshProUGUI>();
                lvlText.fontSize = 16;
                lvlText.alignment = TextAlignmentOptions.Center;
                lvlText.color = new Color(0.8f, 0.65f, 0.2f);
                lvlText.text = "";
            }
        }

        public void TogglePanel()
        {
            _isOpen = !_isOpen;
            _panel.SetActive(_isOpen);

            // Swap button icon
            var btnImg = _toggleButton.GetComponent<Image>();
            btnImg.sprite = _isOpen ? (openScrollSprite != null ? openScrollSprite : closedScrollSprite) : closedScrollSprite;

            if (_isOpen) RefreshSlots();
        }

        private void RefreshSlots()
        {
            GameObject player = GameObject.Find("Player");
            if (player == null) return;

            var skillInfos = new System.Collections.Generic.List<SkillInfo>();

            // Detect FlyingSword
            var sword = player.GetComponent<FlyingSword>();
            if (sword == null) sword = player.GetComponentInChildren<FlyingSword>();
            if (sword != null && sword.gameObject.activeInHierarchy)
            {
                skillInfos.Add(new SkillInfo
                {
                    name = "御剑术",
                    icon = swordIcon,
                    level = sword.skillLevel,
                    element = sword.element
                });
            }

            // Detect FireballSpell
            var fireball = player.GetComponent<FireballSpell>();
            if (fireball != null && fireball.enabled)
            {
                skillInfos.Add(new SkillInfo
                {
                    name = "火球术",
                    icon = fireballIcon,
                    level = 1,
                    element = fireball.element,
                    iconScale = 1.5f
                });
            }

            // Detect SpiritOrbSpell
            var orb = player.GetComponent<SpiritOrbSpell>();
            if (orb != null && orb.enabled)
            {
                skillInfos.Add(new SkillInfo
                {
                    name = "灵力弹",
                    icon = spiritOrbIcon,
                    level = orb.skillLevel,
                    element = orb.element
                });
            }

            // Detect SpiritArraySpell
            var array = player.GetComponent<SpiritArraySpell>();
            if (array != null && array.enabled)
            {
                skillInfos.Add(new SkillInfo
                {
                    name = "灵力阵",
                    icon = arrayIcon,
                    level = 1,
                    element = Element.None
                });
            }

            // Detect QiBladeSpell
            var qiBlade = player.GetComponent<QiBladeSpell>();
            if (qiBlade != null && qiBlade.enabled)
            {
                Element qEl = SpiritRootManager.GetEffectiveElement(qiBlade.element);
                skillInfos.Add(new SkillInfo
                {
                    name = "灵气刃",
                    icon = qiBladeIcon,
                    level = qiBlade.skillLevel,
                    element = qEl
                });
            }

            // Detect TalismanSpell
            var talisman = player.GetComponent<TalismanSpell>();
            if (talisman != null && talisman.enabled)
            {
                Element tEl = SpiritRootManager.GetEffectiveElement(talisman.element);
                skillInfos.Add(new SkillInfo
                {
                    name = "符箓术",
                    icon = talismanIcon,
                    level = talisman.skillLevel,
                    element = tEl
                });
            }

            // Fill slots — empty slots hide icon completely
            for (int i = 0; i < maxSlots; i++)
            {
                Transform slot = _panel.transform.Find($"Slot_{i}");
                if (slot == null) continue;

                var iconImg = slot.Find("Icon")?.GetComponent<Image>();
                var nameText = slot.Find("Name")?.GetComponent<TMP_Text>();
                var lvlText = slot.Find("Level")?.GetComponent<TMP_Text>();
                var slotBg = slot.GetComponent<Image>();

                if (i < skillInfos.Count)
                {
                    var info = skillInfos[i];
                    if (iconImg != null) {
                        if (info.icon != null) {
                            iconImg.sprite = info.icon;
                            iconImg.color = Color.white;
                            // Apply per-skill icon scale
                            float scale = info.iconScale > 0 ? info.iconScale : 1f;
                            var iconRt = iconImg.rectTransform;
                            iconRt.sizeDelta = new Vector2(slotSize * 0.6f * scale, slotSize * 0.6f * scale);
                        } else {
                            iconImg.sprite = null;
                            iconImg.color = new Color(1, 1, 1, 0); // 无图标则隐藏
                        }
                    }
                    if (nameText != null) nameText.text = info.name;
                    if (lvlText != null) lvlText.text = $"Lv{info.level}";

                    Color elemColor = DamageNumber.GetElementColor(info.element);
                    if (slotBg != null) slotBg.color = new Color(elemColor.r, elemColor.g, elemColor.b, 0.3f);
                }
                else
                {
                    // 空格子 — 隐藏icon，显示空
                    if (iconImg != null) { iconImg.sprite = null; iconImg.color = new Color(1,1,1,0); }
                    if (nameText != null) nameText.text = "空";
                    if (lvlText != null) lvlText.text = "";
                    if (slotBg != null) slotBg.color = new Color(0.9f, 0.85f, 0.7f, 0.3f);
                }
            }
        }

        private void Update()
        {
            if (_isOpen) RefreshSlots();
        }

        private struct SkillInfo
        {
            public string name;
            public Sprite icon;
            public int level;
            public Element element;
            public float iconScale;
        }
    }
}