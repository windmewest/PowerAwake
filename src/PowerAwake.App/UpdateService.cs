using System.Diagnostics;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

namespace PowerAwake.App;

internal sealed record UpdateInfo(Version Version, string InstallerUrl, string InstallerName, string? ChecksumUrl, string PageUrl);

/// <summary>Checks GitHub releases for a newer installer and runs it silently.</summary>
internal sealed class UpdateService
{
    private const string Repository = "windmewest/PowerAwake";

    public static Version CurrentVersion { get; } = Normalize(Assembly.GetExecutingAssembly().GetName().Version);

    private static readonly HttpClient Http = CreateClient();

    public static string ReleasesPage => $"https://github.com/{Repository}/releases";

    // Only a copy created by the installer (it has an uninstaller next to it) can be updated in place.
    public static bool IsInstalledCopy => File.Exists(Path.Combine(AppContext.BaseDirectory, "unins000.exe"));

    /// <summary>Returns the newest release when it is newer than this build; otherwise null.</summary>
    public async Task<UpdateInfo?> CheckAsync(CancellationToken cancellationToken)
    {
        using var response = await Http.GetAsync($"https://api.github.com/repos/{Repository}/releases/latest", cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = document.RootElement;

        var tag = root.GetProperty("tag_name").GetString() ?? string.Empty;
        if (!Version.TryParse(tag.TrimStart('v', 'V'), out var parsed))
        {
            return null;
        }

        var latest = Normalize(parsed);
        if (latest <= CurrentVersion)
        {
            return null;
        }

        string? installerUrl = null, installerName = null, checksumUrl = null;
        foreach (var asset in root.GetProperty("assets").EnumerateArray())
        {
            var name = asset.GetProperty("name").GetString() ?? string.Empty;
            var url = asset.GetProperty("browser_download_url").GetString();
            if (name.StartsWith("PowerAwake-Setup-", StringComparison.OrdinalIgnoreCase) && name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                installerUrl = url;
                installerName = name;
            }
            else if (name.Equals("SHA256SUMS.txt", StringComparison.OrdinalIgnoreCase))
            {
                checksumUrl = url;
            }
        }

        var page = root.TryGetProperty("html_url", out var html) ? html.GetString() ?? ReleasesPage : ReleasesPage;
        return installerUrl is null || installerName is null ? null : new UpdateInfo(latest, installerUrl, installerName, checksumUrl, page);
    }

    /// <summary>Downloads and verifies the installer, then starts it silently. The caller must exit the app afterwards.</summary>
    public async Task DownloadAndStartAsync(UpdateInfo update, IProgress<int> progress, CancellationToken cancellationToken)
    {
        var directory = Path.Combine(Path.GetTempPath(), "PowerAwake-Update");
        Directory.CreateDirectory(directory);
        var target = Path.Combine(directory, update.InstallerName);

        using (var response = await Http.GetAsync(update.InstallerUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
        {
            response.EnsureSuccessStatusCode();
            var total = response.Content.Headers.ContentLength ?? 0;
            await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var file = File.Create(target);
            var buffer = new byte[81920];
            long read = 0;
            int count;
            while ((count = await source.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await file.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
                read += count;
                if (total > 0)
                {
                    progress.Report((int)(read * 100 / total));
                }
            }
        }

        if (update.ChecksumUrl is not null)
        {
            var sums = await Http.GetStringAsync(update.ChecksumUrl, cancellationToken);
            var expected = sums.Split('\n')
                .Select(line => line.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries))
                .FirstOrDefault(parts => parts.Length == 2 && parts[1].Trim().Equals(update.InstallerName, StringComparison.OrdinalIgnoreCase))?[0];
            await using var check = File.OpenRead(target);
            var actual = Convert.ToHexString(await SHA256.HashDataAsync(check, cancellationToken));
            if (expected is null || !actual.Equals(expected, StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(target);
                throw new InvalidOperationException("Checksum mismatch");
            }
        }

        Process.Start(new ProcessStartInfo(target, "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS") { UseShellExecute = true });
    }

    private static Version Normalize(Version? version) =>
        new(Math.Max(0, version?.Major ?? 0), Math.Max(0, version?.Minor ?? 0), Math.Max(0, version?.Build ?? 0));

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("PowerAwake", CurrentVersion.ToString()));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return client;
    }
}
