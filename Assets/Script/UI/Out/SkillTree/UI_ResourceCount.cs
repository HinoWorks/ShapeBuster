using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UI_ResourceCount : MonoBehaviour
{
    [SerializeField] Image icon;
    [SerializeField] TextMeshProUGUI tmp_count;

    public void NotActive()
    {
        gameObject.SetActive(false);
    }

    public void SetData(Sprite sprite, string count, Color color)
    {
        gameObject.SetActive(true);
        if (icon != null)
            icon.sprite = sprite;
        if (tmp_count == null) return;
        tmp_count.text = count;
        tmp_count.color = color;
    }
}
