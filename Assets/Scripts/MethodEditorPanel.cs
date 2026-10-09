using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;

namespace BiopunkRV
{
    /// <summary>
    /// 功法面板 — 大厅导航"功法"页 / 秘境F2。
    /// 书架视图: 5本基础功法书平铺(书下有功法名), 点击书进入编辑视图。
    /// 编辑视图: 术法/灵根循环选择 + 5词缀槽(点击弹出选择器) + 启用/保存。
    /// 每本功法有独立可编辑副本(JSON存档), 设计的词缀按槽位顺序出现在局内升级选项中。
    /// </summary>
    public class MethodEditorPanel : MonoBehaviour
    {
        [Header("外部容器 (大厅GongFaPanel); 为空=秘境F2自面板模式")]
        public GameObject externalContainer;

        private GameObject _panel;
        private GameObject _shelfView;
        private GameObject _editorView;
        private Image _bgImage;
        private Sprite _shelfSprite;
        private bool _isOpen;
        private Sprite _cardSprite;
        private Sprite _buttonSprite;
        private Sprite _slotBarSprite;
        private readonly List<GameObject> _bookVisuals = new List<GameObject>();
        private TMPro.TextMeshProUGUI _pickerSubtitle;

        private static readonly Color TextDark = new Color(0.32f, 0.19f, 0.10f);
        private static readonly Color TextTeal = new Color(0.13f, 0.35f, 0.31f);
        private static readonly Color TextGold = new Color(0.55f, 0.42f, 0.16f);
        private static readonly Color TextCream = new Color(0.97f, 0.94f, 0.86f);
        private static readonly Color StatusGreen = new Color(0.15f, 0.45f, 0.25f);
        private readonly List< TMPro.TextMeshProUGUI> _labels = new List<TMPro.TextMeshProUGUI>();
        private readonly List<TMPro.TextMeshProUGUI> _slotLabels = new List<TMPro.TextMeshProUGUI>();
        private TMPro.TextMeshProUGUI _nameLabel;
        private TMPro.TextMeshProUGUI _descLabel;
        private TMPro.TextMeshProUGUI _statusLabel;
        private Image _iconImage;
        private CultivationMethod _editing; // 面板当前展示的功法实例

        // 词缀选择器
        private GameObject _picker;
        private GameObject _pickerScrim;
        private ScrollRect _pickerScroll;
        private RectTransform _pickerContent;
        private TMPro.TextMeshProUGUI _pickerTitle;
        private int _pickerSlot = -1;

        private static readonly Element[] EditableRoots = { Element.None, Element.Fire, Element.Water, Element.Earth, Element.Wind, Element.Thunder };

        private void Awake()
        {
            if (MethodEditor.Presets.Count == 0) MethodEditor.LoadAll();
            BuildUI();
        }

        private void Start()
        {
            // 秘境模式: 进入秘境时自动应用大厅里启用的功法
            // PlayerStatsComponent applies the saved loadout after all Awake methods finish.
        }

        private void OnEnable()
        {
            // 大厅导航打开功法页时回到书架
            if (externalContainer != null && _panel != null) ShowShelf();
        }

        private void Update()
        {
            if (externalContainer == null && Input.GetKeyDown(KeyCode.F2)) Toggle();
        }

        private void Toggle()
        {
            _isOpen = !_isOpen;
            if (!_isOpen) SetPickerVisible(false);
            _panel.SetActive(_isOpen);
            if (_isOpen) ShowShelf();
        }

