using System;
using System.Linq;
using Game.Data;
using UnityEngine;

/// <summary>
/// M0 第一人称训练角色：CharacterController 移动、相机视角、命中扫描射击、
/// 弹药/换弹、后坐力回正、轻度辅助瞄准，以及靠近箱子时拾取。
/// 数值在数据加载完成后从 DataCatalog 读取。
/// </summary>
public sealed class PlayerRig : MonoBehaviour
{
    private const float MoveSpeed = 4.6f;
    private const float Gravity = -22f;
    private const float LookSensitivity = 0.13f;
    private const float InteractRadius = 2.6f;
    private const float ReloadSeconds = 1.6f;

    // 后坐力：以前是直接改 _pitch，连发 30 发准星就永远朝天、必须手动压枪。
    // 现在后坐力单独记在 _recoilPitch 上，停火后自动回正。
    private const float RecoilRecoverDelay = 0.10f;
    private const float RecoilRecoverSpeed = 95f;

    // 辅助瞄准：只在开火瞬间把弹道往目标吸一点，不接管视角。
    // 手机上没有鼠标，纯裸弹道命中率会低到劝退，这是手感的基础项。
    private const float AimAssistDegrees = 2.5f;
    private const float AimAssistStrength = 0.7f;
    private const float AimAssistMaxDistance = 90f;

    private CharacterController _controller;
    private Transform _camera;
    private TouchControls _controls;
    private LineRenderer _tracer;
    private AudioSource _audio;
    private AudioClip[] _shotClips;
    private AudioClip _clickClip;
    private AudioClip _hitClip;
    private float _flashUntil;
    private float _hitMarkerUntil;
    private float _nextDryFireTime;

    private float _yaw;
    private float _pitch;
    private float _recoilPitch;
    private float _recoilYaw;
    private float _recoilRecoverAt;
    private float _verticalSpeed;
    private Vector3 _spawnPosition;
    private float _groundY;
    private float _nextFireTime;
    private float _reloadEndTime;
    private float _tracerEndTime;
    private Vector3 _tracerFrom;
    private Vector3 _tracerTo;
    private float _damage = 25f;
    private float _rpm = 600f;
    private float _kick = 0.5f;
    private int _magazine = 30;
    private int _magazineSize = 30;
    private int _pellets = 1;
    private bool _reloading;
    private bool _weaponLoaded;
    private string _weaponName = "训练武器";

    private static Material _tracerMaterial;

    public event Action<string> Message;

    public string WeaponName => _weaponName;
    public int Magazine => _magazine;
    public int MagazineSize => _magazineSize;
    public bool Reloading => _reloading;
    public bool Grounded => _controller != null && _controller.isGrounded;
    public bool MuzzleFlashActive => Time.time < _flashUntil;
    public float MuzzleFlash01 => Mathf.Clamp01((_flashUntil - Time.time) / 0.05f);
    public bool HitMarkerActive => Time.time < _hitMarkerUntil;
    public float GroundY => _groundY;
    public Vector3 CameraPosition => _camera != null ? _camera.position : Vector3.zero;

    // 曳光弹：LineRenderer 材质拿不到时（内置 shader 被裁），退回 HUD 画 2D 线。
    public bool TracerVisible => Time.time < _tracerEndTime && _tracerFrom != Vector3.zero;
    public bool HudTracer => _tracer == null;
    public Vector3 TracerFrom => _tracerFrom;
    public Vector3 TracerTo => _tracerTo;

    public void Initialize(TouchControls controls)
    {
        _controls = controls;
        _controller = GetComponent<CharacterController>();

        var childCamera = GetComponentInChildren<Camera>();
        if (childCamera != null)
        {
            _camera = childCamera.transform;
        }

        _yaw = transform.eulerAngles.y;
        _spawnPosition = transform.position;
        _groundY = _spawnPosition.y;
        Physics.SyncTransforms();
        _magazine = _magazineSize;
        CreateTracer();
        SetupAudio();
        TryLoadWeapon();
    }

    private void Update()
    {
        if (_controls == null)
        {
            return;
        }

        UpdateRecoilRecovery();
        UpdateLook();
        UpdateMovement();
        UpdateReload();
        TryLoadWeapon();
        UpdateFire();
        UpdateTracer();

        if (_controls.ConsumeInteract())
        {
            TryInteract();
        }
    }

