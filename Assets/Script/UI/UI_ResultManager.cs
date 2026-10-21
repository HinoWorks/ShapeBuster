using System.Numerics;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// リザルト画面。UI_result 上のタイトル、進捗、獲得リソースへ記録を出す。
/// </summary>
public class UI_ResultManager : MonoBehaviour
{
    [SerializeField] Color clearColor;
    [SerializeField] Color endColor;

    [Space(7)]
    [SerializeField] Image background;
    [SerializeField] TextMeshProUGUI tmp_title;
    [SerializeField] Image progressFill;
    [SerializeField] TextMeshProUGUI tmp_progress;
    [SerializeField] UI_ResourceCounter[] resourceCounters;

    void Awake()
    {
        gameObject.SetActive(false);
    }

    void OnResultOpen(bool isOpen)
    {
        gameObject.SetActive(isOpen);
        if (!isOpen) return;

        var record = IngameRecordManager.Inst != null ? IngameRecordManager.Inst.Record : null;
        Show(record);
    }

    public void Show(IngameRecordData record)
    {
        bool cleared = record != null && record.isCleared;
        float stageTime = record != null ? record.stageTime : 0f;

        if (tmp_title != null)
            tmp_title.text = cleared ? "Stage Clear" : "Session End";

        if (background != null)
            background.color = cleared ? clearColor : endColor;

        float ratio = ProgressRatio(stageTime);
        if (progressFill != null)
            progressFill.fillAmount = ratio;
        if (tmp_progress != null)
            tmp_progress.text = $"{Mathf.RoundToInt(ratio * 100f)}%";

        if (resourceCounters == null) return;
        for (int i = 0; i < resourceCounters.Length; i++)
        {
            var counter = resourceCounters[i];
            if (counter == null) continue;
            var count = record != null ? record.GetResourceCount(counter.ResourceType) : BigInteger.Zero;
            counter.SetCount(count);
        }
    }

    static float ProgressRatio(float stageTime)
    {
        var stage = StageTimeManager.Inst;
        if (stage == null || stage.stageMaxTime <= 0f) return 0f;
        return Mathf.Clamp01(stageTime / stage.stageMaxTime);
    }
}
