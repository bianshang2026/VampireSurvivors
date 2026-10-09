using UnityEngine;
using System.Collections.Generic;

namespace BiopunkRV
{
    public class DecoSpawner : MonoBehaviour
    {
        [Header("地图范围")]
        public float mapRadius = 80f;
        public float minDist = 7f;      // 装饰物最小间距 (2026-09-02: 4太密, 加大分散)
        public float decoY = 2.2f;

        [Header("装饰物数量配置")]
        public int boneCount = 25;       // 骨头 最多
        public int mushroomCount = 20;   // 蘑菇
        public int mossCount = 20;       // 苔藓
        public int crystalCount = 10;    // 水晶矿 第二少
        public int rockCount = 5;        // 大石头 最少
        public int rubbleCount = 5;      // 碎石 最少
        public int stalactiteCount = 5;  // 钟乳石 最少

        [Header("摆放参数")]
        public Vector3 decoRotation = new Vector3(61.544f, 343.401f, 346.688f);
        public float baseScaleX = 0.804f;
        public float baseScaleY = 0.799f;
        public float scaleMin = 0.5f;
        public float scaleMax = 1.3f;

        // 精灵路径
        const string PathBone = "EnvPropV2_00";
        const string PathStalactite = "EnvPropV2_01";
        const string PathMoss = "EnvPropV2_02";
        const string PathMushroom = "EnvPropV2_04";
        const string PathCrystal = "EnvPropV2_05";
        const string PathRock = "Rock_Large";
        const string PathRubblePrefix = "RubbleV2_";

        // 运行时缓存的Sprite
        private Dictionary<string, Sprite> spriteCache = new Dictionary<string, Sprite>();
        private Material decoMat;
        private List<Vector3> occupiedPositions = new List<Vector3>();

        void Start()
        {
            SpawnAllDecorations();
        }

        void LoadSprites()
        {
            // 从Resources加载所有装饰物贴图
            Sprite[] allSprites = Resources.LoadAll<Sprite>("Decorations");
            if (allSprites != null && allSprites.Length > 0)
            {
                foreach (var s in allSprites)
                    spriteCache[s.name] = s;
            }

            // 也尝试单独加载
            string[] names = { PathBone, PathStalactite, PathMoss, PathMushroom, PathCrystal, PathRock };
            foreach (var name in names)
            {
                if (!spriteCache.ContainsKey(name))
                {
                    var tex = Resources.Load<Texture2D>($"Decorations/{name}");
                    if (tex != null)
                        spriteCache[name] = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                }
            }

            // 加载碎石RubbleV2_00~05
            for (int i = 0; i < 6; i++)
            {
                string name = $"{PathRubblePrefix}{i:D2}";
                if (!spriteCache.ContainsKey(name))
                {
                    var tex = Resources.Load<Texture2D>($"Decorations/{name}");
                    if (tex != null)
                        spriteCache[name] = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                }
            }
        }

        public void SpawnAllDecorations()
        {
            LoadSprites();

            // 创建材质
            if (decoMat == null)
            {
                var shader = Shader.Find("Sprites/Default");
                if (shader != null)
                    decoMat = new Material(shader);
            }

            // 创建/清空父物体
            Transform parent = transform.Find("Deco_Squares");
            if (parent != null)
            {
                for (int i = parent.childCount - 1; i >= 0; i--)
                    Destroy(parent.GetChild(i).gameObject);
            }
            else
            {
                var go = new GameObject("Deco_Squares");
                go.transform.SetParent(transform, false);
                parent = go.transform;
            }

            occupiedPositions.Clear();

            // 生成顺序：多的先生成（优先占据好位置）
            SpawnType(PathBone, boneCount, false, true, parent);        // 骨头 Y-flip
            SpawnType(PathMushroom, mushroomCount, false, true, parent); // 蘑菇 Y-flip
            SpawnType(PathMoss, mossCount, false, true, parent);        // 苔藓 Y-flip
            SpawnType(PathCrystal, crystalCount, false, true, parent);  // 水晶矿 Y-flip
            SpawnType(PathRock, rockCount, false, false, parent);       // 大石头 不翻转
            SpawnType(PathStalactite, stalactiteCount, false, true, parent); // 钟乳石 Y-flip

            // 碎石：随机选RubbleV2_00~05
            for (int i = 0; i < rubbleCount; i++)
            {
                int idx = Random.Range(0, 6);
                string rubbleName = $"{PathRubblePrefix}{idx:D2}";
                SpawnSingle(rubbleName, false, true, parent);
            }
        }

