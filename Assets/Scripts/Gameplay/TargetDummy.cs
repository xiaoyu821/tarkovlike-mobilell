using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>M0 训练靶：可被射击，血量归零后隐藏并在 3 秒后原地复活。</summary>
public sealed class TargetDummy : MonoBehaviour
{
    private const float RespawnSeconds = 3f;

    /// <summary>当前场景里活着的靶子，供辅助瞄准遍历（避免每发 FindObjectsOfType）。</summary>
    public static readonly List<TargetDummy> Active = new List<TargetDummy>();

    private float _maxHealth = 100f;
    private float _health = 100f;
    private Renderer _renderer;
    private Collider _collider;
    private Material _material;
    private Color _baseColor = Color.white;
    private Vector3 _homePosition;
    private Quaternion _homeRotation;
    private Coroutine _flashRoutine;

    public bool IsAlive => _health > 0f;
    public Vector3 AimPoint => transform.position + Vector3.up * 0.15f;
    public float Health01 => _maxHealth <= 0f ? 0f : Mathf.Clamp01(_health / _maxHealth);

    private void OnEnable()
    {
        if (!Active.Contains(this))
        {
            Active.Add(this);
        }
    }

    private void OnDisable()
    {
        Active.Remove(this);
    }

    public void Initialize(int health)
    {
        _maxHealth = Mathf.Max(1, health);
        _health = _maxHealth;
        _renderer = GetComponent<Renderer>();
        _collider = GetComponent<Collider>();
        _homePosition = transform.position;
        _homeRotation = transform.rotation;

        if (_renderer != null)
        {
            // 缓存材质实例：每次访问 .material 都会 new 一份，长时间玩会一直漏
            _material = _renderer.material;
            _baseColor = _material.color;
        }
    }

    /// <summary>返回是否真的造成了伤害（辅助瞄准/命中反馈要用）。</summary>
    public bool TakeDamage(float amount)
    {
        if (_health <= 0f)
        {
            return false;
        }

        _health -= amount;

        if (_flashRoutine != null)
        {
            StopCoroutine(_flashRoutine);
        }

        _flashRoutine = StartCoroutine(Flash());

        if (_health <= 0f)
        {
            StartCoroutine(Die());
        }

        return true;
    }

    private IEnumerator Flash()
    {
        if (_material != null)
        {
            _material.color = Color.white;
        }

        yield return new WaitForSeconds(0.05f);

        if (_health > 0f && _material != null)
        {
            _material.color = _baseColor;
        }
    }

    private IEnumerator Die()
    {
        if (_renderer != null) _renderer.enabled = false;
        if (_collider != null) _collider.enabled = false;

        yield return new WaitForSeconds(RespawnSeconds);

        _health = _maxHealth;
        transform.position = _homePosition;
        transform.rotation = _homeRotation;

        if (_material != null) _material.color = _baseColor;
        if (_renderer != null) _renderer.enabled = true;
        if (_collider != null) _collider.enabled = true;
    }
}