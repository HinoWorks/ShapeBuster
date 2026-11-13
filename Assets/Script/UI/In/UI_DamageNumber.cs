using UnityEngine;
using TMPro;

/// <summary>
/// ダメージ数字1つ。見た目は子の TextMeshProUGUI を調整する。
/// </summary>
public class UI_DamageNumber : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI label;
    [Tooltip("表示してから消えるまでの秒数")]
    [SerializeField] float duration = 0.48f;
    [Tooltip("消えるまでに上へ動く距離。キャンバスのローカル単位")]
    [SerializeField] float rise = 42f;
    [Tooltip("クリティカルのときの拡大率。通常は 1")]
    [SerializeField] float critScale = 1.65f;

    RectTransform rect;
    UI_DamageNumberPool pool;
    Vector2 origin;
    Color tint = Color.white;
    float age;
    bool playing;

    public bool InPool { get; private set; }

    public void Bind(UI_DamageNumberPool owner)
    {
        pool = owner;
        rect = (RectTransform)transform;
        if (label == null)
            label = GetComponentInChildren<TextMeshProUGUI>(true);
        if (label != null)
            label.raycastTarget = false;
    }

    public void Play(int damage, Color color, bool critical, Vector2 anchored)
    {
        if (label == null)
            label = GetComponentInChildren<TextMeshProUGUI>(true);
        if (label != null)
        {
            label.text = damage.ToString();
            label.color = color;
            label.raycastTarget = false;
        }

        tint = color;
        origin = anchored;
        if (rect == null)
            rect = (RectTransform)transform;
        rect.anchoredPosition = anchored;
        float scale = critical ? Mathf.Max(0.01f, critScale) : 1f;
        rect.localScale = new Vector3(scale, scale, 1f);
        age = 0f;
        playing = true;
        InPool = false;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
    }

    public void StopAndHide()
    {
        playing = false;
        InPool = true;
        gameObject.SetActive(false);
    }

    void Update()
    {
        if (!playing) return;

        float life = Mathf.Max(0.05f, duration);
        age += Time.deltaTime;
        float t = Mathf.Clamp01(age / life);
        rect.anchoredPosition = origin + Vector2.up * (rise * t);
        if (label != null)
        {
            var color = tint;
            color.a *= t < 0.55f ? 1f : 1f - (t - 0.55f) / 0.45f;
            label.color = color;
        }
        if (t < 1f) return;

        playing = false;
        if (pool != null)
            pool.Return(this);
        else
            StopAndHide();
    }
}
