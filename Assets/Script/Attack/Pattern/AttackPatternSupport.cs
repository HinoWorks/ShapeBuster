using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 攻撃演出の生成と返却。未使用はスタックに置き、使い回す。
/// </summary>
public class AttackObjectPool<T> where T : Component
{
    readonly T prefab;
    readonly Transform parent;
    readonly Stack<T> available = new();
    readonly List<T> active = new();

    public AttackObjectPool(T prefab, Transform parent, int prewarm)
    {
        this.prefab = prefab;
        this.parent = parent;
        int count = Mathf.Max(0, prewarm);
        for (int i = 0; i < count; i++)
            available.Push(Create());
    }

    public T Rent()
    {
        while (available.Count > 0)
        {
            var pooled = available.Pop();
            if (pooled == null) continue;
            active.Add(pooled);
            return pooled;
        }

        var created = Create();
        active.Add(created);
        return created;
    }

    public void Return(T item)
    {
        if (item == null || !active.Remove(item)) return;

        item.gameObject.SetActive(false);
        item.transform.SetParent(parent, false);
        available.Push(item);
    }

    public void ReleaseAll()
    {
        for (int i = active.Count - 1; i >= 0; i--)
        {
            var item = active[i];
            if (item == null) continue;
            item.gameObject.SetActive(false);
            item.transform.SetParent(parent, false);
            available.Push(item);
        }
        active.Clear();
    }

    T Create()
    {
        var item = UnityEngine.Object.Instantiate(prefab, parent);
        item.gameObject.SetActive(false);
        return item;
    }
}

public static class AttackPlayfield
{
    public static bool TryGetRect(out Rect rect)
    {
        var camera = Camera.main;
        if (camera == null || !camera.orthographic)
        {
            rect = default;
            return false;
        }

        float halfHeight = camera.orthographicSize;
        float halfWidth = halfHeight * camera.aspect;
        var center = camera.transform.position;
        rect = Rect.MinMaxRect(
            center.x - halfWidth,
            center.y - halfHeight,
            center.x + halfWidth,
            center.y + halfHeight);
        return true;
    }
}

public static class AttackHitQuery
{
    const int BufferSize = 48;
    static readonly Collider2D[] buffer = new Collider2D[BufferSize];
    static ContactFilter2D filter;
    static bool ready;

    public static void Circle(Vector2 center, float radius, List<IDamagable> results)
    {
        Ensure();
        int count = Physics2D.OverlapCircle(center, radius, filter, buffer);
        Copy(count, results);
    }

    public static void Box(Vector2 center, Vector2 size, List<IDamagable> results)
    {
        Box(center, size, 0f, results);
    }

    public static void Box(Vector2 center, Vector2 size, float angle, List<IDamagable> results)
    {
        Ensure();
        int count = Physics2D.OverlapBox(center, size, angle, filter, buffer);
        Copy(count, results);
    }

    /// <summary>
    /// 45°回転した正方形。半径は中心から上下左右の頂点までの距離。
    /// </summary>
    public static void Diamond(Vector2 center, float radius, List<IDamagable> results)
    {
        float side = radius * Mathf.Sqrt(2f);
        Box(center, new Vector2(side, side), 45f, results);
    }

    public static void DamageAll(List<IDamagable> targets, int damage, int attackIndex = -1)
    {
        for (int i = 0; i < targets.Count; i++)
        {
            var target = targets[i];
            if (target == null || !target.isAlive) continue;
            target.Damage(damage, attackIndex: attackIndex);
        }
    }

    static void Ensure()
    {
        if (ready) return;
        filter = ContactFilter2D.noFilter;
        filter.SetLayerMask(LayerMask.GetMask("Enemy"));
        filter.useLayerMask = true;
        filter.useTriggers = true;
        ready = true;
    }

    static void Copy(int count, List<IDamagable> results)
    {
        results.Clear();
        for (int i = 0; i < count; i++)
        {
            var collider = buffer[i];
            buffer[i] = null;
            if (collider == null) continue;
            if (!collider.TryGetComponent(out IDamagable target)) continue;
            if (!target.isAlive) continue;
            if (results.Contains(target)) continue;
            results.Add(target);
        }
    }
}

public static class AttackVisual
{
    static Sprite white;
    static Sprite circle;
    static Sprite diamond;

    public static Sprite White => white != null ? white : white = BuildWhite();
    public static Sprite Circle => circle != null ? circle : circle = BuildCircle(64);
    public static Sprite Diamond => diamond != null ? diamond : diamond = BuildDiamond(64);

    public static Sprite Resolve(Sprite assigned, bool circle)
    {
        if (assigned != null) return assigned;
        return circle ? Circle : White;
    }

    /// <summary>スプライトの原寸を、指定したワールドサイズへ合わせるスケール。</summary>
    public static Vector3 ScaleTo(Sprite sprite, Vector2 worldSize)
    {
        var bounds = sprite != null ? sprite.bounds.size : Vector3.one;
        float x = bounds.x > 0.0001f ? worldSize.x / bounds.x : worldSize.x;
        float y = bounds.y > 0.0001f ? worldSize.y / bounds.y : worldSize.y;
        return new Vector3(x, y, 1f);
    }

    static Sprite BuildWhite()
    {
        var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        texture.hideFlags = HideFlags.HideAndDontSave;
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        var pixels = new Color[16];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = Color.white;
        texture.SetPixels(pixels);
        texture.Apply();
        return CreateSprite(texture, 4f);
    }

    static Sprite BuildCircle(int size)
    {
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.hideFlags = HideFlags.HideAndDontSave;
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;

        float radius = (size - 1) * 0.5f;
        var center = new Vector2(radius, radius);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center) / radius;
                float fill = dist <= 1f ? 0.45f : 0f;
                float ring = Mathf.Clamp01(1f - Mathf.Abs(dist - 0.9f) / 0.08f);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Max(fill, ring)));
            }
        }
        texture.Apply();
        return CreateSprite(texture, size);
    }

    static Sprite BuildDiamond(int size)
    {
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.hideFlags = HideFlags.HideAndDontSave;
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;

        float radius = (size - 1) * 0.5f;
        var center = new Vector2(radius, radius);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                var delta = new Vector2(x, y) - center;
                float dist = (Mathf.Abs(delta.x) + Mathf.Abs(delta.y)) / radius;
                float fill = dist <= 1f ? 0.45f : 0f;
                float ring = Mathf.Clamp01(1f - Mathf.Abs(dist - 0.9f) / 0.08f);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Max(fill, ring)));
            }
        }
        texture.Apply();
        return CreateSprite(texture, size);
    }

    static Sprite CreateSprite(Texture2D texture, float pixelsPerUnit)
    {
        var sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            pixelsPerUnit);
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }
}
