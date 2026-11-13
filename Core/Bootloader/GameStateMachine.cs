
using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class GameStateMachine
{
    private readonly List<IGameStateHandler> handlers = new();
    private readonly GameEventBus eventBus;

    private bool isTransitioning;
    private bool isCleared;

    public GameState CurrentState { get; private set; }
        = GameState.Uninitialized;

    public bool IsTransitioning => isTransitioning;
    public bool IsCleared => isCleared;

    public GameStateMachine(GameEventBus eventBus)
    {
        this.eventBus = eventBus ??
            throw new ArgumentNullException(nameof(eventBus));
    }

    public void RegisterHandler(IGameStateHandler handler)
    {
        ThrowIfCleared();

        if (handler == null)
            throw new ArgumentNullException(nameof(handler));

        if (!handlers.Contains(handler))
            handlers.Add(handler);
    }

    public void UnregisterHandler(IGameStateHandler handler)
    {
        if (isCleared || handler == null)
            return;

        handlers.Remove(handler);
    }

    public bool TryChangeState(GameState newState)
    {
        if (isCleared || isTransitioning)
            return false;

        if (CurrentState == newState)
            return false;

        if (!IsTransitionAllowed(CurrentState, newState))
            return false;

        GameState previousState = CurrentState;
        IGameStateHandler[] handlerSnapshot = handlers.ToArray();

        isTransitioning = true;

        try
        {
            // Exit callbacks are notifications, not vetoes.
            InvokeExitHandlers(handlerSnapshot, previousState, newState);

            CurrentState = newState;

            try
            {
                eventBus.Publish(
                    new GameStateChangedEvent(previousState, newState));
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }

            InvokeEnterHandlers(handlerSnapshot, previousState, newState);

            // The state changed even if a notification failed.
            return true;
        }
        finally
        {
            isTransitioning = false;
        }
    }

    private static void InvokeExitHandlers(
        IGameStateHandler[] snapshot,
        GameState previousState,
        GameState newState)
    {
        foreach (IGameStateHandler handler in snapshot)
        {
            try
            {
                handler.OnStateExit(previousState, newState);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }
    }

    private static void InvokeEnterHandlers(
        IGameStateHandler[] snapshot,
        GameState previousState,
        GameState newState)
    {
        foreach (IGameStateHandler handler in snapshot)
        {
            try
            {
                handler.OnStateEnter(previousState, newState);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }
    }

    private static bool IsTransitionAllowed(
        GameState current,
        GameState next)
    {
        // Shutdown is terminal.
        if (current == GameState.Shutdown)
            return false;

        // Shutdown can be requested from any non-terminal state.
        if (next == GameState.ShuttingDown)
            return true;

        // Shutdown completion is only valid from ShuttingDown.
        if (current == GameState.ShuttingDown)
            return next == GameState.Shutdown;

        return current switch
        {
            GameState.Uninitialized =>
                next == GameState.Initializing,

            GameState.Initializing =>
                next == GameState.Ready,

            GameState.Ready =>
                next == GameState.Running ||
                next == GameState.Suspended ||
                next == GameState.Transitioning,

            GameState.Running =>
                next == GameState.Suspended ||
                next == GameState.Transitioning,

            GameState.Suspended =>
                next == GameState.Running ||
                next == GameState.Transitioning,

            GameState.Transitioning =>
                next == GameState.Ready ||
                next == GameState.Running ||
                next == GameState.Suspended,

            _ => false
        };
    }

    public void Clear()
    {
        if (isCleared)
            return;

        if (isTransitioning)
        {
            Debug.LogWarning(
                "[GameStateMachine] Cannot clear during a state transition.");
            return;
        }

        handlers.Clear();
        isCleared = true;
    }

    private void ThrowIfCleared()
    {
        if (isCleared)
        {
            throw new ObjectDisposedException(
                nameof(GameStateMachine),
                "This state machine has been cleared.");
        }
    }
}