    private void UpdateLook()
    {
        var look = _controls.ConsumeLookDelta() * LookSensitivity;
        _yaw += look.x;
        _pitch = Mathf.Clamp(_pitch - look.y, -85f, 85f);

        transform.rotation = Quaternion.Euler(0f, _yaw, 0f);

        if (_camera != null)
        {
            // 后坐力叠加在视角之上，而不是改写 _pitch，这样松手就能自己回正。
            _camera.localRotation = Quaternion.Euler(
                Mathf.Clamp(_pitch - _recoilPitch, -89f, 89f),
                _recoilYaw,
                0f);
        }
    }

    private void UpdateRecoilRecovery()
    {
        if (Time.time < _recoilRecoverAt)
        {
            return;
        }

        var dt = Mathf.Min(Time.deltaTime, 0.05f);
        _recoilPitch = Mathf.MoveTowards(_recoilPitch, 0f, RecoilRecoverSpeed * dt);
        _recoilYaw = Mathf.MoveTowards(_recoilYaw, 0f, RecoilRecoverSpeed * 0.7f * dt);
    }

    private void UpdateMovement()
    {
        // 首帧 / 卡顿帧的 dt 可能到 0.3s，乘上重力就是好几米位移，
        // CharacterController 的扫掠有可能直接穿过薄地面。钳到 50ms。
        var dt = Mathf.Min(Time.deltaTime, 0.05f);

        var input = _controls.Move;
        var direction = transform.right * input.x + transform.forward * input.y;
        if (direction.sqrMagnitude > 1f)
        {
            direction.Normalize();
        }

        if (_controller.isGrounded && _verticalSpeed < 0f)
        {
            _verticalSpeed = -2f;
        }

        _verticalSpeed += Gravity * dt;
        var velocity = direction * MoveSpeed + Vector3.up * _verticalSpeed;
        _controller.Move(velocity * dt);

        // 硬约束：白盒地面是平的，无论物理为何没挡住，都不允许沉到地面以下。
        if (transform.position.y < _groundY - 0.02f)
        {
            SnapToGround();
        }

        // 兜底：真的掉出世界也别无限下落，拉回出生点并提示
        if (transform.position.y < -5f)
        {
            Respawn();
        }
    }

    private void SnapToGround()
    {
        _verticalSpeed = 0f;
        var pos = transform.position;
        pos.y = _groundY;
        if (_controller != null) _controller.enabled = false;
        transform.position = pos;
        if (_controller != null) _controller.enabled = true;
        Physics.SyncTransforms();
    }

    private void Respawn()
    {
        _verticalSpeed = 0f;
        if (_controller != null)
        {
            _controller.enabled = false;
        }

        transform.position = _spawnPosition;
        Physics.SyncTransforms();

        if (_controller != null)
        {
            _controller.enabled = true;
        }

        Message?.Invoke("掉出世界，已重置位置");
    }

    private void UpdateReload()
    {
        if (_reloading && Time.time >= _reloadEndTime)
        {
            _reloading = false;
            _magazine = _magazineSize;
            _nextFireTime = Time.time; // 换完立刻能打，不要多等一个射击间隔
        }
    }

    private void UpdateFire()
    {
        if (!_controls.FireHeld || _reloading || Time.time < _nextFireTime)
        {
            return;
        }

        if (_magazine <= 0)
        {
            // 空仓要有声音，否则玩家不知道是没子弹还是没点中
            if (Time.time >= _nextDryFireTime)
            {
                _nextDryFireTime = Time.time + 0.35f;
                if (_audio != null && _clickClip != null) _audio.PlayOneShot(_clickClip, 0.5f);
            }
            return;
        }

        Shoot();
        _magazine--;
        _nextFireTime = Time.time + 60f / Mathf.Max(30f, _rpm);

        if (_magazine <= 0)
        {
            StartReload();
        }
    }

