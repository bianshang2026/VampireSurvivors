using UnityEngine;
using System.Collections.Generic;

namespace BiopunkRV
{
    /// <summary>
    /// 灵力阵技能 — 在玩家脚下生成法阵，持续对范围内敌人造成伤害。
    /// 动画：法阵从小到大缩放出现 + 持续旋转灵力流转。
    /// </summary>
    public class SpiritArraySkill : MonoBehaviour
    {
        [Header("Sprites")]
        public Sprite arraySprite;

        [Header("Settings")]
        public float baseDamageMultiplier = 0.5f;  // 技能伤害倍率 50%
        public float damageInterval = 0.5f;
        public float arrayRadius = 6f;
        public float arrayDuration = 3f;
        public float appearTime = 0.3f;
        public float rotateSpeed = 60f;
        public Element element = Element.None;
        public int sortingOrder = 40;

        [Header("Level")]
        public int skillLevel = 1;
        public float upgradeDamageMult = 1f;  // 升级伤害加成 (ApplyLevelEffects设置)
        public float radiusMultiplier = 1f;

        [Header("Affix System")]
        public AffixSystem affixSystem;
        public int evolutionCount = 0;
        public string currentDaoId = "";

        private GameObject _arrayObj;
        private SpriteRenderer _sr;
        private float _damageTimer;
        private float _lifeTimer;
        private float _currentScale;
        private bool _fullyAppeared;
        private List<Transform> _recentlyHit = new List<Transform>();

        private int CalculateDamage(out bool isCrit)
        {
            float mult = baseDamageMultiplier * upgradeDamageMult;
            Element el = SpiritRootManager.GetEffectiveElement(element);
            var psc = PlayerStatsComponent.Instance;
            if (psc != null) return psc.CalculateSkillDamage(mult * SpiritRootManager.GetDamageMult(el), el, out isCrit);
            isCrit = false;
            return Mathf.Max(1, Mathf.CeilToInt(40f * 0.15f * mult));
        }

        private void Awake()
        {
            // Ensure AffixSystem exists (每技能独立子物体)
            if (affixSystem == null)
                affixSystem = AffixSystem.GetOrCreateFor(this);

            // Load skill data from asset
            var data = SkillLibrary.LoadByName("SpiritArray");
            if (data != null)
            {
                baseDamageMultiplier = data.damageMultiplier;
                arrayRadius = data.baseRange;
                arrayDuration = data.cooldown;
                element = data.element;
            }

#if UNITY_EDITOR
            if (arraySprite == null)
                arraySprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/TJGenerators/History/Sprite_20260809_002731.png");
#endif
        }

        public void Activate(Vector3 position)
        {
            _arrayObj = new GameObject("SpiritArray");
            _arrayObj.transform.position = position;
            _arrayObj.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // 平铺地面

            _sr = _arrayObj.AddComponent<SpriteRenderer>();
            _sr.sprite = arraySprite;
            _sr.sortingOrder = sortingOrder;
            Element el = SpiritRootManager.GetEffectiveElement(element);
            _sr.color = new Color(ElementColors.GetTint(el).r, ElementColors.GetTint(el).g, ElementColors.GetTint(el).b, 0.8f);

            // 灵力光晕
            Light glow = _arrayObj.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = ElementColors.GetGlow(el);
            glow.intensity = 0.8f;
            glow.range = arrayRadius * radiusMultiplier * 2f;

            _currentScale = 0f;
            _fullyAppeared = false;
            _lifeTimer = 0f;
            _damageTimer = 0f;

            StartCoroutine(AnimateArray());
        }

        private System.Collections.IEnumerator AnimateArray()
        {
            // 阶段1: 从小到大出现
            float timer = 0f;
            float targetScale = arrayRadius * radiusMultiplier * 2f / 10f; // scale relative to sprite size

            while (timer < appearTime)
            {
                timer += Time.deltaTime;
                float t = timer / appearTime;
                _currentScale = Mathf.Lerp(0f, targetScale, t);
                _arrayObj.transform.localScale = new Vector3(_currentScale, _currentScale, 1f);

                // 淡入
                Color c = _sr.color;
                c.a = Mathf.Lerp(0f, 0.8f, t);
                _sr.color = c;

                yield return null;
            }

            _currentScale = targetScale;
            _arrayObj.transform.localScale = new Vector3(_currentScale, _currentScale, 1f);
            _fullyAppeared = true;

            // 阶段2: 持续旋转 + 定期伤害
            while (_lifeTimer < arrayDuration)
            {
                _lifeTimer += Time.deltaTime;

                // 旋转灵力流转
                _arrayObj.transform.Rotate(0, 0, rotateSpeed * Time.deltaTime, Space.Self);

                // 定期伤害
                _damageTimer += Time.deltaTime;
                if (_damageTimer >= damageInterval)
                {
                    _damageTimer = 0f;
                    DealDamage();
                }

                // 临近消失时淡出
                float remaining = arrayDuration - _lifeTimer;
                if (remaining < 0.5f)
                {
                    Color c = _sr.color;
                    c.a = Mathf.Lerp(0f, 0.8f, remaining / 0.5f);
                    _sr.color = c;
                }

                yield return null;
            }

            // 销毁
            if (_arrayObj != null)
                Destroy(_arrayObj);
        }

