using UnityEngine;
using DG.Tweening;
using System.Numerics;
using TMPro;
using UniRx;
using UnityEngine.UI;


public class UI_ResourceCounter : MonoBehaviour
{
    [SerializeField] ResourceType resourceType;
    public ResourceType ResourceType => resourceType;

    [Space(10)]
    [SerializeField] Image icon;
    [SerializeField] TextMeshProUGUI tmp_resourceCount;
    private float currentResourceFloat;

    public Transform targetPosition => icon.transform;


    public void AwakeCall_Ingame()
    {
        IngameRecordManager.ResourceMod_Ingame.Subscribe(Set_ResourceMod).AddTo(this);
        icon.sprite = SOLoader.ResourceData.GetResourceData(resourceType).icon;
        ResourceUnlockCheck();
    }

    public void AwakeCall_OutGame()
    {
        GameEvent.UI.ResourceMod.Subscribe(Set_ResourceMod).AddTo(this);
        icon.sprite = SOLoader.ResourceData.GetResourceData(resourceType).icon;
        ResourceUnlockCheck();
    }



    /// <summary>
    /// リザルトなど、確定した所持数をそのまま出す。
    /// </summary>
    public void SetCount(BigInteger count)
    {
        var resourceData = SOLoader.ResourceData;
        if (icon != null && resourceData != null && resourceData.resourceDatas != null)
        {
            var data = resourceData.GetResourceData(resourceType);
            if (data != null && data.icon != null)
                icon.sprite = data.icon;
        }

        if (tmp_resourceCount == null) return;
        currentResourceFloat = 0f;
        tmp_resourceCount.text = StaticManager.Get_BigintegerToString(count);
    }

    /// <summary>
    /// 主にインゲームでのカウンター初期化用
    /// </summary>
    public void Set_Init() // TODO here ここをつなぐ
    {
        currentResourceFloat = 0;
        tmp_resourceCount.text = "0";
        ResourceUnlockCheck();
    }
    private bool ResourceUnlockCheck()
    {
        /*
        var isResourceUnlock = GameParamManager.blockChangeRateParam.IsBlockTypeUnlock(resourceType);
        this.gameObject.SetActive(isResourceUnlock);
        return isResourceUnlock;
*/
        return true;
    }

    /// <summary>
    /// カウンター更新チェック
    /// </summary>
    public void CounterUpdateCheck()
    {
        /*
        var isResourceUnlock = ResourceUnlockCheck();
        if (!isResourceUnlock) return;

        //var haveResource = SaveLoader.Inst.Check_ResourceKeyExists(resourceType);
        //if (!haveResource) return;

        if (!this.gameObject.activeSelf) this.gameObject.SetActive(true);
        var resourceCount = SaveLoader.Inst.Get_ResourceCount(resourceType);
        Set_ResourceMod((resourceType, resourceCount));
        */
    }
    private void Set_ResourceMod((ResourceType, BigInteger) _resourceMod)
    {
        if (_resourceMod.Item1 != resourceType) return;
        if (!this.gameObject.activeSelf) this.gameObject.SetActive(true);

        var modCoin = StaticManager.Get_BigintegerToUnit(_resourceMod.Item2);
        tmp_resourceCount.transform.DOScale(1.05f, 0.05f).SetEase(Ease.OutSine);
        DOTween.To(() => currentResourceFloat, x => currentResourceFloat = x, modCoin.num, 0.2f)
            .OnUpdate(() =>
            {
                if (modCoin.unit == "")
                {
                    tmp_resourceCount.text = $"{currentResourceFloat.ToString("F0")} {modCoin.unit}";
                }
                else
                {
                    tmp_resourceCount.text = $"{currentResourceFloat.ToString("F1")} {modCoin.unit}";
                }
            }).OnComplete(() =>
        {
            currentResourceFloat = modCoin.num;
            tmp_resourceCount.transform.DOScale(1f, 0.05f).SetEase(Ease.OutSine);
        });
    }

}
