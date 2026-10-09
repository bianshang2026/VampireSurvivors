using UnityEngine;

namespace BiopunkRV
{
    /// <summary>
    /// 龙 Ribbon 游动器 — 整条龙贴图蒙在"精灵平面内"的连续网格带上。
    /// 与角色同视角: 龙横贯头顶(头右尾左), 行波沿龙身从头传到尾(振幅头小尾大),
    /// 在精灵平面内持续起伏 + 整体缓慢水平巡游漂移 + 浮动。
    /// 每列顶点用贴图自身的上下轮廓(角/腿/尾随列走), 龙剪影完整保留, 零切缝零片段。
    /// ribbonTexture = 脊柱已对齐中线的直线龙贴图 (Resources/Dao_Dragon_Ribbon)。
    /// 根物体 = 锚点(角色上方)。
    /// </summary>
    public class DragonSwimmer : MonoBehaviour
    {
        [Header("贴图")]
        public Texture2D ribbonTexture;   // 空则自动加载 Resources/Dao_Dragon_Ribbon
        public float ribbonScale = 0.6f; // 世界单位 / 100贴图像素

        [Header("锚点")]
        public Transform anchorTarget;    // 空=自动找Player
        public Vector3 anchorOffset = new Vector3(0f, 15f, 0f); // 高于头顶, 腹带投影也不压角色

        [Header("行波蜿蜒 (水平面内, 龙身高度恒定平飞)")]
        public float ampHead = 0.22f;   // 头部蜿蜒幅度 — 全身均匀微幅, 头部也有极轻微律动
        public float ampTail = 0.3f;    // 尾部蜿蜒幅度 — 与头部接近(均匀, 无铰链折点)
        public float wavelength = 7.5f; // 波长
        public float waveFreq = 0.5f;   // 波频(Hz)
        [Range(0f, 1f)]
        public float tiltStrength = 0f; // 截面倾斜强度 — 0=不旋转(无折叠感), >0按需加

        [Header("整体巡游漂移 (水平方向)")]
        public float driftAmp = 3f;     // 水平巡游幅度
        public float driftSpeed = 0.25f; // 巡游频率(Hz)
        public float bobAmp = 0f;       // 竖直浮动幅度(默认0: 龙平飞不上蹿下跳)
        public float bobSpeed = 0.9f;   // 浮动频率

        [Header("渲染")]
        public Color tint = Color.white;
        public int sortingOrder = 60;

        private const int Columns = 48;
        private GameObject _meshGo;
        private Mesh _mesh;
        private Material _mat;
        private Vector3[] _verts;
        private float _bodyLenWorld;  // 龙全长(世界)
        private float[] _topExt;      // 每贴图列: 脊柱到内容顶(px)
        private float[] _botExt;      // 脊柱到内容底(px)
        private float[] _vTop, _vBot; // 每列UV v(静态)
        private float[] _uCol;        // 每列UV u
        private int _texW, _texH, _bx0, _bx1;
        private Transform _anchor;
        private float _t;
        // 行波形变缓存 (避免每帧分配)
        private readonly Vector3[] _spine = new Vector3[Columns];
        private readonly float[] _spineY = new float[Columns];   // 水平Z方向蜿蜒位移
        private readonly Vector3[] _colN = new Vector3[Columns];
        private readonly float[] _colAng = new float[Columns];  // 每列水平面内切线角
        private Vector3 _smoothAnchor; // 低通后的锚点(玩家瞬移不传导)

        // 精灵平面基向量 (Euler(65.7,0,0)): 平面内水平=世界X, 平面内上=(0,cos65.7,sin65.7)
        private static readonly Vector3 Right = new Vector3(1f, 0f, 0f);
        private static readonly Vector3 PlaneUp = new Vector3(0f, 0.4123f, 0.9110f);
        private static readonly Vector3 Forward = new Vector3(0f, 0f, 1f);

