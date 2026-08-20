using Microsoft.Win32;

namespace PowerAwake.Windows.Startup;

public sealed class WindowsStartupService
{
    private const string RunKey = "Software\\Microsoft\\Windows\\CurrentVersion\\Run";
    private const string ValueName = "powerAwake";

    public bool IsEnabled(string executablePath)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, false);
        return string.Equals(key?.GetValue(ValueName) as string, Quote(executablePath) + " --startup", StringComparison.OrdinalIgnoreCase);
    }

    public void SetEnabled(string executablePath, bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, true) ?? Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled)
        {
            key.SetValue(ValueName, Quote(executablePath) + " --startup", RegistryValueKind.String);
        }
        else if (string.Equals(key.GetValue(ValueName) as string, Quote(executablePath) + " --startup", StringComparison.OrdinalIgnoreCase))
        {
            key.DeleteValue(ValueName, false);
        }
    }

    private static string Quote(string path) => $"\"{path.Replace("\"", "\\\"")}\"";
}