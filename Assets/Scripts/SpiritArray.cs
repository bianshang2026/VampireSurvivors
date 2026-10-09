using UnityEngine;

namespace BiopunkRV
{
    /// <summary>
    /// 灵力阵 — 法阵生成在敌人脚下，每秒造成伤害。
    /// 法阵从零扩大到目标尺寸，并持续旋转（灵力流转）。
    /// </summary>
    public class SpiritArray : MonoBehaviour
    {
        [Header("伤害")]
        public float dps = 3f;              // 每秒伤害
        public Element element = Element.None;
        public float damageRadius = 1.5f;   // 法阵有效范围

        [Header("动画")]
        public Sprite arraySprite;
        public float growDuration = 0.8f;   // 从小到大缩放时长
        public float maxScale = 1.5f;       // 放大0.5倍
        public float rotateSpeed = 30f;     // 旋转速度（灵力流转）
        public float lifetime = 3f;         // 存在时间

        private SpriteRenderer _sr;
        private float _elapsed;
        private float _damageTimer;
        private float _growProgress;
        private float _lifeTimer;

        /// <summary>
        /// 原地创建法阵
        /// </summary>
        public static SpiritArray CreateAt(Vector3 position, Sprite sprite, Transform parent = null)
        {
            GameObject go = new GameObject("SpiritArray");
            go.transform.position = new Vector3(position.x, 1.5f, position.z); // 往上提(2026-09-03: 0.5陷地, 提到1.5)
            // 参照火精的摆放角度，平铺地面
            go.transform.rotation = Quaternion.Euler(85.56345f, 180f, 180f);
            if (parent != null) go.transform.SetParent(parent, true);

            var arr = go.AddComponent<SpiritArray>();
            arr.arraySprite = sprite;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = 5; // 低于怪物层级，怪物渲染在法阵上方
            sr.color = new Color(1f, 1f, 1f, 1f); // 纯白提亮，不暗

            Light glow = go.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = new Color(0.6f, 0.8f, 1f);
            glow.intensity = 1.2f;
            glow.range = 4f;

            return arr;
        }

        private void Awake()
        {
            if (_sr == null) _sr = GetComponent<SpriteRenderer>();
        }

        private void Start()
        {
            if (_sr == null) _sr = GetComponent<SpriteRenderer>();
            if (_sr != null && arraySprite != null) _sr.sprite = arraySprite;
            // 初始大小为0
            transform.localScale = Vector3.zero;
        }

        private void Update()
        {
            if (_sr == null) return;

            _elapsed += Time.deltaTime;
            _lifeTimer += Time.deltaTime;

            // 1. 从小到大逐步放大 (maxScale为世界localScale: 0.22×法阵贴图20.48单位≈4.5世界直径, 覆盖一只怪物)
            _growProgress = Mathf.Clamp01(_elapsed / growDuration);
            float easedProgress = 1f - Mathf.Pow(1f - _growProgress, 3f);
            float scale = Mathf.Lerp(0.005f, maxScale, easedProgress);
            transform.localScale = new Vector3(scale, scale, 1f);

            // 2. Z轴灵力流转（保持地面平铺角度不变）
            transform.rotation = Quaternion.Euler(85.56345f, 180f, 180f + _elapsed * rotateSpeed);

            // 3. 每秒伤害
            _damageTimer += Time.deltaTime;
            if (_damageTimer >= 1f)
            {
                _damageTimer -= 1f;
                DealDamage();
            }

            // 4. 消失
            if (_lifeTimer >= lifetime)
            {
                // 淡出后销毁
                _sr.color = new Color(1, 1, 1, Mathf.Lerp(1f, 0f, (_lifeTimer - lifetime) * 3f));
                if (_lifeTimer >= lifetime + 0.5f)
                    Destroy(gameObject);
            }
        }

        private void DealDamage()
        {
            var allEnemies = FindObjectsOfType<EnemyController>();
            foreach (var e in allEnemies)
            {
                if (e == null || !e.gameObject.activeInHierarchy) continue;
                float d = Vector3.Distance(transform.position, e.transform.position);
                if (d < damageRadius)
                {
                    int dmg = Mathf.Max(1, Mathf.RoundToInt(dps));
                    e.TakeDamage(dmg);
                    DamageNumber.CreateAt(e.transform.position + new Vector3(Random.Range(-0.5f, 0.5f), 0, 0), dmg, element);
                }
            }

            var allRabbits = FindObjectsOfType<GhostRabbitController>();
            foreach (var r in allRabbits)
            {
                if (r == null || !r.gameObject.activeInHierarchy) continue;
                float d = Vector3.Distance(transform.position, r.transform.position);
                if (d < damageRadius)
                {
                    int dmg = Mathf.Max(1, Mathf.RoundToInt(dps));
                    r.TakeDamage(dmg);
                    DamageNumber.CreateAt(r.transform.position + new Vector3(Random.Range(-0.5f, 0.5f), 0, 0), dmg, element);
                }
            }
        }
    }
}