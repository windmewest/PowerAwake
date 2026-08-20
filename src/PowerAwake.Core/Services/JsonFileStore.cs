using System.Text.Json;

namespace PowerAwake.Core.Services;

public sealed class JsonFileStore<T>
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string path;

    public JsonFileStore(string path)
    {
        this.path = path;
    }

    public T? Load()
    {
        if (!File.Exists(path))
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(File.ReadAllText(path), SerializerOptions);
    }

    public void Save(T value)
    {
        var directory = Path.GetDirectoryName(path) ?? throw new InvalidOperationException("恢复文件路径无效。");
        Directory.CreateDirectory(directory);
        var temporaryPath = path + ".tmp";
        var json = JsonSerializer.Serialize(value, SerializerOptions);

        using (var stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
        using (var writer = new StreamWriter(stream))
        {
            writer.Write(json);
            writer.Flush();
            stream.Flush(true);
        }

        File.Move(temporaryPath, path, true);
    }

    public void Delete()
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}