    private void Shoot()
    {
        if (_camera == null)
        {
            return;
        }

        var forward = _camera.forward;
        var origin = _camera.position + forward * 0.5f;

        var hitAnyTarget = false;
        var endPoint = origin + forward * 100f;

        for (var i = 0; i < _pellets; i++)
        {
            var direction = ApplySpread(forward, i > 0 ? 2.5f : 0.4f);
            direction = ApplyAimAssist(origin, direction);

            var pelletEnd = origin + direction * 120f;
            if (Physics.Raycast(origin, direction, out var hit, 200f, ~0, QueryTriggerInteraction.Ignore))
            {
                pelletEnd = hit.point;
                var target = hit.collider.GetComponentInParent<TargetDummy>();
                if (target != null && target.TakeDamage(_damage))
                {
                    hitAnyTarget = true;
                }
            }

            if (i == _pellets - 1)
            {
                endPoint = pelletEnd;
            }
        }

        // 曳光弹从"枪管"射出而不是从眼睛射出，视觉上才像在开枪。
        var barrel = _camera.position + forward * 0.55f + _camera.right * 0.17f - _camera.up * 0.16f;
        _tracerFrom = barrel;
        _tracerTo = endPoint;
        _tracerEndTime = Time.time + 0.06f;

        if (_tracer != null)
        {
            _tracer.SetPosition(0, barrel);
            _tracer.SetPosition(1, endPoint);
            _tracer.enabled = true;
        }

        // 后坐力：竖直为主 + 一点随机水平，停火后自动回正
        _recoilPitch += _kick * 0.55f;
        _recoilYaw += UnityEngine.Random.Range(-1f, 1f) * _kick * 0.22f;
        _recoilRecoverAt = Time.time + RecoilRecoverDelay;

        _flashUntil = Time.time + 0.05f;

        if (hitAnyTarget)
        {
            _hitMarkerUntil = Time.time + 0.12f;
            if (_audio != null && _hitClip != null) _audio.PlayOneShot(_hitClip, 0.38f);
        }

        if (_audio != null && _shotClips != null && _shotClips.Length > 0)
        {
            _audio.PlayOneShot(_shotClips[UnityEngine.Random.Range(0, _shotClips.Length)], 0.6f);
        }
    }

    /// <summary>
    /// 把弹道往准星附近的目标吸一点点。只做方向微调，不接管玩家视角；
    /// 中间有墙挡着就不吸（否则会往墙里打）。
    /// </summary>
    private Vector3 ApplyAimAssist(Vector3 origin, Vector3 direction)
    {
        var candidates = TargetDummy.Active;
        if (candidates.Count == 0)
        {
            return direction;
        }

        var minDot = Mathf.Cos(AimAssistDegrees * Mathf.Deg2Rad);
        TargetDummy best = null;
        var bestDot = minDot;

        for (var i = 0; i < candidates.Count; i++)
        {
            var candidate = candidates[i];
            if (candidate == null || !candidate.IsAlive)
            {
                continue;
            }

            var toTarget = candidate.AimPoint - origin;
            var distance = toTarget.magnitude;
            if (distance < 1.5f || distance > AimAssistMaxDistance)
            {
                continue;
            }

            var dot = Vector3.Dot(direction, toTarget / distance);
            if (dot <= bestDot)
            {
                continue;
            }

            if (Physics.Linecast(origin, candidate.AimPoint, out var blocker, ~0, QueryTriggerInteraction.Ignore) &&
                blocker.collider.GetComponentInParent<TargetDummy>() == null)
            {
                continue; // 被掩体挡住，不吸
            }

            best = candidate;
            bestDot = dot;
        }

        if (best == null)
        {
            return direction;
        }

        var aim = (best.AimPoint - origin).normalized;
        return Vector3.Slerp(direction, aim, AimAssistStrength).normalized;
    }

    private Vector3 ApplySpread(Vector3 forward, float degrees)
    {
        var random = UnityEngine.Random.insideUnitCircle * Mathf.Tan(degrees * Mathf.Deg2Rad);
        return (forward + _camera.right * random.x + _camera.up * random.y).normalized;
    }

    private void StartReload()
    {
        if (_reloading)
        {
            return;
        }

        _reloading = true;
        _reloadEndTime = Time.time + ReloadSeconds;
        Message?.Invoke("换弹中…");
    }