        void SpawnType(string spriteName, int count, bool varyScale, bool yFlip, Transform parent)
        {
            for (int i = 0; i < count; i++)
                SpawnSingle(spriteName, varyScale, yFlip, parent);
        }

        void SpawnSingle(string spriteName, bool varyScale, bool yFlip, Transform parent)
        {
            if (!spriteCache.TryGetValue(spriteName, out Sprite sprite) || sprite == null)
                return;

            Vector3 pos = GetRandomPosition();
            if (pos == Vector3.zero && occupiedPositions.Count > 0)
                return; // 找不到合适位置

            var go = new GameObject($"Deco_{spriteName}");
            go.transform.SetParent(parent, false);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = Color.white;
            sr.sortingOrder = 0;
            if (decoMat != null)
                sr.sharedMaterial = decoMat;

            go.transform.rotation = Quaternion.Euler(decoRotation);

            float sx = baseScaleX;
            float sy = baseScaleY;

            if (varyScale)
            {
                float mult = Random.Range(scaleMin, scaleMax);
                sx *= mult;
                sy *= mult;
            }

            if (yFlip)
                sy = -sy;

            go.transform.localScale = new Vector3(sx, sy, 1f);
            go.transform.position = pos;

            // 碰撞体积: 除骨头外的装饰物都加 (挡住玩家; 骨头平躺贴地可穿过)
            if (spriteName != PathBone)
            {
                // 精灵世界footprint折算碰撞半径 (倾斜贴图的地面投影近似)
                float worldW = sprite.bounds.size.x * Mathf.Abs(sx);
                float worldD = sprite.bounds.size.y * Mathf.Abs(sx) * 0.85f;
                float worldR = Mathf.Max(worldW, worldD) * 0.45f; // 0.45倍: 略小于视觉避免透明角挡路
                worldR = Mathf.Max(1.2f, worldR);
                var col = go.AddComponent<SphereCollider>();
                float maxAbs = Mathf.Max(Mathf.Abs(sx), Mathf.Abs(sy), 1f);
                col.radius = Mathf.Clamp(worldR / maxAbs, 0.3f, 6f);
                col.isTrigger = false;
                go.AddComponent<DecoObstacle>(); // 玩家CheckCollision识别此标记
            }

            occupiedPositions.Add(pos);
        }

        Vector3 GetRandomPosition()
        {
            int attempts = 0;
            int maxAttempts = 300; // 间距加大后需要更多尝试次数
            while (attempts < maxAttempts)
            {
                attempts++;
                float angle = Random.Range(0f, Mathf.PI * 2f);
                float dist = Random.Range(8f, mapRadius);
                Vector3 pos = new Vector3(
                    Mathf.Cos(angle) * dist,
                    decoY,
                    Mathf.Sin(angle) * dist
                );

                bool tooClose = false;
                foreach (var ep in occupiedPositions)
                {
                    float dx = pos.x - ep.x;
                    float dz = pos.z - ep.z;
                    if (dx * dx + dz * dz < minDist * minDist)
                    {
                        tooClose = true;
                        break;
                    }
                }

                if (!tooClose)
                    return pos;
            }

            return Vector3.zero;
        }
    }

    /// <summary>装饰物障碍物标记 — 挂在有碰撞体积的装饰物上, 玩家CheckCollision检测此组件即阻挡</summary>
    public class DecoObstacle : MonoBehaviour
    {
    }
}
