using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Scav 敌人：巡逻 → 听到枪声去查看 → 看见玩家进入交战 → 丢失视野后搜索。
/// 没有 NavMesh，用"直线朝目标走 + 卡住换向"这套简化转向，白盒地图够用。
/// </summary>
public sealed class EnemyScav : MonoBehaviour
{
    private enum AiState
    {
        Patrol,
        Investigate,
        Engage,
        Dead
    }

    public static readonly List<EnemyScav> Active = new List<EnemyScav>();

    private const float ViewDistance = 45f;
    private const float ViewAngle = 115f;
    private const float HearDistance = 55f;
    private const float PatrolSpeed = 2.1f;
    private const float ChaseSpeed = 3.5f;

    private GameApp _app;
    private CharacterController _controller;
    private Renderer[] _renderers;

    private AiState _state = AiState.Patrol;
    private Vector3 _home;
    private Vector3 _moveTarget;
    private Vector3 _lastKnown;
    private Vector3 _stuckPosition;
    private Vector3 _detourDirection;

    private float _health = 100f;
    private float _damage = 11f;
    private float _nextRepathTime;
    private float _alertUntil;
    private float _lostSightAt = -1f;
    private float _nextBurstTime;
    private float _nextBurstShot;
    private int _burstRemaining;
    private float _stuckCheckTime;
    private float _detourUntil;
    private float _nextStepSound;
    private Transform _body;
    private SoldierRig _rig;
    private Vector3 _lastAnimPosition;
    private float _nextBodyAlign;
    private float _stuckTime;
    private Vector3 _lastProgressPosition;
    private float _nextProgressCheck;
    private bool _wantsToMove;

    public bool IsAlive => _state != AiState.Dead && _health > 0f;
    public Vector3 AimPoint => transform.position + Vector3.up * 1.15f;

    public void Initialize(GameApp app, int health, float damage, Color color)
    {
        _app = app;
        _health = Mathf.Max(1, health);
        _damage = Mathf.Max(1f, damage);
        _home = transform.position;
        _moveTarget = _home;
        _nextRepathTime = 0f;
        _stuckPosition = transform.position;
        _stuckCheckTime = Time.time + 1f;
        _nextBurstTime = Time.time + Random.Range(0.4f, 1.2f);

        _controller = GetComponent<CharacterController>();

        // 方块士兵模型：脚底在 y=0，总高 1.8，和 CharacterController 对齐。
        // CharacterController 的 skinWidth 会让胶囊底离地 8cm，模型要往下补偿这么多，
        // 否则看起来就是"悬在地面上"。
        var body = Art.BuildSoldier(transform);
        _body = body.transform;
        _body.localPosition = Vector3.zero;
        _rig = body.GetComponent<SoldierRig>();
        _lastAnimPosition = transform.position;

        _renderers = GetComponentsInChildren<Renderer>();

        _lastProgressPosition = transform.position;
        _nextProgressCheck = Time.time + 0.5f;

        SnapToGround();
    }

    private void OnEnable()
    {
        if (!Active.Contains(this)) Active.Add(this);
    }

    private void OnDisable()
    {
        Active.Remove(this);
    }

    private void Update()
    {
        if (_state == AiState.Dead)
        {
            // 死了也要继续跑倒地动画
            if (_rig != null) _rig.Tick(Mathf.Min(Time.deltaTime, 0.05f), 0f, false);
            return;
        }

        if (_app == null) return;

        var dt = Mathf.Min(Time.deltaTime, 0.05f);

        // 重力每帧都生效（以前只在移动时才有，站着不动就悬在半空）
        _wantsToMove = false;
        ApplyGravity(dt);
        AlignBodyToGround();

        var seesPlayer = CanSeePlayer(out var playerChest);

        if (seesPlayer)
        {
            _lastKnown = playerChest;
            _lostSightAt = -1f;

            if (_state != AiState.Engage)
            {
                _state = AiState.Engage;
                _alertUntil = Time.time + 0.55f; // 反应时间：不是瞬狙
            }
        }
        else if (_state == AiState.Engage)
        {
            if (_lostSightAt < 0f) _lostSightAt = Time.time;
            if (Time.time - _lostSightAt > 3.5f)
            {
                _state = AiState.Investigate;
                _moveTarget = _lastKnown;
                _nextRepathTime = Time.time + 6f;
            }
        }

        switch (_state)
        {
            case AiState.Patrol:
                UpdatePatrol(dt);
                break;
            case AiState.Investigate:
                UpdateInvestigate(dt);
                break;
            case AiState.Engage:
                UpdateEngage(dt, seesPlayer);
                break;
        }

        DriveAnimation(dt, seesPlayer);
        CheckProgress();
    }

