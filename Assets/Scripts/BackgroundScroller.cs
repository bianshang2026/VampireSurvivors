using UnityEngine;
using UnityEngine.UI;

namespace BiopunkRV
{
    /// <summary>
    /// Simple background layer horizontal scroll via uvRect.x.
    /// </summary>
    public class BackgroundScroller : MonoBehaviour
    {
        public float speed = 0.08f;
        private RawImage _rawImage;

        private void Start()
        {
            _rawImage = GetComponent<RawImage>();
        }

        private void Update()
        {
            if (_rawImage == null) return;
            float x = Mathf.Repeat(_rawImage.uvRect.x + speed * Time.deltaTime, 1f);
            _rawImage.uvRect = new Rect(x, _rawImage.uvRect.y, _rawImage.uvRect.width, _rawImage.uvRect.height);
        }
    }
}