using System;
using System.Linq;
using Game.Data;
using UnityEngine;

/// <summary>
/// 玩家：移动/冲刺/体力、视角与后坐力、命中扫描射击、换弹、开镜、
/// 生命与护甲、出血与治疗、背包与搜刮。数值来自 DataCatalog，缺数据时用兜底常量。
/// </summary>
public sealed class PlayerRig : MonoBehaviour
{
    private const float WalkSpeed = 4.3f;
    private const float SprintSpeed = 6.6f;
    private const float Gravity = -22f;
    private const float ReloadSeconds = 1.9f;
    private const float HealSeconds = 1.4f;
    private const float BaseFov = 75f;
    private const float AimFov = 46f;

    private const float StaminaDrain = 24f;
    private const float StaminaRegen = 17f;

    private const float RecoilRecoverDelay = 0.10f;
    private const float RecoilRecoverSpeed = 95f;

    private const float AimAssistDegrees = 3.0f;
    private const float AimAssistStrength = 0.75f;
    private const float AimAssistMaxDistance = 90f;

    private CharacterController _controller;
    private Transform _camera;
    private Camera _cameraComponent;
    private WeaponViewmodel _viewmodel;
    private TouchControls _controls;
    private GameApp _app;
    private AudioSource _audio;

    private float _yaw;
    private float _pitch;
    private float _recoilPitch;
    private float _recoilYaw;
    private float _recoilRecoverAt;
    private float _verticalSpeed;
    private Vector3 _spawnPosition;
    private float _groundY;

    private float _health = 100f;
    private float _maxHealth = 100f;
    private float _armor = 40f;
    private float _maxArmor = 40f;
    private bool _bleeding;
    private float _bleedRate = 1.4f;
    private int _bandages = 3;
    private bool _healing;
    private float _healEndTime;

    private float _stamina = 1f;
    private float _bobTime;
    private bool _sprinting;

    private float _nextFireTime;
    private float _reloadEndTime;
    private bool _reloading;
    private int _aimMode;
    private bool _aimToggled;
    private bool _reloadInitiatedByPlayer;
    private float _nextDryFireTime;
    private float _flashUntil;
    private float _hitMarkerUntil;
    private float _damageFlashUntil;
    private Vector3 _damageFrom;
    private float _nextStepSound;

    private float _damage = 40f;
    private float _rpm = 700f;
    private float _kick = 0.5f;
    private int _magazine = 30;
    private int _magazineSize = 30;
    private int _reserve = 120;
    private int _pellets = 1;
    private bool _weaponLoaded;
    private string _weaponName = "Viper-15";
    private string _weaponId = "wpn_viper";

    private float _sensitivity = 0.13f;

    public event Action<string> Message;

    public PlayerInventory Inventory { get; } = new PlayerInventory();

    public float Health => _health;
    public float MaxHealth => _maxHealth;
    public float Armor => _armor;
    public float MaxArmor => _maxArmor;
    public float Health01 => _maxHealth <= 0f ? 0f : Mathf.Clamp01(_health / _maxHealth);
    public float Armor01 => _maxArmor <= 0f ? 0f : Mathf.Clamp01(_armor / _maxArmor);
    public float Stamina01 => Mathf.Clamp01(_stamina);
    public bool Bleeding => _bleeding;
    public bool IsAlive => _health > 0f;
    public int Bandages => _bandages;
    public bool Healing => _healing;
    public float HealProgress01 => _healing
        ? Mathf.Clamp01(1f - (_healEndTime - Time.time) / HealSeconds)
        : 0f;

    public int Magazine => _magazine;
    public int MagazineSize => _magazineSize;
    public int Reserve => _reserve;
    public bool Reloading => _reloading;
    public float ReloadProgress01 => _reloading
        ? Mathf.Clamp01(1f - (_reloadEndTime - Time.time) / ReloadSeconds)
        : 0f;
    public string WeaponName => _weaponName;

