using UnityEngine;
using UnityEngine.UI;

namespace BiopunkRV
{
    /// <summary>
    /// A character inside the interior scene. Supports two modes:
    /// - Idle: gentle breathing bob (for seated characters)
    /// - Walk: patrols back and forth within the room (for standing characters)
    /// </summary>
    public class InteriorCharacter : MonoBehaviour
    {
        public enum Mode { Idle, Walk }

        [Header("Placement")]
        public int segmentIndex = 0;
        public Vector2 localOffset = Vector2.zero;

        [Header("Mode")]
        public Mode mode = Mode.Idle;

        [Header("Idle")]
        public float idleBobAmplitude = 3f;
        public float idleBobSpeed = 2f;

        [Header("Walk")]
        public float walkRange = 120f;       // how far to walk from center
        public float walkSpeed = 40f;         // pixels per second
        public float pauseTime = 0.8f;        // pause at each end
        public float stepBobAmplitude = 4f;
        public float stepBobSpeed = 8f;

        [Header("Facing")]
        public bool faceRight = true;

        private RectTransform _rt;
        private Vector2 _basePos;
        private float _walkOffset;
        private int _walkDir = 1;
        private float _pauseTimer;
        private bool _isPausing;

        private void Awake()
        {
            _rt = GetComponent<RectTransform>();
            _basePos = _rt.anchoredPosition;
        }

        private void Update()
        {
            if (mode == Mode.Idle)
            {
                UpdateIdle();
            }
            else
            {
                UpdateWalk();
            }
        }

        private void UpdateIdle()
        {
            float bob = Mathf.Sin(Time.time * idleBobSpeed) * idleBobAmplitude;
            _rt.anchoredPosition = new Vector2(_basePos.x, _basePos.y + bob);
            ApplyFacing();
        }

        private void UpdateWalk()
        {
            if (_isPausing)
            {
                _pauseTimer -= Time.deltaTime;
                if (_pauseTimer <= 0f)
                {
                    _isPausing = false;
                    _walkDir *= -1;
                    faceRight = _walkDir > 0;
                }
                // Small idle bob while pausing
                float bob = Mathf.Sin(Time.time * idleBobSpeed) * idleBobAmplitude;
                _rt.anchoredPosition = new Vector2(_basePos.x + _walkOffset, _basePos.y + bob);
            }
            else
            {
                // Move
                _walkOffset += _walkDir * walkSpeed * Time.deltaTime;

                // Check bounds
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

                // Step bob (faster than idle)
                float bob = Mathf.Sin(Time.time * stepBobSpeed) * stepBobAmplitude;
                _rt.anchoredPosition = new Vector2(_basePos.x + _walkOffset, _basePos.y + bob);
            }

            ApplyFacing();
        }

        private void ApplyFacing()
        {
            var scale = _rt.localScale;
            scale.x = faceRight ? Mathf.Abs(scale.x) : -Mathf.Abs(scale.x);
            _rt.localScale = scale;
        }

        public void SetBasePosition(Vector2 pos)
        {
            _basePos = pos;
            if (_rt != null)
                _rt.anchoredPosition = pos;
        }
    }
}
