using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 引っ張った逆方向へ、反射を含んだ予測線と先端を出す。太さや見た目はプレハブで調整する。
/// </summary>
public class BlockAimGuide : MonoBehaviour
{
    [SerializeField] Transform pullBar;
    [SerializeField] Transform shotBar;
    [SerializeField] Transform shotTip;
    [SerializeField] float maxLength = 36f;

    readonly List<Transform> extraBars = new();
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

        ShowPull(origin, pull);
        int segments = path == null ? 0 : path.Count - 1;
        for (int i = 0; i < segments; i++)
            ShowBar(Bar(i), path[i], path[i + 1], shotThickness);

        if (segments == 0)
            SetActive(shotBar, false);
        for (int i = Mathf.Max(0, segments - 1); i < extraBars.Count; i++)
            SetActive(extraBars[i], false);

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
        for (int i = 0; i < extraBars.Count; i++)
            SetActive(extraBars[i], false);
    }

    Transform Bar(int index)
    {
        if (index <= 0 || shotBar == null)
            return shotBar;

        while (extraBars.Count < index)
        {
            var copy = Instantiate(shotBar, shotBar.parent);
            copy.gameObject.SetActive(false);
            extraBars.Add(copy);
        }
        return extraBars[index - 1];
    }

    void ShowPull(Vector2 origin, Vector2 pull)
    {
        if (pullBar == null) return;

        pullBar.gameObject.SetActive(true);
        pullBar.position = new Vector3(origin.x, origin.y, 0f);
        pullBar.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(pull.y, pull.x) * Mathf.Rad2Deg);
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