    private void TryInteract()
    {
        var origin = transform.position + Vector3.up * 1f;
        var colliders = Physics.OverlapSphere(origin, InteractRadius);
        LootCrate nearest = null;
        var nearestDistance = float.MaxValue;

        foreach (var collider in colliders)
        {
            var crate = collider.GetComponentInParent<LootCrate>();
            if (crate == null)
            {
                continue;
            }

            var distance = (crate.transform.position - origin).sqrMagnitude;
            if (distance < nearestDistance)
            {
                nearest = crate;
                nearestDistance = distance;
            }
        }

        if (nearest == null)
        {
            Message?.Invoke("附近没有可拾取的箱子");
            return;
        }

        Message?.Invoke(nearest.Interact());
    }

    private void TryLoadWeapon()
    {
        if (_weaponLoaded)
        {
            return;
        }

        var catalog = GameDataBootstrap.Instance != null ? GameDataBootstrap.Instance.Catalog : null;
        if (catalog == null || catalog.Weapons.Count == 0)
        {
            return;
        }

        var weapon = catalog.Weapons.Values
            .Where(x => x.Rpm > 0 && x.MagazineCapacity > 0)
            .OrderByDescending(x => x.Rpm)
            .FirstOrDefault();

        if (weapon == null)
        {
            return;
        }

        _rpm = weapon.Rpm;
        _magazineSize = weapon.MagazineCapacity;
        _magazine = _magazineSize;

        var ammoId = weapon.AllowedAmmo != null ? weapon.AllowedAmmo.FirstOrDefault() : null;
        if (!string.IsNullOrEmpty(ammoId) && catalog.Ammo.TryGetValue(ammoId, out var ammo))
        {
            _damage = ammo.Damage * weapon.DamageMultiplier;
            _pellets = Mathf.Max(1, ammo.Pellets);
        }
        else
        {
            _damage = weapon.BaseDamage * weapon.DamageMultiplier;
            _pellets = 1;
        }

        if (weapon.RecoilCurve != null && weapon.RecoilCurve.Vertical != null &&
            weapon.RecoilCurve.Vertical.Count > 0)
        {
            _kick = Mathf.Clamp(weapon.RecoilCurve.Vertical[0] * 0.4f, 0.1f, 2f);
        }

        if (!string.IsNullOrEmpty(weapon.ItemId) &&
            catalog.Items.TryGetValue(weapon.ItemId, out var item))
        {
            _weaponName = item.DisplayName;
        }

        _weaponLoaded = true;
        Message?.Invoke("装备 " + _weaponName + " · " + _magazineSize + " 发");
    }

    private void SetupAudio()
    {
        _audio = gameObject.AddComponent<AudioSource>();
        _audio.playOnAwake = false;
        _audio.spatialBlend = 0f;
        _audio.volume = 1f;

        // 3 个变体，避免连发时听起来像复读机
        _shotClips = new[]
        {
            BuildShotClip(11, 92f),
            BuildShotClip(23, 84f),
            BuildShotClip(37, 101f)
        };
        _clickClip = BuildClickClip();
        _hitClip = BuildHitClip();
    }

