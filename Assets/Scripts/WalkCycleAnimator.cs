using UnityEngine;
using UnityEngine.UI;

namespace BiopunkRV
{
    /// <summary>Simple frame-based walk cycle animator for RawImage. Cycles through textures at given FPS.</summary>
    public class WalkCycleAnimator : MonoBehaviour
    {
        public Texture2D[] frames;
        public float fps = 8f;
        public bool loop = true;
        public bool playAutomatically = true;

        private RawImage _rawImage;
        private int _currentFrame;
        private float _timer;
        private bool _isPlaying;

        private void Awake()
        {
            _rawImage = GetComponent<RawImage>();
        }

        private void Start()
        {
            if (playAutomatically) Play();
        }

        public void Play()
        {
            _isPlaying = true;
            _timer = 0f;
            _currentFrame = 0;
            ApplyFrame(0);
        }

        public void Stop()
        {
            _isPlaying = false;
        }

        private void Update()
        {
            if (!_isPlaying || frames == null || frames.Length == 0) return;

            _timer += Time.deltaTime;
            float frameDuration = 1f / fps;

            if (_timer >= frameDuration)
            {
                _timer -= frameDuration;
                _currentFrame++;

                if (_currentFrame >= frames.Length)
                {
                    if (loop)
                        _currentFrame = 0;
                    else
                    {
                        _isPlaying = false;
                        _currentFrame = frames.Length - 1;
                    }
                }

                ApplyFrame(_currentFrame);
            }
        }

        private void ApplyFrame(int index)
        {
            if (_rawImage != null && frames != null && index < frames.Length)
                _rawImage.texture = frames[index];
        }
    }
}