        private void BuildUI()
        {
            Transform parent;
            if (externalContainer != null)
            {
                // 大厅模式: 构建进GongFaPanel容器, 由导航栏控制显隐
                parent = externalContainer.transform;
            }
            else
            {
                Canvas canvas = FindObjectOfType<Canvas>();
                if (canvas == null) { Debug.LogWarning("[MethodPanel] no canvas"); return; }
                parent = canvas.transform;
            }

            _panel = new GameObject("MethodEditorPanel");
            _panel.transform.SetParent(parent, false);
            var prt = _panel.AddComponent<RectTransform>();
            prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
            prt.sizeDelta = new Vector2(980, 640);

            _bgImage = _panel.AddComponent<Image>();
            _bgImage.color = new Color(0.12f, 0.1f, 0.18f, 0.96f);
            // 大厅模式由容器(GongFaPanel)控制显隐, 自身常显; 秘境模式F2开关
            _panel.SetActive(externalContainer != null ? true : false);

            _cardSprite = Resources.Load<Sprite>("UI/GongFa/GongFaPanelCard");
            _buttonSprite = Resources.Load<Sprite>("UI/GongFa/GongFaButton");
            _slotBarSprite = Resources.Load<Sprite>("UI/GongFa/GongFaSlotBar");
#if UNITY_EDITOR
            _shelfSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/TJGenerators/History/GongFaShelf.png");
            if (_cardSprite == null) _cardSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/UI/GongFa/GongFaPanelCard.png");
            if (_buttonSprite == null) _buttonSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/UI/GongFa/GongFaButton.png");
            if (_slotBarSprite == null) _slotBarSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/UI/GongFa/GongFaSlotBar.png");
#endif
            if (externalContainer != null)
            {
                foreach (Transform child in externalContainer.transform)
                    if (child.name.StartsWith("BookIcon_base_"))
                        _bookVisuals.Add(child.gameObject);
            }

            BuildShelfView();
            BuildEditorView();
            BuildPicker();
            ShowShelf();
        }

        // ── 书架视图: 书和功法名都在Paper美术资产(GongFaShelf.png)里, 代码只放透明热区 ──
        private void BuildShelfView()
        {
            _shelfView = new GameObject("ShelfView");
            _shelfView.transform.SetParent(_panel.transform, false);
            var rt = _shelfView.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            // 与功法页5个BookIcon的位置一一对应 (用户选定布局: 左上起一排, y=273, 间距280)
            string[] order = { "base_spiritorb", "base_flyingsword", "base_spiritarray", "base_qiblade", "base_talisman" };
            float[] centersX = { -650f, -370f, -90f, 190f, 470f };
            for (int i = 0; i < order.Length; i++)
            {
                var m = MethodEditor.Presets.Find(p => p.methodId == order[i]);
                if (m == null) continue;
                MakeHotspot(_shelfView.transform, m, new Vector2(centersX[i], 273f), new Vector2(300, 250));
            }
        }

        /// <summary>透明点击热区 (不可见, 覆盖在美术资产的书上)</summary>
        private void MakeHotspot(Transform parent, CultivationMethod m, Vector2 pos, Vector2 size)
        {
            var go = new GameObject("Hotspot_" + m.methodId);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var img = go.AddComponent<Image>();
            img.color = new Color(1, 1, 1, 0); // 全透明但接收点击
            var btn = go.AddComponent<Button>();
            var captured = m;
            Transform book = externalContainer != null ? externalContainer.transform.Find("BookIcon_" + m.methodId) : null;
            btn.onClick.AddListener(() => ClickBook(captured, book, pos));
        }

        // ── 书点击反馈: 金色光晕 + 书体回弹, 播完再进编辑器 ──
        private bool _bookClickLock;
        private static Sprite _glowSprite;

        private void ClickBook(CultivationMethod m, Transform book, Vector2 pos)
        {
            if (_bookClickLock) return;
            _bookClickLock = true;
            SpawnGlow(pos);
            StartCoroutine(BookClickRoutine(m, book));
        }