        private void DealDamage()
        {
            Vector3 center = _arrayObj.transform.position;
            float radius = arrayRadius * radiusMultiplier;
            _recentlyHit.Clear();
            Element el = SpiritRootManager.GetEffectiveElement(element);

            var enemies = FindObjectsOfType<EnemyController>();
            foreach (var e in enemies)
            {
                if (e == null || !e.gameObject.activeInHierarchy) continue;
                float dist = Vector3.Distance(center, e.transform.position);
                if (dist < radius)
                {
                    int dmg = CalculateDamage(out bool isCrit);
                    e.TakeDamage(dmg, el);
                    DamageNumber.CreateAt(e.transform.position + new Vector3(Random.Range(-0.3f, 0.3f), 0, 0), dmg, el, isCrit);
                    _recentlyHit.Add(e.transform);
                }
            }

            var rabbits = FindObjectsOfType<GhostRabbitController>();
            foreach (var r in rabbits)
            {
                if (r == null || !r.gameObject.activeInHierarchy) continue;
                if (_recentlyHit.Contains(r.transform)) continue;
                float dist = Vector3.Distance(center, r.transform.position);
                if (dist < radius)
                {
                    int dmg = CalculateDamage(out bool isCrit);
                    r.TakeDamage(dmg, el);
                    DamageNumber.CreateAt(r.transform.position + new Vector3(Random.Range(-0.3f, 0.3f), 0, 0), dmg, el, isCrit);
                }
            }
        }

        /// <summary>
        /// 升级灵力阵
        /// </summary>
        public void LevelUp()
        {
            skillLevel = Mathf.Min(10, skillLevel + 1);
            ApplyLevelEffects();

            if (skillLevel >= 10 && evolutionCount == 0)
            {
                Element el = SpiritRootManager.GetEffectiveElement(element);
                string[] options = DaoSystem.GetDaoOptions("SpiritArray", el);
                if (options.Length == 0) return;
                EnlightenmentChoice.Show(options, selectedDao =>
                {
                    currentDaoId = selectedDao;
                    evolutionCount = 1;
                    skillLevel = 1;
                    baseDamageMultiplier *= 1.5f;
                    arrayRadius *= 1.2f;
                    DaoSystem.ApplyDaoEffect(this, currentDaoId);
                    Debug.Log($"[SpiritArray] 悟道: {DaoSystem.GetDaoName(currentDaoId)}");
                });
            }
            else if (skillLevel >= 10 && evolutionCount >= 1)
            {
                evolutionCount = 2;
                DaoSystem.TriggerDivine(this, currentDaoId, transform);
                Debug.Log($"[SpiritArray] 神通发动: {DaoSystem.GetDaoName("Divine_" + currentDaoId)}");
            }
        }

        private void ApplyLevelEffects()
        {
            switch (skillLevel)
            {
                case 2:
                    upgradeDamageMult = 1.2f;
                    break;
                case 3:
                    radiusMultiplier = 1.2f;
                    break;
                case 4:
                    damageInterval = 0.4f;
                    break;
                case 5:
                    upgradeDamageMult = 1.5f;
                    radiusMultiplier = 1.3f;
                    break;
                case 6:
                    arrayDuration = 4f;
                    break;
                case 7:
                    upgradeDamageMult = 1.8f;
                    break;
                case 8:
                    radiusMultiplier = 1.5f;
                    damageInterval = 0.3f;
                    break;
                case 9:
                    upgradeDamageMult = 2.2f;
                    arrayDuration = 5f;
                    break;
                case 10:
                    upgradeDamageMult = 3f;
                    radiusMultiplier = 2f;
                    damageInterval = 0.25f;
                    arrayDuration = 6f;
                    break;
            }
        }
    }
}


