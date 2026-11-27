using UnityEngine;

/// <summary>
/// 第一人称武器模型：挂在相机下的枪，负责走路晃动、拖拽滞后、开火后坐、
/// 换弹下沉、开镜收向准星、冲刺压低。全部用位移/旋转插值，不需要动画资源。
/// </summary>
public sealed class WeaponViewmodel : MonoBehaviour
{
    private static readonly Vector3 HipPosition = new Vector3(0.17f, -0.155f, 0.42f);
    // 开镜位置按这把枪的机瞄高度算，保证准星线正好落在屏幕中心
    private Vector3 _aimPosition = new Vector3(0f, -0.086f, 0.30f);
    private static readonly Vector3 SprintOffset = new Vector3(0.03f, -0.075f, -0.03f);

    private Transform _model;
    private Vector3 _current;
    private Vector3 _modelOffset;

    private Vector3 _basePosition;
    private Vector2 _lookSway;

    private float _bobTime;
    private float _kick;
    private float _reloadDip;
    private float _moveAmount;
    private ViewmodelHideOnAim[] _hideOnAim = new ViewmodelHideOnAim[0];
    private bool _hiddenApplied;
    private bool _aiming;
    private bool _reloading;
    private bool _sprinting;

    public static WeaponViewmodel Create(Transform cameraTransform, string weaponId)
    {
        if (cameraTransform == null) return null;

        var go = new GameObject("WeaponViewmodel");
        go.transform.SetParent(cameraTransform, false);

        var viewmodel = go.AddComponent<WeaponViewmodel>();
        viewmodel.Build(weaponId);
        return viewmodel;
    }

    private void Build(string weaponId)
    {
        _basePosition = HipPosition;
        _current = HipPosition;
        _modelOffset = Vector3.zero;

        transform.localPosition = HipPosition;
        transform.localRotation = Quaternion.identity;
        transform.localScale = Vector3.one;

        var model = Art.BuildViewmodel(transform, weaponId);
        _model = model.transform;

        var sight = model.GetComponent<ViewmodelSight>();
        if (sight != null) _aimPosition = new Vector3(0f, -sight.Height, 0.32f);

        _hideOnAim = model.GetComponentsInChildren<ViewmodelHideOnAim>(true);

        // 枪不能投影：0.4 米外的模型打阴影会在屏幕上糊一片
        var renderers = model.GetComponentsInChildren<MeshRenderer>();
        for (var i = 0; i < renderers.Length; i++)
        {
            renderers[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderers[i].receiveShadows = false;
        }
    }

    public void AddLook(Vector2 delta)
    {
        _lookSway += delta;
    }

    public void Kick(float strength)
    {
        _kick = Mathf.Min(1.2f, _kick + 0.55f * Mathf.Max(0.2f, strength));
    }

    public void SetAiming(bool value)
    {
        _aiming = value;
    }

    public void SetReloading(bool value)
    {
        _reloading = value;
    }

    public void SetMovement(float moveAmount, bool sprinting)
    {
        _moveAmount = Mathf.Clamp01(moveAmount);
        _sprinting = sprinting;
    }

    private void Update()
    {
        if (_model == null) return;

        var dt = Mathf.Min(Time.deltaTime, 0.05f);

        // 1) 位置：腰射 ↔ 开镜，叠加走路晃动和拖拽滞后
        var target = _aiming ? _aimPosition : _basePosition;

        _bobTime += dt * Mathf.Lerp(5f, 10f, _moveAmount) * (_sprinting ? 1.35f : 1f);
        var bobStrength = Mathf.Lerp(0.0015f, 0.011f, _moveAmount) * (_aiming ? 0.1f : 1f);
        var bobX = Mathf.Sin(_bobTime) * bobStrength;
        var bobY = Mathf.Abs(Mathf.Cos(_bobTime)) * bobStrength * 0.7f;

        _lookSway = Vector2.Lerp(_lookSway, Vector2.zero, 7f * dt);
        var swayScale = _aiming ? 0.12f : 1f;
        var swayX = Mathf.Clamp(-_lookSway.x * 0.00035f, -0.028f, 0.028f) * swayScale;
        var swayY = Mathf.Clamp(-_lookSway.y * 0.00035f, -0.02f, 0.02f) * swayScale;

        var follow = _aiming ? 15f : 9f;
        _current = Vector3.Lerp(_current, target + new Vector3(bobX + swayX, bobY + swayY, 0f), follow * dt);
        transform.localPosition = _current;

        // 开镜时把枪托收起来，不然会顶到镜头前挡视线
        if (_aiming != _hiddenApplied)
        {
            _hiddenApplied = _aiming;
            for (var i = 0; i < _hideOnAim.Length; i++)
            {
                if (_hideOnAim[i] != null) _hideOnAim[i].gameObject.SetActive(!_aiming);
            }
        }

        // 2) 模型：后坐 + 换弹 + 冲刺
        _kick = Mathf.MoveTowards(_kick, 0f, 5f * dt);

        var kickOffset = new Vector3(0f, _kick * 0.010f, -_kick * 0.055f);
        var kickRotation = Quaternion.Euler(-_kick * 8f, _kick * 1.5f, _kick * 2.5f);

        var reloadTarget = _reloading ? 1f : 0f;
        _reloadDip = Mathf.Lerp(_reloadDip, reloadTarget, 9f * dt);
        var reloadOffset = new Vector3(0f, -_reloadDip * 0.11f, -_reloadDip * 0.02f);
        var reloadRotation = Quaternion.Euler(_reloadDip * 28f, _reloadDip * -16f, _reloadDip * -10f);

        if (_reloading)
        {
            reloadRotation *= Quaternion.Euler(Mathf.Sin(Time.time * 24f) * 2.5f, 0f,
                Mathf.Cos(Time.time * 19f) * 2.5f);
        }

        var sprintOffset = _sprinting ? SprintOffset * _moveAmount : Vector3.zero;
        var sprintRotation = Quaternion.Euler(-_moveAmount * (_sprinting ? 16f : 0f), 0f, 0f);

        var targetOffset = kickOffset + reloadOffset + sprintOffset;
        _modelOffset = Vector3.Lerp(_modelOffset, targetOffset, 16f * dt);
        _model.localPosition = _modelOffset;

        var targetRotation = kickRotation * reloadRotation * sprintRotation;
        _model.localRotation = Quaternion.Slerp(_model.localRotation, targetRotation, 16f * dt);
    }
}