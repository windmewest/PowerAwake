using System.IO.Pipes;
using System.Text;

namespace PowerAwake.App;

internal sealed class SingleInstanceNotification : IDisposable
{
    private const string PipeName = "PowerAwake.Settings";
    private readonly CancellationTokenSource cancellation = new();
    private readonly Action notify;

    public SingleInstanceNotification(Action notify)
    {
        this.notify = notify;
        _ = ListenAsync(cancellation.Token);
    }

    public static void NotifyExistingInstance()
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            client.Connect(500);
            var message = Encoding.UTF8.GetBytes("open-settings");
            client.Write(message, 0, message.Length);
        }
        catch (IOException)
        {
        }
        catch (TimeoutException)
        {
        }
    }

    public void Dispose()
    {
        cancellation.Cancel();
        cancellation.Dispose();
    }

    private async Task ListenAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var server = new NamedPipeServerStream(PipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                await server.WaitForConnectionAsync(cancellationToken);
                var buffer = new byte[64];
                var count = await server.ReadAsync(buffer, cancellationToken);
                if (Encoding.UTF8.GetString(buffer, 0, count) == "open-settings")
                {
                    notify();
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (IOException)
            {
            }
        }
    }
}