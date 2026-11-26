using System;
using System.Linq;
using Game.Data;
using UnityEngine;

/// <summary>
/// M0 第一人称训练角色：CharacterController 移动、相机视角、命中扫描射击、
/// 弹药/换弹，以及靠近箱子时拾取。数值在数据加载完成后从 DataCatalog 读取。
/// </summary>
public sealed class PlayerRig : MonoBehaviour
{
    private const float MoveSpeed = 4.5f;
    private const float Gravity = -22f;
    private const float LookSensitivity = 0.12f;
    private const float InteractRadius = 2.5f;
    private const float ReloadSeconds = 1.6f;

    private CharacterController _controller;
    private Transform _camera;
    private TouchControls _controls;
    private LineRenderer _tracer;
    private AudioSource _audio;
    private AudioClip _shotClip;
    private AudioClip _clickClip;
    private float _flashUntil;
    private float _hitMarkerUntil;
    private float _nextDryFireTime;

    private float _yaw;
    private float _pitch;
    private float _verticalSpeed;
    private Vector3 _spawnPosition;
    private float _groundY;
    private float _nextFireTime;
    private float _reloadEndTime;
    private float _tracerEndTime;
    private float _damage = 25f;
    private float _rpm = 600f;
    private float _kick = 0.5f;
    private int _magazine = 30;
    private int _magazineSize = 30;
    private int _pellets = 1;
    private bool _reloading;
    private bool _weaponLoaded;
    private string _weaponName = "训练武器";

    public event Action<string> Message;

    public string WeaponName => _weaponName;
    public int Magazine => _magazine;
    public int MagazineSize => _magazineSize;
    public bool Reloading => _reloading;
    public bool Grounded => _controller != null && _controller.isGrounded;
    public bool MuzzleFlashActive => Time.time < _flashUntil;
    public bool HitMarkerActive => Time.time < _hitMarkerUntil;
    public float GroundY => _groundY;
    public Vector3 CameraPosition => _camera != null ? _camera.position : Vector3.zero;

    public void Initialize(TouchControls controls)
    {
        _controls = controls;
        _controller = GetComponent<CharacterController>();
        _camera = GetComponentInChildren<Camera>().transform;
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
        _camera.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
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
        // （如果这里一直在触发，说明 CC 根本没和地面碰撞，需要在 HUD 上看 ray 数据）
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
        var origin = _camera.position + _camera.forward * 0.5f;
        var endPoint = origin + _camera.forward * 100f;

        var hitAnyTarget = false;
        for (var i = 0; i < _pellets; i++)
        {
            var direction = ApplySpread(_camera.forward, i > 0 ? 2.5f : 0.4f);
            if (Physics.Raycast(origin, direction, out var hit, 200f))
            {
                endPoint = hit.point;
                var target = hit.collider.GetComponentInParent<TargetDummy>();
                if (target != null)
                {
                    target.TakeDamage(_damage);
                    hitAnyTarget = true;
                }
            }
        }

        _tracer.SetPosition(0, origin);
        _tracer.SetPosition(1, endPoint);
        _tracer.enabled = true;
        _tracerEndTime = Time.time + 0.05f;

        _pitch = Mathf.Clamp(_pitch - _kick, -85f, 85f);

        _flashUntil = Time.time + 0.045f;
        if (hitAnyTarget)
        {
            _hitMarkerUntil = Time.time + 0.09f;
        }

        if (_audio != null && _shotClip != null)
        {
            _audio.pitch = UnityEngine.Random.Range(0.94f, 1.06f);
            _audio.PlayOneShot(_shotClip, 0.85f);
        }
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
        _shotClip = BuildShotClip();
        _clickClip = BuildClickClip();
    }

    /// <summary>
    /// 程序化合成枪声：没有音频资源也能有反馈。
    /// 噪声 + 低频冲击 + 指数衰减，比"完全静音"强一百倍。
    /// </summary>
    private static AudioClip BuildShotClip()
    {
        const int rate = 44100;
        const float duration = 0.18f;
        var count = Mathf.CeilToInt(rate * duration);
        var data = new float[count];
        var rng = new System.Random(20261126);

        for (var i = 0; i < count; i++)
        {
            var t = i / (float)rate;
            var noise = (float)(rng.NextDouble() * 2.0 - 1.0);
            var crack = Mathf.Sin(2f * Mathf.PI * 120f * t) * 0.5f;
            var env = Mathf.Exp(-t * 34f);
            data[i] = Mathf.Clamp((noise * 0.75f + crack) * env, -1f, 1f);
        }

        var clip = AudioClip.Create("m0_shot", count, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private static AudioClip BuildClickClip()
    {
        const int rate = 44100;
        const float duration = 0.05f;
        var count = Mathf.CeilToInt(rate * duration);
        var data = new float[count];
        var rng = new System.Random(7);

        for (var i = 0; i < count; i++)
        {
            var t = i / (float)rate;
            var env = Mathf.Exp(-t * 120f);
            data[i] = (float)(rng.NextDouble() * 2.0 - 1.0) * env * 0.6f;
        }

        var clip = AudioClip.Create("m0_click", count, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private void CreateTracer()
    {
        var tracerGo = new GameObject("Tracer");
        tracerGo.transform.SetParent(transform, false);

        _tracer = tracerGo.AddComponent<LineRenderer>();
        _tracer.positionCount = 2;
        _tracer.startWidth = 0.025f;
        _tracer.endWidth = 0.025f;
        _tracer.useWorldSpace = true;
        _tracer.material = new Material(Shader.Find("Sprites/Default"));
        _tracer.startColor = new Color(1f, 0.85f, 0.3f, 0.9f);
        _tracer.endColor = new Color(1f, 0.85f, 0.3f, 0.1f);
        _tracer.enabled = false;
    }

    private void UpdateTracer()
    {
        if (_tracer.enabled && Time.time >= _tracerEndTime)
        {
            _tracer.enabled = false;
        }
    }
}
