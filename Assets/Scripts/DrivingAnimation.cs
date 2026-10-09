using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BiopunkRV
{
    public class DrivingAnimation : MonoBehaviour
    {
        [Header("Background")]
        public RawImage backgroundLayer;
        public float bgSpeed = 0.06f;
        public float bgYOffset = 0f;
        [Tooltip("If true, flips the background texture vertically.")]
        public bool bgFlipY = true;

        [Header("RV Body Bobbing")]
        public RectTransform rvBody;
        public float bobAmplitude = 1.5f;
        public float bobPeriod = 0.3f;

        [Header("Wheel Rotation")]
        public Transform[] wheels;
        public float wheelSpeed = 360f;

        [Header("Dust Particles")]
        public ParticleSystem dustParticles;
        public Vector2 dustOffset = new Vector2(-0.35f, -0.15f);

        [Header("Speed Lines")]
        public Canvas speedLineCanvas;
        public float speedLineInterval = 0.04f;
        public float speedLineSpeed = 2500f;
        public float speedLineLifetime = 0.5f;
        public Color speedLineColor = new Color(0.85f, 0.9f, 1f, 0.35f);

        private float _scrollBg;
        private Vector2 _rvBasePos;
        private float _speedLineTimer;
        private List<GameObject> _activeSpeedLines;

        private void Start()
        {
            if (rvBody != null)
                _rvBasePos = rvBody.anchoredPosition;
            if (dustParticles != null) dustParticles.Play();
            _activeSpeedLines = new List<GameObject>();
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            // Background scroll
            _scrollBg = Mathf.Repeat(_scrollBg + bgSpeed * dt, 1f);
            if (backgroundLayer != null)
            {
                if (bgFlipY)
                    backgroundLayer.uvRect = new Rect(_scrollBg, 1f + bgYOffset, 1f, -1f);
                else
                    backgroundLayer.uvRect = new Rect(_scrollBg, bgYOffset, 1f, 1f);
            }

            // RV body bobbing - very subtle 1-2px jitter
            if (rvBody != null)
            {
                float bob = Mathf.Sin(Time.time * (2f * Mathf.PI / bobPeriod)) * bobAmplitude;
                rvBody.anchoredPosition = new Vector2(_rvBasePos.x, _rvBasePos.y + bob);
            }

            // Wheel rotation - slow
            if (wheels != null)
            {
                float rot = wheelSpeed * dt;
                for (int i = 0; i < wheels.Length; i++)
                {
                    if (wheels[i] != null)
                        wheels[i].Rotate(0f, 0f, -rot);
                }
            }

            // Dust particle follow
            if (dustParticles != null && rvBody != null)
            {
                var rt = rvBody.GetComponent<RectTransform>();
                if (rt != null)
                {
                    Vector3 worldPos = rt.position;
                    worldPos.x += dustOffset.x * rt.rect.width;
                    worldPos.y += dustOffset.y * rt.rect.height;
                    dustParticles.transform.position = worldPos;
                }
            }

            // Speed lines - diagonal 45°
            UpdateSpeedLines(dt);
        }

        private void UpdateSpeedLines(float dt)
        {
            if (speedLineCanvas == null) return;

            _speedLineTimer += dt;
            while (_speedLineTimer >= speedLineInterval)
            {
                _speedLineTimer -= speedLineInterval;
                SpawnSpeedLine();
            }

            for (int i = _activeSpeedLines.Count - 1; i >= 0; i--)
            {
                var go = _activeSpeedLines[i];
                if (go == null) { _activeSpeedLines.RemoveAt(i); continue; }

                var rt = go.GetComponent<RectTransform>();
                if (rt == null) { Destroy(go); _activeSpeedLines.RemoveAt(i); continue; }

                // Move left and down (diagonal 45°)
                float move = speedLineSpeed * dt;
                rt.anchoredPosition -= new Vector2(move, move * 0.4f);

                if (rt.anchoredPosition.x < -2000)
                {
                    Destroy(go);
                    _activeSpeedLines.RemoveAt(i);
                }
            }
        }

        private void SpawnSpeedLine()
        {
            var go = new GameObject("SpeedLine");
            go.transform.SetParent(speedLineCanvas.transform, false);

            var img = go.AddComponent<Image>();
            img.color = speedLineColor;
            img.raycastTarget = false;

            var rt = go.GetComponent<RectTransform>();

            // Random position across full screen height
            float yPos = Random.Range(-540f, 540f);
            float length = Random.Range(40f, 100f);
            float thickness = Random.Range(2f, 3f);

            rt.anchorMin = new Vector2(1f, 0.5f);
            rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.anchoredPosition = new Vector2(50f, yPos);
            // Rotate 15° for diagonal effect
            rt.localRotation = Quaternion.Euler(0, 0, 15f);
            rt.sizeDelta = new Vector2(length, thickness);

            StartCoroutine(FadeSpeedLine(img));

            _activeSpeedLines.Add(go);
        }

        private IEnumerator<YieldInstruction> FadeSpeedLine(Image img)
        {
            float timer = 0f;
            while (timer < speedLineLifetime && img != null)
            {
                timer += Time.deltaTime;
                float alpha = (1f - timer / speedLineLifetime) * speedLineColor.a;
                img.color = new Color(speedLineColor.r, speedLineColor.g, speedLineColor.b, alpha);
                yield return null;
            }
            if (img != null) Destroy(img.gameObject);
        }
    }
}
