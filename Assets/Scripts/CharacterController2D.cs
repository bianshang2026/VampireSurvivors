using UnityEngine;

namespace BiopunkRV
{
    /// <summary>
    /// Character controller with walk-cycle frame animation.
    /// Single direction, flips horizontally for left/right.
    /// </summary>
    public class CharacterController2D : MonoBehaviour
    {
        [Header("Movement")]
        public float moveSpeed = 22f;
        public float minX = -90f;
        public float maxX = 90f;
        public float minZ = -90f;
        public float maxZ = 90f;

        [Header("Walk Frames")]
        public Texture2D[] walkFrames;
        public Texture2D idleFrame;
        public float walkFPS = 30f;  // Base walk animation FPS
        public float bobSpeed = 8f;
        public float flipThreshold = 0.01f;

        [Header("Camera Follow")]
        public Transform cameraTarget;
        public Vector3 cameraOffset = new Vector3(0f, 46.5f, -3.42f);
        public float cameraSmoothSpeed = 5f;
        public float zoomSpeed = 3f;
        public float minZoom = 0f;
        public float maxZoom = 15f;

        private Material _mat;
        private bool _isMoving;
        private Camera _cam;
        private float _collisionRadius = 0.6f;
        private float _camZoom = 0f;
        private int _currentFrame;
        private float _timer;

        // 减速状态 (冰狐减速光环等)
        private float _slowFactor = 1f;
        private float _slowTimer;
        // 击退状态 (石巨人践踏等)
        private Vector3 _knockbackVelocity;
        private Transform _shadow;
        // Update开头捕获的位置, 实心碰撞据此判断玩家本帧是否主动移动
        private Vector3 _updateStartPos;

        private void Awake()
        {
            var mr = GetComponent<MeshRenderer>();
            if (mr != null && mr.material != null) _mat = mr.material;
            _cam = Camera.main;
            _shadow = GameObject.Find("PlayerShadow")?.transform;
            if (_cam == null)
            {
                var camGo = GameObject.Find("Main Camera");
                if (camGo != null) _cam = camGo.GetComponent<Camera>();
            }
        }

        private void Start()
        {
            if (_shadow == null)
                _shadow = GameObject.Find("PlayerShadow")?.transform;
            if (_cam != null && cameraTarget == null) cameraTarget = transform;
            if (_mat != null && idleFrame != null) _mat.mainTexture = idleFrame;
        }

        private void LateUpdate()
        {
            if (_cam == null || cameraTarget == null) return;

            // Mouse scroll: zoom in/out (distance only, angle locked)
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (scroll != 0f)
            {
                _camZoom = Mathf.Clamp(_camZoom - scroll * zoomSpeed * 10f, minZoom, maxZoom);
            }

            // Lock angle to match Scene View camera
            Vector3 desiredPos = cameraTarget.position + cameraOffset + new Vector3(0, _camZoom, 0);
            _cam.transform.position = Vector3.Lerp(_cam.transform.position, desiredPos, cameraSmoothSpeed * Time.deltaTime);
            _cam.transform.rotation = Quaternion.Euler(74.81f, 0f, 0f);

            // Shadow follows player XZ, Y stays at shadow object's own value
            if (_shadow != null)
            {
                Vector3 shadowPos = _shadow.position;
                shadowPos.x = transform.position.x;
                shadowPos.z = transform.position.z - 1f;
                _shadow.position = shadowPos;
            }
        }

        private void Update()
        {
            _updateStartPos = transform.position;

            // 击退位移 (独立于玩家输入, 带碰撞检测)
            if (_knockbackVelocity.sqrMagnitude > 0.01f)
            {
                Vector3 kbPos = transform.position + _knockbackVelocity * Time.deltaTime;
                kbPos.x = Mathf.Clamp(kbPos.x, minX, maxX);
                kbPos.z = Mathf.Clamp(kbPos.z, minZ, maxZ);
                Vector3 tryX = new Vector3(kbPos.x, transform.position.y, transform.position.z);
                if (!CheckCollision(tryX)) transform.position = tryX;
                Vector3 tryZ = new Vector3(transform.position.x, transform.position.y, kbPos.z);
                if (!CheckCollision(tryZ)) transform.position = tryZ;
                _knockbackVelocity = Vector3.Lerp(_knockbackVelocity, Vector3.zero, 8f * Time.deltaTime);
            }

            // 减速计时器
            if (_slowTimer > 0f)
            {
                _slowTimer -= Time.deltaTime;
                if (_slowTimer <= 0f) _slowFactor = 1f;
            }

            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");

            Vector2 input = new Vector2(h, v);
            _isMoving = input.sqrMagnitude > 0.01f;

            if (_isMoving)
            {
                // Move with collision check
                Vector3 dir = new Vector3(h, 0f, v).normalized;
                Vector3 newPos = transform.position + dir * moveSpeed * _slowFactor * Time.deltaTime;
                newPos.x = Mathf.Clamp(newPos.x, minX, maxX);
                newPos.z = Mathf.Clamp(newPos.z, minZ, maxZ);

                Vector3 tryX = new Vector3(newPos.x, transform.position.y, transform.position.z);
                if (!CheckCollision(tryX)) transform.position = tryX;
                Vector3 tryZ = new Vector3(transform.position.x, transform.position.y, newPos.z);
                if (!CheckCollision(tryZ)) transform.position = tryZ;

                ApplyFlip();

                // Walk frame animation - FPS scales with move speed
                _timer += Time.deltaTime;
                float effectiveFPS = walkFPS * (moveSpeed / 16f);
                effectiveFPS = Mathf.Min(effectiveFPS, 60f);
                if (effectiveFPS < 2f) effectiveFPS = 2f;
                float frameDur = 1f / effectiveFPS;
                if (_timer >= frameDur)
                {
                    _timer -= frameDur;
                    if (walkFrames != null && walkFrames.Length > 0)
                    {
                        _currentFrame = (_currentFrame + 1) % walkFrames.Length;
                        if (_mat != null) _mat.mainTexture = walkFrames[_currentFrame];
                    }
                }
            }
            else
            {
                // Idle - show idle frame
                if (_mat != null && idleFrame != null && _mat.mainTexture != idleFrame)
                    _mat.mainTexture = idleFrame;
                _currentFrame = 0;
                _timer = 0f;
            }

            // 实心碰撞解决: 玩家被怪/装饰物推出表面 (防止穿入+卡死)
            ResolveSolidCollisions();
        }

