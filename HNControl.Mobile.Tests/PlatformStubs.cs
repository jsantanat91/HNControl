// In-memory replacements for device storage. Production services are linked unchanged.
public static class Preferences
{
    private static readonly Dictionary<string, object> Values = new();
    public static T Get<T>(string key, T fallback) => Values.TryGetValue(key, out var value) ? (T)value : fallback;
    public static void Set<T>(string key, T value) where T : notnull => Values[key] = value;
    public static void Remove(string key) => Values.Remove(key);
}
public sealed class SecureStorage
{
    public static SecureStorage Default { get; } = new();
    private readonly Dictionary<string, string> _values = new();
    public Task<string?> GetAsync(string key) => Task.FromResult(_values.GetValueOrDefault(key));
    public Task SetAsync(string key, string value) { _values[key] = value; return Task.CompletedTask; }
    public bool Remove(string key) => _values.Remove(key);
}
