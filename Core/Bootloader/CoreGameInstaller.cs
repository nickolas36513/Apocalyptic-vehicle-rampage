
using System;

public sealed class CoreGameInstaller : IGameInstaller
{
    private readonly GameEventBus eventBus;
    private readonly GameBootstrapConfiguration bootstrapConfiguration;

    public CoreGameInstaller(GameEventBus eventBus)
        : this(eventBus, null)
    {
    }

    public CoreGameInstaller(
        GameEventBus eventBus,
        GameBootstrapConfiguration bootstrapConfiguration)
    {
        this.eventBus = eventBus ??
            throw new ArgumentNullException(nameof(eventBus));
        this.bootstrapConfiguration = bootstrapConfiguration;
    }

    public void Install(
        GameServiceRegistry services,
        GameStartupTaskRegistry startupTasks)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));

        if (startupTasks == null)
            throw new ArgumentNullException(nameof(startupTasks));

        if (bootstrapConfiguration == null)
        {
            throw new InvalidOperationException(
                "CoreGameInstaller requires GameBootstrapConfiguration.");
        }

        SaveSystem save = new SaveSystem(
            new JsonSaveProvider(),
            eventBus);

        ApplicationLifecycleManager lifecycle =
            new ApplicationLifecycleManager(eventBus);

        services.Register<SaveSystem>(save);
        services.Register<ApplicationLifecycleManager>(lifecycle);

        services.Register<PlayerProfileRuntimeService>(
            new PlayerProfileRuntimeService(
                bootstrapConfiguration.Definitions,
                bootstrapConfiguration.StartingCurrency));
    }
}