    /// <summary>
    /// 程序化枪声：瞬态爆点 + 中频主体 + 低频冲击 + 房间尾巴。
    /// </summary>
    private static AudioClip BuildShotClip(int seed, float thumpHz)
    {
        const int rate = 44100;
        const float duration = 0.32f;
        var count = Mathf.CeilToInt(rate * duration);
        var data = new float[count];
        var rng = new System.Random(seed);

        var lp1 = 0f; // 中频（枪口主体）
        var lp2 = 0f; // 更低频（空气/墙壁反射）

        for (var i = 0; i < count; i++)
        {
            var t = i / (float)rate;
            var noise = (float)(rng.NextDouble() * 2.0 - 1.0);

            lp1 += (noise - lp1) * 0.42f;
            lp2 += (lp1 - lp2) * 0.06f;

            // 1) 击发瞬态：前 5ms 的高频爆点，决定"脆"
            var transient = t < 0.005f ? noise * (1f - t / 0.005f) * 0.9f : 0f;

            // 2) 主体：中频噪声快速衰减，决定"实"
            var body = lp1 * Mathf.Exp(-t * 60f) * 1.5f;

            // 3) 低频冲击：决定"闷"和力度
            var thump = Mathf.Sin(2f * Mathf.PI * thumpHz * t) * Mathf.Exp(-t * 24f) * 0.85f;

            // 4) 尾巴：极轻的房间回响
            var tail = lp2 * Mathf.Exp(-t * 5.5f) * 0.45f;

            data[i] = Mathf.Clamp(transient + body + thump + tail, -1f, 1f);
        }

        var clip = AudioClip.Create("m0_shot_" + seed, count, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private static AudioClip BuildClickClip()
    {
        const int rate = 44100;
        const float duration = 0.06f;
        var count = Mathf.CeilToInt(rate * duration);
        var data = new float[count];
        var rng = new System.Random(7);
        var lp = 0f;

        for (var i = 0; i < count; i++)
        {
            var t = i / (float)rate;
            var noise = (float)(rng.NextDouble() * 2.0 - 1.0);
            lp += (noise - lp) * 0.6f;
            // 金属撞击：高频噪声 + 一个短促的 2.4kHz 谐振
            var ping = Mathf.Sin(2f * Mathf.PI * 2400f * t) * Mathf.Exp(-t * 90f) * 0.25f;
            data[i] = Mathf.Clamp((lp * 0.7f + ping) * Mathf.Exp(-t * 70f), -1f, 1f);
        }

        var clip = AudioClip.Create("m0_click", count, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>命中反馈音：短促金属"叮"。手机上没这个音，打中了也不敢确定。</summary>
    private static AudioClip BuildHitClip()
    {
        const int rate = 44100;
        const float duration = 0.11f;
        var count = Mathf.CeilToInt(rate * duration);
        var data = new float[count];
        var rng = new System.Random(101);

        for (var i = 0; i < count; i++)
        {
            var t = i / (float)rate;
            var noise = (float)(rng.NextDouble() * 2.0 - 1.0);

            var a = Mathf.Sin(2f * Mathf.PI * 1480f * t) * Mathf.Exp(-t * 38f);
            var b = Mathf.Sin(2f * Mathf.PI * 2260f * t) * Mathf.Exp(-t * 55f) * 0.55f;
            var click = noise * Mathf.Exp(-t * 220f) * 0.35f;

            data[i] = Mathf.Clamp((a + b + click) * 0.5f, -1f, 1f);
        }

        var clip = AudioClip.Create("m0_hit", count, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>
    /// 曳光弹用 LineRenderer，材质 shader 按常见顺序回退。
    /// 之前没有判空：一旦 Shader.Find 返回 null，Shoot() 里就会每发抛异常，开火直接卡死。
    /// </summary>
    private void CreateTracer()
    {
        var material = TracerMaterial();
        if (material == null)
        {
            // 拿不到 shader 就不建 LineRenderer，HUD 会用 2D 线兜底
            _tracer = null;
            return;
        }

        var tracerGo = new GameObject("Tracer");
        tracerGo.transform.SetParent(transform, false);

        _tracer = tracerGo.AddComponent<LineRenderer>();
        _tracer.positionCount = 2;
        _tracer.startWidth = 0.03f;
        _tracer.endWidth = 0.012f;
        _tracer.useWorldSpace = true;
        _tracer.numCapVertices = 0;
        _tracer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _tracer.receiveShadows = false;
        _tracer.material = material;
        _tracer.startColor = new Color(1f, 0.88f, 0.42f, 1f);
        _tracer.endColor = new Color(1f, 0.72f, 0.25f, 0.15f);
        _tracer.enabled = false;
    }

    private static Material TracerMaterial()
    {
        if (_tracerMaterial != null)
        {
            return _tracerMaterial;
        }

        var candidates = new[]
        {
            "Sprites/Default",
            "Unlit/Color",
            "Legacy Shaders/Particles/Additive",
            "Hidden/Internal-Colored",
            "Standard"
        };

        for (var i = 0; i < candidates.Length; i++)
        {
            var shader = Shader.Find(candidates[i]);
            if (shader == null)
            {
                continue;
            }

            var material = new Material(shader) { color = Color.white };
            _tracerMaterial = material;
            return material;
        }

        return null;
    }

    private void UpdateTracer()
    {
        if (_tracer != null && _tracer.enabled && Time.time >= _tracerEndTime)
        {
            _tracer.enabled = false;
        }
    }
}