        private void Start()
        {
            if (anchorTarget == null)
            {
                var p = GameObject.Find("Player");
                if (p != null) anchorTarget = p.transform;
            }
            if (ribbonTexture == null) ribbonTexture = Resources.Load<Texture2D>("Dao_Dragon_Ribbon");
            if (ribbonTexture == null) { enabled = false; return; }

            var px = ribbonTexture.GetPixels32();
            _texW = ribbonTexture.width; _texH = ribbonTexture.height;
            int mid = _texH / 2;
            _bx0 = -1; _bx1 = -1;
            _topExt = new float[_texW];
            _botExt = new float[_texW];
            for (int x = 0; x < _texW; x++)
            {
                int yMin = int.MaxValue, yMax = -1;
                for (int y = 0; y < _texH; y++)
                    if (px[y * _texW + x].a > 10) { if (y < yMin) yMin = y; if (y > yMax) yMax = y; }
                if (yMax < 0) { _topExt[x] = 0f; _botExt[x] = 0f; continue; }
                if (_bx0 < 0) _bx0 = x;
                _bx1 = x;
                // GetPixels32的y=0在图片底部: yMin=内容底边(腿), yMax=内容顶边(龙背/角)
                _topExt[x] = Mathf.Max(0f, yMax - mid);  // 中线向上到内容顶(龙背)
                _botExt[x] = Mathf.Max(0f, mid - yMin);  // 中线向下到内容底(腿)
            }
            _bodyLenWorld = (_bx1 - _bx0) / 100f * ribbonScale;

            // 网格: 48列×2顶点, UV静态(每列用自己的上下轮廓v → 龙剪影完整保留)
            _verts = new Vector3[Columns * 2];
            _uCol = new float[Columns];
            _vTop = new float[Columns];
            _vBot = new float[Columns];
            var uv = new Vector2[Columns * 2];
            var tris = new int[(Columns - 1) * 6];
            for (int j = 0; j < Columns; j++)
            {
                // j=0=尾(u在左) → j=N-1=头(u在右); 位置上头在+x(右)
                float u = (_bx0 + (_bx1 - _bx0) * (float)j / (Columns - 1)) / _texW;
                int texX = Mathf.Clamp(Mathf.RoundToInt(_bx0 + (_bx1 - _bx0) * (float)j / (Columns - 1)), 0, _texW - 1);
                _uCol[j] = u;
                _vTop[j] = (mid + _topExt[texX]) / (float)_texH;  // 内容顶边v(大) — 龙背
                _vBot[j] = (mid - _botExt[texX]) / (float)_texH;  // 内容底边v(小) — 腿
                uv[j * 2] = new Vector2(u, _vTop[j]);
                uv[j * 2 + 1] = new Vector2(u, _vBot[j]);
                if (j < Columns - 1)
                {
                    int t = j * 6;
                    tris[t] = j * 2; tris[t + 1] = j * 2 + 1; tris[t + 2] = j * 2 + 3;
                    tris[t + 3] = j * 2; tris[t + 4] = j * 2 + 3; tris[t + 5] = j * 2 + 2;
                }
            }
            _meshGo = new GameObject("DragonRibbonMesh");
            _mesh = new Mesh { name = "DragonRibbon" };
            _mesh.MarkDynamic();
            _mesh.vertices = _verts;
            _mesh.uv = uv;
            _mesh.triangles = tris;
            _meshGo.AddComponent<MeshFilter>().sharedMesh = _mesh;
            var mr = _meshGo.AddComponent<MeshRenderer>();
            _mat = new Material(Shader.Find("Sprites/Default")) { mainTexture = ribbonTexture, color = tint };
            mr.sharedMaterial = _mat;
            mr.sortingOrder = sortingOrder;
            _smoothAnchor = anchorTarget != null ? anchorTarget.position + anchorOffset : Vector3.zero;
        }

