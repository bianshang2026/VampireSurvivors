using UnityEngine;

namespace BiopunkRV
{
    /// <summary>天降坠落风格 — 决定轨迹与朝向</summary>
    public enum FallStyle
    {
        Ball,   // 球体(灵球/火球/陨石): 两侧斜上方飞向目标, 旋转随弹道
        Sword,  // 剑: 从目标正上方垂直坠落, 固定姿态(剑尖朝下对准敌人)
        Blade   // 月牙刃(灵气刃): 两侧斜上方飞向目标, 凸面(背面)始终对准敌人
    }

    /// <summary>
    /// 天降打击演出工具 — 坠落词缀/神通共用的"预警→坠落→落地爆发"三段式演出。
    /// 预警圈: 属性色法阵从中心展开并脉冲, 末端闪白;
    /// 坠落体: 按FallStyle决定轨迹(球/刃=两侧斜飞, 剑=垂直下落)与朝向(尖端/凸面对准敌人);
    /// 落地: 属性VFX爆发+冲击环+属性地块残留(仅视觉)。
    /// 伤害由调用方在 onImpact 回调里自行处理。
    /// </summary>
    public static class SkyfallStrike
    {
        /// <summary>飞剑贴图缓存 (FlyingSword.Awake 注册, 万剑天降用)</summary>
        public static Sprite SwordSprite;

        /// <summary>剑垂直下落的固定姿态 (2026-09-01用户旋转火精演示的参考姿势: 剑尖对准敌人)</summary>
        public static readonly Quaternion SwordFallRotation = Quaternion.Euler(49.32f, 353.29f, 43.36f);

        // 精灵平面基向量 (rotation Euler(65.69891,0,0) 的 local +X/+Y 在世界空间的方向)
        private static readonly Vector3 PlaneX = new Vector3(1f, 0f, 0f);
        private static readonly Vector3 PlaneY = new Vector3(0f, 0.4115f, 0.9114f);

        private static GameObject _root;
        private static Sprite _warnCircle;

        private static GameObject Root
        {
            get
            {
                if (_root == null)
                {
                    _root = new GameObject("SkyfallFxRoot");
                    Object.DontDestroyOnLoad(_root);
                }
                return _root;
            }
        }

        private static void RunFx(System.Collections.IEnumerator routine)
        {
            var runner = Root.AddComponent<AffixFxRunner>();
            runner.Run(routine);
        }

