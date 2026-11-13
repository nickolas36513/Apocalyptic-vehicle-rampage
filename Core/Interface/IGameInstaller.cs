
public interface IGameInstaller
{
    void Install(
        GameServiceRegistry services,
        GameStartupTaskRegistry startupTasks);
}
