using UnityEngine;
using System.Numerics;
using TMPro;
using UnityEngine.UI;


public class UI_ResourceCounter_Simple : MonoBehaviour
{
    [SerializeField] ResourceType resourceType;
    public ResourceType ResourceType => resourceType;

    [Space(10)]
    [SerializeField] Image icon;
    [SerializeField] TextMeshProUGUI tmp_resourceCount;
    [SerializeField] Color color_enough;
    [SerializeField] Color color_notEnough;

    void Awake()
    {
        icon.sprite = SOLoader.ResourceData.GetResourceData(resourceType).icon;
        tmp_resourceCount.text = "0";
    }


    /// <summary>
    /// リザルトなど、確定した所持数をそのまま出す。
    /// </summary>
    public void SetCount(BigInteger count, bool isEnabled = true)
    {
        if (icon != null) icon.enabled = true;
        tmp_resourceCount.text = StaticManager.Get_BigintegerToString(count);
        tmp_resourceCount.color = isEnabled ? color_enough : color_notEnough;
    }

    /// <summary>まだ入手したことのない資源。種類と個数の代わりに ?? を出す。</summary>
    public void SetUnknown()
    {
        if (icon != null) icon.enabled = false;
        tmp_resourceCount.text = "??";
        tmp_resourceCount.color = color_notEnough;
    }


}