        private void ApplyFlip()
        {
            Vector3 scale = transform.localScale;
            float h = Input.GetAxisRaw("Horizontal");
            
            if (h > flipThreshold)
                scale.x = -Mathf.Abs(scale.x);
            else if (h < -flipThreshold)
                scale.x = Mathf.Abs(scale.x);
            
            transform.localScale = scale;
        }

        private bool CheckCollision(Vector3 position)
        {
            Collider[] hits = Physics.OverlapSphere(position, _collisionRadius);
            foreach (var hit in hits)
            {
                if (hit == null || hit.transform == transform) continue;
                // 场景保存的树木 (怪物/装饰物走ResolveSolidCollisions实心推出, 此处只挡树)
                if (hit.name.StartsWith("Deco_SpiritTree") || hit.name.StartsWith("Deco_RareTree") ||
                    hit.name.StartsWith("Deco_GoldenTree") || hit.name.StartsWith("Deco_TreasureTree") ||
                    hit.name.StartsWith("Deco_TreeFill") || hit.name.StartsWith("Deco_RockLarge"))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 实心碰撞: 怪物和装饰物(DecoObstacle)对玩家是不可穿入的实心圆。
        /// 玩家不能穿过怪, 但怪不能推着玩家走 — 只在玩家试图穿入时挡住, 静止时不被推。
        /// 防乱挤: 每帧只解算穿入最深的一个碰撞体(多个怪围住时不再连续推挤抖动)。
        /// </summary>
        private void ResolveSolidCollisions()
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, 12f);

            // 找穿入最深的碰撞体 (每帧只解算这一个)
            Collider deepest = null;
            float deepestPen = 0f;
            float deepestMinDist = 0f;
            bool deepestIsEnemy = false;
            foreach (var hit in hits)
            {
                if (hit == null || hit.transform == transform) continue;

                bool isEnemy = hit.GetComponentInParent<EnemyController>() != null;
                bool isDeco = hit.GetComponentInParent<DecoObstacle>() != null;
                if (!isEnemy && !isDeco) continue;

                float solidR = hit.bounds.extents.x * (isEnemy ? 0.85f : 1f);
                Vector3 offset = transform.position - hit.transform.position;
                offset.y = 0f;
                float minDist = solidR + 0.3f;
                float pen = minDist - offset.magnitude;
                if (pen > deepestPen)
                {
                    deepestPen = pen;
                    deepest = hit;
                    deepestMinDist = minDist;
                    deepestIsEnemy = isEnemy;
                }
            }
            if (deepest == null) return;

            Vector3 off = transform.position - deepest.transform.position;
            off.y = 0f;
            float dist = off.magnitude;
            Vector3 pushDir = dist > 0.01f ? off / dist : new Vector3(0f, 0f, -1f);

            if (deepestIsEnemy)
            {
                // 玩家本帧主动移向怪 → 挡住(钉在表面, 切向可滑动);
                // 否则(怪自己走过来) → 把怪推回表面, 玩家原地不动不被推。
                Vector3 moveXZ = transform.position - _updateStartPos;
                moveXZ.y = 0f;
                bool playerMovingIn = moveXZ.sqrMagnitude > 0.000001f && Vector3.Dot(moveXZ.normalized, -pushDir) > 0.1f;
                if (playerMovingIn)
                {
                    Vector3 surface = deepest.transform.position + pushDir * deepestMinDist;
                    surface.y = transform.position.y;
                    transform.position = surface;
                }
                else
                {
                    var ec = deepest.GetComponentInParent<EnemyController>();
                    Vector3 enemySurface = transform.position - pushDir * deepestMinDist;
                    if (ec != null)
                    {
                        enemySurface.y = ec.transform.position.y;
                        ec.transform.position = enemySurface;
                    }
                    else
                    {
                        enemySurface.y = deepest.transform.position.y;
                        deepest.transform.position = enemySurface;
                    }
                }
            }
            else
            {
                // 装饰物(静止不动的障碍): 直接推到表面(可被推, 装饰不会动)
                Vector3 fixedPos = deepest.transform.position + pushDir * deepestMinDist;
                fixedPos.y = transform.position.y;
                transform.position = fixedPos;
            }
        }

        /// <summary>
        /// 应用减速效果 (冰狐减速光环等调用)
        /// </summary>
        /// <param name="factor">减速倍率 0~1, 0.5=移速减半</param>
        /// <param name="duration">持续时间(秒)</param>
        public void ApplySlow(float factor, float duration)
        {
            // 取更强的减速效果
            if (factor < _slowFactor)
            {
                _slowFactor = factor;
                _slowTimer = duration;
            }
            else if (_slowTimer < duration)
            {
                _slowTimer = duration;
            }
        }

        /// <summary>对玩家施加击退 (石巨人践踏等), 方向取XZ平面, 力度越大推得越远</summary>
        public void ApplyKnockback(Vector3 direction, float force)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.001f && force > 0f)
                _knockbackVelocity = direction.normalized * force;
        }
    }
}
