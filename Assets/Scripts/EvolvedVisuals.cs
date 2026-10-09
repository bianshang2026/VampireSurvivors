using UnityEngine;

namespace BiopunkRV
{
    /// <summary>
    /// 进阶技能基础外观演示系统。
    /// 架构原则: 基础外观(技能本体长什么样: 球→龙弹/剑→龙剑/阵→属性墙/刃→刃龙卷/符→符环绕)
    ///           × 呈现方式(由词缀决定: 环绕→盘旋/天降→从天而降/穿透→直线贯穿...)
    /// 基础外观全部用 无色资产 + ElementColors换色 + 属性VFX帧 实现(零额外贴图成本)。
    /// DevPanel"进阶外观演示"区按当前循环属性预览。
    /// </summary>
    public static class EvolvedVisuals
    {
        // ── 演示用当前属性 (DevPanel循环按钮切换) ──
        private static readonly Element[] _cycle = { Element.Fire, Element.Water, Element.Wind, Element.Earth, Element.Thunder };
        public static int CycleIndex { get; private set; }
        public static Element CurrentElement => _cycle[CycleIndex];

        /// <summary>循环到下一属性, 返回新属性中文名</summary>
        public static string CycleElement()
        {
            CycleIndex = (CycleIndex + 1) % _cycle.Length;
            return ElementColors.GetName(_cycle[CycleIndex]);
        }

        /// <summary>属性→VFX帧目录 (None→null)</summary>
        public static string GetVfxFolder(Element el)
        {
            switch (el)
            {
                case Element.Fire: return "FireVFX_Frames";
                case Element.Water: return "WaterVFX_Frames";
                case Element.Wind: return "WindVFX_Frames";
                case Element.Earth: return "EarthVFX_Frames";
                case Element.Thunder: return "ThunderVFX_Frames";
                case Element.FlameWind: return "FlameWindVFX";
                case Element.Frost: return "FrostVFX";
                case Element.Wood: return "WoodVFX";
                case Element.MetalFusion: return "MetalFusionVFX";
                default: return null;
            }
        }

        /// <summary>给宿主挂环绕式属性VFX子物体 (帧自带元素色, 不tint)</summary>
        public static void AttachVfx(Transform host, Element el, float worldSize, int count = 2, int sortOrder = 53)
        {
            var folder = GetVfxFolder(el);
            if (folder == null) return;
            var frames = Resources.LoadAll<Sprite>(folder);
            if (frames == null || frames.Length == 0) return;
            System.Array.Sort(frames, (a, b) => string.CompareOrdinal(a.name, b.name));
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("EvVfx" + i);
                go.transform.SetParent(host, false);
                float ang = i * Mathf.PI * 2f / count;
                go.transform.localPosition = new Vector3(Mathf.Cos(ang), 0.4f, Mathf.Sin(ang)) * worldSize * 0.35f;
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = frames[0];
                sr.sortingOrder = sortOrder;
                float unitW = frames[0].bounds.size.x;
                float sc = unitW > 0.01f ? worldSize / unitW : 1f;
                go.transform.localScale = new Vector3(sc, sc, 1f);
                var anim = go.AddComponent<VFXFrameAnimator>();
                anim.frames = frames;
                anim.startFrame = i * (frames.Length / Mathf.Max(1, count));
                anim.fps = 10f;
                anim.loop = true;
                anim.alpha = 0.85f;
            }
        }

        // ══════════════ 1. 龙弹 (灵球术进阶·基础外观=独立美术资产) ══════════════
        // 基础外观: 专用龙弹投射物资产(DragonOrb_Colorless, 无色→tint)+VFX
        // 呈现方式(词缀决定): 直线飞(基础/穿透) + 环绕盘旋(环绕词缀)

