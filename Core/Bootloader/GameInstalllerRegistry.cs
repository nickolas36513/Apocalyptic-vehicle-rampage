
using System;
using System.Collections.Generic;

public sealed class GameInstallerRegistry
{
    private readonly List<IGameInstaller> installers = new();
    private bool isInstalled;

    public int Count => installers.Count;
    public bool IsInstalled => isInstalled;

    public void Register(IGameInstaller installer)
    {
        if (installer == null)
            throw new ArgumentNullException(nameof(installer));

        if (isInstalled)
            throw new InvalidOperationException(
                "Installers cannot be registered after installation.");

        if (installers.Contains(installer))
            throw new InvalidOperationException(
                "Installer is already registered.");

        installers.Add(installer);
    }

    public void InstallAll(
        GameServiceRegistry services,
        GameStartupTaskRegistry startupTasks)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));

        if (startupTasks == null)
            throw new ArgumentNullException(nameof(startupTasks));

        if (isInstalled)
            throw new InvalidOperationException(
                "Installers have already run.");

        try
        {
            foreach (IGameInstaller installer in installers)
                installer.Install(services, startupTasks);

            isInstalled = true;
        }
        catch
        {
            services.Clear();
            startupTasks.Clear();
            throw;
        }
    }

    public void Clear()
    {
        installers.Clear();
        isInstalled = false;
    }
}