    public bool MuzzleFlashActive => Time.time < _flashUntil;
    public float MuzzleFlash01 => Mathf.Clamp01((_flashUntil - Time.time) / 0.05f);
    public bool HitMarkerActive => Time.time < _hitMarkerUntil;
    public bool DamageFlashActive => Time.time < _damageFlashUntil;
    public float DamageFlash01 => Mathf.Clamp01((_damageFlashUntil - Time.time) / 0.5f);
    public Vector3 LastDamageFrom => _damageFrom;
    public bool Aiming =>
        _controls != null && !_sprinting && (_aimMode == 1 ? _aimToggled : _controls.AimHeld);
    public bool Grounded => _controller != null && _controller.isGrounded;
    public float GroundY => _groundY;
    public Vector3 CameraPosition => _camera != null ? _camera.position : transform.position;

    public void Initialize(TouchControls controls, GameApp app, Vector3 spawn, float yaw, float sensitivity,
        LoadoutKit kit)
    {
        _controls = controls;
        _app = app;
        _sensitivity = Mathf.Clamp(sensitivity, 0.04f, 0.5f);
        _controller = GetComponent<CharacterController>();

        _aimMode = app != null && app.Profile != null ? app.Profile.AimMode : 0;

        var childCamera = GetComponentInChildren<Camera>();
        if (childCamera != null)
        {
            _camera = childCamera.transform;
            _cameraComponent = childCamera;
            _cameraComponent.fieldOfView = BaseFov;
        }

        _yaw = yaw;
        _spawnPosition = spawn;
        _groundY = spawn.y;
        transform.rotation = Quaternion.Euler(0f, _yaw, 0f);

        _audio = gameObject.AddComponent<AudioSource>();
        _audio.playOnAwake = false;
        _audio.spatialBlend = 0f;

        Physics.SyncTransforms();

        if (kit == null) kit = Loadouts.All[0];
        _armor = Mathf.Max(0f, kit.Armor);
        _maxArmor = Mathf.Max(1f, kit.Armor);
        _bandages = Mathf.Max(0, kit.Bandages);
        _reserve = Mathf.Max(0, kit.Reserve);
        EquipWeapon(kit.WeaponId);
        _viewmodel = WeaponViewmodel.Create(_camera, _weaponId);
    }

    private void Update()
    {
        if (_controls == null || !IsAlive) return;

        var dt = Mathf.Min(Time.deltaTime, 0.05f);

        // 开镜：按住模式直接用 AimHeld，切换模式点一下保持
        if (_controls.ConsumeAimPress() && _aimMode == 1)
        {
            _aimToggled = !_aimToggled;
        }

        // 切换模式下想冲刺就自动放镜（否则 Aiming 会一直挡住冲刺）
        if (_aimToggled && _controls.SprintHeld && _controls.Move.sqrMagnitude > 0.16f)
        {
            _aimToggled = false;
        }

        UpdateStatusEffects(dt);
        UpdateLook(dt);
        UpdateMovement(dt);
        UpdateAiming(dt);
        UpdateReload();
        if (_viewmodel != null) _viewmodel.SetReloading(_reloading);
        UpdateHealing();
        UpdateFire();
        UpdateInteract();

        if (_controls.ConsumeReload())
        {
            StartReload(true);
        }

        if (_controls.ConsumeHeal())
        {
            StartHeal();
        }

        if (_controls.ConsumeBackpack())
        {
            if (BackpackUi.IsOpen) BackpackUi.Close();
            else BackpackUi.Show(_app, this, _controls);
        }
    }

    // ---------- 生命 / 状态 ----------

