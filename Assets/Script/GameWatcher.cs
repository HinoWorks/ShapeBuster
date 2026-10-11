using UnityEngine;

public class GameWatcher : MonoBehaviour
{
    public static GameWatcher Inst { get; private set; }

    private void Awake()
    {
        if (Inst == null) Inst = this;
        else Destroy(gameObject);
    }



    public void AA()
    {

    }


}