        /// <summary>飞行方向投影到精灵平面后的角度(deg, 相对local +X)</summary>
        private static float FlightAngleInPlane(Vector3 from, Vector3 to)
        {
            Vector3 dir = to - from;
            if (dir.sqrMagnitude < 0.0001f) return -90f; // 垂直下落
            dir.Normalize();
            float px = Vector3.Dot(dir, PlaneX);
            float py = Vector3.Dot(dir, PlaneY);
            return Mathf.Atan2(py, px) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// 完整天降演出 (协程驱动, 不阻塞调用方)。
        /// </summary>
        /// <param name="targetPos">落点(地面位置)</param>
        /// <param name="sprite">坠落体静态贴图(animFrames为空时用)</param>
        /// <param name="animFrames">坠落体序列帧(可选, 优先于sprite)</param>
        /// <param name="tint">静态贴图染色(序列帧本身带色, 用白)</param>
        /// <param name="el">属性(预警圈/地块残留颜色)</param>
        /// <param name="worldSize">坠落体世界尺寸</param>
        /// <param name="fallHeight">坠落起始高度(相对落点Y)</param>
        /// <param name="warnTime">预警圈时长</param>
        /// <param name="fallTime">坠落时长</param>
        /// <param name="aoeRadius">预警圈/落地视觉半径</param>
        /// <param name="onImpact">落地回调(伤害由调用方处理)</param>
        /// <param name="style">坠落风格(球/剑/月牙刃)</param>
        /// <param name="delay">起始延迟(神通波次用)</param>
        /// <param name="leavePatch">落地是否留属性地块残留</param>
        /// <param name="trackTarget">追踪目标(预警+悬浮+坠落全程跟随, 死亡后停在最后位置)</param>
        /// <param name="hoverTime">悬浮时长(神通演出: 生成后在天上停驻, 再落下; 0=直接落)</param>
        public static void Strike(
            Vector3 targetPos,
            Sprite sprite,
            Sprite[] animFrames,
            Color tint,
            Element el,
            float worldSize,
            float fallHeight,
            float warnTime,
            float fallTime,
            float aoeRadius,
            System.Action<Vector3> onImpact,
            FallStyle style = FallStyle.Ball,
            float delay = 0f,
            bool leavePatch = true,
            Transform trackTarget = null,
            float hoverTime = 0f)
        {
            if (sprite == null && (animFrames == null || animFrames.Length == 0)) return;
            RunFx(StrikeRoutine(targetPos, sprite, animFrames, tint, el, worldSize,
                fallHeight, warnTime, fallTime, aoeRadius, onImpact, style, delay, leavePatch, trackTarget, hoverTime));
        }

        /// <summary>目标存活判定 (已销毁/失活=不可追踪)</summary>
        private static bool IsAlive(Transform t)
        {
            return t != null && t.gameObject != null && t.gameObject.activeInHierarchy;
        }

        /// <summary>创建预警圈 (调用方用PlayWarning驱动动画, 结束后自行销毁)</summary>
        public static GameObject CreateWarning(Vector3 pos, Element el, out SpriteRenderer sr, out float unit)
        {
            sr = null; unit = 1f;
            if (_warnCircle == null)
                _warnCircle = Resources.Load<Sprite>("SpawnCircle");
            if (_warnCircle == null) return null;

            var go = new GameObject("SkyfallWarning");
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(65f, 0f, 0f);
            sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _warnCircle;
            Color c = ElementColors.GetTint(el);
            sr.color = new Color(c.r, c.g, c.b, 0.55f);
            sr.sortingOrder = 44;
            unit = _warnCircle.rect.width / _warnCircle.pixelsPerUnit;
            return go;
        }

        /// <summary>预警圈动画: 缩放展开+脉冲, 末端闪白 (不销毁go; 传trackTarget则跟随目标)</summary>
        public static System.Collections.IEnumerator PlayWarning(GameObject go, SpriteRenderer sr, float unit, float radius, float warnTime, Transform trackTarget = null)
        {
            float t = 0f;
            while (t < warnTime)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / warnTime);
                if (go != null)
                {
                    if (IsAlive(trackTarget))
                        go.transform.position = trackTarget.position; // 追踪: 预警圈跟随目标
                    float s = (radius * Mathf.Lerp(0.15f, 1f, p)) / unit;
                    go.transform.localScale = new Vector3(s, s, 1f);
                    // 脉冲: 后半段加速闪烁提示即将落下
                    float pulse = p < 0.6f ? 0.55f : 0.55f + 0.35f * Mathf.Abs(Mathf.Sin(t * 18f));
                    Color wc = sr.color;
                    wc.a = pulse;
                    sr.color = wc;
                }
                yield return null;
            }
            // 预警结束闪白一瞬
            if (sr != null)
                sr.color = new Color(1f, 1f, 1f, 0.8f);
        }

