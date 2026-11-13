public readonly struct SaveCompletedEvent
{
    public readonly string Slot;

    public SaveCompletedEvent(string slot)
    {
        Slot = slot;
    }
}

public readonly struct SaveFailedEvent
{
    public readonly string Slot;
    public readonly string Reason;

    public SaveFailedEvent(string slot, string reason)
    {
        Slot = slot;
        Reason = reason;
    }
}

public readonly struct LoadCompletedEvent
{
    public readonly string Slot;

    public LoadCompletedEvent(string slot)
    {
        Slot = slot;
    }
}

public readonly struct LoadFailedEvent
{
    public readonly string Slot;
    public readonly string Reason;

    public LoadFailedEvent(string slot, string reason)
    {
        Slot = slot;
        Reason = reason;
    }
}

public readonly struct SaveDeletedEvent
{
    public readonly string Slot;

    public SaveDeletedEvent(string slot)
    {
        Slot = slot;
    }
}