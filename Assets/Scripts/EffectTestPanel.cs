using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using BiopunkRV;

/// <summary>
/// 特效测试面板 — 挂到Canvas上，生成按钮控制各种地块特效显示。
/// 支持：选择属性、选择特效类型、调节尺寸/持续时间、生成/清除。
/// </summary>
[ExecuteAlways]
public class EffectTestPanel : MonoBehaviour
{
    // 属性按钮显式映射 — Element枚举顺序与按钮含义不一致, 不能强转
    private static readonly string[] ElementLabels = { "无", "火", "水", "土", "风", "雷", "炎风", "冰", "草", "金" };
    private static readonly Element[] ElementValues =
    {
        Element.None, Element.Fire, Element.Water, Element.Earth, Element.Wind,
        Element.Thunder, Element.FlameWind, Element.Frost, Element.Wood, Element.MetalFusion
    };

    private Element _selectedElement = Element.Fire;
    private int _selectedEffectType = 0;
    private float _radius = 3f;
    private float _duration = 30f; // 默认30s便于观察
    private bool _loop = true; // 默认循环不销毁
    private List<GameObject> _spawnedFx = new List<GameObject>();
    private bool _built = false;

    private void Start()
    {
        EnsureBuilt();
        BindButtons();
    }

    private void OnEnable()
    {
        EnsureBuilt();
        BindButtons();
    }

    private void BindButtons()
    {
        // 运行时绑定按钮事件（lambda闭包无法序列化）
        BindButton("spawn", () => SpawnEffect());
        BindButton("clear", () => ClearAll());
        BindButton("type_patch", () => _selectedEffectType = 0);
        BindButton("type_burst", () => _selectedEffectType = 1);
        BindButton("type_circle", () => _selectedEffectType = 2);
        BindButton("type_multi", () => _selectedEffectType = 3);
        BindButton("affix_lightning", () => DemoLightningBeam());
        BindButton("affix_ice", () => DemoIceCrystal());
        BindButton("affix_shield", () => ToggleShield());
        BindButton("affix_vfx", () => DemoVfxBurst());
        BindButton("affix_windblade", () => DemoWindBlade());
        BindButton("spawn_demoenemy", () => SpawnDemoEnemy());
        for (int i = 0; i < ElementValues.Length; i++)
        {
            Element captured = ElementValues[i];
            BindButton("el_" + i, () => _selectedElement = captured);
        }
        // 滑块和开关
        var radiusSlider = transform.Find("EffectPanel/radius_slider")?.GetComponent<Slider>();
        if (radiusSlider != null) { radiusSlider.onValueChanged.RemoveAllListeners(); radiusSlider.onValueChanged.AddListener(v => _radius = v); }
        var durSlider = transform.Find("EffectPanel/duration_slider")?.GetComponent<Slider>();
        if (durSlider != null) { durSlider.onValueChanged.RemoveAllListeners(); durSlider.onValueChanged.AddListener(v => _duration = v); }
        var toggle = transform.Find("EffectPanel/loop_toggle")?.GetComponent<Toggle>();
        if (toggle != null) { toggle.onValueChanged.RemoveAllListeners(); toggle.onValueChanged.AddListener(v => _loop = v); }
    }

