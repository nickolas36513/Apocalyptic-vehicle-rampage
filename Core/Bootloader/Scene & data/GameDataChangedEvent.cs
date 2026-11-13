public readonly struct GameDataChangedEvent
{
    public readonly string Key;
    public readonly object PreviousValue;
    public readonly object CurrentValue;

    public GameDataChangedEvent(
        string key,
        object previousValue,
        object currentValue)
    {
        Key = key;
        PreviousValue = previousValue;
        CurrentValue = currentValue;
    }
}