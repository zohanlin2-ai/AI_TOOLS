namespace AiTerminal.Services;

public interface ITerminalSession : IAsyncDisposable
{
    event EventHandler<string>? OutputReceived;
    bool IsConnected { get; }
    string LineEnding { get; }
    Task ConnectAsync(CancellationToken cancellationToken = default);
    Task WriteRawAsync(string text, CancellationToken cancellationToken = default);
    Task SendAsync(string command, CancellationToken cancellationToken = default);
}
