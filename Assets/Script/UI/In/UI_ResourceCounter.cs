using UnityEngine;
using DG.Tweening;
using System.Numerics;
using TMPro;
using UniRx;
using UnityEngine.EventSystems;
using UnityEngine.UI;


/// <summary>
/// 資源 1 種類の所持数の表示。アウトゲームでは、マウスを乗せると黄色の枠を付け、説明（UI_ResourceTooltip）を出す。
/// </summary>
public class UI_ResourceCounter : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] ResourceType resourceType;
    public ResourceType ResourceType => resourceType;

    [Space(10)]
    [SerializeField] Image icon;
    [SerializeField] TextMeshProUGUI tmp_resourceCount;
    [Tooltip("マウスが乗ったときに出す黄色の枠（下地の後ろに少し大きく敷いた板）。アウトゲームだけで使う")]
    [SerializeField] GameObject obj_hoverOutline;
    private float currentResourceFloat;
    bool showOwnedConvert;
    bool showTooltip;

    public Transform targetPosition => icon.transform;


    public void AwakeCall_Ingame()
    {
        showOwnedConvert = false;
        IngameRecordManager.ResourceMod_Ingame.Subscribe(Set_ResourceMod).AddTo(this);
        icon.sprite = SOLoader.ResourceData.GetResourceData(resourceType).icon;
        ResourceUnlockCheck();
    }

    public void AwakeCall_OutGame()
    {
        showOwnedConvert = true;
        showTooltip = true;
        // 枠のどこにマウスを乗せても説明が出るように、アイコンと下地もマウスに反応させる。
        foreach (var image in GetComponentsInChildren<Image>(true))
        {
            if (obj_hoverOutline != null && image.transform.IsChildOf(obj_hoverOutline.transform)) continue;
            image.raycastTarget = true;
        }
        SetHoverOutline(false);
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
        var isResourceUnlock = SaveLoader.Inst.Check_ResourceKeyExists(resourceType);
        // 初めての変換では、1 D がまるごと書かれるまで DD のキーがない。変換が始まっていれば端数でも出す。
        if (!isResourceUnlock && showOwnedConvert && resourceType == ResourceType.DD)
            isResourceUnlock = ConvertMine.HasProgress;
        this.gameObject.SetActive(isResourceUnlock);
        return isResourceUnlock;
    }

    /// <summary>
    /// カウンター更新チェック
    /// </summary>
    public void CounterUpdateCheck()
    {
        var isResourceUnlock = ResourceUnlockCheck();
        if (!isResourceUnlock) return;

        var resourceCount = SaveLoader.Inst.Get_ResourceCount(resourceType);
        Set_ResourceMod((resourceType, resourceCount));
    }
    void Update()
    {
        if (!showOwnedConvert || resourceType != ResourceType.DD || tmp_resourceCount == null) return;
        if (!gameObject.activeInHierarchy) return;
        tmp_resourceCount.text = ConvertMine.OwnedText();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!showTooltip) return;
        UI_ResourceTooltip.Show(this);
        SetHoverOutline(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        UI_ResourceTooltip.Hide(this);
        SetHoverOutline(false);
    }

    void OnDisable()
    {
        UI_ResourceTooltip.Hide(this);
        SetHoverOutline(false);
    }

    void SetHoverOutline(bool on)
    {
        if (obj_hoverOutline != null && obj_hoverOutline.activeSelf != on)
            obj_hoverOutline.SetActive(on);
    }

    private void Set_ResourceMod((ResourceType, BigInteger) _resourceMod)
    {
        if (_resourceMod.Item1 != resourceType) return;
        if (!this.gameObject.activeSelf) this.gameObject.SetActive(true);

        if (showOwnedConvert && resourceType == ResourceType.DD)
        {
            tmp_resourceCount.text = ConvertMine.OwnedText();
            return;
        }

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