        public static void DemoDragonOrb()
        {
            var player = GameObject.Find("Player");
            if (player == null) return;
            Element el = CurrentElement;
            Color tint = ElementColors.GetTint(el);
            var spr = Resources.Load<Sprite>("DragonOrb_Colorless"); // 专用龙弹资产
            if (spr == null) spr = Resources.Load<Sprite>("Dao_Dragon_Straight"); // 未导入时暂用
            if (spr == null) { Debug.LogWarning("[EvolvedVisuals] 龙弹贴图缺失"); return; }
            float unitW = spr.bounds.size.x;
            float scale = 2.6f / unitW;

            // 基础呈现: 直线飞出 (词缀: 穿透/直线)
            for (int i = 0; i < 3; i++)
            {
                var go = MakeVisual("DragonOrb_" + i, spr, tint, scale, 52);
                Vector3 dir = Quaternion.Euler(0f, i * 120f, 0f) * Vector3.right;
                go.transform.position = player.transform.position + new Vector3(0f, 2.5f, 0f) + dir * 1.5f;
                AttachVfx(go.transform, el, 1.6f, 2, 53);
                AffixVisuals.RunFx(FlyStraight(go.transform, dir, 8f, 3.5f));
            }
            // 环绕词缀呈现: 绕玩家盘旋
            var orbitGo = MakeVisual("DragonOrb_Orbit", spr, tint, scale, 52);
            orbitGo.transform.position = player.transform.position + new Vector3(0f, 3f, 0f);
            AttachVfx(orbitGo.transform, el, 1.6f, 2, 53);
            AffixVisuals.RunFx(OrbitRoutine(orbitGo.transform, player.transform, 4f, 2.2f, 5f, scale));
            Debug.Log($"[EvolvedVisuals] 龙弹演示({ElementColors.GetName(el)}): 3直线+1环绕");
        }

        // ══════════════ 2. 龙脊剑 (飞剑术进阶·基础外观) ══════════════
        // 基础外观: 飞剑+元素染色+剑身VFX环绕 (龙气附剑)

        public static void DemoDragonSword()
        {
            var player = GameObject.Find("Player");
            if (player == null) return;
            Element el = CurrentElement;
            Color tint = ElementColors.GetTint(el);
            var spr = Resources.Load<Sprite>("DaoSword_Colorless");
            if (spr == null) spr = LoadSpriteEditor("FlyingSword_v2.png");
            if (spr == null) { Debug.LogWarning("[EvolvedVisuals] 飞剑贴图缺失"); return; }
            float unitW = spr.bounds.size.x;
            float scale = 3.2f / unitW;

            for (int i = 0; i < 3; i++)
            {
                var go = MakeVisual("DragonSword_" + i, spr, tint, scale, 54);
                go.transform.position = player.transform.position + new Vector3(0f, 3f, 0f);
                AttachVfx(go.transform, el, 1.8f, 3, 55);
                float phase = i * Mathf.PI * 2f / 3f;
                AffixVisuals.RunFx(OrbitRoutine(go.transform, player.transform, 4.5f, 1.8f, 5f, scale, phase));
            }
            Debug.Log($"[EvolvedVisuals] 龙剑演示({ElementColors.GetName(el)}): 3柄元素龙剑环绕");
        }

        // ══════════════ 3. 属性墙 (灵阵术进阶·基础外观) ══════════════
        // 基础外观: 残留地块同款动态元素墙 — GroundPatchFrames/{属性}16帧动画做墙段,
        // 每个属性的墙都在动(火焰翻腾/雷电闪烁/水波流转), 元素感和残留地块一致

