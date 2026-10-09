using UnityEngine;
using System.Collections.Generic;

namespace BiopunkRV
{
    /// <summary>
    /// 词缀视觉特效静态工具类 — 所有词缀的"看得见"效果。
    /// 贴图纯白原色, 运行时按属性tint。特效协程挂独立Runner(不挂在投射物上)。
    /// </summary>
    public static class AffixVisuals
    {
        // 贴图缓存 (纯白原色, 运行时tint)
        private static Sprite _burstCircle;   // 同心圆爆发 (复用法阵图)
        private static Sprite _spawnCircle;   // 地面法阵
        private static Sprite _critStar;      // 星形 (crit_icon)
        private static Sprite _slashSprite;   // 剑气 (SlashEffect_Sprite)
        private static Sprite _lightningBeam; // 闪电束 (LightningBeam)
        private static Sprite _shockwaveRing; // 冲击波环 (ShockwaveRing)
        private static Sprite _iceCrystal;    // 冰晶块 (IceCrystal)
        private static Sprite _windBladeSpr;  // 小风刃 (WindBlade)
        private static Sprite _shieldDome;    // 护盾罩 (ShieldDome)
        private static bool _spritesLoaded = false;

        // 特效Runner容器 (所有协程挂这里, 避免投射物销毁中断)
        private static GameObject _fxRoot;

        /// <summary>懒加载贴图缓存</summary>
        public static void EnsureSprites()
        {
            if (_spritesLoaded) return;
            _spawnCircle = Resources.Load<Sprite>("SpawnCircle");
            _critStar = Resources.Load<Sprite>("DamageNumbers/crit_icon");
            _slashSprite = Resources.Load<Sprite>("SlashEffect_Sprite");
            _lightningBeam = Resources.Load<Sprite>("LightningBeam");
            _shockwaveRing = Resources.Load<Sprite>("ShockwaveRing");
            _iceCrystal = Resources.Load<Sprite>("IceCrystal");
            _windBladeSpr = Resources.Load<Sprite>("WindBlade");
            _shieldDome = Resources.Load<Sprite>("ShieldDome");
            // BurstCircle优先冲击波环, 缺失回退法阵图
            _burstCircle = _shockwaveRing != null ? _shockwaveRing : _spawnCircle;
            _spritesLoaded = true;
        }

        private static GameObject FxRoot
        {
            get
            {
                if (_fxRoot == null)
                {
                    _fxRoot = new GameObject("AffixFxRoot");
                    Object.DontDestroyOnLoad(_fxRoot);
                }
                return _fxRoot;
            }
        }

        /// <summary>启动特效协程(挂独立Runner) — public: RootMechanics等外部系统复用此Runner</summary>
        public static void RunFx(System.Collections.IEnumerator routine)
        {
            var runner = FxRoot.AddComponent<AffixFxRunner>();
            runner.Run(routine);
        }

        // ── 爆发: 灵爆/水波/震击/爆裂/延迟爆发 (同心圆快速放大+淡出) ──
        public static void PlayBurst(Vector3 pos, Element el, float radius, float dur = 0.3f)
        {
            EnsureSprites();
            if (_burstCircle == null) return;
            Color tint = ElementColors.GetTint(el);
            RunFx(BurstAnim(pos, tint, radius, dur));
        }

        private static System.Collections.IEnumerator BurstAnim(Vector3 pos, Color tint, float radius, float dur)
        {
            var go = new GameObject("AffixBurst");
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(65f, 0, 0);
            go.AddComponent<SpriteRenderer>().sprite = _burstCircle;
            var sr = go.GetComponent<SpriteRenderer>();
            sr.color = new Color(tint.r, tint.g, tint.b, 0.9f);
            sr.sortingOrder = 45;

            // 世界尺寸归一化
            float unitWidth = _burstCircle.rect.width / _burstCircle.pixelsPerUnit;
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float p = t / dur;
                float worldSize = Mathf.Lerp(radius * 0.3f, radius, p);
                float scale = worldSize / unitWidth;
                go.transform.localScale = new Vector3(scale, scale, 1);
                Color c = sr.color;
                c.a = Mathf.Lerp(0.9f, 0f, p);
                sr.color = c;
                yield return null;
            }
            Object.Destroy(go);
        }

        // ── 地面法阵: 余威/延迟/深流/疾风/增殖 (旋转+慢淡出) ──
        public static void PlayGroundCircle(Vector3 pos, Element el, float radius, float dur, bool rotate = true)
        {
            EnsureSprites();
            if (_spawnCircle == null) return;
            Color tint = ElementColors.GetTint(el);
            RunFx(GroundCircleAnim(pos, tint, radius, dur, rotate));
        }