    /// <summary>把移动速度、是否交战喂给骨架：走路摆腿、举枪、转头看人。</summary>
    private void DriveAnimation(float dt, bool seesPlayer)
    {
        if (_rig == null) return;

        var delta = transform.position - _lastAnimPosition;
        delta.y = 0f;
        _lastAnimPosition = transform.position;

        var speed = delta.magnitude / Mathf.Max(dt, 0.0001f);
        var speed01 = Mathf.Clamp01(speed / 3.2f);
        var aiming = _state == AiState.Engage && seesPlayer;

        _rig.Tick(dt, speed01, aiming);

        if (aiming && _app != null)
        {
            var toPlayer = _app.PlayerPosition - transform.position;
            toPlayer.y = 0f;

            if (toPlayer.sqrMagnitude > 0.01f)
            {
                var local = transform.InverseTransformDirection(toPlayer.normalized);
                _rig.SetHeadYaw(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg);
            }
        }
        else
        {
            _rig.SetHeadYaw(0f);
        }
    }

    private bool CanSeePlayer(out Vector3 playerChest)
    {
        playerChest = _app.PlayerPosition + Vector3.up * 1.45f;

        if (!_app.PlayerAlive) return false;

        var eye = transform.position + Vector3.up * 1.5f;
        var toPlayer = playerChest - eye;
        var distance = toPlayer.magnitude;

        if (distance > ViewDistance) return false;

        // 近距离全向感知，远距离只在视野锥内
        if (distance > 7f && Vector3.Angle(transform.forward, toPlayer) > ViewAngle * 0.5f)
        {
            return false;
        }

        if (Physics.Linecast(eye, playerChest, out var hit, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider == null || hit.collider.GetComponentInParent<PlayerRig>() == null)
            {
                return false;
            }
        }

        return true;
    }

    private void UpdatePatrol(float dt)
    {
        if (Time.time >= _nextRepathTime || FlatDistance(transform.position, _moveTarget) < 0.8f)
        {
            _nextRepathTime = Time.time + Random.Range(4f, 8f);
            _moveTarget = PickWanderPoint(_home, 9f);
        }

        MoveTowards(_moveTarget, PatrolSpeed, dt, false);
    }

    private void UpdateInvestigate(float dt)
    {
        if (FlatDistance(transform.position, _moveTarget) < 1.2f || Time.time >= _nextRepathTime)
        {
            _state = AiState.Patrol;
            _nextRepathTime = 0f;
            return;
        }

        MoveTowards(_moveTarget, ChaseSpeed * 0.85f, dt, false);
    }

