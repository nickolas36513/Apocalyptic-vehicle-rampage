using System;
using System.IO;
using UnityEngine;

public sealed class JsonSaveProvider : ISaveProvider
{
    private readonly string directoryPath;

    public JsonSaveProvider(string directoryName = "Saves")
    {
        if (string.IsNullOrWhiteSpace(directoryName) ||
            directoryName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            directoryName.Contains("/") ||
            directoryName.Contains("\\") ||
            directoryName == "." ||
            directoryName == "..")
        {
            throw new ArgumentException(
                "Invalid save directory name.",
                nameof(directoryName));
        }

        directoryPath = Path.Combine(
            Application.persistentDataPath,
            directoryName);
    }

    public bool Save(
        string slot,
        string content,
        out string error)
    {
        error = null;

        if (!IsValidSlot(slot, out error))
            return false;

        if (content == null)
        {
            error = "Save content cannot be null.";
            return false;
        }

        string path = GetPath(slot);
        string temporaryPath = path + ".tmp";

        try
        {
            Directory.CreateDirectory(directoryPath);

            File.WriteAllText(temporaryPath, content);

            if (File.Exists(path))
            {
                string backupPath = path + ".bak";

                if (File.Exists(backupPath))
                    File.Delete(backupPath);

                File.Replace(temporaryPath, path, backupPath);

                if (File.Exists(backupPath))
                    File.Delete(backupPath);
            }
            else
            {
                File.Move(temporaryPath, path);
            }

            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            Debug.LogException(exception);
            return false;
        }
        finally
        {
            try
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
            catch (Exception cleanupException)
            {
                Debug.LogWarning(
                    $"[SaveProvider] Temporary file cleanup failed: " +
                    cleanupException.Message);
            }
        }
    }

    public bool TryLoad(
        string slot,
        out string content,
        out string error)
    {
        content = null;
        error = null;

        if (!IsValidSlot(slot, out error))
            return false;

        string path = GetPath(slot);

        try
        {
            if (!File.Exists(path))
            {
                error = $"Save slot '{slot}' does not exist.";
                return false;
            }

            content = File.ReadAllText(path);
            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            Debug.LogException(exception);
            return false;
        }
    }

    public bool Exists(string slot)
    {
        if (!IsValidSlot(slot, out _))
            return false;

        return File.Exists(GetPath(slot));
    }

    public bool Delete(string slot, out string error)
    {
        error = null;

        if (!IsValidSlot(slot, out error))
            return false;

        string path = GetPath(slot);

        try
        {
            if (File.Exists(path))
                File.Delete(path);

            string backupPath = path + ".bak";

            if (File.Exists(backupPath))
                File.Delete(backupPath);

            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            Debug.LogException(exception);
            return false;
        }
    }

    private string GetPath(string slot)
    {
        return Path.Combine(directoryPath, slot + ".json");
    }

    private static bool IsValidSlot(
        string slot,
        out string error)
    {
        error = null;

        if (string.IsNullOrWhiteSpace(slot) ||
            slot == "." ||
            slot == ".." ||
            slot.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            slot.Contains("/") ||
            slot.Contains("\\"))
        {
            error = "Invalid save slot name.";
            return false;
        }

        return true;
    }
}