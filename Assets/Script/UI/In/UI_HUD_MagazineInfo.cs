using TMPro;
using UnityEngine;

/// <summary>
/// 装填中の弾数を出す。予備弾は無限なので、合計は出さない。
/// </summary>
public class UI_HUD_MagazineInfo : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI tmp_magazine;
    [SerializeField] TextMeshProUGUI tmp_bulletTotal;

    public void SetBullet(int ammoInMag, int magazineCapacity)
    {
        if (tmp_magazine != null)
            tmp_magazine.text = $"{ammoInMag}/{magazineCapacity}";
        if (tmp_bulletTotal != null)
            tmp_bulletTotal.gameObject.SetActive(false);
    }
}
