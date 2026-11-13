using System.Threading;
using System.Threading.Tasks;

public interface IGameStartupTask
{
    int Order { get; }
    Task ExecuteAsync(CancellationToken cancellationToken);
}
