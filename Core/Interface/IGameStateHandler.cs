public interface IGameStateHandler
{
    void OnStateEnter(GameState previousState, GameState newState);
    void OnStateExit(GameState previousState, GameState newState);
}