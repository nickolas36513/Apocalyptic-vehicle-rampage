using System;
using System.Collections.Generic;

public sealed class GameEventBus
{
    private readonly Dictionary<Type, Delegate> listeners = new();

    public void Subscribe<T>(Action<T> listener)
    {
        if (listener == null)
            throw new ArgumentNullException(nameof(listener));

        Type eventType = typeof(T);

        if (listeners.TryGetValue(eventType, out Delegate existing))
            listeners[eventType] = Delegate.Combine(existing, listener);
        else
            listeners.Add(eventType, listener);
    }

    public void Unsubscribe<T>(Action<T> listener)
    {
        if (listener == null)
            return;

        Type eventType = typeof(T);

        if (!listeners.TryGetValue(eventType, out Delegate existing))
            return;

        Delegate updated = Delegate.Remove(existing, listener);

        if (updated == null)
            listeners.Remove(eventType);
        else
            listeners[eventType] = updated;
    }

    public void Publish<T>(T gameEvent)
    {
        if (!listeners.TryGetValue(typeof(T), out Delegate existing))
            return;

        if (existing is not Action<T> callbacks)
            return;

        foreach (Delegate callback in callbacks.GetInvocationList())
        {
            try
            {
                ((Action<T>)callback).Invoke(gameEvent);
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogException(exception);
            }
        }
    }

    public void Clear()
    {
        listeners.Clear();
    }
}