using UnityEngine;

namespace BiopunkRV
{
    /// <summary>
    /// 落雷 — 雷灵根专属技能，挂在Player上。
    /// 学了雷灵根(ThunderSpiritRoot.IsActive)才解锁施放。
    /// 每4秒一道雷从天而降劈向随机敌人，雷数随雷灵根等级成长(Lv1-2一道/Lv3-5两道/Lv6-8三道/Lv9-10四道)。
    /// 视觉: 竖直闪电束从天而降闪3次 + 落地紫色冲击环。
    /// </summary>
    public class SkyThunderStrike : MonoBehaviour
    {
        [Header("施放参数")]
        public float castInterval = 4f;       // 施放间隔
        public float damageMult = 1.2f;       // 伤害倍率(单道)
        public float aoeRadius = 2.5f;        // 落雷AoE半径
        public int sortingOrder = 54;

        [Header("Affix System")]
        public AffixSystem affixSystem;

        private float _castTimer;
        private Transform _player;

        private void Awake()
        {
            _player = transform;
            if (affixSystem == null)
                affixSystem = AffixSystem.GetOrCreateFor(this);
        }

        private void Update()
        {
            // 解锁门控: 学了雷灵根才施放
            if (!ThunderSpiritRoot.IsActive) return;

            affixSystem.Tick();
            affixSystem.UpdateAffixes(ThunderSpiritRoot.Instance.level);

            _castTimer += Time.deltaTime;
            if (_castTimer >= castInterval)
            {
                _castTimer = 0f;
                Cast();
            }
        }

        /// <summary>雷道数随雷灵根等级成长</summary>
        private int StrikeCount => ThunderSpiritRoot.Instance != null
            ? 1 + (ThunderSpiritRoot.Instance.level >= 3 ? 1 : 0) + (ThunderSpiritRoot.Instance.level >= 6 ? 1 : 0) + (ThunderSpiritRoot.Instance.level >= 9 ? 1 : 0)
            : 1;

        private void Cast()
        {
            // 收集存活敌人
            var enemies = new System.Collections.Generic.List<EnemyController>();
            foreach (var e in FindObjectsOfType<EnemyController>())
                if (e != null && e.gameObject.activeInHierarchy && e.currentHealth > 0)
                    enemies.Add(e);
            if (enemies.Count == 0) return;

            int strikes = StrikeCount;
            // 优先劈离玩家最近的敌人 (落雷在玩家视野内可见)
            enemies.Sort((a, b) => ((a.transform.position - _player.position).sqrMagnitude)
                .CompareTo((b.transform.position - _player.position).sqrMagnitude));
            for (int i = 0; i < strikes; i++)
            {
                var target = enemies[i % enemies.Count];
                StartCoroutine(StrikeRoutine(target.transform.position, target.transform, i * 0.12f));
            }
        }

        /// <summary>单道落雷: 追踪目标(劈在敌人当前位置) + 闪3次竖直闪电束 + 冲击环 + AoE伤害 + 麻痹判定</summary>
        private System.Collections.IEnumerator StrikeRoutine(Vector3 initialPos, Transform trackTarget, float delay)
        {
            if (delay > 0f)
                yield return new WaitForSeconds(delay);

            // 追踪: 落雷劈在敌人当前位置 (敌人死亡则落在最后位置)
            Vector3 targetPos = trackTarget != null ? trackTarget.position : initialPos;

            // AoE伤害 (雷属性 → 走雷灵根麻痹判定)
            var psc = PlayerStatsComponent.Instance;
            var enemies = FindObjectsOfType<EnemyController>();
            foreach (var e in enemies)
            {
                if (e == null || !e.gameObject.activeInHierarchy) continue;
                float dx = targetPos.x - e.transform.position.x;
                float dz = targetPos.z - e.transform.position.z;
                if (dx * dx + dz * dz >= aoeRadius * aoeRadius) continue;

                bool isCrit = false;
                int dmg = 1;
                if (psc != null) dmg = psc.CalculateSkillDamage(damageMult, Element.Thunder, out isCrit);
                e.TakeDamage(dmg, Element.Thunder);
                DamageNumber.CreateAt(e.transform.position + new Vector3(Random.Range(-0.5f, 0.5f), 0, 0), dmg, Element.Thunder, isCrit);
                if (affixSystem != null)
                {
                    affixSystem.OnHitEnemy(e.transform, dmg, Element.Thunder);
                    if (e.currentHealth <= 0) affixSystem.OnKillEnemy();
                }
            }

            // 落地紫色冲击环
            AffixVisuals.PlayBurst(targetPos, Element.Thunder, aoeRadius, 0.3f);
            AudioManager.PlayEnemyHit();

            // 竖直落雷: 单个bolt对象存活全程0.33s, SpriteRenderer.enabled切换闪烁3次
            // (对象不销毁 — 4K低帧率下截屏/视觉都能稳定捕获, 不会落在销毁间隔里)
            var beamTex = Resources.Load<Texture2D>("SkyThunderBolt");
            if (beamTex == null) beamTex = Resources.Load<Texture2D>("LightningBeam"); // 回退
            if (beamTex == null) yield break;

            const float beamLen = 20f; // 从怪物身上向上延伸的长度(屏幕方向)
            var cam = Camera.main;
            Vector3 camUp = cam != null ? cam.transform.up : Vector3.up;
            Vector3 camRight = cam != null ? cam.transform.right : Vector3.right;
            Quaternion billboardRot = cam != null ? cam.transform.rotation : Quaternion.Euler(65.7f, 0f, 0f);

            Vector3 mid = targetPos + camUp * (beamLen * 0.5f);

            var go = new GameObject("SkyThunderBolt");
            go.transform.position = mid + camRight * Random.Range(-0.4f, 0.4f);
            go.transform.rotation = billboardRot;

            var sr = go.AddComponent<SpriteRenderer>();
            // PPU=100: 贴图世界尺寸=10.24×10.24单位
            sr.sprite = Sprite.Create(beamTex, new Rect(0, 0, beamTex.width, beamTex.height), new Vector2(0.5f, 0.5f), 100f);
            sr.color = Color.white; // 贴图自带紫色, 不再tint
            sr.sortingOrder = sortingOrder;

            // 纵向覆盖怪物→天上 (贴图闪电竖直, 屏幕Y轴=长度)
            float unitH = beamTex.height / 100f;
            float sy = beamLen / unitH;
            go.transform.localScale = new Vector3(1f, sy, 1f);

            // 3次闪烁: enabled切换 (对象全程存活)
            for (int i = 0; i < 3; i++)
            {
                sr.enabled = false;
                yield return new WaitForSeconds(0.03f);
                sr.enabled = true;
                yield return new WaitForSeconds(0.08f);
            }
            Object.Destroy(go);
        }
    }
}
