using UnityEngine;

/// <summary>
/// フィールドの隅に置く固定砲台。向きだけを持ち、弾は BlockSession が撃つ。
/// </summary>
public class BlockTurret : MonoBehaviour
{
    Transform muzzle;

    public Vector2 Muzzle
    {
        get
        {
            var tip = Tip();
            if (tip != null)
                return tip.position;
            return (Vector2)transform.position + (Vector2)transform.up * 0.7f;
        }
    }

    public void Aim(Vector2 target)
    {
        Vector2 dir = target - (Vector2)transform.position;
        if (dir.sqrMagnitude < 0.0001f) return;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    Transform Tip()
    {
        if (muzzle == null)
            muzzle = transform.Find("Muzzle");
        return muzzle;
    }
}