        private void Update()
        {
            if (_mesh == null || anchorTarget == null) return;
            _t += Time.deltaTime;

            // 锚点: 目标上方 + 缓慢水平巡游漂移; 低通滤波(3Hz) —
            // 玩家受击/挤开的瞬移不会传导成龙跳动, 只有平滑跟随
            Vector3 targetAnchor = anchorTarget.position + anchorOffset;
            targetAnchor.x += Mathf.Sin(_t * driftSpeed * 2f * Mathf.PI) * driftAmp;
            float smoothK = 1f - Mathf.Exp(-Time.deltaTime * 3f);
            _smoothAnchor = Vector3.Lerp(_smoothAnchor, targetAnchor, smoothK);
            Vector3 anchor = _smoothAnchor;

            // 行波: 头在+x端, 波从头传向尾(相位沿-x递增); 振幅头小尾大
            // ── 脊柱: 蜿蜒发生在水平面内(蛇形S形游动, 俯视平面意义上的起伏), 高度不变 ──
            float bodyLen = _bodyLenWorld;
            float k = 2f * Mathf.PI / Mathf.Max(1f, wavelength);
            float omega = 2f * Mathf.PI * waveFreq;
            var spine = _spine;
            var spineZ = _spineY;   // 水平Z方向的蜿蜒位移
            var colAng = _colAng;   // 每列当地切线角(水平面内)
            float dxStep = bodyLen / (Columns - 1);
            for (int j = 0; j < Columns; j++)
            {
                float frac = (float)j / (Columns - 1);          // 0=尾(左) 1=头(右)
                float xLocal = -bodyLen * 0.5f + bodyLen * frac;
                float s = bodyLen * 0.5f - xLocal;              // 距头弧长(0=头)
                // 全身均匀微幅(头尾接近, 无铰链折点)
                float amp = Mathf.Lerp(ampTail, ampHead, 1f - s / bodyLen);
                spineZ[j] = amp * Mathf.Sin(k * s - omega * _t);
                spine[j] = anchor + Right * xLocal + Forward * spineZ[j]; // Z水平蜿蜒
                // 解析斜率 → 切线角 (tiltStrength=0时不使用, 不产生折叠感)
                colAng[j] = Mathf.Atan2(spineZ[jNextSafe(j)] - spineZ[jPrevSafe(j)], 2f * dxStep);
            }

            // 切线角低通平滑 (仅tiltStrength>0时执行)
            if (tiltStrength > 0.001f)
            {
                for (int pass = 0; pass < 3; pass++)
                {
                    float a0 = colAng[0], aLast = colAng[Columns - 1];
                    for (int j = 0; j < Columns; j++)
                    {
                        float prev = j > 0 ? colAng[j - 1] : a0;
                        float next = j < Columns - 1 ? colAng[j + 1] : aLast;
                        _colN[j].x = (prev + colAng[j] * 2f + next) * 0.25f;
                    }
                    for (int j = 0; j < Columns; j++) colAng[j] = _colN[j].x;
                }
            }

            // ── 顶点: 截面沿PlaneUp站立展开 (tiltStrength=0时纯站立, 零折叠) ──
            for (int j = 0; j < Columns; j++)
            {
                int texX = ColTexX(j);
                float eff = colAng[j] * tiltStrength;
                float sinA = Mathf.Sin(eff), cosA = Mathf.Cos(eff);
                var colUp = new Vector3(-sinA, 0.4123f * cosA, 0.9110f * cosA);
                float topW = _topExt[texX] / 100f * ribbonScale;  // 脊柱到轮廓顶(龙背)
                float botW = _botExt[texX] / 100f * ribbonScale;  // 脊柱到轮廓底(腿)
                _verts[j * 2] = spine[j] + colUp * topW;
                _verts[j * 2 + 1] = spine[j] - colUp * botW;
            }
            _mesh.vertices = _verts;
            _mesh.RecalculateBounds(); // 动态顶点需更新bounds(否则视锥剔除用旧值)
        }

        private int jNextSafe(int j) => Mathf.Min(Columns - 1, j + 1);
        private int jPrevSafe(int j) => Mathf.Max(0, j - 1);

        // 列j对应的贴图列x (与UV的u映射一致)
        private int ColTexX(int j) => Mathf.Clamp(Mathf.RoundToInt(_bx0 + (_bx1 - _bx0) * (float)j / (Columns - 1)), _bx0, _bx1);

        /// <summary>运行时换色 (元素龙)</summary>
        public void SetTint(Color c)
        {
            tint = c;
            if (_mat != null) _mat.color = c;
        }

        /// <summary>元素化: 换色 + 沿身体分布属性VFX帧 (元素龙基础视觉=无色龙+属性色+属性粒子)</summary>
        public void SetElement(Element el)
        {
            SetTint(ElementColors.GetTint(el));
            BuildElementVfx(el);
        }

        private GameObject _vfxRoot;

        private void BuildElementVfx(Element el)
        {
            if (_vfxRoot != null) { Destroy(_vfxRoot); _vfxRoot = null; }
            var folder = EvolvedVisuals.GetVfxFolder(el);
            if (folder == null) return;
            var frames = Resources.LoadAll<Sprite>(folder);
            if (frames == null || frames.Length == 0) return;
            System.Array.Sort(frames, (a, b) => string.CompareOrdinal(a.name, b.name));
            _vfxRoot = new GameObject("ElementVfx");
            _vfxRoot.transform.SetParent(transform, false);
            const int count = 5;
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("Vfx" + i);
                go.transform.SetParent(_vfxRoot.transform, false);
                // 沿身体长度分布, 高低错落 (身体起伏极小, 固定分布即可贴合)
                float tx = Mathf.Lerp(-_bodyLenWorld * 0.42f, _bodyLenWorld * 0.42f, (float)i / (count - 1));
                go.transform.localPosition = new Vector3(tx, (i % 2 == 0) ? 0.6f : -0.3f, 0f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = frames[0];
                sr.sortingOrder = sortingOrder + 2;
                float unitW = frames[0].bounds.size.x;
                float sc = unitW > 0.01f ? 1.7f / unitW : 1f;
                go.transform.localScale = new Vector3(sc, sc, 1f);
                var anim = go.AddComponent<VFXFrameAnimator>();
                anim.frames = frames;
                anim.startFrame = i * (frames.Length / count); // 相位错开
                anim.fps = 10f;
                anim.loop = true;
                anim.alpha = 0.8f;
            }
        }

        private void OnDestroy()
        {
            if (_meshGo != null) Destroy(_meshGo);
            if (_mesh != null) Destroy(_mesh);
            if (_mat != null) Destroy(_mat);
        }
    }
}
