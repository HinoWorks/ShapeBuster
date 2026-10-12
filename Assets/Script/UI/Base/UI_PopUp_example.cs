using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UI_PopUp_example : UI_PopUpBase
{

    public override void Open()
    {

        base.Open();
    }


#if UNITY_EDITOR
    [SerializeField] bool _isOn;
    private void OnValidate()
    {
        if (_isOn)
        {
            _isOn = false;
            ButtonProcess();
        }
    }

    private void ButtonProcess()
    {
        Open();
        Debug.Log("Pushed Button");
    }
#endif

}
