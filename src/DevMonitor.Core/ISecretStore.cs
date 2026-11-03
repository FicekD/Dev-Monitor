namespace DevMonitor.Core;

public interface ISecretStore
{
    string? Get(string name);
    void Set(string name, string secret);
    bool Delete(string name);
}
