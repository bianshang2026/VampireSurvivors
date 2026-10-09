using UnityEngine;
using UnityEngine.UI;

namespace BiopunkRV
{
    /// <summary>
    /// 2D sticker ghost rabbit enemy with hop animation.
    /// Inherits movement, health, hit flash from EnemyController.
    /// </summary>
    public class GhostRabbitController : EnemyController
    {
        [Header("Sprites")]
        public Texture2D frontSprite;
        public Texture2D sideSprite;

        [Header("Hop Animation")]
        public float hopHeight = 0.25f;
        public float hopSpeed = 4f;
        public float squash = 0.15f;

        private bool _useSide;
        private static AudioSource _hurtAudio;

        protected override void OnAwake()
        {
            // Ghost Rabbit stats — 无属性
            maxHealth = 20f;
            currentHealth = 20f;
            attack = 5f;
            defense = 5f;
            element = Element.None;
            elementDefense = 0f;
            isRanged = false;

            if (_hurtAudio == null)
            {
                var sfxGO = GameObject.Find("SFX_RabbitHurt");
                if (sfxGO != null) _hurtAudio = sfxGO.GetComponent<AudioSource>();
            }
        }

        protected override void OnStart()
        {
            UpdateSprite();
        }

        protected override void AnimateMovement()
        {
            // Determine side sprite usage
            _useSide = false;
            if (target != null)
            {
                Vector3 toTarget = target.position - transform.position;
                toTarget.y = 0f;
                if (_isMoving && Mathf.Abs(toTarget.x) > Mathf.Abs(toTarget.z) * 0.7f && sideSprite != null)
                    _useSide = true;
            }
            UpdateSprite();

            // Hop animation
            if (_isMoving)
            {
                _animTimer += Time.deltaTime * hopSpeed;
                float cycle = Mathf.Sin(_animTimer * Mathf.PI);
                float bob = Mathf.Max(0, cycle) * hopHeight;
                float absCycle = Mathf.Abs(cycle);
                float scaleY = 1f + absCycle * squash;
                float scaleX = 1f - absCycle * squash * 0.5f;

                Vector3 pos = transform.position;
                pos.y = _baseY + bob;
                transform.position = pos;

                Vector3 scale = _baseScale;
                scale.x *= scaleX;
                scale.y *= scaleY;
                scale.x = _facingLeft ? -Mathf.Abs(scale.x) : Mathf.Abs(scale.x);
                transform.localScale = scale;
            }
            else
            {
                _animTimer = 0f;
                Vector3 pos = transform.position;
                pos.y = _baseY;
                transform.position = pos;
                Vector3 scale = _baseScale;
                scale.x = _facingLeft ? -Mathf.Abs(scale.x) : Mathf.Abs(scale.x);
                transform.localScale = scale;
            }
        }

        public override void TakeDamage(float damage)
        {
            base.TakeDamage(damage);
            if (_hurtAudio != null) _hurtAudio.Play();
        }

        private void UpdateSprite()
        {
            if (_mat == null) return;
            Texture2D tex = _useSide ? sideSprite : frontSprite;
            if (tex != null && _mat.mainTexture != tex)
                _mat.mainTexture = tex;
        }
    }

    /// <summary>
    /// Spirit orb pickup. Floats and gets attracted to player when close.
    /// </summary>
    public class SpiritOrbPickup : MonoBehaviour
    {
        public float floatSpeed = 2f;
        public float floatHeight = 0.1f;
        public float attractRange = 3f;
        public float attractSpeed = 5f;
        public float pickupRange = 0.5f;
        public int expValue = 20;  // 基础经验球 = 20点
        public enum OrbTier { Basic, Medium, Advanced }
        public OrbTier orbTier = OrbTier.Basic;

        private Transform _player;
        private float _timer;
        private Vector3 _basePos;

        private void Start()
        {
            var player = GameObject.Find("Player");
            if (player != null) _player = player.transform;
            _basePos = transform.position;
        }

        private void Update()
        {
            _timer += Time.deltaTime;
            
            if (_player != null)
            {
                float dist = Vector3.Distance(transform.position, _player.position);
                
                if (dist < pickupRange)
                {
                    // Add exp based on orb tier
                    int expAmount = orbTier == OrbTier.Basic ? 20 : orbTier == OrbTier.Medium ? 200 : 800;
                    ExpBar.AddExp(expAmount);
                    Destroy(gameObject);
                    return;
                }
                
                if (dist < attractRange)
                {
                    Vector3 dir = (_player.position - transform.position).normalized;
                    transform.position += dir * attractSpeed * Time.deltaTime;
                }
                else
                {
                    float bob = Mathf.Sin(_timer * floatSpeed) * floatHeight;
                    transform.position = _basePos + new Vector3(0, bob, 0);
                }
            }
        }
    }