        public static void DemoElementWall()
        {
            var player = GameObject.Find("Player");
            if (player == null) return;
            Element el = CurrentElement;
            string folder = "GroundPatchFrames/" + el;
            var frames = Resources.LoadAll<Sprite>(folder);
            if (frames == null || frames.Length == 0)
            { Debug.LogWarning("[EvolvedVisuals] 残留地块帧缺失 " + folder); return; }
            System.Array.Sort(frames, (a, b) => string.CompareOrdinal(a.name, b.name));

            const int segments = 8;
            const float radius = 5.5f;
            var root = new GameObject("ElementWall_" + el);
            float unitW = frames[0].bounds.size.x;
            float sc = (radius * 1.05f) / unitW; // 墙段宽度≈环边长
            for (int i = 0; i < segments; i++)
            {
                float ang = i * Mathf.PI * 2f / segments;
                var go = new GameObject("WallSeg" + i);
                go.transform.SetParent(root.transform, false);
                go.transform.position = player.transform.position + new Vector3(Mathf.Cos(ang) * radius, 1.3f, Mathf.Sin(ang) * radius);
                float tangent = Mathf.Atan2(Mathf.Cos(ang), -Mathf.Sin(ang)) * Mathf.Rad2Deg;
                go.transform.rotation = Quaternion.Euler(65.69891f, 0f, tangent);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = frames[0];
                sr.sortingOrder = 48;
                go.transform.localScale = new Vector3(sc, sc, 1f);
                var anim = go.AddComponent<VFXFrameAnimator>();
                anim.frames = frames;
                anim.startFrame = i * (frames.Length / segments); // 相位错开, 环形流动感
                anim.fps = 10f;
                anim.loop = true;
                anim.alpha = 0.95f;
            }
            Object.Destroy(root, 8f);
            Debug.Log($"[EvolvedVisuals] 属性墙演示({ElementColors.GetName(el)}): 8面{ElementColors.GetName(el)}墙(残留地块同款动态), 8秒后消失");
        }

        // ══════════════ 4. 灵刃环绕 (灵气刃进阶·基础外观) ══════════════
        // 基础外观: 元素染色的灵气刃; 呈现(环绕词缀): 一直平绕玩家转

        public static void DemoBladeOrbit()
        {
            var player = GameObject.Find("Player");
            if (player == null) return;
            Element el = CurrentElement;
            Color tint = ElementColors.GetTint(el);
            var qb = Object.FindObjectOfType<QiBladeSpell>();
            var spr = Resources.Load<Sprite>("DaoBlade_Colorless");
            if (spr == null) spr = qb != null ? qb.bladeSprite : null;
#if UNITY_EDITOR
            if (spr == null) spr = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/TJGenerators/History/Sprite_20260812_200505.png");
#endif
            if (spr == null) { Debug.LogWarning("[EvolvedVisuals] 灵气刃贴图缺失"); return; }
            float unitW = spr.bounds.size.x;
            float scale = 1.8f / unitW;

            for (int i = 0; i < 6; i++)
            {
                var go = MakeVisual("BladeOrbit_" + i, spr, tint, scale, 56);
                go.transform.position = player.transform.position + new Vector3(0f, 2.5f, 0f);
                AttachVfx(go.transform, el, 1.2f, 1, 57);
                float phase = i * Mathf.PI * 2f / 6f;
                AffixVisuals.RunFx(OrbitRoutine(go.transform, player.transform, 3.6f, 2.2f, 8f, scale, phase));
            }
            Debug.Log($"[EvolvedVisuals] 灵刃环绕演示({ElementColors.GetName(el)}): 6刃持续平绕");
        }

        // ══════════════ 5. 符环绕 (符箓术进阶·基础外观) ══════════════
        // 基础外观: 元素染色的符箓; 呈现(环绕词缀): 一直平绕玩家转(带自旋)

        public static void DemoTalismanOrbit()
        {
            var player = GameObject.Find("Player");
            if (player == null) return;
            Element el = CurrentElement;
            Color tint = ElementColors.GetTint(el);
            var ts = Object.FindObjectOfType<TalismanSpell>();
            var spr = Resources.Load<Sprite>("DaoTalisman_Colorless");
            if (spr == null) spr = ts != null ? ts.talismanSprite : null;
#if UNITY_EDITOR
            if (spr == null) spr = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/TJGenerators/History/Sprite_20260812_163612.png");
#endif
            if (spr == null) { Debug.LogWarning("[EvolvedVisuals] 符箓贴图缺失"); return; }
            float unitW = spr.bounds.size.x;
            float scale = 1.5f / unitW;

            for (int i = 0; i < 8; i++)
            {
                var go = MakeVisual("TalismanOrbit_" + i, spr, tint, scale, 56);
                go.transform.position = player.transform.position + new Vector3(0f, 2.5f, 0f);
                AttachVfx(go.transform, el, 1.2f, 1, 57);
                float phase = i * Mathf.PI * 2f / 8f;
                AffixVisuals.RunFx(OrbitRoutine(go.transform, player.transform, 3.2f, 1.8f, 8f, scale, phase));
            }
            Debug.Log($"[EvolvedVisuals] 符环绕演示({ElementColors.GetName(el)}): 8符持续平绕");
        }

