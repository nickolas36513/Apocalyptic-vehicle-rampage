public interface ISaveProvider
{
    bool Save(string slot, string content, out string error);

    bool TryLoad(string slot, out string content, out string error);

    bool Exists(string slot);

    bool Delete(string slot, out string error);
}