using UnityEngine;

public class PlayerManager : MonoBehaviour
{

    public static PlayerManager Inst { get; private set; }

    [SerializeField] PlayerHPCont playerHPCont;
    public PlayerHPCont PlayerHPCont => playerHPCont;



    void Awake()
    {
        if (Inst == null) { Inst = this; }
        else { Destroy(this); return; }

        playerHPCont.Init_Once();
    }

    public void Init()
    {
        playerHPCont.Init();
    }


}
