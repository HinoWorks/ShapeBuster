using TMPro;
using UnityEngine;

/// <summary>
/// 装填中の弾数と、所持している弾の合計を出す。
/// </summary>
public class UI_HUD_MagazineInfo : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI tmp_magazine;
    [SerializeField] TextMeshProUGUI tmp_bulletTotal;

    public void SetBullet(int ammoInMag, int magazineCapacity, int totalBullets)
    {
        if (tmp_magazine != null)
            tmp_magazine.text = $"{ammoInMag}/{magazineCapacity}";
        if (tmp_bulletTotal != null)
            tmp_bulletTotal.text = totalBullets.ToString();
    }
}