    private void UpdateStatusEffects(float dt)
    {
        if (_bleeding)
        {
            _health -= _bleedRate * dt;
            if (_health <= 0f)
            {
                _health = 0f;
                Die("失血过多");
                return;
            }
        }

        if (!_bleeding && _health < _maxHealth && Time.time - _damageFlashUntil > 6f)
        {
            // 脱战缓慢回血，避免一局后半段只剩残血硬苟
            _health = Mathf.Min(_maxHealth, _health + 0.6f * dt);
        }

        if (_bleeding && Time.time >= _nextStepSound)
        {
            _nextStepSound = Time.time + 2.6f;
            Fx.Play2D(Fx.Hurt, 0.18f);
        }
    }

    public void TakeDamage(float amount, Vector3 from)
    {
        if (!IsAlive) return;

        var incoming = Mathf.Max(0f, amount);

        // 护甲先吃 55%，并消耗耐久
        if (_armor > 0f)
        {
            var absorbed = incoming * 0.55f;
            _armor = Mathf.Max(0f, _armor - absorbed * 0.8f);
            incoming -= absorbed;
        }

        _health -= incoming;
        _damageFrom = from;
        _damageFlashUntil = Time.time + 0.5f;

        Fx.Play2D(Fx.Hurt, 0.55f);

        if (!_bleeding && UnityEngine.Random.value < 0.28f)
        {
            _bleeding = true;
            _bleedRate = UnityEngine.Random.Range(1.0f, 1.9f);
            Message?.Invoke("你出血了，按「医」包扎");
        }

        // 被打断治疗
        if (_healing)
        {
            _healing = false;
            Message?.Invoke("治疗被打断");
        }

        if (_health <= 0f)
        {
            _health = 0f;
            Die("阵亡");
        }
    }

    private void Die(string reason)
    {
        _health = 0f;
        _bleeding = false;
        _healing = false;

        if (_controller != null) _controller.enabled = false;
        if (_controls != null) _controls.SetInputEnabled(false);

        if (_camera != null)
        {
            _camera.localRotation = Quaternion.Euler(18f, 0f, 12f);
            _camera.localPosition = new Vector3(0f, 0.9f, 0f);
        }

        if (_viewmodel != null) _viewmodel.gameObject.SetActive(false);

        Fx.Play2D(Fx.Death, 0.7f);
        Message?.Invoke(reason);
        _app?.OnPlayerDied();
    }

    private void UpdateHealing()
    {
        if (!_healing) return;

        if (Time.time >= _healEndTime)
        {
            _healing = false;
            _bandages--;
            _health = Mathf.Min(_maxHealth, _health + 28f);
            Fx.Play2D(Fx.Heal, 0.6f);

            if (_bleeding)
            {
                _bleeding = false;
                Message?.Invoke("已包扎，出血停止");
            }
            else
            {
                Message?.Invoke("已使用绷带");
            }
        }
    }

    public bool StartHeal()
    {
        if (!IsAlive || _healing) return false;

        if (_bandages <= 0)
        {
            Message?.Invoke("没有绷带了");
            return false;
        }

        if (_health >= _maxHealth && !_bleeding)
        {
            Message?.Invoke("现在不需要治疗");
            return false;
        }

        _healing = true;
        _healEndTime = Time.time + HealSeconds;
        Fx.Play2D(Fx.Search, 0.4f);
        return true;
    }

    // ---------- 视角 / 移动 ----------

    private void UpdateLook(float dt)
    {
        var rawLook = _controls.ConsumeLookDelta();
        if (_viewmodel != null) _viewmodel.AddLook(rawLook);

        var look = rawLook * _sensitivity * (Aiming ? 0.55f : 1f);
        _yaw += look.x;
        _pitch = Mathf.Clamp(_pitch - look.y, -85f, 85f);

        transform.rotation = Quaternion.Euler(0f, _yaw, 0f);

        if (Time.time >= _recoilRecoverAt)
        {
            _recoilPitch = Mathf.MoveTowards(_recoilPitch, 0f, RecoilRecoverSpeed * dt);
            _recoilYaw = Mathf.MoveTowards(_recoilYaw, 0f, RecoilRecoverSpeed * 0.7f * dt);
        }

        if (_camera != null)
        {
            _camera.localRotation = Quaternion.Euler(
                Mathf.Clamp(_pitch - _recoilPitch, -89f, 89f),
                _recoilYaw,
                0f);
        }
    }

