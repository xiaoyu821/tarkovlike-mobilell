using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 程序化音效 + 曳光弹池。整个项目没有任何美术/音频资源文件，
/// 所有声音都是启动时用代码合成的，所以不存在"资源被裁掉"的问题。
/// </summary>
public sealed class Fx : MonoBehaviour
{
    private const int TracerPoolSize = 12;
    private const int SourcePoolSize = 16;

    private static Fx _host;
    private static Material _lineMaterial;

    private static readonly List<LineRenderer> _tracers = new List<LineRenderer>();
    private static readonly List<float> _tracerDeadline = new List<float>();
    private static readonly List<AudioSource> _sources = new List<AudioSource>();
    private static int _tracerCursor;
    private static int _sourceCursor;

    private static AudioClip _shotA;
    private static AudioClip _shotB;
    private static AudioClip _shotC;
    private static AudioClip _dry;
    private static AudioClip _hitFlesh;
    private static AudioClip _hitMetal;
    private static AudioClip _step;
    private static AudioClip _hurt;
    private static AudioClip _heal;
    private static AudioClip _search;
    private static AudioClip _beep;
    private static AudioClip _death;
    private static AudioClip _click;

    // ---------- 对外接口 ----------

    public static AudioClip RandomShot => PickShot();
    public static AudioClip Dry => _dry != null ? _dry : (_dry = BuildClick(2400f, 0.06f));
    public static AudioClip HitFlesh => _hitFlesh != null ? _hitFlesh : (_hitFlesh = BuildHit(1180f, 0.10f));
    public static AudioClip HitMetal => _hitMetal != null ? _hitMetal : (_hitMetal = BuildHit(2150f, 0.14f));
    public static AudioClip Step => _step != null ? _step : (_step = BuildStep());
    public static AudioClip Hurt => _hurt != null ? _hurt : (_hurt = BuildHurt());
    public static AudioClip Heal => _heal != null ? _heal : (_heal = BuildHeal());
    public static AudioClip Search => _search != null ? _search : (_search = BuildSearch());
    public static AudioClip Beep => _beep != null ? _beep : (_beep = BuildBeep());
    public static AudioClip Death => _death != null ? _death : (_death = BuildDeath());
    public static AudioClip Click => _click != null ? _click : (_click = BuildClick(1600f, 0.05f));

    /// <summary>提前把所有 clip 合成好，避免第一次开枪时卡一下。</summary>
    public static void EnsureLoaded()
    {
        EnsureHost();
        PickShot();
        var unused = Dry; unused = HitFlesh; unused = HitMetal; unused = Step;
        unused = Hurt; unused = Heal; unused = Search; unused = Beep; unused = Death; unused = Click;
    }

    /// <summary>2D 音效（玩家自己的声音、UI）。</summary>
    public static void Play2D(AudioClip clip, float volume)
    {
        if (clip == null) return;
        EnsureHost();
        var source = NextSource(false);
        source.transform.position = Vector3.zero;
        source.PlayOneShot(clip, Mathf.Clamp01(volume));
    }

    /// <summary>3D 音效（敌人枪声、脚步），带距离衰减，听声辨位用。</summary>
    public static void PlayAt(Vector3 position, AudioClip clip, float volume)
    {
        if (clip == null) return;
        EnsureHost();
        var source = NextSource(true);
        source.transform.position = position;
        source.PlayOneShot(clip, Mathf.Clamp01(volume));
    }

    public static void Tracer(Vector3 from, Vector3 to, Color color, float width)
    {
        EnsureHost();
        if (_tracers.Count == 0) return;

        var index = _tracerCursor;
        _tracerCursor = (_tracerCursor + 1) % _tracers.Count;

        var tracer = _tracers[index];
        if (tracer == null) return;

        tracer.startWidth = width;
        tracer.endWidth = width * 0.4f;
        tracer.startColor = color;
        tracer.endColor = new Color(color.r, color.g, color.b, color.a * 0.15f);
        tracer.SetPosition(0, from);
        tracer.SetPosition(1, to);
        tracer.enabled = true;
        _tracerDeadline[index] = Time.time + 0.07f;
    }

    // ---------- 内部 ----------

    private void Update()
    {
        for (var i = 0; i < _tracers.Count; i++)
        {
            var tracer = _tracers[i];
            if (tracer != null && tracer.enabled && Time.time >= _tracerDeadline[i])
            {
                tracer.enabled = false;
            }
        }
    }

