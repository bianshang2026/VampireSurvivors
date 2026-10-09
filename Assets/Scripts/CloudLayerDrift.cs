using UnityEngine;

/// <summary>
/// 大厅云海分层动画：云层单向连续流动（传送带式双副本无缝循环）；
/// 岛屿/其他层用 ping-pong 漂移（driftX/driftY）、呼吸、alpha 脉动。
/// </summary>
public class CloudLayerDrift : MonoBehaviour
{
    public float flowX = 0f;        // 单向流动速度（世界单位/秒，负=向左，0=不流动）
    [Range(0f, 0.5f)]
    public float flowOverlap = 0f;  // 传送带副本之间的重叠比例（配合边缘渐变贴图做交叉淡化消接缝）
    public float driftX = 0f;       // ping-pong 水平漂移幅度
    public float driftY = 0f;       // ping-pong 垂直浮动幅度
    public float driftSpeed = 0.05f;
    public float breathe = 0f;
    public float breatheSpeed = 0.2f;
    public float alphaPulse = 0f;
    public float alphaSpeed = 0.1f;
    public float phase = 0f;

    static bool _cloning;

    Vector3 _basePos;
    Vector3 _baseScale;
    float _baseAlpha;
    float _spacing;
    SpriteRenderer _sr;
    bool _isCopy;

    void Awake()
    {
        _basePos = transform.localPosition;
        _baseScale = transform.localScale;
        _sr = GetComponent<SpriteRenderer>();
        _baseAlpha = _sr != null ? _sr.color.a : 1f;
        _spacing = _sr != null ? _sr.bounds.size.x * (1f - flowOverlap) : 0f;
        if (flowX != 0f && !_isCopy && !_cloning && _spacing > 0f)
            SpawnConveyorCopy();
    }

    void SpawnConveyorCopy()
    {
        _cloning = true;
        var go = Instantiate(gameObject, transform.parent);
        _cloning = false;
        go.GetComponent<CloudLayerDrift>()._isCopy = true;
        // 入场侧：向右流副本在左，向左流副本在右
        float side = flowX > 0f ? -_spacing : _spacing;
        go.transform.localPosition = _basePos + new Vector3(side, 0f, 0f);
    }

    void Update()
    {
        float t = Time.time + phase;
        if (flowX != 0f)
        {
            var p = transform.localPosition + new Vector3(flowX * Time.deltaTime, 0f, 0f);
            if (flowX > 0f && p.x > _basePos.x + _spacing) p.x -= 2f * _spacing;
            else if (flowX < 0f && p.x < _basePos.x - _spacing) p.x += 2f * _spacing;
            transform.localPosition = p;
        }
        else if (driftX != 0f || driftY != 0f)
        {
            transform.localPosition = _basePos + new Vector3(
                Mathf.Sin(t * driftSpeed) * driftX,
                Mathf.Sin(t * driftSpeed * 0.83f) * driftY, 0f);
        }
        if (breathe != 0f)
            transform.localScale = _baseScale * (1f + Mathf.Sin(t * breatheSpeed) * breathe);
        if (alphaPulse != 0f && _sr != null)
        {
            var c = _sr.color;
            c.a = _baseAlpha + Mathf.Sin(t * alphaSpeed) * alphaPulse;
            _sr.color = c;
        }
    }
}