    private void BindButton(string name, System.Action action)
    {
        var go = FindDeepChild(transform, name);
        if (go == null) return;
        var btn = go.GetComponent<Button>();
        if (btn == null) return;
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() => action());
    }

    private Transform FindDeepChild(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            var found = FindDeepChild(child, name);
            if (found != null) return found;
        }
        return null;
    }

    private void EnsureBuilt()
    {
        if (_built) return;
        // 检查是否有完整的UI（含EffectPanel子物体）
        var existing = transform.Find("EffectPanel");
        if (existing != null && existing.childCount > 10) { _built = true; return; }
        // 清空不完整UI重建
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i).gameObject;
            if (Application.isPlaying) Destroy(child);
            else DestroyImmediate(child);
        }
        _built = true;
        BuildUI();
    }

    private void BuildUI()
    {
        var panelGo = new GameObject("EffectPanel");
        panelGo.transform.SetParent(transform, false);
        var prt = panelGo.AddComponent<RectTransform>();
        prt.anchorMin = new Vector2(0, 0.5f);
        prt.anchorMax = new Vector2(0, 0.5f);
        prt.pivot = new Vector2(0, 0.5f);
        prt.anchoredPosition = new Vector2(20, 0);
        prt.sizeDelta = new Vector2(320, 1120);
        var pimg = panelGo.AddComponent<Image>();
        pimg.color = new Color(0.1f, 0.1f, 0.15f, 0.95f);

        float y = 400f;
        MakeLabel(panelGo, "特效测试面板", 24, new Vector2(160, y), new Vector2(300, 40));
        y -= 50;

        for (int i = 0; i < ElementValues.Length; i++)
        {
            Element captured = ElementValues[i];
            float bx = i % 2 == 0 ? 80 : 240;
            float by = y - (i / 2) * 55;
            MakeButton(panelGo, "el_" + i, ElementLabels[i], new Vector2(bx, by), new Vector2(140, 45),
                () => { _selectedElement = captured; });
        }
        y -= 5 * 55 + 30;

        MakeLabel(panelGo, "特效类型:", 18, new Vector2(160, y), new Vector2(300, 30));
        y -= 40;
        MakeButton(panelGo, "type_patch", "地块残留", new Vector2(80, y), new Vector2(140, 40), () => _selectedEffectType = 0);
        MakeButton(panelGo, "type_burst", "爆发", new Vector2(240, y), new Vector2(140, 40), () => _selectedEffectType = 1);
        y -= 50;
        MakeButton(panelGo, "type_circle", "法阵", new Vector2(80, y), new Vector2(140, 40), () => _selectedEffectType = 2);
        MakeButton(panelGo, "type_multi", "多根生成", new Vector2(240, y), new Vector2(140, 40), () => _selectedEffectType = 3);
        y -= 60;

        MakeLabel(panelGo, "半径:", 18, new Vector2(60, y), new Vector2(100, 30));
        var radiusSlider = MakeSlider(panelGo, "radius_slider", new Vector2(180, y), new Vector2(200, 20), 0.5f, 10f, _radius);
        radiusSlider.onValueChanged.AddListener(v => _radius = v);
        y -= 40;

        MakeLabel(panelGo, "时长:", 18, new Vector2(60, y), new Vector2(100, 30));
        var durSlider = MakeSlider(panelGo, "duration_slider", new Vector2(180, y), new Vector2(200, 20), 0.5f, 10f, _duration);
        durSlider.onValueChanged.AddListener(v => _duration = v);
        y -= 50;

        var toggle = MakeToggle(panelGo, "loop_toggle", "循环", new Vector2(60, y));
        toggle.onValueChanged.AddListener(v => _loop = v);
        y -= 50;

        MakeButton(panelGo, "spawn", "生成特效", new Vector2(160, y), new Vector2(280, 50), null);
        y -= 60;
        MakeButton(panelGo, "clear", "清除所有", new Vector2(160, y), new Vector2(280, 50), ClearAll);

        // ── 词缀特效演示区 ──
        y -= 58;
        MakeLabel(panelGo, "── 词缀特效演示 ──", 16, new Vector2(160, y), new Vector2(300, 26));
        y -= 42;
        MakeButton(panelGo, "affix_lightning", "闪电束", new Vector2(80, y), new Vector2(140, 40), DemoLightningBeam);
        MakeButton(panelGo, "affix_ice", "冰晶", new Vector2(240, y), new Vector2(140, 40), DemoIceCrystal);
        y -= 50;
        MakeButton(panelGo, "affix_shield", "护盾罩", new Vector2(80, y), new Vector2(140, 40), ToggleShield);
        MakeButton(panelGo, "affix_vfx", "VFX粒子", new Vector2(240, y), new Vector2(140, 40), DemoVfxBurst);
        y -= 50;
        MakeButton(panelGo, "affix_windblade", "风刃", new Vector2(80, y), new Vector2(140, 40), DemoWindBlade);
        MakeButton(panelGo, "spawn_demoenemy", "生成敌人", new Vector2(240, y), new Vector2(140, 40), SpawnDemoEnemy);
    }

    // ── 词缀特效演示 (复用AffixVisuals正式接口) ──
    private bool _shieldOn;

    /// <summary>闪电束: 中心±4两点间电弧</summary>
    private void DemoLightningBeam()
    {
        Vector3 c = new Vector3(0, 2f, 0);
        AffixVisuals.PlayLightningBeam(c + new Vector3(-4, 0, 0), c + new Vector3(4, 0, 0));
    }

    /// <summary>冰晶: 中心弹出</summary>
    private void DemoIceCrystal()
    {
        AffixVisuals.PlayIceCrystal(new Vector3(0, 2f, 0), _radius);
    }

    /// <summary>护盾罩: 挂在Player物体上, 再点关闭</summary>
    private void ToggleShield()
    {
        _shieldOn = !_shieldOn;
        var player = GameObject.Find("Player");
        if (_shieldOn && player != null)
            AffixVisuals.PlayShieldAura(player.transform);
        else
            AffixVisuals.StopShieldAura();
    }

    /// <summary>VFX粒子: 当前选中属性的VFX帧爆发</summary>
    private void DemoVfxBurst()
    {
        AffixVisuals.PlayVfxBurst(new Vector3(0, 2f, 0), _selectedElement, _radius + 0.5f, 1.5f);
    }

    /// <summary>风刃: 反射调AffixSystem.WindBladeRoutine, 飞向演示敌人</summary>
    private void DemoWindBlade()
    {
        var demoAffix = new GameObject("DemoAffix");
        var affix = demoAffix.AddComponent<AffixSystem>();
        var m = typeof(AffixSystem).GetMethod("WindBladeRoutine",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (m != null)
            StartCoroutine((System.Collections.IEnumerator)m.Invoke(affix, new object[] { new Vector3(0, 2f, 0), 15f }));
        Destroy(demoAffix, 4f);
    }

    /// <summary>生成演示敌人(鬼兔)供风刃/连锁类特效命中</summary>
    private void SpawnDemoEnemy()
    {
        var prefab = Resources.Load<GameObject>("Enemies/GhostRabbit_Enemy");
        if (prefab == null)
        {
            Debug.LogWarning("[EffectTest] GhostRabbit_Enemy prefab not in Resources/Enemies");
            return;
        }
        Vector3 p = new Vector3(Random.Range(-6f, 6f), 1.6f, Random.Range(3f, 7f));
        var e = Instantiate(prefab, p, prefab.transform.rotation);
        e.name = "DemoEnemy";
        var ec = e.GetComponent<EnemyController>();
        var player = GameObject.Find("Player");
        if (ec != null && player != null) ec.target = player.transform;
        _spawnedFx.Add(e); // 清除所有时一起删
    }

    // 属性 → VFX序列帧文件夹 (与技能脚本GetVfxFolderFor一致)
    private static string GetVfxFolderFor(Element el)
    {
        switch (el)
        {
            case Element.Water: return "WaterVFX_Frames";
            case Element.Wind: return "WindVFX_Frames";
            case Element.Thunder: return "ThunderVFX_Frames";
            case Element.Earth: return "EarthVFX_Frames";
            case Element.Fire: return "FireVFX_Frames";
            case Element.FlameWind: return "FlameWindVFX_Frames";
            case Element.Frost: return "FrostVFX_Frames";
            case Element.Wood: return "WoodVFX_Frames";
            case Element.MetalFusion: return "MetalFusionVFX_Frames";
            default: return null;
        }
    }

    private static readonly Dictionary<Element, Sprite[]> _vfxCache = new Dictionary<Element, Sprite[]>();

    private static Sprite[] GetVfxFrames(Element el)
    {
        Sprite[] frames;
        if (_vfxCache.TryGetValue(el, out frames)) return frames;
        string folder = GetVfxFolderFor(el);
        frames = folder == null ? null : Resources.LoadAll<Sprite>(folder);
        if (frames != null && frames.Length == 0) frames = null;
        _vfxCache[el] = frames;
        return frames;
    }

    /// <summary>生成属性序列帧特效(VFXFrameAnimator)。worldSize=目标世界直径。返回null表示该属性无序列帧。</summary>
    private GameObject SpawnVfx(Vector3 pos, Element el, float worldSize, bool loop)
    {
        var frames = GetVfxFrames(el);
        if (frames == null) return null;
        var go = new GameObject("TestVfx_" + el);
        go.transform.position = pos + new Vector3(0, 0.5f, 0);
        go.transform.rotation = Quaternion.Euler(65, 0, 0);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = frames[0];
        sr.sortingOrder = 46;
        float unitWidth = frames[0].rect.width / frames[0].pixelsPerUnit;
        float scale = worldSize / unitWidth;
        go.transform.localScale = new Vector3(scale, scale, 1);
        var anim = go.AddComponent<VFXFrameAnimator>();
        anim.frames = frames;
        anim.fps = 12f;
        anim.loop = loop;
        anim.alpha = 0.9f;
        return go;
    }

    private void SpawnEffect()
    {
        Vector3 center = new Vector3(0, 2f, 0); // 抬高到Ground之上
        GameObject fx = null;

        switch (_selectedEffectType)
        {
            case 0: // 地块残留: 正式余威接口(贴纸风属性地块+程序化动画)
                {
                    float dur = _loop ? 4f : Mathf.Max(_duration, 1f);
                    AffixVisuals.PlayGroundPatch(center, _selectedElement, _radius, dur);
                }
                return;
            case 1: // 爆发: 同心圆扩散 + 序列帧(循环开关决定是否单次)
                AffixVisuals.PlayBurst(center, _selectedElement, _radius);
                TrackFx(SpawnVfx(center, _selectedElement, _radius * 2f, _loop));
                return;
            case 2: // 法阵: 旋转法阵 + 序列帧循环
                AffixVisuals.PlayGroundCircle(center, _selectedElement, _radius, _duration > 0 ? _duration : 3f, false);
                TrackFx(SpawnVfx(center, _selectedElement, _radius * 1.5f, _loop));
                return;
            case 3:
                SpawnMultiElements(center, _selectedElement);
                return;
        }

        if (fx != null) TrackFx(fx);
    }

    /// <summary>登记特效对象, 非循环模式按时长销毁</summary>
    private void TrackFx(GameObject fx)
    {
        if (fx == null) return;
        _spawnedFx.Add(fx);
        if (!_loop && _duration > 0) Destroy(fx, _duration);
    }

    private GameObject SpawnSpriteFx(Vector3 pos, Sprite sprite)
    {
        var go = new GameObject("TestFx_" + sprite.name);
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(65, 0, 0);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = 44;
        // 地面特效尺寸: 世界尺寸 = 直径(radius×2), 独立于贴图PPU
        float targetWorldSize = _radius * 2f;
        float spriteWorldWidth = sprite.bounds.size.x; // 未缩放时的世界宽度
        float scale = targetWorldSize / spriteWorldWidth;
        go.transform.localScale = new Vector3(scale, scale, 1);
        Debug.Log($"[EffectTest] Spawned {sprite.name}: worldSize={targetWorldSize}, scale={scale}");
        return go;
    }

    private void SpawnMultiElements(Vector3 center, Element el)
    {
        int count = el == Element.Wood ? 3 : 5;
        // 草/金用单元素贴图(藤蔓/尖刺), 其他用地块贴图
        string spriteName = el == Element.Wood ? "WoodVine" : (el == Element.MetalFusion ? "MetalSpike" : el + "Ground");
        var sprite = Resources.Load<Sprite>("GroundEffects/" + spriteName);
        if (sprite == null)
        {
            Debug.LogWarning("[EffectTest] Sprite not found: " + spriteName);
            return;
        }
        for (int i = 0; i < count; i++)
        {
            float angle = (360f / count) * i;
            Vector3 offset = Quaternion.Euler(0, angle, 0) * Vector3.forward * _radius * 0.5f;
            var go = SpawnSpriteFx(center + offset, sprite);
            go.transform.localRotation = Quaternion.Euler(65, angle, 0);
            go.transform.localScale *= Random.Range(0.7f, 1.2f);
            TrackFx(go);
        }
        // 中心放属性序列帧
        TrackFx(SpawnVfx(center, el, _radius * 2f, true));
    }

    private void ClearAll()
    {
        foreach (var fx in _spawnedFx)
            if (fx != null) Destroy(fx);
        _spawnedFx.Clear();
        _shieldOn = false;
        AffixVisuals.StopShieldAura();
    }

    private Button MakeButton(GameObject parent, string name, string text, Vector2 pos, Vector2 size, System.Action onClick)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var img = go.AddComponent<Image>();
        img.color = new Color(0.3f, 0.5f, 0.8f, 0.8f);
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        var txt = new GameObject("Text");
        txt.transform.SetParent(go.transform, false);
        var trt = txt.AddComponent<RectTransform>();
        trt.sizeDelta = size;
        var t = txt.AddComponent<Text>();
        t.text = text;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = 20;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = Color.white;
        t.raycastTarget = false; // 不挡Button射线
        if (onClick != null) btn.onClick.AddListener(() => onClick());
        return btn;
    }

    private Text MakeLabel(GameObject parent, string text, int size, Vector2 pos, Vector2 box)
    {
        var go = new GameObject("Label");
        go.transform.SetParent(parent.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = box;
        var t = go.AddComponent<Text>();
        t.text = text;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = size;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = Color.white;
        return t;
    }

    private Slider MakeSlider(GameObject parent, string name, Vector2 pos, Vector2 size, float min, float max, float val)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var bg = new GameObject("Background");
        bg.transform.SetParent(go.transform, false);
        var bgrt = bg.AddComponent<RectTransform>();
        bgrt.sizeDelta = size;
        var bgimg = bg.AddComponent<Image>();
        bgimg.color = new Color(0.2f, 0.2f, 0.3f, 0.8f);
        var fill = new GameObject("Fill");
        fill.transform.SetParent(go.transform, false);
        var frt = fill.AddComponent<RectTransform>();
        frt.sizeDelta = size;
        var fimg = fill.AddComponent<Image>();
        fimg.color = new Color(0.4f, 0.6f, 0.9f, 1f);
        var handle = new GameObject("Handle");
        handle.transform.SetParent(go.transform, false);
        var hrt = handle.AddComponent<RectTransform>();
        hrt.sizeDelta = new Vector2(20, 30);
        var himg = handle.AddComponent<Image>();
        himg.color = Color.white;
        var slider = go.AddComponent<Slider>();
        slider.minValue = min;
        slider.maxValue = max;
        slider.value = val;
        slider.targetGraphic = bgimg;
        return slider;
    }

    private Toggle MakeToggle(GameObject parent, string name, string label, Vector2 pos)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(100, 40);
        var bg = new GameObject("Background");
        bg.transform.SetParent(go.transform, false);
        var bgrt = bg.AddComponent<RectTransform>();
        bgrt.sizeDelta = new Vector2(40, 40);
        var bgimg = bg.AddComponent<Image>();
        bgimg.color = Color.white;
        var check = new GameObject("Checkmark");
        check.transform.SetParent(bg.transform, false);
        var crt = check.AddComponent<RectTransform>();
        crt.sizeDelta = new Vector2(30, 30);
        var cimg = check.AddComponent<Image>();
        cimg.color = new Color(0.3f, 0.6f, 1f, 1f);
        var txt = new GameObject("Label");
        txt.transform.SetParent(go.transform, false);
        var trt = txt.AddComponent<RectTransform>();
        trt.anchoredPosition = new Vector2(60, 0);
        trt.sizeDelta = new Vector2(80, 40);
        var t = txt.AddComponent<Text>();
        t.text = label;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = 18;
        t.alignment = TextAnchor.MiddleLeft;
        t.color = Color.white;
        var toggle = go.AddComponent<Toggle>();
        toggle.graphic = cimg;
        toggle.targetGraphic = bgimg;
        return toggle;
    }
}
