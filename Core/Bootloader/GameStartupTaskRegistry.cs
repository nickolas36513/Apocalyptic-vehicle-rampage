
using System;
using System.Collections.Generic;

public sealed class GameStartupTaskRegistry
{
    private readonly List<IGameStartupTask> tasks = new();
    private bool isSealed;

    public int Count => tasks.Count;
    public bool IsSealed => isSealed;

    public void Register(IGameStartupTask task)
    {
        if (task == null)
            throw new ArgumentNullException(nameof(task));

        if (isSealed)
            throw new InvalidOperationException(
                "Startup task registration is closed.");

        if (tasks.Contains(task))
            throw new InvalidOperationException(
                "Startup task is already registered.");

        tasks.Add(task);
    }

    public IReadOnlyList<IGameStartupTask> Seal()
    {
        isSealed = true;

        tasks.Sort((a, b) => a.Order.CompareTo(b.Order));

        return tasks.AsReadOnly();
    }

    public void Clear()
    {
        tasks.Clear();
        isSealed = false;
    }
}
