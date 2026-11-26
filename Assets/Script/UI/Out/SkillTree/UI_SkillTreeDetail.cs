using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class UI_SkillTreeDetail : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI tmp_skillName;
    [SerializeField] TextMeshProUGUI tmp_description;
    [SerializeField] TextMeshProUGUI tmp_level;
    [SerializeField] TextMeshProUGUI tmp_paramNow;
    [SerializeField] TextMeshProUGUI tmp_paramNext;
    [SerializeField] GameObject obj_param;
    [SerializeField] GameObject obj_paramArrow;
    [SerializeField] GameObject obj_complete;
    [SerializeField] GameObject obj_resourceRoot;
    [SerializeField] UI_ResourceCounter_Simple[] ui_resourceCounts;
    [Header("置き方")]
    [Tooltip("見えている枠。中身に合わせて高さが変わる。空なら子の main を使う")]
    [SerializeField] RectTransform box;
    [Tooltip("はみ出さないようにする範囲。空なら親（スキルツリーの画面）")]
    [SerializeField] RectTransform frame;
    [Tooltip("上にあって、詳細を重ねたくないもの（タブなど）。この下の端より上には出さない。空ならアウトゲームの header/headerButtons")]
    [SerializeField] RectTransform[] avoidTop;
    [Tooltip("ノードと詳細のすき間（キャンバスの単位）")]
    [SerializeField] float nodeGap = 12f;
    [Tooltip("枠の端から離す幅（キャンバスの単位）")]
    [SerializeField] float edgeMargin = 16f;
    [Tooltip("ズームにどれだけ大きさを合わせるか。0 で合わせない、1 でズームと同じ")]
    [SerializeField, Range(0f, 1f)] float zoomFollow = 0.3f;
    [SerializeField] float minScale = 0.85f;
    [SerializeField] float maxScale = 1.15f;


    private UI_SkillTreeUnit currentUnit;
    private List<ResourceCount> requredResources = new List<ResourceCount>();
    public List<ResourceCount> RequredResources => requredResources;
    public bool IsEnhanceReady => resourceReady && !isMaxLevel;
    private bool isMaxLevel = false;
    private bool resourceReady = false;

    private RectTransform rectTr;
    private RectTransform anchor;
    private readonly Vector3[] corners = new Vector3[4];


    public bool IsShowing(UI_SkillTreeUnit skillTreeUnit)
    {
        return currentUnit == skillTreeUnit && gameObject.activeSelf;
    }

    public void Hide()
    {
        SetData(null);
    }

    public void SetData(UI_SkillTreeUnit skillTreeUnit = null)
    {
        if (skillTreeUnit == null)
        {
            gameObject.SetActive(false);
            currentUnit = null;
            anchor = null;
            requredResources.Clear();
            resourceReady = false;
            isMaxLevel = true;
            return;
        }
        currentUnit = skillTreeUnit;
        SetData_Base(currentUnit.level);
    }

    /// <summary>
    /// ノードの上か下の、枠に収まるほうへ置く（上を優先）。どちらにも収まらなければ、広いほうに置いて枠の中へ寄せる。
    /// 大きさはズームに少しだけ合わせる。表示しているあいだは、ズームやスクロールに合わせて毎フレーム置き直す。
    /// </summary>
    public void SetPositionWithAutoFlip(RectTransform node)
    {
        EnsureCachedRefs();
        anchor = node;
        var target = Box();
        if (target != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(target);
        Place();
    }

    void LateUpdate()
    {
        if (anchor != null && currentUnit != null)
            Place();
    }

    void Place()
    {
        var parent = rectTr.parent as RectTransform;
        if (parent == null || anchor == null) return;

        // ノードの四隅を親の座標に直す。ズームはここに入る。
        anchor.GetWorldCorners(corners);
        Vector2 min = parent.InverseTransformPoint(corners[0]);
        Vector2 max = parent.InverseTransformPoint(corners[2]);
        float nodeHeight = max.y - min.y;
        float zoom = anchor.rect.height > 0.01f ? nodeHeight / anchor.rect.height : 1f;

        float scale = Mathf.Clamp(1f + (zoom - 1f) * zoomFollow, minScale, maxScale);
        rectTr.localScale = new Vector3(scale, scale, 1f);

        var target = Box();
        Vector2 size = (target != null ? target.rect.size : rectTr.rect.size) * scale;
        float half = size.y * 0.5f;
        float gap = nodeGap * scale;

        var area = (frame != null ? frame : parent).rect;
        if (frame != null && frame != parent)
            area = RectIn(parent, frame);
        // タブなど、上に重ねたくないものの下で止める。
        foreach (var block in AvoidTop())
        {
            if (block == null || !block.gameObject.activeInHierarchy) continue;
            area.yMax = Mathf.Min(area.yMax, RectIn(parent, block).yMin);
        }
        float top = area.yMax - edgeMargin;
        float bottom = area.yMin + edgeMargin;

        float above = max.y + gap + half;
        float below = min.y - gap - half;
        float y;
        if (above + half <= top)
            y = above;
        else if (below - half >= bottom)
            y = below;
        else
        {
            // どちらにも収まらない。広いほうに置いて、枠の中へ寄せる。
            y = (top - max.y) >= (min.y - bottom) ? above : below;
        }
        // ノードがタブの裏などにあると、下に出してもはみ出すので、最後に枠の中へ寄せる。
        y = top - bottom >= size.y ? Mathf.Clamp(y, bottom + half, top - half) : (top + bottom) * 0.5f;

        // 左右も枠の中へ寄せる。枠より広いときは真ん中。
        float halfWidth = size.x * 0.5f;
        float left = area.xMin + edgeMargin;
        float right = area.xMax - edgeMargin;
        float x = (min.x + max.x) * 0.5f;
        x = right - left >= size.x ? Mathf.Clamp(x, left + halfWidth, right - halfWidth) : (left + right) * 0.5f;
        rectTr.localPosition = new Vector3(x, y, rectTr.localPosition.z);
    }

    /// <summary>target の四隅を、parent の座標の矩形にする。</summary>
    Rect RectIn(RectTransform parent, RectTransform target)
    {
        target.GetWorldCorners(corners);
        Vector2 a = parent.InverseTransformPoint(corners[0]);
        Vector2 b = parent.InverseTransformPoint(corners[2]);
        return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
    }

    RectTransform[] AvoidTop()
    {
        if (avoidTop != null && avoidTop.Length > 0) return avoidTop;
        var outGame = GetComponentInParent<UI_OutGameManager>(true);
        var tabs = outGame != null ? outGame.transform.Find("header/headerButtons") as RectTransform : null;
        avoidTop = tabs != null ? new[] { tabs } : System.Array.Empty<RectTransform>();
        return avoidTop;
    }

    /// <summary>見えている枠。中身に合わせて高さが変わる。</summary>
    RectTransform Box()
    {
        if (box == null && rectTr != null)
            box = rectTr.Find("main") as RectTransform;
        return box;
    }

    private void SetData_Base(int currentLevel)
    {
        if (currentUnit == null || currentUnit.skillTree == null) return;

        var so = currentUnit.skillTree;
        int maxLevel = Mathf.Max(1, so.maxLevel);
        isMaxLevel = currentLevel >= maxLevel;
        if (tmp_skillName != null)
        {
            Loc.UseFont(tmp_skillName);
            tmp_skillName.SetText(Loc.Content(Loc.SkillName(so.baseSkillIndex), so.skillName));
        }
        if (tmp_level != null)
        {
            Loc.UseFont(tmp_level);
            tmp_level.SetText(isMaxLevel ?
                Loc.Text("ui.skill.level.max") :
                Loc.Format("ui.skill.level", currentLevel, maxLevel));
        }
        if (tmp_description != null)
        {
            Loc.UseFont(tmp_description);
            tmp_description.SetText(FormatDescription(so));
        }

        SetParamValues(so, currentLevel, isMaxLevel);
        SetData_RequiredCost(currentLevel);

        if (obj_complete != null)
            obj_complete.SetActive(isMaxLevel);
        if (obj_resourceRoot != null)
            obj_resourceRoot.SetActive(!isMaxLevel);
        gameObject.SetActive(true);
    }

    void OnEnable()
    {
        Loc.Changed += OnLanguage;
    }

    void OnDisable()
    {
        Loc.Changed -= OnLanguage;
    }

    void OnLanguage(GameLanguage _)
    {
        if (currentUnit != null)
            SetData_Base(currentUnit.level);
    }

    static string FormatDescription(SkillTreeBase skill)
    {
        string template = Loc.Content(Loc.SkillDesc(skill.baseSkillIndex), "");
        if (string.IsNullOrEmpty(template)) return "";
        // 説明の数字は、いつも 1 レベル分の差分（deltaValue）。合計は上の「今 → 次」の欄に出す。
        string value = FormatDelta(skill);
        if (template.Contains("{0}"))
        {
            try
            {
                return string.Format(template, value);
            }
            catch (System.FormatException)
            {
                return template;
            }
        }
        return template.Replace("[A]", value);
    }

    /// <summary>1 レベル分の差分。加速帯の間隔は、縮む秒数（符号なし）。</summary>
    static string FormatDelta(SkillTreeBase skill)
    {
        float delta = skill.deltaValue;
        if (skill.paramStat == SkillParamStat.BlockCompact)
            return SkillTreeBonus.FormatBlockCompact(delta);
        if (skill.paramStat == SkillParamStat.WallBoostInterval)
            return Mathf.Abs(delta).ToString("F0") + " " + Loc.Text("ui.unit.sec");
        if (!HasDisplayUnit(skill.unit))
            return "";
        return FormatParam(skill.unit, delta);
    }

    static string FormatParams(SkillTreeBase skill, int atLevel)
    {
        float value = skill.deltaValue * atLevel;
        if (skill.paramStat == SkillParamStat.BlockCompact)
            return SkillTreeBonus.FormatBlockCompact(value);
        // 加速帯の間隔は、縮める秒数ではなく、実際に出る間隔を出す。まだ取っていなければ「-」。
        if (skill.paramStat == SkillParamStat.WallBoostInterval)
            return atLevel <= 0 ? "-" : SkillTreeBonus.WallBoostIntervalFor(value).ToString("F0") + " " + Loc.Text("ui.unit.sec");
        if (!HasDisplayUnit(skill.unit))
            return "";
        return FormatParam(skill.unit, value);
    }

    void SetParamValues(SkillTreeBase skill, int currentLevel, bool maxed)
    {
        bool showValue = HasDisplayUnit(skill.unit) || skill.paramStat == SkillParamStat.BlockCompact
            || skill.paramStat == SkillParamStat.WallBoostInterval;
        if (obj_param != null)
            obj_param.SetActive(showValue);

        bool showNext = showValue && !maxed;
        if (tmp_paramNow != null)
        {
            tmp_paramNow.gameObject.SetActive(showValue);
            if (showValue)
                tmp_paramNow.SetText(FormatParamDisplay(skill, currentLevel));
        }
        if (tmp_paramNext != null)
        {
            tmp_paramNext.gameObject.SetActive(showNext);
            if (showNext)
                tmp_paramNext.SetText(FormatParamDisplay(skill, currentLevel + 1));
        }
        if (obj_paramArrow != null)
            obj_paramArrow.SetActive(showNext);
    }

    string FormatParamDisplay(SkillTreeBase skill, int atLevel)
    {
        return FormatParams(skill, atLevel);
    }

    static bool HasDisplayUnit(string unit)
    {
        return !string.IsNullOrEmpty(unit) && unit != "NA";
    }

    static string FormatParam(string unit, float value)
    {
        switch (unit)
        {
            case "%":
                return $"{Signed(value * 100f, "F1")} %";
            case "Sec":
                return Signed(value, "F1") + " " + Loc.Text("ui.unit.sec");
            case "Count":
                return Signed(value, "F0");
            case "NA":
            case "":
            case null:
                return "";
            default:
                return $"{Signed(value, "F1")} {unit}";
        }
    }

    static string Signed(float value, string format)
    {
        string number = Mathf.Abs(value).ToString(format);
        return value < 0f ? "-" + number : "+" + number;
    }

    private void EnsureCachedRefs()
    {
        if (rectTr == null) rectTr = transform as RectTransform;
    }

    public void SetData_Enhanced(int currentLevel)
    {
        SetData_Base(currentLevel);
    }

    private void SetData_RequiredCost(int currentLevel)
    {
        requredResources.Clear();
        if (ui_resourceCounts != null)
        {
            foreach (var cont in ui_resourceCounts)
            {
                if (cont != null) cont.gameObject.SetActive(false);
            }
        }

        if (currentUnit == null || isMaxLevel)
        {
            resourceReady = false;
            return;
        }

        var table = SOLoader.SkillTreeData;
        var costRow = table != null
            ? table.GetSkillTreeUnitCost(currentUnit.skillIndex, currentLevel + 1)
            : null;
        if (costRow == null)
        {
            resourceReady = false;
            return;
        }

        var costs = costRow.CopyPositiveCosts();
        requredResources.AddRange(costs);
        resourceReady = StaticManager.IsResourceEnough(costs);

        for (int i = 0; i < costs.Length; i++)
        {
            var resource = costs[i];
            var cont = FindResourceCounter(resource.resourceType);
            if (cont == null) continue;

            cont.gameObject.SetActive(true);
            bool known = SaveLoader.Inst != null && SaveLoader.Inst.Check_ResourceKeyExists(resource.resourceType);
            if (!known)
                cont.SetUnknown();
            else
            {
                var IsResourceEnough = StaticManager.IsResourceEnough(resource.resourceType, resource.requiredCount);
                cont.SetCount(resource.requiredCount, IsResourceEnough);
            }
        }
    }

    UI_ResourceCounter_Simple FindResourceCounter(ResourceType resourceType)
    {
        if (ui_resourceCounts == null) return null;
        for (int i = 0; i < ui_resourceCounts.Length; i++)
        {
            var cont = ui_resourceCounts[i];
            if (cont != null && cont.ResourceType == resourceType)
                return cont;
        }
        return null;
    }
}
