using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using TMPro;

namespace BiopunkRV
{
    /// <summary>
    /// Handles hover highlight and click-to-enter-interior for each RV cabin segment.
    /// On click, loads the InteriorScene via SceneManager and passes the segment index.
    /// </summary>
    [RequireComponent(typeof(RawImage))]
    public class RVCabinSegment : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        [Header("Segment Info")]
        public string segmentName = "Segment";
        public int segmentIndex = 1;
        public int totalSegments = 4;

        [Header("Hover Effect")]
        public Color hoverColor = new Color(1f, 0.65f, 0f, 0.35f);
        public float pulseSpeed = 3f;

        [Header("Click Flash")]
        public float flashDuration = 0.3f;
        public Color flashColor = new Color(1f, 0.7f, 0.2f, 0.7f);

        [Header("Scene Settings")]
        public string interiorSceneName = "InteriorScene";

        private RawImage _rawImage;
        private Color _originalColor = Color.white;
        private bool _isHovered;
        private bool _isFlashing;
        private float _flashTimer;

        private void Awake()
        {
            _rawImage = GetComponent<RawImage>();
            if (_rawImage != null)
                _originalColor = _rawImage.color;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _isHovered = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _isHovered = false;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_isFlashing) return;
            Debug.Log($"[RVCabinSegment] Clicked: {segmentName} (Segment {segmentIndex})");
            _isFlashing = true;
            _flashTimer = 0f;
        }

        private void Update()
        {
            if (_rawImage == null) return;

            if (_isFlashing)
            {
                _flashTimer += Time.deltaTime;
                float t = _flashTimer / flashDuration;
                if (t >= 1f)
                {
                    _isFlashing = false;
                    // Pass segment index to the interior scene
                    InteriorSceneController.EntrySegmentIndex = segmentIndex - 1;
                    SceneManager.LoadScene(interiorSceneName);
                }
                else
                {
                    float pulse = Mathf.Sin(t * Mathf.PI * 3f) * 0.5f + 0.5f;
                    float alpha = flashColor.a * (1f - t);
                    _rawImage.color = new Color(
                        Mathf.Lerp(_originalColor.r, flashColor.r, alpha * pulse),
                        Mathf.Lerp(_originalColor.g, flashColor.g, alpha * pulse),
                        Mathf.Lerp(_originalColor.b, flashColor.b, alpha * pulse),
                        1f
                    );
                }
                return;
            }

            if (_isHovered)
            {
                float pulse = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
                float alpha = hoverColor.a * (0.6f + 0.4f * pulse);
                _rawImage.color = new Color(
                    Mathf.Lerp(_originalColor.r, hoverColor.r, alpha),
                    Mathf.Lerp(_originalColor.g, hoverColor.g, alpha),
                    Mathf.Lerp(_originalColor.b, hoverColor.b, alpha),
                    1f
                );
            }
            else
            {
                _rawImage.color = _originalColor;
            }
        }
    }
}
