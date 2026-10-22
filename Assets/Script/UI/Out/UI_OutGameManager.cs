using UniRx;
using UnityEngine;
using UnityEngine.UI;

public class UI_OutGameManager : MonoBehaviour
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
        SetScreenVisible(state == GameStateType.OutGame);
    }

    void SetScreenVisible(bool show)
    {
        if (canvas != null) canvas.enabled = show;
        if (raycaster != null) raycaster.enabled = show;
    }

    #region -- OnClick --
    public void OnClick_ToIngame()
    {
        GameWatcher.Inst.SetGameState(GameStateType.InGame_Ready);
    }

    public void OnClick_ToTitle()
    {
        GameWatcher.Inst.SetGameState(GameStateType.Title);
    }
    #endregion
}
