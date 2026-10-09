using UnityEngine;

namespace BiopunkRV
{
    /// <summary>
    /// 通用序列帧动画组件 — 挂在任何GameObject上播放Sprite[]序列帧。
    /// 用于属性特效VFX(雷/风/水粒子动画)。
    /// </summary>
    public class VFXFrameAnimator : MonoBehaviour
    {
        public Sprite[] frames;
        public float fps = 12f;
        public bool loop = true;
        public float alpha = 0.8f;
        public int startFrame = 0;   // 起始帧(多实例错开相位用)

        private SpriteRenderer _sr;
        private int _frameIndex;
        private float _timer;
        private bool _phaseApplied;

        private void Awake()
        {
            _sr = GetComponent<SpriteRenderer>();
            if (_sr == null) _sr = gameObject.AddComponent<SpriteRenderer>();
            if (frames != null && frames.Length > 0)
            {
                _frameIndex = Mathf.Clamp(startFrame, 0, frames.Length - 1);
                _sr.sprite = frames[_frameIndex];
                _phaseApplied = true;
            }
        }

        private void Update()
        {
            if (frames == null || frames.Length == 0) return;

            // frames在Awake之后才被赋值的场景: 首次Update时补应用起始相位
            if (!_phaseApplied)
            {
                _phaseApplied = true;
                _frameIndex = Mathf.Clamp(startFrame, 0, frames.Length - 1);
                if (_sr != null) _sr.sprite = frames[_frameIndex];
            }

            _timer += Time.deltaTime;
            float frameDur = 1f / fps;
            if (_timer >= frameDur)
            {
                _timer -= frameDur;
                _frameIndex++;
                if (_frameIndex >= frames.Length)
                {
                    if (loop) _frameIndex = 0;
                    else { Destroy(gameObject); return; }
                }
                if (_sr != null)
                    _sr.sprite = frames[_frameIndex];
            }

            if (_sr != null && _sr.color.a != alpha)
                _sr.color = new Color(1f, 1f, 1f, alpha);
        }
    }
}
