
using System.Threading;
using System.Threading.Tasks;

public interface IGameShutdownHandler
{
    int ShutdownOrder { get; }

    Task ShutdownAsync(CancellationToken cancellationToken);
}