    private static AudioClip PickShot()
    {
        var slot = Random.Range(0, 3);
        if (slot == 0) return _shotA != null ? _shotA : (_shotA = BuildShot(11, 92f));
        if (slot == 1) return _shotB != null ? _shotB : (_shotB = BuildShot(23, 84f));
        return _shotC != null ? _shotC : (_shotC = BuildShot(37, 101f));
    }

    private static void EnsureHost()
    {
        if (_host != null && _tracers.Count > 0)
        {
            return;
        }

        if (_host == null)
        {
            var go = new GameObject("FxPool");
            DontDestroyOnLoad(go);
            _host = go.AddComponent<Fx>();
        }

        var root = _host.transform;

        if (_lineMaterial == null)
        {
            _lineMaterial = BuildLineMaterial();
        }

        while (_tracers.Count < TracerPoolSize)
        {
            var tracerGo = new GameObject("Tracer" + _tracers.Count);
            tracerGo.transform.SetParent(root, false);

            var tracer = tracerGo.AddComponent<LineRenderer>();
            tracer.positionCount = 2;
            tracer.useWorldSpace = true;
            tracer.numCapVertices = 0;
            tracer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            tracer.receiveShadows = false;
            if (_lineMaterial != null) tracer.material = _lineMaterial;
            tracer.startColor = new Color(1f, 0.88f, 0.42f, 1f);
            tracer.endColor = new Color(1f, 0.72f, 0.25f, 0.15f);
            tracer.enabled = false;

            _tracers.Add(tracer);
            _tracerDeadline.Add(0f);
        }

        while (_sources.Count < SourcePoolSize)
        {
            var sourceGo = new GameObject("Sfx" + _sources.Count);
            sourceGo.transform.SetParent(root, false);

            var source = sourceGo.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 4f;
            source.maxDistance = 80f;
            _sources.Add(source);
        }
    }

    private static AudioSource NextSource(bool spatial)
    {
        var source = _sources[_sourceCursor];
        _sourceCursor = (_sourceCursor + 1) % _sources.Count;
        source.spatialBlend = spatial ? 1f : 0f;
        return source;
    }

    private static Material BuildLineMaterial()
    {
        // 内置 shader 在 IL2CPP 包里可能被裁，按顺序回退；全都没有就返回 null，
        // 调用方（Fx.Tracer）会自动跳过，不会抛异常。
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
            if (shader != null)
            {
                return new Material(shader) { color = Color.white };
            }
        }

