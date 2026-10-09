using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace BiopunkRV
{
    /// <summary>Runtime lobby entry, independent of scene serialization and editor tooling.</summary>
    public class CultivationLobby : MonoBehaviour
    {
        private RectTransform panel, content;
        private Text wallet, status;
        private Font font;
        private int page, slot;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            MetaProgression.EnsureLoaded();
            Time.timeScale = 1f;
            if (SceneTransitionManager.Instance == null) new GameObject("SceneTransitions").AddComponent<SceneTransitionManager>();
            if (scene.name == "大厅" && FindObjectOfType<CultivationLobby>() == null)
            {
                MetaProgression.SettleRun();
                new GameObject("CultivationLobby").AddComponent<CultivationLobby>();
            }
            if (scene.name == "秘境") RunInventory.Reset();
        }

        private void Start()
        {
            MethodEditor.LoadAll();
            // Five neutral base techniques are available from the start.
            foreach (var method in MethodEditor.Presets)
                if (method.spiritRoot == Element.None && !MetaProgression.Data.methods.Contains(method.methodId))
                    MetaProgression.Data.methods.Add(method.methodId);
            MetaProgression.Save();
            font = Resources.Load<Font>("CultivationFont") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (EventSystem.current == null) new GameObject("LobbyEvents", typeof(EventSystem), typeof(StandaloneInputModule));
            var canvasObject = new GameObject("CultivationCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 250;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.matchWidthOrHeight = .5f;
            Button(canvasObject.transform, "修仙 · 养成", new Vector2(670, 360), new Vector2(220, 60), () => Open(0));
            panel = Rect(canvasObject.transform, "修仙养成", Vector2.zero, new Vector2(1220, 730));
            panel.gameObject.AddComponent<Image>().color = new Color(.94f, .96f, .91f, .99f);
            Label(panel, "一念成仙 · 宗门修炼", new Vector2(-260, 310), new Vector2(660, 55), 32);
            Button(panel, "关闭", new Vector2(515, 310), new Vector2(120, 48), () => panel.gameObject.SetActive(false));
            wallet = Label(panel, "", new Vector2(-80, 255), new Vector2(1000, 45), 23);
            string[] tabs = { "藏经阁", "炼丹房", "宗门建设", "悟道树", "本命法宝" };
            for (int i = 0; i < tabs.Length; i++)
            {
                int captured = i;
                Button(panel, tabs[i], new Vector2(-460 + i * 230, 200), new Vector2(210, 48), () => Open(captured));
            }
            var view = Rect(panel, "Scroll", new Vector2(0, -40), new Vector2(1140, 390));
            view.gameObject.AddComponent<Image>().color = new Color(.87f, .91f, .85f);
            view.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            var scroll = view.gameObject.AddComponent<ScrollRect>();
            content = Rect(view, "Content", Vector2.zero, new Vector2(1120, 390));
            content.anchorMin = new Vector2(.5f, 1); content.anchorMax = new Vector2(.5f, 1); content.pivot = new Vector2(.5f, 1);
            content.anchoredPosition = Vector2.zero;
            scroll.viewport = view; scroll.content = content; scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 35;
            status = Label(panel, "秘境所得用于永久成长；属性在下次进入秘境时生效。", new Vector2(-70, -280), new Vector2(990, 65), 21);
            Button(panel, "进入秘境", new Vector2(425, -330), new Vector2(260, 48), () => SceneTransitionManager.Instance.EnterSecretRealm());
            Open(0);
            panel.gameObject.SetActive(false);
        }

        public void Open(int targetPage)
        {
            if (panel == null) return;
            page = targetPage;
            panel.gameObject.SetActive(true);
            Refresh();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F3)) { if (panel.gameObject.activeSelf) panel.gameObject.SetActive(false); else Open(page); }
            if (Input.GetKeyDown(KeyCode.Escape)) panel.gameObject.SetActive(false);
        }

        private void Act(Func<bool> action)
        {
            bool success = action();
            status.text = success ? "修炼成功，进度已保存；进入秘境后生效。" : "条件不足：请检查灵石、碎片、设施等级或技能重复。";
            Refresh();
        }

        private void Refresh()
        {
            foreach (Transform child in content) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            var d = MetaProgression.Data;
            wallet.text = $"灵石 {d.stones}    功法碎片 {d.methodFragments}    词缀碎片 {d.affixFragments}    境界 {RealmSystem.GetRealmName()}";
            int row = 0;
            if (page == 0)
            {
                string[] slots = { "主修 · 50灵石", "辅修 · 20灵石", "禁术 · 100灵石" };
                for (int i = 0; i < 3; i++)
                {
                    int captured = i;
                    Button(content, (slot == i ? "● " : "") + slots[i], new Vector2(-360 + i * 360, -30), new Vector2(330, 45), () => { slot = captured; Refresh(); });
                }
                row++;
                RowText(row++, "选择一个槽位后解锁并携带；也可用5功法碎片合成。相同基础术法只能携带一本。");
                string[] equipped = { d.mainMethod, d.supportMethod, d.forbiddenMethod };
                foreach (var m in MethodEditor.Presets)
                {
                    var method = m;
                    bool unlocked = MetaProgression.IsMethodUnlocked(m.methodId);
                    RowText(row, m.displayName + "  ·  " + m.baseSkillId + (Array.IndexOf(equipped, m.methodId) >= 0 ? "  [已携带]" : ""));
                    if (unlocked) RowButton(row, "携带", 350, () => Act(() => MetaProgression.EquipMethod(method.methodId, slot)));
                    else
                    {
                        RowButton(row, "灵石解锁", 280, () => Act(() => MetaProgression.UnlockMethod(method.methodId, slot, false)));
                        RowButton(row, "碎片合成", 450, () => Act(() => MetaProgression.UnlockMethod(method.methodId, slot, true)));
                    }
                    row++;
                }
            }
            else if (page == 1)
            {
                string[] gains = { "生命+10", "攻击+2", "防御+2", "速度+2", "幸运+1", "暴击+1%" };
                for (int i = 0; i < 6; i++)
                {
                    int captured = i;
                    RowText(row, $"{MetaProgression.UpgradeNames[i]} · {gains[i]}    {d.upgrades[i]}/{MetaProgression.UpgradeLimits[i]}");
                    RowButton(row++, MetaProgression.UpgradeCosts[i] + "灵石强化", 400, () => Act(() => MetaProgression.Upgrade(captured)));
                }
            }
            else if (page == 2)
            {
                string[] effects = { "解锁悟道与天赋层级", "每级拾取范围+5", "每级攻击+2" };
                for (int i = 0; i < 3; i++)
                {
                    int captured = i;
                    RowText(row, $"{MetaProgression.FacilityNames[i]} Lv{d.facilities[i]}/10 · {effects[i]}");
                    RowButton(row++, (20 * (d.facilities[i] + 1)) + "灵石建设", 400, () => Act(() => MetaProgression.ImproveFacility(captured)));
                }
                RowText(row++, "出生灵根（术法保持基础形态，灵根赋予元素）");
                Element[] roots = { Element.None, Element.Fire, Element.Water, Element.Earth, Element.Wind, Element.Thunder };
                string[] names = { "无", "火", "水", "土", "风", "雷" };
                for (int i = 0; i < roots.Length; i++)
                {
                    int captured = i;
                    RowText(row, (d.selectedRoot == (int)roots[i] ? "● " : "") + names[i] + "灵根");
                    RowButton(row++, "选择", 400, () => { d.selectedRoot = (int)roots[captured]; MetaProgression.Save(); Refresh(); });
                }
            }
            else if (page == 3)
            {
                RowText(row++, "每两级天赋需要一级悟道台；悟道解锁需悟道台Lv1、25灵石和3功法碎片。");
                for (int i = 0; i < 3; i++)
                {
                    int captured = i;
                    RowText(row, $"{MetaProgression.TalentNames[i]} Lv{d.talents[i]}/10 · 永久攻击/生命/幸运");
                    RowButton(row++, $"{10 * (d.talents[i] + 1)}灵石+{d.talents[i] + 1}词缀碎片", 370, () => Act(() => MetaProgression.ImproveTalent(captured)));
                }
                var seen = new System.Collections.Generic.HashSet<string>();
                foreach (string skill in new[] { "SpiritOrb", "FlyingSword", "QiBlade", "Talisman", "SpiritArray" })
                foreach (Element root in new[] { Element.Fire, Element.Water, Element.Earth, Element.Wind, Element.Thunder })
                foreach (string id in DaoSystem.GetAllDaoOptions(skill, root))
                {
                    if (!seen.Add(id) || id.StartsWith("Default")) continue;
                    string captured = id;
                    RowText(row, DaoSystem.GetDaoName(id));
                    if (d.daos.Contains(id)) RowText(row++, "已领悟", 370);
                    else RowButton(row++, "解锁悟道", 400, () => Act(() => MetaProgression.UnlockDao(captured)));
                }
            }
            else
            {
                RowText(row++, $"本命法宝 · {ArtifactSystem.GetTierName()} · 精炼+{ArtifactSystem.RefineLevel}/10");
                RowText(row, "升阶增加攻击、气血与防御");
                RowButton(row++, ArtifactSystem.GetUpgradeCost() + "灵石升阶", 400, () => Act(ArtifactSystem.TryUpgradeTier));
                RowText(row, "每次精炼提高法宝攻击倍率2%");
                RowButton(row++, "5灵石精炼", 400, () => Act(ArtifactSystem.TryRefine));
            }
            content.sizeDelta = new Vector2(1120, Mathf.Max(390, row * 64 + 20));
        }

        private void RowText(int row, string value, float x = -190) => Label(content, value, new Vector2(x, -32 - row * 64), new Vector2(x == -190 ? 690 : 260, 54), 20);
        private void RowButton(int row, string value, float x, Action action) => Button(content, value, new Vector2(x, -32 - row * 64), new Vector2(160, 45), action);
        private static RectTransform Rect(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>(); rt.anchorMin = rt.anchorMax = new Vector2(.5f, .5f); rt.sizeDelta = size; rt.anchoredPosition = position;
            if (parent.name == "Content") rt.anchorMin = rt.anchorMax = new Vector2(.5f, 1);
            return rt;
        }
        private Text Label(Transform parent, string value, Vector2 position, Vector2 size, int fontSize)
        {
            var rt = Rect(parent, "Label", position, size); var text = rt.gameObject.AddComponent<Text>();
            text.font = font; text.text = value; text.fontSize = fontSize; text.color = new Color(.12f, .25f, .24f); text.alignment = TextAnchor.MiddleLeft; text.raycastTarget = false;
            return text;
        }
        private void Button(Transform parent, string value, Vector2 position, Vector2 size, Action action)
        {
            var rt = Rect(parent, value, position, size); rt.gameObject.AddComponent<Image>().color = new Color(.22f, .43f, .41f);
            var button = rt.gameObject.AddComponent<Button>(); button.onClick.AddListener(() => action());
            var text = Label(rt, value, Vector2.zero, size, 20); text.alignment = TextAnchor.MiddleCenter; text.color = Color.white;
        }
    }
}