        private System.Collections.IEnumerator BookClickRoutine(CultivationMethod m, Transform book)
        {
            Vector3 baseScale = book != null ? book.localScale : Vector3.one;
            float t = 0f;
            const float dur = 0.18f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / dur);
                float s = k < 0.5f ? Mathf.Lerp(1f, 1.12f, k * 2f) : Mathf.Lerp(1.12f, 1f, (k - 0.5f) * 2f);
                if (book != null) book.localScale = baseScale * s;
                yield return null;
            }
            if (book != null) book.localScale = baseScale;
            _bookClickLock = false;
            EnterEditor(m);
        }

        private void SpawnGlow(Vector2 pos)
        {
            var go = new GameObject("BookClickGlow");
            go.transform.SetParent(_panel.transform, false);
            go.transform.SetAsLastSibling();
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(280, 280);
            var img = go.AddComponent<Image>();
            img.sprite = GetGlowSprite();
            img.raycastTarget = false;
            StartCoroutine(GlowRoutine(go, img, rt));
        }

        private System.Collections.IEnumerator GlowRoutine(GameObject go, Image img, RectTransform rt)
        {
            float t = 0f;
            const float dur = 0.35f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / dur);
                rt.localScale = Vector3.one * Mathf.Lerp(0.5f, 1.5f, k);
                img.color = new Color(1f, 0.85f, 0.4f, Mathf.Lerp(0.9f, 0f, k));
                yield return null;
            }
            if (go != null) Destroy(go);
        }

        private static Sprite GetGlowSprite()
        {
            if (_glowSprite != null) return _glowSprite;
            const int S = 128;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
            var px = new Color32[S * S];
            for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float dx = (x - S / 2f) / (S / 2f), dy = (y - S / 2f) / (S / 2f);
                float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                px[y * S + x] = new Color(1f, 0.85f, 0.4f, a * a); // 柔和径向衰减
            }
            tex.SetPixels32(px);
            tex.Apply();
            _glowSprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
            return _glowSprite;
        }

        // ── 编辑视图: 左卡功法信息 + 右卡词缀配置 (纸面雕花卡片, 与书架同画风) ──
        private void BuildEditorView()
        {
            _editorView = new GameObject("EditorView");
            _editorView.transform.SetParent(_panel.transform, false);
            var ert = _editorView.AddComponent<RectTransform>();
            ert.anchorMin = Vector2.zero; ert.anchorMax = Vector2.one;
            ert.offsetMin = ert.offsetMax = Vector2.zero;
            Transform parent = _editorView.transform;

            // 返回按钮在纸面左上角 (卡片外)
            MakeButton(parent, "Back", "◀ 返回", new Vector2(-680, 395), new Vector2(200, 56), () => ShowShelf());

            // ── 左卡: 功法信息 ──
            MakeCard(parent, new Vector2(-380, 30));
            _nameLabel = MakeLabel(parent, "", 28, new Vector2(-379, 307), new Vector2(400, 40), TextCream);
            var iconGo = new GameObject("MethodIcon");
            iconGo.transform.SetParent(parent, false);
            var iconRt = iconGo.AddComponent<RectTransform>();
            iconRt.anchorMin = iconRt.anchorMax = new Vector2(0.5f, 0.5f);
            iconRt.anchoredPosition = new Vector2(-380, 225);
            iconRt.sizeDelta = new Vector2(80, 80);
            _iconImage = iconGo.AddComponent<Image>();
            _iconImage.preserveAspect = true;
            _descLabel = MakeLabel(parent, "", 18, new Vector2(-380, 150), new Vector2(620, 60), TextDark);
            _descLabel.enableWordWrapping = true;

            MakeCycleRow(parent, "术法", 95f, CycleSkill, GetSkillText);
            MakeCycleRow(parent, "灵根", 35f, CycleRoot, GetRootText);

            MakeButton(parent, "Apply", "启用功法", new Vector2(-500, -235), new Vector2(200, 56),
                () => ApplyEditing());
            MakeButton(parent, "Save", "保存副本", new Vector2(-260, -235), new Vector2(200, 56),
                () => { MethodEditor.SaveCustomCopy(_editing); SetStatus("功法副本已保存"); });
            _statusLabel = MakeLabel(parent, "", 15, new Vector2(-380, -285), new Vector2(620, 24), StatusGreen);

            // ── 右卡: 词缀配置 ──
            MakeCard(parent, new Vector2(380, 30));
            MakeLabel(parent, "词缀配置", 28, new Vector2(381, 307), new Vector2(400, 40), TextCream);
            for (int i = 0; i < 5; i++)
            {
                int slot = i;
                float y = 195f - i * 80f;
                var cap = MakeLabel(parent, $"词缀槽{i + 1} (Lv{AffixSystem.UnlockLevels[i]})", 16, new Vector2(250, y + 40), new Vector2(300, 26), TextTeal);
                cap.alignment = TMPro.TextAlignmentOptions.Left;
                var bar = MakeBar(parent, new Vector2(380, y), new Vector2(600, 56), () => OpenAffixPicker(slot));
                var slotText = MakeBarText(bar, "-", 20, TextDark);
                _slotLabels.Add(slotText);
            }
            MakeLabel(parent, "词缀将按槽位顺序出现在局内升级选项中，选取后解锁", 15,
                new Vector2(380, -195), new Vector2(620, 26), TextGold);
        }

        /// <summary>书架书封显隐 (编辑视图下隐藏, 避免与编辑卡片叠乱)</summary>
        private void SetBooksVisible(bool visible)
        {
            foreach (var b in _bookVisuals)
                if (b != null) b.SetActive(visible);
        }

        private void ShowShelf()
        {
            SetPickerVisible(false);
            if (_editorView != null) _editorView.SetActive(false);
            if (_shelfView != null) _shelfView.SetActive(true);
            SetBooksVisible(true);
            // 大厅: 背景即Paper美术资产的书架图, 代码底板隐藏; 秘境F2: 底板直接显示书架图
            if (_bgImage != null)
            {
                if (externalContainer != null)
                {
                    _bgImage.enabled = false;
                }
                else if (_shelfSprite != null)
                {
                    _bgImage.enabled = true;
                    _bgImage.sprite = _shelfSprite;
                    _bgImage.color = Color.white;
                    _bgImage.preserveAspect = true;
                }
            }
        }

        /// <summary>点击功法书进入编辑 (每本功法独立可编辑副本, 自动从预设克隆)</summary>
        private void EnterEditor(CultivationMethod m)
        {
            _editing = MethodEditor.GetWorkingCopy(m);
            MethodEditor.Selected = m;
            SetBooksVisible(false);
            _shelfView.SetActive(false);
            _editorView.SetActive(true);
            // 大厅: 编辑卡片直接落在Paper上, 底板隐藏; 秘境F2: 加暗色衬底保证可读
            if (_bgImage != null)
            {
                if (externalContainer != null)
                {
                    _bgImage.enabled = false;
                }
                else
                {
                    _bgImage.enabled = true;
                    _bgImage.sprite = null;
                    _bgImage.color = new Color(0.1f, 0.08f, 0.15f, 0.85f);
                    _bgImage.preserveAspect = false;
                }
            }
            RefreshAll();
        }

        /// <summary>启用当前功法: 存档副本; 秘境里立即应用; 大厅里记录为当前功法, 进入秘境时自动应用</summary>
        private void ApplyEditing()
        {
            if (_editing == null) return;
            if (!MetaProgression.IsMethodUnlocked(_editing.methodId))
            {
                SetStatus("请先在修仙养成 · 藏经阁解锁该功法");
                return;
            }
            if (!MetaProgression.EquipMethod(_editing.methodId, 0))
            {
                SetStatus("辅修或禁术已有相同基础术法，请先调整携带功法");
                return;
            }
            MethodEditor.SaveCustomCopy(_editing); // 启用即存档
            var p = GameObject.Find("Player");
            if (p != null) MethodRuntime.ApplySavedLoadout(p);
            PlayerPrefs.SetString("ActiveMethodId", _editing.methodId);
            PlayerPrefs.Save();
            SetStatus($"「{_editing.displayName}」已启用" + (p == null ? "，进入秘境后自动生效" : ""));
        }

        private void SetStatus(string msg)
        {
            if (_statusLabel != null) _statusLabel.text = msg;
            Debug.Log("[MethodPanel] " + msg);
        }

        /// <summary>秘境入口: 自动应用大厅里启用的功法 (优先副本)</summary>
        // ── 循环行: [字段名] [◀] [当前值条] [▶] ──
        private void MakeCycleRow(Transform parent, string fieldName, float y, System.Action<int> cycle, System.Func<string> getText)
        {
            var fieldLabel = MakeLabel(parent, fieldName, 20, new Vector2(-570, y), new Vector2(110, 36), TextTeal);
            fieldLabel.alignment = TMPro.TextAlignmentOptions.Right;
            MakeArrow(parent, "Prev_" + fieldName, "◀", new Vector2(-490, y), () => { cycle(-1); RefreshAll(); });
            var bar = MakeBar(parent, new Vector2(-295, y), new Vector2(300, 44), null);
            var valueLabel = MakeBarText(bar, "", 20, TextDark);
            MakeArrow(parent, "Next_" + fieldName, "▶", new Vector2(-110, y), () => { cycle(1); RefreshAll(); });
            _labels.Add(valueLabel);
            _cycleGetters.Add(getText);
        }

        private readonly List<System.Func<string>> _cycleGetters = new List<System.Func<string>>();

        // ── 循环逻辑 ──
        private static readonly string[] SkillIds = { "", "SpiritOrb", "Fireball", "QiBlade", "Talisman", "SpiritArray", "FlyingSword" };

        private void CycleSkill(int dir)
        {
            if (_editing == null) return;
            int idx = System.Array.IndexOf(SkillIds, _editing.baseSkillId);
            idx = (idx + dir + SkillIds.Length) % SkillIds.Length;
            _editing.baseSkillId = SkillIds[idx];
        }
        private string GetSkillText()
        {
            if (_editing == null) return "-";
            switch (_editing.baseSkillId)
            {
                case "SpiritOrb": return "灵球术";
                case "Fireball": return "火球术";
                case "QiBlade": return "灵气刃";
                case "Talisman": return "符箓术";
                case "SpiritArray": return "灵力阵";
                case "FlyingSword": return "御剑术";
                default: return "默认配置";
            }
        }

        private void CycleRoot(int dir)
        {
            if (_editing == null) return;
            int idx = System.Array.IndexOf(EditableRoots, _editing.spiritRoot);
            idx = (idx + dir + EditableRoots.Length) % EditableRoots.Length;
            _editing.spiritRoot = EditableRoots[idx];
            _editing.spiritRootLevel = _editing.spiritRoot == Element.None ? 0 : 1;
        }
        private string GetRootText()
        {
            if (_editing == null) return "-";
            return _editing.spiritRoot == Element.None ? "无灵根" : ElementColors.GetName(_editing.spiritRoot) + "灵根 Lv" + _editing.spiritRootLevel;
        }

        // ── 词缀选择器 ──

        private void OpenAffixPicker(int slot)
        {
            if (_editing == null || _picker == null) return;
            _pickerSlot = slot;
            RefreshPicker();
            SetPickerVisible(true);
        }

        private void SetPickerVisible(bool visible)
        {
            if (_pickerScrim != null) _pickerScrim.SetActive(visible);
            if (_picker != null) _picker.SetActive(visible);
        }

        private void BuildPicker()
        {
            // 模态衬底: 盖住编辑卡片的暗色层, 阻挡点击穿透 (先创建, 渲染在弹窗之下)
            _pickerScrim = new GameObject("AffixPickerScrim");
            _pickerScrim.transform.SetParent(_panel.transform, false);
            var srt = _pickerScrim.AddComponent<RectTransform>();
            srt.anchorMin = srt.anchorMax = new Vector2(0.5f, 0.5f);
            srt.sizeDelta = new Vector2(2400, 1600);
            var simg = _pickerScrim.AddComponent<Image>();
            simg.color = new Color(0f, 0f, 0f, 0.5f);
            _pickerScrim.SetActive(false);

            _picker = new GameObject("AffixPicker");
            _picker.transform.SetParent(_panel.transform, false);
            var rt = _picker.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0, 30);
            rt.sizeDelta = new Vector2(700, 657);
            var bg = _picker.AddComponent<Image>();
            bg.sprite = _cardSprite;
            bg.type = Image.Type.Simple;
            bg.preserveAspect = true;

            _pickerTitle = MakeLabel(_picker.transform, "选择词缀", 28, new Vector2(1, 277), new Vector2(400, 40), TextCream);
            _pickerSubtitle = MakeLabel(_picker.transform, "", 16, new Vector2(0, 238), new Vector2(560, 26), TextTeal);
            MakeButton(_picker.transform, "ClosePicker", "关闭", new Vector2(0, -240), new Vector2(200, 56), () => SetPickerVisible(false));

            // 滚动视口
            var viewport = new GameObject("Viewport");
            viewport.transform.SetParent(_picker.transform, false);
            var vrt = viewport.AddComponent<RectTransform>();
            vrt.anchorMin = vrt.anchorMax = new Vector2(0.5f, 0.5f);
            vrt.anchoredPosition = new Vector2(0, -5);
            vrt.sizeDelta = new Vector2(620, 420);
            var vimg = viewport.AddComponent<Image>();
            vimg.color = new Color(0.5f, 0.45f, 0.3f, 0.08f);
            viewport.AddComponent<RectMask2D>();

            var content = new GameObject("Content");
            content.transform.SetParent(viewport.transform, false);
            _pickerContent = content.AddComponent<RectTransform>();
            _pickerContent.anchorMin = new Vector2(0f, 1f);
            _pickerContent.anchorMax = new Vector2(1f, 1f);
            _pickerContent.pivot = new Vector2(0.5f, 1f);
            _pickerContent.anchoredPosition = Vector2.zero;

            _pickerScroll = _picker.AddComponent<ScrollRect>();
            _pickerScroll.viewport = vrt;
            _pickerScroll.content = _pickerContent;
            _pickerScroll.horizontal = false;
            _pickerScroll.movementType = ScrollRect.MovementType.Clamped;

            _picker.SetActive(false);
        }

        private void RefreshPicker()
        {
            if (_pickerSlot < 0 || _editing == null) return;
            _pickerTitle.text = "选择词缀";
            _pickerSubtitle.text = $"槽位{_pickerSlot + 1} (Lv{AffixSystem.UnlockLevels[_pickerSlot]})"
                + (_editing.isPreset ? " · 预设将被复制后编辑" : "");
            foreach (Transform c in _pickerContent) Object.Destroy(c.gameObject);

            float y = -6f;
            void Section(string title)
            {
                var lbl = MakeLabel(_pickerContent, title, 20, Vector2.zero, new Vector2(300, 30), new Color(0.95f, 0.85f, 0.4f));
                lbl.alignment = TMPro.TextAlignmentOptions.Left;
                AnchorTopCenter(lbl.transform, new Vector2(-340, y));
                y -= 36f;
            }
            void Entry(AffixType a)
            {
                bool cur = _editing.affixes[_pickerSlot] == a;
                var captured = a;
                MakePickerEntry(_pickerContent, captured, cur, y, () =>
                {
                    _editing.affixes[_pickerSlot] = captured;
                    RefreshAll();
                    SetPickerVisible(false);
                });
                y -= 46f;
            }

            Section("通用词缀");
            for (AffixType t = AffixType.Split; t <= AffixType.Fall; t++)
            {
                if (t == AffixType.Track) continue; // 追踪词缀已移除
                Entry(t);
            }
            var spec = SkillAffixesFor(_editing.baseSkillId);
            if (spec.Length > 0)
            {
                Section("术法专属 · " + GetSkillText());
                foreach (var t in spec) Entry(t);
            }
            var roots = RootAffixesFor(_editing.spiritRoot);
            if (roots.Length > 0)
            {
                Section(ElementColors.GetName(_editing.spiritRoot) + "灵根词缀");
                foreach (var t in roots) Entry(t);
            }

            _pickerContent.sizeDelta = new Vector2(0f, -y + 10f);
            _pickerScroll.verticalNormalizedPosition = 1f;
        }

        /// <summary>词缀条目按钮: 名称+效果说明, 当前槽位词缀金色高亮, 其他槽已用的标注</summary>
        private void MakePickerEntry(Transform parent, AffixType affix, bool isCurrent, float topY, System.Action onClick)
        {
            var go = new GameObject("Entry_" + affix);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, topY);
            rt.sizeDelta = new Vector2(600, 46);
            var img = go.AddComponent<Image>();
            img.sprite = _slotBarSprite;
            img.type = Image.Type.Simple;
            img.preserveAspect = false;
            img.color = isCurrent ? new Color(1f, 0.9f, 0.6f) : Color.white;
            var btn = go.AddComponent<Button>();
            btn.onClick.AddListener(() => onClick());

            // 其他槽位已使用同一词缀时标注
            string usedMark = "";
            if (_editing != null && !isCurrent)
                for (int i = 0; i < 5; i++)
                    if (_editing.affixes[i] == affix) { usedMark = $" <color=#8A6D2B>（已装在槽位{i + 1}）</color>"; break; }

            var tgo = new GameObject("T");
            tgo.transform.SetParent(go.transform, false);
            var trt = tgo.AddComponent<RectTransform>();
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(70, 0); trt.offsetMax = new Vector2(-8, 0);
            var tmp = tgo.AddComponent<TMPro.TextMeshProUGUI>();
            tmp.text = (isCurrent ? "▶ " : "") + $"<b>{AffixSystem.GetAffixName(affix)}</b>{usedMark}  <color=#5C6B66>{AffixSystem.GetAffixDesc(affix)}</color>";
            tmp.fontSize = 19;
            tmp.alignment = TMPro.TextAlignmentOptions.Left;
            tmp.color = TextDark;
            tmp.raycastTarget = false;
        }

        private static void AnchorTopCenter(Transform t, Vector2 pos)
        {
            var rt = (RectTransform)t;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = pos;
        }

        /// <summary>术法专属词缀按当前术法过滤</summary>
        private static AffixType[] SkillAffixesFor(string skillId)
        {
            switch (skillId)
            {
                case "FlyingSword": return new[] { AffixType.SwordArray, AffixType.SwordQi };
                case "SpiritOrb":
                case "Fireball": return new[] { AffixType.OrbCharge, AffixType.OrbBurst };
                case "SpiritArray": return new[] { AffixType.ArrayExpand, AffixType.ArrayStack };
                case "QiBlade": return new[] { AffixType.BladeSlash, AffixType.BladeWind };
                case "Talisman": return new[] { AffixType.TalismanChain, AffixType.TalismanScatter };
                default: return new AffixType[0];
            }
        }

        /// <summary>灵根词缀按当前灵根过滤 (无灵根不显示)</summary>
        private static AffixType[] RootAffixesFor(Element root)
        {
            switch (root)
            {
                case Element.Fire: return new[] { AffixType.BurnEnhance, AffixType.BurnExplode, AffixType.BurnSpread };
                case Element.Water: return new[] { AffixType.SlowEnhance, AffixType.Ripple, AffixType.Freeze };
                case Element.Earth: return new[] { AffixType.Quake, AffixType.ShieldBody, AffixType.KnockbackEnhance };
                case Element.Wind: return new[] { AffixType.Haste, AffixType.ProjectileSpeed, AffixType.WindBlade };
                case Element.Thunder: return new[] { AffixType.Chain, AffixType.ThunderCrit, AffixType.Paralyze };
                default: return new AffixType[0];
            }
        }

        private void RefreshAll()
        {
            if (_editing == null) _editing = MethodEditor.Selected;
            if (_editing == null) return;
            _nameLabel.text = _editing.displayName;
            _descLabel.text = _editing.description + (_editing.isPreset ? "  (预设·只读)" : "");
            if (_iconImage != null)
            {
                _iconImage.sprite = _editing.icon;
                _iconImage.enabled = _editing.icon != null;
            }
            for (int i = 0; i < _labels.Count && i < _cycleGetters.Count; i++)
                _labels[i].text = _cycleGetters[i]();
            for (int i = 0; i < _slotLabels.Count; i++)
                _slotLabels[i].text = AffixSystem.GetAffixName(_editing.affixes[i]);
            if (_picker != null && _picker.activeSelf) RefreshPicker();
        }

        // ── UI工厂 ──
        private TMPro.TextMeshProUGUI MakeLabel(Transform parent, string text, float size, Vector2 pos, Vector2 sz, Color color)
        {
            var go = new GameObject("Lbl_" + text.Substring(0, Mathf.Min(8, text.Length)));
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = sz;
            var tmp = go.AddComponent<TMPro.TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.alignment = TMPro.TextAlignmentOptions.Center;
            tmp.color = color;
            return tmp;
        }

        private TMPro.TextMeshProUGUI MakeButton(Transform parent, string name, string text, Vector2 pos, Vector2 sz, System.Action onClick)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = sz;
            var img = go.AddComponent<Image>();
            img.sprite = _buttonSprite;
            img.type = Image.Type.Simple;
            img.preserveAspect = false;
            var btn = go.AddComponent<Button>();
            btn.onClick.AddListener(() => onClick());

            var tgo = new GameObject("T");
            tgo.transform.SetParent(go.transform, false);
            var trt = tgo.AddComponent<RectTransform>();
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
            var tmp = tgo.AddComponent<TMPro.TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = sz.y > 50 ? 22 : 18;
            tmp.alignment = TMPro.TextAlignmentOptions.Center;
            tmp.color = TextCream;
            tmp.raycastTarget = false;
            return tmp;
        }

        /// <summary>雕花纸面卡片 (固定尺寸, Simple不切片防角花变形)</summary>
        private Image MakeCard(Transform parent, Vector2 pos)
        {
            var go = new GameObject("Card");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(700, 657);
            var img = go.AddComponent<Image>();
            img.sprite = _cardSprite;
            img.type = Image.Type.Simple;
            img.preserveAspect = true;
            return img;
        }

        /// <summary>纸面槽条 (左端带金色小菱形点缀), 可传点击</summary>
        private GameObject MakeBar(Transform parent, Vector2 pos, Vector2 sz, System.Action onClick)
        {
            var go = new GameObject("Bar");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = sz;
            var img = go.AddComponent<Image>();
            img.sprite = _slotBarSprite;
            img.type = Image.Type.Simple;
            img.preserveAspect = false;
            if (onClick != null)
            {
                var btn = go.AddComponent<Button>();
                btn.onClick.AddListener(() => onClick());
            }
            return go;
        }

        /// <summary>槽条内嵌文字 (左侧让开金牌匾)</summary>
        private TMPro.TextMeshProUGUI MakeBarText(GameObject bar, string text, float fontSize, Color color)
        {
            var tgo = new GameObject("T");
            tgo.transform.SetParent(bar.transform, false);
            var trt = tgo.AddComponent<RectTransform>();
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(60, 2); trt.offsetMax = new Vector2(-10, -2);
            var tmp = tgo.AddComponent<TMPro.TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = TMPro.TextAlignmentOptions.Center;
            tmp.color = color;
            tmp.raycastTarget = false;
            return tmp;
        }

        /// <summary>纯文字箭头按钮 (小尺寸不适用玉按钮, 用黛青文字)</summary>
        private void MakeArrow(Transform parent, string name, string text, Vector2 pos, System.Action onClick)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(44, 44);
            var img = go.AddComponent<Image>();
            img.color = new Color(1, 1, 1, 0);
            var btn = go.AddComponent<Button>();
            btn.onClick.AddListener(() => onClick());
            var tmp = MakeLabel(go.transform, text, 30, Vector2.zero, new Vector2(44, 44), TextTeal);
            tmp.raycastTarget = false;
        }

        private void MakeSmallButton(Transform parent, string name, string text, Vector2 pos, Vector2 sz, System.Action onClick, Sprite icon = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = sz;
            var img = go.AddComponent<Image>();
            img.color = new Color(0.25f, 0.22f, 0.35f, 1f);
            var btn = go.AddComponent<Button>();
            btn.onClick.AddListener(() => onClick());

            float textOffsetL = 0f;
            if (icon != null)
            {
                // 图标在按钮左侧, 文字右移
                var igo = new GameObject("Icon");
                igo.transform.SetParent(go.transform, false);
                var irt = igo.AddComponent<RectTransform>();
                irt.anchorMin = irt.anchorMax = new Vector2(0f, 0.5f);
                irt.pivot = new Vector2(0f, 0.5f);
                irt.anchoredPosition = new Vector2(4, 0);
                irt.sizeDelta = new Vector2(sz.y - 8, sz.y - 8);
                var iimg = igo.AddComponent<Image>();
                iimg.sprite = icon;
                iimg.preserveAspect = true;
                iimg.raycastTarget = false;
                textOffsetL = sz.y - 4;
            }

            var tgo = new GameObject("T");
            tgo.transform.SetParent(go.transform, false);
            var trt = tgo.AddComponent<RectTransform>();
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(textOffsetL, 0); trt.offsetMax = Vector2.zero;
            var tmp = tgo.AddComponent<TMPro.TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = icon != null ? 15 : 17;
            tmp.alignment = TMPro.TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.raycastTarget = false;
        }
    }
}

