using UnityEngine;

namespace BiopunkRV
{
    /// <summary>
    /// Applies a wind-sway vertex animation to a quad MeshRenderer.
    /// Top vertices bend with a sine wave; bottom stays fixed.
    /// Each instance gets a random phase so trees don't sway in sync.
    /// </summary>
    [RequireComponent(typeof(MeshFilter))]
    public class WindSway : MonoBehaviour
    {
        [Header("Wind")]
        public float swayAmount = 0.15f;
        public float swaySpeed = 1.5f;

        private Mesh mesh;
        private Vector3[] originalVertices;
        private float phase;
        private float intensity = 1f;

        void Awake()
        {
            mesh = GetComponent<MeshFilter>().mesh;
            originalVertices = mesh.vertices.Clone() as Vector3[];
            phase = Random.Range(0f, Mathf.PI * 2f);
            intensity = Random.Range(0.7f, 1.3f);
        }

        void LateUpdate()
        {
            if (mesh == null) return;

            float t = Time.time * swaySpeed + phase;
            // Bend factor: 0 at bottom (y=-0.5), 1 at top (y=0.5)
            // Use quadratic so bending is concentrated near the top
            float offset = Mathf.Sin(t) * swayAmount * intensity;

            Vector3[] verts = mesh.vertices;
            for (int i = 0; i < verts.Length; i++)
            {
                float y = originalVertices[i].y;
                // Normalize: -0.5 (bottom) -> 0, 0.5 (top) -> 1
                float bend = Mathf.Clamp01((y + 0.5f));
                bend = bend * bend; // quadratic for more natural bend

                verts[i].x = originalVertices[i].x + offset * bend;
                verts[i].y = originalVertices[i].y + Mathf.Abs(offset) * 0.3f * bend; // slight vertical lift
            }
            mesh.vertices = verts;
        }

        void OnDestroy()
        {
            if (mesh != null && originalVertices != null)
                mesh.vertices = originalVertices;
        }
    }
}
