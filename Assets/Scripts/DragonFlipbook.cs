using UnityEngine;

namespace BiopunkRV
{
    /// <summary>
    /// 姿态序列帧+代码合成播放器 (进阶龙形态等大幅姿态序列帧用)。
    /// ① 序列帧层: 姿态帧循环切换
    /// ② 过渡层: 帧末段双渲染器交叉淡化(叠影), 消除大幅姿态切换的频闪/翻页感
    /// ③ 代码层: 自建Visual子物体做浮动+倾斜摇摆, 连续运动感叠加在离散帧之上
    /// 根物体位置/旋转/缩放由外部控制, 本组件只动Visual子物体, 不冲突。
    /// tint运行时换色(元素龙): 白体黑描边结构下 sr.color 乘算。
    /// </summary>
    public class DragonFlipbook : MonoBehaviour
    {
        [Header("序列帧")]
        public Sprite[] frames;
        public float fps = 7f;
        [Range(0f, 0.8f)] public float crossfade = 0.45f; // 帧时长末段的交叉淡化占比

        [Header("代码运动层 (Visual子物体)")]
        public float bobAmp = 0.7f;    // 浮动幅度(世界单位)
        public float bobSpeed = 2.4f;  // 浮动频率
        public float bankAmp = 10f;    // 倾斜摇摆幅度(度)
        public float bankSpeed = 1.8f; // 倾斜频率

        [Header("渲染")]
        public Color tint = Color.white;
        public int sortingOrder = 55;

        private Transform _visual;
        private SpriteRenderer _main, _ghost;
        private float _t, _life;
        private int _index;

        private void Awake()
        {
            var visualGo = new GameObject("Visual");
            visualGo.transform.SetParent(transform, false);
            _visual = visualGo.transform;
            _main = visualGo.AddComponent<SpriteRenderer>();
            var ghostGo = new GameObject("FlipGhost");
            ghostGo.transform.SetParent(visualGo.transform, false);
            _ghost = ghostGo.AddComponent<SpriteRenderer>();
        }

        private void Update()
        {
            if (frames == null || frames.Length == 0) return;
            _life += Time.deltaTime;

            // ── 序列帧层 + 交叉淡化 ──
            int n = frames.Length;
            float frameDur = 1f / Mathf.Max(1f, fps);
            _t += Time.deltaTime;
            if (_t >= frameDur)
            {
                _t -= frameDur;
                _index = (_index + 1) % n;
                _main.sprite = frames[_index];
                _ghost.sprite = null;
            }
            _main.color = tint;
            _ghost.color = tint;
            _main.sortingOrder = sortingOrder;
            _ghost.sortingOrder = sortingOrder;

            float fadeDur = frameDur * crossfade;
            if (fadeDur > 0.001f && _t > frameDur - fadeDur)
            {
                // 帧末段: 下一帧叠影淡入, 当前帧淡出 → 姿态跳变读作运动残影
                float a = Mathf.Clamp01((_t - (frameDur - fadeDur)) / fadeDur);
                _ghost.sprite = frames[(_index + 1) % n];
                var gc = tint; gc.a = tint.a * a;
                _ghost.color = gc;
                var mc = tint; mc.a = tint.a * (1f - a);
                _main.color = mc;
            }

            // ── 代码运动层: 浮动 + 倾斜 (相位错开, 避免同步机械感) ──
            _visual.localPosition = new Vector3(0f, Mathf.Sin(_life * bobSpeed) * bobAmp, 0f);
            _visual.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(_life * bankSpeed + 0.7f) * bankAmp);
        }

        /// <summary>运行时换色 (元素龙: 传入ElementColors.GetTint)</summary>
        public void SetTint(Color c) { tint = c; }
    }
}
