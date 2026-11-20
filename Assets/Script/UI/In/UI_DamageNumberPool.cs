using System;
using System.Collections.Generic;
using UniRx;
using UnityEngine;

/// <summary>
/// ダメージ数字のプール。Canvas_Ingame(dynamic) に置き、子として出す。
/// 色はボール種ごとにここの一覧で変える。
/// </summary>
public class UI_DamageNumberPool : MonoBehaviour
{
    public static UI_DamageNumberPool Inst { get; private set; }

    [SerializeField] UI_DamageNumber prefab;
    [SerializeField] int prewarm = 32;
    [Tooltip("同じ位置の数字が重ならないよう、ずらす半径")]
    [SerializeField] float scatter = 14f;
    [SerializeField] Color fallback = Color.white;
    [SerializeField] KindColor[] colors;

    readonly Stack<UI_DamageNumber> available = new();
    readonly List<UI_DamageNumber> active = new();
    readonly Dictionary<int, Color> byKind = new();

    [Serializable]
    public struct KindColor
    {
        public BlockBallKind kind;
        public Color color;
    }

    void Awake()
    {
        if (Inst != null && Inst != this)
        {
            Destroy(this);
            return;
        }

        Inst = this;
        BuildColors();
        Warm();
        GameEvent.GameState.SetGameState.Subscribe(OnGameState).AddTo(this);
    }

    void OnDestroy()
    {
        if (Inst == this) Inst = null;
    }

    void OnGameState(GameStateType state)
    {
        switch (state)
        {
            case GameStateType.InGame_Ready:
            case GameStateType.InGame_End:
            case GameStateType.Title:
            case GameStateType.OutGame:
                ReleaseAll();
                break;
        }
    }

    public static void Show(Vector2 world, int damage, int ballKind, bool critical)
    {
        if (damage <= 0 || !GameOptions.DamageNumbers) return;
        Inst?.Spawn(world, damage, ballKind, critical);
    }

    public void Return(UI_DamageNumber number)
    {
        if (number == null || number.InPool) return;
        if (!active.Remove(number))
        {
            Destroy(number.gameObject);
            return;
        }

        number.StopAndHide();
        number.transform.SetParent(transform, false);
        available.Push(number);
    }

    void Spawn(Vector2 world, int damage, int ballKind, bool critical)
    {
        if (prefab == null) return;

        var canvas = GetComponent<Canvas>();
        var parent = (RectTransform)transform;
        var cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;
        var screen = RectTransformUtility.WorldToScreenPoint(cam, world);
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, cam, out var local))
            return;

        if (scatter > 0f)
            local += (Vector2)UnityEngine.Random.insideUnitCircle * scatter;

        UI_DamageNumber number = null;
        while (available.Count > 0)
        {
            number = available.Pop();
            if (number != null) break;
        }
        if (number == null)
            number = Create();

        active.Add(number);
        number.transform.SetParent(transform, false);
        number.Play(damage, ColorOf(ballKind), critical, local);
    }

    void ReleaseAll()
    {
        for (int i = active.Count - 1; i >= 0; i--)
            Return(active[i]);
    }

    void Warm()
    {
        if (prefab == null) return;
        int count = Mathf.Max(0, prewarm);
        for (int i = 0; i < count; i++)
            available.Push(Create());
    }

    UI_DamageNumber Create()
    {
        var number = Instantiate(prefab, transform);
        number.Bind(this);
        number.StopAndHide();
        return number;
    }

    void BuildColors()
    {
        byKind.Clear();
        if (colors == null) return;
        for (int i = 0; i < colors.Length; i++)
            byKind[(int)colors[i].kind] = colors[i].color;
    }

    Color ColorOf(int ballKind)
    {
        if (ballKind >= 0 && byKind.TryGetValue(ballKind, out var color))
            return color;
        return fallback;
    }
}
