using UnityEngine;

namespace BiopunkRV
{
    /// <summary>
    /// 石巨人 — 土系精英怪 (HP800/ATK32/DEF160/Earth, 必掉灵石+功法碎片)
    /// 远处投石(RockOrb岩石投射物), 玩家靠近时践踏(AOE伤害+击退)。
    /// 蓄力抬脚 → 落地践踏 → 土系爆发圆环 + 玩家击退。
    /// </summary>
    public class StoneGolemController : EnemyController
    {
        [Header("投石")]
        public float rockThrowRange = 16f;      // 投石触发距离(也是停下距离)
        public float rockThrowInterval = 3.5f;  // 投石间隔
        public float rockSpeed = 14f;
        public float rockDamageMult = 1f;
        public float rockWorldSize = 3f;        // 岩石投射物世界尺寸

        [Header("践踏")]
        public float stompRadius = 6f;          // 践踏生效半径
        public float stompDamageMult = 1.5f;    // 践踏伤害倍率(×attack)
        public float stompKnockback = 12f;      // 玩家击退力度
        public float stompTelegraph = 0.7f;     // 蓄力(抬脚)时长
        public float stompInterval = 6f;        // 践踏冷却

        [Header("动画")]
        public float bobHeight = 0.06f;
        public float bobSpeed = 2f;

        [Header("走路序列帧")]
        public float walkFPS = 3.5f;

        private SpriteRenderer _sr;
        private float _stompTimer;
        private float _stompAnimTimer;
        private bool _stomping;
        private Sprite[] _walkFrames;
        private Sprite _idleSprite;
        private int _frameIndex;
        private float _frameTimer;
        private float _walkScaleMult = 1f; // 走路帧与原始贴图的宽度比补偿

        protected override void OnAwake()
        {
            maxHealth = 800f;
            currentHealth = 800f;
            attack = 32f;
            defense = 160f;
            element = Element.Earth;
            elementDefense = 20f;
            isRanged = true;      // 投石远程 (基类在attackRange内停下, FireProjectile被重写为扔岩石)
            isElite = true;       // 精英怪 — 必掉灵石+功法碎片
            moveSpeed = 3f;
            stopDistance = 3f;
            separationRadius = 4.5f;
            attackRange = rockThrowRange;
            attackInterval = rockThrowInterval;
            projectileSpeed = rockSpeed;
            _sr = GetComponent<SpriteRenderer>();
            _baseY += 1f;
        }

        protected override void OnStart()
        {
            _stompTimer = stompInterval * 0.5f; // 出场稍等再可践踏
            if (_sr != null && _sr.sprite != null)
                _idleSprite = _sr.sprite;

            // 加载走路序列帧 (Resources/StoneGolemWalkFrames, Sprite/Single导入)
            _walkFrames = Resources.LoadAll<Sprite>("StoneGolemWalkFrames");
            if (_walkFrames != null && _walkFrames.Length > 0)
            {
                System.Array.Sort(_walkFrames, (a, b) => a.name.CompareTo(b.name));

                // 帧宽度与原始贴图不同 → 补偿scale保持世界尺寸一致
                if (_idleSprite != null)
                {
                    float idleW = _idleSprite.bounds.size.x;
                    float frameW = _walkFrames[0].bounds.size.x;
                    if (frameW > 0.001f)
                        _walkScaleMult = idleW / frameW;
                }
                Debug.Log($"[StoneGolem] Loaded {_walkFrames.Length} walk frames, scaleMult={_walkScaleMult:F3}");

                // 立即切到站立首帧, 避免原贴图配补偿scale的尺寸闪变
                if (_sr != null)
                    _sr.sprite = _walkFrames[0];
            }
        }

        /// <summary>投石: 棕色岩石(RockOrb)直线飞行, Earth属性</summary>
        protected override void FireProjectile()
        {
            if (target == null) return;

            Vector3 dir = target.position - transform.position;
            dir.y = 0f;
            dir.Normalize();

            GameObject proj = new GameObject("GolemRock");
            proj.transform.position = transform.position + Vector3.up * 1.2f;

            SpriteRenderer sr = proj.AddComponent<SpriteRenderer>();
            sr.color = Color.white;
            sr.sortingOrder = 55;

            var rockTex = Resources.Load<Texture2D>("RockOrb");
            if (rockTex != null)
                sr.sprite = Sprite.Create(rockTex, new Rect(0, 0, rockTex.width, rockTex.height), new Vector2(0.5f, 0.5f), 100f);
            else
                sr.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);

            // 岩石尺寸归一化: 世界尺寸=rockWorldSize, 与贴图PPU无关
            float unitWidth = sr.sprite.bounds.size.x;
            float sc = unitWidth > 0.01f ? rockWorldSize / unitWidth : 1f;
            proj.transform.localScale = new Vector3(sc, sc, 1f);
            proj.transform.rotation = Quaternion.Euler(65.69891f, 0, Random.Range(0f, 360f));

