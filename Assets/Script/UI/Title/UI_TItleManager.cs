using UniRx;
using UnityEngine;
using UnityEngine.UI;

public class UI_TItleManager : MonoBehaviour
{
    Canvas canvas;
    GraphicRaycaster raycaster;

    void Awake()
    {
        canvas = GetComponent<Canvas>();
        raycaster = GetComponent<GraphicRaycaster>();
        GameEvent.GameState.SetGameState.Subscribe(OnGameState).AddTo(this);
        Apply(GameStateType.Title);
    }


    void OnGameState(GameStateType state)
    {
        Apply(state);
    }

    void Apply(GameStateType state)
    {
        SetScreenVisible(state == GameStateType.Title);
    }

    void SetScreenVisible(bool show)
    {
        if (canvas != null) canvas.enabled = show;
        if (raycaster != null) raycaster.enabled = show;
    }

    #region -- OnClick --
    public void OnClick_ToOutGame()
    {
        GameWatcher.Inst.SetGameState(GameStateType.OutGame);
    }
    #endregion
}