        return null;
    }

    // ---------- 合成 ----------

    private static AudioClip BuildShot(int seed, float thumpHz)
    {
        const int rate = 44100;
        const float duration = 0.30f;
        var count = Mathf.CeilToInt(rate * duration);
        var data = new float[count];
        var rng = new System.Random(seed);

        var low = 0f;
        var lower = 0f;

        for (var i = 0; i < count; i++)
        {
            var t = i / (float)rate;
            var noise = (float)(rng.NextDouble() * 2.0 - 1.0);

            low += (noise - low) * 0.42f;
            lower += (low - lower) * 0.06f;

            var transient = t < 0.005f ? noise * (1f - t / 0.005f) * 0.9f : 0f;
            var body = low * Mathf.Exp(-t * 60f) * 1.5f;
            var thump = Mathf.Sin(2f * Mathf.PI * thumpHz * t) * Mathf.Exp(-t * 24f) * 0.85f;
            var tail = lower * Mathf.Exp(-t * 5.5f) * 0.45f;

            data[i] = Mathf.Clamp(transient + body + thump + tail, -1f, 1f);
        }

        var clip = AudioClip.Create("m0_shot_" + seed, count, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private static AudioClip BuildClick(float frequency, float duration)
    {
        const int rate = 44100;
        var count = Mathf.CeilToInt(rate * duration);
        var data = new float[count];
        var rng = new System.Random(7);
        var low = 0f;

        for (var i = 0; i < count; i++)
        {
            var t = i / (float)rate;
            var noise = (float)(rng.NextDouble() * 2.0 - 1.0);
            low += (noise - low) * 0.6f;
            var ping = Mathf.Sin(2f * Mathf.PI * frequency * t) * Mathf.Exp(-t * 90f) * 0.25f;
            data[i] = Mathf.Clamp((low * 0.7f + ping) * Mathf.Exp(-t * 70f), -1f, 1f);
        }

        var clip = AudioClip.Create("fx_click", count, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private static AudioClip BuildHit(float frequency, float duration)
    {
        const int rate = 44100;
        var count = Mathf.CeilToInt(rate * duration);
        var data = new float[count];
        var rng = new System.Random(101);

        for (var i = 0; i < count; i++)
        {
            var t = i / (float)rate;
            var noise = (float)(rng.NextDouble() * 2.0 - 1.0);
            var a = Mathf.Sin(2f * Mathf.PI * frequency * t) * Mathf.Exp(-t * 38f);
            var b = Mathf.Sin(2f * Mathf.PI * frequency * 1.55f * t) * Mathf.Exp(-t * 55f) * 0.55f;
            var click = noise * Mathf.Exp(-t * 220f) * 0.35f;
            data[i] = Mathf.Clamp((a + b + click) * 0.5f, -1f, 1f);
        }

        var clip = AudioClip.Create("fx_hit", count, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private static AudioClip BuildStep()
    {
        const int rate = 44100;
        const float duration = 0.09f;
        var count = Mathf.CeilToInt(rate * duration);
        var data = new float[count];
        var rng = new System.Random(33);

        for (var i = 0; i < count; i++)
        {
            var t = i / (float)rate;
            var noise = (float)(rng.NextDouble() * 2.0 - 1.0);
            data[i] = Mathf.Clamp(noise * Mathf.Exp(-t * 55f) * 0.35f, -1f, 1f);
        }

        var clip = AudioClip.Create("fx_step", count, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private static AudioClip BuildHurt()
    {
        const int rate = 44100;
        const float duration = 0.22f;
        var count = Mathf.CeilToInt(rate * duration);
        var data = new float[count];
        var rng = new System.Random(55);

        for (var i = 0; i < count; i++)
        {
            var t = i / (float)rate;
            var noise = (float)(rng.NextDouble() * 2.0 - 1.0);
            var thud = Mathf.Sin(2f * Mathf.PI * 140f * t) * Mathf.Exp(-t * 18f) * 0.8f;
            data[i] = Mathf.Clamp(thud + noise * Mathf.Exp(-t * 40f) * 0.3f, -1f, 1f);
        }

        var clip = AudioClip.Create("fx_hurt", count, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private static AudioClip BuildHeal()
    {
        const int rate = 44100;
        const float duration = 0.45f;
        var count = Mathf.CeilToInt(rate * duration);
        var data = new float[count];
        var rng = new System.Random(66);
        var low = 0f;

        for (var i = 0; i < count; i++)
        {
            var t = i / (float)rate;
            var noise = (float)(rng.NextDouble() * 2.0 - 1.0);
            low += (noise - low) * 0.15f;

            var freq = Mathf.Lerp(300f, 620f, t / duration);
            var tone = Mathf.Sin(2f * Mathf.PI * freq * t) * Mathf.Clamp01(1f - t / duration) * 0.22f;
            data[i] = Mathf.Clamp(tone + low * 0.3f, -1f, 1f);
        }

        var clip = AudioClip.Create("fx_heal", count, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private static AudioClip BuildSearch()
    {
        const int rate = 44100;
        const float duration = 0.35f;
        var count = Mathf.CeilToInt(rate * duration);
        var data = new float[count];
        var rng = new System.Random(77);
        var low = 0f;

        for (var i = 0; i < count; i++)
        {
            var t = i / (float)rate;
            var noise = (float)(rng.NextDouble() * 2.0 - 1.0);
            low += (noise - low) * 0.3f;
            var gate = Mathf.Sin(2f * Mathf.PI * 9f * t) * 0.5f + 0.5f;
            data[i] = Mathf.Clamp(low * gate * 0.45f, -1f, 1f);
        }

        var clip = AudioClip.Create("fx_search", count, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private static AudioClip BuildBeep()
    {
        const int rate = 44100;
        const float duration = 0.18f;
        var count = Mathf.CeilToInt(rate * duration);
        var data = new float[count];

        for (var i = 0; i < count; i++)
        {
            var t = i / (float)rate;
            var freq = t < duration * 0.5f ? 880f : 1320f;
            data[i] = Mathf.Clamp(Mathf.Sin(2f * Mathf.PI * freq * t) * 0.28f, -1f, 1f);
        }

        var clip = AudioClip.Create("fx_beep", count, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private static AudioClip BuildDeath()
    {
        const int rate = 44100;
        const float duration = 0.7f;
        var count = Mathf.CeilToInt(rate * duration);
        var data = new float[count];
        var rng = new System.Random(99);

        for (var i = 0; i < count; i++)
        {
            var t = i / (float)rate;
            var noise = (float)(rng.NextDouble() * 2.0 - 1.0);
            var freq = Mathf.Lerp(220f, 55f, t / duration);
            var tone = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.4f;
            data[i] = Mathf.Clamp((tone + noise * 0.2f) * Mathf.Exp(-t * 3f), -1f, 1f);
        }

        var clip = AudioClip.Create("fx_death", count, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}