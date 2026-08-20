using PowerAwake.Core.Models;

namespace PowerAwake.Core.Services;

public sealed class SettingsService
{
    private readonly JsonFileStore<AppSettings> store;

    public SettingsService(string path)
    {
        store = new JsonFileStore<AppSettings>(path);
    }

    public AppSettings Load() => store.Load() ?? new AppSettings();

    public void Save(AppSettings settings)
    {
        var errors = SettingsValidation.Validate(settings);
        if (errors.Count > 0)
        {
            throw new ArgumentException(string.Join(Environment.NewLine, errors), nameof(settings));
        }

        store.Save(settings);
    }
}