        private static System.Collections.IEnumerator StrikeRoutine(
            Vector3 targetPos, Sprite sprite, Sprite[] animFrames, Color tint, Element el,
            float worldSize, float fallHeight, float warnTime, float fallTime, float aoeRadius,
            System.Action<Vector3> onImpact, FallStyle style, float delay, bool leavePatch, Transform trackTarget, float hoverTime)
        {
            if (delay > 0f)
                yield return new WaitForSeconds(delay);

            // 剑: 在预警追踪改写targetPos之前捕获阵型偏移(调用方落点相对敌人的水平偏移)
            Vector3 formationOffset = Vector3.zero;
            if (style == FallStyle.Sword && IsAlive(trackTarget))
            {
                formationOffset = targetPos - trackTarget.position;
                formationOffset.y = 0f;
            }

            // ── 阶段1: 预警圈 (缩放展开+脉冲, 坠落体后半段开始下落; 追踪活目标) ──
            SpriteRenderer warnSr;
            float warnUnit;
            GameObject warnGo = CreateWarning(targetPos, el, out warnSr, out warnUnit);

            GameObject body = null;
            float bodyStart = warnTime * 0.5f;
            float elapsed = 0f;
            while (elapsed < warnTime)
            {
                elapsed += Time.deltaTime;
                float p = Mathf.Clamp01(elapsed / warnTime);
                if (IsAlive(trackTarget))
                {
                    targetPos = trackTarget.position; // 追踪: 落点跟随目标
                    if (warnGo != null) warnGo.transform.position = targetPos;
                }
                if (warnGo != null)
                {
                    float s = (aoeRadius * Mathf.Lerp(0.15f, 1f, p)) / warnUnit;
                    warnGo.transform.localScale = new Vector3(s, s, 1f);
                    float pulse = p < 0.6f ? 0.55f : 0.55f + 0.35f * Mathf.Abs(Mathf.Sin(elapsed * 18f));
                    Color wc = warnSr.color;
                    wc.a = pulse;
                    warnSr.color = wc;
                }
                if (body == null && elapsed >= bodyStart)
                    body = CreateFallingBody(targetPos, sprite, animFrames, tint, el, worldSize, fallHeight, style);
                yield return null;
            }
            if (body == null)
                body = CreateFallingBody(targetPos, sprite, animFrames, tint, el, worldSize, fallHeight, style);

            if (warnSr != null)
                warnSr.color = new Color(1f, 1f, 1f, 0.8f);

            // ── 阶段1.5: 悬浮 (神通演出: 天上聚集停驻, 再落下) ──
            if (hoverTime > 0f && body != null)
            {
                Vector3 hoverBase = body.transform.position;
                Color warnTint = ElementColors.GetTint(el);
                float ht = 0f;
                while (ht < hoverTime)
                {
                    ht += Time.deltaTime;
                    if (IsAlive(trackTarget))
                    {
                        targetPos = trackTarget.position;
                        if (warnGo != null) warnGo.transform.position = targetPos;
                    }
                    float bob = Mathf.Sin(ht * 5f) * 0.35f;
                    if (style == FallStyle.Sword)
                    {
                        // 剑: 阵型编队悬停在目标上空 (保持编队偏移, 轻微浮动)
                        body.transform.position = new Vector3(targetPos.x + formationOffset.x, targetPos.y + fallHeight + bob, targetPos.z + formationOffset.z);
                    }
                    else
                    {
                        body.transform.position = new Vector3(hoverBase.x, hoverBase.y + bob, hoverBase.z);
                    }
                    // 预警圈保持属性色脉冲
                    if (warnGo != null && warnSr != null)
                    {
                        float pulse = 0.5f + 0.25f * Mathf.Abs(Mathf.Sin(ht * 8f));
                        warnSr.color = new Color(warnTint.r, warnTint.g, warnTint.b, pulse);
                    }
                    yield return null;
                }
            }

            // ── 阶段2: 坠落 (重力加速 ease-in; 剑=垂直, 球/刃=两侧斜飞; 追踪活目标) ──
            Vector3 start = body.transform.position;
            Vector3 ground = new Vector3(targetPos.x, targetPos.y, targetPos.z);
            float ft = 0f;
            while (ft < fallTime)
            {
                ft += Time.deltaTime;
                float p = Mathf.Clamp01(ft / fallTime);
                if (IsAlive(trackTarget))
                    ground = trackTarget.position; // 追踪: 坠落过程跟随目标
                body.transform.position = Vector3.Lerp(start, ground, p * p); // p²=加速
                if (style == FallStyle.Blade)
                {
                    // 月牙刃: 凸面(贴图-X, 180°)始终对准敌人(飞行方向)
                    float z = FlightAngleInPlane(body.transform.position, ground) - 180f;
                    body.transform.rotation = Quaternion.Euler(65.69891f, 0f, z);
                }
                // 剑=固定姿态(创建时已设), 球体=圆形旋转无感不处理
                yield return null;
            }
            body.transform.position = ground;

            // ── 阶段3: 落地爆发 (用追踪后的最终落点) ──
            Object.Destroy(body);
            if (warnGo != null) Object.Destroy(warnGo);

            if (onImpact != null)
                onImpact(ground);

            // 落地视觉: 属性VFX爆发 + 冲击环 + 属性地块残留
            AffixVisuals.PlayBurst(ground, el, aoeRadius * 1.1f, 0.3f);
            AffixVisuals.PlayVfxBurst(ground, el, aoeRadius * 1.6f, 0.7f);
            if (leavePatch)
                AffixVisuals.PlayGroundPatch(ground, el, aoeRadius * 0.8f, 1.5f);
        }