    private void UpdateMovement(float dt)
    {
        var input = _controls.Move;
        var magnitude = Mathf.Clamp01(input.magnitude);

        var wantsSprint = _controls.SprintHeld && magnitude > 0.2f && !Aiming;
        _sprinting = wantsSprint && _stamina > 0.02f;

        if (_sprinting)
        {
            _stamina = Mathf.Max(0f, _stamina - StaminaDrain * dt);
            if (_stamina <= 0f) _sprinting = false;
        }
        else
        {
            _stamina = Mathf.Min(1f, _stamina + StaminaRegen * dt * (magnitude < 0.1f ? 1.4f : 0.7f));
        }

        if (_viewmodel != null) _viewmodel.SetMovement(magnitude, _sprinting);

        var speed = _sprinting ? SprintSpeed : WalkSpeed;
        if (Aiming) speed *= 0.55f;

        var direction = transform.right * input.x + transform.forward * input.y;
        if (direction.sqrMagnitude > 1f) direction.Normalize();

        if (_controller.isGrounded && _verticalSpeed < 0f) _verticalSpeed = -2f;
        _verticalSpeed += Gravity * dt;

        _controller.Move((direction * speed + Vector3.up * _verticalSpeed) * dt);

        if (transform.position.y < _groundY - 0.02f)
        {
            SnapToGround();
        }

        if (transform.position.y < -8f)
        {
            Respawn();
        }

        UpdateCameraBob(dt, magnitude);

        // 脚步声
        if (_controller.isGrounded && magnitude > 0.15f && Time.time >= _nextStepSound)
        {
            _nextStepSound = Time.time + (_sprinting ? 0.31f : 0.45f);
            Fx.Play2D(Fx.Step, _sprinting ? 0.3f : 0.18f);
        }
    }

    /// <summary>走路/冲刺时镜头轻微上下起伏，脚步感和步兵重量感全靠这个。</summary>
    private void UpdateCameraBob(float dt, float moveAmount)
    {
        if (_camera == null) return;

        var amount = _sprinting ? 0.05f : 0.022f;
        var active = _controller.isGrounded ? moveAmount : 0f;

        _bobTime += dt * (_sprinting ? 12f : 8.5f) * active;

        var y = Mathf.Abs(Mathf.Sin(_bobTime)) * amount * active;
        var x = Mathf.Sin(_bobTime * 0.5f) * amount * 0.55f * active;

        _camera.localPosition = Vector3.Lerp(_camera.localPosition,
            new Vector3(x, 1.62f + y, 0f), 14f * dt);
    }

    private void SnapToGround()
    {
        _verticalSpeed = 0f;
        var position = transform.position;
        position.y = _groundY;
        if (_controller != null) _controller.enabled = false;
        transform.position = position;
        if (_controller != null) _controller.enabled = true;
        Physics.SyncTransforms();
    }

    private void Respawn()
    {
        _verticalSpeed = 0f;
        if (_controller != null) _controller.enabled = false;
        transform.position = _spawnPosition;
        Physics.SyncTransforms();
        if (_controller != null) _controller.enabled = true;
        Message?.Invoke("掉出地图，已回到出生点");
    }

    private void UpdateAiming(float dt)
    {
        if (_cameraComponent == null) return;

        if (_viewmodel != null) _viewmodel.SetAiming(Aiming);

        var target = Aiming ? AimFov : BaseFov;
        _cameraComponent.fieldOfView = Mathf.Lerp(_cameraComponent.fieldOfView, target, 12f * dt);
    }

    // ---------- 射击 ----------

