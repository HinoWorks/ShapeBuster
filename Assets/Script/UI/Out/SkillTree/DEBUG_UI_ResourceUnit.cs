using TMPro;
using UnityEngine;

public class DEBUG_UI_ResourceUnit : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI tmp_count;

    public void SetResourceCount(int count)
    {
        if (tmp_count != null)
            tmp_count.text = count.ToString();
    }
}
