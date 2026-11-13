using System;

public sealed class ApplicationLifecycleManager : IGameService
{
    private readonly GameEventBus eventBus;

    public bool IsInitialized { get; private set; }
    public bool IsPaused { get; private set; }
    public bool HasFocus { get; private set; } = true;
    public bool IsShuttingDown { get; private set; }

    public ApplicationLifecycleManager(GameEventBus eventBus)
    {
        this.eventBus = eventBus ??
            throw new ArgumentNullException(nameof(eventBus));
    }

    public void Initialize()
    {
        if (IsInitialized || IsShuttingDown)
            return;

        IsInitialized = true;

        eventBus.Publish(
            new ApplicationLifecycleInitializedEvent());

    }

    public void HandlePause(bool paused)
    {
        if (!IsInitialized || IsShuttingDown || IsPaused == paused)
            return;

        IsPaused = paused;

        if (paused)
        {
            eventBus.Publish(new ApplicationPausedEvent());
        }
        else
        {
            eventBus.Publish(new ApplicationResumedEvent());
        }
    }

    public void HandleFocus(bool hasFocus)
    {
        if (!IsInitialized || IsShuttingDown || HasFocus == hasFocus)
            return;

        HasFocus = hasFocus;

        eventBus.Publish(
            new ApplicationFocusChangedEvent(hasFocus));

    }

    public void HandleShutdown()
    {
        if (!IsInitialized || IsShuttingDown)
            return;

        IsShuttingDown = true;

        eventBus.Publish(new ApplicationShutdownEvent());

    }

    public void Shutdown()
    {
        if (!IsInitialized)
            return;

        HandleShutdown();

        IsInitialized = false;

    }
}
