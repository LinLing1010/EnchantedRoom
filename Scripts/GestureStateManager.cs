using System.Collections.Generic;
using UnityEngine;

public enum AppGestureState
{
    Idle,
    ShowingOnPalm,   // 有东西在手掌显示
    Placing,         // 放置瞬间
    ObjectPlaced,    // 已放置到场景
}

public class GestureStateManager : MonoBehaviour
{
    public static GestureStateManager Instance { get; private set; }
    public AppGestureState CurrentState { get; private set; } = AppGestureState.Idle;

    private static readonly Dictionary<AppGestureState, int> _priority = new()
    {
        { AppGestureState.ObjectPlaced,  0 },
        { AppGestureState.Placing,       1 },
        { AppGestureState.ShowingOnPalm, 2 },
        { AppGestureState.Idle,         99 },
    };

    void Awake() => Instance = this;

    public bool TryEnter(AppGestureState newState)
    {
        if (_priority[newState] <= _priority[CurrentState])
        {
            Debug.Log($"[GestureState] {CurrentState} → {newState}");
            CurrentState = newState;
            return true;
        }
        return false;
    }

    public void Exit(AppGestureState state)
    {
        if (CurrentState == state)
            CurrentState = AppGestureState.Idle;
    }

    public bool Is(AppGestureState state) => CurrentState == state;
}