    /// <summary>
    /// Exp bar controller. Fill scales with current exp.
    /// </summary>
    public class ExpBar : MonoBehaviour
    {
        public static float currentExp = 0f;  // 初始为空
        public static float maxExp = 100f;  // Level 1: 1²×100=100
        public static float currentLevel = 1f;
        public static float currentHP = 100f;
        public static float maxHP = 100f;

        // Exp orb values
        public const float EXP_ORB_BASIC = 20f;    // 基础经验球
        public const float EXP_ORB_MEDIUM = 200f;  // 中级经验球
        public const float EXP_ORB_ADVANCED = 800f; // 高级经验球

        private static RawImage _fill;
        private static RawImage _hpFill;
        private static float _fullWidth = 1994f;
        private static float _hpFullWidth = 360.9f;

        private void Start()
        {
            // Find ExpBar RawImage (the fill bar itself on HUD_Canvas)
            var expBar = GameObject.Find("ExpBar");
            if (expBar != null)
            {
                _fill = expBar.GetComponent<RawImage>();
                if (_fill == null)
                {
                    var fillT = expBar.transform.Find("Fill");
                    if (fillT != null) _fill = fillT.GetComponent<RawImage>();
                }
            }
            _hpFill = transform.Find("HPBarFill")?.GetComponent<RawImage>();
            
            if (_fill != null)
            {
                _fullWidth = _fill.rectTransform.sizeDelta.x;
                if (_fullWidth <= 0) _fullWidth = 360.9f;
            }
            
            if (_hpFill != null)
            {
                _hpFullWidth = _hpFill.rectTransform.sizeDelta.x;
                if (_hpFullWidth <= 0) _hpFullWidth = 360.9f;
            }
            
            UpdateFill();
            UpdateHPFill();
            UpdateLevelText();
        }

        public static void AddExp(float amount)
        {
            currentExp += amount;
            if (currentExp >= maxExp)
            {
                currentLevel++;
                currentExp = 0f;
                // Level up formula: n² × 100 (n = current level)
                maxExp = currentLevel * currentLevel * 100f;
                UpdateLevelText();
                
                // Trigger level up upgrade selection
                if (LevelUpSystem.Instance != null)
                    LevelUpSystem.Instance.ShowUpgrades();
                AudioManager.PlayLevelUp();
            }
            UpdateFill();
        }

        private static void UpdateLevelText()
        {
            var levelTextGO = GameObject.Find("LevelText");
            if (levelTextGO != null)
            {
                var tmp = levelTextGO.GetComponent<TMPro.TextMeshProUGUI>();
                if (tmp != null)
                    tmp.text = ((int)currentLevel).ToString();
            }
        }

        public static void TakeDamage(float damage)
        {
            currentHP = Mathf.Max(0, currentHP - damage);
            UpdateHPFill();
        }

        public static void SetMaxHP(float newMax)
        {
            maxHP = newMax;
            currentHP = newMax;
            UpdateHPFill();
        }

        private static void UpdateFill()
        {
            if (_fill == null)
            {
                var expBar = GameObject.Find("ExpBar");
                if (expBar != null)
                {
                    _fill = expBar.GetComponent<RawImage>();
                    if (_fill == null)
                    {
                        var fillT = expBar.transform.Find("Fill");
                        if (fillT != null) _fill = fillT.GetComponent<RawImage>();
                    }
                }
            }
            if (_fill == null) return;
            
            if (_fullWidth <= 0)
            {
                _fullWidth = _fill.rectTransform.sizeDelta.x;
                if (_fullWidth <= 0) _fullWidth = 1994f;
            }
            
            float pct = Mathf.Clamp01(currentExp / maxExp);
            float width = _fullWidth * pct;
            _fill.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        }

        private static void UpdateHPFill()
        {
            if (_hpFill == null)
            {
                var expBar = GameObject.Find("ExpBar");
                if (expBar != null)
                {
                    var hpT = expBar.transform.Find("HPBarFill");
                    if (hpT != null) _hpFill = hpT.GetComponent<RawImage>();
                }
            }
            if (_hpFill == null) return;

            float pct = Mathf.Clamp01(currentHP / maxHP);
            float width = _hpFullWidth * pct;
            _hpFill.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        }
    }
}
