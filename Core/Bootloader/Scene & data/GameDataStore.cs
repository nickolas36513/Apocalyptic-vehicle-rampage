using System;
using System.Collections.Generic;

public sealed class GameDataStore
{
    private readonly Dictionary<string, object> data = new();
    private readonly GameEventBus eventBus;

    public int Count => data.Count;

    public GameDataStore(GameEventBus eventBus)
    {
        this.eventBus = eventBus ??
            throw new ArgumentNullException(nameof(eventBus));
    }

    public bool Set<T>(string key, T value)
    {
        ValidateKey(key);

        object newValue = value;

        data.TryGetValue(key, out object previousValue);

        if (data.ContainsKey(key) &&
            Equals(previousValue, newValue))
        {
            return false;
        }

        data[key] = newValue;

        eventBus.Publish(
            new GameDataChangedEvent(
                key,
                previousValue,
                newValue));

        return true;
    }

    public bool TryGet<T>(string key, out T value)
    {
        ValidateKey(key);

        if (data.TryGetValue(key, out object storedValue) &&
            storedValue is T typedValue)
        {
            value = typedValue;
            return true;
        }

        value = default;
        return false;
    }

    public T Get<T>(string key)
    {
        if (TryGet<T>(key, out T value))
            return value;

        throw new KeyNotFoundException(
            $"Missing data or incompatible type for key '{key}'.");
    }

    public bool Contains(string key)
    {
        ValidateKey(key);
        return data.ContainsKey(key);
    }

    public bool Remove(string key)
    {
        ValidateKey(key);

        if (!data.TryGetValue(key, out object previousValue))
            return false;

        data.Remove(key);

        eventBus.Publish(
            new GameDataChangedEvent(
                key,
                previousValue,
                null));

        return true;
    }

    public void Clear()
    {
        string[] keys = new string[data.Count];
        data.Keys.CopyTo(keys, 0);

        foreach (string key in keys)
            Remove(key);
    }

    private static void ValidateKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException(
                "Data key cannot be empty.",
                nameof(key));
        }
    }
}