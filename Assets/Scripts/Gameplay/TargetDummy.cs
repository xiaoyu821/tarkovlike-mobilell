using System.Collections;
using UnityEngine;

/// <summary>M0 训练靶：可被射击，血量归零后隐藏并在 3 秒后原地复活。</summary>
public sealed class TargetDummy : MonoBehaviour
{
    private const float RespawnSeconds = 3f;

    private float _maxHealth = 100f;
    private float _health = 100f;
    private Renderer _renderer;
    private Collider _collider;
    private Color _baseColor;
    private Vector3 _homePosition;
    private Quaternion _homeRotation;
    private Coroutine _flashRoutine;

    public void Initialize(int health)
    {
        _maxHealth = Mathf.Max(1, health);
        _health = _maxHealth;
        _renderer = GetComponent<Renderer>();
        _collider = GetComponent<Collider>();
        _homePosition = transform.position;
        _homeRotation = transform.rotation;
        _baseColor = _renderer.material.color;
    }

    public void TakeDamage(float amount)
    {
        if (_health <= 0f)
        {
            return;
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
    }

    private IEnumerator Flash()
    {
        _renderer.material.color = Color.white;
        yield return new WaitForSeconds(0.05f);

        if (_health > 0f)
        {
            _renderer.material.color = _baseColor;
        }
    }

    private IEnumerator Die()
    {
        _renderer.enabled = false;
        _collider.enabled = false;
        yield return new WaitForSeconds(RespawnSeconds);

        _health = _maxHealth;
        transform.position = _homePosition;
        transform.rotation = _homeRotation;
        _renderer.material.color = _baseColor;
        _renderer.enabled = true;
        _collider.enabled = true;
    }
}
