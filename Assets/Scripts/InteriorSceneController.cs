using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

namespace BiopunkRV
{
    /// <summary>
    /// Controls the standalone interior scene: a single wide cutaway image that
    /// smoothly scrolls horizontally between 4 cabin sections (Beholder-style pan).
    /// </summary>
    public class InteriorSceneController : MonoBehaviour
    {
        [Header("Wide Interior Image")]
        public RawImage interiorImage;
        public int totalSegments = 4;

        [Header("Scroll")]
        public float scrollDuration = 0.6f;
        public AnimationCurve scrollCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("UI Elements")]
        public Button leftArrow;
        public Button rightArrow;
        public Button backButton;
        public TextMeshProUGUI titleLabel;
        public TextMeshProUGUI pageNumberLabel;

        [Header("Segment Names")]
        public string[] segmentNames = { "睡眠区", "储物卫生区", "起居区", "驾驶区" };

        /// <summary>Set before SceneManager.LoadScene to tell the controller which segment to start on.</summary>
        public static int EntrySegmentIndex = 0;

        private int _currentIndex = 0;
        private bool _isScrolling;
        private float _segmentWidthUV;

        private void Start()
        {
            _currentIndex = Mathf.Clamp(EntrySegmentIndex, 0, totalSegments - 1);

            // Each segment occupies 1/totalSegments of the UV width
            _segmentWidthUV = 1f / totalSegments;

            // Wire buttons
            if (leftArrow != null) leftArrow.onClick.AddListener(GoPrev);
            if (rightArrow != null) rightArrow.onClick.AddListener(GoNext);
            if (backButton != null) backButton.onClick.AddListener(ReturnToDriving);

            // Show initial segment instantly
            SetUVForSegment(_currentIndex);
            UpdateUI(_currentIndex);
        }

        private void SetUVForSegment(int index)
        {
            if (interiorImage == null) return;
            float x = index * _segmentWidthUV;
            interiorImage.uvRect = new Rect(x, 0f, _segmentWidthUV, 1f);
        }

        private IEnumerator ScrollToSegment(int newIndex)
        {
            _isScrolling = true;
            int oldIndex = _currentIndex;
            _currentIndex = newIndex;

            float fromX = oldIndex * _segmentWidthUV;
            float toX = newIndex * _segmentWidthUV;

            UpdateUI(newIndex);

            float timer = 0f;
            while (timer < scrollDuration)
            {
                timer += Time.deltaTime;
                float t = scrollCurve.Evaluate(timer / scrollDuration);
                float x = Mathf.Lerp(fromX, toX, t);
                interiorImage.uvRect = new Rect(x, 0f, _segmentWidthUV, 1f);
                yield return null;
            }

            interiorImage.uvRect = new Rect(toX, 0f, _segmentWidthUV, 1f);
            _isScrolling = false;
        }

        private void UpdateUI(int index)
        {
            if (titleLabel != null)
                titleLabel.text = $"{segmentNames[index]} · 侧面剖视";

            if (pageNumberLabel != null)
                pageNumberLabel.text = $"{index + 1}/{totalSegments}";

            if (leftArrow != null)
                leftArrow.gameObject.SetActive(index > 0);

            if (rightArrow != null)
                rightArrow.gameObject.SetActive(index < totalSegments - 1);
        }

        public void GoPrev()
        {
            if (_currentIndex > 0 && !_isScrolling)
                StartCoroutine(ScrollToSegment(_currentIndex - 1));
        }

        public void GoNext()
        {
            if (_currentIndex < totalSegments - 1 && !_isScrolling)
                StartCoroutine(ScrollToSegment(_currentIndex + 1));
        }

        public void ReturnToDriving()
        {
            SceneManager.LoadScene("RestScene");
        }
    }
}
