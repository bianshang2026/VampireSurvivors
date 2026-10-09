using UnityEngine;

namespace BiopunkRV
{
    /// <summary>
    /// 灵气小妖怪 — 最普通近战小怪
    /// </summary>
    public class SpiritMonsterController : EnemyController
    {
        [Header("漂浮动画")]
        public float floatHeight = 0.03f;
        public float floatSpeed = 3f;

        [Header("走路序列帧")]
        public Sprite[] walkFrames;
        public float walkFPS = 3f;
        public Sprite idleFrame;

        private int _currentFrame;
        private float _frameTimer;
        private SpriteRenderer _sr;

        protected override void OnAwake()
        {
            maxHealth = 15f;
            currentHealth = 15f;
            attack = 4f;
            defense = 5f;
            element = Element.None;
            elementDefense = 0f;
            isRanged = false;
            moveSpeed = 5f;
            stopDistance = 2f;
            separationRadius = 2.5f;
            _sr = GetComponent<SpriteRenderer>();
            _baseY += 1f;
        }

        protected override void AnimateMovement()
        {
            if (_isMoving)
            {
                _animTimer += Time.deltaTime * floatSpeed;
                float bob = Mathf.Sin(_animTimer) * floatHeight;

                Vector3 pos = transform.position;
                pos.y = _baseY + bob;
                transform.position = pos;

                // Walk frame animation
                if (walkFrames != null && walkFrames.Length > 0 && _sr != null)
                {
                    _frameTimer += Time.deltaTime;
                    float effectiveFPS = walkFPS * (moveSpeed / 3f);
                    if (effectiveFPS < 2f) effectiveFPS = 2f;
                    float frameDur = 1f / effectiveFPS;
                    if (_frameTimer >= frameDur)
                    {
                        _frameTimer -= frameDur;
                        _currentFrame = (_currentFrame + 1) % walkFrames.Length;
                        // 播放顺序: 左迈×3→站×2→右迈×3→站×2, 循环
                        int[] order = { 1, 3, 1, 0, 4, 5, 7, 5, 4, 0 };
                        int idx = order[_currentFrame % order.Length];
                        _sr.sprite = walkFrames[idx];
                    }
                }
            }
            else
            {
                _animTimer += Time.deltaTime * floatSpeed * 0.5f;
                float bob = Mathf.Sin(_animTimer) * floatHeight * 0.5f;

                Vector3 pos = transform.position;
                pos.y = _baseY + bob;
                transform.position = pos;

                // Idle frame
                if (idleFrame != null && _sr != null && _sr.sprite != idleFrame)
                    _sr.sprite = idleFrame;
                _currentFrame = 0;
                _frameTimer = 0f;
            }
        }
    }
}
