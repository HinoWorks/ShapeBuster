using UnityEngine;

/// <summary>HButton に付けると、押したときとカーソルが乗ったときに効果音を鳴らす。None なら鳴らさない。</summary>
public class HButtonSound : MonoBehaviour
{
    [SerializeField] SEType clickSE = SEType.UI_Click;
    [SerializeField] SEType hoverSE = SEType.None;

    HButton button;

    void Awake()
    {
        button = GetComponent<HButton>();
        if (button == null) return;
        button.onClick.AddListener(PlayClick);
        button.onMouseOver += PlayHover;
    }

    void PlayHover(bool isHover)
    {
        if (isHover)
            SoundManager.SE(hoverSE);
    }

    void PlayClick()
    {
        SoundManager.SE(clickSE);
    }
}
