namespace PowerAwake.Core.Services;

public sealed class FileAppLogger : IAppLogger
{
    private readonly string logDirectory;
    private readonly object sync = new();

    public FileAppLogger(string logDirectory)
    {
        this.logDirectory = logDirectory;
        Directory.CreateDirectory(logDirectory);
    }

    public void Info(string message) => Write("INFO", message);

    public void Error(string message, Exception? exception = null) =>
        Write("ERROR", exception is null ? message : $"{message} {exception.Message}");

    public void RemoveExpired(TimeSpan retention)
    {
        foreach (var file in Directory.EnumerateFiles(logDirectory, "powerawake-*.log"))
        {
            if (File.GetLastWriteTimeUtc(file) < DateTime.UtcNow.Subtract(retention))
            {
                File.Delete(file);
            }
        }
    }

    private void Write(string level, string message)
    {
        var path = Path.Combine(logDirectory, $"powerawake-{DateTime.UtcNow:yyyy-MM-dd}.log");
        lock (sync)
        {
            File.AppendAllText(path, $"{DateTimeOffset.UtcNow:O} [{level}] {message}{Environment.NewLine}");
        }
    }
}