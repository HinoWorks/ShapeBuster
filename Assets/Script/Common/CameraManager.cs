using UnityEngine;

public enum CameraShakeLevel
{
    Small,
    Medium,
    Large,
}

/// <summary>
/// 画面シェイク。CameraManager.Inst.Shake で強さを指定して呼ぶ。
/// </summary>
public class CameraManager : MonoBehaviour
{
    public static CameraManager Inst { get; private set; }

    [SerializeField] Transform shakeTarget;

    [Header("Small")]
    [SerializeField] float amplitudeSmall = 0.06f;
    [SerializeField] float durationSmall = 0.1f;

    [Header("Medium")]
    [SerializeField] float amplitudeMedium = 0.14f;
    [SerializeField] float durationMedium = 0.18f;

    [Header("Large")]
    [SerializeField] float amplitudeLarge = 0.28f;
    [SerializeField] float durationLarge = 0.28f;

    [SerializeField] float frequency = 28f;

    Vector3 restLocalPosition;
    float amplitude;
    float duration;
    float remaining;
    float noiseTime;
    float seed;

    void Awake()
    {
        if (Inst == null) { Inst = this; }
        else { Destroy(this); return; }

        if (shakeTarget == null)
        {
            var cam = GetComponent<Camera>();
            if (cam == null) cam = Camera.main;
            shakeTarget = cam != null ? cam.transform : transform;
        }

        restLocalPosition = shakeTarget.localPosition;
        seed = Random.Range(0f, 100f);
    }

    void OnDestroy()
    {
        if (Inst == this) Inst = null;
        Restore();
    }

    public void Shake(CameraShakeLevel level)
    {
        var preset = Resolve(level);
        if (preset.amplitude <= 0f || preset.duration <= 0f) return;

        if (remaining <= 0f || preset.amplitude >= amplitude)
        {
            amplitude = preset.amplitude;
            duration = preset.duration;
            remaining = preset.duration;
        }
    }

    void LateUpdate()
    {
        if (shakeTarget == null || remaining <= 0f) return;

        float dt = Time.unscaledDeltaTime;
        remaining -= dt;
        noiseTime += dt * frequency;

        if (remaining <= 0f)
        {
            remaining = 0f;
            amplitude = 0f;
            shakeTarget.localPosition = restLocalPosition;
            return;
        }

        float damp = duration <= 0f ? 0f : remaining / duration;
        float power = amplitude * damp * damp;
        float ox = (Mathf.PerlinNoise(seed, noiseTime) * 2f - 1f) * power;
        float oy = (Mathf.PerlinNoise(seed + 17f, noiseTime) * 2f - 1f) * power;
        shakeTarget.localPosition = restLocalPosition + new Vector3(ox, oy, 0f);
    }

    (float amplitude, float duration) Resolve(CameraShakeLevel level)
    {
        switch (level)
        {
            case CameraShakeLevel.Medium:
                return (amplitudeMedium, durationMedium);
            case CameraShakeLevel.Large:
                return (amplitudeLarge, durationLarge);
            default:
                return (amplitudeSmall, durationSmall);
        }
    }

    void Restore()
    {
        if (shakeTarget == null) return;
        shakeTarget.localPosition = restLocalPosition;
    }
}