    private void UpdateReload()
    {
        if (_reloading && Time.time >= _reloadEndTime)
        {
            var needed = _magazineSize - _magazine;
            var loaded = Mathf.Min(needed, _reserve);
            _magazine += loaded;
            _reserve -= loaded;
            _reloading = false;
            _nextFireTime = Time.time;
            Fx.Play2D(Fx.Click, 0.5f);
        }
    }

    public void StartReload(bool manual)
    {
        if (_reloading || !IsAlive) return;
        if (_magazine >= _magazineSize) return;
        if (_reserve <= 0)
        {
            if (manual) Message?.Invoke("没有备用弹药了");
            return;
        }

        _reloading = true;
        _reloadInitiatedByPlayer = manual;
        _reloadEndTime = Time.time + ReloadSeconds;
        Fx.Play2D(Fx.Search, 0.35f);
    }

    private void UpdateFire()
    {
        if (!_controls.FireHeld || _reloading || _healing || Time.time < _nextFireTime) return;

        if (_magazine <= 0)
        {
            if (Time.time >= _nextDryFireTime)
            {
                _nextDryFireTime = Time.time + 0.35f;
                Fx.Play2D(Fx.Dry, 0.45f);
                if (_reserve > 0) StartReload(true);
                else Message?.Invoke("没子弹了");
            }
            return;
        }

        Shoot();
        _magazine--;
        _nextFireTime = Time.time + 60f / Mathf.Max(30f, _rpm);

        if (_magazine <= 0 && _reserve > 0)
        {
            StartReload(false);
        }
    }

    private void Shoot()
    {
        if (_camera == null) return;

        var forward = _camera.forward;
        var origin = _camera.position + forward * 0.5f;

        var baseSpread = Aiming ? 0.18f : 0.45f;
        var movePenalty = _sprinting ? 2.4f : (_controls.Move.magnitude > 0.15f ? 1.1f : 0f);

        var hitAnything = false;
        var hitEnemy = false;
        var endPoint = origin + forward * 120f;

        for (var i = 0; i < _pellets; i++)
        {
            var direction = ApplySpread(forward, baseSpread + movePenalty + (i > 0 ? 2.2f : 0f));
            direction = ApplyAimAssist(origin, direction);

            var pelletEnd = origin + direction * 140f;
            // 球形扫描（半径 6cm）：贴着掩体边缘的敌人也能打中，
            // 纯射线经常会从模型缝隙里穿过去
            if (Physics.SphereCast(origin, 0.06f, direction, out var hit, 200f, ~0, QueryTriggerInteraction.Ignore))
            {
                pelletEnd = hit.point;
                hitAnything = true;

                var enemy = hit.collider.GetComponentInParent<EnemyScav>();
                if (enemy != null && enemy.TakeDamage(_damage))
                {
                    hitEnemy = true;
                }
            }

            if (i == _pellets - 1) endPoint = pelletEnd;
        }

        var barrel = _camera.position + forward * 0.55f + _camera.right * 0.17f - _camera.up * 0.16f;
        Fx.Tracer(barrel, endPoint, new Color(1f, 0.88f, 0.42f, 0.95f), 0.03f);
        Fx.Play2D(Fx.RandomShot, 0.62f);
        if (_viewmodel != null) _viewmodel.Kick(1f);

        // 枪声会把附近的敌人引过来
        _app?.OnShotFired(transform.position);

        _recoilPitch += _kick * (Aiming ? 0.35f : 0.55f);
        _recoilYaw += UnityEngine.Random.Range(-1f, 1f) * _kick * 0.22f;
        _recoilRecoverAt = Time.time + RecoilRecoverDelay;

        _flashUntil = Time.time + 0.05f;

        if (hitEnemy)
        {
            _hitMarkerUntil = Time.time + 0.12f;
            Fx.Play2D(Fx.HitFlesh, 0.4f);
        }
        else if (hitAnything)
        {
            Fx.Play2D(Fx.HitMetal, 0.18f);
        }
    }

