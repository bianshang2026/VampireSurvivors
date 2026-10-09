using UnityEngine;
using UnityEngine.UI;

namespace BiopunkRV
{
    /// <summary>Cycles through wheel frames to create rotation animation.</summary>
    public class WheelAnimation : MonoBehaviour
    {
        public Texture2D[] frames;
        public float fps = 15f;
        private RawImage _rawImage;
        private float _timer;
        private int _currentFrame;

        private void Start()
        {
            _rawImage = GetComponent<RawImage>();
        }

        private void Update()
        {
            if (frames == null || frames.Length == 0 || _rawImage == null) return;
            
            _timer += Time.deltaTime;
            if (_timer >= 1f / fps)
            {
                _timer = 0f;
                _currentFrame = (_currentFrame + 1) % frames.Length;
                _rawImage.texture = frames[_currentFrame];
            }
        }
    }
}