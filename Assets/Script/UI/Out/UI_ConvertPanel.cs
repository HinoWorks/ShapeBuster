using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI_Convert の参照を ConvertMine につなぐ。
/// 払える額のボタンは変換中でも押せる。速度強化は変換中は止める。
/// </summary>
public class UI_ConvertPanel : MonoBehaviour
{
    [SerializeField] TMP_Text rateFrom;
    [SerializeField] TMP_Text rateTo;
    [SerializeField] TMP_Text depositedLabel;
    [SerializeField] TMP_Text returnsLabel;
    [SerializeField] TMP_Text timeLabel;
    [SerializeField] TMP_Text speedLabel;
    [SerializeField] TMP_Text nextSpeedLabel;
    [SerializeField] HButton[] depositButtons;
    [SerializeField] TMP_Text[] depositLabels;
    [SerializeField] HButton upgradeButton;
    [Tooltip("速度を上げる値段（EE の数）")]
    [SerializeField] TMP_Text upgradeCostLabel;
    [Tooltip("速度を上げる値段の資源アイコン。SO_ResourceData の EE の絵を入れる")]
    [SerializeField] Image upgradeCostIcon;

    CanvasGroup group;
    bool wired;

    void Awake()
    {
        EnsureGroup();
        Wire();
        ApplyVisible(false);
    }

    public void Setup()
    {
        RefreshUnlock();
        Show(false);
    }

    public void RefreshUnlock()
    {
        if (!SkillTreeBonus.ConvertUnlocked)
            Show(false);
    }

    public void Show(bool show)
    {
        if (!gameObject.activeSelf)
            gameObject.SetActive(true);
        ApplyVisible(show && SkillTreeBonus.ConvertUnlocked);
    }

    void ApplyVisible(bool visible)
    {
        EnsureGroup();
        group.alpha = visible ? 1f : 0f;
        group.interactable = visible;
        group.blocksRaycasts = visible;
        if (visible)
            Refresh();
    }

    void EnsureGroup()
    {
        if (group == null)
            group = GetComponent<CanvasGroup>();
        if (group == null)
            group = gameObject.AddComponent<CanvasGroup>();
    }

    void Update()
    {
        if (!SkillTreeBonus.ConvertUnlocked) return;
        ConvertMine.Tick(Time.unscaledDeltaTime);
        if (group != null && group.alpha > 0f)
            Refresh();
    }

    void OnApplicationPause(bool paused)
    {
        if (paused)
            ConvertMine.Flush();
    }

    void OnApplicationQuit()
    {
        ConvertMine.Flush();
    }

    void Wire()
    {
        if (wired) return;
        wired = true;

        if (depositButtons != null)
        {
            for (int i = 0; i < depositButtons.Length; i++)
            {
                int index = i;
                var button = depositButtons[i];
                if (button == null) continue;
                if (depositLabels != null && index < depositLabels.Length && depositLabels[index] != null
                    && index < ConvertMine.DepositAmounts.Length)
                    depositLabels[index].text = AmountLabel(ConvertMine.DepositAmounts[index]);
                // 預けられたかどうかで音を変えるので、ボタンの押した音は鳴らさない。
                button.ClickSE = SEType.None;
                button.onClick.AddListener(() => OnDeposit(index));
            }
        }

        if (upgradeButton != null)
        {
            upgradeButton.ClickSE = SEType.None;
            upgradeButton.onClick.AddListener(OnUpgrade);
        }
    }

    void OnDeposit(int index)
    {
        if (index < 0 || index >= ConvertMine.DepositAmounts.Length) return;
        if (ConvertMine.TryDeposit(ConvertMine.DepositAmounts[index]))
        {
            SoundManager.SE(SEType.UI_Purchase);
            Refresh();
        }
        else
            SoundManager.SE(SEType.UI_Error);
    }

    void OnUpgrade()
    {
        if (ConvertMine.TryUpgradeSpeed())
        {
            SoundManager.SE(SEType.UI_Purchase);
            Refresh();
        }
        else
            SoundManager.SE(SEType.UI_Error);
    }

    void Refresh()
    {
        if (rateFrom != null) rateFrom.text = "1";
        if (rateTo != null) rateTo.text = ConvertMine.RateText();
        if (depositedLabel != null) depositedLabel.text = ConvertMine.Deposited.ToString();
        if (returnsLabel != null) returnsLabel.text = ConvertMine.OwnedText();
        if (timeLabel != null) timeLabel.text = ConvertMine.TimeText();
        if (speedLabel != null) speedLabel.text = Loc.Format("ui.convert.perSec", ConvertMine.Speed);
        if (nextSpeedLabel != null) nextSpeedLabel.text = Loc.Format("ui.convert.perSec", NextSpeed());

        var owned = SaveLoader.Inst != null ? SaveLoader.Inst.Get_ResourceCount(ResourceType.AA) : 0;
        if (owned < 0) owned = 0;
        if (depositButtons != null)
        {
            for (int i = 0; i < depositButtons.Length; i++)
            {
                if (depositButtons[i] == null || i >= ConvertMine.DepositAmounts.Length) continue;
                depositButtons[i].Set_Interactable(owned >= ConvertMine.DepositAmounts[i]);
            }
        }

        if (upgradeButton != null)
            upgradeButton.Set_Interactable(ConvertMine.CanUpgradeSpeed());
        if (upgradeCostLabel != null)
            upgradeCostLabel.text = ConvertMine.SpeedLevel >= ConvertMine.MaxLevel ? "MAX" : ConvertMine.UpgradeCost.ToString("N0");
        if (upgradeCostIcon != null)
        {
            var data = SOLoader.ResourceData != null ? SOLoader.ResourceData.GetResourceData(ConvertMine.UpgradeResource) : null;
            if (data != null && data.icon != null)
                upgradeCostIcon.sprite = data.icon;
        }
    }

    static int NextSpeed()
    {
        int level = ConvertMine.SpeedLevel;
        if (level >= ConvertMine.MaxLevel)
            return ConvertMine.Speed;
        int index = Mathf.Clamp(level + 1, 0, ConvertMine.SpeedByLevel.Length - 1);
        return ConvertMine.SpeedByLevel[index];
    }

    static string AmountLabel(int amount)
    {
        if (amount >= 1000000) return "1m";
        if (amount >= 1000) return $"{amount / 1000}k";
        return amount.ToString();
    }
}