    private void UpdateEngage(float dt, bool seesPlayer)
    {
        var playerPosition = _app.PlayerPosition;
        var distance = FlatDistance(transform.position, playerPosition);

        if (seesPlayer)
        {
            // 看见就面向玩家，不追太近也不站太远
            var look = playerPosition - transform.position;
            look.y = 0f;
            if (look.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation,
                    Quaternion.LookRotation(look.normalized), 10f * dt);
            }

            if (distance > 20f)
            {
                MoveTowards(playerPosition, ChaseSpeed, dt, true);
            }
            else if (distance < 7f)
            {
                var away = transform.position + (transform.position - playerPosition).normalized * 4f;
                MoveTowards(away, ChaseSpeed, dt, true);
            }

            TryShoot();
        }
        else
        {
            MoveTowards(_lastKnown, ChaseSpeed * 0.9f, dt, false);
        }
    }

    private void TryShoot()
    {
        if (Time.time < _alertUntil) return;

        if (_burstRemaining <= 0)
        {
            if (Time.time < _nextBurstTime) return;
            _burstRemaining = Random.Range(2, 5);
        }

        if (Time.time < _nextBurstShot) return;

        _nextBurstShot = Time.time + 0.12f;
        _burstRemaining--;

        if (_burstRemaining <= 0)
        {
            _nextBurstTime = Time.time + Random.Range(0.55f, 1.35f);
        }

        FireOne();
    }

    private void FireOne()
    {
        var player = _app.Player;
        if (player == null || !player.IsAlive) return;

        var muzzle = transform.position + Vector3.up * 1.42f + transform.forward * 0.45f;
        var target = player.CameraPosition;

        // 距离越远越不准：这是玩家能靠走位活下来的关键
        var distance = Vector3.Distance(muzzle, target);
        var spreadDegrees = Mathf.Lerp(1.2f, 4.5f, Mathf.InverseLerp(5f, 45f, distance));

        var direction = (target - muzzle).normalized;
        var random = Random.insideUnitCircle * Mathf.Tan(spreadDegrees * Mathf.Deg2Rad);
        direction = (direction + transform.right * random.x + transform.up * random.y).normalized;

        var endpoint = muzzle + direction * 120f;
        var hitsPlayer = false;

        if (Physics.Raycast(muzzle, direction, out var hit, 150f, ~0, QueryTriggerInteraction.Ignore))
        {
            endpoint = hit.point;
            if (hit.collider != null && hit.collider.GetComponentInParent<PlayerRig>() != null)
            {
                hitsPlayer = true;
            }
        }

        Fx.Tracer(muzzle, endpoint, new Color(1f, 0.45f, 0.3f, 0.85f), 0.025f);
        Fx.PlayAt(muzzle, Fx.RandomShot, 0.55f);

        if (hitsPlayer)
        {
            player.TakeDamage(_damage, transform.position);
        }
    }

    private void MoveTowards(Vector3 target, float speed, float dt, bool faceTarget)
    {
        var toTarget = target - transform.position;
        toTarget.y = 0f;

        if (toTarget.sqrMagnitude < 0.05f) return;

        _wantsToMove = toTarget.sqrMagnitude > 1f;

        var direction = toTarget.normalized;

        if (!faceTarget)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation,
                Quaternion.LookRotation(direction), 6f * dt);
        }

        if (Time.time < _detourUntil)
        {
            direction = _detourDirection;
        }

        // 贴墙滑行：前方有墙就沿墙面走，别一头撞上去卡死
        direction = AvoidWalls(direction, toTarget);

        if (_controller != null && _controller.enabled)
        {
            _controller.Move(direction * speed * dt);
        }

        // 卡住检测：1 秒内几乎没动就横着绕一下
        if (Time.time >= _stuckCheckTime)
        {
            if ((transform.position - _stuckPosition).sqrMagnitude < 0.12f)
            {
                var side = Random.value < 0.5f ? -1f : 1f;
                _detourDirection = Quaternion.Euler(0f, 65f * side, 0f) * direction;
                _detourUntil = Time.time + 1.1f;
            }

            _stuckPosition = transform.position;
            _stuckCheckTime = Time.time + 1f;
        }

        if (Time.time >= _nextStepSound && speed > 2.5f)
        {
            _nextStepSound = Time.time + 0.42f;
            Fx.PlayAt(transform.position, Fx.Step, 0.35f);
        }
    }

    private void ApplyGravity(float dt)
    {
        if (_controller == null || !_controller.enabled) return;
        if (_controller.isGrounded) return;

        _controller.Move(Vector3.down * 9.81f * dt);
    }

    private void SnapToGround()
    {
        var ground = Art.GroundY(transform.position);
        var position = transform.position;

        if (Mathf.Abs(position.y - ground) < 0.02f) return;

        position.y = ground;

        if (_controller != null) _controller.enabled = false;
        transform.position = position;
        if (_controller != null) _controller.enabled = true;
        Physics.SyncTransforms();
    }

    /// <summary>
    /// 前方 1.6 米内有墙就沿着墙面走。之前是一头撞墙站在原地，
    /// 玩家看到的是半个身子嵌进墙里的敌人，还打不到。
    /// </summary>
    private Vector3 AvoidWalls(Vector3 direction, Vector3 toTarget)
    {
        var origin = transform.position + Vector3.up * 0.95f;

        if (!Physics.SphereCast(origin, 0.34f, direction, out var hit, 1.6f, ~0, QueryTriggerInteraction.Ignore))
        {
            return direction;
        }

        if (hit.collider == null) return direction;
        if (hit.collider.GetComponentInParent<PlayerRig>() != null) return direction;
        if (hit.collider.GetComponentInParent<EnemyScav>() != null) return direction;

        var tangent = Vector3.ProjectOnPlane(direction, hit.normal);
        if (tangent.sqrMagnitude < 0.0004f)
        {
            tangent = Vector3.Cross(Vector3.up, hit.normal);
        }

        tangent.Normalize();
        if (Vector3.Dot(tangent, toTarget) < 0f) tangent = -tangent;

        // 稍微离开墙面一点，避免贴着墙摩擦
        return (tangent * 0.92f + hit.normal * 0.28f).normalized;
    }

    /// <summary>
    /// 让模型脚底始终贴着地面：CharacterController 的 skinWidth 会留一点缝，
    /// 上坡下坡也会有偏差，所以每 0.2 秒按射线重新对一次，肉眼看不出悬空。
    /// </summary>
    private void AlignBodyToGround()
    {
        if (_body == null || (_rig != null && _rig.Dead) || Time.time < _nextBodyAlign) return;
        _nextBodyAlign = Time.time + 0.2f;

        var ground = Art.GroundY(transform.position);
        var offset = Mathf.Clamp(ground - transform.position.y, -0.8f, 0.8f);
        _body.localPosition = new Vector3(0f, offset, 0f);
    }

    /// <summary>
    /// 想走却原地不动超过 1.5 秒 = 真卡住了，找一个附近的空地重新落位。
    /// 之前是"只要碰到墙就抬到墙顶"，结果敌人被弹到 3.4 米高的墙顶再掉下来，
    /// 看起来就像一群人在天上飘。
    /// </summary>
    private void CheckProgress()
    {
        if (Time.time < _nextProgressCheck) return;
        _nextProgressCheck = Time.time + 0.5f;

        var moved = (transform.position - _lastProgressPosition).sqrMagnitude;
        _lastProgressPosition = transform.position;

        if (!_wantsToMove || moved > 0.04f)
        {
            _stuckTime = 0f;
            return;
        }

        _stuckTime += 0.5f;
        if (_stuckTime < 1.5f) return;

        _stuckTime = 0f;
        RelocateToOpenSpace();
    }

    private void RelocateToOpenSpace()
    {
        for (var attempt = 0; attempt < 12; attempt++)
        {
            var angle = Random.value * Mathf.PI * 2f;
            var distance = Random.Range(3f, 8f);
            var candidate = transform.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
            candidate.y = Art.GroundY(candidate);

            if (Physics.CheckCapsule(candidate + Vector3.up * 0.45f, candidate + Vector3.up * 1.35f, 0.4f,
                    ~0, QueryTriggerInteraction.Ignore))
            {
                continue;
            }

            if (_controller != null) _controller.enabled = false;
            transform.position = candidate;
            if (_controller != null) _controller.enabled = true;
            Physics.SyncTransforms();

            _lastProgressPosition = candidate;
            return;
        }
    }

    private Vector3 PickWanderPoint(Vector3 origin, float radius)
    {
        for (var attempt = 0; attempt < 8; attempt++)
        {
            var angle = Random.value * Mathf.PI * 2f;
            var distance = Random.Range(3f, radius);
            var point = origin + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;

            if (Physics.Raycast(point + Vector3.up * 3f, Vector3.down, out var ground, 8f,
                    ~0, QueryTriggerInteraction.Ignore))
            {
                point.y = ground.point.y + 0.1f;
            }
            else
            {
                continue;
            }

            // 目标点别落在墙里
            if (Physics.CheckSphere(point + Vector3.up * 0.9f, 0.45f, ~0, QueryTriggerInteraction.Ignore))
            {
                continue;
            }

            return point;
        }

        return origin;
    }

    /// <summary>听到枪声，跑过去看看。</summary>
    public void HearShot(Vector3 position)
    {
        if (_state == AiState.Dead || !IsAlive) return;
        if (Vector3.Distance(transform.position, position) > HearDistance) return;

        if (_state == AiState.Engage) return;

        _state = AiState.Investigate;
        _lastKnown = position;
        _moveTarget = position;
        _nextRepathTime = Time.time + 8f;
    }

    public bool TakeDamage(float amount)
    {
        if (!IsAlive) return false;

        _health -= amount;

        Fx.PlayAt(transform.position + Vector3.up * 1.2f, Fx.HitFlesh, 0.45f);
        if (_rig != null) _rig.Flinch(0.9f);

        // 被打了一定会警觉，哪怕没看见
        if (_app != null && _app.PlayerAlive)
        {
            _lastKnown = _app.PlayerPosition + Vector3.up * 1.4f;
            if (_state != AiState.Engage)
            {
                _state = AiState.Engage;
                _alertUntil = Mathf.Min(_alertUntil, Time.time + 0.2f);
            }
        }

        if (_health <= 0f)
        {
            Die();
        }

        return true;
    }

    private void Die()
    {
        _state = AiState.Dead;

        if (_controller != null) _controller.enabled = false;

        var collider = GetComponent<Collider>();
        if (collider != null) collider.enabled = false;

        // 保留渲染：尸体躺在地上比瞬间消失自然得多
        if (_rig != null) _rig.Die();

        Fx.PlayAt(transform.position, Fx.Death, 0.45f);

        var root = transform.parent != null ? transform.parent : transform;
        LootContainer.Spawn(root, transform.position + Vector3.up * 0.25f,
            "loot_scav_body", "尸体", new Color(0.42f, 0.16f, 0.16f), new Vector3(0.9f, 0.4f, 0.9f));

        if (_app != null) _app.OnEnemyKilled(this);

        Destroy(gameObject, 15f);
    }

    private static float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    public string DebugState => _state.ToString();
}