using UnityEngine;

/// <summary>
/// 方块士兵的骨架与程序化动作。没有 AnimationClip，
/// 走路摆腿摆手、呼吸、举枪、受击后仰、中弹倒地全靠插值。
/// </summary>
public sealed class SoldierRig : MonoBehaviour
{
    private Transform _legL;
    private Transform _legR;
    private Transform _shoulderL;
    private Transform _shoulderR;
    private Transform _torso;
    private Transform _head;
    private Transform _gun;

    private float _phase;
    private float _aimBlend;
    private float _flinch;
    private float _headYaw;
    private float _breathSeed;

    private bool _dead;
    private float _deadTime;

    public bool Dead => _dead;

    public void Bind(Transform legL, Transform legR, Transform shoulderL, Transform shoulderR,
        Transform torso, Transform head, Transform gun)
    {
        _legL = legL;
        _legR = legR;
        _shoulderL = shoulderL;
        _shoulderR = shoulderR;
        _torso = torso;
        _head = head;
        _gun = gun;

        _breathSeed = Random.value * 10f;
    }

    public void SetHeadYaw(float degrees)
    {
        _headYaw = Mathf.Clamp(degrees, -60f, 60f);
    }

    public void Flinch(float amount)
    {
        _flinch = Mathf.Clamp01(_flinch + amount);
    }

    public void Die()
    {
        if (_dead) return;
        _dead = true;
        _deadTime = 0f;
    }

    public void Tick(float dt, float speed01, bool aiming)
    {
        if (_legL == null) return;

        if (_dead)
        {
            TickDeath(dt);
            return;
        }

        // 步频跟着移动速度走
        var cadence = Mathf.Lerp(0f, 9.5f, speed01);
        _phase += dt * cadence * Mathf.PI * 2f * 0.5f;

        var stride = Mathf.Sin(_phase) * (12f + 18f * speed01);
        var bob = Mathf.Abs(Mathf.Cos(_phase)) * 0.045f * speed01;
        var breath = Mathf.Sin(Time.time * 1.7f + _breathSeed) * 0.008f;
        var sway = Mathf.Sin(_phase * 0.5f) * 3f * speed01;

        _aimBlend = Mathf.MoveTowards(_aimBlend, aiming ? 1f : 0f, dt * 5.5f);
        _flinch = Mathf.MoveTowards(_flinch, 0f, dt * 3.2f);

        // 腿：正向摆动
        _legL.localRotation = Quaternion.Euler(stride, 0f, 0f);
        _legR.localRotation = Quaternion.Euler(-stride, 0f, 0f);

        // 躯干：上下起伏 + 左右轻晃 + 受击后仰
        _torso.localPosition = new Vector3(0f, 0.88f + bob + breath, 0f);
        _torso.localRotation = Quaternion.Euler(-_flinch * 14f, sway * 0.4f, sway);

        // 手臂：走路反向摆，开镜时抬枪
        var armSwing = Mathf.Sin(_phase + Mathf.PI) * (8f + 14f * speed01) * (1f - _aimBlend);

        _shoulderL.localRotation = Quaternion.Slerp(
            Quaternion.Euler(armSwing, 0f, 0f),
            Quaternion.Euler(-62f, -22f, 0f),
            _aimBlend);

        _shoulderR.localRotation = Quaternion.Slerp(
            Quaternion.Euler(-armSwing, 0f, 0f),
            Quaternion.Euler(-74f, 6f, 0f),
            _aimBlend);

        _gun.localRotation = Quaternion.Slerp(
            Quaternion.identity,
            Quaternion.Euler(72f, 0f, 0f),
            _aimBlend);

        // 头：看向目标方向 + 一点点走路摆动
        _head.localRotation = Quaternion.Euler(0f, _headYaw * (1f - _aimBlend * 0.7f), sway * 0.5f);
    }

    private void TickDeath(float dt)
    {
        _deadTime += dt;

        var t = Mathf.Clamp01(_deadTime / 0.55f);
        var ease = 1f - (1f - t) * (1f - t);

        // 绕脚底倒下，躺平后脑袋正好在地上
        transform.localRotation = Quaternion.Euler(-84f * ease, 6f * ease, 14f * ease);
        transform.localPosition = new Vector3(0f, 0.05f * ease, 0f);

        if (_legL != null)
        {
            _legL.localRotation = Quaternion.Slerp(_legL.localRotation, Quaternion.Euler(14f, 0f, 0f),
                dt * 4f);
            _legR.localRotation = Quaternion.Slerp(_legR.localRotation, Quaternion.Euler(-10f, 0f, 0f),
                dt * 4f);
        }

        if (_shoulderR != null)
        {
            _shoulderR.localRotation = Quaternion.Slerp(_shoulderR.localRotation,
                Quaternion.Euler(24f, 0f, -20f), dt * 3f);
            _shoulderL.localRotation = Quaternion.Slerp(_shoulderL.localRotation,
                Quaternion.Euler(16f, 0f, 18f), dt * 3f);
        }
    }
}