
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public sealed class GameStartupPipeline
{
    private readonly List<IGameStartupTask> tasks = new();

    public bool IsRunning { get; private set; }
    public bool IsCompleted { get; private set; }

    public void Configure(IEnumerable<IGameStartupTask> startupTasks)
    {
        if (IsRunning || IsCompleted)
            throw new InvalidOperationException(
                "The startup pipeline can no longer be configured.");

        if (startupTasks == null)
            throw new ArgumentNullException(nameof(startupTasks));

        tasks.Clear();
        tasks.AddRange(startupTasks);

        tasks.Sort((a, b) => a.Order.CompareTo(b.Order));
    }

    public async Task ExecuteAsync(
        CancellationToken cancellationToken = default)
    {
        if (IsRunning)
            throw new InvalidOperationException(
                "Startup pipeline is already running.");

        if (IsCompleted)
            return;

        IsRunning = true;

        try
        {
            foreach (IGameStartupTask task in tasks)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await task.ExecuteAsync(cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
            IsCompleted = true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Expected cancellation. Do not log as an error.
            throw;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            throw;
        }
        finally
        {
            IsRunning = false;
        }
    }

    public void Clear()
    {
        if (IsRunning)
            throw new InvalidOperationException(
                "Cannot clear a running startup pipeline.");

        tasks.Clear();
        IsCompleted = false;
    }
}
