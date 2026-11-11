using System.Text.Json;
using System.Text.Json.Serialization;

namespace TripPlanner;

/// <summary>Persistence contract so services can be tested against an in-memory store.</summary>
public interface IStore
{
    TripData Load();
    void Save(TripData data);
}

/// <summary>
/// JSON-file store. Writes are atomic: the new content goes to a temp file in
/// the same directory and is then moved over the old file, so a crash mid-save
/// can never leave a half-written database behind.
/// </summary>
public sealed class JsonFileStore : IStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _path;

    public JsonFileStore(string path) => _path = path;

    public TripData Load()
    {
        if (!File.Exists(_path))
            return new TripData();

        var json = File.ReadAllText(_path);
        return JsonSerializer.Deserialize<TripData>(json, Options) ?? new TripData();
    }

    public void Save(TripData data)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(_path))!;
        Directory.CreateDirectory(dir);
        var temp = Path.Combine(dir, "." + Path.GetFileName(_path) + ".tmp");
        File.WriteAllText(temp, JsonSerializer.Serialize(data, Options));
        File.Move(temp, _path, overwrite: true);
    }
}

/// <summary>In-memory store for tests — no disk involved.</summary>
public sealed class InMemoryStore : IStore
{
    private TripData _data = new();
    public TripData Load() => _data;
    public void Save(TripData data) => _data = data;
}
