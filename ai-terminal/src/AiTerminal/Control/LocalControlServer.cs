using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.IO;

namespace AiTerminal.Control;

public sealed class LocalControlServer : IAsyncDisposable
{
    private readonly Func<ControlRequest, Task<ControlResponse>> _handler;
    private readonly CancellationTokenSource _cancellation = new();
    private Task? _listener;

    public LocalControlServer(Func<ControlRequest, Task<ControlResponse>> handler) => _handler = handler;

    public void Start() => _listener = ListenAsync(_cancellation.Token);

    private async Task ListenAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                var pipe = new NamedPipeServerStream(ControlPipe.Name, PipeDirection.InOut, 8, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                await pipe.WaitForConnectionAsync(token);
                _ = HandleClientAsync(pipe, token);
            }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task HandleClientAsync(NamedPipeServerStream pipe, CancellationToken token)
    {
        await using (pipe)
        {
        using var reader = new StreamReader(pipe, Encoding.UTF8, leaveOpen: true);
        await using var writer = new StreamWriter(pipe, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };
        try
        {
            var line = await reader.ReadLineAsync(token);
            var request = line is null ? null : JsonSerializer.Deserialize<ControlRequest>(line);
            var response = request is null
                ? new ControlResponse(false, "Invalid control request.")
                : await _handler(request);
            await writer.WriteLineAsync(JsonSerializer.Serialize(response));
        }
        catch (Exception ex)
        {
            await writer.WriteLineAsync(JsonSerializer.Serialize(new ControlResponse(false, ex.Message)));
        }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _cancellation.Cancel();
        if (_listener is not null) await _listener;
        _cancellation.Dispose();
    }
}
