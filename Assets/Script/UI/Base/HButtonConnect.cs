using UnityEngine;
using UnityEngine.Events;

public class HButtonConnect : MonoBehaviour
{
    [SerializeField] GameObject obj_OFF;
    [SerializeField] GameObject obj_mouseOver;
    [SerializeField] GameObject obj_clickSelect;

    public UnityAction rightClick;

    void Awake()
    {
        var button = this.GetComponent<HButton>();
        button.onMouseOver += Set_MouseOverActive;
        button.onSelect += Set_SelectActive;
        button.rightClick += RightClickAction;
        button.onInteractableChange += Set_Interactable;
    }

    public void Set_StateInit()
    {
        if (obj_mouseOver != null) obj_mouseOver.SetActive(false);
        if (obj_clickSelect != null) obj_clickSelect.SetActive(false);
    }

    public void Set_MouseOverActive(bool _active)
    {
        if (obj_mouseOver == null) return;
        obj_mouseOver.SetActive(_active);
    }

    public void Set_SelectActive(bool _active)
    {
        if (obj_clickSelect == null) return;
        obj_clickSelect.SetActive(_active);
    }

    public void RightClickAction()
    {
        if (rightClick != null)
        {
            rightClick.Invoke();
        }
    }

    public void Set_Interactable(bool _interactable)
    {
        if (obj_OFF != null) obj_OFF.SetActive(!_interactable);
        if (obj_mouseOver != null) obj_mouseOver.SetActive(false); //リセット
        if (obj_clickSelect != null) obj_clickSelect.SetActive(false); //リセット
    }


}
