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


    void Awake()
    {
        icon.sprite = SOLoader.ResourceData.GetResourceData(resourceType).icon;
        tmp_resourceCount.text = "0";
    }


    /// <summary>
    /// リザルトなど、確定した所持数をそのまま出す。
    /// </summary>
    public void SetCount(BigInteger count)
    {
        tmp_resourceCount.text = StaticManager.Get_BigintegerToString(count);
    }


}
