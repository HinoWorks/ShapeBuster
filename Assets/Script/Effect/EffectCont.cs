using UnityEngine;

/// <summary>
/// プールから借りたエフェクト。再生後、表示時間を過ぎたら EffectManager へ返す。
/// </summary>
public class EffectCont : MonoBehaviour
{
    [SerializeField] float lifeTime = 0.75f;

    EffectManager owner;
    ParticleSystem particles;
    float remain;
    bool playing;

    public EffectType Type { get; private set; }
    public bool InPool { get; private set; }

    void Awake()
    {
        particles = GetComponent<ParticleSystem>();
    }

    public void Bind(EffectManager manager, EffectType type)
    {
        owner = manager;
        Type = type;
    }

    public void MarkInPool()
    {
        InPool = true;
        playing = false;
    }

    public void MarkRented()
    {
        InPool = false;
    }

    public void Play(Vector3 worldPosition)
    {
        transform.position = worldPosition;
        gameObject.SetActive(true);

        if (particles != null)
        {
            particles.Clear(true);
            particles.Play(true);
        }

        remain = lifeTime > 0f ? lifeTime : 1f;
        playing = true;
    }

    public void StopAndHide()
    {
        playing = false;
        if (particles != null)
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        gameObject.SetActive(false);
    }

    void Update()
    {
        if (!playing) return;

        remain -= Time.deltaTime;
        if (remain > 0f) return;

        playing = false;
        if (owner != null)
            owner.Return(this);
        else
            Destroy(gameObject);
    }
}
