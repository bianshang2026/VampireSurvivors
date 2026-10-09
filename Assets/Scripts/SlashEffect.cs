using UnityEngine;

namespace BiopunkRV
{
    /// <summary>
    /// Slash hit effect using SlashEffect_Sprite.
    /// Appears at enemy position, flashes and fades quickly.
    /// </summary>
    public class SlashEffect : MonoBehaviour
    {
        public float duration = 0.4f;
        public float startScale = 0.47f;
        public float endScale = 0.8f;
        public float rotationSpeed = 720f;

        private SpriteRenderer _sr;
        private float _timer;

        private void Awake()
        {
            _sr = gameObject.AddComponent<SpriteRenderer>();

            // Load the slash sprite from Resources
            var tex = Resources.Load<Texture2D>("SlashEffect_Sprite");
            if (tex != null)
            {
                _sr.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            }

            _sr.color = Color.white;
            _sr.sortingOrder = 100;
        }

        private void Update()
        {
            _timer += Time.deltaTime;
            float t = _timer / duration;

            if (t >= 1f)
            {
                Destroy(gameObject);
                return;
            }

            // Scale: pop up then shrink
            float scaleT = Mathf.Sin(t * Mathf.PI);
            float scale = Mathf.Lerp(startScale, endScale, scaleT);
            transform.localScale = new Vector3(scale, scale * 1.13f, 1f);

            // Rotate for slash feel
            transform.Rotate(0, 0, rotationSpeed * Time.deltaTime);

            // Fade out
            Color c = _sr.color;
            c.a = Mathf.Lerp(1f, 0f, t * t);
            _sr.color = c;
        }

        /// <summary>
        /// Create slash effect at enemy position, matching the Square object's style.
        /// </summary>
        public static void CreateAt(Vector3 position, float randomOffset = 0f)
        {
            var go = new GameObject("SlashEffect");

            // Position directly on enemy body (same Y as enemy)
            go.transform.position = position;

            // Match the Square rotation (60.5, 0, ~244)
            float zRot = 200f + Random.Range(-30f, 60f);
            go.transform.rotation = Quaternion.Euler(60.5f, 0, zRot);
            go.transform.localScale = new Vector3(0.47f, 0.53f, 1f);

            go.AddComponent<SlashEffect>();

            Destroy(go, 0.6f);
        }
    }
}
