using BigInteger = System.Numerics.BigInteger;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// AAをB、C、Dに換える。余りは次の換金まで残す。
/// </summary>
public class UI_ConvertPanel : MonoBehaviour
{
    const string RemainderKey = "shapebuster.convert.remainder";

    UI_OutGameManager manager;
    GameObject root;
    GameObject tab;
    Text rateLabel;
    Text remainderLabel;
    ResourceType selected = ResourceType.BB;

    public void Build(UI_OutGameManager owner, UI_OutGameTabBase anchor)
    {
        manager = owner;
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        BuildTab(anchor, font);
        BuildPanel(font);
        RefreshUnlock();
        Show(false);
    }

    public void RefreshUnlock()
    {
        if (tab != null)
            tab.SetActive(SkillTreeBonus.ConvertUnlocked);
    }

    public void Show(bool show)
    {
        if (root != null)
            root.SetActive(show && SkillTreeBonus.ConvertUnlocked);
        if (show)
            RefreshLabels();
    }

    void BuildTab(UI_OutGameTabBase anchor, Font font)
    {
        tab = new GameObject("ConvertTab", typeof(RectTransform), typeof(Image), typeof(Button));
        var rect = tab.GetComponent<RectTransform>();
        if (anchor != null)
        {
            rect.SetParent(anchor.transform.parent, false);
            var source = anchor.transform as RectTransform;
            if (source != null)
            {
                rect.anchorMin = source.anchorMin;
                rect.anchorMax = source.anchorMax;
                rect.pivot = source.pivot;
                rect.sizeDelta = source.sizeDelta;
                rect.anchoredPosition = source.anchoredPosition + new Vector2(source.sizeDelta.x + 12f, 0f);
            }
        }
        else
        {
            rect.SetParent(transform, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(160f, 48f);
            rect.anchoredPosition = new Vector2(180f, -12f);
        }

        tab.GetComponent<Image>().color = new Color(0.16f, 0.2f, 0.24f, 1f);
        tab.GetComponent<Button>().onClick.AddListener(() => manager.ActiveTabChange(OutGame_MenuType.Convert));
        AddLabel(tab.transform, font, "Convert", 22, Vector2.zero, new Vector2(160f, 48f));
    }

    void BuildPanel(Font font)
    {
        root = new GameObject("ConvertPanel", typeof(RectTransform), typeof(Image));
        var rect = root.GetComponent<RectTransform>();
        rect.SetParent(transform, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(520f, 280f);
        root.GetComponent<Image>().color = new Color(0.08f, 0.09f, 0.11f, 0.94f);

        AddLabel(root.transform, font, "Convert", 28, new Vector2(0f, 100f), new Vector2(480f, 40f));
        rateLabel = AddLabel(root.transform, font, "", 22, new Vector2(0f, 48f), new Vector2(480f, 36f));
        remainderLabel = AddLabel(root.transform, font, "", 18, new Vector2(0f, 12f), new Vector2(480f, 32f));
        AddChoice(font, "B", ResourceType.BB, -160f);
        AddChoice(font, "C", ResourceType.CC, 0f);
        AddChoice(font, "D", ResourceType.DD, 160f);
        AddAction(font, "1つ換える", false, new Vector2(-110f, -90f));
        AddAction(font, "まとめて換える", true, new Vector2(110f, -90f));
    }

    void AddChoice(Font font, string label, ResourceType type, float x)
    {
        var button = MakeButton(root.transform, font, label, new Vector2(x, -28f), new Vector2(120f, 40f));
        button.onClick.AddListener(() =>
        {
            selected = type;
            RefreshLabels();
        });
    }

    void AddAction(Font font, string label, bool all, Vector2 position)
    {
        var button = MakeButton(root.transform, font, label, position, new Vector2(200f, 44f));
        button.onClick.AddListener(() => Convert(all));
    }

    void Convert(bool all)
    {
        if (!SkillTreeBonus.ConvertUnlocked || SaveLoader.Inst == null) return;
        int cost = SkillTreeBonus.ConvertCost(selected);
        if (cost <= 0) return;

        int bank = PlayerPrefs.GetInt(RemainderKey, 0);
        BigInteger owned = SaveLoader.Inst.Get_ResourceCount(ResourceType.AA);
        if (owned < 0) owned = 0;
        BigInteger pool = bank + owned;
        BigInteger count = all ? pool / cost : (pool >= cost ? 1 : 0);
        if (count <= 0) return;

        BigInteger spent = count * cost - bank;
        BigInteger nextBank = pool - count * cost;
        PlayerPrefs.SetInt(RemainderKey, (int)nextBank);
        SaveLoader.Inst.Request_SaveResource(ResourceType.AA, -spent);
        SaveLoader.Inst.Request_SaveResource(selected, count);
        RefreshLabels();
    }

    void RefreshLabels()
    {
        if (rateLabel == null) return;
        int cost = SkillTreeBonus.ConvertCost(selected);
        int bank = PlayerPrefs.GetInt(RemainderKey, 0);
        rateLabel.text = $"{selected} 1つに AA {cost}";
        remainderLabel.text = $"繰り越し AA {bank}";
    }

    static Button MakeButton(Transform parent, Font font, string label, Vector2 position, Vector2 size)
    {
        var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        go.GetComponent<Image>().color = new Color(0.22f, 0.28f, 0.34f, 1f);
        AddLabel(go.transform, font, label, 20, Vector2.zero, size);
        return go.GetComponent<Button>();
    }

    static Text AddLabel(Transform parent, Font font, string value, int size, Vector2 position, Vector2 box)
    {
        var go = new GameObject("Label", typeof(RectTransform));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = box;
        rect.anchoredPosition = position;
        var text = go.AddComponent<Text>();
        text.font = font;
        text.fontSize = size;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.text = value;
        text.raycastTarget = false;
        return text;
    }
}
