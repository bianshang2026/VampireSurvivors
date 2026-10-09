using UnityEngine;

namespace BiopunkRV
{
    /// <summary>
    /// 雷球怪敌人: 雷属性远程怪, 发射紫色雷弹, 缓慢漂浮。
    /// 继承 EnemyController 基类的移动/分离/血条/受击/死亡系统。
    /// 使用 SpriteRenderer。
    /// </summary>
    public class ThunderOrbController : EnemyController
    {
        [Header("雷球怪动画")]
        public float floatHeight = 0.12f;
        public float swayAngle = 8f;

        private SpriteRenderer _sr;
        private float _bobTimer;
        private Quaternion _baseRot;

        protected override void OnAwake()
        {
            // 雷球怪属性 — 雷属性远程
            maxHealth = 30f;
            currentHealth = 30f;
            attack = 8f;
            defense = 8f;
            element = Element.Thunder;
            elementDefense = 0f;
            isRanged = true;
            attackRange = 16f;
            attackInterval = 3f;
            projectileSpeed = 12f;
            moveSpeed = 3.5f;
            stopDistance = 2.4f;
            separationRadius = 3f;
            separationForce = 3f;

            _sr = GetComponent<SpriteRenderer>();
            if (_sr == null)
                _sr = gameObject.AddComponent<SpriteRenderer>();
            _sr.sortingOrder = 15;

            _baseRot = transform.localRotation;
        }

        /// <summary>
        /// 雷球怪动画: 缓慢漂浮 + 轻微左右摇摆 + 缩放脉动
        /// </summary>
        protected override void AnimateMovement()
        {
            _bobTimer += Time.deltaTime * 2.5f;

            Vector3 pos = transform.position;
            pos.y = _baseY + Mathf.Sin(_bobTimer) * floatHeight;
            transform.position = pos;

            float pulse = 1f + Mathf.Sin(_bobTimer * 2f) * 0.05f;
            Vector3 scale = _baseScale * pulse;
            scale.x = _facingLeft ? -Mathf.Abs(scale.x) : Mathf.Abs(scale.x);
            transform.localScale = scale;

            transform.localRotation = _baseRot * Quaternion.Euler(0f, 0f, Mathf.Sin(_bobTimer) * swayAngle);
        }

        /// <summary>
        /// 重写投射物: 发射雷球贴图(专属贴图不tint, 保持原色)
        /// </summary>
        protected override void FireProjectile()
        {
            if (target == null) return;

            Vector3 dir = target.position - transform.position;
            dir.y = 0f;
            dir.Normalize();

            GameObject proj = new GameObject("EnemyProjectile");
            proj.transform.position = transform.position + Vector3.up * 0.5f;

            SpriteRenderer sr = proj.AddComponent<SpriteRenderer>();
            sr.color = Color.white;
            sr.sortingOrder = 55;

            var tex = Resources.Load<Texture2D>("ThunderOrb");
            if (tex != null)
                sr.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.height);
            else
                sr.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
            proj.transform.localScale = new Vector3(1.5f, 1.5f, 1f);

            float angle = Mathf.Atan2(dir.z, dir.x) * Mathf.Rad2Deg + 90f;
            proj.transform.rotation = Quaternion.Euler(65.69891f, 0, angle);

            Light glow = proj.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = new Color(0.7f, 0.4f, 1f);
            glow.intensity = 1.5f;
            glow.range = 2f;

            EnemyProjectile ep = proj.AddComponent<EnemyProjectile>();
            ep.velocity = dir * projectileSpeed;
            ep.damage = attack;
            ep.element = element;
            ep.lifetime = 15f;
            Destroy(proj, 15f);
        }
    }
}
