namespace PowerAwake.App;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        using var mutex = new Mutex(true, "Local\\PowerAwake", out var isFirstInstance);
        if (!isFirstInstance)
        {
            SingleInstanceNotification.NotifyExistingInstance();
            return;
        }

        Application.Run(new TrayApplicationContext());
    }
}