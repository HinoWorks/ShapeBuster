using UniRx;
using UnityEngine;
using UnityEngine.UI;

public class UI_TItleManager : MonoBehaviour
{
    Canvas canvas;
    GraphicRaycaster raycaster;

    [Tooltip("ワクチンを撒いたあとに出す、小さな緑のしずくの印")]
    [SerializeField] GameObject obj_vaccineMark;

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
        bool show = state == GameStateType.Title;
        SetScreenVisible(show);
        if (obj_vaccineMark != null)
            obj_vaccineMark.SetActive(show && VaccineRuntime.Cleared);
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

    public void OnClick_Exit()
    {
        UI_Options.QuitGame();
    }
    #endregion
}
