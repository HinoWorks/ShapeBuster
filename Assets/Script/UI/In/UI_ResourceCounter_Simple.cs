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
        tmp_resourceCount.text = StaticManager.Get_BigintegerToString(count);
        tmp_resourceCount.color = isEnabled ? color_enough : color_notEnough;
    }


}
