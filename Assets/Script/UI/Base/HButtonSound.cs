using UnityEngine;
using UnityEngine.UI;

public class HButtonSound : MonoBehaviour
{
    private HButton button;
    [SerializeField] int index_SE_Click;
    [SerializeField] int index_SE_Hover;
    void Awake()
    {
        button = this.GetComponent<HButton>();
        button.onClick.AddListener(PlaySE_Click);
        button.onMouseOver += PlaySE_Hover;
    }


    private void PlaySE_Hover(bool _isHover)
    {
        if (_isHover)
        {
            SoundManager.Inst.PlaySE_UI(index_SE_Hover);
        }
    }
    private void PlaySE_Click()
    {
        if (index_SE_Click < 0) return;
        SoundManager.Inst.PlaySE_UI(index_SE_Click);
    }

}