        /// <summary>生成坠落体 — 剑=正上方垂直+固定姿态, 球/刃=两侧斜上方出发</summary>
        private static GameObject CreateFallingBody(
            Vector3 targetPos, Sprite sprite, Sprite[] animFrames, Color tint, Element el,
            float worldSize, float fallHeight, FallStyle style)
        {
            var go = new GameObject("SkyfallBody");

            if (style == FallStyle.Sword)
            {
                // 剑: 从目标正上方出发, 固定参考姿态(剑尖朝下对准敌人)
                go.transform.position = new Vector3(targetPos.x, targetPos.y + fallHeight, targetPos.z);
                go.transform.rotation = SwordFallRotation;
            }
            else
            {
                // 球/刃: 左右随机一侧斜上方出发
                float sideSign = Random.value < 0.5f ? -1f : 1f;
                float lateralOffset = fallHeight * 0.7f * sideSign;
                Vector3 spawnPos = new Vector3(targetPos.x + lateralOffset, targetPos.y + fallHeight, targetPos.z);
                go.transform.position = spawnPos;
                if (style == FallStyle.Blade)
                {
                    // 月牙刃初始朝向: 凸面对准目标
                    float z = FlightAngleInPlane(spawnPos, targetPos) - 180f;
                    go.transform.rotation = Quaternion.Euler(65.69891f, 0f, z);
                }
                else
                {
                    go.transform.rotation = Quaternion.Euler(65.69891f, 0f, 0f);
                }
            }

            var sr = go.AddComponent<SpriteRenderer>();
            bool useFrames = animFrames != null && animFrames.Length > 0;
            sr.sprite = useFrames ? animFrames[0] : sprite;
            sr.color = useFrames ? Color.white : tint;
            sr.sortingOrder = 46;

            // 世界尺寸归一化 (不同分辨率贴图视觉大小一致)
            float unit = sr.sprite != null && sr.sprite.pixelsPerUnit > 0
                ? sr.sprite.rect.width / sr.sprite.pixelsPerUnit : 1f;
            float s = unit > 0.01f ? worldSize / unit : worldSize;
            go.transform.localScale = new Vector3(s, s, 1f);

            if (useFrames)
            {
                var anim = go.AddComponent<VFXFrameAnimator>();
                anim.frames = animFrames;
                anim.fps = 12f;
                anim.loop = true;
                anim.alpha = 1f;
            }

            // 坠落光晕 + 拖尾 (遮天蔽日批量时省略光晕保性能)
            _bodiesAlive++;
            if (_bodiesAlive <= 20)
            {
                Light glow = go.AddComponent<Light>();
                glow.type = LightType.Point;
                glow.color = ElementColors.GetGlow(el);
                glow.intensity = 2f;
                glow.range = worldSize * 1.5f;
                go.AddComponent<SkyfallBodyLight>().OnDestroyed += () => _bodiesAlive--;
            }
            else
            {
                go.AddComponent<SkyfallBodyLight>().OnDestroyed += () => _bodiesAlive--;
            }

            TrailRenderer trail = go.AddComponent<TrailRenderer>();
            trail.time = 0.15f;
            trail.startWidth = worldSize * 0.18f;
            trail.endWidth = 0f;
            Color tc = ElementColors.GetTrail(el);
            trail.startColor = new Color(tc.r, tc.g, tc.b, 0.7f);
            trail.endColor = new Color(tc.r, tc.g, tc.b, 0f);
            trail.material = new Material(Shader.Find("Sprites/Default"));

            return go;
        }

        /// <summary>存活天降体计数 (销毁时回调递减, 用于光晕性能上限)</summary>
        private static int _bodiesAlive;
    }

    /// <summary>天降体生命周期回调组件 (销毁时递减计数)</summary>
    internal class SkyfallBodyLight : MonoBehaviour
    {
        public event System.Action OnDestroyed;
        private void OnDestroy() { if (OnDestroyed != null) OnDestroyed(); }
    }
}
