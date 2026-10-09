using UnityEngine;

namespace BiopunkRV
{
    /// <summary>
    /// 掉落类型 — 灵石(局外通用货币) / 功法碎片 / 词缀碎片
    /// </summary>
    public enum DropType { SpiritStone, MethodFragment, AffixFragment }

    /// <summary>
    /// 局外资产背包 — 本次运行的掉落累计。
    /// 存档接口预留: 游戏结束/通关时由局外系统读取并持久化。
    /// </summary>
    public static class RunInventory
    {
        public static int spiritStones;
        public static int methodFragments;
        public static int affixFragments;

        public static void Reset()
        {
            spiritStones = 0;
            methodFragments = 0;
            affixFragments = 0;
        }
    }

    /// <summary>
    /// 掉落物系统 — 敌人死亡时按规则掉落。
    /// 精英怪(isElite)必掉: 2灵石+1碎片(功法/词缀各50%)。
    /// 普通怪: 5%掉1灵石。
    /// </summary>
    public static class DropSystem
    {
        public static void OnEnemyDeath(Vector3 pos, bool isElite)
        {
            if (isElite)
            {
                SpawnDrop(pos + RandomOffset(0.6f), DropType.SpiritStone);
                SpawnDrop(pos + RandomOffset(0.6f), DropType.SpiritStone);
                SpawnDrop(pos + RandomOffset(0.6f), Random.value < 0.5f ? DropType.MethodFragment : DropType.AffixFragment);
            }
            else if (Random.value < 0.05f)
            {
                SpawnDrop(pos, DropType.SpiritStone);
            }
        }

        private static Vector3 RandomOffset(float range)
        {
            var v = Random.insideUnitSphere * range;
            v.y = 0f;
            return v;
        }

        private static void SpawnDrop(Vector3 pos, DropType type)
        {
            var go = new GameObject("Drop_" + type);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(77.38111f, 0, 0);

            float scale;
            Color color, lightColor;
            switch (type)
            {
                case DropType.SpiritStone:
                    scale = 0.35f; color = new Color(1f, 0.82f, 0.25f); lightColor = new Color(1f, 0.8f, 0.3f);
                    break;
                case DropType.MethodFragment:
                    scale = 0.32f; color = new Color(0.55f, 0.75f, 1f); lightColor = new Color(0.4f, 0.6f, 1f);
                    break;
                default: // AffixFragment
                    scale = 0.32f; color = new Color(0.8f, 0.5f, 1f); lightColor = new Color(0.7f, 0.4f, 1f);
                    break;
            }
            go.transform.localScale = new Vector3(scale, scale, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            // 专属贴图优先; 缺失时回退经验球+染色
            var tex = GetDropTexture(type);
            bool hasCustom = tex != null;
            if (tex == null) tex = Resources.Load<Texture2D>("SpiritOrb_Drop");
            if (tex != null)
                sr.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            sr.color = hasCustom ? Color.white : color;
            sr.sortingOrder = 51;

            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = lightColor;
            light.intensity = 1.6f;
            light.range = 3f;

            var item = go.AddComponent<DropItem>();
            item.dropType = type;

            Destroy2(go, 30f);
        }

        private static Texture2D GetDropTexture(DropType type)
        {
            switch (type)
            {
                case DropType.SpiritStone: return Resources.Load<Texture2D>("Drops/SpiritStone");
                case DropType.MethodFragment: return Resources.Load<Texture2D>("Drops/MethodFragment");
                case DropType.AffixFragment: return Resources.Load<Texture2D>("Drops/AffixFragment");
                default: return null;
            }
        }

        private static void Destroy2(Object go, float t) => Object.Destroy(go, t);
    }

    /// <summary>
    /// 掉落物拾取 — 漂浮+吸附+拾取入包 (模式同经验球SpiritOrbPickup)
    /// </summary>
    public class DropItem : MonoBehaviour
    {
        public DropType dropType;
        public float floatSpeed = 2.5f;
        public float floatHeight = 0.12f;
        public float attractRange = 3.5f;
        public float attractSpeed = 6f;
        public float pickupRange = 0.6f;

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

            if (_player == null) return;
            float dist = Vector3.Distance(transform.position, _player.position);

            if (dist < pickupRange)
            {
                switch (dropType)
                {
                    case DropType.SpiritStone: RunInventory.spiritStones++; RealmSystem.AddCultivationPoints(1); break;
                    case DropType.MethodFragment: RunInventory.methodFragments++; break;
                    case DropType.AffixFragment: RunInventory.affixFragments++; break;
                }
                Debug.Log($"[Drop] 拾取 {dropType} — 灵石{RunInventory.spiritStones}/功法碎{RunInventory.methodFragments}/词缀碎{RunInventory.affixFragments}");
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
