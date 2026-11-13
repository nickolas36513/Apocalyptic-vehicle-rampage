using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class SaveSystem : IGameService
{
    private const int CurrentVersion = 1;

    private readonly ISaveProvider provider;
    private readonly GameEventBus eventBus;

    private SaveData currentData;

    public bool IsInitialized { get; private set; }

    public SaveSystem(
        ISaveProvider provider,
        GameEventBus eventBus)
    {
        this.provider = provider ??
            throw new ArgumentNullException(nameof(provider));

        this.eventBus = eventBus ??
            throw new ArgumentNullException(nameof(eventBus));
    }

    public void Initialize()
    {
        if (IsInitialized)
            return;

        currentData = CreateEmptySave();
        IsInitialized = true;
    }

    public void Shutdown()
    {
        currentData = null;
        IsInitialized = false;
    }

    public bool Set<T>(string key, T value)
    {
        if (!EnsureInitialized() || !ValidateKey(key))
            return false;

        try
        {
            string typeName = typeof(T).AssemblyQualifiedName;

            string json = JsonUtility.ToJson(
                new SerializedValue<T> { Value = value });

            SaveRecord record = currentData.Records.Find(
                item => item.Key == key);

            if (record == null)
            {
                record = new SaveRecord { Key = key };
                currentData.Records.Add(record);
            }

            record.TypeName = typeName;
            record.JsonValue = json;

            currentData.UpdatedAtUtc =
                DateTime.UtcNow.ToString("O");

            return true;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            return false;
        }
    }

    public bool TryGet<T>(string key, out T value)
    {
        value = default;

        if (!EnsureInitialized() || !ValidateKey(key))
            return false;

        SaveRecord record = currentData.Records.Find(
            item => item.Key == key);

        if (record == null ||
            record.TypeName != typeof(T).AssemblyQualifiedName)
        {
            return false;
        }

        try
        {
            SerializedValue<T> wrapper =
                JsonUtility.FromJson<SerializedValue<T>>(
                    record.JsonValue);

            if (wrapper == null)
                return false;

            value = wrapper.Value;
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            return false;
        }
    }

    public bool Remove(string key)
    {
        if (!EnsureInitialized() || !ValidateKey(key))
            return false;

        int removed = currentData.Records.RemoveAll(
            item => item.Key == key);

        if (removed > 0)
            currentData.UpdatedAtUtc = DateTime.UtcNow.ToString("O");

        return removed > 0;
    }

    public bool Save(string slot)
    {
        if (!EnsureInitialized())
            return FailSave(slot, "Save system is not initialized.");

        if (!ValidateSlot(slot))
            return FailSave(slot, "Invalid save slot.");

        try
        {
            currentData.Version = CurrentVersion;
            currentData.UpdatedAtUtc = DateTime.UtcNow.ToString("O");

            string json = JsonUtility.ToJson(currentData, true);

            if (!provider.Save(slot, json, out string error))
                return FailSave(slot, error);

            eventBus.Publish(new SaveCompletedEvent(slot));
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            return FailSave(slot, exception.Message);
        }
    }

    public bool Load(string slot)
    {
        if (!EnsureInitialized())
            return FailLoad(slot, "Save system is not initialized.");

        if (!ValidateSlot(slot))
            return FailLoad(slot, "Invalid save slot.");

        try
        {
            if (!provider.TryLoad(
                slot, out string json, out string error))
            {
                return FailLoad(slot, error);
            }

            SaveData loadedData = JsonUtility.FromJson<SaveData>(json);

            if (loadedData == null || loadedData.Records == null)
                return FailLoad(slot, "Save file is invalid.");

            if (loadedData.Version != CurrentVersion)
            {
                return FailLoad(
                    slot,
                    $"Unsupported save version: {loadedData.Version}");
            }

            var uniqueKeys = new HashSet<string>();

            foreach (SaveRecord record in loadedData.Records)
            {
                if (record == null ||
                    string.IsNullOrWhiteSpace(record.Key) ||
                    string.IsNullOrWhiteSpace(record.TypeName) ||
                    string.IsNullOrWhiteSpace(record.JsonValue) ||
                    !uniqueKeys.Add(record.Key))
                {
                    return FailLoad(
                        slot,
                        "Save file contains invalid or duplicate records.");
                }
            }

            // Replace current data only after validation succeeds.
            currentData = loadedData;

            eventBus.Publish(new LoadCompletedEvent(slot));
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            return FailLoad(slot, exception.Message);
        }
    }

    public bool Delete(string slot)
    {
        if (!EnsureInitialized())
            return false;

        if (!ValidateSlot(slot))
        {
            eventBus.Publish(
                new SaveFailedEvent(slot, "Invalid save slot."));

            return false;
        }

        if (!provider.Delete(slot, out string error))
        {
            eventBus.Publish(new SaveFailedEvent(slot, error));
            return false;
        }

        eventBus.Publish(new SaveDeletedEvent(slot));
        return true;
    }

    public bool Exists(string slot)
    {
        return EnsureInitialized() &&
               ValidateSlot(slot) &&
               provider.Exists(slot);
    }

    private bool FailSave(string slot, string reason)
    {
        eventBus.Publish(new SaveFailedEvent(slot, reason));
        return false;
    }

    private bool FailLoad(string slot, string reason)
    {
        eventBus.Publish(new LoadFailedEvent(slot, reason));
        return false;
    }

    private bool EnsureInitialized()
    {
        if (IsInitialized)
            return true;

        Debug.LogError("[SaveSystem] Not initialized.");
        return false;
    }

    private static bool ValidateKey(string key)
    {
        if (!string.IsNullOrWhiteSpace(key))
            return true;

        Debug.LogError("[SaveSystem] Data key cannot be empty.");
        return false;
    }

    private static bool ValidateSlot(string slot)
    {
        return !string.IsNullOrWhiteSpace(slot) &&
               slot != "." &&
               slot != ".." &&
               !slot.Contains("/") &&
               !slot.Contains("\\") &&
               slot.IndexOfAny(
                   System.IO.Path.GetInvalidFileNameChars()) < 0;
    }

    private static SaveData CreateEmptySave()
    {
        string now = DateTime.UtcNow.ToString("O");

        return new SaveData
        {
            Version = CurrentVersion,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            Records = new List<SaveRecord>()
        };
    }

    [Serializable]
    private sealed class SerializedValue<T>
    {
        public T Value;
    }
}