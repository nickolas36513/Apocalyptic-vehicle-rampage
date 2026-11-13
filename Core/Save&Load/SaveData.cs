using System;
using System.Collections.Generic;

[Serializable]
public sealed class SaveData
{
    public int Version = 1;
    public string CreatedAtUtc;
    public string UpdatedAtUtc;

    public List<SaveRecord> Records = new();
}

[Serializable]
public sealed class SaveRecord
{
    public string Key;
    public string TypeName;
    public string JsonValue;
}