using UnityEngine;

/// <summary>
/// 撤离点：站在圈里按住 5 秒撤离。离开会缓慢回退（不是瞬间清零），
/// 这样被枪线打断时不会太惩罚玩家。
/// </summary>
public sealed class ExtractionZone : MonoBehaviour
{
    public const float HoldSeconds = 5f;

    private GameApp _app;
    private Vector3 _center;
    private float _radius = 4.5f;
    private float _progress;
    private bool _completed;
    private float _nextBeep;

    public string ZoneName { get; private set; } = "撤离点";
    public bool PlayerInside { get; private set; }
    public float Progress01 => Mathf.Clamp01(_progress / HoldSeconds);
    public float DistanceToPlayer { get; private set; } = 999f;

    public void Initialize(GameApp app, string zoneName, Vector3 center, float radius)
    {
        _app = app;
        ZoneName = zoneName;
        _center = center;
        _radius = Mathf.Max(1f, radius);

        var trigger = gameObject.AddComponent<SphereCollider>();
        trigger.radius = _radius;
        trigger.isTrigger = true;
        trigger.center = Vector3.zero;
    }

    public Vector3 Center => _center;

    private void Update()
    {
        if (_app == null || _completed || !_app.PlayerAlive)
        {
            PlayerInside = false;
            return;
        }

        var playerPosition = _app.PlayerPosition;
        var flat = new Vector2(playerPosition.x - _center.x, playerPosition.z - _center.z);
        DistanceToPlayer = flat.magnitude;
        PlayerInside = DistanceToPlayer <= _radius;

        if (PlayerInside)
        {
            _progress += Time.deltaTime;

            if (Time.time >= _nextBeep)
            {
                _nextBeep = Time.time + Mathf.Lerp(0.12f, 0.6f, Progress01);
                Fx.Play2D(Fx.Beep, 0.35f);
            }

            if (_progress >= HoldSeconds)
            {
                _completed = true;
                _app.OnExtractionComplete(this);
            }
        }
        else
        {
            _progress = Mathf.Max(0f, _progress - Time.deltaTime * 1.5f);
        }
    }
}