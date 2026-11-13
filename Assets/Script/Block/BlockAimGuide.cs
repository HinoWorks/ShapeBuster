using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// カーソル方向へ、反射を含んだ点線と先端を出す。太さや見た目はプレハブで調整する。
/// </summary>
public class BlockAimGuide : MonoBehaviour
{
    [SerializeField] Transform pullBar;
    [SerializeField] Transform shotBar;
    [SerializeField] Transform shotTip;
    [SerializeField] float maxLength = 36f;
    [SerializeField] float dotLength = 0.08f;
    [SerializeField] float dotGap = 0.1f;

    readonly List<Transform> dots = new();
    float shotThickness = 0.07f;
    Vector3 tipScale = Vector3.one * 0.18f;

    public float PredictionLength => Mathf.Max(4f, maxLength);

    void Awake()
    {
        if (shotBar != null)
            shotThickness = Mathf.Abs(shotBar.localScale.y);
        if (shotTip != null)
            tipScale = shotTip.localScale;
        EnsureSprite(shotBar, false);
        EnsureSprite(shotTip, true);
        Hide();
    }

    public void Show(Vector2 origin, Vector2 pull, List<Vector2> path)
    {
        if (pull.sqrMagnitude < 0.0025f)
        {
            Hide();
            return;
        }

        SetActive(pullBar, false);
        int segments = path == null ? 0 : path.Count - 1;
        int used = 0;
        for (int i = 0; i < segments; i++)
            used = ShowDots(path[i], path[i + 1], used);

        if (used == 0)
            SetActive(shotBar, false);
        for (int i = used; i < dots.Count; i++)
            SetActive(dots[i], false);

        if (shotTip == null || segments <= 0)
        {
            SetActive(shotTip, false);
            return;
        }

        var end = path[path.Count - 1];
        shotTip.gameObject.SetActive(true);
        shotTip.position = new Vector3(end.x, end.y, 0f);
        shotTip.localScale = tipScale;
    }

    public void Hide()
    {
        SetActive(pullBar, false);
        SetActive(shotBar, false);
        SetActive(shotTip, false);
        for (int i = 0; i < dots.Count; i++)
            SetActive(dots[i], false);
    }

    int ShowDots(Vector2 from, Vector2 to, int used)
    {
        Vector2 delta = to - from;
        float length = delta.magnitude;
        if (length < 0.02f) return used;

        float mark = Mathf.Max(0.02f, dotLength);
        float gap = Mathf.Max(0.02f, dotGap);
        float step = mark + gap;
        Vector2 dir = delta / length;
        float cursor = 0f;
        while (cursor < length - 0.01f)
        {
            float dash = Mathf.Min(mark, length - cursor);
            if (dash < 0.02f) break;
            ShowBar(Dot(used), from + dir * cursor, from + dir * (cursor + dash), shotThickness);
            used++;
            cursor += step;
        }
        return used;
    }

    Transform Dot(int index)
    {
        if (shotBar == null) return null;
        if (dots.Count == 0)
            dots.Add(shotBar);

        while (dots.Count <= index)
        {
            var copy = Instantiate(shotBar, shotBar.parent);
            copy.gameObject.SetActive(false);
            dots.Add(copy);
        }
        return dots[index];
    }

    static void EnsureSprite(Transform target, bool circle)
    {
        if (target == null) return;
        var sprite = target.GetComponent<SpriteRenderer>();
        if (sprite == null || sprite.sprite != null) return;
        sprite.sprite = circle ? BlockSession.CircleSprite : BlockSession.SquareSprite;
    }

    static void ShowBar(Transform bar, Vector2 from, Vector2 to, float thickness)
    {
        if (bar == null) return;

        Vector2 delta = to - from;
        float length = delta.magnitude;
        if (length < 0.02f)
        {
            bar.gameObject.SetActive(false);
            return;
        }

        bar.gameObject.SetActive(true);
        bar.position = new Vector3((from.x + to.x) * 0.5f, (from.y + to.y) * 0.5f, 0f);
        bar.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        bar.localScale = new Vector3(length, thickness, 1f);
    }

    static void SetActive(Transform target, bool active)
    {
        if (target != null)
            target.gameObject.SetActive(active);
    }
}