    private Vector3 ApplyAimAssist(Vector3 origin, Vector3 direction)
    {
        var candidates = EnemyScav.Active;
        if (candidates.Count == 0) return direction;

        var minDot = Mathf.Cos(AimAssistDegrees * Mathf.Deg2Rad);
        EnemyScav best = null;
        var bestDot = minDot;

        for (var i = 0; i < candidates.Count; i++)
        {
            var candidate = candidates[i];
            if (candidate == null || !candidate.IsAlive) continue;

            var toTarget = candidate.AimPoint - origin;
            var distance = toTarget.magnitude;
            if (distance < 1.5f || distance > AimAssistMaxDistance) continue;

            var dot = Vector3.Dot(direction, toTarget / distance);
            if (dot <= bestDot) continue;

            if (Physics.Linecast(origin, candidate.AimPoint, out var blocker, ~0, QueryTriggerInteraction.Ignore) &&
                blocker.collider.GetComponentInParent<EnemyScav>() == null)
            {
                continue;
            }

            best = candidate;
            bestDot = dot;
        }

        if (best == null) return direction;

        var aim = (best.AimPoint - origin).normalized;
        return Vector3.Slerp(direction, aim, AimAssistStrength).normalized;
    }

    private Vector3 ApplySpread(Vector3 forward, float degrees)
    {
        if (degrees <= 0.001f) return forward;

        var random = UnityEngine.Random.insideUnitCircle * Mathf.Tan(degrees * Mathf.Deg2Rad);
        return (forward + _camera.right * random.x + _camera.up * random.y).normalized;
    }

    // ---------- 搜刮 ----------

    private void UpdateInteract()
    {
        if (!_controls.ConsumeInteract()) return;

        if (_healing)
        {
            Message?.Invoke("治疗中，先别动");
            return;
        }

        var nearest = NearestContainer();
        if (nearest == null)
        {
            Message?.Invoke("附近没有可搜的东西");
            return;
        }

        if (nearest.IsEmpty)
        {
            Message?.Invoke(nearest.Label + " 已经搜过了");
            return;
        }

        if (!nearest.BeginSearch(this))
        {
            Message?.Invoke("靠得太远了");
        }
    }

    public LootContainer NearestContainer()
    {
        LootContainer best = null;
        var bestDistance = LootContainer.ReachDistance;

        for (var i = 0; i < LootContainer.Active.Count; i++)
        {
            var container = LootContainer.Active[i];
            if (container == null || container.IsEmpty) continue;

            var distance = Vector3.Distance(transform.position, container.transform.position);
            if (distance < bestDistance)
            {
                best = container;
                bestDistance = distance;
            }
        }

        return best;
    }

    // ---------- 装备 ----------

    private void EquipWeapon(string weaponId)
    {
        _weaponId = string.IsNullOrEmpty(weaponId) ? "wpn_viper" : weaponId;

        var catalog = GameDataBootstrap.Instance != null ? GameDataBootstrap.Instance.Catalog : null;

        if (catalog == null || catalog.Weapons.Count == 0)
        {
            // 数据没加载完也能玩：用兜底数值
            _damage = 40f;
            _rpm = 700f;
            _magazineSize = 30;
            _magazine = 30;
            if (_reserve <= 0) _reserve = 120;
            return;
        }

        WeaponData weapon = null;
        if (!string.IsNullOrEmpty(weaponId))
        {
            catalog.Weapons.TryGetValue(weaponId, out weapon);
        }

        if (weapon == null)
        {
            weapon = catalog.Weapons.Values
                .Where(x => x.Rpm > 0 && x.MagazineCapacity > 0)
                .OrderByDescending(x => x.Rpm)
                .FirstOrDefault();
        }

        if (weapon == null) return;

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

        _reserve = _reserve > 0 ? _reserve : _magazineSize * 4;

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
    }

    public void SetSensitivity(float value)
    {
        _sensitivity = Mathf.Clamp(value, 0.04f, 0.5f);
    }

    public string WeaponSummary => _weaponLoaded ? _weaponName : "备用武器";
}