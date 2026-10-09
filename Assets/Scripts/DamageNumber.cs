using UnityEngine;
using UnityEngine.UI;

namespace BiopunkRV
{
    public enum Element { None, Water, Fire, Wind, Earth, Thunder, Metal, FlameWind, Frost, Wood, MetalFusion }

    /// <summary>
    /// Floating damage number that pops up at hit position and floats up + fades.
    /// Uses number sprites (0-9) from Resources/DamageNumbers.
    /// Color changes based on element type.
    /// </summary>
    public class DamageNumber : MonoBehaviour
    {
        public float floatSpeed = 2f;
        public float duration = 0.8f;
        public float startScale = 1.5f;
        public float popScale = 2.2f;
        public float digitSize = 1.8f;
        public float digitSpacing = 0.9f;

        private Sprite[] _digits;
        private float _timer;
        private bool _initialized;

        // Element colors (tint applied to white sprite)
        public static readonly Color ColorWater = new Color(0.2f, 0.3f, 0.9f, 1f);   // 蓝色
        public static readonly Color ColorFire = new Color(0.9f, 0.15f, 0.1f, 1f);   // 红色
        public static readonly Color ColorWind = new Color(0.4f, 0.85f, 0.9f, 1f);    // 淡青色
        public static readonly Color ColorEarth = new Color(0.55f, 0.35f, 0.2f, 1f);  // 棕色
        public static readonly Color ColorThunder = new Color(0.6f, 0.2f, 0.9f, 1f);  // 紫色
        public static readonly Color ColorMetal = new Color(0.85f, 0.75f, 0.2f, 1f);  // 金色
        public static readonly Color ColorNone = Color.white;                           // 白色
        // 融合属性
        public static readonly Color ColorFlameWind = new Color(1f, 0.35f, 0.15f, 1f);  // 炎=炽橙红
        public static readonly Color ColorFrost = new Color(0.5f, 0.85f, 1f, 1f);       // 冰=冰蓝
        public static readonly Color ColorWood = new Color(0.4f, 0.8f, 0.3f, 1f);       // 草=翠绿
        public static readonly Color ColorMetalFusion = new Color(1f, 0.8f, 0.3f, 1f);  // 金=亮金

        public static Color GetElementColor(Element element)
        {
            switch (element)
            {
                case Element.Water: return ColorWater;
                case Element.Fire: return ColorFire;
                case Element.Wind: return ColorWind;
                case Element.Earth: return ColorEarth;
                case Element.Thunder: return ColorThunder;
                case Element.Metal: return ColorMetal;
                case Element.FlameWind: return ColorFlameWind;
                case Element.Frost: return ColorFrost;
                case Element.Wood: return ColorWood;
                case Element.MetalFusion: return ColorMetalFusion;
                default: return ColorNone;
            }
        }

        private static Sprite _critIconSprite;

        public void SetDamage(int damage, Element element = Element.None, bool isCrit = false)
        {
            // Load only num_* textures (exclude crit_icon)
            var textures = Resources.LoadAll<Texture2D>("DamageNumbers");
            if (textures == null || textures.Length == 0) return;

            var digitTextures = new System.Collections.Generic.List<Texture2D>();
            foreach (var tex in textures)
            {
                if (tex.name.StartsWith("num_"))
                    digitTextures.Add(tex);
            }
            digitTextures.Sort((a, b) => a.name.CompareTo(b.name));

            float digitSpacing = 0.7f;

            _digits = new Sprite[digitTextures.Count];
            for (int i = 0; i < digitTextures.Count; i++)
            {
                float ppu = digitTextures[i].height / 0.8f;
                _digits[i] = Sprite.Create(digitTextures[i], new Rect(0, 0, digitTextures[i].width, digitTextures[i].height), new Vector2(0.5f, 0.5f), ppu);
            }

            string damageStr = damage.ToString();
            float startX = -(damageStr.Length - 1) * digitSpacing * 0.5f;

            Color numColor = GetElementColor(element);

            for (int i = 0; i < damageStr.Length; i++)
            {
                int digit = damageStr[i] - '0';
                if (digit < 0 || digit > 9 || _digits == null || digit >= _digits.Length) continue;

                var childGO = new GameObject("Digit_" + i);
                childGO.transform.SetParent(transform, false);
                childGO.transform.localPosition = new Vector3(startX + i * digitSpacing, 0, 0);
                childGO.transform.localScale = Vector3.one;

                var sr = childGO.AddComponent<SpriteRenderer>();
                sr.sprite = _digits[digit];
                sr.color = numColor;
                sr.sortingOrder = 200;
            }

            if (isCrit)
            {
                if (_critIconSprite == null)
                {
                    var critTex = Resources.Load<Texture2D>("DamageNumbers/crit_icon");
                    if (critTex != null)
                    {
                        float ppu = critTex.height / 0.8f;
                        _critIconSprite = Sprite.Create(critTex, new Rect(0, 0, critTex.width, critTex.height), new Vector2(0.5f, 0.5f), ppu);
                    }
                }

                if (_critIconSprite != null)
                {
                    var critGO = new GameObject("CritIcon");
                    critGO.transform.SetParent(transform, false);
                    // Position to the LEFT of the digits
                    critGO.transform.localPosition = new Vector3(startX - digitSpacing, 0, 0);
                    critGO.transform.localScale = Vector3.one;

                    var critSR = critGO.AddComponent<SpriteRenderer>();
                    critSR.sprite = _critIconSprite;
                    critSR.color = numColor;
                    critSR.sortingOrder = 201;
                }
            }

            _initialized = true;
        }

        private void Update()
        {
            if (!_initialized) return;

            _timer += Time.deltaTime;
            float t = _timer / duration;

            if (t >= 1f)
            {
                Destroy(gameObject);
                return;
            }

            Vector3 pos = transform.position;
            pos.y += floatSpeed * Time.deltaTime;
            transform.position = pos;

            float scaleT = Mathf.Sin(t * Mathf.PI);
            float scale = Mathf.Lerp(startScale, popScale, scaleT);
            transform.localScale = new Vector3(scale, scale, 1f);

            var renderers = GetComponentsInChildren<SpriteRenderer>();
            float alpha = Mathf.Lerp(1f, 0f, t * t);
            foreach (var sr in renderers)
            {
                Color c = sr.color;
                c.a = alpha;
                sr.color = c;
            }
        }

        /// <summary>
        /// Create a floating damage number at position with element color.
        /// </summary>
        public static void CreateAt(Vector3 position, int damage, Element element = Element.None, bool isCrit = false)
        {
            var go = new GameObject("DamageNumber");
            go.transform.position = position + new Vector3(0, 1f, 0);
            go.transform.rotation = Quaternion.Euler(60.5f, 0, 0);
            go.transform.localScale = Vector3.one;

            var dn = go.AddComponent<DamageNumber>();
            dn.SetDamage(damage, element, isCrit);

            Destroy(go, 1.2f);
        }
    }
}