        private static System.Collections.IEnumerator GroundCircleAnim(Vector3 pos, Color tint, float radius, float dur, bool rotate)
        {
            var go = new GameObject("AffixGroundCircle");
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(65f, 0, 0);
            go.AddComponent<SpriteRenderer>().sprite = _spawnCircle;
            var sr = go.GetComponent<SpriteRenderer>();
            sr.color = new Color(tint.r, tint.g, tint.b, 0.7f);
            sr.sortingOrder = 44;

            float unitWidth = _spawnCircle.rect.width / _spawnCircle.pixelsPerUnit;
            float scale = radius / unitWidth;
            go.transform.localScale = new Vector3(scale, scale, 1);

            // 淡入
            float t = 0f;
            float fadeIn = 0.2f;
            while (t < fadeIn)
            {
                t += Time.deltaTime;
                float p = t / fadeIn;
                Color c = sr.color;
                c.a = Mathf.Lerp(0f, 0.7f, p);
                sr.color = c;
                yield return null;
            }

            // 持续 + 旋转 + 淡出
            t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float p = t / dur;
                if (rotate) go.transform.Rotate(0, 0, 60f * Time.deltaTime);
                Color c = sr.color;
                c.a = Mathf.Lerp(0.7f, 0f, p * p);
                sr.color = c;
                yield return null;
            }
            Object.Destroy(go);
        }

        // ── 属性地块: 余威专属 (每属性一张地块贴图, 颜色已烘焙, 不tint) ──
        private static readonly Dictionary<Element, Sprite> _groundPatches = new Dictionary<Element, Sprite>();

        /// <summary>按属性加载地块特效贴图 (Resources/GroundEffects/{el}Ground)</summary>
        public static Sprite GetGroundPatch(Element el)
        {
            Sprite s;
            if (_groundPatches.TryGetValue(el, out s)) return s;
            s = Resources.Load<Sprite>("GroundEffects/" + el + "Ground");
            _groundPatches[el] = s; // 缓存null: 缺失时回退法阵
            return s;
        }

        private static readonly Dictionary<Element, Sprite[]> _patchFrames = new Dictionary<Element, Sprite[]>();
        private static readonly Dictionary<Element, Sprite[]> _patchFramesV2 = new Dictionary<Element, Sprite[]>();

        /// <summary>雷(雷泽)/风/炎风: 旧版厚涂动画帧 (Resources/GroundPatchFrames/{el}/frame_00~15, 2026-08-31用户指定恢复)</summary>
        public static Sprite[] GetPatchFrames(Element el)
        {
            Sprite[] f;
            if (_patchFrames.TryGetValue(el, out f)) return f;
            f = Resources.LoadAll<Sprite>("GroundPatchFrames/" + el);
            if (f != null && f.Length > 1)
                System.Array.Sort(f, (a, b) => string.CompareOrdinal(a.name, b.name));
            _patchFrames[el] = f;
            return f;
        }

        /// <summary>
        /// 余威地块特效 — 贴纸风属性地块+各属性特色动态 (2026-09-03):
        /// 岩浆慢转/炎风旋涡/土震动/水波纹/雷颤动/冰微光, 木(藤蔓)/金(尖刺)用多根布置,
        /// 风/雷/炎风的旧GroundPatchFrames帧动画已废弃, 改用GroundEffects稳定池+特色动态。
        /// </summary>
        public static void PlayGroundPatch(Vector3 pos, Element el, float radius, float dur)
        {
            // 无属性不留残留物 (无色地块无意义, 用户指定)
            if (el == Element.None) return;
            // 木(藤蔓)/金(尖刺): 单元素贴图, 区域内随机布置多根形成"冒出"效果 (独特结构保留)
            if (el == Element.Wood || el == Element.MetalFusion)
            {
                var patchM = GetGroundPatch(el);
                if (patchM != null)
                {
                    RunFx(MultiSpikeAnim(pos, patchM, radius, dur, el, popOut: el == Element.MetalFusion));
                    return;
                }
            }
            // 风: 保留旧版厚涂动画帧 (风灵蝶飘带质感)
            if (el == Element.Wind)
            {
                var frames = GetPatchFrames(el);
                if (frames != null && frames.Length > 0)
                {
                    RunFx(GroundPatchFramesAnim(pos, frames, radius, dur));
                    return;
                }
            }
            // 炎风/冰/雷: 8帧序列帧动画 (循环播放, 稳定不闪, 长停留淡出)
            if (el == Element.FlameWind || el == Element.Frost || el == Element.Thunder)
            {
                var frames2 = GetPatchFramesV2(el);
                if (frames2 != null && frames2.Length > 0)
                {
                    RunFx(GroundPatchFramesLoopAnim(pos, frames2, radius, Mathf.Max(dur, 4f)));
                    return;
                }
            }
            // 其余属性: 稳定池演出 + 各属性特色动态
            var patch = GetGroundPatch(el);
            if (patch == null)
            {
                PlayGroundCircle(pos, el, radius, dur, el == Element.Wind);
                return;
            }
            // 属性特色子层: 炎风的青色旋流层 (运行时持续旋转)
            Sprite accent = null;
            float accentSpeed = 0f;
            if (el == Element.FlameWind)
            {
                accent = Resources.Load<Sprite>("GroundEffects/FlameWind_swirl");
                accentSpeed = 20f;
            }
            RunFx(PoolAnim(pos, patch, radius, Mathf.Max(dur, 4f), GetPoolMotion(el), accent, accentSpeed));
        }

        /// <summary>炎风/冰/雷: 新8帧序列帧 (Resources/GroundPatchFrames_v2/{el}/frame_00~07)</summary>
        public static Sprite[] GetPatchFramesV2(Element el)
        {
            Sprite[] f;
            if (_patchFramesV2.TryGetValue(el, out f)) return f;
            f = Resources.LoadAll<Sprite>("GroundPatchFrames_v2/" + el);
            if (f != null && f.Length > 1)
                System.Array.Sort(f, (a, b) => string.CompareOrdinal(a.name, b.name));
            _patchFramesV2[el] = f;
            return f;
        }

        /// <summary>炎风/冰/雷: 8帧序列帧循环动画 — 稳定alpha不闪, 帧内容动画, 长停留淡出</summary>
        private static System.Collections.IEnumerator GroundPatchFramesLoopAnim(Vector3 pos, Sprite[] frames, float radius, float dur)
        {
            var go = new GameObject("GroundPatchFramesLoop");
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(65f, 0, 0);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = frames[0];
            sr.sortingOrder = 44;

            // 世界尺寸=伤害区直径(2×radius)
            float unitWidth = frames[0].rect.width / frames[0].pixelsPerUnit;
            float scale = (radius * 2f) / unitWidth;
            go.transform.localScale = new Vector3(scale, scale, 1);

            const float fps = 6f; // 8帧/6fps ≈ 1.33s循环
            const float fadeIn = 0.2f;
            const float fadeOut = 0.6f;
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                if (sr != null)
                {
                    sr.sprite = frames[(int)(t * fps) % frames.Length];
                    float a = 0.95f;
                    if (t < fadeIn) a *= t / fadeIn;
                    float remain = dur - t;
                    if (remain < fadeOut) a *= remain / fadeOut;
                    var c = sr.color;
                    c.a = a;
                    sr.color = c;
                }
                yield return null;
            }
            Object.Destroy(go);
        }

        /// <summary>草/金地块: 单元素在半径内随机布置多根(不同角度/大小/翻转), 随DoT脉冲</summary>
        private static System.Collections.IEnumerator MultiSpikeAnim(Vector3 pos, Sprite elem, float radius, float dur, Element el, bool popOut)
        {
            float unitWidth = elem.rect.width / elem.pixelsPerUnit;
            int count = 5;
            var items = new List<(Transform t, SpriteRenderer sr, float baseScale, float baseZ, bool flip)>();
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("AffixGroundElem");
                // 中心一根 + 环形4根
                float r = i == 0 ? 0f : radius * Random.Range(0.35f, 0.6f);
                float ang = i == 0 ? Random.Range(0f, 360f) : (i - 1) * 90f + Random.Range(-25f, 25f);
                Vector3 off = new Vector3(Mathf.Cos(ang * Mathf.Deg2Rad) * r, 0f, Mathf.Sin(ang * Mathf.Deg2Rad) * r);
                go.transform.position = pos + off;
                float baseZ = Random.Range(0f, 360f);
                go.transform.rotation = Quaternion.Euler(65f, 0f, baseZ);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = elem;
                sr.color = Color.white;
                sr.sortingOrder = 44;
                float sc = Random.Range(0.75f, 1.05f);
                bool flip = Random.value < 0.5f;
                // 单元素世界尺寸≈半径的1.2倍
                float s = (radius * 1.2f / unitWidth) * sc;
                go.transform.localScale = new Vector3(flip ? -s : s, s, 1);
                items.Add((go.transform, sr, s, baseZ, flip));
            }

            const float fadeIn = 0.15f;
            const float fadeOut = 0.4f;
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float a = 0.6f + 0.3f * Mathf.Pow(1f - Mathf.Repeat(t, 0.5f) / 0.5f, 2f);
                if (t < fadeIn) a *= t / fadeIn;
                float remain = dur - t;
                if (remain < fadeOut) a *= remain / fadeOut;
                foreach (var it in items)
                {
                    if (it.t == null) continue;
                    var c = it.sr.color;
                    c.a = a;
                    it.sr.color = c;
                    if (popOut)
                    {
                        // 尖刺: 出场0.2s弹出(0.2→1.15→1)
                        if (t < 0.2f)
                        {
                            float p = t / 0.2f;
                            float ps = p < 0.7f ? Mathf.Lerp(0.2f, 1.15f, p / 0.7f) : Mathf.Lerp(1.15f, 1f, (p - 0.7f) / 0.3f);
                            it.t.localScale = new Vector3(it.flip ? -it.baseScale * ps : it.baseScale * ps, it.baseScale * ps, 1);
                        }
                        else it.t.localScale = new Vector3(it.flip ? -it.baseScale : it.baseScale, it.baseScale, 1);
                    }
                    else
                    {
                        // 藤蔓: 摆动±8°
                        it.t.rotation = Quaternion.Euler(65f, 0f, it.baseZ + Mathf.Sin(t * 3f + it.baseZ) * 8f);
                    }
                }
                yield return null;
            }
            foreach (var it in items)
                if (it.t != null) Object.Destroy(it.t.gameObject);
        }

        private static System.Collections.IEnumerator GroundPatchAnim(Vector3 pos, Sprite patch, float radius, float dur, Element el)
        {
            var go = new GameObject("AffixGroundPatch");
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(65f, 0, 0);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = patch;
            sr.color = Color.white;
            sr.sortingOrder = 44;

            // 世界尺寸=伤害区直径(2×radius), 贴图色已烘焙
            float unitWidth = patch.rect.width / patch.pixelsPerUnit;
            float scale = (radius * 2f) / unitWidth;
            go.transform.localScale = new Vector3(scale, scale, 1);

            // 元素动态特征 (7.6.4) — 草/金已走MultiSpikeAnim, 此处不含
            bool shake = el == Element.Earth;                              // 震裂地: 地震抖动
            bool vortex = el == Element.Wind || el == Element.FlameWind;   // 漩涡类: 旋转
            float ampMult = el == Element.Fire ? 1.3f : 1f;                // 岩浆: 脉冲增幅
            float pulsePeriod = el == Element.Water ? 0.7f : 0.5f;         // 水潭: 慢速脉冲

            const float fadeIn = 0.15f;
            const float fadeOut = 0.4f;
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                // 每跳亮起后衰减的锯齿脉冲
                float a = 0.55f + 0.3f * ampMult * Mathf.Pow(1f - Mathf.Repeat(t, pulsePeriod) / pulsePeriod, 2f);
                if (t < fadeIn) a *= t / fadeIn;
                float remain = dur - t;
                if (remain < fadeOut) a *= remain / fadeOut;
                var c = sr.color;
                c.a = Mathf.Min(1f, a);
                sr.color = c;

                if (vortex) go.transform.Rotate(0, 0, 50f * Time.deltaTime);
                if (shake) go.transform.position = pos + new Vector3(Random.Range(-0.15f, 0.15f), 0f, Random.Range(-0.15f, 0.15f));
                yield return null;
            }
            Object.Destroy(go);
        }

        /// <summary>
        /// 岩浆池专属演出 — 稳定显示不闪烁, 缓慢旋转呈流动感, 长停留(≥4s)后自然淡出。
        /// </summary>
        private enum PoolMotion
        {
            RotateSlow, // 岩浆: 慢速旋转 (6°/s)
            Swirl,      // 炎风: 快速旋涡 (25°/s)
            Shake,      // 土: 地震抖动
            Ripple,     // 水: 波纹脉冲
            Jitter,     // 雷: 电弧颤动
            Shimmer,    // 冰: 微光流转
            Static,     // 静止
        }

        /// <summary>按属性映射池动态特色</summary>
        private static PoolMotion GetPoolMotion(Element el)
        {
            switch (el)
            {
                case Element.Fire: return PoolMotion.RotateSlow;
                case Element.FlameWind: return PoolMotion.Swirl;
                case Element.Earth: return PoolMotion.Shake;
                case Element.Water: return PoolMotion.Ripple;
                case Element.Thunder: return PoolMotion.Jitter;
                case Element.Frost: return PoolMotion.Shimmer;
                default: return PoolMotion.Static;
            }
        }

        private enum PoolMotion2 { Placeholder }

        private static System.Collections.IEnumerator PoolAnim(Vector3 pos, Sprite magma, float radius, float dur, PoolMotion motion, Sprite accentSprite = null, float accentRotateSpeed = 0f)
        {
            var go = new GameObject("MagmaPool");
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(65f, 0, 0);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = magma;
            sr.color = new Color(1f, 1f, 1f, 0f);
            sr.sortingOrder = 44;

            // 属性特色动态子层 (如炎风的青色旋流层, 运行时持续旋转, alpha跟随母体)
            SpriteRenderer accentSr = null;
            if (accentSprite != null)
            {
                var accentGo = new GameObject("PoolAccent");
                accentGo.transform.SetParent(go.transform, false);
                accentGo.transform.localPosition = Vector3.zero;
                accentGo.transform.localRotation = Quaternion.identity;
                var asr = accentGo.AddComponent<SpriteRenderer>();
                asr.sprite = accentSprite;
                asr.sortingOrder = sr.sortingOrder + 1;
                asr.color = new Color(1f, 1f, 1f, 0f);
                accentSr = asr;
            }

            const float fadeIn = 0.2f;
            const float fadeOut = 0.6f;
            Vector3 basePos = pos;
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                // 稳定alpha (不闪烁): 淡入→常驻0.95→尾部淡出
                float a = 0.95f;
                if (t < fadeIn) a *= t / fadeIn;
                float remain = dur - t;
                if (remain < fadeOut) a *= remain / fadeOut;

                // 按属性特色动态
                switch (motion)
                {
                    case PoolMotion.RotateSlow:
                        go.transform.Rotate(0, 0, 6f * Time.deltaTime, Space.Self);
                        break;
                    case PoolMotion.Swirl:
                        go.transform.Rotate(0, 0, 25f * Time.deltaTime, Space.Self);
                        break;
                    case PoolMotion.Shake:
                        go.transform.position = basePos + new Vector3(Random.Range(-0.15f, 0.15f), 0f, Random.Range(-0.15f, 0.15f));
                        break;
                    case PoolMotion.Ripple:
                        a = 0.75f + 0.2f * Mathf.Sin(t * 2f * Mathf.PI / 0.9f);
                        break;
                    case PoolMotion.Jitter:
                        go.transform.position = basePos + new Vector3(Random.Range(-0.06f, 0.06f), 0f, Random.Range(-0.06f, 0.06f));
                        a = Random.value > 0.5f ? 1f : 0.8f; // 电弧颤动
                        break;
                    case PoolMotion.Shimmer:
                        a = 0.9f + 0.05f * Mathf.Sin(t * 2f * Mathf.PI / 1.6f);
                        break;
                }

                var c = sr.color;
                c.a = Mathf.Clamp01(a);
                sr.color = c;

                // 属性子层: alpha跟随母体 + 炎风旋流层持续旋转
                if (accentSr != null)
                {
                    accentSr.transform.Rotate(0, 0, accentRotateSpeed * Time.deltaTime, Space.Self);
                    var ac = accentSr.color;
                    ac.a = sr.color.a;
                    accentSr.color = ac;
                }
                yield return null;
            }
            Object.Destroy(go);
        }

        /// <summary>雷(雷泽)/风/炎风地块: GroundPatchFrames帧循环动画 (10fps, 淡入0.15/淡出0.4)</summary>
        private static System.Collections.IEnumerator GroundPatchFramesAnim(Vector3 pos, Sprite[] frames, float radius, float dur)
        {
            var go = new GameObject("AffixGroundPatchAnim");
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(65f, 0, 0);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = frames[0];
            sr.sortingOrder = 44;

            // 世界尺寸=伤害区直径(2×radius)
            float unitWidth = frames[0].rect.width / frames[0].pixelsPerUnit;
            float scale = (radius * 2f) / unitWidth;
            go.transform.localScale = new Vector3(scale, scale, 1);

            const float fps = 10f;
            const float fadeIn = 0.15f;
            const float fadeOut = 0.4f;
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                if (sr != null)
                {
                    sr.sprite = frames[(int)(t * fps) % frames.Length];
                    float a = 0.85f;
                    if (t < fadeIn) a *= t / fadeIn;
                    float remain = dur - t;
                    if (remain < fadeOut) a *= remain / fadeOut;
                    var c = sr.color;
                    c.a = a;
                    sr.color = c;
                }
                yield return null;
            }
            Object.Destroy(go);
        }

        // ── 白闪: 穿透/弹射/剑阵 (0.1s闪白) ──
        public static void PlayFlashWhite(SpriteRenderer sr, float dur = 0.1f)
        {
            if (sr == null) return;
            var runner = FxRoot.AddComponent<AffixFxRunner>();
            runner.Run(FlashAnim(sr, dur));
        }

        private static System.Collections.IEnumerator FlashAnim(SpriteRenderer sr, float dur)
        {
            Color orig = sr.color;
            sr.color = Color.white;
            float t = 0f;
            while (t < dur && sr != null)
            {
                t += Time.deltaTime;
                float p = t / dur;
                sr.color = Color.Lerp(Color.white, orig, p);
                yield return null;
            }
            if (sr != null) sr.color = orig;
        }

        // ── 吸灵飞点: 3个白点从敌人飞向玩家 ──
        public static void PlayAbsorbDots(Vector3 from, Vector3 toPlayer)
        {
            RunFx(AbsorbDotsAnim(from, toPlayer));
        }

        private static System.Collections.IEnumerator AbsorbDotsAnim(Vector3 from, Vector3 to)
        {
            // 用小SpriteRenderer白点
            var tex = Texture2D.whiteTexture;
            var dotSprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);

            for (int i = 0; i < 3; i++)
            {
                var go = new GameObject("AbsorbDot");
                go.transform.position = from + new Vector3(Random.Range(-0.5f, 0.5f), Random.Range(0.5f, 1f), Random.Range(-0.5f, 0.5f));
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = dotSprite;
                sr.color = new Color(0.9f, 0.95f, 1f, 0.9f);
                sr.sortingOrder = 50;
                go.transform.localScale = new Vector3(0.3f, 0.3f, 1);

                // 独立协程飞向玩家
                RunFx(DotFly(go.transform, to, 0.3f + i * 0.1f));
            }
            yield break;
        }

        private static System.Collections.IEnumerator DotFly(Transform dot, Vector3 target, float dur)
        {
            Vector3 start = dot.position;
            float t = 0f;
            while (t < dur && dot != null)
            {
                t += Time.deltaTime;
                float p = t / dur;
                dot.position = Vector3.Lerp(start, target, p * p); // 加速飞向
                yield return null;
            }
            if (dot != null) Object.Destroy(dot.gameObject);
        }

        // ── 星爆: 雷暴/斩杀触发 (星形放大+淡出) ──
        public static void PlayStarPop(Vector3 pos, Element el, float scale = 1f)
        {
            EnsureSprites();
            if (_critStar == null) return;
            Color tint = ElementColors.GetTint(el);
            RunFx(StarPopAnim(pos, tint, scale));
        }

        /// <summary>星爆(自定义颜色) — 连杀金色标记等</summary>
        public static void PlayStarPop(Vector3 pos, Color color, float scale = 1f)
        {
            EnsureSprites();
            if (_critStar == null) return;
            RunFx(StarPopAnim(pos, color, scale));
        }

        private static System.Collections.IEnumerator StarPopAnim(Vector3 pos, Color tint, float scale)
        {
            var go = new GameObject("AffixStarPop");
            go.transform.position = pos + new Vector3(0, 1f, 0);
            go.transform.rotation = Quaternion.Euler(65f, 0, 0);
            go.AddComponent<SpriteRenderer>().sprite = _critStar;
            var sr = go.GetComponent<SpriteRenderer>();
            sr.color = new Color(tint.r, tint.g, tint.b, 1f);
            sr.sortingOrder = 55;

            float dur = 0.3f;
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float p = t / dur;
                float s = Mathf.Lerp(scale * 0.5f, scale * 1.5f, p);
                go.transform.localScale = new Vector3(s, s, 1);
                go.transform.Rotate(0, 0, 120f * Time.deltaTime);
                Color c = sr.color;
                c.a = Mathf.Lerp(1f, 0f, p);
                sr.color = c;
                yield return null;
            }
            Object.Destroy(go);
        }

        // ── 剑气扩散: 剑气/切裂词缀 (SlashEffect放大+旋转+淡出) ──
        public static void PlaySlashWave(Vector3 pos, Element el, float radius, float dur = 0.35f)
        {
            EnsureSprites();
            if (_slashSprite == null) { PlayBurst(pos, el, radius, dur); return; }
            Color tint = ElementColors.GetTint(el);
            RunFx(SlashWaveAnim(pos, tint, radius, dur));
        }

        private static System.Collections.IEnumerator SlashWaveAnim(Vector3 pos, Color tint, float radius, float dur)
        {
            var go = new GameObject("AffixSlashWave");
            go.transform.position = pos + new Vector3(0, 0.8f, 0);
            go.transform.rotation = Quaternion.Euler(65f, 0, Random.Range(0f, 360f));
            go.AddComponent<SpriteRenderer>().sprite = _slashSprite;
            var sr = go.GetComponent<SpriteRenderer>();
            sr.color = new Color(tint.r, tint.g, tint.b, 0.95f);
            sr.sortingOrder = 52;

            float unitWidth = _slashSprite.rect.width / _slashSprite.pixelsPerUnit;
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float p = t / dur;
                float worldSize = Mathf.Lerp(radius * 0.4f, radius, p);
                float scale = worldSize / unitWidth;
                go.transform.localScale = new Vector3(scale, scale, 1);
                go.transform.Rotate(0, 0, 90f * Time.deltaTime);
                Color c = sr.color;
                c.a = Mathf.Lerp(0.95f, 0f, p * p);
                sr.color = c;
                yield return null;
            }
            Object.Destroy(go);
        }

        // ── 切裂闪现: 敌人身上随机角度剑气闪过 ──
        public static void PlaySlashFlash(Vector3 pos, Element el, float scale = 1.2f)
        {
            EnsureSprites();
            if (_slashSprite == null) return;
            Color tint = ElementColors.GetTint(el);
            RunFx(SlashFlashAnim(pos, tint, scale));
        }

        private static System.Collections.IEnumerator SlashFlashAnim(Vector3 pos, Color tint, float scale)
        {
            var go = new GameObject("AffixSlashFlash");
            go.transform.position = pos + new Vector3(0, 1f, 0);
            go.transform.rotation = Quaternion.Euler(65f, 0, Random.Range(-40f, 40f));
            go.AddComponent<SpriteRenderer>().sprite = _slashSprite;
            var sr = go.GetComponent<SpriteRenderer>();
            sr.color = new Color(tint.r, tint.g, tint.b, 1f);
            sr.sortingOrder = 56;
            go.transform.localScale = new Vector3(scale, scale, 1);

            float dur = 0.18f;
            float t = 0f;
            while (t < dur && go != null)
            {
                t += Time.deltaTime;
                float p = t / dur;
                float s = Mathf.Lerp(scale * 1.3f, scale * 0.8f, p);
                go.transform.localScale = new Vector3(s, s, 1);
                Color c = sr.color;
                c.a = Mathf.Lerp(1f, 0f, p);
                sr.color = c;
                yield return null;
            }
            if (go != null) Object.Destroy(go);
        }

        // ── 电弧飞行: 连锁词缀 (紫色电点从A快速飞到B) ──
        public static void PlayLightningArc(Vector3 from, Vector3 to)
        {
            EnsureSprites();
            if (_critStar == null) return;
            RunFx(LightningArcAnim(from, to));
        }

        private static System.Collections.IEnumerator LightningArcAnim(Vector3 from, Vector3 to)
        {
            var go = new GameObject("AffixArcDot");
            go.transform.position = from + new Vector3(0, 1f, 0);
            go.transform.rotation = Quaternion.Euler(65f, 0, 0);
            go.AddComponent<SpriteRenderer>().sprite = _critStar;
            var sr = go.GetComponent<SpriteRenderer>();
            sr.color = new Color(0.75f, 0.4f, 1f, 1f); // 紫电
            sr.sortingOrder = 54;
            go.transform.localScale = new Vector3(0.5f, 0.5f, 1);

            float dur = 0.09f;
            Vector3 start = go.transform.position;
            Vector3 end = to + new Vector3(0, 1f, 0);
            float t = 0f;
            while (t < dur && go != null)
            {
                t += Time.deltaTime;
                float p = t / dur;
                go.transform.position = Vector3.Lerp(start, end, p);
                go.transform.Rotate(0, 0, 720f * Time.deltaTime); // 高速旋转的电花
                yield return null;
            }
            // 到达处小电花爆点
            if (go != null)
            {
                var c = sr.color;
                c.a = 0f;
                sr.color = c;
            }
            if (go != null) Object.Destroy(go, 0.05f);
        }

        // ── 护体光环: 玩家常驻旋转法阵圈 (护体词缀) ──
        private static GameObject _shieldAura;
        /// <summary>光环所有权 — 只有拥有护体词缀的技能可以创建/销毁, 其他技能的Tick不得干涉</summary>
        public static object ShieldAuraOwner { get; set; }

        public static void PlayShieldAura(Transform player)
        {
            if (_shieldAura != null)
            {
                // 已存在: 只确保跟随目标正确
                if (_shieldAura.transform.parent != player)
                    _shieldAura.transform.SetParent(player, false);
                return;
            }
            EnsureSprites();
            if (_spawnCircle == null || player == null) return;
            // 护盾罩贴图优先, 缺失回退法阵图
            Sprite shieldSpr = _shieldDome != null ? _shieldDome : _spawnCircle;
            _shieldAura = new GameObject("AffixShieldAura");
            _shieldAura.transform.SetParent(player, false);
            _shieldAura.transform.localPosition = new Vector3(0, 0.1f, 0);
            _shieldAura.transform.rotation = Quaternion.Euler(65f, 0, 0);
            var sr = _shieldAura.AddComponent<SpriteRenderer>();
            sr.sprite = shieldSpr;
            sr.color = new Color(0.9f, 0.82f, 0.55f, 0.4f); // 土金护罩色
            sr.sortingOrder = 42;
            float unitWidth = shieldSpr.rect.width / shieldSpr.pixelsPerUnit;
            float scale = 4.5f / unitWidth;
            _shieldAura.transform.localScale = new Vector3(scale, scale, 1);
            RunFx(ShieldAuraSpin());
        }

        public static void StopShieldAura()
        {
            ShieldAuraOwner = null;
            if (_shieldAura != null) Object.Destroy(_shieldAura);
            _shieldAura = null;
        }

        private static System.Collections.IEnumerator ShieldAuraSpin()
        {
            while (_shieldAura != null)
            {
                _shieldAura.transform.Rotate(0, 0, 40f * Time.deltaTime, Space.Self);
                yield return null;
            }
        }

        // ── 闪电束: 连锁词缀 (两端点拉伸+闪烁, 贴图缺失回退电点飞行) ──
        public static void PlayLightningBeam(Vector3 from, Vector3 to)
        {
            EnsureSprites();
            if (_lightningBeam == null) { PlayLightningArc(from, to); return; }
            RunFx(LightningBeamAnim(from, to));
        }

        private static System.Collections.IEnumerator LightningBeamAnim(Vector3 from, Vector3 to)
        {
            Vector3 a = from + new Vector3(0, 1.2f, 0);
            Vector3 b = to + new Vector3(0, 1.2f, 0);
            Vector3 mid = (a + b) * 0.5f;

            // 闪3次(每次随机中点弯曲偏移模拟电弧抖动)
            for (int i = 0; i < 3; i++)
            {
                var go = new GameObject("AffixLightningBeam");
                Vector3 jitter = new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f));
                go.transform.position = mid + jitter;
                go.transform.rotation = Quaternion.Euler(65f, 0, 0);

                // 朝向: XZ平面A→B方向
                Vector3 dir = b - a; dir.y = 0f;
                float yaw = Mathf.Atan2(dir.z, dir.x) * Mathf.Rad2Deg;
                go.transform.Rotate(0, -yaw, 0, Space.Self);

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = _lightningBeam;
                sr.color = new Color(0.85f, 0.65f, 1f, 1f); // 亮紫电
                sr.sortingOrder = 54;

                // 长度拉伸: 贴图横向覆盖A→B距离, 宽度≈距离的1/4
                float dist = Vector3.Distance(a, b);
                float unitW = _lightningBeam.bounds.size.x;
                float unitH = _lightningBeam.bounds.size.y;
                float sx = dist / unitW;
                float sy = Mathf.Max(dist * 0.25f, 1.5f) / unitH;
                go.transform.localScale = new Vector3(sx, sy, 1);

                yield return new WaitForSeconds(0.05f);
                if (go != null) Object.Destroy(go);
                yield return new WaitForSeconds(0.02f);
            }
        }

        // ── 冰晶: 冻结词缀 (冰晶块弹出+旋转淡出, 缺失回退星爆) ──
        public static void PlayIceCrystal(Vector3 pos, float scale = 1.5f)
        {
            EnsureSprites();
            if (_iceCrystal == null) { PlayStarPop(pos, Element.Water, scale); return; }
            RunFx(IceCrystalAnim(pos, scale));
        }

        private static System.Collections.IEnumerator IceCrystalAnim(Vector3 pos, float scale)
        {
            var go = new GameObject("AffixIceCrystal");
            go.transform.position = pos + new Vector3(0, 1f, 0);
            go.transform.rotation = Quaternion.Euler(65f, 0, Random.Range(0f, 360f));
            go.AddComponent<SpriteRenderer>().sprite = _iceCrystal;
            var sr = go.GetComponent<SpriteRenderer>();
            sr.color = new Color(0.7f, 0.9f, 1f, 1f); // 冰蓝
            sr.sortingOrder = 54;
            go.transform.localScale = new Vector3(0.2f, 0.2f, 1);

            // 弹出放大→停顿→碎裂淡出
            float popIn = 0.12f, hold = 0.5f, fade = 0.35f;
            float t = 0f;
            while (t < popIn)
            {
                t += Time.deltaTime;
                float p = t / popIn;
                float s = Mathf.Lerp(0.2f, scale * 1.15f, p);
                go.transform.localScale = new Vector3(s, s, 1);
                yield return null;
            }
            go.transform.localScale = new Vector3(scale, scale, 1);
            t = 0f;
            while (t < hold) { t += Time.deltaTime; go.transform.Rotate(0, 0, 30f * Time.deltaTime); yield return null; }
            t = 0f;
            while (t < fade && go != null)
            {
                t += Time.deltaTime;
                float p = t / fade;
                float s = Mathf.Lerp(scale, scale * 1.4f, p);
                go.transform.localScale = new Vector3(s, s, 1);
                Color c = sr.color;
                c.a = Mathf.Lerp(1f, 0f, p);
                sr.color = c;
                yield return null;
            }
            if (go != null) Object.Destroy(go);
        }

        // ── 属性VFX帧爆发: 麻痹(ThunderVFX)/震击尘土(EarthVFX)/燃传播(FireVFX) ──
        private static readonly Dictionary<Element, Sprite[]> _vfxFramesCache = new Dictionary<Element, Sprite[]>();

        /// <summary>获取属性VFX粒子帧 ({el}VFX_Frames, 16帧)</summary>
        public static Sprite[] GetVfxFrames(Element el)
        {
            Sprite[] frames;
            if (_vfxFramesCache.TryGetValue(el, out frames)) return frames;
            frames = Resources.LoadAll<Sprite>(el + "VFX_Frames");
            if (frames != null && frames.Length > 0)
                System.Array.Sort(frames, (a, b) => a.name.CompareTo(b.name));
            else
                frames = null;
            _vfxFramesCache[el] = frames;
            return frames;
        }

        /// <summary>属性VFX帧播放 (雷=电弧缠绕/土=尘土/火=飞焰), 缺帧回退Burst圆环</summary>
        public static void PlayVfxBurst(Vector3 pos, Element el, float worldSize = 3f, float dur = 0.8f)
        {
            var frames = GetVfxFrames(el);
            if (frames == null || frames.Length == 0)
            {
                PlayBurst(pos, el, worldSize * 0.6f, 0.3f);
                return;
            }
            RunFx(VfxBurstRoutine(pos, frames, worldSize, dur));
        }

        private static System.Collections.IEnumerator VfxBurstRoutine(Vector3 pos, Sprite[] frames, float worldSize, float dur)
        {
            var go = new GameObject("AffixVfxBurst");
            go.transform.position = pos + new Vector3(0, 1f, 0);
            go.transform.rotation = Quaternion.Euler(65f, 0, 0);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = frames[0];
            sr.color = Color.white;
            sr.sortingOrder = 53;

            float unitW = frames[0].rect.width / frames[0].pixelsPerUnit;
            float sc = worldSize / unitW;
            go.transform.localScale = new Vector3(sc, sc, 1);

            var anim = go.AddComponent<VFXFrameAnimator>();
            anim.frames = frames;
            anim.fps = 16f;
            anim.loop = true;
            anim.alpha = 0.9f;

            float t = 0f;
            while (t < dur) { t += Time.deltaTime; yield return null; }

            // 淡出
            float fade = 0.25f;
            t = 0f;
            while (t < fade && go != null)
            {
                t += Time.deltaTime;
                float p = t / fade;
                float a = 0.9f * (1f - p);
                anim.alpha = a;
                Color c = sr.color;
                c.a = a;
                sr.color = c;
                yield return null;
            }
            if (go != null) Object.Destroy(go);
        }

        // ── 小风刃贴图 (风刃词缀投射物用, 缺失回退WindOrb) ──
        public static Sprite GetWindBladeSprite()
        {
            EnsureSprites();
            if (_windBladeSpr != null) return _windBladeSpr;
            return Resources.Load<Sprite>("WindOrb");
        }

        // ── 斩杀标记: 敌人头顶红剑开关 ──
        private static readonly Dictionary<int, GameObject> _executeMarks = new Dictionary<int, GameObject>();

        public static void PlayExecuteMark(Transform enemy, bool on)
        {
            int id = enemy.GetInstanceID();
            if (on)
            {
                if (_executeMarks.ContainsKey(id)) return;
                EnsureSprites();
                if (_critStar == null) return;

                var go = new GameObject("ExecuteMark");
                go.transform.SetParent(enemy, false);
                go.transform.localPosition = new Vector3(0, 2.5f, 0);
                go.transform.rotation = Quaternion.Euler(65f, 0, 0);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = _critStar;
                sr.color = new Color(1f, 0.2f, 0.15f, 0.9f); // 红色
                sr.sortingOrder = 55;
                go.transform.localScale = new Vector3(0.8f, 0.8f, 1);
                _executeMarks[id] = go;
            }
            else
            {
                GameObject go;
                if (_executeMarks.TryGetValue(id, out go))
                {
                    if (go != null) Object.Destroy(go);
                    _executeMarks.Remove(id);
                }
            }
        }
    }

    /// <summary>特效协程Runner — 挂在AffixFxRoot上, 自毁式</summary>
    public class AffixFxRunner : MonoBehaviour
    {
        private bool _running = false;

        public void Run(System.Collections.IEnumerator routine)
        {
            if (_running) return; // 防重复
            _running = true;
            StartCoroutine(Wrap(routine));
        }

        private System.Collections.IEnumerator Wrap(System.Collections.IEnumerator routine)
        {
            yield return routine;
            Destroy(this); // 协程结束自毁组件
        }
    }
}
