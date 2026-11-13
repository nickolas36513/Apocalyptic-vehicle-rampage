
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public sealed class GameShutdownCoordinator
{
    private readonly List<IGameShutdownHandler> handlers = new();

    public bool IsShuttingDown { get; private set; }
    public bool IsShutdown { get; private set; }

    public void Register(IGameShutdownHandler handler)
    {
        if (handler == null)
            throw new ArgumentNullException(nameof(handler));

        if (IsShuttingDown || IsShutdown)
            throw new InvalidOperationException(
                "Cannot register handlers after shutdown begins.");

        if (handlers.Contains(handler))
            throw new InvalidOperationException(
                "Shutdown handler is already registered.");

        handlers.Add(handler);
    }

    public async Task ShutdownAsync(
        CancellationToken cancellationToken = default)
    {
        if (IsShutdown || IsShuttingDown)
            return;

        IsShuttingDown = true;

        try
        {
            foreach (IGameShutdownHandler handler in
                     handlers.OrderByDescending(h => h.ShutdownOrder))
            {
                try
                {
                    await handler.ShutdownAsync(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    Debug.LogWarning(
                        "[Shutdown] A shutdown handler was cancelled.");
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }

            IsShutdown = true;
        }
        finally
        {
            IsShuttingDown = false;
        }
    }

    public void Clear()
    {
        if (IsShuttingDown)
            throw new InvalidOperationException(
                "Cannot clear handlers while shutdown is running.");

        handlers.Clear();
    }
}
