public readonly struct ApplicationLifecycleInitializedEvent { }

public readonly struct ApplicationPausedEvent { }

public readonly struct ApplicationResumedEvent { }

public readonly struct ApplicationFocusChangedEvent
{
    public readonly bool HasFocus;

    public ApplicationFocusChangedEvent(bool hasFocus)
    {
        HasFocus = hasFocus;
    }
}

public readonly struct ApplicationShutdownEvent { }