using System.Numerics;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// マイルストーン1件。mission に内容と進捗、reward に受け取るボーナスを出す。
/// claim は、いま表示している目標へ到達しているときだけ押せる。
/// </summary>
public class UI_MileStoneUnit : MonoBehaviour
{
    [SerializeField] int milestoneId;
    [SerializeField] TextMeshProUGUI tmp_description;
    [SerializeField] TextMeshProUGUI tmp_now;
    [SerializeField] TextMeshProUGUI tmp_target;
    [SerializeField] Image image_fill;
    [SerializeField] TextMeshProUGUI tmp_bonusName;
    [SerializeField] TextMeshProUGUI tmp_bonusValue;
    [SerializeField] HButton button_claim;

    CompositeDisposable disposable;

    public int MilestoneId => milestoneId;

    void Awake()
    {
        Resolve();
        if (button_claim != null)
            button_claim.onClick.AddListener(OnClickClaim);
    }

    void OnEnable()
    {
        disposable?.Dispose();
        disposable = new CompositeDisposable();
        MilestoneRuntime.ProgressChanged.Subscribe(OnProgress).AddTo(disposable);
        Refresh();
    }

    void OnDisable()
    {
        disposable?.Dispose();
        disposable = null;
    }

    public void SetMilestone(int id)
    {
        milestoneId = id;
        if (isActiveAndEnabled)
            Refresh();
    }

    public void Refresh()
    {
        if (!MilestoneRuntime.TryGetState(milestoneId, out var state))
        {
            ApplyEmpty();
            return;
        }

        Apply(state);
    }

    void OnProgress(MilestoneProgressChange change)
    {
        if (!MilestoneRuntime.TryGetState(milestoneId, out var state)) return;
        bool sameCounter = change.track == state.track && change.key == state.trackKey;
        bool claimed = change.track == MilestoneTrack.ClaimedTier && change.key == milestoneId;
        if (!sameCounter && !claimed) return;
        Apply(state);
    }

    void OnClickClaim()
    {
        if (!MilestoneRuntime.TryClaim(milestoneId)) return;
        Refresh();
    }

    void Apply(MilestoneState state)
    {
        tmp_description.SetText(state.title);
        tmp_now.SetText(FormatAmount(state.unit, state.progress));
        tmp_target.SetText(FormatAmount(state.unit, state.nextThreshold));
        image_fill.fillAmount = state.ratioToNext;
        tmp_bonusName.SetText(state.bonusLabel);
        tmp_bonusValue.SetText(state.FormatPerTier());
        button_claim.Set_Interactable(state.canClaim);
    }

    void ApplyEmpty()
    {
        tmp_description.SetText(string.Empty);
        tmp_now.SetText(string.Empty);
        tmp_target.SetText(string.Empty);
        image_fill.fillAmount = 0f;
        tmp_bonusName.SetText(string.Empty);
        tmp_bonusValue.SetText(string.Empty);
        button_claim.Set_Interactable(false);
    }

    void Resolve()
    {
        if (tmp_description == null)
            tmp_description = FindText("mission/tmp_description");
        if (tmp_now == null)
            tmp_now = FindText("mission/gauge/tmp_now");
        if (tmp_target == null)
            tmp_target = FindText("mission/gauge/tmp_target");
        if (image_fill == null)
        {
            var fill = transform.Find("mission/gauge/fill");
            if (fill != null)
                image_fill = fill.GetComponent<Image>();
        }
        if (tmp_bonusName == null)
            tmp_bonusName = FindText("reward/tmp_description");
        if (tmp_bonusValue == null)
            tmp_bonusValue = FindText("reward/tmp_description (1)");
        if (button_claim == null)
            button_claim = GetComponentInChildren<HButton>(true);
    }

    TextMeshProUGUI FindText(string path)
    {
        var child = transform.Find(path);
        return child != null ? child.GetComponent<TextMeshProUGUI>() : null;
    }

    static string FormatAmount(MilestoneUnit unit, BigInteger value)
    {
        if (value < 0) value = BigInteger.Zero;
        if (unit == MilestoneUnit.Seconds)
            return FormatSeconds(value);
        return StaticManager.Get_BigintegerToString(value);
    }

    static string FormatSeconds(BigInteger value)
    {
        if (value > int.MaxValue)
            return StaticManager.Get_BigintegerToString(value);

        int seconds = (int)value;
        if (seconds < 60) return seconds.ToString();

        int minutes = seconds / 60;
        int remain = seconds % 60;
        if (minutes < 60)
            return $"{minutes}:{remain:00}";

        int hours = minutes / 60;
        minutes %= 60;
        return $"{hours}:{minutes:00}:{remain:00}";
    }
}
