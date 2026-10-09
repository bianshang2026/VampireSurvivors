using UnityEngine;
using UnityEngine.UI;

namespace BiopunkRV
{
    /// <summary>
    /// Simple walk cycle animation: cycles through sprite frames at a set FPS.
    /// Also handles horizontal movement and direction flipping.
    /// </summary>
    public class GirlWalkAnimation : MonoBehaviour
    {
        [Header("Walk Frames")]
        public Texture2D[] walkFrames;

        [Header("Animation")]
        public float framesPerSecond = 8f;
        public bool isWalking = false;

        [Header("Movement")]
        public float walkSpeed = 80f;
        public float walkRange = 300f;
        public float pauseTime = 1f;

        private RawImage _rawImage;
        private float _frameTimer;
        private int _currentFrame;
        private float _walkOffset;
        private int _walkDir = 1;
        private float _pauseTimer;
        private bool _isPausing;
        private Vector2 _basePos;

        private void Awake()
        {
            _rawImage = GetComponent<RawImage>();
            _basePos = _rawImage.rectTransform.anchoredPosition;
        }

        private void Update()
        {
            if (walkFrames == null || walkFrames.Length == 0) return;

            if (isWalking)
            {
                // Frame animation
                _frameTimer += Time.deltaTime;
                if (_frameTimer >= 1f / framesPerSecond)
                {
                    _frameTimer = 0f;
                    _currentFrame = (_currentFrame + 1) % walkFrames.Length;
                    _rawImage.texture = walkFrames[_currentFrame];
                }

                // Movement
                if (_isPausing)
                {
                    _pauseTimer -= Time.deltaTime;
                    if (_pauseTimer <= 0f)
                    {
                        _isPausing = false;
                        _walkDir *= -1;
                        UpdateFacing();
                    }
                }
                else
                {
                    _walkOffset += _walkDir * walkSpeed * Time.deltaTime;
                    if (_walkOffset >= walkRange)
                    {
                        _walkOffset = walkRange;
                        _isPausing = true;
                        _pauseTimer = pauseTime;
                    }
                    else if (_walkOffset <= -walkRange)
                    {
                        _walkOffset = -walkRange;
                        _isPausing = true;
                        _pauseTimer = pauseTime;
                    }
                }

                _rawImage.rectTransform.anchoredPosition = new Vector2(_basePos.x + _walkOffset, _basePos.y);
            }
            else
            {
                // Idle - show frame 0
                if (_rawImage.texture != walkFrames[0])
                    _rawImage.texture = walkFrames[0];
            }
        }

        private void UpdateFacing()
        {
            var scale = _rawImage.rectTransform.localScale;
            scale.x = _walkDir > 0 ? Mathf.Abs(scale.x) : -Mathf.Abs(scale.x);
            _rawImage.rectTransform.localScale = scale;
        }

        public void SetWalking(bool walking)
        {
            isWalking = walking;
            if (!walking && walkFrames != null && walkFrames.Length > 0)
            {
                _rawImage.texture = walkFrames[0];
            }
        }
    }
}