            EnemyProjectile ep = proj.AddComponent<EnemyProjectile>();
            ep.velocity = dir * projectileSpeed;
            ep.damage = attack * rockDamageMult;
            ep.element = Element.Earth;
            ep.lifetime = 8f;
            Destroy(proj, 8f);
        }

        protected override void AnimateMovement()
        {
            _stompTimer += Time.deltaTime;

            // 践踏触发: 玩家进入半径且冷却完成
            if (!_stomping && _stompTimer >= stompInterval && target != null)
            {
                Vector3 toT = target.position - transform.position;
                toT.y = 0f;
                if (toT.magnitude <= stompRadius)
                {
                    _stomping = true;
                    _stompAnimTimer = 0f;
                }
            }

            // 践踏蓄力: 抬脚(拉高+收窄)
            if (_stomping)
            {
                _stompAnimTimer += Time.deltaTime;
                float t = Mathf.Clamp01(_stompAnimTimer / stompTelegraph);
                float raise = Mathf.Sin(t * Mathf.PI * 0.5f) * 0.25f;

                Vector3 scale = BaseDisplayScale;
                scale.x *= 1f - raise * 0.3f;
                scale.y *= 1f + raise;
                scale.x = _facingLeft ? -Mathf.Abs(scale.x) : Mathf.Abs(scale.x);
                transform.localScale = scale;

                Vector3 pos = transform.position;
                pos.y = _baseY + raise * 0.5f;
                transform.position = pos;

                if (_stompAnimTimer >= stompTelegraph)
                {
                    _stomping = false;
                    _stompTimer = 0f;
                    DoStomp();

                    // 落地瞬间下压
                    Vector3 slam = BaseDisplayScale;
                    slam.y *= 0.82f;
                    slam.x = _facingLeft ? -Mathf.Abs(slam.x) : Mathf.Abs(slam.x);
                    transform.localScale = slam;
                    Vector3 p = transform.position;
                    p.y = _baseY;
                    transform.position = p;
                }

                _isMoving = false;
                return;
            }

            // 正常行走: 缓慢沉重bob + 序列帧
            if (_isMoving)
            {
                _animTimer += Time.deltaTime * bobSpeed;
                float bob = Mathf.Abs(Mathf.Sin(_animTimer * Mathf.PI)) * bobHeight;
                Vector3 pos = transform.position;
                pos.y = _baseY + bob;
                transform.position = pos;

                // 走路序列帧 (FPS随移速, 基准moveSpeed=3)
                if (_walkFrames != null && _walkFrames.Length > 0 && _sr != null)
                {
                    _frameTimer += Time.deltaTime;
                    float effectiveFPS = walkFPS * (moveSpeed / 3f);
                    if (effectiveFPS < 2f) effectiveFPS = 2f;
                    float frameDur = 1f / effectiveFPS;
                    if (_frameTimer >= frameDur)
                    {
                        _frameTimer -= frameDur;
                        _frameIndex = (_frameIndex + 1) % _walkFrames.Length;
                        _sr.sprite = _walkFrames[_frameIndex];
                    }
                }
            }
            else
            {
                Vector3 pos = transform.position;
                pos.y = _baseY;
                transform.position = pos;

                // 静止: 恢复站立帧
                Sprite idle = IdleDisplaySprite;
                if (idle != null && _sr != null && _sr.sprite != idle)
                    _sr.sprite = idle;
                _frameIndex = 0;
                _frameTimer = 0f;
            }

            Vector3 s = BaseDisplayScale;
            s.x = _facingLeft ? -Mathf.Abs(s.x) : Mathf.Abs(s.x);
            transform.localScale = s;
        }

        /// <summary>当前显示用的基础scale: 走路帧与原贴图宽度不同时做尺寸归一化补偿</summary>
        private Vector3 BaseDisplayScale
        {
            get
            {
                Vector3 s = _baseScale;
                s.x *= _walkScaleMult;
                s.y *= _walkScaleMult;
                return s;
            }
        }

        /// <summary>静止站立帧: 有序列帧用首帧(与走路帧同尺寸), 否则用原始贴图</summary>
        private Sprite IdleDisplaySprite =>
            (_walkFrames != null && _walkFrames.Length > 0) ? _walkFrames[0] : _idleSprite;

        /// <summary>践踏: 土系爆发圆环 + 半径内玩家受伤+击退</summary>
        private void DoStomp()
        {
            AffixVisuals.PlayBurst(transform.position, Element.Earth, stompRadius, 0.4f);

            if (target == null) return;
            Vector3 toT = target.position - transform.position;
            toT.y = 0f;
            float dist = toT.magnitude;
            if (dist > stompRadius) return;

            var psc = target.GetComponent<PlayerStatsComponent>();
            if (psc != null && !psc.godMode)
                psc.stats.TakeDamage(attack * stompDamageMult, true);

            var cc = target.GetComponent<CharacterController2D>();
            if (cc != null && toT.sqrMagnitude > 0.01f)
                cc.ApplyKnockback(toT.normalized, stompKnockback);
        }
    }
}
