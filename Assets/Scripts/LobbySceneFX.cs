using UnityEngine;

/// <summary>
/// 大厅场景动态效果驱动：云海漂移、瀑布UV流动、宝塔符文环旋转、灵气弧线呼吸。
/// 所有参数在 Inspector 中可调；对象由编辑器脚本一次性布置。
/// </summary>
public class LobbySceneFX : MonoBehaviour
{
    [Header("云海层（每层两个拷贝横向循环）")]
    public Transform[] clouds;
    public float[] cloudSpeeds;
    public float cloudSpan = 48f;
    public float cloudMinX = -21f;

    [Header("瀑布材质（_MainTex UV向下滚动）")]
    public Material[] waterfalls;
    public float waterfallSpeed = 0.55f;

    [Header("符文环（绕世界Y轴旋转）")]
    public Transform[] rings;
    public float[] ringSpeeds;

    [Header("灵气弧线（呼吸明暗）")]
    public SpriteRenderer[] arcs;
    public float pulseSpeed = 1.1f;
    public float pulseMin = 0.10f;
    public float pulseMax = 0.40f;

    [Header("浮空法阵（上下缓浮+发光呼吸）")]
    public Transform[] floaters;
    public float floatAmplitude = 0.12f;
    public float floatFrequency = 0.45f;
    public SpriteRenderer[] floaterGlows;
    public float glowMin = 0.05f;
    public float glowMax = 0.35f;

    float _wfOffset;
    float[] _floaterBaseY;

    void Start()
    {
        if (floaters == null) return;
        _floaterBaseY = new float[floaters.Length];
        for (int i = 0; i < floaters.Length; i++)
            if (floaters[i] != null) _floaterBaseY[i] = floaters[i].position.y;
    }

    void Update()
    {
        float dt = Time.deltaTime;

        if (clouds != null)
        {
            for (int i = 0; i < clouds.Length; i++)
            {
                var t = clouds[i];
                if (t == null) continue;
                float sp = cloudSpeeds != null && i < cloudSpeeds.Length ? cloudSpeeds[i] : 0.15f;
                var p = t.position;
                p.x -= sp * dt;
                if (p.x < cloudMinX) p.x += cloudSpan;
                t.position = p;
            }
        }

        if (waterfalls != null && waterfalls.Length > 0)
        {
            _wfOffset += waterfallSpeed * dt;
            for (int i = 0; i < waterfalls.Length; i++)
                if (waterfalls[i] != null)
                    waterfalls[i].SetTextureOffset("_MainTex", new Vector2(0, -_wfOffset));
        }

        if (rings != null)
        {
            for (int i = 0; i < rings.Length; i++)
            {
                if (rings[i] == null) continue;
                float sp = ringSpeeds != null && i < ringSpeeds.Length ? ringSpeeds[i] : 20f;
                rings[i].Rotate(0f, sp * dt, 0f, Space.World);
            }
        }

        if (arcs != null && arcs.Length > 0)
        {
            float t = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
            float a = Mathf.Lerp(pulseMin, pulseMax, t);
            for (int i = 0; i < arcs.Length; i++)
            {
                if (arcs[i] == null) continue;
                var c = arcs[i].color;
                c.a = a;
                arcs[i].color = c;
            }
        }

        // 浮空物：正弦上下缓浮 + 发光层反向相位呼吸
        if (floaters != null && _floaterBaseY != null)
        {
            for (int i = 0; i < floaters.Length; i++)
            {
                var t = floaters[i];
                if (t == null) continue;
                var p = t.position;
                p.y = _floaterBaseY[i] + Mathf.Sin(Time.time * 6.2831f * floatFrequency + i * 1.3f) * floatAmplitude;
                t.position = p;
            }
        }
        if (floaterGlows != null && floaterGlows.Length > 0)
        {
            float gt = (Mathf.Sin(Time.time * 6.2831f * floatFrequency * 1.5f) + 1f) * 0.5f;
            float ga = Mathf.Lerp(glowMin, glowMax, gt);
            for (int i = 0; i < floaterGlows.Length; i++)
            {
                if (floaterGlows[i] == null) continue;
                var c = floaterGlows[i].color;
                c.a = ga;
                floaterGlows[i].color = c;
            }
        }
    }
}