        // ══════════════ 工具 ══════════════

        private static GameObject MakeVisual(string name, Sprite spr, Color tint, float scale, int sortOrder)
        {
            var go = new GameObject(name);
            go.transform.rotation = Quaternion.Euler(65.69891f, 0f, 0f);
            go.transform.localScale = new Vector3(scale, scale, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = spr;
            sr.color = tint;
            sr.sortingOrder = sortOrder;
            return go;
        }

        private static Sprite LoadSpriteEditor(string fileName)
        {
            var spr = Object.FindObjectOfType<FlyingSword>();
            Sprite s = null;
            if (spr != null)
            {
                var sr = spr.GetComponentInChildren<SpriteRenderer>();
                if (sr != null) s = sr.sprite;
            }
#if UNITY_EDITOR
            if (s == null) s = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/TJGenerators/History/" + fileName);
#endif
            return s;
        }

        /// <summary>直线飞行(基础呈现), life后自毁</summary>
        private static System.Collections.IEnumerator FlyStraight(Transform t, Vector3 dir, float speed, float life)
        {
            float e = 0f;
            while (e < life && t != null)
            {
                e += Time.deltaTime;
                t.position += dir * speed * Time.deltaTime;
                yield return null;
            }
            if (t != null) Object.Destroy(t.gameObject);
        }

        /// <summary>环绕呈现 (环绕词缀): 绕中心盘旋, 朝左镜像Y防肚皮朝天</summary>
        private static System.Collections.IEnumerator OrbitRoutine(Transform t, Transform center, float radius, float speedRad, float life, float baseScale, float phase = 0f)
        {
            float e = 0f, ang = phase;
            while (e < life && t != null)
            {
                e += Time.deltaTime;
                ang += speedRad * Time.deltaTime;
                t.position = center.position + new Vector3(Mathf.Cos(ang) * radius, 2.5f + Mathf.Sin(e * 2f) * 0.4f, Mathf.Sin(ang) * radius);
                // 朝切线方向; 龙贴图头朝+X
                float z = Mathf.Atan2(Mathf.Cos(ang), -Mathf.Sin(ang)) * Mathf.Rad2Deg;
                t.rotation = Quaternion.Euler(65.69891f, 0f, z);
                bool left = Mathf.Cos(z * Mathf.Deg2Rad) < 0f;
                t.localScale = new Vector3(baseScale, left ? -baseScale : baseScale, 1f);
                yield return null;
            }
            if (t != null) Object.Destroy(t.gameObject);
        }

        /// <summary>平绕+自旋呈现 (环绕词缀的符/刃形态), life后自毁</summary>
        private static System.Collections.IEnumerator OrbitSpinRoutine(Transform t, Transform center, float radius, float speedRad, float life, float phase)
        {
            float e = 0f, ang = phase;
            while (e < life && t != null)
            {
                e += Time.deltaTime;
                ang += speedRad * Time.deltaTime;
                t.position = center.position + new Vector3(Mathf.Cos(ang) * radius, 2.5f + Mathf.Sin(e * 2f + phase) * 0.3f, Mathf.Sin(ang) * radius);
                t.Rotate(0f, 0f, 260f * Time.deltaTime);
                yield return null;
            }
            if (t != null) Object.Destroy(t.gameObject);
        }
    }
}
