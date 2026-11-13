using System;

public sealed class UIInstaller : IGameInstaller
{
    private readonly UIScreenCatalog catalog;

    public UIInstaller(UIScreenCatalog catalog)
    {
        this.catalog = catalog ??
            throw new ArgumentNullException(nameof(catalog));
    }

    public void Install(
        GameServiceRegistry services,
        GameStartupTaskRegistry startupTasks)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));

        services.Register(
            new UIScreenRegistry(catalog));
    }
}