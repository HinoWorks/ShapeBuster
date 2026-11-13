using UnityEngine;

/// <summary>
/// フィールドの壁の外に置く横ビームの発射体。高さは BlockSession が動かす。
/// </summary>
public class BlockRowEmitter : MonoBehaviour
{
    Transform muzzle;

    public Vector2 Muzzle
    {
        get
        {
            var tip = Tip();
            if (tip != null)
                return tip.position;
            return (Vector2)transform.position + (Vector2)transform.right * 0.55f;
        }
    }

    Transform Tip()
    {
        if (muzzle == null)
            muzzle = transform.Find("Muzzle");
        return muzzle;
    }
}
