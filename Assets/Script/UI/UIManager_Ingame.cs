using UnityEngine;

public class UIManager_Ingame : MonoBehaviour
{
    public static UIManager_Ingame Inst { get; private set; }
    [SerializeField] UI_PlayerHpCont playerHpCont;

    private void Awake()
    {
        if (Inst == null) Inst = this;
        else Destroy(gameObject);
    }


    private void Start()
    {
        playerHpCont.InitOnce();
    }

}
