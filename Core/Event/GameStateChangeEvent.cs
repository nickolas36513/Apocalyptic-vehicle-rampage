public readonly struct GameStateChangedEvent
{
    public readonly GameState PreviousState;
    public readonly GameState CurrentState;

    public GameStateChangedEvent(
        GameState previousState,
        GameState currentState)
    {
        PreviousState = previousState;
        CurrentState = currentState;
    }
}